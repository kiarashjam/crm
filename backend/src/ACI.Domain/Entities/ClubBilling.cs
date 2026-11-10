namespace ACI.Domain.Entities;

// What the club's billing does on its own: the daily run that bills subscriptions once per
// period, chases what is overdue, lapses a member who has not paid after the grace period
// and sends the monthly statements. Each thing it does once is recorded with a key the
// database will not take twice (a charge per subscription and period on ClubCharges, a
// reminder per charge and step, a monthly statement per member and month), so a run that
// is repeated, retried, or started on two servers at once does nothing a second time.

/// <summary>Who started a billing run.</summary>
public static class BillingRunTriggers
{
    /// <summary>The daily run, once per workspace per day in the workspace's time zone.</summary>
    public const string Schedule = "schedule";

    /// <summary>Someone pressed "Bill what is due now".</summary>
    public const string Manual = "manual";
}

/// <summary>How a billing run ended.</summary>
public static class BillingRunStatuses
{
    public const string Running = "running";

    /// <summary>Everything it set out to do was done.</summary>
    public const string Succeeded = "succeeded";

    /// <summary>It finished, but some subscriptions or members could not be done; the failures say which and why.</summary>
    public const string PartlyFailed = "partly_failed";

    /// <summary>It stopped before the end; what it did before stopping stands.</summary>
    public const string Failed = "failed";
}

/// <summary>One run of the club's billing: what it billed, chased, lapsed and sent, and what failed.</summary>
public class ClubBillingRun : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }

    /// <summary><see cref="BillingRunTriggers"/>.</summary>
    public string Trigger { get; set; } = BillingRunTriggers.Schedule;

    /// <summary>
    /// For the daily run, the workspace's local date it ran for: one per workspace per day,
    /// which the database holds to, so a second server finds the day taken. None for a run
    /// someone started.
    /// </summary>
    public DateOnly? ScheduledFor { get; set; }

    /// <summary><see cref="BillingRunStatuses"/>.</summary>
    public string Status { get; set; } = BillingRunStatuses.Running;

    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public Guid? StartedByUserId { get; set; }
    public string? StartedByName { get; set; }

    /// <summary>Subscriptions that had at least one period billed, and the charges posted for them.</summary>
    public int SubscriptionsBilled { get; set; }
    public int ChargesPosted { get; set; }

    /// <summary>What those charges came to, in <see cref="Currency"/> (the workspace's).</summary>
    public decimal AmountBilled { get; set; }
    public string Currency { get; set; } = "CHF";

    /// <summary>Charges past their due date and not paid in full when the run looked.</summary>
    public int OverdueCharges { get; set; }

    /// <summary>Subscriptions set past due, and set back to active once their dues were paid.</summary>
    public int SubscriptionsPastDue { get; set; }

    public int RemindersQueued { get; set; }
    public int MembersLapsed { get; set; }
    public int MembersReinstated { get; set; }
    public int StatementsQueued { get; set; }

    /// <summary>What could not be done, as a JSON array of { what, name, error }: the run carries on past each.</summary>
    public string? Failures { get; set; }
    public int FailureCount { get; set; }

    /// <summary>The steps it took, in words: "billed", "reminders", "lapses", "statements".</summary>
    public string? Steps { get; set; }
}

/// <summary>What became of a payment reminder.</summary>
public static class ReminderStatuses
{
    /// <summary>Handed to the outbox.</summary>
    public const string Queued = "queued";

    /// <summary>
    /// Not sent, on purpose: a later step was due by the time reminders were first sent for
    /// the charge, so only that one went; or the member has no usable address.
    /// </summary>
    public const string Skipped = "skipped";

    /// <summary>The outbox refused it: the address is on the suppression list.</summary>
    public const string Suppressed = "suppressed";
}

/// <summary>
/// One step of chasing one overdue charge: the first reminder, the second, the final notice.
/// There is at most one per charge and step, so a member is reminded of a charge once at
/// each step however often the run goes.
/// </summary>
public class ClubPaymentReminder : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid ChargeId { get; set; }
    public Guid MemberId { get; set; }

    /// <summary>1 for the first reminder, 2 for the second, and so on.</summary>
    public int Step { get; set; }

    /// <summary>The days after the due date this step was set for, when it was sent.</summary>
    public int DaysAfterDue { get; set; }

    /// <summary><see cref="ReminderStatuses"/>.</summary>
    public string Status { get; set; } = ReminderStatuses.Queued;

    /// <summary>The email carrying it; one email can remind a member of several charges.</summary>
    public Guid? OutboundMessageId { get; set; }

    public string? Reason { get; set; }
    public Guid? RunId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>Why a statement was sent.</summary>
public static class StatementSources
{
    /// <summary>The monthly run: once per member per month, which the database holds to.</summary>
    public const string Monthly = "monthly";

    /// <summary>Someone pressed "Email statement" on the member's statement.</summary>
    public const string OnDemand = "on_demand";
}

/// <summary>A statement emailed to a member, as a PDF, for a period.</summary>
public class ClubStatementDelivery : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid MemberId { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }

    /// <summary><see cref="StatementSources"/>.</summary>
    public string Source { get; set; } = StatementSources.OnDemand;

    /// <summary><see cref="ReminderStatuses"/>: queued, skipped (no address) or suppressed.</summary>
    public string Status { get; set; } = ReminderStatuses.Queued;

    public Guid? OutboundMessageId { get; set; }
    public string? Reason { get; set; }

    /// <summary>What the member owed at the end of the period, as the statement says.</summary>
    public decimal AmountDue { get; set; }
    public string Currency { get; set; } = "CHF";

    public Guid? RunId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
