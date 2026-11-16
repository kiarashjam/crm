namespace ACI.Domain.Entities;

/// <summary>A workspace rule: when something happens, do the stored actions.</summary>
public class AutomationRule : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string Trigger { get; set; } = string.Empty;

    /// <summary>
    /// The trigger's own settings, as a JSON object of strings: how many days before a
    /// membership renews it fires, say. Keys an older client sent that name a condition
    /// (stageId, status, source) are copied into <see cref="ConditionsJson"/>.
    /// </summary>
    public string? TriggerConfigJson { get; set; }

    /// <summary>
    /// What the record that set the rule off has to be for it to run (a condition group,
    /// see AutomationConditions); null runs it for every record, as rules always ran.
    /// </summary>
    public string? ConditionsJson { get; set; }

    /// <summary>The steps, in order: actions, waits and branches (see AutomationSteps).</summary>
    public string ActionsJson { get; set; } = "[]";

    /// <summary>
    /// The days and hours its emails may go, in the workspace's time zone; null for any
    /// time. An email step that comes due outside them waits for the next opening.
    /// </summary>
    public string? SendWindowJson { get; set; }

    public int RunCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// A new value on every edit of the rule, and the column a save is checked against, so
    /// two people editing one rule do not overwrite each other (If-Match, 409). Not stamped
    /// on every save like <see cref="Common.IVersioned"/>: the run count moves each time the
    /// rule runs, and that must not make an editor's copy stale.
    /// </summary>
    public Guid Version { get; set; }
}

/// <summary>Stops one rule from doing the same thing twice for the same record.</summary>
public class AutomationFire : Common.BaseEntity
{
    public Guid RuleId { get; set; }
    public string SourceKey { get; set; } = string.Empty;
    public DateTime FiredAtUtc { get; set; }
}

/// <summary>
/// One time a rule was set off: where it has got to in its steps, when it is next due,
/// and who holds it while a step runs.
/// </summary>
/// <remarks>
/// <para>
/// A rule used to do all its actions inside the save that set it off, and what happened
/// was logged and lost. Now each time it fires is a run. The steps up to the first wait
/// still run in that save, as before; a wait, an email held for the send window, or a
/// step that failed and is tried again later leaves the run due at a time, and the
/// automation runner takes it from there.
/// </para>
/// <para>
/// A step runs only while its runner holds the run (<see cref="ClaimedUntilUtc"/>), taken
/// with one conditional update on <see cref="ClaimCount"/>, so two app instances, or a
/// runner and a retry, never run the same step at once. Like the outbox, a runner that dies
/// part-way leaves the step to be run again once its claim runs out.
/// </para>
/// </remarks>
public class AutomationRun : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid RuleId { get; set; }

    /// <summary>The rule's name when it fired, for the log.</summary>
    public string RuleName { get; set; } = string.Empty;

    public string Trigger { get; set; } = string.Empty;

    /// <summary>What it fired for (the rule fires once per rule and key; see <see cref="AutomationFire"/>).</summary>
    public string SourceKey { get; set; } = string.Empty;

    /// <summary><see cref="AutomationRunStatuses"/>.</summary>
    public string Status { get; set; } = AutomationRunStatuses.Running;

    /// <summary>Who set it off: tasks it makes are created by them, as before.</summary>
    public Guid ActorUserId { get; set; }

    /// <summary>Who worked the record when it fired: the task goes to them when nobody works it now.</summary>
    public Guid? AssigneeId { get; set; }

    /// <summary>Set off by a row of an import the person importing asked to run the rules for.</summary>
    public bool FromImport { get; set; }

    /// <summary>The kind of record it is about (lead, deal, task, member, reservation, …).</summary>
    public string RecordType { get; set; } = string.Empty;
    public Guid? RecordId { get; set; }

    /// <summary>The record's name when it fired, for the log.</summary>
    public string? RecordName { get; set; }

    public Guid? LeadId { get; set; }
    public Guid? DealId { get; set; }
    public Guid? TaskId { get; set; }
    public Guid? MemberId { get; set; }
    public Guid? ContractId { get; set; }

    /// <summary>The address the event named, for a lead that has none of its own.</summary>
    public string? Email { get; set; }

    /// <summary>What the event said that the record does not (the status it came from, the date a period ends), as JSON.</summary>
    public string? EventJson { get; set; }

    /// <summary>The rule's steps as they were when it fired: a run carries on with those whatever is edited since.</summary>
    public string StepsJson { get; set; } = "[]";

    /// <summary>The step it is at: its index, through branches ("2", "3.t.0").</summary>
    public string CurrentPath { get; set; } = "0";

    /// <summary>When it reached the step it is at: a wait of two days counts from here.</summary>
    public DateTime? StepStartedAtUtc { get; set; }

    /// <summary>Tries of the step it is at, the one in progress included.</summary>
    public int StepAttempts { get; set; }

    /// <summary>When it is next due; null once it has finished.</summary>
    public DateTime? NextAttemptAtUtc { get; set; }

    /// <summary>Until when one runner holds it; nobody else runs its step until then.</summary>
    public DateTime? ClaimedUntilUtc { get; set; }

    /// <summary>
    /// Moves on every claim, and a runner's save is checked against the value it claimed at:
    /// one whose claim ran out and was taken by another cannot write over that one.
    /// </summary>
    public int ClaimCount { get; set; }

    /// <summary>What it waits for, in words: "2 days", "the send window".</summary>
    public string? WaitingFor { get; set; }

    /// <summary>Why its last try of a step failed.</summary>
    public string? LastError { get; set; }

    public DateTime StartedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
}

/// <summary>Where a run stands.</summary>
public static class AutomationRunStatuses
{
    /// <summary>A step is running now.</summary>
    public const string Running = "running";

    /// <summary>Waiting for a time: a wait step, or an email held for the send window.</summary>
    public const string Waiting = "waiting";

    /// <summary>A step failed and is tried again at <see cref="AutomationRun.NextAttemptAtUtc"/>.</summary>
    public const string Retrying = "retrying";

    public const string Completed = "completed";

    /// <summary>A step still failed after its last try. Someone can try it again from the rules page.</summary>
    public const string Failed = "failed";

    /// <summary>Stopped before its end: by a person, or because what it was about is gone.</summary>
    public const string Cancelled = "cancelled";

    /// <summary>The statuses of a run that has not finished.</summary>
    public static readonly IReadOnlySet<string> Active = new HashSet<string>(StringComparer.Ordinal) { Running, Waiting, Retrying };
}

/// <summary>What one step of a run did, in the run's log.</summary>
public class AutomationRunStep : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid RunId { get; set; }

    /// <summary>The step's place in the run's steps (see <see cref="AutomationRun.CurrentPath"/>).</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>The step's type: create_task, send_email, wait, branch…</summary>
    public string StepType { get; set; } = string.Empty;

    /// <summary><see cref="AutomationStepOutcomes"/>.</summary>
    public string Outcome { get; set; } = AutomationStepOutcomes.Done;

    /// <summary>What it did, in words a person reads in the log.</summary>
    public string Summary { get; set; } = string.Empty;

    public string? Error { get; set; }

    /// <summary>Which try of the step this was.</summary>
    public int Attempt { get; set; }

    /// <summary>The email an email step queued in the outbox; its delivery is read from there.</summary>
    public Guid? OutboundMessageId { get; set; }

    /// <summary>The task a task step created.</summary>
    public Guid? TaskId { get; set; }

    public DateTime AtUtc { get; set; }
}

/// <summary>How a step of a run ended.</summary>
public static class AutomationStepOutcomes
{
    public const string Done = "done";

    /// <summary>Nothing to do, for a reason that is not a failure: no address, do not contact, the record has no owner.</summary>
    public const string Skipped = "skipped";

    /// <summary>The run waits here: a wait step, or an email held for the send window.</summary>
    public const string Waiting = "waiting";

    /// <summary>It failed, and is tried again later.</summary>
    public const string Retrying = "retrying";

    /// <summary>It failed on its last try, and the run stopped.</summary>
    public const string Failed = "failed";
}
