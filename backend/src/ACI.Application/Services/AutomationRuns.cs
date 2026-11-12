using ACI.Domain.Entities;

namespace ACI.Application.Services;

/// <summary>A run that is due, as the runner finds it: which one, and the claim count it was seen at.</summary>
public sealed record DueAutomationRun(Guid Id, int ClaimCount);

/// <summary>Where runs and their logs are kept, and how a runner takes one.</summary>
public interface IAutomationRunStore
{
    /// <summary>
    /// Marks the rule fired for the run's key and adds the run, in one save. False when the
    /// rule has already fired for that key (here, or on another instance at the same moment).
    /// </summary>
    Task<bool> StartAsync(AutomationRun run, CancellationToken ct = default);

    /// <summary>Whether the rule has fired for the key already, so nothing about it needs reading.</summary>
    Task<bool> HasFiredAsync(Guid ruleId, string sourceKey, CancellationToken ct = default);

    /// <summary>Adds one to the rule's run count, without touching its version.</summary>
    Task CountRunAsync(Guid ruleId, CancellationToken ct = default);

    /// <summary>The run, tracked, by id in the workspace being acted for.</summary>
    Task<AutomationRun?> GetAsync(Guid runId, CancellationToken ct = default);

    /// <summary>Adds a line to a run's log; saved with the run's next save.</summary>
    void Log(AutomationRunStep step);

    /// <summary>
    /// Saves the run and its log. Throws <see cref="Common.ConcurrencyConflictException"/> when
    /// the run's claim was taken by someone else meanwhile.
    /// </summary>
    Task SaveAsync(CancellationToken ct = default);

    /// <summary>
    /// What the unit of work holds before a step runs, so a step that fails part-way can be
    /// taken back without touching anything the request had pending before it.
    /// </summary>
    object Checkpoint();

    /// <summary>Takes back whatever a failed step left unsaved since <paramref name="checkpoint"/>.</summary>
    void RevertTo(object checkpoint);

    /// <summary>
    /// Runs due at <paramref name="utcNow"/> whose rule is on, in every workspace: the runner
    /// starts in none and acts for each run's own.
    /// </summary>
    Task<IReadOnlyList<DueAutomationRun>> DueAsync(DateTime utcNow, int take, CancellationToken ct = default);

    /// <summary>
    /// Takes a due run for <paramref name="claimedUntilUtc"/> with one conditional update on its
    /// claim count: of two runners that saw it, exactly one gets it. Across workspaces, by id.
    /// </summary>
    Task<bool> TryClaimAsync(Guid runId, int claimCount, DateTime utcNow, DateTime claimedUntilUtc, CancellationToken ct = default);

    /// <summary>The workspace a run belongs to and who set it off, read by id across workspaces.</summary>
    Task<(Guid? OrganizationId, Guid ActorUserId)?> OwnerOfAsync(Guid runId, CancellationToken ct = default);

    /// <summary>The rule a run belongs to, untracked; null when it was deleted.</summary>
    Task<AutomationRule?> RuleAsync(Guid ruleId, CancellationToken ct = default);

    // ── what the rules page reads, in the workspace acted for ─────────────────

    Task<IReadOnlyList<AutomationRun>> ListAsync(Guid? ruleId, string? status, int take, CancellationToken ct = default);
    Task<AutomationRun?> FindAsync(Guid runId, CancellationToken ct = default);
    Task<IReadOnlyList<AutomationRunStep>> StepsAsync(Guid runId, CancellationToken ct = default);

    /// <summary>The outbox status of the emails steps queued, by message id.</summary>
    Task<IReadOnlyDictionary<Guid, string>> EmailStatusesAsync(IReadOnlyCollection<Guid> messageIds, CancellationToken ct = default);

    /// <summary>For each rule, its runs still going and those that failed.</summary>
    Task<IReadOnlyDictionary<Guid, AutomationRuleRunCounts>> CountsAsync(CancellationToken ct = default);

    /// <summary>
    /// A failed run made due again at once, its failed step to be tried afresh; false when it is
    /// not failed (any more).
    /// </summary>
    Task<bool> TryRetryAsync(Guid runId, DateTime utcNow, CancellationToken ct = default);

    /// <summary>A run not yet finished stopped, unless a runner holds it right now; false otherwise.</summary>
    Task<bool> TryCancelAsync(Guid runId, string reason, DateTime utcNow, CancellationToken ct = default);

    /// <summary>The workspaces with a rule on this trigger switched on, across workspaces.</summary>
    Task<IReadOnlyList<Guid>> WorkspacesWithRulesAsync(string trigger, CancellationToken ct = default);
}

/// <summary>How many of a rule's runs are still going and how many failed.</summary>
public sealed record AutomationRuleRunCounts(int Active, int Failed);

/// <summary>The ids that name what a run is about.</summary>
public sealed record AutomationSubject(
    string RecordType,
    Guid? RecordId,
    Guid? LeadId = null,
    Guid? DealId = null,
    Guid? TaskId = null,
    Guid? MemberId = null,
    Guid? ContractId = null);

/// <summary>
/// The record a run is about, as it is now: the facts its conditions test, the dates its
/// waits wait for, the member its email goes to, and where a notification links.
/// </summary>
public sealed class AutomationRecordView
{
    public string Name { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, FactValue> Facts { get; init; } = new Dictionary<string, FactValue>();
    public IReadOnlyDictionary<string, DateTime> Dates { get; init; } = new Dictionary<string, DateTime>();
    public Guid? MemberId { get; init; }
    public string? MemberName { get; init; }
    public string? MemberEmail { get; init; }

    /// <summary>The contact a member is linked to: a task about the member is put on it.</summary>
    public Guid? ContactId { get; init; }

    /// <summary>The page a notification about it opens.</summary>
    public string Link { get; init; } = "/";

    /// <summary>When it has gone, what to say in the log ("The lead was deleted"); null while it is there.</summary>
    public string? Gone { get; init; }
}

/// <summary>Reads the record a run is about, in the workspace being acted for.</summary>
public interface IAutomationRecordReader
{
    Task<AutomationRecordView> ReadAsync(AutomationSubject subject, CancellationToken ct = default);

    /// <summary>
    /// The running subscriptions whose current period ends between the two times, for the
    /// renewal trigger; in the workspace being acted for.
    /// </summary>
    Task<IReadOnlyList<RenewalDue>> RenewalsDueAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>The workspace's owner: who a rule set off by the system itself acts as.</summary>
    Task<Guid?> WorkspaceOwnerAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>The workspace's time zone, for send windows.</summary>
    Task<TimeZoneInfo> ZoneAsync(Guid organizationId, CancellationToken ct = default);
}

/// <summary>A subscription whose period ends soon: the renewal trigger's event.</summary>
public sealed record RenewalDue(Guid SubscriptionId, Guid MemberId, string MemberName, DateTime PeriodEndsAtUtc);

// ── what the API answers ─────────────────────────────────────────────────────

public sealed record AutomationRunDto(
    Guid Id,
    Guid RuleId,
    string RuleName,
    string Trigger,
    string Status,
    string RecordType,
    Guid? RecordId,
    string? RecordName,
    string? RecordLink,
    string CurrentPath,
    string? CurrentStep,
    int StepAttempts,
    DateTime? NextAttemptAtUtc,
    string? WaitingFor,
    string? LastError,
    DateTime StartedAtUtc,
    DateTime UpdatedAtUtc,
    DateTime? FinishedAtUtc);

public sealed record AutomationRunStepDto(
    Guid Id,
    string Path,
    string StepType,
    string Outcome,
    string Summary,
    string? Error,
    int Attempt,
    DateTime AtUtc,
    Guid? OutboundMessageId,
    string? EmailStatus,
    Guid? TaskId);

public sealed record AutomationRunDetailDto(AutomationRunDto Run, IReadOnlyList<AutomationRunStepDto> Steps);
