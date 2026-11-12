using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>
/// Reads the record an automation run is about (see <see cref="IAutomationRecordReader"/>),
/// in the workspace being acted for: the facts its conditions test, the dates its waits
/// wait for, the member its emails go to, and where a notification about it links.
/// </summary>
public sealed class AutomationRecordReader : IAutomationRecordReader
{
    private readonly AppDbContext _db;

    public AutomationRecordReader(AppDbContext db) => _db = db;

    public Task<AutomationRecordView> ReadAsync(AutomationSubject subject, CancellationToken ct = default) => subject.RecordType switch
    {
        AutomationRecords.Lead => LeadAsync(subject.RecordId ?? subject.LeadId, ct),
        AutomationRecords.Deal => DealAsync(subject.RecordId ?? subject.DealId, ct),
        AutomationRecords.Task => TaskAsync(subject.RecordId ?? subject.TaskId, ct),
        AutomationRecords.Contract => ContractAsync(subject, ct),
        AutomationRecords.Member => MemberViewAsync(subject.RecordId ?? subject.MemberId, ct),
        AutomationRecords.Subscription => SubscriptionAsync(subject.RecordId, ct),
        AutomationRecords.Reservation => ReservationAsync(subject.RecordId, ct),
        AutomationRecords.Registration => RegistrationAsync(subject.RecordId, ct),
        AutomationRecords.Application => ApplicationAsync(subject.RecordId, ct),
        AutomationRecords.Payment => PaymentAsync(subject.RecordId, ct),
        _ => Task.FromResult(Gone("The record is not one automations know.")),
    };

    private static AutomationRecordView Gone(string why) => new() { Gone = why };

    // ── sales ────────────────────────────────────────────────────────────────

    private async Task<AutomationRecordView> LeadAsync(Guid? id, CancellationToken ct)
    {
        var lead = id == null ? null : await _db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead == null) return Gone("Stopped: the lead was deleted.");
        return new AutomationRecordView
        {
            Name = lead.Name,
            Facts = LeadFacts(lead),
            Link = $"/leads/{lead.Id}",
        };
    }

    private static Dictionary<string, FactValue> LeadFacts(Lead lead) => new(StringComparer.Ordinal)
    {
        ["status"] = FactValue.Of(lead.Status, lead.LeadStatusId?.ToString()),
        ["source"] = FactValue.Of(lead.Source, lead.LeadSourceId?.ToString()),
        ["owner"] = FactValue.OfId(lead.AssignedToUserId),
        ["score"] = FactValue.OfNumber(lead.LeadScore),
        ["email"] = FactValue.Of(lead.Email),
        ["country"] = FactValue.Of(lead.Country),
        ["industry"] = FactValue.Of(lead.Industry),
        ["companySize"] = FactValue.Of(lead.CompanySize),
    };

    private async Task<AutomationRecordView> DealAsync(Guid? id, CancellationToken ct)
    {
        var deal = id == null ? null : await _db.Deals.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (deal == null) return Gone("Stopped: the deal was deleted.");
        var facts = await DealFactsAsync(deal, ct);
        return new AutomationRecordView
        {
            Name = deal.Name,
            Facts = facts,
            Dates = Dates(("expectedClose", deal.ExpectedCloseDateUtc)),
            Link = $"/deals/{deal.Id}",
        };
    }

    private async Task<Dictionary<string, FactValue>> DealFactsAsync(Deal deal, CancellationToken ct)
    {
        // A stage and a pipeline are tested by id or by name, so a rule written with either matches.
        var stageName = deal.DealStageId == null ? null
            : await _db.DealStages.AsNoTracking().Where(s => s.Id == deal.DealStageId).Select(s => s.Name).FirstOrDefaultAsync(ct);
        var pipelineName = deal.PipelineId == null ? null
            : await _db.Pipelines.AsNoTracking().Where(p => p.Id == deal.PipelineId).Select(p => p.Name).FirstOrDefaultAsync(ct);
        return new Dictionary<string, FactValue>(StringComparer.Ordinal)
        {
            ["stage"] = FactValue.Of(deal.DealStageId?.ToString(), stageName, deal.Stage),
            ["pipeline"] = FactValue.Of(deal.PipelineId?.ToString(), pipelineName),
            ["value"] = FactValue.OfNumber(deal.Amount),
            ["currency"] = FactValue.Of(deal.Currency),
            ["owner"] = FactValue.OfId(deal.AssigneeId),
            ["probability"] = FactValue.OfNumber(deal.Probability),
        };
    }

    private async Task<AutomationRecordView> TaskAsync(Guid? id, CancellationToken ct)
    {
        var task = id == null ? null : await _db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task == null) return Gone("Stopped: the task was deleted.");
        return new AutomationRecordView
        {
            Name = task.Title,
            Facts = new Dictionary<string, FactValue>(StringComparer.Ordinal)
            {
                ["title"] = FactValue.Of(task.Title),
                ["priority"] = FactValue.Of(task.Priority.ToString().ToLowerInvariant()),
                ["assignee"] = FactValue.OfId(task.AssigneeId),
            },
            Dates = Dates(("due", task.DueDateUtc)),
            Link = $"/tasks/{task.Id}",
        };
    }

    private async Task<AutomationRecordView> ContractAsync(AutomationSubject subject, CancellationToken ct)
    {
        var contract = subject.ContractId == null ? null
            : await _db.Contracts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == subject.ContractId, ct);
        if (contract == null) return Gone("Stopped: the contract was deleted.");
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal) { ["title"] = FactValue.Of(contract.Title) };
        var dates = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        string link = "/";
        if (contract.DealId is Guid dealId && await _db.Deals.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dealId, ct) is { } deal)
        {
            var dealFacts = await DealFactsAsync(deal, ct);
            facts["stage"] = dealFacts["stage"];
            facts["value"] = dealFacts["value"];
            facts["owner"] = dealFacts["owner"];
            if (deal.ExpectedCloseDateUtc is DateTime close) dates["expectedClose"] = close;
            link = $"/deals/{deal.Id}";
        }
        if (contract.LeadId is Guid leadId && await _db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == leadId, ct) is { } lead)
        {
            facts["status"] = FactValue.Of(lead.Status, lead.LeadStatusId?.ToString());
            if (!facts.ContainsKey("owner")) facts["owner"] = FactValue.OfId(lead.AssignedToUserId);
            if (link == "/") link = $"/leads/{lead.Id}";
        }
        return new AutomationRecordView { Name = contract.Title, Facts = facts, Dates = dates, Link = link };
    }

    // ── club ─────────────────────────────────────────────────────────────────

    private async Task<ClubMember?> MemberAsync(Guid? id, CancellationToken ct) =>
        id == null ? null : await _db.ClubMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id, ct);

    private static string NameOf(ClubMember member) => $"{member.FirstName} {member.LastName}".Trim();

    /// <summary>The member's facts and dates, added to a club record's own.</summary>
    private static void AddMember(ClubMember? member, Dictionary<string, FactValue> facts, Dictionary<string, DateTime> dates)
    {
        facts["tier"] = FactValue.Of(member?.Tier);
        facts["memberStatus"] = FactValue.Of(member?.Status);
        facts["duesFrequency"] = FactValue.Of(member?.DuesFrequency);
        if (member == null) return;
        if (member.RenewsAtUtc > DateTime.MinValue) dates["renewsAt"] = member.RenewsAtUtc;
        if (member.StartsOnUtc is DateTime starts) dates["startsOn"] = starts;
    }

    private static AutomationRecordView ClubView(string name, ClubMember? member, Dictionary<string, FactValue> facts,
        Dictionary<string, DateTime> dates, string link) => new()
    {
        Name = name,
        Facts = facts,
        Dates = dates,
        MemberId = member?.Id,
        MemberName = member == null ? null : NameOf(member),
        MemberEmail = string.IsNullOrWhiteSpace(member?.Email) ? null : member!.Email,
        ContactId = member?.ContactId,
        Link = link,
    };

    private async Task<AutomationRecordView> MemberViewAsync(Guid? id, CancellationToken ct)
    {
        var member = await MemberAsync(id, ct);
        if (member == null) return Gone("Stopped: the member was deleted.");
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal);
        var dates = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        AddMember(member, facts, dates);
        return ClubView(NameOf(member), member, facts, dates, $"/members/{member.Id}");
    }

    private async Task<AutomationRecordView> SubscriptionAsync(Guid? id, CancellationToken ct)
    {
        var subscription = id == null ? null : await _db.ClubSubscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (subscription == null) return Gone("Stopped: the subscription was deleted.");
        var member = await MemberAsync(subscription.MemberId, ct);
        if (member == null) return Gone("Stopped: the member was deleted.");
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal)
        {
            ["subscription"] = FactValue.Of(subscription.Name),
            ["cadence"] = FactValue.Of(subscription.Cadence),
            ["amount"] = FactValue.OfNumber(subscription.Amount),
        };
        var dates = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        if (subscription.NextChargeAtUtc is DateTime next)
        {
            dates["periodEnd"] = next;
            facts["daysUntil"] = FactValue.OfNumber((decimal)Math.Ceiling((next - DateTime.UtcNow).TotalDays));
        }
        AddMember(member, facts, dates);
        return ClubView($"{NameOf(member)}: {subscription.Name}", member, facts, dates, $"/members/{member.Id}");
    }

    private async Task<AutomationRecordView> ReservationAsync(Guid? id, CancellationToken ct)
    {
        var booking = id == null ? null : await _db.ClubReservations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (booking == null) return Gone("Stopped: the booking was deleted.");
        var member = await MemberAsync(booking.MemberId, ct);
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal)
        {
            ["resource"] = FactValue.Of(booking.ResourceId.ToString(), booking.ResourceName),
            ["partySize"] = FactValue.OfNumber(booking.PartySize),
            ["bookingStatus"] = FactValue.Of(booking.Status),
            ["forMember"] = FactValue.OfFlag(member != null),
        };
        var dates = new Dictionary<string, DateTime>(StringComparer.Ordinal) { ["start"] = booking.StartAtUtc, ["end"] = booking.EndAtUtc };
        AddMember(member, facts, dates);
        return ClubView($"{booking.MemberName}: {booking.ResourceName}", member, facts, dates, "/reservations");
    }

    private async Task<AutomationRecordView> RegistrationAsync(Guid? id, CancellationToken ct)
    {
        var place = id == null ? null : await _db.ClubEventRegistrations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (place == null) return Gone("Stopped: the place on the event's list was removed.");
        var ev = await _db.ClubEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == place.EventId, ct);
        if (ev == null) return Gone("Stopped: the event was deleted.");
        var member = await MemberAsync(place.MemberId, ct);
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal)
        {
            ["event"] = FactValue.Of(ev.Id.ToString(), ev.Name),
            ["placeStatus"] = FactValue.Of(place.Status),
            ["placeSource"] = FactValue.Of(place.Source),
            ["ticket"] = FactValue.Of(place.TicketTier),
            ["forMember"] = FactValue.OfFlag(member != null),
        };
        var dates = new Dictionary<string, DateTime>(StringComparer.Ordinal) { ["eventStart"] = ev.StartAtUtc, ["eventEnd"] = ev.EndAtUtc };
        AddMember(member, facts, dates);
        return ClubView($"{place.Name}: {ev.Name}", member, facts, dates, $"/events/{ev.Id}");
    }

    private async Task<AutomationRecordView> ApplicationAsync(Guid? id, CancellationToken ct)
    {
        var app = id == null ? null : await _db.ClubApplications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
        if (app == null) return Gone("Stopped: the application was deleted.");
        var member = await MemberAsync(app.MemberId, ct);
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal)
        {
            ["appliedThrough"] = FactValue.Of(app.Source),
            ["approvedThrough"] = FactValue.Of(app.ApprovalTrigger),
        };
        var dates = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        AddMember(member, facts, dates);
        var name = $"{app.FirstName} {app.LastName}".Trim();
        return ClubView(name, member, facts, dates, member == null ? "/applications" : $"/members/{member.Id}");
    }

    private async Task<AutomationRecordView> PaymentAsync(Guid? id, CancellationToken ct)
    {
        var payment = id == null ? null : await _db.ClubOnlinePayments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (payment == null) return Gone("Stopped: the payment was deleted.");
        var member = await MemberAsync(payment.MemberId, ct);
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal)
        {
            ["amount"] = FactValue.OfNumber(payment.Amount),
            ["paymentKind"] = FactValue.Of(payment.Purpose),
            ["reason"] = FactValue.Of(payment.FailureMessage),
        };
        var dates = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        AddMember(member, facts, dates);
        return ClubView($"{payment.MemberName}: {payment.Description}", member, facts, dates, $"/members/{payment.MemberId}");
    }

    // ── the scan and the workspace ───────────────────────────────────────────

    public async Task<IReadOnlyList<RenewalDue>> RenewalsDueAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await _db.ClubSubscriptions.AsNoTracking()
            .Where(s => (s.Status == "Active" || s.Status == "PastDue")
                && s.NextChargeAtUtc != null && s.NextChargeAtUtc > fromUtc && s.NextChargeAtUtc <= toUtc)
            .OrderBy(s => s.NextChargeAtUtc)
            .Select(s => new RenewalDue(s.Id, s.MemberId, s.MemberName, s.NextChargeAtUtc!.Value))
            .ToListAsync(ct);

    // The organization row: rules set off by the system itself (the renewal scan, a payment
    // Stripe reports) act as the workspace's owner, as a lead from the website does.
    public Task<Guid?> WorkspaceOwnerAsync(Guid organizationId, CancellationToken ct = default) =>
        _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => (Guid?)o.OwnerUserId).FirstOrDefaultAsync(ct);

    public async Task<TimeZoneInfo> ZoneAsync(Guid organizationId, CancellationToken ct = default) =>
        RegionalSettings.ZoneOf(await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => o.TimeZone).FirstOrDefaultAsync(ct));

    private static Dictionary<string, DateTime> Dates(params (string Key, DateTime? Value)[] values) =>
        values.Where(v => v.Value != null).ToDictionary(v => v.Key, v => v.Value!.Value, StringComparer.Ordinal);
}
