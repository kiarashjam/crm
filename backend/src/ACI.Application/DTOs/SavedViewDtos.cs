using System.Text.Json;

namespace ACI.Application.DTOs;

/// <summary>A saved view of a list, as the person asking sees it.</summary>
/// <param name="Query">The list's filters and order, as its query string parameters: {"status": "archived", "tag": ["…"]}.</param>
/// <param name="Columns">The columns shown, in order; null keeps the person's own.</param>
/// <param name="Mine">Whether the person asking made it, and so may change it.</param>
/// <param name="IsDefault">Whether the person asking opens the list with it.</param>
/// <param name="OwnerName">Who made it, for a shared view of someone else's.</param>
public record SavedViewDto(
    Guid Id,
    string List,
    string Name,
    bool Shared,
    bool Mine,
    bool IsDefault,
    JsonElement Query,
    JsonElement? Columns,
    string? OwnerName,
    Guid Version,
    DateTime UpdatedAtUtc);

public record CreateSavedViewRequest(string List, string Name, bool? Shared, JsonElement? Query, JsonElement? Columns);

/// <summary>A change to a view; what is left out stays. Columns sent as null go back to the person's own.</summary>
public class UpdateSavedViewRequest : Common.IMergePatchRequest
{
    public string? Name { get; set; }
    public bool? Shared { get; set; }
    public JsonElement? Query { get; set; }
    public JsonElement? Columns { get; set; }

    /// <summary>The version the change was made from; or send it as If-Match.</summary>
    public Guid? Version { get; set; }

    public ISet<string> NullsSent { get; } = new HashSet<string>(StringComparer.Ordinal);
}

/// <summary>The view a person opens a list with; none to open it as it comes.</summary>
public record SetDefaultViewRequest(string List, Guid? ViewId);
