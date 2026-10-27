namespace ACI.Domain.Entities;

/// <summary>
/// How two contacts are related: a couple, a parent and child, siblings, and so on.
/// </summary>
/// <remarks>
/// One row per pair, read as "<see cref="RelatedContact"/> is <see cref="Contact"/>'s
/// <see cref="Type"/>". Seen from the related contact the type is turned around (a parent
/// is seen back as a child); see <see cref="ContactRelationshipTypes.Inverse"/>.
/// </remarks>
public class ContactRelationship : Common.BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid ContactId { get; set; }
    public Guid RelatedContactId { get; set; }
    /// <summary>One of <see cref="ContactRelationshipTypes.All"/>.</summary>
    public string Type { get; set; } = ContactRelationshipTypes.Spouse;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public Organization? Organization { get; set; }
    public Contact Contact { get; set; } = null!;
    public Contact RelatedContact { get; set; } = null!;
}

/// <summary>The kinds of relationship two contacts can have.</summary>
public static class ContactRelationshipTypes
{
    public const string Spouse = "spouse";
    public const string Partner = "partner";
    public const string Parent = "parent";
    public const string Child = "child";
    public const string Sibling = "sibling";
    public const string Family = "family";
    public const string Friend = "friend";
    public const string Colleague = "colleague";
    public const string Other = "other";

    public static readonly IReadOnlyList<string> All =
        [Spouse, Partner, Parent, Child, Sibling, Family, Friend, Colleague, Other];

    /// <summary>A couple: a contact has at most one of these at a time (the couples package).</summary>
    public static bool IsCouple(string type) => type is Spouse or Partner;

    /// <summary>The same relationship seen from the other contact.</summary>
    public static string Inverse(string type) => type switch
    {
        Parent => Child,
        Child => Parent,
        _ => type,
    };

    /// <summary>The known type for <paramref name="value"/>, ignoring case, or null.</summary>
    public static string? Normalize(string? value)
    {
        var v = value?.Trim().ToLowerInvariant();
        return v != null && All.Contains(v) ? v : null;
    }
}
