namespace ACI.Domain.Entities;

/// <summary>
/// A named way of looking at one list: its filters, its order and its columns, kept for
/// the person who made it or shared with everyone in the workspace.
/// </summary>
/// <remarks>
/// The filters and the order are kept as the list's own query string parameters
/// (<see cref="QueryJson"/>), so a view opens the list exactly as the address bar would,
/// and a filter added to a list later needs nothing here.
/// </remarks>
public class SavedView : Common.BaseEntity, Common.IVersioned
{
    public const int MaxNameLength = 80;

    public Guid? OrganizationId { get; set; }

    /// <summary>Who made it: only they change a view, and only they see it unless it is shared.</summary>
    public Guid OwnerUserId { get; set; }

    /// <summary>One of <see cref="SavedViewLists.All"/>.</summary>
    public string List { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Seen by everyone in the workspace, not only its owner.</summary>
    public bool Shared { get; set; }

    /// <summary>The list's filters and order, as a JSON object of query string parameters to values.</summary>
    public string QueryJson { get; set; } = "{}";

    /// <summary>The columns shown, in order, as a JSON array of column ids; null keeps the person's own.</summary>
    public string? ColumnsJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Guid Version { get; set; }
}

/// <summary>The view a person opens a list with: one per person, workspace and list.</summary>
public class SavedViewDefault : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }

    public Guid UserId { get; set; }

    /// <summary>One of <see cref="SavedViewLists.All"/>.</summary>
    public string List { get; set; } = string.Empty;

    public Guid SavedViewId { get; set; }

    public SavedView SavedView { get; set; } = null!;

    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>The lists views can be saved for. They match the client's list pages.</summary>
public static class SavedViewLists
{
    public const string Leads = "leads";
    public const string Contacts = "contacts";
    public const string Companies = "companies";
    public const string Deals = "deals";
    public const string Members = "members";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Leads, Contacts, Companies, Deals, Members,
    };
}
