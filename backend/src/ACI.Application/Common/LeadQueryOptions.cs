namespace ACI.Application.Common;

/// <summary>Query options for listing leads with pagination.</summary>
public sealed class LeadQueryOptions
{
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? Source { get; init; }
    /// <summary>null = all, true = converted only, false = active (not converted).</summary>
    public bool? IsConverted { get; init; }
    /// <summary>"person" or "establishment"; null or "all" for both.</summary>
    public string? Kind { get; init; }
    public string SortBy { get; init; } = "createdAt";
    public string SortDir { get; init; } = "desc";

    /// <summary>The leads' tags and custom fields the list filters and sorts by.</summary>
    public FlexFilter Flex { get; init; } = FlexFilter.None;
}
