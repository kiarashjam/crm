namespace ACI.Application.DTOs;

/// <summary>
/// A workspace's System health page in one read: the verdict, then each area with how many
/// things it has and the first few of them.
/// </summary>
public sealed record SystemHealthDto(
    DateTime CheckedAtUtc,
    SystemHealthOverallDto Overall,
    HandoffHealthDto Handoffs,
    OutboxHealthDto Outbox,
    WebhookHealthDto Webhooks,
    PaymentHealthDto Payments,
    AutomationHealthDto Automations,
    EmailHealthDto Email,
    IReadOnlyList<JobHealthDto> Jobs,
    PortalHealthDto Portal);

/// <param name="Status">ok, attention (something to look at) or failing (a job has stopped or keeps failing, or the portal did not answer its last ping).</param>
/// <param name="Issues">How many areas have something in them.</param>
/// <param name="Counts">Each area's count, by its name: handoffs, outbox, webhooks, payments, automations, email, jobs, portal.</param>
public sealed record SystemHealthOverallDto(string Status, int Issues, IReadOnlyDictionary<string, int> Counts);

/// <summary>Something on the page that can be tried again with an endpoint that already does that.</summary>
/// <param name="Kind">handoff (POST /api/onboarding/{id}/resend) or automation (POST /api/automations/runs/{id}/retry).</param>
public sealed record HealthRetryDto(string Kind, Guid Id);

/// <summary>Welcome handoffs the portal has not taken after an hour, or that need attention.</summary>
public sealed record HandoffHealthDto(int Count, IReadOnlyList<HandoffIssueDto> Items);

/// <param name="State">late (still being handed over after an hour) or failed (it needs attention, as the deal's Welcome card says).</param>
/// <param name="Person">The person handed over.</param>
/// <param name="SinceUtc">When it was last handed over (or sent again).</param>
public sealed record HandoffIssueDto(
    Guid Id, Guid DealId, string? DealName, string Person, string State, string? LastError, int Attempts,
    DateTime SinceUtc, string Link, HealthRetryDto? Retry);

/// <param name="Queued">Messages still queued after 30 minutes.</param>
/// <param name="Failed">Messages given up on, queued in the last 7 days.</param>
public sealed record OutboxHealthDto(int Count, int Queued, int Failed, IReadOnlyList<OutboxIssueDto> Items);

/// <param name="Kind">email, webhook or handoff.</param>
/// <param name="State">queued or failed.</param>
/// <param name="To">The addresses, for an email; the event, for a webhook or a handoff.</param>
/// <param name="Link">The record it was about, or where its kind is set up; null when there is none.</param>
public sealed record OutboxIssueDto(
    Guid Id, string Kind, string State, string? To, string? Subject, string? LastError, int Attempts,
    DateTime CreatedAtUtc, DateTime? NextAttemptAtUtc, string? Link, HealthRetryDto? Retry);

/// <summary>Events posted to the workspace's own address that failed, in the last 7 days (Settings, Integrations).</summary>
public sealed record WebhookHealthDto(int Count, IReadOnlyList<WebhookDeliveryDto> Items, string Link);

/// <summary>Stripe events that could not be processed, in the last 7 days. Stripe posts each again, for a few days.</summary>
public sealed record PaymentHealthDto(int Count, IReadOnlyList<PaymentIssueDto> Items, string Link);

/// <param name="EventId">Stripe's evt_ id.</param>
/// <param name="Note">Why it failed.</param>
public sealed record PaymentIssueDto(Guid Id, string EventId, string Type, string? Note, int Attempts, DateTime ReceivedAtUtc);

/// <summary>Automation runs that failed in the last 7 days.</summary>
public sealed record AutomationHealthDto(int Count, IReadOnlyList<AutomationIssueDto> Items);

public sealed record AutomationIssueDto(
    Guid RunId, Guid RuleId, string RuleName, string? RecordName, string? LastError, int Attempts,
    DateTime FailedAtUtc, string Link, HealthRetryDto? Retry);

/// <summary>What the mail service reported in the last 7 days: bounces and spam reports, and the addresses no longer emailed.</summary>
/// <param name="Count">Bounces and spam reports.</param>
/// <param name="NewSuppressions">Addresses put on the do-not-send list (Settings, Team).</param>
/// <param name="Items">The bounced and reported emails, newest first.</param>
public sealed record EmailHealthDto(int Count, int Bounces, int SpamReports, int NewSuppressions, IReadOnlyList<EmailIssueDto> Items, string Link);

/// <param name="Kind">bounce or spam_report.</param>
/// <param name="Detail">What the receiving server said.</param>
public sealed record EmailIssueDto(Guid Id, string Kind, string? To, string? Subject, string? Detail, DateTime AtUtc, string? Link);

/// <summary>
/// A background job, from the heartbeats every job reports: shared by every workspace, as the
/// jobs are. Its error is the one line the registry keeps.
/// </summary>
public sealed record JobHealthDto(
    string Name, int IntervalSeconds, DateTime LastSeenAtUtc, DateTime? LastBeatAtUtc, DateTime? LastFailureAtUtc,
    bool Failing, bool Silent, int ConsecutiveFailures, string? LastError);

/// <summary>The daily ping to the workspace's portal.</summary>
/// <param name="Configured">Welcome &amp; portal has an address and a secret.</param>
/// <param name="Ok">What the last ping found; null before the first on this server.</param>
public sealed record PortalHealthDto(bool Configured, bool? Ok, DateTime? AtUtc, int? StatusCode, string? Error, string Link);
