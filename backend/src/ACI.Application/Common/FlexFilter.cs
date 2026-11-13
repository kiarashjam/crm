namespace ACI.Application.Common;

/// <summary>
/// What a list asks of its records' tags and custom fields: which tags they carry or not,
/// what their fields hold, and a field to sort by. Read from the list's query string, so a
/// filtered list survives a reload, can be shared, and is what a saved view keeps.
/// </summary>
/// <remarks>
/// <para>The query string parameters:</para>
/// <list type="bullet">
/// <item><c>tag=&lt;id&gt;</c>, repeated: records with any of these tags, or with all of them when <c>tagMatch=all</c>;</item>
/// <item><c>notTag=&lt;id&gt;</c>, repeated: records with none of these;</item>
/// <item><c>field=&lt;key&gt;:&lt;op&gt;[:&lt;value&gt;]</c>, repeated, all of which must hold: <c>tier:is:Gold</c>,
/// <c>budget:gte:1000</c>, <c>joined:between:2026-01-01..2026-06-30</c>, <c>interests:any:Golf|Tennis</c>,
/// <c>vip:true</c>, <c>notes:empty</c>;</item>
/// <item><c>sortBy=cf:&lt;key&gt;</c> with the list's own <c>sortDir</c>: sort by a field, records without a value last.</item>
/// </list>
/// A condition on a field that no longer exists, or that cannot be read, is left out rather
/// than refused, so a saved view still opens after a field it named was deleted.
/// </remarks>
public sealed class FlexFilter
{
    public const string SortPrefix = "cf:";

    public static readonly FlexFilter None = new();

    public IReadOnlyList<Guid> Tags { get; init; } = Array.Empty<Guid>();

    /// <summary>Whether a record needs every tag of <see cref="Tags"/> rather than any one.</summary>
    public bool AllTags { get; init; }

    public IReadOnlyList<Guid> NotTags { get; init; } = Array.Empty<Guid>();

    public IReadOnlyList<FieldCondition> Fields { get; init; } = Array.Empty<FieldCondition>();

    /// <summary>The key of the custom field to sort by; null to sort as the list does.</summary>
    public string? SortField { get; init; }

    public bool SortDescending { get; init; }

    public bool Filters => Tags.Count > 0 || NotTags.Count > 0 || Fields.Count > 0;

    /// <summary>Reads the list's parameters. Anything that cannot be read is left out.</summary>
    public static FlexFilter Parse(
        IEnumerable<Guid>? tags, string? tagMatch, IEnumerable<Guid>? notTags, IEnumerable<string>? fields, string? sortBy = null, string? sortDir = null)
    {
        var sort = sortBy?.Trim();
        return new FlexFilter
        {
            Tags = (tags ?? Array.Empty<Guid>()).Where(t => t != Guid.Empty).Distinct().Take(20).ToList(),
            AllTags = string.Equals(tagMatch?.Trim(), "all", StringComparison.OrdinalIgnoreCase),
            NotTags = (notTags ?? Array.Empty<Guid>()).Where(t => t != Guid.Empty).Distinct().Take(20).ToList(),
            Fields = (fields ?? Array.Empty<string>()).Select(FieldCondition.Parse).OfType<FieldCondition>().Take(20).ToList(),
            SortField = sort != null && sort.StartsWith(SortPrefix, StringComparison.OrdinalIgnoreCase) && sort.Length > SortPrefix.Length
                ? sort[SortPrefix.Length..]
                : null,
            SortDescending = string.Equals(sortDir?.Trim(), "desc", StringComparison.OrdinalIgnoreCase),
        };
    }
}

/// <summary>One test of one custom field: its key, the comparison, and the value(s) compared with.</summary>
public sealed record FieldCondition(string Key, string Op, string? Value, string? To = null)
{
    /// <summary>The values of "any of" and "none of", written with | between them.</summary>
    public IReadOnlyList<string> Values =>
        (Value ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>"key:op:value"; a between's value is "low..high". Null when it cannot be read.</summary>
    public static FieldCondition? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 500) return null;
        var parts = raw.Split(':', 3);
        if (parts.Length < 2) return null;
        var key = parts[0].Trim();
        var op = parts[1].Trim().ToLowerInvariant();
        if (key.Length == 0 || op.Length == 0) return null;
        var value = parts.Length == 3 ? parts[2] : null;
        if (op == "between" && value != null && value.Split("..", 2) is [var low, var high])
            return new FieldCondition(key, op, low.Trim(), high.Trim());
        return new FieldCondition(key, op, value?.Trim());
    }
}
