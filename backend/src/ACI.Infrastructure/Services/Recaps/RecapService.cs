using ACI.Application.Common;
using ACI.Application.Common.I18n;
using ACI.Application.Configuration;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.Infrastructure.Services.Retention;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Infrastructure.Services.Recaps;

/// <inheritdoc cref="IRecapEmails"/>
/// <remarks>
/// <para>
/// What each figure counts, for a period between two local midnights:
/// new leads by when they were created, grouped by their source as typed; a referral is a lead
/// with someone recorded as referring them, or the source "Referral"; a lead marked lost by its
/// status history (a move to "Lost / Not Interested" or a status like it); an invitation by when
/// the "Invite to a meeting" email went; a meeting booked by when the website reported it, held
/// by when it took place (not cancelled), cancelled by when it was; a contract sent by when it
/// went out, signed by when the counterparty signed in CadenceE or the signed copy was uploaded;
/// a deposit by when it was paid; a lead converted and a deal won by when they were; a new member
/// by the day they joined. The pipeline, members at risk and renewals coming up are as they stand.
/// </para>
/// <para>
/// Read as the recipient: the deals they may see (which, seeing every deal, is all of them), and
/// nothing about anyone beyond the names of the top referrers. Members erased for privacy count
/// in none of it.
/// </para>
/// <para>
/// Sending: a person's recap for a frequency and period is taken by inserting its RecapSend row
/// (unique per workspace, person, frequency and period), so two servers never send it twice; a row
/// left half done by a server that died is taken over after a quarter of an hour. The row's "sent"
/// is saved by the same save that puts the email in the outbox, so neither is kept without the
/// other; anything that fails before then gives the period back for the next pass.
/// </para>
/// </remarks>
public sealed class RecapService : IRecapEmails
{
    private const int TopSources = 5;
    private const int TopReferrers = 5;
    private const int RenewalDays = 30;
    private static readonly TimeSpan TakeOverAfter = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Status names that mean the lead said no, beyond the agreed "Lost / Not Interested" and the
    /// labels it replaced (LeadStatusVocabulary): what a workspace that named its own calls it.
    /// </summary>
    private static readonly HashSet<string> LostStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "lost", "closed lost", "not interested", "no longer interested", "lost or not interested",
        "unqualified", "disqualified", "rejected", "dead",
    };

    private readonly AppDbContext _db;
    private readonly IOutbox _outbox;
    private readonly EmailSettings _email;
    private readonly ILogger<RecapService> _logger;

    public RecapService(AppDbContext db, IOutbox outbox, IOptions<EmailSettings> email, ILogger<RecapService> logger)
    {
        _db = db;
        _outbox = outbox;
        _email = email.Value;
        _logger = logger;
    }

    // ── who receives which ─────────────────────────────────────────────────

    private sealed record Person(Guid UserId, string Name, string Email, OrgMemberRole Role, string? Preset, string? Permissions)
    {
        public bool SeesAllDeals => MemberPermissions.Effective(Role, Preset, Permissions).Contains(MemberPermissions.SeeAllDeals);
    }

    private async Task<List<Person>> TeamAsync(Guid organizationId, Guid? userId, CancellationToken ct) =>
        await (from m in _db.OrganizationMembers.AsNoTracking()
               where m.OrganizationId == organizationId && (userId == null || m.UserId == userId)
               join u in _db.Users.AsNoTracking() on m.UserId equals u.Id
               select new Person(m.UserId, u.Name, u.Email, m.Role, m.PermissionPreset, m.Permissions))
            .ToListAsync(ct);

    public async Task<RecapRecipientsDto> RecipientsAsync(Guid organizationId, CancellationToken ct = default)
    {
        var org = await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId)
            .Select(o => new { o.TimeZone, o.OwnerUserId }).FirstOrDefaultAsync(ct);
        var team = await TeamAsync(organizationId, null, ct);
        var subscriptions = await _db.RecapSubscriptions.AsNoTracking().Where(s => s.OrganizationId == organizationId)
            .ToDictionaryAsync(s => s.UserId, ct);
        var people = team.Where(p => p.SeesAllDeals)
            .OrderBy(p => p.UserId == org?.OwnerUserId ? 0 : 1)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(p => Dto(p, subscriptions.GetValueOrDefault(p.UserId)))
            .ToList();
        return new RecapRecipientsDto(RegionalSettings.ZoneOf(org?.TimeZone).Id, people, team.Count - people.Count);
    }

    public async Task<Result<RecapRecipientDto>> SetRecipientAsync(
        Guid organizationId, Guid changedByUserId, Guid userId, RecapFrequenciesRequest request, CancellationToken ct = default)
    {
        var person = (await TeamAsync(organizationId, userId, ct)).FirstOrDefault();
        if (person == null) return DomainErrors.Recaps.PersonNotFound;
        if (SwitchesOn(request) && !person.SeesAllDeals) return DomainErrors.Recaps.NotEligible(person.Name);
        var saved = await SaveAsync(organizationId, userId, changedByUserId, request, ct);
        return Dto(person, saved);
    }

    public async Task<MyRecapsDto> MineAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        var person = (await TeamAsync(organizationId, userId, ct)).FirstOrDefault();
        var zone = await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => o.TimeZone).FirstOrDefaultAsync(ct);
        var mine = await _db.RecapSubscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.UserId == userId, ct);
        return new MyRecapsDto(person?.SeesAllDeals == true,
            mine?.DailySinceUtc != null, mine?.WeeklySinceUtc != null, mine?.MonthlySinceUtc != null, RegionalSettings.ZoneOf(zone).Id);
    }

    public async Task<Result<MyRecapsDto>> SetMineAsync(Guid organizationId, Guid userId, RecapFrequenciesRequest request, CancellationToken ct = default)
    {
        var person = (await TeamAsync(organizationId, userId, ct)).FirstOrDefault();
        if (person == null) return DomainErrors.Recaps.PersonNotFound;
        if (SwitchesOn(request) && !person.SeesAllDeals) return DomainErrors.Recaps.NotEligibleYourself;
        await SaveAsync(organizationId, userId, userId, request, ct);
        return await MineAsync(organizationId, userId, ct);
    }

    private static bool SwitchesOn(RecapFrequenciesRequest r) => r.Daily == true || r.Weekly == true || r.Monthly == true;

    private static RecapRecipientDto Dto(Person p, RecapSubscription? s) => new(
        p.UserId, p.Name, p.Email, MemberPermissions.PresetOf(p.Role, p.Preset).Key,
        s?.DailySinceUtc != null, s?.WeeklySinceUtc != null, s?.MonthlySinceUtc != null);

    /// <summary>
    /// Switches the frequencies named on (from now) or off, leaving the others as they are. Only the
    /// columns that change are written, so two people switching different ones at once both count.
    /// </summary>
    private async Task<RecapSubscription> SaveAsync(Guid organizationId, Guid userId, Guid changedByUserId, RecapFrequenciesRequest request, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var now = DateTime.UtcNow;
            var row = await _db.RecapSubscriptions.FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.UserId == userId, ct);
            var added = row == null;
            row ??= _db.RecapSubscriptions.Add(new RecapSubscription
            {
                Id = Guid.NewGuid(), OrganizationId = organizationId, UserId = userId, CreatedAtUtc = now,
            }).Entity;
            row.DailySinceUtc = Switched(row.DailySinceUtc, request.Daily, now);
            row.WeeklySinceUtc = Switched(row.WeeklySinceUtc, request.Weekly, now);
            row.MonthlySinceUtc = Switched(row.MonthlySinceUtc, request.Monthly, now);
            row.UpdatedAtUtc = now;
            row.UpdatedByUserId = changedByUserId;
            try
            {
                await _db.SaveChangesAsync(ct);
                return row;
            }
            catch (DbUpdateException ex) when (added && attempt < 3 && ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // Made at the same moment by another request: the change goes on that row.
                _db.Entry(row).State = EntityState.Detached;
            }
        }
    }

    /// <summary>On since it was switched on (kept when it already was), off when asked, as it was when not named.</summary>
    private static DateTime? Switched(DateTime? since, bool? on, DateTime now) => on switch
    {
        true => since ?? now,
        false => null,
        null => since,
    };

    // ── the figures ────────────────────────────────────────────────────────

    public async Task<RecapFigures> FiguresAsync(Guid organizationId, Guid userId, RecapPeriod period, DateTime nowUtc, CancellationToken ct = default)
    {
        // Read as this workspace and person: the job that sends recaps acts for none.
        using var acting = _db.ActFor(organizationId, userId);
        var org = await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId)
            .Select(o => new { o.Currency, o.TimeZone, o.SalesModuleOff, o.ClubModuleOff })
            .FirstAsync(ct);
        var zone = RegionalSettings.ZoneOf(org.TimeZone);
        var previous = RecapPeriods.Previous(period, zone);
        var currency = RegionalSettings.TryCurrency(org.Currency, out var code) ? code : RegionalSettings.DefaultCurrency;
        var w = new Window(previous.FromUtc, period.FromUtc, period.ToUtc);

        var sales = org.SalesModuleOff ? null : await SalesAsync(organizationId, userId, w, currency, ct);
        RecapCount? joined = null;
        RecapClubFigures? club = null;
        if (!org.ClubModuleOff)
        {
            joined = await CountAsync(_db.ClubMembers.AsNoTracking()
                .Where(m => m.OrganizationId == organizationId && m.ErasedAtUtc == null)
                .Select(m => (DateTime?)m.JoinedAtUtc), w, ct);
            club = await ClubAsync(organizationId, w, nowUtc, ct);
        }
        return new RecapFigures(period, previous, sales, joined, club);
    }

    /// <summary>The two periods as instants: the one before runs from <see cref="From"/> to <see cref="Split"/>, the period from there to <see cref="To"/>.</summary>
    private sealed record Window(DateTime From, DateTime Split, DateTime To)
    {
        public bool InPeriod(DateTime at) => at >= Split && at < To;
        public bool InBefore(DateTime at) => at >= From && at < Split;
    }

    /// <summary>How many instants fall in each period; a row without one counts in neither.</summary>
    private static async Task<RecapCount> CountAsync(IQueryable<DateTime?> instants, Window w, CancellationToken ct)
    {
        var now = await instants.CountAsync(t => t >= w.Split && t < w.To, ct);
        var before = await instants.CountAsync(t => t >= w.From && t < w.Split, ct);
        return new RecapCount(now, before);
    }

    private static RecapCount CountIn<T>(IEnumerable<T> rows, Func<T, DateTime> at, Window w)
    {
        var list = rows as IReadOnlyCollection<T> ?? rows.ToList();
        return new RecapCount(list.Count(r => w.InPeriod(at(r))), list.Count(r => w.InBefore(at(r))));
    }

    private async Task<RecapSalesFigures> SalesAsync(Guid organizationId, Guid userId, Window w, string currency, CancellationToken ct)
    {
        // ── leads: new ones by source, those through a referral and who referred them ──
        var leads = _db.Leads.AsNoTracking().Where(l => l.OrganizationId == organizationId);
        var created = await leads.Where(l => l.CreatedAtUtc >= w.From && l.CreatedAtUtc < w.To)
            .Select(l => new { l.CreatedAtUtc, l.Source, l.ReferredByContactId })
            .ToListAsync(ct);
        var newLeads = CountIn(created, l => l.CreatedAtUtc, w);

        var bySource = created
            .GroupBy(l => (l.Source ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new RecapNamedCount(g.First().Source?.Trim() ?? "", g.Count(l => w.InPeriod(l.CreatedAtUtc)), g.Count(l => w.InBefore(l.CreatedAtUtc))))
            .OrderByDescending(s => s.Now).ThenByDescending(s => s.Before).ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var top = bySource.Take(TopSources).ToList();
        var rest = bySource.Skip(TopSources).ToList();
        var otherSources = new RecapCount(rest.Sum(s => s.Now), rest.Sum(s => s.Before));

        var referredRows = created.Where(l => l.ReferredByContactId != null
            || string.Equals((l.Source ?? "").Trim(), "Referral", StringComparison.OrdinalIgnoreCase)).ToList();
        var referred = CountIn(referredRows, l => l.CreatedAtUtc, w);
        var referrerCounts = created.Where(l => l.ReferredByContactId != null)
            .GroupBy(l => l.ReferredByContactId!.Value)
            .Select(g => new { Id = g.Key, Now = g.Count(l => w.InPeriod(l.CreatedAtUtc)), Before = g.Count(l => w.InBefore(l.CreatedAtUtc)) })
            .Where(r => r.Now > 0)
            .ToList();
        var referrerIds = referrerCounts.Select(r => r.Id).ToList();
        var names = referrerIds.Count == 0 ? new Dictionary<Guid, string>() : await _db.Contacts.AsNoTracking()
            .Where(c => referrerIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var referrers = referrerCounts
            .Where(r => names.ContainsKey(r.Id))
            .Select(r => new RecapNamedCount(names[r.Id].Trim(), r.Now, r.Before))
            .OrderByDescending(r => r.Now).ThenByDescending(r => r.Before).ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(TopReferrers)
            .ToList();

        // ── leads marked lost, by their status history: each lead once per period ──
        var moves = await _db.LeadStatusEvents.AsNoTracking()
            .Where(e => e.OrganizationId == organizationId && e.ChangedAtUtc >= w.From && e.ChangedAtUtc < w.To)
            .Select(e => new { e.LeadId, e.ToStatus, e.ChangedAtUtc })
            .ToListAsync(ct);
        var lostMoves = moves.Where(m => IsLost(m.ToStatus)).ToList();
        var lost = new RecapCount(
            lostMoves.Where(m => w.InPeriod(m.ChangedAtUtc)).Select(m => m.LeadId).Distinct().Count(),
            lostMoves.Where(m => w.InBefore(m.ChangedAtUtc)).Select(m => m.LeadId).Distinct().Count());

        // ── meetings: the invitations that went, and the meetings the website reported ──
        var invitations = await CountAsync(_db.OutboundMessages.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.Source == OutboundSources.Newsletter && m.Status == OutboundStatuses.Sent)
            .Select(m => m.SentAtUtc), w, ct);
        var bookings = _db.MeetingBookings.AsNoTracking().Where(b => b.OrganizationId == organizationId);
        var booked = await CountAsync(bookings.Select(b => (DateTime?)b.CreatedAtUtc), w, ct);
        var held = await CountAsync(bookings.Where(b => b.Status == MeetingBookingStatuses.Booked).Select(b => (DateTime?)b.StartsAtUtc), w, ct);
        var cancelled = await CountAsync(bookings.Where(b => b.Status == MeetingBookingStatuses.Cancelled).Select(b => (DateTime?)b.UpdatedAtUtc), w, ct);

        // ── contracts: sent, signed (a signed copy uploaded counts when it was), deposits paid ──
        var contracts = _db.Contracts.AsNoTracking().Where(c => c.OrganizationId == organizationId);
        var sent = await CountAsync(contracts.Select(c => c.SentAtUtc), w, ct);
        var signed = await CountAsync(contracts.Select(c => c.SignedCopyAttachmentId == null ? c.ClientSignedAtUtc : c.CounterSignedAtUtc), w, ct);
        var paid = await _db.ContractDeposits.AsNoTracking()
            .Where(d => d.OrganizationId == organizationId && d.PaidAtUtc >= w.From && d.PaidAtUtc < w.To)
            .Select(d => new { PaidAtUtc = d.PaidAtUtc!.Value, d.Currency, Amount = d.AmountPaid ?? d.Amount })
            .ToListAsync(ct);
        var deposits = CountIn(paid, d => d.PaidAtUtc, w);
        var depositAmounts = Amounts(paid.Select(d => (d.PaidAtUtc, Code(d.Currency, currency), (decimal?)d.Amount)), w);

        // ── conversions: leads converted, deals won as the recipient sees them ──
        var converted = await CountAsync(leads.Where(l => l.IsConverted).Select(l => l.ConvertedAtUtc), w, ct);
        var won = await _db.Deals.AsNoTracking().VisibleTo(_db, userId, organizationId)
            .Where(d => d.IsWon == true && d.ClosedAtUtc >= w.From && d.ClosedAtUtc < w.To)
            .Select(d => new { ClosedAtUtc = d.ClosedAtUtc!.Value, d.Currency, d.Amount })
            .ToListAsync(ct);
        var wonCount = CountIn(won, d => d.ClosedAtUtc, w);
        var wonValue = Amounts(won.Select(d => (d.ClosedAtUtc, Code(d.Currency, currency), d.Amount)), w);
        var withoutValue = won.Count(d => d.Amount == null && w.InPeriod(d.ClosedAtUtc));

        // ── the pipeline now: open leads by the phase their tracker says they are in ──
        var open = await leads.Where(l => !l.IsConverted).Select(l => new { l.Status, l.PipelineState }).ToListAsync(ct);
        var phases = new int[LeadPhases.Count];
        foreach (var lead in open)
        {
            if (IsLost(lead.Status) || LeadStatusDerivation.DeriveStage(LeadStatusDerivation.ParsePipeline(lead.PipelineState)).Terminal) continue;
            phases[LeadPhases.Current(lead.PipelineState) - 1]++;
        }

        return new RecapSalesFigures(newLeads, top, otherSources, referred, referrers, lost,
            invitations, booked, held, cancelled, sent, signed, deposits, depositAmounts,
            converted, wonCount, wonValue, withoutValue, phases);
    }

    /// <summary>Whether a status says the lead said no: the agreed lost stage, a label it replaced, or a name like it.</summary>
    private static bool IsLost(string? status)
    {
        var name = (status ?? "").Trim();
        if (name.Length == 0) return false;
        return LeadStatusVocabulary.StageIndex(name) == LeadStatusVocabulary.StageIndex(LeadStatusVocabulary.Lost) || LostStatuses.Contains(name);
    }

    /// <summary>A currency as the books read it: its code, or the workspace's when it has none.</summary>
    private static string Code(string? raw, string fallback) => RegionalSettings.TryCurrency(raw, out var code) ? code : fallback;

    /// <summary>Money per currency in each period, largest first; amounts that could not be read are left out.</summary>
    private static List<RecapAmount> Amounts(IEnumerable<(DateTime At, string Currency, decimal? Amount)> rows, Window w) =>
        rows.Where(r => r.Amount != null)
            .GroupBy(r => r.Currency, StringComparer.Ordinal)
            .Select(g => new RecapAmount(g.Key,
                g.Where(r => w.InPeriod(r.At)).Sum(r => r.Amount!.Value),
                g.Where(r => w.InBefore(r.At)).Sum(r => r.Amount!.Value)))
            .Where(a => a.Now != 0 || a.Before != 0)
            .OrderByDescending(a => a.Now).ThenByDescending(a => a.Before).ThenBy(a => a.Currency, StringComparer.Ordinal)
            .ToList();

    private async Task<RecapClubFigures> ClubAsync(Guid organizationId, Window w, DateTime nowUtc, CancellationToken ct)
    {
        var visits = await CountAsync(_db.ClubVisits.AsNoTracking().Where(v => v.OrganizationId == organizationId)
            .Select(v => (DateTime?)v.VisitedAtUtc), w, ct);
        // Held: published (or marked completed) and not cancelled, by when they started.
        var events = await _db.ClubEvents.AsNoTracking()
            .Where(e => e.OrganizationId == organizationId && (e.Status == ClubEventStatuses.Published || e.Status == ClubEventStatuses.Completed)
                && e.StartAtUtc >= w.From && e.StartAtUtc < w.To)
            .Select(e => new
            {
                e.StartAtUtc,
                CheckedIn = _db.ClubEventRegistrations.Count(r => r.EventId == e.Id && r.Status == RegistrationStatuses.CheckedIn),
            })
            .ToListAsync(ct);
        var held = CountIn(events, e => e.StartAtUtc, w);
        var attendance = new RecapCount(events.Where(e => w.InPeriod(e.StartAtUtc)).Sum(e => e.CheckedIn), events.Where(e => w.InBefore(e.StartAtUtc)).Sum(e => e.CheckedIn));

        var atRisk = await (from r in _db.ClubMemberRisks.AsNoTracking()
                            join m in _db.ClubMembers.AsNoTracking() on r.MemberId equals m.Id
                            where r.OrganizationId == organizationId && r.Level == RiskLevels.AtRisk && m.ErasedAtUtc == null && m.Status == "Active"
                            select r.Id)
            .CountAsync(ct);
        // As the morning brief counts renewals coming up, over the next 30 days.
        var renewals = await _db.ClubMembers.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.ErasedAtUtc == null && m.Status == "Active"
                && m.RenewsAtUtc >= nowUtc && m.RenewsAtUtc < nowUtc.AddDays(RenewalDays))
            .NotMarkedNotRenewing(_db)
            .CountAsync(ct);
        return new RecapClubFigures(visits, held, attendance, atRisk, renewals);
    }

    // ── sending ────────────────────────────────────────────────────────────

    public async Task<int> SendDueAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        // Who has a recap on, across workspaces, read past the filters: the job acts for no
        // workspace, and each recap is then built acting for its own workspace and person.
        var subscriptions = await _db.RecapSubscriptions.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.DailySinceUtc != null || s.WeeklySinceUtc != null || s.MonthlySinceUtc != null)
            .ToListAsync(ct);
        if (subscriptions.Count == 0) return 0;
        var orgIds = subscriptions.Select(s => s.OrganizationId).Distinct().ToList();
        var userIds = subscriptions.Select(s => s.UserId).Distinct().ToList();
        var zones = await _db.Organizations.AsNoTracking().Where(o => orgIds.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.TimeZone, ct);
        // Only people still in the workspace who see every deal: someone who left, or lost
        // deals.all, gets nothing, whatever their row says.
        var seeAll = (await _db.OrganizationMembers.AsNoTracking()
                .Where(m => orgIds.Contains(m.OrganizationId) && userIds.Contains(m.UserId))
                .Select(m => new { m.OrganizationId, m.UserId, m.Role, m.PermissionPreset, m.Permissions })
                .ToListAsync(ct))
            .Where(m => MemberPermissions.Effective(m.Role, m.PermissionPreset, m.Permissions).Contains(MemberPermissions.SeeAllDeals))
            .Select(m => (m.OrganizationId, m.UserId))
            .ToHashSet();
        // The recaps already taken in the last weeks, so a pass that finds them done reads no more.
        var since = DateOnly.FromDateTime(nowUtc).AddMonths(-3);
        var takeOverBefore = nowUtc - TakeOverAfter;
        var taken = (await _db.RecapSends.IgnoreQueryFilters().AsNoTracking()
                .Where(r => r.PeriodStart >= since && (r.Status != RecapSendStatuses.Preparing || r.CreatedAtUtc > takeOverBefore))
                .Select(r => new { r.OrganizationId, r.UserId, r.Frequency, r.PeriodStart })
                .ToListAsync(ct))
            .Select(r => (r.OrganizationId, r.UserId, r.Frequency, r.PeriodStart))
            .ToHashSet();

        var queued = 0;
        foreach (var subscription in subscriptions)
        {
            if (!zones.TryGetValue(subscription.OrganizationId, out var zoneId) || !seeAll.Contains((subscription.OrganizationId, subscription.UserId))) continue;
            var zone = RegionalSettings.ZoneOf(zoneId);
            foreach (var (frequency, on) in new[]
            {
                (RecapFrequencies.Daily, subscription.DailySinceUtc),
                (RecapFrequencies.Weekly, subscription.WeeklySinceUtc),
                (RecapFrequencies.Monthly, subscription.MonthlySinceUtc),
            })
            {
                if (on is not DateTime switchedOn) continue;
                var period = RecapPeriods.LatestDue(frequency, nowUtc, zone);
                // Switched on after this period fell due: their first is the next one.
                if (switchedOn > period.DueUtc) continue;
                if (taken.Contains((subscription.OrganizationId, subscription.UserId, frequency, period.FirstDay))) continue;
                try
                {
                    if (await SendOneAsync(subscription.OrganizationId, subscription.UserId, period, nowUtc, ct)) queued++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // One recap failing never holds up anyone else's; its period was given back.
                    _logger.LogError(ex, "The {Frequency} recap for user {UserId} in workspace {OrganizationId} could not be prepared",
                        frequency, subscription.UserId, subscription.OrganizationId);
                }
            }
        }
        return queued;
    }

    private async Task<bool> SendOneAsync(Guid organizationId, Guid userId, RecapPeriod period, DateTime nowUtc, CancellationToken ct)
    {
        // Everything below as this workspace and person: the claim, the figures, the outbox's checks.
        using var acting = _db.ActFor(organizationId, userId);
        var claim = await ClaimAsync(organizationId, userId, period, nowUtc, ct);
        if (claim == null) return false;
        var done = false;
        try
        {
            var figures = await FiguresAsync(organizationId, userId, period, nowUtc, ct);
            var user = await _db.Users.AsNoTracking().Where(u => u.Id == userId)
                .Select(u => new { u.Name, u.Email, u.Language }).FirstOrDefaultAsync(ct);
            if (!figures.HasNews || string.IsNullOrWhiteSpace(user?.Email))
            {
                // Nothing to report (or nowhere to report it): no email, and the period is done.
                claim.Status = figures.HasNews ? RecapSendStatuses.Refused : RecapSendStatuses.Nothing;
                claim.SentAtUtc = nowUtc;
                await _db.SaveChangesAsync(ct);
                done = true;
                return false;
            }

            var org = await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId)
                .Select(o => new { o.Name, o.Locale, o.SalesModuleOff }).FirstAsync(ct);
            var language = AppLanguages.Resolve(user.Language, org.Locale);
            var (subject, text, html) = RecapEmail.Compose(figures,
                new RecapEmailContext(language, user.Name, org.Name, org.Locale, _email.FrontendBaseUrl ?? "", !org.SalesModuleOff));

            // The claim's "sent" goes with the outbox's own save of the email: both or neither.
            claim.Status = RecapSendStatuses.Sent;
            claim.SentAtUtc = nowUtc;
            var message = await _outbox.QueueEmailAsync(new OutboundEmailRequest
            {
                OrganizationId = organizationId,
                Source = OutboundSources.Recap,
                SourceId = claim.Id,
                To = [user.Email!],
                RecipientName = user.Name,
                Language = language,
                // Record text (a source, a referrer, the workspace's name) is never read as a placeholder.
                Subject = subject.Replace("{{", "{ {", StringComparison.Ordinal),
                Body = text.Replace("{{", "{ {", StringComparison.Ordinal),
                Html = html,
                // For someone on the team about the workspace: nothing to unsubscribe from; they
                // switch it off in their notification settings, which the email links to.
                ForOwnTeam = true,
            }, ct);
            if (message.IsFailure)
            {
                // Refused for good (their address is on the do-not-send list, say): tried again
                // every pass, it would only be refused again.
                claim.Status = RecapSendStatuses.Refused;
                await _db.SaveChangesAsync(ct);
                done = true;
                _logger.LogWarning("The {Frequency} recap for user {UserId} in workspace {OrganizationId} was not queued: {Error}",
                    period.Frequency, userId, organizationId, message.Error.Description);
                return false;
            }
            done = true;
            claim.OutboundMessageId = message.Value.Id;
            await _db.SaveChangesAsync(ct);
            return true;
        }
        finally
        {
            if (!done) await ReleaseAsync(claim.Id, CancellationToken.None);
        }
    }

    /// <summary>Takes the person's recap for the period: inserts its row, or takes over one a server left half done.</summary>
    private async Task<RecapSend?> ClaimAsync(Guid organizationId, Guid userId, RecapPeriod period, DateTime nowUtc, CancellationToken ct)
    {
        var existing = await _db.RecapSends.FirstOrDefaultAsync(r => r.OrganizationId == organizationId && r.UserId == userId
            && r.Frequency == period.Frequency && r.PeriodStart == period.FirstDay, ct);
        if (existing != null)
        {
            if (existing.Status != RecapSendStatuses.Preparing || existing.CreatedAtUtc > nowUtc - TakeOverAfter) return null;
            var stale = existing.CreatedAtUtc;
            if (_db.Database.IsRelational())
            {
                // Only one server takes it over: the one whose statement still finds it as it was.
                var tookOver = await _db.RecapSends
                    .Where(r => r.Id == existing.Id && r.Status == RecapSendStatuses.Preparing && r.CreatedAtUtc == stale)
                    .ExecuteUpdateAsync(set => set.SetProperty(r => r.CreatedAtUtc, nowUtc), ct);
                if (tookOver != 1) return null;
                await _db.Entry(existing).ReloadAsync(ct);
                return existing;
            }
            // The in-memory test host: one process, so no race.
            existing.CreatedAtUtc = nowUtc;
            await _db.SaveChangesAsync(ct);
            return existing;
        }

        var row = new RecapSend
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Frequency = period.Frequency,
            PeriodStart = period.FirstDay,
            Status = RecapSendStatuses.Preparing,
            CreatedAtUtc = nowUtc,
        };
        _db.RecapSends.Add(row);
        try
        {
            await _db.SaveChangesAsync(ct);
            return row;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Another server took it first.
            _db.Entry(row).State = EntityState.Detached;
            return null;
        }
    }

    /// <summary>Gives back a recap that could not be finished, so the next pass tries again.</summary>
    private async Task ReleaseAsync(Guid claimId, CancellationToken ct)
    {
        try
        {
            // Whatever the failed attempt left unsaved goes with it.
            _db.ChangeTracker.Clear();
            if (_db.Database.IsRelational())
            {
                await _db.RecapSends.IgnoreQueryFilters()
                    .Where(r => r.Id == claimId && r.Status == RecapSendStatuses.Preparing)
                    .ExecuteDeleteAsync(ct);
                return;
            }
            var row = await _db.RecapSends.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Id == claimId, ct);
            if (row == null || row.Status != RecapSendStatuses.Preparing) return;
            _db.RecapSends.Remove(row);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "A recap's claim could not be given back; it is taken over after a quarter of an hour");
        }
    }
}
