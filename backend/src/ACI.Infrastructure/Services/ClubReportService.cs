using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>The club's figures, summed by the database (see <see cref="IClubReports"/>).</summary>
public sealed class ClubReportService : IClubReports
{
    /// <summary>
    /// Points that make one unit of currency when members redeem them: the rate the Loyalty
    /// page has always stated (100 points = 1). The setting that once claimed to change it was
    /// never read by anything and is retired (ClubSettings.Retired).
    /// </summary>
    public const int PointsPerUnit = 100;

    public const string Day = "day";
    public const string Week = "week";
    public const string Month = "month";

    /// <summary>A report covers at most three years; one by day at most this many days.</summary>
    private const int MaxDays = 3 * 366 + 1;
    private const int MaxPeriods = 400;

    private const int ReservationsShown = 8;
    private const int TabsShown = 8;
    private const int ApplicationsShown = 6;
    private const int PastDueShown = 4;
    private const int EventsShown = 6;
    private const int AnniversaryDays = 14;

    private readonly AppDbContext _db;

    public ClubReportService(AppDbContext db) => _db = db;

    // ── the report ───────────────────────────────────────────────────────────

    public async Task<Result<ClubReportDto>> ReportAsync(Guid organizationId, ClubReportQuery query, CancellationToken ct = default)
    {
        var org = await WorkspaceAsync(organizationId, ct);
        if (org == null) return DomainErrors.Organization.NotFound;
        var zone = RegionalSettings.ZoneOf(org.TimeZone);
        var now = DateTime.UtcNow;
        var today = RegionalSettings.TodayIn(zone, now);

        // Left out: the last twelve months, by month, on the workspace's calendar.
        var fromUtc = query.FromUtc is DateTime from ? Utc(from) : RegionalSettings.StartOfDayUtc(new DateOnly(today.Year, today.Month, 1).AddMonths(-11), zone);
        var toUtc = query.ToUtc is DateTime to ? Utc(to) : now;
        if (fromUtc >= toUtc || (toUtc - fromUtc).TotalDays > MaxDays) return DomainErrors.ClubReports.PeriodInvalid;

        var grain = (query.Grain ?? "").Trim().ToLowerInvariant();
        if (grain.Length == 0) grain = (toUtc - fromUtc).TotalDays <= 62 ? Day : (toUtc - fromUtc).TotalDays <= 26 * 7 ? Week : Month;
        if (grain is not (Day or Week or Month)) return DomainErrors.ClubReports.GrainInvalid;
        var periods = Periods(fromUtc, toUtc, grain, zone);
        if (periods.Count > MaxPeriods) return DomainErrors.ClubReports.TooManyPeriods;

        // What stands at a moment is read as the books stood at the end of the period, or now
        // for a period that is not over.
        var asOf = toUtc < now ? toUtc : now;

        var cash = await CashAsync(organizationId, fromUtc, toUtc, periods, org.Currency, ct);
        var (accrual, byKind) = await AccrualAsync(organizationId, fromUtc, toUtc, periods, org.Currency, ct);
        var receivables = Ordered(await ReceivablesAsync(organizationId, asOf, ct), org.Currency, a => a.Currency);
        var giftCards = Ordered(await GiftCardsAsync(organizationId, asOf, ct), org.Currency, g => g.Currency);
        var points = await PointsAsync(organizationId, asOf, org.Currency, ct);
        var mrr = Ordered(await MrrAsync(organizationId, ct), org.Currency, m => m.Currency);
        var events = await EventRatesAsync(organizationId, fromUtc, toUtc, now, ct);

        return new ClubReportDto(org.Currency, zone.Id, fromUtc, toUtc, asOf, grain, periods,
            cash, accrual, byKind, receivables, giftCards, points, mrr, events);
    }

    /// <summary>
    /// The days, weeks (Monday to Sunday) or months the period spans on the workspace's
    /// calendar; the first and the last are cut to the period.
    /// </summary>
    public static List<ClubReportPeriodDto> Periods(DateTime fromUtc, DateTime toUtc, string grain, TimeZoneInfo zone)
    {
        var first = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(fromUtc, zone));
        var cursor = grain switch
        {
            Month => new DateOnly(first.Year, first.Month, 1),
            Week => first.AddDays(-(((int)first.DayOfWeek + 6) % 7)),
            _ => first,
        };
        var periods = new List<ClubReportPeriodDto>();
        while (periods.Count <= MaxPeriods)
        {
            var next = grain switch { Month => cursor.AddMonths(1), Week => cursor.AddDays(7), _ => cursor.AddDays(1) };
            var startUtc = RegionalSettings.StartOfDayUtc(cursor, zone);
            var endUtc = RegionalSettings.StartOfDayUtc(next, zone);
            if (startUtc >= toUtc) break;
            if (endUtc > fromUtc)
                periods.Add(new ClubReportPeriodDto(cursor.ToString("yyyy-MM-dd"), startUtc < fromUtc ? fromUtc : startUtc, endUtc > toUtc ? toUtc : endUtc));
            cursor = next;
        }
        return periods;
    }

    /// <summary>
    /// Money received from members, and money paid back to them: refunds, and payments taken
    /// back whole (a cheque that bounced). A credit brought over from before the ledger, or one
    /// set by hand, is not money that came in, and nor is taking back such a credit.
    /// </summary>
    private async Task<List<ClubMoneyFlowDto>> CashAsync(Guid organizationId, DateTime fromUtc, DateTime toUtc,
        IReadOnlyList<ClubReportPeriodDto> periods, string workspaceCurrency, CancellationToken ct)
    {
        var rows = await _db.ClubPayments.AsNoTracking()
            .Where(p => p.OrganizationId == organizationId && p.ReceivedAtUtc >= fromUtc && p.ReceivedAtUtc < toUtc
                && (p.EntryType == ClubEntryTypes.Payment || p.EntryType == ClubEntryTypes.Refund
                    || (p.EntryType == ClubEntryTypes.Void
                        && _db.ClubPayments.Any(o => o.Id == p.ReversesPaymentId && o.EntryType == ClubEntryTypes.Payment))))
            .GroupBy(p => new { p.Currency, Day = p.ReceivedAtUtc.Date, p.ReceivedAtUtc.Hour })
            .Select(g => new HourAmounts(g.Key.Currency, g.Key.Day, g.Key.Hour,
                g.Sum(p => p.Amount > 0 ? p.Amount : 0m), g.Sum(p => p.Amount < 0 ? -p.Amount : 0m)))
            .ToListAsync(ct);
        return Flows(rows, periods, workspaceCurrency);
    }

    /// <summary>
    /// What was charged, and charges voided: the sales a period made, whether paid or not. An
    /// opening balance or a balance corrected by hand is owed but is not a sale, and voiding
    /// one takes back no sale. A void counts in its own period, so a period already reported
    /// never changes.
    /// </summary>
    private async Task<(List<ClubMoneyFlowDto> Flows, List<ClubKindRevenueDto> ByKind)> AccrualAsync(Guid organizationId, DateTime fromUtc, DateTime toUtc,
        IReadOnlyList<ClubReportPeriodDto> periods, string workspaceCurrency, CancellationToken ct)
    {
        var sales = _db.ClubCharges.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && c.PostedAtUtc >= fromUtc && c.PostedAtUtc < toUtc
                && (c.EntryType == ClubEntryTypes.Charge
                    || (c.EntryType == ClubEntryTypes.Void
                        && _db.ClubCharges.Any(o => o.Id == c.ReversesChargeId && o.EntryType == ClubEntryTypes.Charge))));

        var rows = await sales
            .GroupBy(c => new { c.Currency, Day = c.PostedAtUtc.Date, c.PostedAtUtc.Hour })
            .Select(g => new HourAmounts(g.Key.Currency, g.Key.Day, g.Key.Hour,
                g.Sum(c => c.Amount > 0 ? c.Amount : 0m), g.Sum(c => c.Amount < 0 ? -c.Amount : 0m)))
            .ToListAsync(ct);

        var kinds = await sales
            .GroupBy(c => new { c.Currency, c.Kind })
            .Select(g => new
            {
                g.Key.Currency,
                g.Key.Kind,
                Charged = g.Sum(c => c.Amount > 0 ? c.Amount : 0m),
                Voided = g.Sum(c => c.Amount < 0 ? -c.Amount : 0m),
            })
            .ToListAsync(ct);
        var byKind = kinds
            .Select(k => new ClubKindRevenueDto(k.Currency, k.Kind, k.Charged, k.Voided, k.Charged - k.Voided))
            .OrderBy(k => k.Currency == workspaceCurrency ? 0 : 1).ThenBy(k => k.Currency)
            .ThenByDescending(k => k.Net).ThenBy(k => k.Kind, StringComparer.Ordinal)
            .ToList();
        return (Flows(rows, periods, workspaceCurrency), byKind);
    }

    private sealed record HourAmounts(string Currency, DateTime Day, int Hour, decimal In, decimal Out);

    /// <summary>
    /// The database sums each currency by the UTC hour; each hour then falls in the period it
    /// starts in. The workspace's days begin on a whole UTC hour wherever its offset is whole
    /// hours (all of Europe), so this is exact there; in a zone half an hour off, the half hour
    /// around midnight counts in the day next to it.
    /// </summary>
    private static List<ClubMoneyFlowDto> Flows(IReadOnlyList<HourAmounts> rows, IReadOnlyList<ClubReportPeriodDto> periods, string workspaceCurrency)
    {
        var starts = periods.Select(p => p.StartUtc).ToArray();
        return rows.GroupBy(r => r.Currency)
            .Select(currency =>
            {
                var ins = new decimal[periods.Count];
                var outs = new decimal[periods.Count];
                foreach (var row in currency)
                {
                    var at = DateTime.SpecifyKind(row.Day, DateTimeKind.Utc).AddHours(row.Hour);
                    var i = Array.BinarySearch(starts, at);
                    if (i < 0) i = ~i - 1;
                    i = Math.Clamp(i, 0, periods.Count - 1);
                    ins[i] += row.In;
                    outs[i] += row.Out;
                }
                var byPeriod = ins.Select((amount, i) => new ClubPeriodAmountDto(amount, outs[i], amount - outs[i])).ToList();
                var totalIn = ins.Sum();
                var totalOut = outs.Sum();
                return new ClubMoneyFlowDto(currency.Key, totalIn, totalOut, totalIn - totalOut, byPeriod);
            })
            .OrderBy(f => f.Currency == workspaceCurrency ? 0 : 1).ThenBy(f => f.Currency)
            .ToList();
    }

    /// <summary>
    /// What members owed at <paramref name="asOfUtc"/>, by how many days past its due date each
    /// charge was then (from the day it was posted when it has no due date): the charges then
    /// posted and not yet voided, less what had been allocated to them by then.
    /// </summary>
    private async Task<List<ClubAgeingDto>> ReceivablesAsync(Guid organizationId, DateTime asOfUtc, CancellationToken ct)
    {
        var open = await OpenChargesAsync(organizationId, asOfUtc)
            .Select(c => new { c.Currency, Due = c.DueAtUtc ?? c.PostedAtUtc, c.Owed })
            .ToListAsync(ct);
        return open.GroupBy(c => c.Currency).Select(g =>
        {
            decimal current = 0, d30 = 0, d60 = 0, d90 = 0, over = 0;
            foreach (var c in g)
            {
                var late = (asOfUtc - c.Due).TotalDays;
                if (late <= 0) current += c.Owed;
                else if (late <= 30) d30 += c.Owed;
                else if (late <= 60) d60 += c.Owed;
                else if (late <= 90) d90 += c.Owed;
                else over += c.Owed;
            }
            return new ClubAgeingDto(g.Key, current, d30, d60, d90, over, current + d30 + d60 + d90 + over, g.Count());
        }).ToList();
    }

    private sealed class OpenCharge
    {
        public Guid Id { get; init; }
        public Guid MemberId { get; init; }
        public string MemberName { get; init; } = "";
        public string Description { get; init; } = "";
        public string Currency { get; init; } = "";
        public DateTime PostedAtUtc { get; init; }
        public DateTime? DueAtUtc { get; init; }
        public decimal Owed { get; init; }
    }

    /// <summary>
    /// The charges with something still owed at <paramref name="asOfUtc"/>: posted by then, not
    /// voided by then, less what had been allocated to them by then (a payment's share, a
    /// credit applied, or a refund's or a void's taking back). Opening balances and balances set
    /// by hand are owed like charges, so they count here.
    /// </summary>
    private IQueryable<OpenCharge> OpenChargesAsync(Guid organizationId, DateTime asOfUtc) =>
        _db.ClubCharges.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && c.ReversesChargeId == null && c.EntryType != ClubEntryTypes.Void
                && c.PostedAtUtc < asOfUtc
                && !_db.ClubCharges.Any(v => v.ReversesChargeId == c.Id && v.PostedAtUtc < asOfUtc))
            .Select(c => new OpenCharge
            {
                Id = c.Id,
                MemberId = c.MemberId,
                MemberName = c.MemberName,
                Description = c.Description,
                Currency = c.Currency,
                PostedAtUtc = c.PostedAtUtc,
                DueAtUtc = c.DueAtUtc,
                Owed = c.Amount - (_db.ClubPaymentAllocations
                    .Where(a => a.ChargeId == c.Id && a.CreatedAtUtc < asOfUtc)
                    .Sum(a => (decimal?)a.Amount) ?? 0m),
            })
            .Where(c => c.Owed > 0);

    /// <summary>
    /// What was left on gift cards at <paramref name="asOfUtc"/>: the club's debt to whoever holds
    /// them. A card past its expiry owes nothing, whether or not the job that writes off what is
    /// left on expired cards has reached it yet.
    /// </summary>
    private async Task<List<ClubGiftCardsOutstandingDto>> GiftCardsAsync(Guid organizationId, DateTime asOfUtc, CancellationToken ct)
    {
        var cards = await _db.ClubGiftCardTransactions.AsNoTracking()
            .Where(t => t.OrganizationId == organizationId && t.AtUtc < asOfUtc
                && !_db.ClubGiftCards.Any(c => c.Id == t.GiftCardId && c.ExpiresAtUtc != null && c.ExpiresAtUtc <= asOfUtc))
            .GroupBy(t => new { t.GiftCardId, t.Currency })
            .Select(g => new { g.Key.Currency, Left = g.Sum(t => t.Amount) })
            .Where(c => c.Left > 0)
            .ToListAsync(ct);
        return cards.GroupBy(c => c.Currency)
            .Select(g => new ClubGiftCardsOutstandingDto(g.Key, g.Sum(c => c.Left), g.Count()))
            .ToList();
    }

    /// <summary>The loyalty points members held at <paramref name="asOfUtc"/>, and what they are worth at the redemption rate.</summary>
    private async Task<ClubPointsOutstandingDto> PointsAsync(Guid organizationId, DateTime asOfUtc, string currency, CancellationToken ct)
    {
        // Per member, so a member taken below zero by hand does not cancel out another's points.
        var held = await _db.LoyaltyLedger.AsNoTracking()
            .Where(e => e.OrganizationId == organizationId && e.CreatedAtUtc < asOfUtc)
            .GroupBy(e => e.MemberId)
            .Select(g => new { Points = g.Sum(e => e.Points) })
            .Where(m => m.Points > 0)
            .ToListAsync(ct);
        var points = held.Sum(m => m.Points);
        return new ClubPointsOutstandingDto(points, held.Count, PointsPerUnit, Math.Round(points / (decimal)PointsPerUnit, 2), currency);
    }

    /// <summary>
    /// What the subscriptions running now (active, or past due and still billed) bring in each
    /// month. A subscription's history is not kept, so this is always today's.
    /// </summary>
    private async Task<List<ClubMrrDto>> MrrAsync(Guid organizationId, CancellationToken ct)
    {
        var running = await _db.ClubSubscriptions.AsNoTracking()
            .Where(s => s.OrganizationId == organizationId && (s.Status == "Active" || s.Status == "PastDue"))
            .GroupBy(s => new { s.Currency, s.Cadence })
            .Select(g => new { g.Key.Currency, g.Key.Cadence, Sum = g.Sum(s => s.Amount), Count = g.Count() })
            .ToListAsync(ct);
        return running
            .Select(r => (r.Currency, Months: MonthsIn(r.Cadence), r.Sum, r.Count))
            .Where(r => r.Months > 0)
            .GroupBy(r => r.Currency)
            .Select(g =>
            {
                var monthly = Math.Round(g.Sum(r => r.Sum / r.Months), 2);
                return new ClubMrrDto(g.Key, monthly, monthly * 12, g.Sum(r => r.Count));
            })
            .ToList();
    }

    private static int MonthsIn(string cadence) => cadence switch
    {
        "Monthly" => 1,
        "Quarterly" => 3,
        "SemiAnnual" => 6,
        "Annual" => 12,
        _ => 0,
    };

    /// <summary>
    /// The period's events that took place or will: how full the ones with a seat limit were, and
    /// of those over, how many who had a seat did not come.
    /// </summary>
    private async Task<ClubEventRatesDto> EventRatesAsync(Guid organizationId, DateTime fromUtc, DateTime toUtc, DateTime now, CancellationToken ct)
    {
        var inPeriod = _db.ClubEvents.AsNoTracking()
            .Where(e => e.OrganizationId == organizationId && e.StartAtUtc >= fromUtc && e.StartAtUtc < toUtc
                && (e.Status == ClubEventStatuses.Published || e.Status == ClubEventStatuses.Completed));
        var events = await inPeriod.Select(e => new { e.Id, e.Capacity, e.UnnamedSeats, e.EndAtUtc }).ToListAsync(ct);
        var counts = await _db.ClubEventRegistrations.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId && inPeriod.Any(e => e.Id == r.EventId))
            .GroupBy(r => new { r.EventId, r.Status })
            .Select(g => new { g.Key.EventId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);
        int Count(Guid eventId, string status) => counts.Where(c => c.EventId == eventId && c.Status == status).Sum(c => c.Count);

        int limited = 0, capacity = 0, taken = 0, ended = 0, checkedIn = 0, noShows = 0, notMarked = 0;
        foreach (var e in events)
        {
            if (e.Capacity > 0)
            {
                limited++;
                capacity += e.Capacity;
                // A no-show had a seat as much as someone who came.
                taken += Count(e.Id, RegistrationStatuses.Reserved) + Count(e.Id, RegistrationStatuses.CheckedIn)
                    + Count(e.Id, RegistrationStatuses.NoShow) + e.UnnamedSeats;
            }
            if (e.EndAtUtc < now)
            {
                ended++;
                checkedIn += Count(e.Id, RegistrationStatuses.CheckedIn);
                noShows += Count(e.Id, RegistrationStatuses.NoShow);
                notMarked += Count(e.Id, RegistrationStatuses.Reserved);
            }
        }
        decimal? fill = capacity > 0 ? Math.Round(taken / (decimal)capacity, 4) : null;
        decimal? noShowRate = checkedIn + noShows > 0 ? Math.Round(noShows / (decimal)(checkedIn + noShows), 4) : null;
        return new ClubEventRatesDto(events.Count, limited, capacity, taken, fill, ended, checkedIn, noShows, notMarked, noShowRate);
    }

    // ── today ────────────────────────────────────────────────────────────────

    public async Task<Result<ClubTodayDto>> TodayAsync(Guid organizationId, CancellationToken ct = default)
    {
        var org = await WorkspaceAsync(organizationId, ct);
        if (org == null) return DomainErrors.Organization.NotFound;
        var zone = RegionalSettings.ZoneOf(org.TimeZone);
        var now = DateTime.UtcNow;
        var today = RegionalSettings.TodayIn(zone, now);
        var dayStart = RegionalSettings.StartOfDayUtc(today, zone);
        var dayEnd = RegionalSettings.StartOfDayUtc(today.AddDays(1), zone);

        var checkIns = await _db.ClubVisits.AsNoTracking()
            .CountAsync(v => v.OrganizationId == organizationId && v.VisitedAtUtc >= dayStart && v.VisitedAtUtc < dayEnd, ct);

        var bookings = _db.ClubReservations.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId && r.StartAtUtc >= dayStart && r.StartAtUtc < dayEnd
                && r.Status != ReservationStatuses.Cancelled);
        var reservations = new ClubTodayListDto<ClubTodayReservationDto>(
            await bookings.CountAsync(ct),
            await bookings.OrderBy(r => r.StartAtUtc).Take(ReservationsShown)
                .Select(r => new ClubTodayReservationDto(r.Id, r.MemberName, r.ResourceName, r.PartySize, r.StartAtUtc, r.Status))
                .ToListAsync(ct));

        var openTabs = _db.ClubTabs.AsNoTracking().Where(t => t.OrganizationId == organizationId && t.Status == "Open");
        var tabTotals = await openTabs.GroupBy(t => t.Currency)
            .Select(g => new ClubCurrencyAmountDto(g.Key, g.Sum(t => t.Total))).ToListAsync(ct);
        var tabs = new ClubTodayTabsDto(
            await openTabs.CountAsync(ct),
            Ordered(tabTotals, org.Currency, t => t.Currency),
            await openTabs.OrderBy(t => t.OpenedAtUtc).Take(TabsShown)
                .Select(t => new ClubTodayTabDto(t.Id, t.MemberName, t.Venue,
                    _db.ClubTabItems.Count(i => i.TabId == t.Id && i.RemovedAtUtc == null), t.Total, t.Currency))
                .ToListAsync(ct));

        var waiting = _db.ClubApplications.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId
                && (a.Status == ApplicationStatuses.Submitted || a.Status == ApplicationStatuses.UnderReview));
        var applications = new ClubTodayListDto<ClubTodayApplicationDto>(
            await waiting.CountAsync(ct),
            await waiting.OrderBy(a => a.SubmittedAtUtc).Take(ApplicationsShown)
                .Select(a => new ClubTodayApplicationDto(a.Id, a.FirstName, a.LastName, a.RequestedTier, a.Occupation, a.Status))
                .ToListAsync(ct));

        // Past due as the money pages say it: a due date gone by with something still owed.
        var overdue = await OpenChargesAsync(organizationId, now)
            .Where(c => c.DueAtUtc != null && c.DueAtUtc < now)
            .Select(c => new { c.Id, c.MemberName, c.Description, c.Owed, c.Currency, Due = c.DueAtUtc!.Value })
            .ToListAsync(ct);
        var pastDueSubs = _db.ClubSubscriptions.AsNoTracking().Where(s => s.OrganizationId == organizationId && s.Status == "PastDue");
        var pastDue = new ClubTodayPastDueDto(
            overdue.Count,
            Ordered(overdue.GroupBy(c => c.Currency).Select(g => new ClubCurrencyAmountDto(g.Key, g.Sum(c => c.Owed))).ToList(), org.Currency, a => a.Currency),
            overdue.OrderBy(c => c.Due).Take(PastDueShown)
                .Select(c => new ClubTodayOverdueChargeDto(c.Id, c.MemberName, c.Description, c.Owed, c.Currency, c.Due)).ToList(),
            await pastDueSubs.CountAsync(ct),
            await pastDueSubs.OrderBy(s => s.NextChargeAtUtc).Take(PastDueShown)
                .Select(s => new ClubTodaySubscriptionDto(s.Id, s.MemberName, s.Name, s.Amount, s.Currency))
                .ToListAsync(ct));

        var weekEnd = now.AddDays(7);
        var coming = _db.ClubEvents.AsNoTracking()
            .Where(e => e.OrganizationId == organizationId && e.Status == ClubEventStatuses.Published && e.StartAtUtc >= now && e.StartAtUtc < weekEnd);
        var events = new ClubTodayListDto<ClubTodayEventDto>(
            await coming.CountAsync(ct),
            await coming.OrderBy(e => e.StartAtUtc).Take(EventsShown)
                .Select(e => new ClubTodayEventDto(e.Id, e.Name, e.CoverEmoji, e.StartAtUtc, e.Capacity,
                    _db.ClubEventRegistrations.Count(r => r.EventId == e.Id
                        && (r.Status == RegistrationStatuses.Reserved || r.Status == RegistrationStatuses.CheckedIn)) + e.UnnamedSeats))
                .ToListAsync(ct));

        var anniversaries = await AnniversariesAsync(organizationId, today, zone, ct);

        return new ClubTodayDto(today.ToString("yyyy-MM-dd"), zone.Id, org.Currency, checkIns, reservations, tabs, applications,
            pastDue, events, anniversaries);
    }

    /// <summary>
    /// Active members whose joining day comes round in the next fortnight (today included), a year
    /// or more after they joined, on the workspace's calendar. Late in December that reaches into
    /// January; someone who joined on 29 February has theirs on the 28th in other years.
    /// </summary>
    private async Task<List<ClubTodayAnniversaryDto>> AnniversariesAsync(Guid organizationId, DateOnly today, TimeZoneInfo zone, CancellationToken ct)
    {
        var members = await _db.ClubMembers.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.Status == "Active")
            .Select(m => new { m.Id, m.FirstName, m.LastName, m.Tier, m.JoinedAtUtc })
            .ToListAsync(ct);
        var last = today.AddDays(AnniversaryDays - 1);
        var found = new List<(ClubTodayAnniversaryDto Row, DateOnly On)>();
        foreach (var m in members)
        {
            var joined = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(m.JoinedAtUtc, DateTimeKind.Utc), zone));
            foreach (var year in new[] { today.Year, today.Year + 1 })
            {
                var on = new DateOnly(year, joined.Month, Math.Min(joined.Day, DateTime.DaysInMonth(year, joined.Month)));
                if (on < today || on > last) continue;
                var years = year - joined.Year;
                if (years >= 1)
                    found.Add((new ClubTodayAnniversaryDto(m.Id, m.FirstName, m.LastName, m.Tier, on.ToString("yyyy-MM-dd"), years), on));
                break;
            }
        }
        return found.OrderBy(f => f.On).ThenBy(f => f.Row.LastName, StringComparer.OrdinalIgnoreCase).Select(f => f.Row).ToList();
    }

    // ── shared ───────────────────────────────────────────────────────────────

    private sealed record Workspace(string Currency, string TimeZone);

    private async Task<Workspace?> WorkspaceAsync(Guid organizationId, CancellationToken ct)
    {
        var org = await _db.Organizations.AsNoTracking()
            .Where(o => o.Id == organizationId)
            .Select(o => new { o.Currency, o.TimeZone })
            .FirstOrDefaultAsync(ct);
        return org == null ? null
            : new Workspace(string.IsNullOrWhiteSpace(org.Currency) ? "CHF" : org.Currency, org.TimeZone);
    }

    /// <summary>The workspace's own currency first, then the others by code.</summary>
    private static List<T> Ordered<T>(IEnumerable<T> rows, string workspaceCurrency, Func<T, string> currencyOf) =>
        rows.OrderBy(r => currencyOf(r) == workspaceCurrency ? 0 : 1).ThenBy(currencyOf, StringComparer.Ordinal).ToList();

    private static DateTime Utc(DateTime at) => at.Kind switch
    {
        DateTimeKind.Utc => at,
        DateTimeKind.Local => at.ToUniversalTime(),
        _ => DateTime.SpecifyKind(at, DateTimeKind.Utc),
    };
}
