using System.Text.Json;

namespace ACI.Application.Services;

/// <summary>
/// One step of a rule: an action (create a task, send an email…), a wait, or a branch
/// that goes one way or the other on a condition.
/// </summary>
/// <remarks>
/// A rule's steps are stored as JSON in AutomationRules.ActionsJson. Rules made before
/// waits and branches have a list of actions, {id, type, config}, which is a list of steps
/// as it stands, so nothing stored had to change.
/// </remarks>
public sealed class AutomationStep
{
    public Guid Id { get; set; }

    /// <summary><see cref="AutomationStepTypes"/>.</summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>The step's settings: a task's title, an email's subject, how long a wait lasts.</summary>
    public Dictionary<string, string> Config { get; set; } = new();

    /// <summary>A branch's condition, read on the record as it is when the branch is reached.</summary>
    public ConditionGroup? Condition { get; set; }

    /// <summary>A branch's steps for when its condition holds.</summary>
    public List<AutomationStep>? Then { get; set; }

    /// <summary>A branch's steps for when it does not.</summary>
    public List<AutomationStep>? Else { get; set; }
}

public static class AutomationStepTypes
{
    public const string CreateTask = "create_task";
    public const string SendEmail = "send_email";
    public const string Notify = "notify";
    public const string Assign = "assign";
    public const string AddToSequence = "add_to_sequence";
    public const string Wait = "wait";
    public const string Branch = "branch";

    /// <summary>The steps that do something to a record or a person.</summary>
    public static readonly IReadOnlySet<string> Actions = new HashSet<string>(StringComparer.Ordinal)
    {
        CreateTask, SendEmail, Notify, Assign, AddToSequence,
    };

    public static readonly IReadOnlySet<string> All = new HashSet<string>(Actions, StringComparer.Ordinal) { Wait, Branch };
}

/// <summary>Reading a rule's steps, and finding a run's way through them.</summary>
/// <remarks>
/// A run's place in its steps is a path: the index of a top-level step ("2"), and through
/// a branch, the branch's index, t (its "then" steps) or e (its "else" steps), and the
/// index in those ("3.t.0"). When the last step of a branch's list is done, the run goes on
/// with the step after the branch.
/// </remarks>
public static class AutomationSteps
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Most steps a rule may have, branches' steps included.</summary>
    public const int MaxSteps = 30;

    /// <summary>How deep branches may go inside each other: a branch in a branch in a branch.</summary>
    public const int MaxDepth = 3;

    public const string First = "0";

    /// <summary>A rule's or a run's steps; none when the JSON cannot be read.</summary>
    public static List<AutomationStep> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<AutomationStep>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string Serialize(IReadOnlyList<AutomationStep> steps) => JsonSerializer.Serialize(steps, Json);

    /// <summary>Every step, branches' steps included.</summary>
    public static int Count(IReadOnlyList<AutomationStep> steps) =>
        steps.Sum(s => 1 + Count(s.Then ?? []) + Count(s.Else ?? []));

    /// <summary>How deep the branches go: 0 with none, 1 with branches of actions only.</summary>
    public static int Depth(IReadOnlyList<AutomationStep> steps) =>
        steps.Count == 0 ? 0 : steps.Max(s => s.Type == AutomationStepTypes.Branch
            ? 1 + Math.Max(Depth(s.Then ?? []), Depth(s.Else ?? []))
            : 0);

    /// <summary>The step at <paramref name="path"/>, or null when there is none there.</summary>
    public static AutomationStep? At(IReadOnlyList<AutomationStep> steps, string path)
    {
        if (!TryParse(path, out var parts)) return null;
        IReadOnlyList<AutomationStep>? list = steps;
        AutomationStep? step = null;
        for (var i = 0; i < parts.Count; i += 2)
        {
            if (list == null || parts[i] is not int index || index < 0 || index >= list.Count) return null;
            step = list[index];
            if (i + 1 < parts.Count) list = parts[i + 1] is 't' ? step.Then : step.Else;
        }
        return step;
    }

    /// <summary>
    /// Where a run goes after the step at <paramref name="path"/>: the next step in its list,
    /// or after a branch's last step the step after that branch. Null when it was the last.
    /// </summary>
    public static string? Next(IReadOnlyList<AutomationStep> steps, string path)
    {
        if (!TryParse(path, out var parts)) return null;
        while (parts.Count > 0)
        {
            var index = (int)parts[^1];
            var list = ListAt(steps, parts.Take(parts.Count - 1).ToList());
            if (list != null && index + 1 < list.Count)
            {
                parts[^1] = index + 1;
                return Write(parts);
            }
            // The last of its list: out of the branch it is in, to the step after that branch.
            if (parts.Count == 1) return null;
            parts.RemoveRange(parts.Count - 2, 2);
        }
        return null;
    }

    /// <summary>
    /// Where a run goes from a branch: the first of the steps it took, or when it took a side
    /// with none, the step after the branch.
    /// </summary>
    public static string? Enter(IReadOnlyList<AutomationStep> steps, string path, bool then)
    {
        var branch = At(steps, path);
        var side = then ? branch?.Then : branch?.Else;
        return side is { Count: > 0 } ? $"{path}.{(then ? 't' : 'e')}.0" : Next(steps, path);
    }

    /// <summary>The list a path ends in: the top-level steps, or a branch's side.</summary>
    private static IReadOnlyList<AutomationStep>? ListAt(IReadOnlyList<AutomationStep> steps, IReadOnlyList<object> parent)
    {
        IReadOnlyList<AutomationStep>? list = steps;
        for (var i = 0; i < parent.Count; i += 2)
        {
            if (list == null || parent[i] is not int index || index < 0 || index >= list.Count || i + 1 >= parent.Count) return null;
            list = parent[i + 1] is 't' ? list[index].Then : list[index].Else;
        }
        return list;
    }

    private static bool TryParse(string? path, out List<object> parts)
    {
        parts = new List<object>();
        var segments = (path ?? "").Split('.');
        for (var i = 0; i < segments.Length; i++)
        {
            if (i % 2 == 0)
            {
                if (!int.TryParse(segments[i], out var index) || index < 0) return false;
                parts.Add(index);
            }
            else if (segments[i] is "t" or "e") parts.Add(segments[i][0]);
            else return false;
        }
        // A path ends on an index, never on a side.
        return parts.Count % 2 == 1;
    }

    private static string Write(IReadOnlyList<object> parts) => string.Join('.', parts.Select(p => p is char c ? c.ToString() : p.ToString()));
}
