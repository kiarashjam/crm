using ACI.Domain.Common;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

/// <summary>The kinds of value a condition can be about, and so which comparisons it offers.</summary>
public static class AutomationFieldTypes
{
    public const string Text = "text";
    /// <summary>One of a list (a status, a stage, a tier): compared by its id or its name.</summary>
    public const string Choice = "choice";
    /// <summary>Someone in the workspace, by their user id.</summary>
    public const string User = "user";
    public const string Number = "number";
    public const string Money = "money";
    public const string Bool = "bool";
}

/// <summary>One fixed choice of a <see cref="AutomationFieldTypes.Choice"/> field.</summary>
public sealed record AutomationChoice(string Value, string Label);

/// <summary>
/// A field of the record a trigger is about that a condition can test.
/// </summary>
/// <param name="Options">
/// For a choice whose values are the workspace's own (its lead statuses, stages, tiers…),
/// the list the rule builder reads them from; null for a fixed list (<paramref name="Choices"/>).
/// </param>
public sealed record AutomationField(string Key, string Label, string Type, string? Options = null, IReadOnlyList<AutomationChoice>? Choices = null);

/// <summary>A date on the record a wait can wait for ("until 1 day before the event starts").</summary>
public sealed record AutomationDateField(string Key, string Label);

/// <summary>Something that sets rules off, and what its rules can test and do.</summary>
/// <param name="Module">The module it belongs to (<see cref="WorkspaceModules"/>); null for one in neither.</param>
/// <param name="Record">The kind of record it is about.</param>
/// <param name="TakesDaysBefore">Whether each rule says how many days ahead it fires (a renewal coming up).</param>
public sealed record AutomationTrigger(
    string Key,
    string Label,
    string Group,
    string? Module,
    string Record,
    IReadOnlyList<AutomationField> Fields,
    IReadOnlyList<AutomationDateField> Dates,
    IReadOnlyList<string> Actions,
    string Description,
    bool TakesDaysBefore = false);

/// <summary>The kinds of record a run can be about.</summary>
public static class AutomationRecords
{
    public const string Lead = "lead";
    public const string Deal = "deal";
    public const string Task = "task";
    public const string Contract = "contract";
    public const string Member = "member";
    public const string Subscription = "subscription";
    public const string Reservation = "reservation";
    public const string Registration = "registration";
    public const string Application = "application";
    public const string Payment = "payment";
}

/// <summary>
/// Every trigger, with the fields its conditions can test, the dates its waits can wait for
/// and the actions it can take. The engine evaluates by it, saving a rule is checked against
/// it, and the rule builder is drawn from it (GET /api/automations/catalog).
/// </summary>
public static class AutomationCatalog
{
    public const string LeadCreated = "lead.created";
    public const string LeadStatusChanged = "lead.status_changed";
    public const string DealStageChanged = "deal.stage_changed";
    public const string DealWon = "deal.won";
    public const string DealLost = "deal.lost";
    public const string TaskOverdue = "task.overdue";
    public const string ContractSigned = "contract.signed";
    public const string ContractCountersigned = "contract.countersigned";
    public const string RenewalDue = "membership.renewal_due";
    public const string MemberCreated = "member.created";
    public const string ApplicationApproved = "application.approved";
    public const string ReservationCreated = "reservation.created";
    public const string EventRegistered = "event.registered";
    public const string PaymentFailed = "payment.failed";

    /// <summary>How many days ahead a renewal rule fires when it does not say.</summary>
    public const int DefaultDaysBefore = 14;
    public const int MaxDaysBefore = 90;

    /// <summary>The trigger setting with the days ahead (TriggerConfig).</summary>
    public const string DaysBeforeKey = "daysBefore";

    // ── fixed choices ────────────────────────────────────────────────────────

    private static AutomationChoice[] Same(params string[] values) => values.Select(v => new AutomationChoice(v, v)).ToArray();

    private static readonly AutomationChoice[] MemberStatuses = Same("Pending", "Active", "Lapsed", "Cancelled");
    private static readonly AutomationChoice[] Cadences =
    [
        new("Monthly", "Monthly"), new("Quarterly", "Quarterly"), new("SemiAnnual", "Twice a year"), new("Annual", "Yearly"),
    ];
    private static readonly AutomationChoice[] TaskPriorities =
    [
        new("none", "None"), new("low", "Low"), new("medium", "Medium"), new("high", "High"),
    ];
    private static readonly AutomationChoice[] BookingStatuses =
    [
        new(ReservationStatuses.Pending, "Pending"), new(ReservationStatuses.Confirmed, "Confirmed"),
        new(ReservationStatuses.CheckedIn, "Checked in"),
    ];
    private static readonly AutomationChoice[] PlaceStatuses =
    [
        new(RegistrationStatuses.Reserved, "Has a seat"), new(RegistrationStatuses.Waitlisted, "On the waitlist"),
        new(RegistrationStatuses.CheckedIn, "Checked in at the door"),
    ];
    private static readonly AutomationChoice[] PlaceSources =
    [
        new(RegistrationSources.Registered, "Registered ahead"), new(RegistrationSources.WalkIn, "Walked in"),
    ];
    private static readonly AutomationChoice[] Tickets = [new(TicketTiers.Member, "Member"), new(TicketTiers.Guest, "Guest")];
    private static readonly AutomationChoice[] ApplicationVia =
    [
        new(ApplicationSources.Staff, "Added by staff"), new(ApplicationSources.PublicForm, "The public form"),
        new(ApplicationSources.Lead, "From a lead or contact"),
    ];
    private static readonly AutomationChoice[] ApprovedVia =
    [
        new(ApprovalTriggers.Staff, "Approved by staff"), new(ApprovalTriggers.Contract, "A countersigned contract"),
        new(ApprovalTriggers.Deposit, "A paid deposit"),
    ];
    private static readonly AutomationChoice[] PaymentKinds =
    [
        new(OnlinePaymentPurposes.Checkout, "Paid through a pay link"), new(OnlinePaymentPurposes.AutoPay, "Dues charged to a saved card"),
        new(OnlinePaymentPurposes.SavedCard, "Charged to a saved card by staff"),
    ];

    // ── fields ───────────────────────────────────────────────────────────────

    private static readonly AutomationField[] LeadFields =
    [
        new("status", "Status", AutomationFieldTypes.Choice, "leadStatus"),
        new("source", "Source", AutomationFieldTypes.Choice, "leadSource"),
        new("owner", "Owner", AutomationFieldTypes.User, "user"),
        new("score", "Score", AutomationFieldTypes.Number),
        new("email", "Email", AutomationFieldTypes.Text),
        new("country", "Country", AutomationFieldTypes.Text),
        new("industry", "Industry", AutomationFieldTypes.Text),
        new("companySize", "Company size", AutomationFieldTypes.Text),
    ];

    private static readonly AutomationField FromStatus = new("fromStatus", "Previous status", AutomationFieldTypes.Choice, "leadStatus");

    private static readonly AutomationField[] DealFields =
    [
        new("stage", "Stage", AutomationFieldTypes.Choice, "dealStage"),
        new("pipeline", "Pipeline", AutomationFieldTypes.Choice, "pipeline"),
        new("value", "Value", AutomationFieldTypes.Money),
        new("currency", "Currency", AutomationFieldTypes.Text),
        new("owner", "Owner", AutomationFieldTypes.User, "user"),
        new("probability", "Probability (%)", AutomationFieldTypes.Number),
    ];

    private static readonly AutomationField FromStage = new("fromStage", "Previous stage", AutomationFieldTypes.Choice, "dealStage");

    private static readonly AutomationField[] TaskFields =
    [
        new("title", "Title", AutomationFieldTypes.Text),
        new("priority", "Priority", AutomationFieldTypes.Choice, null, TaskPriorities),
        new("assignee", "Assigned to", AutomationFieldTypes.User, "user"),
    ];

    private static readonly AutomationField[] ContractFields =
    [
        new("title", "Contract title", AutomationFieldTypes.Text),
        new("owner", "Owner of the lead or deal", AutomationFieldTypes.User, "user"),
        new("status", "Lead status", AutomationFieldTypes.Choice, "leadStatus"),
        new("stage", "Deal stage", AutomationFieldTypes.Choice, "dealStage"),
        new("value", "Deal value", AutomationFieldTypes.Money),
    ];

    private static readonly AutomationField[] MemberFields =
    [
        new("tier", "Member's tier", AutomationFieldTypes.Choice, "tier"),
        new("memberStatus", "Member's status", AutomationFieldTypes.Choice, null, MemberStatuses),
        new("duesFrequency", "Member's dues", AutomationFieldTypes.Choice, null, Cadences),
    ];

    private static readonly AutomationField[] RenewalFields =
    [
        new("subscription", "Subscription", AutomationFieldTypes.Text),
        new("cadence", "Billed", AutomationFieldTypes.Choice, null, Cadences),
        new("amount", "Amount", AutomationFieldTypes.Money),
        new("daysUntil", "Days until it renews", AutomationFieldTypes.Number),
    ];

    private static readonly AutomationField[] ReservationFields =
    [
        new("resource", "Space", AutomationFieldTypes.Choice, "resource"),
        new("partySize", "Party size", AutomationFieldTypes.Number),
        new("bookingStatus", "Booking status", AutomationFieldTypes.Choice, null, BookingStatuses),
        new("forMember", "Booked for a member", AutomationFieldTypes.Bool),
    ];

    private static readonly AutomationField[] RegistrationFields =
    [
        new("event", "Event", AutomationFieldTypes.Choice, "event"),
        new("placeStatus", "Place", AutomationFieldTypes.Choice, null, PlaceStatuses),
        new("placeSource", "How they came", AutomationFieldTypes.Choice, null, PlaceSources),
        new("ticket", "Ticket", AutomationFieldTypes.Choice, null, Tickets),
        new("forMember", "A member", AutomationFieldTypes.Bool),
    ];

    private static readonly AutomationField[] ApplicationFields =
    [
        new("appliedThrough", "Applied through", AutomationFieldTypes.Choice, null, ApplicationVia),
        new("approvedThrough", "Approved through", AutomationFieldTypes.Choice, null, ApprovedVia),
    ];

    private static readonly AutomationField[] PaymentFields =
    [
        new("amount", "Amount", AutomationFieldTypes.Money),
        new("paymentKind", "Kind of payment", AutomationFieldTypes.Choice, null, PaymentKinds),
        new("reason", "Reason given", AutomationFieldTypes.Text),
    ];

    // ── dates ────────────────────────────────────────────────────────────────

    private static readonly AutomationDateField ExpectedClose = new("expectedClose", "The deal's expected close date");
    private static readonly AutomationDateField TaskDue = new("due", "The task's due date");
    private static readonly AutomationDateField[] MemberDates =
    [
        new("renewsAt", "The member's renewal date"),
        new("startsOn", "The date the membership starts"),
    ];
    private static readonly AutomationDateField PeriodEnd = new("periodEnd", "The date the subscription renews");
    private static readonly AutomationDateField[] BookingDates = [new("start", "The booking's start"), new("end", "The booking's end")];
    private static readonly AutomationDateField[] EventDates = [new("eventStart", "The event's start"), new("eventEnd", "The event's end")];

    // ── actions ──────────────────────────────────────────────────────────────

    /// <summary>What a rule on a lead or a deal can do, as it always could.</summary>
    private static readonly string[] SalesActions =
    [
        AutomationStepTypes.CreateTask, AutomationStepTypes.SendEmail, AutomationStepTypes.Notify,
        AutomationStepTypes.Assign, AutomationStepTypes.AddToSequence,
    ];

    /// <summary>
    /// What a rule on a club record can do: a member has no owner to hand over and is in no
    /// sequence, so the actions are a task, the member's email, and a notification.
    /// </summary>
    private static readonly string[] ClubActions =
    [
        AutomationStepTypes.CreateTask, AutomationStepTypes.SendEmail, AutomationStepTypes.Notify,
    ];

    private static AutomationField[] With(IEnumerable<AutomationField> fields, params AutomationField[] more) => fields.Concat(more).ToArray();

    public static readonly IReadOnlyList<AutomationTrigger> Triggers =
    [
        new(LeadCreated, "When a lead is created", "Sales", WorkspaceModules.Sales, AutomationRecords.Lead,
            LeadFields, [], SalesActions,
            "A lead is added by hand, by a form, by the website or by an import that asks for the rules to run."),
        new(LeadStatusChanged, "When a lead status changes", "Sales", WorkspaceModules.Sales, AutomationRecords.Lead,
            With(LeadFields, FromStatus), [], SalesActions,
            "A lead moves to another status, by hand or because its pipeline moved it."),
        new(DealStageChanged, "When a deal moves stage", "Sales", WorkspaceModules.Sales, AutomationRecords.Deal,
            With(DealFields, FromStage), [ExpectedClose], SalesActions,
            "A deal is moved to another stage of its pipeline."),
        new(DealWon, "When a deal is won", "Sales", WorkspaceModules.Sales, AutomationRecords.Deal,
            DealFields, [ExpectedClose], SalesActions, "A deal reaches a stage that counts as won."),
        new(DealLost, "When a deal is lost", "Sales", WorkspaceModules.Sales, AutomationRecords.Deal,
            DealFields, [ExpectedClose], SalesActions, "A deal reaches a stage that counts as lost."),
        new(ContractSigned, "When a client signs a contract", "Sales", WorkspaceModules.Sales, AutomationRecords.Contract,
            ContractFields, [ExpectedClose], SalesActions,
            "The client signs a contract sent for signature. The rule works on the contract's lead or deal."),
        new(ContractCountersigned, "When a contract is countersigned", "Sales", WorkspaceModules.Sales, AutomationRecords.Contract,
            ContractFields, [ExpectedClose], SalesActions,
            "Someone in the workspace countersigns a contract the client signed, so both have signed."),
        new(TaskOverdue, "When a task becomes overdue", "Tasks", null, AutomationRecords.Task,
            TaskFields, [TaskDue], SalesActions, "An open task passes its due time."),
        new(RenewalDue, "When a membership is due to renew", "Club", WorkspaceModules.Club, AutomationRecords.Subscription,
            With(MemberFields, RenewalFields), [PeriodEnd, .. MemberDates], ClubActions,
            "A running subscription's current period ends within the days the rule says. It fires once for each period.",
            TakesDaysBefore: true),
        new(MemberCreated, "When a member is added", "Club", WorkspaceModules.Club, AutomationRecords.Member,
            MemberFields, MemberDates, ClubActions, "A member is added by hand, or made by an approved application."),
        new(ApplicationApproved, "When an application is approved", "Club", WorkspaceModules.Club, AutomationRecords.Application,
            With(MemberFields, ApplicationFields), MemberDates, ClubActions,
            "A membership application is approved: by staff, a countersigned contract or a paid deposit."),
        new(ReservationCreated, "When a booking is made", "Club", WorkspaceModules.Club, AutomationRecords.Reservation,
            With(ReservationFields, MemberFields), BookingDates, ClubActions, "A space is booked."),
        new(EventRegistered, "When someone registers for an event", "Club", WorkspaceModules.Club, AutomationRecords.Registration,
            With(RegistrationFields, MemberFields), EventDates, ClubActions,
            "Someone is put on an event's list: given a seat, put on the waitlist, or let in at the door."),
        new(PaymentFailed, "When an online payment fails", "Club", WorkspaceModules.Club, AutomationRecords.Payment,
            With(PaymentFields, MemberFields), MemberDates, ClubActions,
            "A card or direct debit payment is declined, or a direct debit does not go through."),
    ];

    private static readonly Dictionary<string, AutomationTrigger> ByKey = Triggers.ToDictionary(t => t.Key, StringComparer.Ordinal);

    public static AutomationTrigger? Find(string? key) => key != null && ByKey.TryGetValue(key, out var trigger) ? trigger : null;

    public static AutomationField? Field(AutomationTrigger trigger, string? key) =>
        trigger.Fields.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.Ordinal));

    /// <summary>The days ahead a renewal rule fires, from its trigger settings.</summary>
    public static int DaysBefore(IReadOnlyDictionary<string, string>? config) =>
        config != null && config.TryGetValue(DaysBeforeKey, out var raw) && int.TryParse(raw, out var days)
            ? Math.Clamp(days, 1, MaxDaysBefore)
            : DefaultDaysBefore;
}
