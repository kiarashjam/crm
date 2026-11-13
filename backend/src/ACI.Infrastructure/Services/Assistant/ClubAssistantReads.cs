using System.Globalization;
using System.Text;
using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services.Assistant;

/// <inheritdoc cref="IClubAssistantReads"/>
/// <remarks>
/// Money comes from the club's own services (the ledger's balance, Today at the Club's past-due
/// list), so the assistant says what the money pages say. Times are on the workspace's clock.
/// Every query names its workspace, and the request's workspace filter applies as well.
/// </remarks>
public sealed class ClubAssistantReads : IClubAssistantReads
{
    private const int MaxRows = 25;

    private readonly AppDbContext _db;
    private readonly IClubLedger _ledger;
    private readonly IClubReports _reports;

    public ClubAssistantReads(AppDbContext db, IClubLedger ledger, IClubReports reports)
    {
        _db = db;
        _ledger = ledger;
        _reports = reports;
    }

    public async Task<string> SearchMembersAsync(Guid organizationId, string? query, int limit, CancellationToken ct = default)
    {
        var take = Math.Clamp(limit, 1, MaxRows);
        var members = Live(organizationId);
        var q = query?.Trim();
        if (!string.IsNullOrEmpty(q))
            members = members.Where(m => (m.FirstName + " " + m.LastName).Contains(q) || m.Email.Contains(q));
        var total = await members.CountAsync(ct);
        var rows = await members.OrderByDescending(m => m.JoinedAtUtc).Take(take)
            .Select(m => new { m.Id, m.FirstName, m.LastName, m.Tier, m.Status, m.HouseAccountBalance, m.RenewsAtUtc })
            .ToListAsync(ct);
        if (rows.Count == 0)
            return string.IsNullOrEmpty(q) ? "No members in this club." : $"No members match \"{Clip(q, 80)}\".";
        var (currency, zone) = await WorkspaceAsync(organizationId, ct);
        var lines = new List<string> { $"Members ({rows.Count} of {total}):" };
        lines.AddRange(rows.Select(m =>
            $"- {Name(m.FirstName, m.LastName)} · {m.Tier} · {m.Status} · balance {Money(m.HouseAccountBalance, currency)} (negative is owed) · renews {Day(m.RenewsAtUtc, zone)} · id {m.Id}"));
        if (rows.Count < total) lines.Add("This list is incomplete. Narrow the search to see the rest.");
        return string.Join('\n', lines);
    }

    public async Task<string?> MemberAsync(Guid organizationId, Guid memberId, CancellationToken ct = default)
    {
        var m = await Live(organizationId).FirstOrDefaultAsync(x => x.Id == memberId, ct);
        if (m == null) return null;
        var (_, zone) = await WorkspaceAsync(organizationId, ct);
        var lines = new List<string>
        {
            $"Member: {Name(m.FirstName, m.LastName)} · id {m.Id}",
            $"Tier {m.Tier} · status {m.Status} · joined {Day(m.JoinedAtUtc, zone)} · renews {Day(m.RenewsAtUtc, zone)}",
        };
        var balance = await _ledger.BalanceAsync(organizationId, memberId, null, ct);
        if (balance.IsSuccess)
        {
            var b = balance.Value;
            lines.Add($"House account: balance {Money(b.Balance, b.Currency)} (negative is owed); owed on {b.OpenCharges} open charge(s) {Money(b.Owed, b.Currency)}; unspent credit {Money(b.Credit, b.Currency)}.");
        }
        var now = DateTime.UtcNow;
        var overdue = await _db.ClubCharges.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && c.MemberId == memberId && c.ReversesChargeId == null
                && c.EntryType != ClubEntryTypes.Void && c.DueAtUtc != null && c.DueAtUtc < now
                && !_db.ClubCharges.Any(v => v.ReversesChargeId == c.Id))
            .Select(c => new { c.Description, c.Currency, c.DueAtUtc, Owed = c.Amount - (_db.ClubPaymentAllocations.Where(a => a.ChargeId == c.Id).Sum(a => (decimal?)a.Amount) ?? 0m) })
            .Where(c => c.Owed > 0)
            .OrderBy(c => c.DueAtUtc).Take(5).ToListAsync(ct);
        if (overdue.Count > 0)
            lines.Add("Past due: " + string.Join("; ", overdue.Select(c => $"{Clip(c.Description, 60)} {Money(c.Owed, c.Currency)} (due {Day(c.DueAtUtc!.Value, zone)})")));
        var bookings = await _db.ClubReservations.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId && r.MemberId == memberId && r.StartAtUtc >= now && r.Status != ReservationStatuses.Cancelled)
            .OrderBy(r => r.StartAtUtc).Take(3)
            .Select(r => new { r.ResourceName, r.StartAtUtc, r.PartySize, r.Status }).ToListAsync(ct);
        if (bookings.Count > 0)
            lines.Add("Coming bookings: " + string.Join("; ", bookings.Select(r => $"{Clip(r.ResourceName, 40)} {When(r.StartAtUtc, zone)}, {r.PartySize} people, {r.Status}")));
        var lastVisit = await _db.ClubVisits.AsNoTracking()
            .Where(v => v.OrganizationId == organizationId && v.MemberId == memberId)
            .MaxAsync(v => (DateTime?)v.VisitedAtUtc, ct);
        lines.Add(lastVisit is DateTime visited ? $"Last check-in: {Day(visited, zone)}." : "No check-ins recorded.");
        if (!string.IsNullOrWhiteSpace(m.Notes)) lines.Add("Recorded notes: " + Clip(m.Notes, 200));
        return string.Join('\n', lines);
    }

    public async Task<string> TodayAsync(Guid organizationId, CancellationToken ct = default)
    {
        var result = await _reports.TodayAsync(organizationId, ct);
        if (!result.IsSuccess) return "Error: today at the club could not be read.";
        var t = result.Value;
        var zone = RegionalSettings.ZoneOf(t.TimeZone);
        var lines = new List<string>
        {
            $"Today at the club ({t.Date}, {t.TimeZone}):",
            $"Check-ins so far: {t.CheckIns}.",
            $"Bookings today: {t.Reservations.Count}."
        };
        lines.AddRange(t.Reservations.Items.Select(r => $"- {Clock(r.StartAtUtc, zone)} {Clip(r.ResourceName, 40)} · {Clip(r.MemberName, 60)} · {r.PartySize} people · {r.Status}"));
        lines.Add($"Open tabs: {t.OpenTabs.Count}{(t.OpenTabs.Totals.Count == 0 ? "" : ", " + string.Join(" · ", t.OpenTabs.Totals.Select(a => Money(a.Amount, a.Currency))))}.");
        lines.Add($"Applications waiting for a decision: {t.Applications.Count}.");
        lines.AddRange(t.Applications.Items.Select(a => $"- {Clip(a.FirstName + " " + a.LastName, 60)} · {a.RequestedTier} · {a.Status}"));
        lines.Add($"Charges past due: {t.PastDue.Charges}{(t.PastDue.Owed.Count == 0 ? "" : ", owed " + string.Join(" · ", t.PastDue.Owed.Select(a => Money(a.Amount, a.Currency))))}; subscriptions past due: {t.PastDue.Subscriptions}.");
        lines.AddRange(t.PastDue.ChargeItems.Select(c => $"- {Clip(c.MemberName, 60)} · {Clip(c.Description, 60)} · {Money(c.Owed, c.Currency)} · due {Day(c.DueAtUtc, zone)}"));
        lines.Add($"Events in the next 7 days: {t.EventsThisWeek.Count}.");
        lines.AddRange(t.EventsThisWeek.Items.Select(e => $"- {When(e.StartAtUtc, zone)} {Clip(e.Name, 60)} · {e.RegisteredCount}{(e.Capacity > 0 ? $" of {e.Capacity}" : "")} seats taken"));
        return string.Join('\n', lines);
    }

    public async Task<string> RenewalsDueAsync(Guid organizationId, int days, CancellationToken ct = default)
    {
        var window = Math.Clamp(days, 1, 120);
        var now = DateTime.UtcNow;
        var due = Live(organizationId).Where(m => m.Status == "Active" && m.RenewsAtUtc >= now && m.RenewsAtUtc < now.AddDays(window));
        var total = await due.CountAsync(ct);
        var rows = await due.OrderBy(m => m.RenewsAtUtc).Take(MaxRows)
            .Select(m => new { m.Id, m.FirstName, m.LastName, m.Tier, m.RenewsAtUtc, m.DuesAmount, m.DuesFrequency }).ToListAsync(ct);
        if (rows.Count == 0) return $"No active memberships renew in the next {window} days.";
        var (currency, zone) = await WorkspaceAsync(organizationId, ct);
        var lines = new List<string> { $"Memberships renewing in the next {window} days ({rows.Count} of {total}):" };
        lines.AddRange(rows.Select(m => $"- {Name(m.FirstName, m.LastName)} · {m.Tier} · renews {Day(m.RenewsAtUtc, zone)} · dues {Money(m.DuesAmount, currency)} {m.DuesFrequency.ToLowerInvariant()} · id {m.Id}"));
        return string.Join('\n', lines);
    }

    public async Task<string> UpcomingEventsAsync(Guid organizationId, int days, CancellationToken ct = default)
    {
        var window = Math.Clamp(days, 1, 120);
        var now = DateTime.UtcNow;
        var events = await _db.ClubEvents.AsNoTracking()
            .Where(e => e.OrganizationId == organizationId && e.Status == ClubEventStatuses.Published && e.EndAtUtc >= now && e.StartAtUtc < now.AddDays(window))
            .OrderBy(e => e.StartAtUtc).Take(MaxRows)
            .Select(e => new
            {
                e.Id, e.Name, e.StartAtUtc, e.Capacity,
                Taken = _db.ClubEventRegistrations.Count(r => r.EventId == e.Id
                    && (r.Status == RegistrationStatuses.Reserved || r.Status == RegistrationStatuses.CheckedIn)) + e.UnnamedSeats,
                Waiting = _db.ClubEventRegistrations.Count(r => r.EventId == e.Id && r.Status == RegistrationStatuses.Waitlisted),
            })
            .ToListAsync(ct);
        if (events.Count == 0) return $"No published events in the next {window} days.";
        var (_, zone) = await WorkspaceAsync(organizationId, ct);
        var lines = new List<string> { $"Events in the next {window} days:" };
        lines.AddRange(events.Select(e =>
            $"- {When(e.StartAtUtc, zone)} {Clip(e.Name, 60)} · {e.Taken}{(e.Capacity > 0 ? $" of {e.Capacity}" : "")} seats taken{(e.Waiting > 0 ? $", {e.Waiting} waiting" : "")} · id {e.Id}"));
        return string.Join('\n', lines);
    }

    public async Task<string> ResourcesAsync(Guid organizationId, CancellationToken ct = default)
    {
        var rows = await _db.ClubResources.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId)
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Name).Take(50)
            .Select(r => new { r.Id, r.Name, r.Kind, r.Capacity, r.Active, r.BookingMode, r.DepositAmount, r.Currency })
            .ToListAsync(ct);
        if (rows.Count == 0) return "The club has no bookable resources yet.";
        var lines = new List<string> { "Bookable resources:" };
        lines.AddRange(rows.Select(r =>
            $"- {Clip(r.Name, 60)} · {r.Kind} · up to {r.Capacity} people · {r.BookingMode.ToLowerInvariant()}{(r.DepositAmount > 0 ? $" · deposit {Money(r.DepositAmount, r.Currency)}" : "")}{(r.Active ? "" : " · not bookable")} · id {r.Id}"));
        return string.Join('\n', lines);
    }

    public async Task<ClubMemberRef?> MemberRefAsync(Guid organizationId, Guid memberId, CancellationToken ct = default) =>
        await Live(organizationId).Where(m => m.Id == memberId)
            .Select(m => new ClubMemberRef(m.Id, (m.FirstName + " " + m.LastName).Trim(), m.Status, m.Tier))
            .FirstOrDefaultAsync(ct);

    public async Task<ClubResourceRef?> ResourceRefAsync(Guid organizationId, Guid resourceId, CancellationToken ct = default) =>
        await _db.ClubResources.AsNoTracking().Where(r => r.OrganizationId == organizationId && r.Id == resourceId)
            .Select(r => new ClubResourceRef(r.Id, r.Name, r.Kind, r.Capacity, r.Active, r.DepositAmount, r.Currency))
            .FirstOrDefaultAsync(ct);

    public async Task<(string Currency, TimeZoneInfo Zone)> WorkspaceAsync(Guid organizationId, CancellationToken ct = default)
    {
        var org = await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId)
            .Select(o => new { o.Currency, o.TimeZone }).FirstOrDefaultAsync(ct);
        return (string.IsNullOrWhiteSpace(org?.Currency) ? "CHF" : org!.Currency, RegionalSettings.ZoneOf(org?.TimeZone));
    }

    /// <summary>The workspace's members, without the renamed rows of members whose personal data was erased.</summary>
    private IQueryable<ClubMember> Live(Guid organizationId) =>
        _db.ClubMembers.AsNoTracking().Where(m => m.OrganizationId == organizationId && m.ErasedAtUtc == null);

    private static string Name(string first, string last) => Clip($"{first} {last}".Trim(), 80);

    private static string Money(decimal amount, string currency) =>
        $"{currency} {amount.ToString("#,##0.00", CultureInfo.InvariantCulture)}";

    private static string Day(DateTime utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone).ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture);

    private static string When(DateTime utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone).ToString("ddd d MMM HH:mm", CultureInfo.InvariantCulture);

    private static string Clock(DateTime utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone).ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Record text on one line, short, and unable to pose as the end of a data block.</summary>
    private static string Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var text = new StringBuilder(value.Length);
        foreach (var ch in value) text.Append(char.IsControl(ch) ? ' ' : ch);
        var flat = text.ToString().Replace("END DATA", "END-DATA", StringComparison.OrdinalIgnoreCase).Trim();
        return flat.Length <= max ? flat : flat[..max] + "…";
    }
}
