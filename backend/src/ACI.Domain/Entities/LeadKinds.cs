namespace ACI.Domain.Entities;

/// <summary>
/// What a lead can be, and what a workspace's leads are. A club (Pavillon 46) sells to
/// people; a business that sells to restaurants (Bonap) sells to establishments.
/// </summary>
public static class LeadKinds
{
    /// <summary>A person: a name and an email to reach them at.</summary>
    public const string Person = "person";

    /// <summary>A business, such as a restaurant: its name and address, and a contact person.</summary>
    public const string Establishment = "establishment";

    /// <summary>A workspace whose leads are either (<see cref="Organization.LeadKinds"/>).</summary>
    public const string Both = "both";

    /// <summary>What a workspace's leads may be: people only, establishments only, or both.</summary>
    public static readonly IReadOnlyList<string> WorkspaceModes = new[] { Person, Establishment, Both };

    public static readonly IReadOnlyList<string> EstablishmentTypes = new[]
    {
        "restaurant", "cafe", "bar", "hotel", "bakery", "catering", "food_truck", "shop", "other",
    };

    /// <summary>How each type reads, for the company the establishment becomes.</summary>
    public static string TypeLabel(string? type) => type switch
    {
        "restaurant" => "Restaurant",
        "cafe" => "Café",
        "bar" => "Bar",
        "hotel" => "Hotel",
        "bakery" => "Bakery",
        "catering" => "Catering",
        "food_truck" => "Food truck",
        "shop" => "Shop",
        "other" => "Other",
        _ => "",
    };

    /// <summary>Whether a workspace with this mode takes leads of this kind. No mode (a personal workspace): both.</summary>
    public static bool Allows(string? workspaceMode, string kind) => workspaceMode switch
    {
        Person => kind == Person,
        Establishment => kind == Establishment,
        _ => true,
    };

    /// <summary>The kind a new lead is when nobody said: the workspace's own, or a person.</summary>
    public static string DefaultFor(string? workspaceMode) => workspaceMode == Establishment ? Establishment : Person;
}
