using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

public record AutomationRuleDto(
    Guid Id,
    string Name,
    bool Enabled,
    string Trigger,
    Dictionary<string, string>? TriggerConfig,
    ConditionGroup? Conditions,
    SendWindow? SendWindow,
    IReadOnlyList<AutomationStep> Actions,
    int RunCount,
    DateTime CreatedAtUtc,
    Guid Version,
    int ActiveRuns,
    int FailedRuns);

/// <summary>
/// A rule as the builder sends it. On an edit, a field left out is kept as it was, and
/// conditions, a send window or trigger settings sent as null are taken off.
/// </summary>
public class AutomationWriteRequest : IMergePatchRequest
{
    public string? Name { get; set; }
    public bool? Enabled { get; set; }
    public string? Trigger { get; set; }
    public Dictionary<string, string>? TriggerConfig { get; set; }
    public ConditionGroup? Conditions { get; set; }
    public SendWindow? SendWindow { get; set; }

    /// <summary>The steps: actions, waits and branches.</summary>
    public List<AutomationStep>? Actions { get; set; }

    /// <summary>The version the rule was read at; an edit of an older one is refused (409).</summary>
    public Guid? Version { get; set; }

    [JsonIgnore]
    public ISet<string> NullsSent { get; } = new HashSet<string>(StringComparer.Ordinal);
}

/// <summary>What the rule builder is drawn from: the triggers and the comparisons by kind of field.</summary>
public sealed record AutomationCatalogDto(IReadOnlyList<AutomationTrigger> Triggers, IReadOnlyDictionary<string, IReadOnlyList<string>> Ops);

/// <summary>Wakes the automation runner on this instance, for a run that is due now.</summary>
public interface IAutomationSignal
{
    void Notify();
}

public interface IAutomationService
{
    Task<IReadOnlyList<AutomationRuleDto>> ListAsync(Guid? organizationId, CancellationToken ct = default);
    Task<Result<AutomationRuleDto>> CreateAsync(Guid userId, Guid? organizationId, AutomationWriteRequest request, CancellationToken ct = default);
    Task<Result<AutomationRuleDto>> UpdateAsync(Guid id, Guid? organizationId, AutomationWriteRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid? organizationId, CancellationToken ct = default);
    AutomationCatalogDto Catalog();

    /// <summary>The catalogue as the caller's workspace sees it: each trigger with the workspace's tags and custom fields its rules can test.</summary>
    Task<AutomationCatalogDto> CatalogAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AutomationRunDto>> RunsAsync(Guid? ruleId, string? status, int take, CancellationToken ct = default);
    Task<Result<AutomationRunDetailDto>> RunAsync(Guid runId, CancellationToken ct = default);
    Task<Result<AutomationRunDto>> RetryAsync(Guid runId, CancellationToken ct = default);
    Task<Result<AutomationRunDto>> CancelAsync(Guid runId, CancellationToken ct = default);
}

public sealed class AutomationService : IAutomationService
{
    private const int MaxName = 200;
    private const int MaxRuns = 200;
    private const int MaxTitle = 300;
    private const int MaxSubject = 300;
    private const int MaxBody = 10_000;

    private readonly IAutomationRepository _rules;
    private readonly IOrganizationRepository? _organizations;
    private readonly ISequenceRepository? _sequences;
    private readonly IAutomationRunStore? _runs;
    private readonly IAutomationSignal? _signal;
    private readonly IAutomationRecordReader? _records;

    public AutomationService(IAutomationRepository rules, IOrganizationRepository? organizations = null, ISequenceRepository? sequences = null,
        IAutomationRunStore? runs = null, IAutomationSignal? signal = null, IAutomationRecordReader? records = null)
    {
        _rules = rules;
        _organizations = organizations;
        _sequences = sequences;
        _runs = runs;
        _signal = signal;
        _records = records;
    }

    /// <summary>The workspace's custom fields and tags, which its rules can test besides a trigger's own fields.</summary>
    private Task<AutomationWorkspaceFields> WorkspaceFieldsAsync(CancellationToken ct) =>
        _records?.WorkspaceFieldsAsync(ct) ?? Task.FromResult(AutomationWorkspaceFields.None);

    public async Task<IReadOnlyList<AutomationRuleDto>> ListAsync(Guid? organizationId, CancellationToken ct = default)
    {
        var rows = await _rules.ListAsync(organizationId, ct);
        var counts = _runs == null ? new Dictionary<Guid, AutomationRuleRunCounts>() : await _runs.CountsAsync(ct);
        return rows.Select(r => ToDto(r, counts.GetValueOrDefault(r.Id))).ToList();
    }

    public async Task<Result<AutomationRuleDto>> CreateAsync(Guid userId, Guid? organizationId, AutomationWriteRequest request, CancellationToken ct = default)
    {
        var name = (request.Name ?? "").Trim();
        if (name.Length == 0) return new Error("Automation.NameRequired", "A rule needs a name.");
        if (name.Length > MaxName) return NameTooLong;
        var trigger = AutomationCatalog.Find((request.Trigger ?? "").Trim());
        if (trigger == null) return TriggerInvalid;
        var steps = Normalize(request.Actions);
        if (steps.Count == 0) return ActionRequired;
        var sent = AutomationConditions.Clean(request.Conditions);
        var conditions = AutomationConditions.IsEmpty(sent) ? null : sent;
        if (await CheckAsync(trigger, request.TriggerConfig, conditions, request.SendWindow, steps, organizationId, ct) is { } invalid) return invalid;

        var row = new AutomationRule
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            OwnerUserId = userId,
            Name = name,
            Enabled = request.Enabled ?? true,
            Trigger = trigger.Key,
            TriggerConfigJson = TriggerConfigJson(trigger, request.TriggerConfig),
            ConditionsJson = AutomationConditions.Serialize(conditions),
            ActionsJson = AutomationSteps.Serialize(steps),
            SendWindowJson = SendWindows.Serialize(request.SendWindow),
            CreatedAtUtc = DateTime.UtcNow,
            Version = Guid.NewGuid(),
        };
        await _rules.SaveAsync(row, ct);
        return ToDto(row, null);
    }

    public async Task<Result<AutomationRuleDto>> UpdateAsync(Guid id, Guid? organizationId, AutomationWriteRequest request, CancellationToken ct = default)
    {
        var row = await _rules.GetAsync(id, organizationId, ct);
        if (row == null) return DomainErrors.Automation.NotFound;
        // Someone saved the rule after this edit read it: theirs stands, and the editor is told.
        if (request.Version is Guid expected && expected != row.Version) return DomainErrors.Automation.Changed;

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            if (request.Name.Trim().Length > MaxName) return NameTooLong;
            row.Name = request.Name.Trim();
        }
        if (request.Enabled.HasValue) row.Enabled = request.Enabled.Value;

        // What the rule will be once this edit is applied, checked as a whole: conditions and
        // steps that fitted the old trigger may not fit a new one.
        var trigger = AutomationCatalog.Find(string.IsNullOrWhiteSpace(request.Trigger) ? row.Trigger : request.Trigger.Trim());
        if (trigger == null) return TriggerInvalid;
        // Settings left as they were keep only the ones the trigger has: keys an older client
        // stored, which name a condition, were copied into the conditions and are not settings.
        var config = request.TriggerConfig ?? (request.Clears(nameof(request.TriggerConfig)) ? null
            : AutomationEngine.TriggerConfig(row)?
                .Where(p => trigger.TakesDaysBefore && p.Key == AutomationCatalog.DaysBeforeKey)
                .ToDictionary(p => p.Key, p => p.Value));
        var sent = AutomationConditions.Clean(request.Conditions);
        var conditions = sent != null
            ? (AutomationConditions.IsEmpty(sent) ? null : sent)
            : request.Clears(nameof(request.Conditions)) ? null : AutomationConditions.Parse(row.ConditionsJson);
        var window = request.SendWindow ?? (request.Clears(nameof(request.SendWindow)) ? null : SendWindows.Parse(row.SendWindowJson));
        var steps = request.Actions != null ? Normalize(request.Actions) : AutomationSteps.Parse(row.ActionsJson);
        if (request.Actions != null && steps.Count == 0) return ActionRequired;
        // The steps are checked again when they change, and when the trigger does: steps kept
        // from a lead's rule (an assignment, a sequence, a wait for a lead's date) cannot run on
        // a member's, and moved there unchecked they would be skipped every time it fires.
        var triggerChanged = !string.Equals(trigger.Key, row.Trigger, StringComparison.Ordinal);
        var stepsChanged = request.Actions != null || triggerChanged;
        // Conditions kept as they were are not checked again either: one on a custom field
        // deleted since would otherwise keep the rule from being renamed or switched off.
        var conditionsChanged = sent != null || request.Clears(nameof(request.Conditions)) || triggerChanged;
        if (await CheckAsync(trigger, config, conditions, window, steps, organizationId, ct, stepsChanged, conditionsChanged) is { } invalid)
            return invalid;

        row.Trigger = trigger.Key;
        row.TriggerConfigJson = TriggerConfigJson(trigger, config);
        row.ConditionsJson = AutomationConditions.Serialize(conditions);
        row.SendWindowJson = SendWindows.Serialize(window);
        if (request.Actions != null) row.ActionsJson = AutomationSteps.Serialize(steps);
        row.Version = Guid.NewGuid();
        try
        {
            await _rules.SaveAsync(row, ct);
        }
        catch (ConcurrencyConflictException)
        {
            return DomainErrors.Automation.Changed;
        }
        var counts = _runs == null ? null : (await _runs.CountsAsync(ct)).GetValueOrDefault(row.Id);
        return ToDto(row, counts);
    }

    public async Task<Result> DeleteAsync(Guid id, Guid? organizationId, CancellationToken ct = default)
    {
        var deleted = await _rules.DeleteAsync(id, organizationId, ct);
        return deleted ? Result.Success() : DomainErrors.Automation.NotFound;
    }

    public AutomationCatalogDto Catalog() => Catalog(AutomationCatalog.Triggers);

    public async Task<AutomationCatalogDto> CatalogAsync(CancellationToken ct = default)
    {
        var workspace = await WorkspaceFieldsAsync(ct);
        return Catalog(AutomationCatalog.Triggers.Select(t => AutomationCatalog.WithFlex(t, workspace.CustomFields, workspace.Tags)).ToList());
    }

    private static AutomationCatalogDto Catalog(IReadOnlyList<AutomationTrigger> triggers) => new(triggers, new Dictionary<string, IReadOnlyList<string>>
    {
        [AutomationFieldTypes.Text] = AutomationOps.For(AutomationFieldTypes.Text),
        [AutomationFieldTypes.Choice] = AutomationOps.For(AutomationFieldTypes.Choice),
        [AutomationFieldTypes.User] = AutomationOps.For(AutomationFieldTypes.User),
        [AutomationFieldTypes.Number] = AutomationOps.For(AutomationFieldTypes.Number),
        [AutomationFieldTypes.Money] = AutomationOps.For(AutomationFieldTypes.Money),
        [AutomationFieldTypes.Bool] = AutomationOps.For(AutomationFieldTypes.Bool),
    });

    private static readonly Error NameTooLong = new("Automation.NameTooLong", $"A rule name can be at most {MaxName} characters.");
    private static readonly Error TriggerInvalid = new("Automation.TriggerInvalid", "That trigger is not supported.");
    private static readonly Error ActionRequired = new("Automation.ActionRequired", "A rule needs at least one action.");

    // ── checking a rule ──────────────────────────────────────────────────────

    /// <summary>
    /// What is wrong with a rule as it would be saved; null when nothing is. What an action
    /// points at has to be in this workspace: a rule that assigns leads to someone outside it,
    /// or enrolls them in another workspace's sequence, is refused when it is saved rather than
    /// failing quietly every time it runs.
    /// </summary>
    private async Task<Error?> CheckAsync(AutomationTrigger trigger, IReadOnlyDictionary<string, string>? config, ConditionGroup? conditions,
        SendWindow? window, IReadOnlyList<AutomationStep> steps, Guid? organizationId, CancellationToken ct, bool stepsChanged = true,
        bool conditionsChanged = true)
    {
        // The trigger as this workspace sees it: with its tags and custom fields to test, and
        // its custom dates to wait for.
        if (conditionsChanged || stepsChanged)
        {
            var workspace = await WorkspaceFieldsAsync(ct);
            trigger = AutomationCatalog.WithFlex(trigger, workspace.CustomFields, workspace.Tags);
        }
        foreach (var key in (config ?? new Dictionary<string, string>()).Keys)
        {
            if (!trigger.TakesDaysBefore || key != AutomationCatalog.DaysBeforeKey)
                return DomainErrors.Automation.Invalid(
                    $"\"{key}\" is not a setting of this trigger. To run a rule only for some records, give it conditions.");
            if (!int.TryParse(config![key], NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) || days < 1 || days > AutomationCatalog.MaxDaysBefore)
                return DomainErrors.Automation.Invalid($"A renewal rule fires between 1 and {AutomationCatalog.MaxDaysBefore} days ahead.");
        }
        if (conditionsChanged && AutomationConditions.Problem(conditions, trigger) is { } badConditions) return DomainErrors.Automation.Invalid(badConditions);
        if (window != null && SendWindows.Problem(window) is { } badWindow) return DomainErrors.Automation.Invalid(badWindow);
        // Steps saved before are not checked again when only the name or the switch changes:
        // a rule that was valid then keeps saving, as it did.
        if (!stepsChanged) return null;
        if (AutomationSteps.Count(steps) > AutomationSteps.MaxSteps)
            return new Error("Automation.TooManyActions", $"A rule can have at most {AutomationSteps.MaxSteps} steps.");
        if (AutomationSteps.Depth(steps) > AutomationSteps.MaxDepth)
            return DomainErrors.Automation.Invalid($"Branches can go at most {AutomationSteps.MaxDepth} deep.");
        return await CheckStepsAsync(trigger, steps, organizationId, ct);
    }

    private async Task<Error?> CheckStepsAsync(AutomationTrigger trigger, IReadOnlyList<AutomationStep> steps, Guid? organizationId, CancellationToken ct)
    {
        foreach (var step in steps)
        {
            var config = step.Config;
            switch (step.Type)
            {
                case AutomationStepTypes.Wait:
                    if (WaitProblem(trigger, config) is { } badWait) return DomainErrors.Automation.Invalid(badWait);
                    continue;
                case AutomationStepTypes.Branch:
                    if (AutomationConditions.IsEmpty(step.Condition))
                        return DomainErrors.Automation.Invalid("A branch needs a condition to decide on.");
                    if (AutomationConditions.Problem(step.Condition, trigger) is { } badBranch) return DomainErrors.Automation.Invalid(badBranch);
                    if ((step.Then ?? []).Count == 0 && (step.Else ?? []).Count == 0)
                        return DomainErrors.Automation.Invalid("A branch needs at least one step on one of its sides.");
                    if (await CheckStepsAsync(trigger, step.Then ?? [], organizationId, ct) is { } badThen) return badThen;
                    if (await CheckStepsAsync(trigger, step.Else ?? [], organizationId, ct) is { } badElse) return badElse;
                    continue;
            }
            if (!trigger.Actions.Contains(step.Type))
                return DomainErrors.Automation.Invalid($"\"{trigger.Label}\" cannot run the step \"{step.Type}\".");
            switch (step.Type)
            {
                case AutomationStepTypes.Assign when _organizations != null && organizationId is Guid org:
                    if (!Guid.TryParse(config.GetValueOrDefault("userId"), out var userId)
                        || await _organizations.GetMemberRoleAsync(userId, org, ct) == null)
                        return new Error("Automation.AssigneeInvalid", "Assign to someone in this workspace.");
                    break;
                case AutomationStepTypes.AddToSequence when _sequences != null:
                    if (!Guid.TryParse(config.GetValueOrDefault("sequenceId"), out var sequenceId)
                        || await _sequences.GetAsync(sequenceId, organizationId, ct) == null)
                        return new Error("Automation.SequenceInvalid", "Pick a sequence from this workspace.");
                    break;
                case AutomationStepTypes.SendEmail:
                    var subject = config.GetValueOrDefault("subject");
                    var body = config.GetValueOrDefault("body");
                    if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
                        return new Error("Automation.EmailIncomplete", "An email action needs a subject and a body.");
                    if (subject.Length > MaxSubject || body.Length > MaxBody)
                        return DomainErrors.Automation.Invalid($"An email's subject can be at most {MaxSubject} characters and its body {MaxBody}.");
                    if (config.GetValueOrDefault("purpose") is { Length: > 0 } purpose
                        && purpose is not (ConsentPurposes.Marketing or ConsentPurposes.Service))
                        return DomainErrors.Automation.Invalid("An email to a member is for marketing or about their membership.");
                    break;
                case AutomationStepTypes.CreateTask:
                    if ((config.GetValueOrDefault("title") ?? "").Length > MaxTitle)
                        return DomainErrors.Automation.Invalid($"A task title can be at most {MaxTitle} characters.");
                    if (config.GetValueOrDefault("dueInDays") is { Length: > 0 } due
                        && (!int.TryParse(due, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dueDays) || dueDays < 0 || dueDays > 365))
                        return DomainErrors.Automation.Invalid("A task is due between 0 and 365 days after the step runs.");
                    if (await PersonProblemAsync(config.GetValueOrDefault("assigneeId"), organizationId, ct) is { } badAssignee) return badAssignee;
                    break;
                case AutomationStepTypes.Notify:
                    if (await PersonProblemAsync(config.GetValueOrDefault("userId"), organizationId, ct) is { } badRecipient) return badRecipient;
                    break;
            }
        }
        return null;
    }

    /// <summary>A person a step may name: none, or someone in this workspace.</summary>
    private async Task<Error?> PersonProblemAsync(string? raw, Guid? organizationId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raw) || _organizations == null || organizationId is not Guid org) return null;
        return Guid.TryParse(raw, out var userId) && await _organizations.GetMemberRoleAsync(userId, org, ct) != null
            ? null
            : new Error("Automation.AssigneeInvalid", "Pick someone in this workspace.");
    }

    private static readonly string[] Units = ["minutes", "hours", "days", "weeks"];

    private static string? WaitProblem(AutomationTrigger trigger, IReadOnlyDictionary<string, string> config)
    {
        if (string.Equals(config.GetValueOrDefault("mode"), "until", StringComparison.OrdinalIgnoreCase))
        {
            var date = config.GetValueOrDefault("date");
            if (trigger.Dates.All(d => d.Key != date))
                return trigger.Dates.Count == 0
                    ? $"\"{trigger.Label}\" has no date to wait for; wait for a time instead."
                    : "Pick the date to wait for.";
            var offset = config.GetValueOrDefault("offsetAmount");
            if (!string.IsNullOrWhiteSpace(offset)
                && (!int.TryParse(offset, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) || Math.Abs(n) > 365))
                return "Give the time before or after the date as a whole number up to 365.";
            if (!string.IsNullOrWhiteSpace(config.GetValueOrDefault("offsetUnit")) && !Units.Contains(config["offsetUnit"]))
                return "A wait is counted in minutes, hours, days or weeks.";
            return null;
        }
        if (!int.TryParse(config.GetValueOrDefault("amount"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) || amount < 1)
            return "Say how long to wait, as a whole number.";
        var unit = config.GetValueOrDefault("unit") ?? "days";
        if (!Units.Contains(unit)) return "A wait is counted in minutes, hours, days or weeks.";
        var span = unit switch { "minutes" => TimeSpan.FromMinutes(amount), "hours" => TimeSpan.FromHours(amount), "weeks" => TimeSpan.FromDays(7 * amount), _ => TimeSpan.FromDays(amount) };
        return span > TimeSpan.FromDays(366) ? "A wait can last at most a year." : null;
    }

    /// <summary>
    /// The steps as they will be kept: types trimmed, each with an id of its own (one sent is
    /// kept, so a step keeps its id across edits), a branch's sides cleaned the same way.
    /// </summary>
    private static List<AutomationStep> Normalize(IEnumerable<AutomationStep>? steps) =>
        (steps ?? []).Where(s => s != null).Select(s =>
        {
            var type = (s.Type ?? "").Trim();
            return new AutomationStep
            {
                Id = s.Id == Guid.Empty ? Guid.NewGuid() : s.Id,
                Type = type,
                Config = (s.Config ?? new Dictionary<string, string>())
                    .Where(p => p.Value != null)
                    .ToDictionary(p => p.Key, p => p.Value),
                Condition = type == AutomationStepTypes.Branch ? AutomationConditions.Clean(s.Condition) : null,
                Then = type == AutomationStepTypes.Branch ? Normalize(s.Then) : null,
                Else = type == AutomationStepTypes.Branch ? Normalize(s.Else) : null,
            };
        // A step of a type this version does not know is kept here so the check refuses it by
        // name, rather than dropped without a word.
        }).Where(s => s.Type.Length > 0).ToList();

    private static string? TriggerConfigJson(AutomationTrigger trigger, IReadOnlyDictionary<string, string>? config) =>
        config == null || config.Count == 0 || !trigger.TakesDaysBefore
            ? null
            : JsonSerializer.Serialize(config.Where(p => p.Key == AutomationCatalog.DaysBeforeKey).ToDictionary(p => p.Key, p => p.Value.Trim()), AutomationSteps.Json);

    private static AutomationRuleDto ToDto(AutomationRule row, AutomationRuleRunCounts? counts)
    {
        var trigger = AutomationCatalog.Find(row.Trigger);
        // Only the settings the trigger has: keys an older client stored, which name a
        // condition, were copied into the conditions and are not shown as settings.
        var config = AutomationEngine.TriggerConfig(row)?
            .Where(p => trigger?.TakesDaysBefore == true && p.Key == AutomationCatalog.DaysBeforeKey)
            .ToDictionary(p => p.Key, p => p.Value);
        return new AutomationRuleDto(row.Id, row.Name, row.Enabled, row.Trigger,
            config is { Count: > 0 } ? config : null,
            AutomationConditions.Parse(row.ConditionsJson),
            SendWindows.Parse(row.SendWindowJson),
            AutomationSteps.Parse(row.ActionsJson),
            row.RunCount, row.CreatedAtUtc, row.Version,
            counts?.Active ?? 0, counts?.Failed ?? 0);
    }

    // ── runs ─────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AutomationRunDto>> RunsAsync(Guid? ruleId, string? status, int take, CancellationToken ct = default)
    {
        if (_runs == null) return [];
        var rows = await _runs.ListAsync(ruleId, status, Math.Clamp(take, 1, MaxRuns), ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<Result<AutomationRunDetailDto>> RunAsync(Guid runId, CancellationToken ct = default)
    {
        if (_runs == null) return DomainErrors.Automation.RunNotFound;
        var run = await _runs.FindAsync(runId, ct);
        if (run == null) return DomainErrors.Automation.RunNotFound;
        var steps = await _runs.StepsAsync(runId, ct);
        var emails = await _runs.EmailStatusesAsync(steps.Where(s => s.OutboundMessageId != null).Select(s => s.OutboundMessageId!.Value).ToList(), ct);
        return new AutomationRunDetailDto(ToDto(run), steps.Select(s => new AutomationRunStepDto(
            s.Id, s.Path, s.StepType, s.Outcome, s.Summary, s.Error, s.Attempt, s.AtUtc, s.OutboundMessageId,
            s.OutboundMessageId is Guid m ? emails.GetValueOrDefault(m) : null, s.TaskId)).ToList());
    }

    public async Task<Result<AutomationRunDto>> RetryAsync(Guid runId, CancellationToken ct = default)
    {
        if (_runs == null || await _runs.FindAsync(runId, ct) == null) return DomainErrors.Automation.RunNotFound;
        if (!await _runs.TryRetryAsync(runId, DateTime.UtcNow, ct)) return DomainErrors.Automation.RunNotFailed;
        _signal?.Notify();
        return ToDto((await _runs.FindAsync(runId, ct))!);
    }

    public async Task<Result<AutomationRunDto>> CancelAsync(Guid runId, CancellationToken ct = default)
    {
        if (_runs == null) return DomainErrors.Automation.RunNotFound;
        var run = await _runs.FindAsync(runId, ct);
        if (run == null) return DomainErrors.Automation.RunNotFound;
        if (!AutomationRunStatuses.Active.Contains(run.Status)) return DomainErrors.Automation.RunFinished;
        if (!await _runs.TryCancelAsync(runId, "Stopped from the automations page.", DateTime.UtcNow, ct))
        {
            var now = await _runs.FindAsync(runId, ct);
            return now != null && AutomationRunStatuses.Active.Contains(now.Status) ? DomainErrors.Automation.RunBusy : DomainErrors.Automation.RunFinished;
        }
        return ToDto((await _runs.FindAsync(runId, ct))!);
    }

    private static AutomationRunDto ToDto(AutomationRun run)
    {
        var step = AutomationSteps.At(AutomationSteps.Parse(run.StepsJson), run.CurrentPath);
        return new AutomationRunDto(run.Id, run.RuleId, run.RuleName, run.Trigger, run.Status, run.RecordType, run.RecordId, run.RecordName,
            LinkOf(run), run.CurrentPath, AutomationRunStatuses.Active.Contains(run.Status) || run.Status == AutomationRunStatuses.Failed ? step?.Type : null,
            run.StepAttempts, run.NextAttemptAtUtc, run.WaitingFor, run.LastError, run.StartedAtUtc, run.UpdatedAtUtc, run.FinishedAtUtc);
    }

    /// <summary>The page of the record a run is about.</summary>
    private static string? LinkOf(AutomationRun run) => run.RecordType switch
    {
        AutomationRecords.Lead => run.LeadId is Guid lead ? $"/leads/{lead}" : null,
        AutomationRecords.Deal => run.DealId is Guid deal ? $"/deals/{deal}" : null,
        AutomationRecords.Task => run.TaskId is Guid task ? $"/tasks/{task}" : null,
        AutomationRecords.Contract => run.DealId is Guid d ? $"/deals/{d}" : run.LeadId is Guid l ? $"/leads/{l}" : null,
        AutomationRecords.Reservation => "/reservations",
        AutomationRecords.Registration => "/events",
        _ => run.MemberId is Guid member ? $"/members/{member}" : null,
    };
}
