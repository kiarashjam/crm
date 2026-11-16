using ACI.Application.Common;

namespace ACI.Application.Interfaces;

/// <summary>What lists and exports read of their records' tags and custom fields.</summary>
public interface IRecordFlexQuery
{
    /// <summary>
    /// The ids of the workspace's records of this kind that pass the filter's tag and field
    /// conditions; null when it has none, so every record passes.
    /// </summary>
    Task<IReadOnlySet<Guid>?> MatchingAsync(string entityType, FlexFilter filter, CancellationToken ct = default);

    /// <summary>
    /// For an export: one column per custom field of the kind, in their order, then one of the
    /// tags, and each record's cells in them.
    /// </summary>
    Task<FlexColumns> ExportColumnsAsync(Guid userId, Guid? organizationId, string entityType, IReadOnlyCollection<Guid> recordIds, CancellationToken ct = default);
}

/// <summary>Extra columns of an export, and each record's cells in them.</summary>
public sealed record FlexColumns(IReadOnlyList<string> Headers, IReadOnlyDictionary<Guid, IReadOnlyList<string>> Cells)
{
    public static readonly FlexColumns None = new(Array.Empty<string>(), new Dictionary<Guid, IReadOnlyList<string>>());

    /// <summary>A record's cells, empty ones for a record with nothing in them.</summary>
    public IReadOnlyList<string> CellsOf(Guid recordId) =>
        Cells.TryGetValue(recordId, out var cells) ? cells : Enumerable.Repeat(string.Empty, Headers.Count).ToList();
}
