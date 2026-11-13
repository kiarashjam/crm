using System.Globalization;
using System.Text.Json;
using ACI.Application.Common;

namespace ACI.Application.Services;

/// <summary>
/// Conditions joined by all (and) or any (or), with groups of their own inside: "status is
/// Qualified and (value is over 10'000 or source is Referral)".
/// </summary>
public sealed class ConditionGroup
{
    /// <summary><c>all</c>: every condition and group holds; <c>any</c>: at least one does.</summary>
    public string Match { get; set; } = AutomationConditions.All;

    public List<AutomationCondition> Conditions { get; set; } = new();

    /// <summary>Groups inside this one, one level deep.</summary>
    public List<ConditionGroup>? Groups { get; set; }
}

/// <summary>One test of one field of the record: "stage is Proposal", "value is at least 5000".</summary>
public sealed class AutomationCondition
{
    /// <summary>A field the trigger offers (<see cref="AutomationCatalog"/>).</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary><see cref="AutomationOps"/>.</summary>
    public string Op { get; set; } = string.Empty;

    public string? Value { get; set; }

    /// <summary>The choices for is any of / is none of.</summary>
    public List<string>? Values { get; set; }

    /// <summary>The upper end for between.</summary>
    public string? To { get; set; }
}

/// <summary>The comparisons a condition can make, by the kind of field.</summary>
public static class AutomationOps
{
    public const string Is = "is";
    public const string IsNot = "is_not";
    public const string IsAnyOf = "is_any_of";
    public const string IsNoneOf = "is_none_of";
    public const string Contains = "contains";
    public const string NotContains = "not_contains";
    public const string IsEmpty = "is_empty";
    public const string IsNotEmpty = "is_not_empty";
    public const string EqualTo = "eq";
    public const string NotEqualTo = "neq";
    public const string GreaterThan = "gt";
    public const string AtLeast = "gte";
    public const string LessThan = "lt";
    public const string AtMost = "lte";
    public const string Between = "between";
    public const string IsTrue = "is_true";
    public const string IsFalse = "is_false";

    private static readonly string[] ChoiceOps = [Is, IsNot, IsAnyOf, IsNoneOf, IsEmpty, IsNotEmpty];
    private static readonly string[] TextOps = [Is, IsNot, Contains, NotContains, IsAnyOf, IsNoneOf, IsEmpty, IsNotEmpty];
    private static readonly string[] NumberOps = [EqualTo, NotEqualTo, GreaterThan, AtLeast, LessThan, AtMost, Between, IsEmpty, IsNotEmpty];
    private static readonly string[] BoolOps = [IsTrue, IsFalse];

    /// <summary>The comparisons a field of this kind offers.</summary>
    public static IReadOnlyList<string> For(string fieldType) => fieldType switch
    {
        AutomationFieldTypes.Text => TextOps,
        AutomationFieldTypes.Number or AutomationFieldTypes.Money => NumberOps,
        AutomationFieldTypes.Bool => BoolOps,
        _ => ChoiceOps,
    };
}

/// <summary>
/// What a record is, as conditions read it: for each field, the texts it can be called by
/// (a stage's id and its name), or its number, or yes or no.
/// </summary>
public sealed record FactValue(IReadOnlyList<string> Texts, decimal? Number = null, bool? Flag = null)
{
    public static readonly FactValue None = new(Array.Empty<string>());

    public bool IsEmpty => Texts.Count == 0 && Number == null && Flag == null;

    public static FactValue Of(params string?[] texts) =>
        new(texts.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList());

    public static FactValue OfId(Guid? id, string? name = null) => Of(id?.ToString(), name);

    public static FactValue OfNumber(decimal? number) => number == null ? None : new(Array.Empty<string>(), number);

    public static FactValue OfFlag(bool flag) => new(Array.Empty<string>(), null, flag);
}

/// <summary>Reading, checking and evaluating a rule's conditions.</summary>
public static class AutomationConditions
{
    public const string All = "all";
    public const string Any = "any";

    /// <summary>Most conditions one rule's condition group, or one branch's, may hold.</summary>
    public const int MaxConditions = 30;

    private const int MaxValue = 500;

    /// <summary>The group stored for a rule; null when it has none or the JSON cannot be read.</summary>
    public static ConditionGroup? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var group = JsonSerializer.Deserialize<ConditionGroup>(json, AutomationSteps.Json);
            return IsEmpty(group) ? null : group;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? Serialize(ConditionGroup? group) =>
        IsEmpty(group) ? null : JsonSerializer.Serialize(group, AutomationSteps.Json);

    /// <summary>
    /// The group as a request sent it, without what JSON can leave null in it (a list sent as
    /// null, a condition, group or value sent as null): checking and evaluating it then never
    /// trip over one, and a body with one is refused or read, not answered with an error.
    /// </summary>
    public static ConditionGroup? Clean(ConditionGroup? group) => group == null ? null : new ConditionGroup
    {
        Match = group.Match,
        Conditions = (group.Conditions ?? []).Where(c => c != null).Select(c => new AutomationCondition
        {
            Field = c.Field ?? "",
            Op = c.Op ?? "",
            Value = c.Value,
            Values = c.Values?.Where(v => v != null).ToList(),
            To = c.To,
        }).ToList(),
        Groups = group.Groups?.Where(g => g != null).Select(g => Clean(g)!).ToList(),
    };

    /// <summary>A group with no conditions in it or in its groups, which every record meets.</summary>
    public static bool IsEmpty(ConditionGroup? group) =>
        group == null || (group.Conditions.Count == 0 && (group.Groups ?? []).All(IsEmpty));

    /// <summary>Whether the record meets the group. No group, or an empty one, is met by every record.</summary>
    public static bool Holds(ConditionGroup? group, IReadOnlyDictionary<string, FactValue> facts)
    {
        if (IsEmpty(group)) return true;
        var results = group!.Conditions.Select(c => Holds(c, facts))
            .Concat((group.Groups ?? []).Where(g => !IsEmpty(g)).Select(g => Holds(g, facts)));
        return string.Equals(group.Match, Any, StringComparison.OrdinalIgnoreCase) ? results.Any(r => r) : results.All(r => r);
    }

    public static bool Holds(AutomationCondition condition, IReadOnlyDictionary<string, FactValue> facts)
    {
        var fact = facts.TryGetValue(condition.Field, out var value) ? value : FactValue.None;
        var wanted = (condition.Value ?? "").Trim();
        var choices = (condition.Values ?? []).Select(v => v.Trim()).Where(v => v.Length > 0).ToList();
        return condition.Op switch
        {
            AutomationOps.Is => fact.Texts.Any(t => Same(t, wanted)),
            AutomationOps.IsNot => !fact.Texts.Any(t => Same(t, wanted)),
            AutomationOps.IsAnyOf => fact.Texts.Any(t => choices.Any(c => Same(t, c))),
            AutomationOps.IsNoneOf => !fact.Texts.Any(t => choices.Any(c => Same(t, c))),
            AutomationOps.Contains => wanted.Length > 0 && fact.Texts.Any(t => t.Contains(wanted, StringComparison.OrdinalIgnoreCase)),
            AutomationOps.NotContains => !fact.Texts.Any(t => wanted.Length > 0 && t.Contains(wanted, StringComparison.OrdinalIgnoreCase)),
            AutomationOps.IsEmpty => fact.IsEmpty,
            AutomationOps.IsNotEmpty => !fact.IsEmpty,
            AutomationOps.EqualTo => Compare(fact, wanted, d => d == 0),
            AutomationOps.NotEqualTo => Compare(fact, wanted, d => d != 0),
            AutomationOps.GreaterThan => Compare(fact, wanted, d => d > 0),
            AutomationOps.AtLeast => Compare(fact, wanted, d => d >= 0),
            AutomationOps.LessThan => Compare(fact, wanted, d => d < 0),
            AutomationOps.AtMost => Compare(fact, wanted, d => d <= 0),
            AutomationOps.Between => Compare(fact, wanted, d => d >= 0) && Compare(fact, (condition.To ?? "").Trim(), d => d <= 0),
            AutomationOps.IsTrue => fact.Flag == true,
            AutomationOps.IsFalse => fact.Flag == false,
            _ => false,
        };
    }

    /// <summary>
    /// What is wrong with a group for a trigger, as a person reads it; null when nothing is.
    /// </summary>
    /// <param name="nested">Whether the group is already inside another, where it may hold no more groups.</param>
    public static string? Problem(ConditionGroup? group, AutomationTrigger trigger, bool nested = false)
    {
        if (group == null) return null;
        if (!string.Equals(group.Match, All, StringComparison.OrdinalIgnoreCase) && !string.Equals(group.Match, Any, StringComparison.OrdinalIgnoreCase))
            return "Conditions are joined by all or any.";
        if (nested && (group.Groups ?? []).Count > 0) return "A group of conditions cannot hold another group.";
        if (Count(group) > MaxConditions) return $"A rule can test at most {MaxConditions} conditions in one place.";
        foreach (var condition in group.Conditions)
        {
            var field = AutomationCatalog.Field(trigger, condition.Field);
            if (field == null) return $"\"{condition.Field}\" is not something \"{trigger.Label.ToLowerInvariant()}\" can test.";
            if (!AutomationOps.For(field.Type).Contains(condition.Op)) return $"{field.Label} cannot be tested with \"{condition.Op}\".";
            if (ValueProblem(condition, field) is { } problem) return problem;
        }
        foreach (var inner in group.Groups ?? [])
            if (Problem(inner, trigger, nested: true) is { } problem) return problem;
        return null;
    }

    private static int Count(ConditionGroup group) => group.Conditions.Count + (group.Groups ?? []).Sum(Count);

    private static string? ValueProblem(AutomationCondition condition, AutomationField field)
    {
        var value = (condition.Value ?? "").Trim();
        if (value.Length > MaxValue || (condition.Values ?? []).Any(v => v.Length > MaxValue) || (condition.To ?? "").Length > MaxValue)
            return $"A value tested for {field.Label} can be at most {MaxValue} characters.";
        switch (condition.Op)
        {
            case AutomationOps.IsEmpty or AutomationOps.IsNotEmpty or AutomationOps.IsTrue or AutomationOps.IsFalse:
                return null;
            case AutomationOps.IsAnyOf or AutomationOps.IsNoneOf:
                return (condition.Values ?? []).Any(v => !string.IsNullOrWhiteSpace(v)) ? null : $"Pick at least one value for {field.Label}.";
            case AutomationOps.Between:
                if (!TryNumber(value, out var low) || !TryNumber(condition.To, out var high)) return $"Give two numbers for {field.Label}.";
                return low <= high ? null : $"The first number for {field.Label} has to be the smaller one.";
            case AutomationOps.EqualTo or AutomationOps.NotEqualTo or AutomationOps.GreaterThan or AutomationOps.AtLeast
                or AutomationOps.LessThan or AutomationOps.AtMost:
                return TryNumber(value, out _) ? null : $"Give a number for {field.Label}.";
            default:
                return value.Length > 0 ? null : $"Give a value for {field.Label}.";
        }
    }

    private static bool Same(string a, string b) => string.Equals(a.Trim(), b, StringComparison.OrdinalIgnoreCase);

    private static bool Compare(FactValue fact, string wanted, Func<int, bool> holds) =>
        fact.Number is decimal number && TryNumber(wanted, out var against) && holds(number.CompareTo(against));

    /// <summary>A number as typed, with a point for decimals; a Swiss apostrophe or a space between thousands is allowed.</summary>
    public static bool TryNumber(string? raw, out decimal number) =>
        decimal.TryParse((raw ?? "").Trim().Replace("'", "").Replace("’", "").Replace(" ", ""),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out number);

    /// <summary>The conditions in words, for the run log: "status is Qualified and value is at least 5000".</summary>
    public static string Describe(ConditionGroup? group, AutomationTrigger? trigger)
    {
        if (IsEmpty(group)) return "always";
        var joiner = string.Equals(group!.Match, Any, StringComparison.OrdinalIgnoreCase) ? " or " : " and ";
        var parts = group.Conditions.Select(c => Describe(c, trigger))
            .Concat((group.Groups ?? []).Where(g => !IsEmpty(g)).Select(g => $"({Describe(g, trigger)})"));
        return string.Join(joiner, parts);
    }

    private static string Describe(AutomationCondition c, AutomationTrigger? trigger)
    {
        var field = trigger == null ? null : AutomationCatalog.Field(trigger, c.Field);
        var name = (field?.Label ?? c.Field).ToLowerInvariant();
        // A choice by its name (a tag's, an option's), not the id the rule keeps.
        string Named(string? raw) => field?.Choices?.FirstOrDefault(x => Same(x.Value, (raw ?? "").Trim()))?.Label ?? raw ?? "";
        var value = Named(c.Value);
        return c.Op switch
        {
            AutomationOps.Is => $"{name} is {value}",
            AutomationOps.IsNot => $"{name} is not {value}",
            AutomationOps.IsAnyOf => $"{name} is any of {string.Join(", ", (c.Values ?? []).Select(Named))}",
            AutomationOps.IsNoneOf => $"{name} is none of {string.Join(", ", (c.Values ?? []).Select(Named))}",
            AutomationOps.Contains => $"{name} contains {value}",
            AutomationOps.NotContains => $"{name} does not contain {value}",
            AutomationOps.IsEmpty => $"{name} is empty",
            AutomationOps.IsNotEmpty => $"{name} is set",
            AutomationOps.EqualTo => $"{name} is {value}",
            AutomationOps.NotEqualTo => $"{name} is not {value}",
            AutomationOps.GreaterThan => $"{name} is over {value}",
            AutomationOps.AtLeast => $"{name} is at least {value}",
            AutomationOps.LessThan => $"{name} is under {value}",
            AutomationOps.AtMost => $"{name} is at most {value}",
            AutomationOps.Between => $"{name} is between {value} and {c.To}",
            AutomationOps.IsTrue => $"{name}: yes",
            AutomationOps.IsFalse => $"{name}: no",
            _ => name,
        };
    }
}
