namespace ACI.Application.Common;

/// <summary>Query options for listing leads with pagination.</summary>
public sealed class LeadQueryOptions
{
    public string? Search { get; init; }
    public string? Status { get; init; }
    public string? Source { get; init; }
    /// <summary>null = all, true = converted only, false = active (not converted).</summary>
    public bool? IsConverted { get; init; }
    public string SortBy { get; init; } = "createdAt";
    public string SortDir { get; init; } = "desc";
}
