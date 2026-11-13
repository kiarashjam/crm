using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.Infrastructure.Services;
using ACI.WebApi.Mcp;

namespace ACI.WebApi.Assistant;

/// <summary>
/// Who the assistant works for in one request: the person and workspace its tools read as,
/// what that person may do there, and which modules are on.
/// </summary>
public sealed record AssistantScope(
    ToolActor Actor,
    IReadOnlySet<string> Permissions,
    bool SalesOn,
    bool ClubOn,
    TimeZoneInfo Zone,
    string Currency,
    DateTime NowUtc)
{
    public bool May(string permission) => Permissions.Contains(permission);
}

/// <summary>
/// The tools the language model may call, as the signed-in person: lookups (the MCP endpoint's,
/// the club's) and proposals of changes.
/// </summary>
/// <remarks>
/// <para>
/// A tool never shows or does more than the person could on their own pages. The lookups read
/// through the same services as those pages, as that person, so a private deal they cannot see
/// is not found, and each module's tools are offered only while the module is on.
/// </para>
/// <para>
/// A change is never made here. A propose_ tool is offered only to someone whose role allows
/// that change, checks that every record it names is one they can see in this workspace, and
/// returns a proposal: the app shows it, and only the person's Confirm sends it to the same
/// endpoint their page would, which checks their permission again.
/// </para>
/// </remarks>
public sealed class AssistantTools
{
    private const int DefaultBookingMinutes = 120;

    private readonly CrmMcpTools _crm;
    private readonly IClubAssistantReads _club;
    private readonly ILeadService _leads;
    private readonly IDealService _deals;
    private readonly IContactService _contacts;
    private readonly IDealStageService _stages;
    private readonly IMorningBrief _brief;
    private readonly ILogger<AssistantTools> _logger;

    public AssistantTools(
        CrmMcpTools crm,
        IClubAssistantReads club,
        ILeadService leads,
        IDealService deals,
        IContactService contacts,
        IDealStageService stages,
        IMorningBrief brief,
        ILogger<AssistantTools> logger)
    {
        _crm = crm;
        _club = club;
        _leads = leads;
        _deals = deals;
        _contacts = contacts;
        _stages = stages;
        _brief = brief;
        _logger = logger;
    }

    /// <summary>The MCP tools that read records of both modules (contacts and tasks belong to both).</summary>
    private static readonly HashSet<string> SharedCrmTools = ["list_contacts", "get_contact", "list_tasks", "list_activities"];

    /// <summary>The tools this person may be offered here, with what each does and its arguments.</summary>
    public IReadOnlyList<AssistantTool> ToolsFor(AssistantScope scope)
    {
        var tools = new List<AssistantTool>();
        foreach (var definition in CrmMcpTools.Definitions().OfType<JsonObject>())
        {
            var name = definition["name"]!.GetValue<string>();
            if (!scope.SalesOn && !SharedCrmTools.Contains(name)) continue;
            tools.Add(new AssistantTool(name, definition["description"]!.GetValue<string>(), (JsonObject)definition["inputSchema"]!.DeepClone()));
        }

        // The person's own day, as the morning brief puts it: what their role lets them see.
        tools.Add(Tool("my_brief", "The person's day today, as their morning brief puts it: their tasks due and overdue, their deals gone quiet, and as their role allows members past due, today's bookings and events, and renewals coming up.",
            Props()));

        if (scope.ClubOn)
        {
            tools.Add(Tool("search_members", "Find club members by name or email, with their tier, status, balance and renewal date.",
                Props(("query", Str("Name or email to look for; leave out for the newest members.")), ("limit", Int("How many (1–25, default 10).", 1, 25)))));
            tools.Add(Tool("get_member", "One club member: membership, house account (balance, open and past-due charges), coming bookings and last check-in.",
                Props(("id", Str("Member id (GUID)."))), "id"));
            tools.Add(Tool("club_today", "Today at the club: check-ins, today's bookings, open tabs, applications waiting, charges past due, events this week.",
                Props()));
            tools.Add(Tool("renewals_due", "Active memberships renewing in the next N days.",
                Props(("days", Int("Days ahead (1–120, default 30).", 1, 120)))));
            tools.Add(Tool("upcoming_events", "Published club events in the next N days, with seats taken and waiting lists.",
                Props(("days", Int("Days ahead (1–120, default 14).", 1, 120)))));
            tools.Add(Tool("list_resources", "The club's bookable rooms, tables and courts, with their ids, to propose a booking.",
                Props()));
        }

        if (scope.May(MemberPermissions.EditRecords))
        {
            tools.Add(Tool("propose_task", "Propose a task for the person to confirm. Nothing is created until they press Confirm.",
                Props(
                    ("title", Str("What to do, as the task's title.")),
                    ("due_date", Str("Due date, yyyy-MM-dd, on the workspace's calendar. Leave out for none.")),
                    ("lead_id", Str("Lead it is about (GUID), if any.")),
                    ("deal_id", Str("Deal it is about (GUID), if any.")),
                    ("contact_id", Str("Contact it is about (GUID), if any.")),
                    ("notes", Str("More detail for the task, if any."))),
                "title"));
            tools.Add(Tool("propose_log_activity", "Propose logging a call, meeting, email or note on a lead, deal or contact, for the person to confirm.",
                Props(
                    ("type", Enum("What happened.", "call", "meeting", "email", "note")),
                    ("subject", Str("A short subject.")),
                    ("body", Str("What was said or agreed.")),
                    ("outcome", Str("For a call: connected, no_answer, left_voicemail, left_message, busy, wrong_number. For a meeting: completed, no_show, rescheduled, cancelled.")),
                    ("direction", Enum("For a call or an email: who reached out.", "inbound", "outbound")),
                    ("duration_minutes", Int("For a call or meeting: how long it took.", 0, ActivityFacts.MaxDurationMinutes)),
                    ("occurred_at", Str("When it happened, yyyy-MM-dd HH:mm on the workspace's clock; leave out for now.")),
                    ("lead_id", Str("Lead (GUID).")),
                    ("deal_id", Str("Deal (GUID).")),
                    ("contact_id", Str("Contact (GUID)."))),
                "type"));
            if (scope.SalesOn)
            {
                tools.Add(Tool("propose_deal_stage", "Propose moving a deal to another stage of its pipeline, for the person to confirm.",
                    Props(("deal_id", Str("Deal (GUID).")), ("stage", Str("The stage's name or id, from the deal's own pipeline."))),
                    "deal_id", "stage"));
            }
            tools.Add(Tool("propose_email_draft", "Propose an email to a lead, contact or deal's contact. It opens in the email composer for the person to read, change and send; nothing is sent from here.",
                Props(
                    ("lead_id", Str("Lead to write to (GUID).")),
                    ("contact_id", Str("Contact to write to (GUID).")),
                    ("deal_id", Str("Deal whose contact to write to (GUID).")),
                    ("subject", Str("Subject line.")),
                    ("body", Str("The email's text, ready to read."))),
                "subject", "body"));
        }

        if (scope.ClubOn && scope.May(MemberPermissions.FrontDesk))
        {
            tools.Add(Tool("propose_booking", "Propose booking a resource for a member, for the person to confirm. A clash or too many people is refused when they confirm; a resource that takes a deposit charges it to the member's house account when they confirm.",
                Props(
                    ("member_id", Str("Member (GUID).")),
                    ("resource_id", Str("Resource (GUID), from list_resources.")),
                    ("start", Str("Start, yyyy-MM-dd HH:mm on the club's clock.")),
                    ("duration_minutes", Int($"How long (15–1440, default {DefaultBookingMinutes}).", 15, 1440)),
                    ("party_size", Int("How many people (default 2).", 1, 500)),
                    ("notes", Str("Notes for the booking, if any."))),
                "member_id", "resource_id", "start"));
        }

        if (scope.ClubOn && scope.May(MemberPermissions.ManageMoney))
        {
            tools.Add(Tool("propose_charge", "Propose a charge to a member's house account, for the person to confirm.",
                Props(
                    ("member_id", Str("Member (GUID).")),
                    ("description", Str("What it is for, as the statement shows it.")),
                    ("amount", new JsonObject { ["type"] = "number", ["description"] = $"Amount in {scope.Currency}, more than 0.", ["exclusiveMinimum"] = 0 }),
                    ("kind", Enum("Kind of charge.", ClubLedgerService.ChargeKinds))),
                "member_id", "description", "amount"));
        }
        return tools;
    }

    /// <summary>
    /// Carries out one call: a lookup's text, or for a proposal the line the model is told (and
    /// the proposal, added to <paramref name="proposals"/>). A call the person may not make, or
    /// that names a record they cannot see, is an error the model reads, never an exception.
    /// </summary>
    public async Task<string> RunAsync(AssistantScope scope, AssistantToolCall call, List<AssistantProposal> proposals, CancellationToken ct)
    {
        // Only a tool that was offered may run: the model cannot reach one by naming it.
        if (ToolsFor(scope).All(t => t.Name != call.Name))
            return $"Error: there is no tool called {call.Name} for this person here.";
        try
        {
            if (CrmMcpTools.Names.Contains(call.Name))
                return (await _crm.RunAsync(scope.Actor, call.Name, call.Arguments, ct)).Text;
            var org = scope.Actor.OrganizationId;
            var args = call.Arguments;
            return call.Name switch
            {
                "search_members" => await _club.SearchMembersAsync(org, Text(args, "query", 100), Int(args, "limit") ?? 10, ct),
                "get_member" => Id(args, "id") is Guid id ? await _club.MemberAsync(org, id, ct) ?? "Error: no such member in this club." : "Error: id must be a member's GUID.",
                "my_brief" => _brief.PlainText(await _brief.BuildAsync(org, scope.Actor.UserId, scope.NowUtc, ct)),
                "club_today" => await _club.TodayAsync(org, ct),
                "renewals_due" => await _club.RenewalsDueAsync(org, Int(args, "days") ?? 30, ct),
                "upcoming_events" => await _club.UpcomingEventsAsync(org, Int(args, "days") ?? 14, ct),
                "list_resources" => await _club.ResourcesAsync(org, ct),
                "propose_task" => await ProposeTaskAsync(scope, args, proposals, ct),
                "propose_log_activity" => await ProposeActivityAsync(scope, args, proposals, ct),
                "propose_deal_stage" => await ProposeDealStageAsync(scope, args, proposals, ct),
                "propose_email_draft" => await ProposeEmailAsync(scope, args, proposals, ct),
                "propose_booking" => await ProposeBookingAsync(scope, args, proposals, ct),
                "propose_charge" => await ProposeChargeAsync(scope, args, proposals, ct),
                _ => $"Error: there is no tool called {call.Name}.",
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Assistant tool {Tool} failed for organization {OrganizationId}", call.Name, scope.Actor.OrganizationId);
            return "Error: that lookup failed. Try again in a moment.";
        }
    }

    // ── proposals ──────────────────────────────────────────────────────────

    private async Task<string> ProposeTaskAsync(AssistantScope scope, JsonObject args, List<AssistantProposal> proposals, CancellationToken ct)
    {
        var title = Text(args, "title", 200);
        if (title == null) return "Error: a task needs a title.";
        var (record, refusal) = await RecordAsync(scope, args, required: false, ct);
        if (refusal != null) return refusal;
        DateOnly? due = null;
        if (Text(args, "due_date", 20) is { } dueText)
        {
            if (!DateOnly.TryParseExact(dueText, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                return "Error: due_date must be yyyy-MM-dd.";
            due = day;
        }
        var notes = Text(args, "notes", 2000);

        var payload = new JsonObject { ["title"] = title };
        if (notes != null) payload["description"] = notes;
        // The end of the day on the workspace's calendar, as the task form sets a date with no time.
        if (due is DateOnly d) payload["dueDateUtc"] = UtcIso(TimeZoneInfo.ConvertTimeToUtc(d.ToDateTime(new TimeOnly(17, 0)), scope.Zone));
        record?.AddTo(payload);
        var details = new List<string> { $"Task: {title}" };
        if (due is DateOnly shown) details.Add($"Due: {shown.ToString("ddd d MMM yyyy", CultureInfo.InvariantCulture)}");
        if (record != null) details.Add($"On {record.Noun}: {record.Name}");
        if (notes != null) details.Add($"Notes: {Clip(notes, 160)}");
        return Propose(proposals, AssistantProposalKinds.Task, "Create a task", details, payload, record?.Link);
    }

    private async Task<string> ProposeActivityAsync(AssistantScope scope, JsonObject args, List<AssistantProposal> proposals, CancellationToken ct)
    {
        var type = Text(args, "type", 20)?.ToLowerInvariant();
        if (type is not ("call" or "meeting" or "email" or "note")) return "Error: type must be call, meeting, email or note.";
        var (record, refusal) = await RecordAsync(scope, args, required: true, ct);
        if (refusal != null) return refusal;
        var payload = new JsonObject { ["type"] = type };
        var details = new List<string> { $"Log a {type} on {record!.Noun} {record.Name}" };
        if (Text(args, "subject", 200) is { } subject) { payload["subject"] = subject; details.Add($"Subject: {subject}"); }
        if (Text(args, "body", 5000) is { } body) { payload["body"] = body; details.Add($"Text: {Clip(body, 200)}"); }
        if (ActivityFacts.Word(Text(args, "outcome", 32)) is { } outcome)
        {
            if (!ActivityFacts.OutcomesFor(type).Contains(outcome))
                return $"Error: a {type} has no outcome '{outcome}'.";
            payload["outcome"] = outcome;
            details.Add($"Outcome: {outcome.Replace('_', ' ')}");
        }
        if (ActivityFacts.Word(Text(args, "direction", 16)) is { } direction)
        {
            if (!ActivityFacts.HasDirection(type) || !ActivityFacts.Directions.Contains(direction))
                return $"Error: direction is inbound or outbound, for a call or an email.";
            payload["direction"] = direction;
            details.Add($"Direction: {direction}");
        }
        if (Int(args, "duration_minutes") is int minutes)
        {
            if (!ActivityFacts.HasDuration(type) || minutes < 0 || minutes > ActivityFacts.MaxDurationMinutes)
                return "Error: a duration is for a call or a meeting, from 0 to 1440 minutes.";
            payload["durationMinutes"] = minutes;
            details.Add($"Duration: {minutes} min");
        }
        if (Text(args, "occurred_at", 20) is { } when)
        {
            if (!TryLocal(when, scope.Zone, out var at)) return "Error: occurred_at must be yyyy-MM-dd HH:mm.";
            if (at > scope.NowUtc.Add(ActivityFacts.ClockSkew)) return "Error: an activity that has not happened yet cannot be logged; propose a task for it instead.";
            payload["occurredAtUtc"] = UtcIso(at);
            details.Add($"When: {Local(at, scope.Zone)}");
        }
        record.AddTo(payload);
        return Propose(proposals, AssistantProposalKinds.Activity, $"Log a {type}", details, payload, record.Link);
    }

    private async Task<string> ProposeDealStageAsync(AssistantScope scope, JsonObject args, List<AssistantProposal> proposals, CancellationToken ct)
    {
        if (Id(args, "deal_id") is not Guid dealId) return "Error: deal_id must be a deal's GUID.";
        var deal = await _deals.GetByIdAsync(dealId, scope.Actor.UserId, scope.Actor.OrganizationId, ct);
        if (deal.IsFailure) return "Error: that deal was not found in this workspace.";
        if (deal.Value.PipelineId is not Guid pipelineId) return "Error: that deal has no pipeline, so its stage cannot be chosen here. Open the deal to set one.";
        var stages = await _stages.GetByPipelineIdAsync(pipelineId, scope.Actor.OrganizationId, ct);
        if (stages.IsFailure || stages.Value.Count == 0) return "Error: the deal's pipeline has no stages.";
        var asked = Text(args, "stage", 120);
        if (asked == null) return "Error: say which stage.";
        var stage = Guid.TryParse(asked, out var stageId)
            ? stages.Value.FirstOrDefault(s => s.Id == stageId)
            : stages.Value.FirstOrDefault(s => string.Equals(s.Name.Trim(), asked, StringComparison.OrdinalIgnoreCase));
        if (stage == null)
            return $"Error: the deal's pipeline has no stage '{Clip(asked, 60)}'. Its stages are: {string.Join(", ", stages.Value.OrderBy(s => s.DisplayOrder).Select(s => s.Name))}.";
        if (stage.Id == deal.Value.DealStageId) return $"The deal is already at {stage.Name}; nothing to propose.";

        var payload = new JsonObject { ["dealId"] = dealId.ToString(), ["dealStageId"] = stage.Id.ToString() };
        // The deal as it was read: if anyone changes it before the person confirms, the move is
        // refused (409) rather than made on top of their change.
        if (deal.Value.Version is Guid version) payload["version"] = version.ToString();
        var details = new List<string>
        {
            $"Deal: {Clip(deal.Value.Name, 120)}",
            $"From {deal.Value.DealStageName ?? deal.Value.Stage ?? "no stage"} to {stage.Name}",
        };
        if (stage.IsWon) details.Add("This stage marks the deal won.");
        if (stage.IsLost) details.Add("This stage marks the deal lost.");
        return Propose(proposals, AssistantProposalKinds.DealStage, "Move a deal", details, payload, $"/deals/{dealId}");
    }

    private async Task<string> ProposeEmailAsync(AssistantScope scope, JsonObject args, List<AssistantProposal> proposals, CancellationToken ct)
    {
        var subject = Text(args, "subject", 200);
        var body = Text(args, "body", 10000, keepLines: true);
        if (subject == null || body == null) return "Error: an email needs a subject and a text.";
        var (record, refusal) = await RecordAsync(scope, args, required: true, ct);
        if (refusal != null) return refusal;
        if (string.IsNullOrWhiteSpace(record!.Email)) return $"Error: {record.Noun} {record.Name} has no email address.";
        if (record.DoNotContact) return $"Error: {record.Name} asked not to be contacted, so no email is proposed.";

        var payload = new JsonObject { ["to"] = record.Email, ["subject"] = subject, ["body"] = body };
        record.AddTo(payload);
        var details = new List<string> { $"To: {record.Name} <{record.Email}>", $"Subject: {subject}", Clip(body, 300) };
        return Propose(proposals, AssistantProposalKinds.EmailDraft, "Open an email in the composer", details, payload, record.Link);
    }

    private async Task<string> ProposeBookingAsync(AssistantScope scope, JsonObject args, List<AssistantProposal> proposals, CancellationToken ct)
    {
        var org = scope.Actor.OrganizationId;
        if (Id(args, "member_id") is not Guid memberId) return "Error: member_id must be a member's GUID.";
        if (Id(args, "resource_id") is not Guid resourceId) return "Error: resource_id must be a resource's GUID.";
        var member = await _club.MemberRefAsync(org, memberId, ct);
        if (member == null) return "Error: no such member in this club.";
        var resource = await _club.ResourceRefAsync(org, resourceId, ct);
        if (resource == null) return "Error: no such resource in this club.";
        if (!resource.Active) return $"Error: {resource.Name} is not bookable at the moment.";
        if (Text(args, "start", 20) is not { } startText || !TryLocal(startText, scope.Zone, out var start))
            return "Error: start must be yyyy-MM-dd HH:mm.";
        if (start < scope.NowUtc) return "Error: that time has passed.";
        var minutes = Math.Clamp(Int(args, "duration_minutes") ?? DefaultBookingMinutes, 15, 1440);
        var party = Math.Clamp(Int(args, "party_size") ?? 2, 1, 500);
        var end = start.AddMinutes(minutes);
        var notes = Text(args, "notes", 1000);

        var payload = new JsonObject
        {
            ["resourceId"] = resourceId.ToString(),
            ["memberId"] = memberId.ToString(),
            ["memberName"] = member.Name,
            ["partySize"] = party,
            ["startAtUtc"] = UtcIso(start),
            ["endAtUtc"] = UtcIso(end),
            ["status"] = "Confirmed",
        };
        if (notes != null) payload["notes"] = notes;
        var details = new List<string>
        {
            $"Book {resource.Name} for {member.Name}",
            $"{Local(start, scope.Zone)} to {Local(end, scope.Zone).Split(' ').Last()}, {party} {(party == 1 ? "person" : "people")}",
        };
        if (party > resource.Capacity && resource.Capacity > 0) details.Add($"Note: {resource.Name} holds {resource.Capacity}; the booking will be refused as it stands.");
        // Confirming books through the bookings endpoint, which charges the resource's deposit to
        // the member's house account (its default for a deposit): the card says so, so nobody
        // confirms a booking without knowing it moves money.
        if (resource.DepositAmount > 0)
            details.Add($"Confirming charges a deposit of {resource.Currency} {resource.DepositAmount.ToString("#,##0.00", CultureInfo.InvariantCulture)} to {member.Name}'s house account, given back when they are checked in.");
        if (notes != null) details.Add($"Notes: {Clip(notes, 160)}");
        return Propose(proposals, AssistantProposalKinds.Booking, "Book a resource", details, payload, $"/members/{memberId}");
    }

    private async Task<string> ProposeChargeAsync(AssistantScope scope, JsonObject args, List<AssistantProposal> proposals, CancellationToken ct)
    {
        if (Id(args, "member_id") is not Guid memberId) return "Error: member_id must be a member's GUID.";
        var member = await _club.MemberRefAsync(scope.Actor.OrganizationId, memberId, ct);
        if (member == null) return "Error: no such member in this club.";
        var description = Text(args, "description", 200);
        if (description == null) return "Error: a charge needs a description.";
        if (Decimal(args, "amount") is not decimal amount || amount <= 0 || amount > 1_000_000m)
            return "Error: amount must be a number above 0.";
        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        var kind = Text(args, "kind", 20) is { } k && ClubLedgerService.ChargeKinds.FirstOrDefault(c => string.Equals(c, k, StringComparison.OrdinalIgnoreCase)) is { } known
            ? known : "Other";

        var payload = new JsonObject
        {
            ["memberId"] = memberId.ToString(),
            ["memberName"] = member.Name,
            ["kind"] = kind,
            ["description"] = description,
            ["amount"] = amount,
        };
        var details = new List<string>
        {
            $"Charge {member.Name}'s house account",
            $"{description}: {scope.Currency} {amount.ToString("#,##0.00", CultureInfo.InvariantCulture)} ({kind})",
        };
        return Propose(proposals, AssistantProposalKinds.Charge, "Charge a house account", details, payload, $"/members/{memberId}");
    }

    private static string Propose(List<AssistantProposal> proposals, string kind, string title, List<string> details, JsonObject payload, string? link)
    {
        if (proposals.Count >= 5) return "Error: five changes are proposed already; ask the person to confirm those first.";
        proposals.Add(new AssistantProposal(Guid.NewGuid().ToString("N"), kind, title, details, payload, link));
        return $"Proposed to the person as a card they must confirm (nothing is changed yet): {title}. {string.Join(" · ", details)}";
    }

    /// <summary>The lead, deal or contact a proposal names, as the person sees it; or why it cannot be used.</summary>
    private async Task<(RecordRef? Record, string? Refusal)> RecordAsync(AssistantScope scope, JsonObject args, bool required, CancellationToken ct)
    {
        var leadId = Id(args, "lead_id");
        var dealId = Id(args, "deal_id");
        var contactId = Id(args, "contact_id");
        var named = new[] { leadId, dealId, contactId }.Count(x => x != null);
        if (named > 1) return (null, "Error: name one record: a lead, a deal or a contact.");
        if (named == 0) return required ? (null, "Error: say which lead, deal or contact it is for (its GUID).") : (null, null);
        var (user, org) = (scope.Actor.UserId, scope.Actor.OrganizationId);
        if (leadId is Guid l)
        {
            if (!scope.SalesOn) return (null, "Error: leads belong to the Sales module, which is off here.");
            var lead = await _leads.GetByIdAsync(l, user, org, ct);
            return lead.IsSuccess
                // A lead has no do-not-contact flag of its own: the outbox refuses an address on the
                // suppression list (unsubscribed, bounced, erased) when the email is sent.
                ? (new RecordRef("lead", lead.Value.Name, lead.Value.Email, false, $"/leads/{l}", "leadId", l), null)
                : (null, "Error: that lead was not found in this workspace.");
        }
        if (dealId is Guid d)
        {
            if (!scope.SalesOn) return (null, "Error: deals belong to the Sales module, which is off here.");
            var deal = await _deals.GetByIdAsync(d, user, org, ct);
            if (deal.IsFailure) return (null, "Error: that deal was not found in this workspace.");
            string? email = null;
            var doNotContact = false;
            if (deal.Value.ContactId is Guid contactOfDeal)
            {
                var contact = await _contacts.GetByIdAsync(contactOfDeal, user, org, ct);
                if (contact.IsSuccess) (email, doNotContact) = (contact.Value.Email, contact.Value.DoNotContact);
            }
            return (new RecordRef("deal", deal.Value.Name, email, doNotContact, $"/deals/{d}", "dealId", d), null);
        }
        var found = await _contacts.GetByIdAsync(contactId!.Value, user, org, ct);
        return found.IsSuccess
            ? (new RecordRef("contact", found.Value.Name, found.Value.Email, found.Value.DoNotContact, $"/contacts/{contactId}", "contactId", contactId.Value), null)
            : (null, "Error: that contact was not found in this workspace.");
    }

    private sealed record RecordRef(string Noun, string Name, string? Email, bool DoNotContact, string Link, string Key, Guid Id)
    {
        public void AddTo(JsonObject payload) => payload[Key] = Id.ToString();
    }

    // ── schemas and arguments ─────────────────────────────────────────────

    private static AssistantTool Tool(string name, string description, JsonObject properties, params string[] required)
    {
        var schema = new JsonObject { ["type"] = "object", ["properties"] = properties, ["additionalProperties"] = false };
        if (required.Length > 0) schema["required"] = new JsonArray(required.Select(r => (JsonNode)JsonValue.Create(r)!).ToArray());
        return new AssistantTool(name, description, schema);
    }

    private static JsonObject Props(params (string Name, JsonObject Schema)[] properties)
    {
        var obj = new JsonObject();
        foreach (var (name, schema) in properties) obj[name] = schema;
        return obj;
    }

    private static JsonObject Str(string description) => new() { ["type"] = "string", ["description"] = description };

    private static JsonObject Int(string description, int min, int max) =>
        new() { ["type"] = "integer", ["description"] = description, ["minimum"] = min, ["maximum"] = max };

    private static JsonObject Enum(string description, params string[] values) =>
        new() { ["type"] = "string", ["description"] = description, ["enum"] = new JsonArray(values.Select(v => (JsonNode)JsonValue.Create(v)!).ToArray()) };

    /// <summary>A string argument, trimmed and clipped; one line unless <paramref name="keepLines"/>.</summary>
    private static string? Text(JsonObject args, string name, int max, bool keepLines = false)
    {
        if (!args.TryGetPropertyValue(name, out var node) || node is not JsonValue value || value.GetValueKind() != JsonValueKind.String) return null;
        var text = value.GetValue<string>();
        if (!keepLines) text = string.Join(' ', text.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        text = text.Trim();
        if (text.Length > max) text = text[..max];
        return text.Length == 0 ? null : text;
    }

    private static int? Int(JsonObject args, string name)
    {
        if (!args.TryGetPropertyValue(name, out var node) || node is not JsonValue value) return null;
        if (value.TryGetValue<int>(out var i)) return i;
        if (value.TryGetValue<double>(out var d) && d is >= int.MinValue and <= int.MaxValue) return (int)Math.Round(d);
        return value.TryGetValue<string>(out var s) && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static decimal? Decimal(JsonObject args, string name)
    {
        if (!args.TryGetPropertyValue(name, out var node) || node is not JsonValue value) return null;
        if (value.TryGetValue<decimal>(out var m)) return m;
        if (value.TryGetValue<double>(out var d)) return (decimal)d;
        return value.TryGetValue<string>(out var s) && decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static Guid? Id(JsonObject args, string name) =>
        Text(args, name, 60) is { } text && Guid.TryParse(text, out var id) ? id : null;

    /// <summary>A time on the workspace's clock ("2026-11-14 19:00", or with a T), as UTC.</summary>
    private static bool TryLocal(string text, TimeZoneInfo zone, out DateTime utc)
    {
        utc = default;
        if (!DateTime.TryParseExact(text.Replace('T', ' '), ["yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
            return false;
        var at = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(at)) at = at.AddHours(1);
        utc = TimeZoneInfo.ConvertTimeToUtc(at, zone);
        return true;
    }

    private static string Local(DateTime utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone).ToString("ddd d MMM yyyy HH:mm", CultureInfo.InvariantCulture);

    private static string UtcIso(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max] + "…";
}
