namespace ACI.Application.Common;

/// <summary>
/// What the companies list asks for: which companies, in what order. Applied to the whole
/// workspace before a page is taken, as for contacts (see <see cref="ContactQueryOptions"/>).
/// </summary>
public sealed class CompanyQueryOptions
{
    public string? Search { get; init; }

    /// <summary>Only companies in this industry, ignoring case.</summary>
    public string? Industry { get; init; }

    /// <summary>False: only companies with no industry. True: only those with one. Null: either.</summary>
    public bool? HasIndustry { get; init; }

    /// <summary>Only companies of this size, as stored ("11-50").</summary>
    public string? Size { get; init; }

    /// <summary>name, createdAt, contacts or deals.</summary>
    public string SortBy { get; init; } = "name";

    /// <summary>asc or desc.</summary>
    public string SortDir { get; init; } = "asc";
}
