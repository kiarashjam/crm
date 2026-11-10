namespace ACI.Application.Common;

/// <summary>
/// What the contacts list asks for: which contacts, in what order. The server applies all
/// of it to the whole workspace before taking a page, so a filter or a sort reaches every
/// contact, not only the twenty on screen.
/// </summary>
public sealed class ContactQueryOptions
{
    public const string Active = "active";
    public const string Archived = "archived";
    public const string All = "all";

    /// <summary>Activity in the last <see cref="RecentDays"/> days.</summary>
    public const string Recent = "recent";
    /// <summary>No activity in the last <see cref="QuietDays"/> days, or none at all.</summary>
    public const string Quiet = "quiet";

    public const int RecentDays = 30;
    public const int QuietDays = 90;

    public string? Search { get; init; }

    /// <summary><see cref="Active"/> (the default), <see cref="Archived"/> or <see cref="All"/>.</summary>
    public string Status { get; init; } = Active;

    public Guid? CompanyId { get; init; }

    /// <summary>True: at a company. False: at none. Null: either.</summary>
    public bool? HasCompany { get; init; }

    /// <summary><see cref="Recent"/>, <see cref="Quiet"/>, or null for any.</summary>
    public string? Activity { get; init; }

    /// <summary>name, email, lastActivity or createdAt.</summary>
    public string SortBy { get; init; } = "name";

    /// <summary>asc or desc.</summary>
    public string SortDir { get; init; } = "asc";

    /// <summary>The status as one of the three known values; anything else reads as active.</summary>
    public string NormalizedStatus => Status?.Trim().ToLowerInvariant() switch
    {
        Archived => Archived,
        All => All,
        _ => Active,
    };
}
