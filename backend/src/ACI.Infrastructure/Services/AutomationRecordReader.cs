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
        var facts = LeadFacts(lead);
        var dates = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        var flex = await AddFlexAsync(AutomationRecords.Lead, lead.Id, facts, dates, ct);
        return new AutomationRecordView
        {
            Name = lead.Name,
            Facts = facts,
            Dates = dates,
            FlexFields = flex.Fields,
            FlexDates = flex.Dates,
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
        ["kind"] = FactValue.Of(lead.Kind),
        ["establishmentType"] = FactValue.Of(lead.EstablishmentType),
        ["city"] = FactValue.Of(lead.City),
    };

    private async Task<AutomationRecordView> DealAsync(Guid? id, CancellationToken ct)
    {
        var deal = id == null ? null : await _db.Deals.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id, ct);
        if (deal == null) return Gone("Stopped: the deal was deleted.");
        var facts = await DealFactsAsync(deal, ct);
        var dates = Dates(("expectedClose", deal.ExpectedCloseDateUtc));
        var flex = await AddFlexAsync(AutomationRecords.Deal, deal.Id, facts, dates, ct);
        return new AutomationRecordView
        {
            Name = deal.Name,
            Facts = facts,
            Dates = dates,
            FlexFields = flex.Fields,
            FlexDates = flex.Dates,
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

    /// <summary>
    /// Why a run about a record of this member's stops: their personal data was erased, at their
    /// request or by the retention settings. The member row stays for the books under a pseudonym,
    /// so it is still found, but a task "about Erased member 3", or a notice linking to them, would
    /// be work about someone the club no longer holds. Null for anyone else.
    /// </summary>
    private static string? Erased(ClubMember? member) =>
        member?.ErasedAtUtc != null ? "Stopped: the member's personal data was erased." : null;

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
        Dictionary<string, DateTime> dates, string link, Flex flex) => new()
    {
        Name = name,
        Facts = facts,
        Dates = dates,
        FlexFields = flex.Fields,
        FlexDates = flex.Dates,
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
        if (Erased(member) is { } erased) return Gone(erased);
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal);
        var dates = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        AddMember(member, facts, dates);
        // The member's at-risk score as the daily scoring last worked it out: what a rule on
        // "When a member becomes at risk" can test ("score at least 80").
        var risk = await _db.ClubMemberRisks.AsNoTracking().Where(r => r.MemberId == member.Id).Select(r => (int?)r.Score).FirstOrDefaultAsync(ct);
        facts["riskScore"] = FactValue.OfNumber(risk);
        var flex = await AddFlexAsync(AutomationRecords.Member, member.Id, facts, dates, ct);
        return ClubView(NameOf(member), member, facts, dates, $"/members/{member.Id}", flex);
    }

    private async Task<AutomationRecordView> SubscriptionAsync(Guid? id, CancellationToken ct)
    {
        var subscription = id == null ? null : await _db.ClubSubscriptions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
        if (subscription == null) return Gone("Stopped: the subscription was deleted.");
        // A subscription cancelled or paused does not renew: a run for its renewal stops, or a
        // "your membership renews on Friday" email would reach someone who has just cancelled.
        // Picked up again, it renews at a later period, which sets the rule off afresh.
        if (subscription.Status is "Cancelled" or "Paused")
            return Gone($"Stopped: the subscription was {subscription.Status.ToLowerInvariant()}.");
        var member = await MemberAsync(subscription.MemberId, ct);
        if (member == null) return Gone("Stopped: the member was deleted.");
        if (Erased(member) is { } erased) return Gone(erased);
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
        var flex = await AddFlexAsync(AutomationRecords.Member, member.Id, facts, dates, ct);
        return ClubView($"{NameOf(member)}: {subscription.Name}", member, facts, dates, $"/members/{member.Id}", flex);
    }

    private async Task<AutomationRecordView> ReservationAsync(Guid? id, CancellationToken ct)
    {
        var booking = id == null ? null : await _db.ClubReservations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (booking == null) return Gone("Stopped: the booking was deleted.");
        // A cancelled booking is kept, with its status, for its money and its history; a run
        // about it stops as for one deleted, so its reminder does not reach someone who cancelled.
        if (booking.Status == ReservationStatuses.Cancelled) return Gone("Stopped: the booking was cancelled.");
        var member = await MemberAsync(booking.MemberId, ct);
        if (Erased(member) is { } erased) return Gone(erased);
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal)
        {
            ["resource"] = FactValue.Of(booking.ResourceId.ToString(), booking.ResourceName),
            ["partySize"] = FactValue.OfNumber(booking.PartySize),
            ["bookingStatus"] = FactValue.Of(booking.Status),
            ["forMember"] = FactValue.OfFlag(member != null),
        };
        var dates = new Dictionary<string, DateTime>(StringComparer.Ordinal) { ["start"] = booking.StartAtUtc, ["end"] = booking.EndAtUtc };
        AddMember(member, facts, dates);
        var flex = await AddFlexAsync(AutomationRecords.Member, member?.Id, facts, dates, ct);
        return ClubView($"{booking.MemberName}: {booking.ResourceName}", member, facts, dates, "/reservations", flex);
    }

    private async Task<AutomationRecordView> RegistrationAsync(Guid? id, CancellationToken ct)
    {
        var place = id == null ? null : await _db.ClubEventRegistrations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (place == null) return Gone("Stopped: the place on the event's list was removed.");
        var ev = await _db.ClubEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == place.EventId, ct);
        if (ev == null) return Gone("Stopped: the event was deleted.");
        // Cancelled places and events stay on the list with their status: a run about one stops,
        // so nobody who cancelled, or whose event was called off, is reminded to come.
        if (ev.Status == ClubEventStatuses.Cancelled) return Gone("Stopped: the event was cancelled.");
        if (place.Status == RegistrationStatuses.Cancelled) return Gone("Stopped: the place on the event's list was cancelled.");
        var member = await MemberAsync(place.MemberId, ct);
        if (Erased(member) is { } erased) return Gone(erased);
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
        var flex = await AddFlexAsync(AutomationRecords.Member, member?.Id, facts, dates, ct);
        return ClubView($"{place.Name}: {ev.Name}", member, facts, dates, $"/events/{ev.Id}", flex);
    }

    private async Task<AutomationRecordView> ApplicationAsync(Guid? id, CancellationToken ct)
    {
        var app = id == null ? null : await _db.ClubApplications.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
        if (app == null) return Gone("Stopped: the application was deleted.");
        var member = await MemberAsync(app.MemberId, ct);
        if (Erased(member) is { } erased) return Gone(erased);
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal)
        {
            ["appliedThrough"] = FactValue.Of(app.Source),
            ["approvedThrough"] = FactValue.Of(app.ApprovalTrigger),
        };
        var dates = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        AddMember(member, facts, dates);
        var name = $"{app.FirstName} {app.LastName}".Trim();
        var flex = await AddFlexAsync(AutomationRecords.Member, member?.Id, facts, dates, ct);
        return ClubView(name, member, facts, dates, member == null ? "/applications" : $"/members/{member.Id}", flex);
    }

    private async Task<AutomationRecordView> PaymentAsync(Guid? id, CancellationToken ct)
    {
        var payment = id == null ? null : await _db.ClubOnlinePayments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        if (payment == null) return Gone("Stopped: the payment was deleted.");
        var member = await MemberAsync(payment.MemberId, ct);
        if (Erased(member) is { } erased) return Gone(erased);
        var facts = new Dictionary<string, FactValue>(StringComparer.Ordinal)
        {
            ["amount"] = FactValue.OfNumber(payment.Amount),
            ["paymentKind"] = FactValue.Of(payment.Purpose),
            ["reason"] = FactValue.Of(payment.FailureMessage),
        };
        var dates = new Dictionary<string, DateTime>(StringComparer.Ordinal);
        AddMember(member, facts, dates);
        var flex = await AddFlexAsync(AutomationRecords.Member, member?.Id, facts, dates, ct);
        return ClubView($"{payment.MemberName}: {payment.Description}", member, facts, dates, $"/members/{payment.MemberId}", flex);
    }

    // ── tags and custom fields ───────────────────────────────────────────────

    /// <summary>The workspace's own fields a record was read with, named for the run log.</summary>
    private sealed record Flex(IReadOnlyList<AutomationField> Fields, IReadOnlyList<AutomationDateField> Dates);

    /// <summary>
    /// Adds the record's tags and custom fields to its facts, as conditions test them
    /// (<see cref="AutomationCatalog.FlexFields"/>), and its custom dates to the dates a wait can
    /// wait for. For the club's records they are the member's; a record without a member has
    /// none, so "tags is VIP" does not hold for it and "tags is empty" does.
    /// </summary>
    private async Task<Flex> AddFlexAsync(string automationRecord, Guid? recordId, Dictionary<string, FactValue> facts,
        Dictionary<string, DateTime> dates, CancellationToken ct)
    {
        var entityType = AutomationCatalog.FlexRecordType(automationRecord);
        if (entityType == null) return new Flex([], []);
        var definitions = await _db.CustomFieldDefinitions.AsNoTracking()
            .Where(d => d.EntityType == entityType)
            .OrderBy(d => d.Order).ThenBy(d => d.CreatedAtUtc)
            .ToListAsync(ct);
        var tags = (await _db.Tags.AsNoTracking().OrderBy(t => t.Name).Select(t => new { t.Id, t.Name }).ToListAsync(ct))
            .Select(t => new AutomationChoice(t.Id.ToString(), t.Name)).ToList();
        var flex = new Flex(AutomationCatalog.FlexFields(automationRecord, definitions, tags), AutomationCatalog.FlexDates(automationRecord, definitions));
        if (recordId is not Guid id) return flex;

        var carried = (await _db.RecordTags.AsNoTracking().Where(t => t.RecordId == id).Select(t => t.TagId).ToListAsync(ct))
            .Select(t => t.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        facts[AutomationCatalog.TagsField] = FactValue.Of(tags.Where(t => carried.Contains(t.Value)).SelectMany(t => new[] { t.Value, t.Label }).ToArray());
        if (definitions.Count == 0) return flex;

        var entries = await _db.CustomFieldEntries.AsNoTracking().Where(e => e.RecordId == id && e.EntityType == entityType).ToListAsync(ct);
        var names = await LinkNamesAsync(definitions, entries, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var definition in definitions)
        {
            var key = AutomationCatalog.CustomFieldPrefix + definition.Key;
            var rows = entries.Where(e => e.FieldId == definition.Id).ToList();
            var first = rows.FirstOrDefault();
            switch (definition.Type)
            {
                case CustomFieldTypes.Number or CustomFieldTypes.Money:
                    facts[key] = FactValue.OfNumber(first?.NumberValue);
                    break;
                // A box never ticked reads as unticked, as it does on the record and in the lists.
                case CustomFieldTypes.Checkbox:
                    facts[key] = FactValue.OfFlag(first?.BoolValue == true);
                    break;
                case CustomFieldTypes.Date:
                    if (first?.DateValue is DateOnly date)
                    {
                        facts[key] = FactValue.OfNumber(date.DayNumber - today.DayNumber);
                        dates[key] = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
                    }
                    break;
                case CustomFieldTypes.Link:
                    if (first?.LinkId is Guid link) facts[key] = FactValue.Of(link.ToString(), names.GetValueOrDefault(link));
                    break;
                default:
                    facts[key] = FactValue.Of(rows.Select(r => r.TextValue).ToArray());
                    break;
            }
        }
        return flex;
    }

    /// <summary>The names of the records link fields point at, so "is" and "contains" test them by name.</summary>
    private async Task<Dictionary<Guid, string>> LinkNamesAsync(
        IReadOnlyList<CustomFieldDefinition> definitions, IReadOnlyList<CustomFieldEntry> entries, CancellationToken ct)
    {
        var names = new Dictionary<Guid, string>();
        foreach (var group in definitions.Where(d => d.Type == CustomFieldTypes.Link).GroupBy(d => d.LinkEntityType))
        {
            var fieldIds = group.Select(d => d.Id).ToHashSet();
            var ids = entries.Where(e => fieldIds.Contains(e.FieldId) && e.LinkId != null).Select(e => e.LinkId!.Value).Distinct().ToList();
            if (ids.Count == 0) continue;
            IQueryable<LinkName>? named = group.Key switch
            {
                RecordTypes.Lead => _db.Leads.AsNoTracking().Where(r => ids.Contains(r.Id)).Select(r => new LinkName(r.Id, r.Name)),
                RecordTypes.Contact => _db.Contacts.AsNoTracking().Where(r => ids.Contains(r.Id)).Select(r => new LinkName(r.Id, r.Name)),
                RecordTypes.Company => _db.Companies.AsNoTracking().Where(r => ids.Contains(r.Id)).Select(r => new LinkName(r.Id, r.Name)),
                RecordTypes.Deal => _db.Deals.AsNoTracking().Where(r => ids.Contains(r.Id)).Select(r => new LinkName(r.Id, r.Name)),
                RecordTypes.Member => _db.ClubMembers.AsNoTracking().Where(r => ids.Contains(r.Id) && r.ErasedAtUtc == null)
                    .Select(r => new LinkName(r.Id, (r.FirstName + " " + r.LastName).Trim())),
                _ => null,
            };
            if (named == null) continue;
            foreach (var record in await named.ToListAsync(ct)) names[record.Id] = record.Name;
        }
        return names;
    }

    private sealed record LinkName(Guid Id, string Name);

    // ── the scan and the workspace ───────────────────────────────────────────

    // A member's dues only: a company's (a corporate membership's, on the company's account) has
    // no member for a rule's email or task to be about, and the corporate membership renews on its own.
    public async Task<IReadOnlyList<RenewalDue>> RenewalsDueAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await _db.ClubSubscriptions.AsNoTracking()
            .Where(s => (s.Status == "Active" || s.Status == "PastDue")
                && s.NextChargeAtUtc != null && s.NextChargeAtUtc > fromUtc && s.NextChargeAtUtc <= toUtc
                && _db.ClubMembers.Any(m => m.Id == s.MemberId))
            .OrderBy(s => s.NextChargeAtUtc)
            .Select(s => new RenewalDue(s.Id, s.MemberId, s.MemberName, s.NextChargeAtUtc!.Value))
            .ToListAsync(ct);

    // The organization row: rules set off by the system itself (the renewal scan, a payment
    // Stripe reports) act as the workspace's owner, as a lead from the website does.
    public Task<Guid?> WorkspaceOwnerAsync(Guid organizationId, CancellationToken ct = default) =>
        _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => (Guid?)o.OwnerUserId).FirstOrDefaultAsync(ct);

    public async Task<AutomationWorkspaceFields> WorkspaceFieldsAsync(CancellationToken ct = default) => new(
        await _db.CustomFieldDefinitions.AsNoTracking().OrderBy(d => d.EntityType).ThenBy(d => d.Order).ThenBy(d => d.CreatedAtUtc).ToListAsync(ct),
        (await _db.Tags.AsNoTracking().OrderBy(t => t.Name).Select(t => new { t.Id, t.Name }).ToListAsync(ct))
            .Select(t => new AutomationChoice(t.Id.ToString(), t.Name)).ToList());

    public async Task<TimeZoneInfo> ZoneAsync(Guid organizationId, CancellationToken ct = default) =>
        RegionalSettings.ZoneOf(await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => o.TimeZone).FirstOrDefaultAsync(ct));

    private static Dictionary<string, DateTime> Dates(params (string Key, DateTime? Value)[] values) =>
        values.Where(v => v.Value != null).ToDictionary(v => v.Key, v => v.Value!.Value, StringComparer.Ordinal);
}
