using System.Globalization;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>Something that happened, as the rules for its trigger see it.</summary>
/// <param name="SourceKey">What a rule fires once for: a lead and the status it reached, a booking.</param>
/// <param name="FromImport">A row of an import the person importing asked to run the rules for.</param>
/// <param name="RecordId">The record itself, for a trigger whose record is none of the other ids (a booking, a subscription, a payment).</param>
/// <param name="Facts">What the event knows that the record does not keep: the status or stage it came from.</param>
/// <param name="DueAtUtc">For a renewal coming up, when the period ends.</param>
public sealed record AutomationEvent(
    Guid? OrganizationId,
    Guid ActorUserId,
    string SourceKey,
    string? RecordName,
    Guid? LeadId = null,
    Guid? DealId = null,
    Guid? TaskId = null,
    string? Email = null,
    Guid? AssigneeId = null,
    bool FromImport = false,
    Guid? MemberId = null,
    Guid? ContractId = null,
    Guid? RecordId = null,
    IReadOnlyDictionary<string, string[]>? Facts = null,
    DateTime? DueAtUtc = null);

public interface IAutomationEngine
{
    /// <summary>Starts a run of every rule on the trigger whose conditions the record meets.</summary>
    Task DispatchAsync(string trigger, AutomationEvent evt, CancellationToken ct = default);

    /// <summary>One due run, claimed and carried on from its step: what the automation runner does for each.</summary>
    Task ResumeAsync(DueAutomationRun due, CancellationToken ct = default);
}

public interface IAutomationRepository
{
    Task<IReadOnlyList<AutomationRule>> ListAsync(Guid? organizationId, CancellationToken ct = default);
    Task<AutomationRule?> GetAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    Task SaveAsync(AutomationRule rule, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<AutomationRule>> EnabledForTriggerAsync(Guid? organizationId, string trigger, CancellationToken ct = default);
}

/// <summary>
/// Runs saved rules: checks each rule's conditions on the record that set it off, starts a
/// run, and goes through the run's steps. A failure in one step does not undo the lead or
/// deal that set the rule off.
/// </summary>
/// <remarks>
/// <para>
/// The steps up to the first wait run in the save that set the rule off, as actions always
/// did, so a lead is assigned and its task made before the save answers. A wait, an email
/// held for the rule's send window, or a step that failed leaves the run due at a time, and
/// the automation runner (AutomationRunnerBackgroundService) carries it on from there,
/// claiming it first so no step runs twice at once (see <see cref="AutomationRun"/>).
/// </para>
/// <para>
/// A step that fails is tried again after a minute, five, thirty and two hours, and after
/// the fifth try the run is marked failed with the reason; the rules page lists it, and
/// someone can try it again from there. Every step leaves a line in the run's log.
/// </para>
/// </remarks>
public sealed partial class AutomationEngine : IAutomationEngine
{
    /// <summary>How long a runner holds a run while it runs a step.</summary>
    internal static readonly TimeSpan ClaimFor = TimeSpan.FromMinutes(5);

    /// <summary>The wait after each failed try of a step.</summary>
    internal static readonly TimeSpan[] RetryDelays =
    {
        TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2),
    };

    /// <summary>The first try of a step and one after each retry delay.</summary>
    public static readonly int MaxStepAttempts = RetryDelays.Length + 1;

    /// <summary>
    /// Most steps one go runs before it hands the run back to the runner, so a rule of many
    /// steps cannot keep the save that set it off waiting.
    /// </summary>
    private const int MaxStepsPerGo = 40;

    /// <summary>How long a club rule's run waits before it looks again while the club is turned off.</summary>
    private static readonly TimeSpan ModuleOffRecheck = TimeSpan.FromHours(1);

    private readonly IAutomationRepository _rules;
    private readonly IAutomationRunStore _runs;
    private readonly IAutomationRecordReader _records;
    private readonly ITaskService _tasks;
    private readonly INotificationService _notifications;
    private readonly ILeadRepository _leads;
    private readonly IDealRepository _deals;
    private readonly IOutbox _outbox;
    private readonly ISequenceService _sequences;
    private readonly ISequenceRepository _targets;
    private readonly IOrganizationRepository _organizations;
    private readonly ILogger<AutomationEngine> _logger;
    private readonly ICollaborationNotifier? _notifier;
    private readonly IWorkspaceModules? _modules;
    private readonly ITenantContext? _tenant;

    /// <summary>The time waits, send windows and retries are counted on; a test moves it on.</summary>
    private readonly TimeProvider _clock;

    private DateTime Now() => _clock.GetUtcNow().UtcDateTime;

    /// <summary>Told when a runner has seen a run due and is about to claim it: where a test holds two runners at once.</summary>
    private readonly ILedgerReadObserver? _reads;

    public AutomationEngine(
        IAutomationRepository rules,
        IAutomationRunStore runs,
        IAutomationRecordReader records,
        ITaskService tasks,
        INotificationService notifications,
        ILeadRepository leads,
        IDealRepository deals,
        IOutbox outbox,
        ISequenceService sequences,
        ISequenceRepository targets,
        IOrganizationRepository organizations,
        ILogger<AutomationEngine> logger,
        ICollaborationNotifier? notifier = null,
        IWorkspaceModules? modules = null,
        ITenantContext? tenant = null,
        TimeProvider? clock = null,
        ILedgerReadObserver? reads = null)
    {
        _clock = clock ?? TimeProvider.System;
        _reads = reads;
        _rules = rules;
        _runs = runs;
        _records = records;
        _tasks = tasks;
        _notifications = notifications;
        _leads = leads;
        _deals = deals;
        _outbox = outbox;
        _sequences = sequences;
        _targets = targets;
        _organizations = organizations;
        _logger = logger;
        _notifier = notifier;
        _modules = modules;
        _tenant = tenant;
    }

    // ── starting runs ────────────────────────────────────────────────────────

    public async Task DispatchAsync(string trigger, AutomationEvent evt, CancellationToken ct = default)
    {
        // Rules belong to an organization. A personal workspace has none of its own, and
        // the rows left with no organization are not any one person's to run.
        if (evt.OrganizationId is not Guid organizationId) return;
        IReadOnlyList<AutomationRule> rules;
        try
        {
            rules = await _rules.EnabledForTriggerAsync(organizationId, trigger, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load automations for {Trigger}", trigger);
            return;
        }
        if (rules.Count == 0) return;

        var definition = AutomationCatalog.Find(trigger);
        var subject = SubjectOf(definition, evt);
        // A rule with no conditions fires for every record, as rules always did. Conditions are
        // checked on the record as the event left it, read once before any rule runs, so what
        // one rule does to the record does not decide whether the next one fires.
        var key = Clip(evt.SourceKey, 80) ?? "";
        AutomationRecordView? view = null;
        if (rules.Any(r => !AutomationConditions.IsEmpty(AutomationConditions.Parse(r.ConditionsJson))))
        {
            // Inside the catch, the lookups too: the save that set the rules off has been
            // committed by the time they are dispatched, and a rule that cannot be looked at
            // must not turn that save into an error for whoever made it.
            try
            {
                if (!await AllFiredAsync(rules, key, ct)) view = await _records.ReadAsync(subject, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Could not read the record for {Trigger} {Source}; rules with conditions did not run", trigger, evt.SourceKey);
            }
        }
        foreach (var rule in rules)
        {
            try
            {
                // A renewal rule says how many days ahead it fires; the event is the period's end.
                if (definition?.TakesDaysBefore == true && !DueWithin(rule, evt)) continue;
                // Fired for this already (the hourly renewal scan sees the same period again and
                // again): nothing to read or decide.
                if (await _runs.HasFiredAsync(rule.Id, key, ct)) continue;
                // Checked before the rule is marked fired: a record that does not meet them now
                // may set it off later.
                var conditions = AutomationConditions.Parse(rule.ConditionsJson);
                if (!AutomationConditions.IsEmpty(conditions)
                    && (view == null || view.Gone != null || !AutomationConditions.Holds(conditions, FactsOf(view, evt.Facts))))
                    continue;
                var run = NewRun(rule, trigger, evt, subject, view?.Name);
                if (!await _runs.StartAsync(run, ct)) continue;
                await _runs.CountRunAsync(rule.Id, ct);
                // Not the request's token: once the run has started, a client that hangs up does
                // not cut its steps off half-way. What runs here was always part of the save.
                await RunStepsAsync(run, rule, CancellationToken.None);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Automation {RuleId} failed for {Source}", rule.Id, evt.SourceKey);
            }
        }
    }

    /// <summary>Whether every rule with conditions has fired for the key already, so the record need not be read.</summary>
    private async Task<bool> AllFiredAsync(IReadOnlyList<AutomationRule> rules, string key, CancellationToken ct)
    {
        foreach (var rule in rules.Where(r => !AutomationConditions.IsEmpty(AutomationConditions.Parse(r.ConditionsJson))))
            if (!await _runs.HasFiredAsync(rule.Id, key, ct)) return false;
        return true;
    }

    public async Task ResumeAsync(DueAutomationRun due, CancellationToken ct = default)
    {
        if (_reads != null) await _reads.ReadAsync("automations", ct);
        var now = Now();
        if (!await _runs.TryClaimAsync(due.Id, due.ClaimCount, now, now.Add(ClaimFor), ct)) return;
        var owner = await _runs.OwnerOfAsync(due.Id, ct);
        if (owner == null) return;
        // The runner starts in no workspace. Everything this run reads and writes (its record,
        // its rule, the tasks and emails it makes) is its own workspace's.
        using var acting = _tenant?.ActFor(owner.Value.OrganizationId, owner.Value.ActorUserId);
        var run = await _runs.GetAsync(due.Id, ct);
        if (run == null) return;
        var rule = await _runs.RuleAsync(run.RuleId, ct);
        // A rule deleted takes its runs with it; one turned off keeps them waiting until it is on again.
        if (rule == null) return;
        if (!rule.Enabled)
        {
            run.ClaimedUntilUtc = null;
            await SaveAsync(run, ct);
            return;
        }
        await RunStepsAsync(run, rule, ct);
    }

    private static AutomationSubject SubjectOf(AutomationTrigger? trigger, AutomationEvent evt)
    {
        var kind = trigger?.Record ?? (evt.LeadId != null ? AutomationRecords.Lead : evt.DealId != null ? AutomationRecords.Deal : AutomationRecords.Task);
        var id = kind switch
        {
            AutomationRecords.Lead => evt.LeadId,
            AutomationRecords.Deal => evt.DealId,
            AutomationRecords.Task => evt.TaskId,
            AutomationRecords.Contract => evt.ContractId,
            AutomationRecords.Member => evt.MemberId,
            _ => evt.RecordId,
        };
        return new AutomationSubject(kind, id, evt.LeadId, evt.DealId, evt.TaskId, evt.MemberId, evt.ContractId);
    }

    private bool DueWithin(AutomationRule rule, AutomationEvent evt)
    {
        if (evt.DueAtUtc is not DateTime due) return false;
        var days = AutomationCatalog.DaysBefore(TriggerConfig(rule));
        var left = due - Now();
        return left > TimeSpan.Zero && left <= TimeSpan.FromDays(days);
    }

    /// <summary>A rule's trigger settings (the days ahead a renewal rule fires); null when it has none.</summary>
    public static IReadOnlyDictionary<string, string>? TriggerConfig(AutomationRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.TriggerConfigJson)) return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(rule.TriggerConfigJson, AutomationSteps.Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private AutomationRun NewRun(AutomationRule rule, string trigger, AutomationEvent evt, AutomationSubject subject, string? recordName)
    {
        var steps = AutomationSteps.Parse(rule.ActionsJson);
        // A rule of actions alone runs its assignments first, as rules always have: they decide
        // who works the record, and the task the same rule creates goes to that person. In the
        // order saved, "create a task, then assign to Rep B" gave the task to whoever set the rule
        // off. A rule with waits or branches runs its steps in the order they are written.
        if (steps.All(s => AutomationStepTypes.Actions.Contains((s.Type ?? "").Trim())))
            steps = steps.OrderBy(s => (s.Type ?? "").Trim() == AutomationStepTypes.Assign ? 0 : 1).ToList();
        var now = Now();
        return new AutomationRun
        {
            Id = Guid.NewGuid(),
            OrganizationId = evt.OrganizationId,
            RuleId = rule.Id,
            RuleName = Clip(rule.Name, 200) ?? "",
            Trigger = trigger,
            SourceKey = Clip(evt.SourceKey, 80) ?? "",
            Status = AutomationRunStatuses.Running,
            ActorUserId = evt.ActorUserId,
            AssigneeId = evt.AssigneeId,
            FromImport = evt.FromImport,
            RecordType = subject.RecordType,
            RecordId = subject.RecordId,
            RecordName = Clip(evt.RecordName ?? recordName, 300),
            LeadId = evt.LeadId,
            DealId = evt.DealId,
            TaskId = evt.TaskId,
            MemberId = evt.MemberId,
            ContractId = evt.ContractId,
            Email = Clip(evt.Email, 320),
            EventJson = evt.Facts is { Count: > 0 } ? JsonSerializer.Serialize(evt.Facts, AutomationSteps.Json) : null,
            StepsJson = AutomationSteps.Serialize(steps),
            CurrentPath = AutomationSteps.First,
            StepStartedAtUtc = now,
            // Held by the save that starts it while its first steps run there; if that save dies,
            // the hold runs out and the runner carries it on.
            ClaimedUntilUtc = now.Add(ClaimFor),
            ClaimCount = 1,
            NextAttemptAtUtc = now,
            StartedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    // ── going through the steps ──────────────────────────────────────────────

    /// <summary>How a step ended, and so where the run goes next.</summary>
    private sealed record Outcome(string Kind, string Summary, DateTime? Until = null, string? WaitingFor = null,
        string? JumpTo = null, bool JumpEnds = false, Guid? MessageId = null, Guid? TaskId = null)
    {
        public const string Done = "done", Skipped = "skipped", Wait = "wait", Jump = "jump", Stop = "stop", Fail = "fail";
        public static Outcome Did(string summary, Guid? messageId = null, Guid? taskId = null) => new(Done, summary, MessageId: messageId, TaskId: taskId);
        public static Outcome Skip(string reason) => new(Skipped, reason);
        public static Outcome Hold(DateTime until, string waitingFor, string summary) => new(Wait, summary, until, waitingFor);
        public static Outcome Go(string? path, string summary) => new(Jump, summary, JumpTo: path, JumpEnds: path == null);
        public static Outcome End(string reason) => new(Stop, reason);
        public static Outcome Failed(string error) => new(Fail, error);
    }

    /// <summary>
    /// Runs the run's steps from where it is, saving after each, until it finishes, has to
    /// wait, or a step fails.
    /// </summary>
    private async Task RunStepsAsync(AutomationRun run, AutomationRule rule, CancellationToken ct)
    {
        var steps = AutomationSteps.Parse(run.StepsJson);
        var trigger = AutomationCatalog.Find(run.Trigger);
        for (var go = 0; go < MaxStepsPerGo; go++)
        {
            var now = Now();
            var step = AutomationSteps.At(steps, run.CurrentPath);
            if (step == null)
            {
                Finish(run, AutomationRunStatuses.Completed, null, now);
                await SaveAsync(run, ct);
                return;
            }

            Outcome outcome;
            if (trigger?.Module == WorkspaceModules.Club && run.OrganizationId is Guid org && _modules != null
                && !await _modules.IsOnAsync(org, WorkspaceModules.Club, ct))
            {
                // The club's pages are gone while it is off, so nothing its rules do could be seen
                // or stopped there: the run waits, and carries on when the club is on again.
                outcome = Outcome.Hold(now.Add(ModuleOffRecheck), "the Club module to be turned back on",
                    "Waiting: the Club module is turned off.");
            }
            else
            {
                var checkpoint = _runs.Checkpoint();
                try
                {
                    outcome = await StepAsync(run, rule, step, steps, trigger, now, ct);
                    // A step can fail without throwing, when a service catches the error and
                    // answers with it: what it left half-made in the unit of work is taken back
                    // too, or the save of the run's log would make it after all, and the retry
                    // make it a second time.
                    if (outcome.Kind == Outcome.Fail) _runs.RevertTo(checkpoint);
                }
                catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                {
                    // Whatever the step left unsaved is taken back, so the save of the run's log,
                    // and of the request it ran in, does not try it again.
                    _runs.RevertTo(checkpoint);
                    _logger.LogWarning(ex, "Automation run {RunId} failed at step {Path}", run.Id, run.CurrentPath);
                    outcome = Outcome.Failed(Clip(ex.Message, 900) ?? "The step failed.");
                }
            }

            if (!await ApplyAsync(run, step, steps, outcome, now, ct)) return;
        }
        // Many steps in one go: the rest is for the runner, at once.
        run.Status = AutomationRunStatuses.Running;
        run.NextAttemptAtUtc = Now();
        run.ClaimedUntilUtc = null;
        await SaveAsync(run, ct);
    }

    /// <summary>Writes a step's outcome on the run and its log; true when the run goes on to its next step now.</summary>
    private async Task<bool> ApplyAsync(AutomationRun run, AutomationStep step, IReadOnlyList<AutomationStep> steps, Outcome outcome, DateTime now, CancellationToken ct)
    {
        var path = run.CurrentPath;
        switch (outcome.Kind)
        {
            case Outcome.Done or Outcome.Skipped:
                Log(run, step, path, outcome.Kind == Outcome.Done ? AutomationStepOutcomes.Done : AutomationStepOutcomes.Skipped,
                    outcome.Summary, null, outcome.MessageId, outcome.TaskId, run.StepAttempts + 1);
                if (!MoveTo(run, AutomationSteps.Next(steps, path), now)) break;
                return await SaveAsync(run, ct);

            case Outcome.Jump:
                Log(run, step, path, AutomationStepOutcomes.Done, outcome.Summary, null, null, null, run.StepAttempts + 1);
                if (!MoveTo(run, outcome.JumpTo, now)) break;
                return await SaveAsync(run, ct);

            case Outcome.Wait:
                // Said once in the log, not again each time it looks and has to go on waiting.
                if (!string.Equals(run.WaitingFor, outcome.WaitingFor, StringComparison.Ordinal))
                    Log(run, step, path, AutomationStepOutcomes.Waiting, outcome.Summary, null, null, null, run.StepAttempts + 1);
                run.Status = AutomationRunStatuses.Waiting;
                run.WaitingFor = Clip(outcome.WaitingFor, 300);
                run.NextAttemptAtUtc = outcome.Until;
                run.ClaimedUntilUtc = null;
                await SaveAsync(run, ct);
                return false;

            case Outcome.Stop:
                Log(run, step, path, AutomationStepOutcomes.Skipped, outcome.Summary, null, null, null, run.StepAttempts + 1);
                Finish(run, AutomationRunStatuses.Cancelled, outcome.Summary, now);
                break;

            default:
                run.StepAttempts += 1;
                var error = Clip(outcome.Summary, 1000);
                if (run.StepAttempts < MaxStepAttempts)
                {
                    var delay = RetryDelays[Math.Min(run.StepAttempts - 1, RetryDelays.Length - 1)];
                    Log(run, step, path, AutomationStepOutcomes.Retrying, $"Failed; tried again in {Words(delay)}.", error, null, null, run.StepAttempts);
                    run.Status = AutomationRunStatuses.Retrying;
                    run.LastError = error;
                    run.WaitingFor = null;
                    run.NextAttemptAtUtc = now.Add(delay);
                    run.ClaimedUntilUtc = null;
                }
                else
                {
                    Log(run, step, path, AutomationStepOutcomes.Failed, $"Failed {run.StepAttempts} times; the run stopped.", error, null, null, run.StepAttempts);
                    Finish(run, AutomationRunStatuses.Failed, error, now);
                }
                break;
        }
        await SaveAsync(run, ct);
        return false;
    }

    /// <summary>Puts the run at the step at <paramref name="path"/>; false when there is none, and it has finished.</summary>
    private static bool MoveTo(AutomationRun run, string? path, DateTime now)
    {
        if (path == null)
        {
            Finish(run, AutomationRunStatuses.Completed, null, now);
            return false;
        }
        run.CurrentPath = path;
        run.StepStartedAtUtc = now;
        run.StepAttempts = 0;
        run.Status = AutomationRunStatuses.Running;
        run.WaitingFor = null;
        run.LastError = null;
        return true;
    }

    private static void Finish(AutomationRun run, string status, string? reason, DateTime now)
    {
        run.Status = status;
        run.FinishedAtUtc = now;
        run.NextAttemptAtUtc = null;
        run.ClaimedUntilUtc = null;
        run.WaitingFor = null;
        if (status != AutomationRunStatuses.Completed) run.LastError = Clip(reason, 1000);
    }

    private void Log(AutomationRun run, AutomationStep step, string path, string outcome, string summary, string? error, Guid? messageId, Guid? taskId, int attempt) =>
        _runs.Log(new AutomationRunStep
        {
            Id = Guid.NewGuid(),
            OrganizationId = run.OrganizationId,
            RunId = run.Id,
            Path = Clip(path, 40) ?? "",
            StepType = Clip(step.Type, 40) ?? "",
            Outcome = outcome,
            Summary = Clip(summary, 1000) ?? "",
            Error = Clip(error, 1000),
            Attempt = Math.Max(1, attempt),
            OutboundMessageId = messageId,
            TaskId = taskId,
            AtUtc = Now(),
        });

    /// <summary>Saves the run; false when another runner has taken it meanwhile, and this one must stop.</summary>
    private async Task<bool> SaveAsync(AutomationRun run, CancellationToken ct)
    {
        run.UpdatedAtUtc = Now();
        try
        {
            await _runs.SaveAsync(ct);
            return true;
        }
        catch (ConcurrencyConflictException)
        {
            _logger.LogWarning("Automation run {RunId} was taken by another runner while it ran; this one stops", run.Id);
            return false;
        }
    }

    /// <summary>What one step does.</summary>
    private async Task<Outcome> StepAsync(AutomationRun run, AutomationRule rule, AutomationStep step, IReadOnlyList<AutomationStep> steps,
        AutomationTrigger? trigger, DateTime now, CancellationToken ct)
    {
        var config = step.Config ?? new Dictionary<string, string>();
        switch ((step.Type ?? "").Trim())
        {
            case AutomationStepTypes.Wait:
                return await WaitAsync(run, config, trigger, now, ct);
            case AutomationStepTypes.Branch:
            {
                var view = await ViewAsync(run, ct);
                if (view.Gone != null) return Outcome.End(view.Gone);
                var holds = AutomationConditions.Holds(step.Condition, FactsOf(view, EventFacts(run)));
                var next = AutomationSteps.Enter(steps, run.CurrentPath, holds);
                var said = AutomationConditions.Describe(step.Condition,
                    trigger == null ? null : AutomationCatalog.WithFlex(trigger, view.FlexFields, view.FlexDates));
                return Outcome.Go(next, holds ? $"Yes: {said}." : $"No: not {said}.");
            }
            case AutomationStepTypes.SendEmail:
            {
                // Held for the rule's send window, whatever came before it: the run carries on
                // from here when the window next opens.
                var window = SendWindows.Parse(rule.SendWindowJson);
                if (window != null && run.OrganizationId is Guid org)
                {
                    var zone = await _records.ZoneAsync(org, ct);
                    var opening = SendWindows.NextOpening(window, zone, now);
                    if (opening > now)
                        return Outcome.Hold(opening, $"the send window ({SendWindows.Describe(window)})",
                            $"Held for the send window until {Local(opening, zone)}.");
                }
                return await SendAsync(run, config, ct);
            }
            case AutomationStepTypes.CreateTask:
                return await CreateTaskAsync(run, config, ct);
            case AutomationStepTypes.Notify:
                return await NotifyAsync(run, config, ct);
            case AutomationStepTypes.Assign:
                return await AssignAsync(run, config, ct);
            case AutomationStepTypes.AddToSequence:
                return await EnrollAsync(run, config, ct);
            default:
                return Outcome.Skip($"\"{step.Type}\" is not a step this version knows.");
        }
    }

    private async Task<Outcome> WaitAsync(AutomationRun run, IReadOnlyDictionary<string, string> config, AutomationTrigger? trigger, DateTime now, CancellationToken ct)
    {
        var zone = run.OrganizationId is Guid org ? await _records.ZoneAsync(org, ct) : TimeZoneInfo.Utc;
        DateTime until;
        string waitingFor;
        if (string.Equals(Value(config, "mode"), "until", StringComparison.OrdinalIgnoreCase))
        {
            // Read now, every time it looks: a booking moved to another day moves the wait with it.
            var key = Value(config, "date") ?? "";
            var view = await ViewAsync(run, ct);
            if (view.Gone != null) return Outcome.End(view.Gone);
            var label = trigger?.Dates.FirstOrDefault(d => d.Key == key)?.Label
                ?? view.FlexDates.FirstOrDefault(d => d.Key == key)?.Label ?? key;
            if (!view.Dates.TryGetValue(key, out var date))
                return Outcome.End($"Stopped: there is no {Lower(label)} to wait for.");
            until = date.Add(Span(Value(config, "offsetAmount"), Value(config, "offsetUnit")));
            waitingFor = $"{Offset(Value(config, "offsetAmount"), Value(config, "offsetUnit"))}{Lower(label)}";
        }
        else
        {
            var span = Span(Value(config, "amount"), Value(config, "unit"));
            until = (run.StepStartedAtUtc ?? now).Add(span);
            waitingFor = Words(span);
        }
        if (now >= until) return Outcome.Did($"Waited ({waitingFor}).");
        return Outcome.Hold(until, $"{waitingFor} (until {Local(until, zone)})", $"Waiting {waitingFor}, until {Local(until, zone)}.");
    }

    // ── the record, read when a step needs it ────────────────────────────────

    /// <summary>
    /// The run's record as it is now. Read again by every step that needs it: a branch after an
    /// assign, or a wait for a booking that was moved, sees the record as it is then.
    /// </summary>
    private Task<AutomationRecordView> ViewAsync(AutomationRun run, CancellationToken ct) =>
        _records.ReadAsync(new AutomationSubject(run.RecordType, run.RecordId, run.LeadId, run.DealId, run.TaskId, run.MemberId, run.ContractId), ct);

    private static IReadOnlyDictionary<string, string[]>? EventFacts(AutomationRun run)
    {
        if (string.IsNullOrWhiteSpace(run.EventJson)) return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string[]>>(run.EventJson, AutomationSteps.Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The record's facts with what the event knew of it on top (the status it came from).</summary>
    private static IReadOnlyDictionary<string, FactValue> FactsOf(AutomationRecordView view, IReadOnlyDictionary<string, string[]>? eventFacts)
    {
        if (eventFacts == null || eventFacts.Count == 0) return view.Facts;
        var facts = new Dictionary<string, FactValue>(view.Facts, StringComparer.Ordinal);
        foreach (var (key, texts) in eventFacts) facts[key] = FactValue.Of(texts);
        return facts;
    }

    // ── words ────────────────────────────────────────────────────────────────

    private static TimeSpan Span(string? amount, string? unit)
    {
        var n = int.TryParse(amount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        return (unit ?? "days").Trim().ToLowerInvariant() switch
        {
            "minutes" => TimeSpan.FromMinutes(n),
            "hours" => TimeSpan.FromHours(n),
            "weeks" => TimeSpan.FromDays(7 * n),
            _ => TimeSpan.FromDays(n),
        };
    }

    private static string Offset(string? amount, string? unit)
    {
        var span = Span(amount, unit);
        if (span == TimeSpan.Zero) return "";
        return span < TimeSpan.Zero ? $"{Words(span.Negate())} before " : $"{Words(span)} after ";
    }

    internal static string Words(TimeSpan span)
    {
        static string Count(double n, string one) => $"{n:0.##} {one}{(Math.Abs(n - 1) < 0.0001 ? "" : "s")}";
        if (span.TotalDays >= 1 && span.TotalDays % 7 == 0 && span.TotalDays >= 14) return Count(span.TotalDays / 7, "week");
        if (span.TotalDays >= 1) return Count(span.TotalDays, "day");
        if (span.TotalHours >= 1) return Count(span.TotalHours, "hour");
        return Count(Math.Max(0, span.TotalMinutes), "minute");
    }

    private static string Local(DateTime utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone)
            .ToString("ddd d MMM yyyy, HH:mm", CultureInfo.GetCultureInfo("en-GB"));

    private static string Lower(string text) => text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];

    private static string? Value(IReadOnlyDictionary<string, string> config, string key) =>
        config.TryGetValue(key, out var value) ? value : null;

    private static string? Clip(string? text, int max) => text == null ? null : text.Length <= max ? text : text[..max];
}
