namespace ACI.Domain.Entities;

/// <summary>
/// A label a workspace puts on its records ("VIP", "Board member", "Golf"), with a colour.
/// One list of tags serves every kind of record that carries them (<see cref="RecordTypes.WithFields"/>).
/// </summary>
public class Tag : Common.BaseEntity
{
    public const int MaxNameLength = 60;

    /// <summary>The workspace it belongs to; null for a personal workspace.</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>Who made it. Scopes a personal workspace's tags to that user.</summary>
    public Guid OwnerUserId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>One of <see cref="TagColors.All"/>.</summary>
    public string Color { get; set; } = TagColors.Slate;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>A tag on one record.</summary>
/// <remarks>
/// Keyed by record type and id rather than a foreign key, like attachments and custom field
/// values, because one table serves five kinds of record; deleting a record removes its
/// tags in the same save (RecordDependents). Filtering a list by a tag is a semi-join
/// through the unique (TagId, RecordId) index.
/// </remarks>
public class RecordTag : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }

    /// <summary>Who put it on. Scopes a personal workspace's rows to that user.</summary>
    public Guid OwnerUserId { get; set; }

    public Guid TagId { get; set; }

    public Tag Tag { get; set; } = null!;

    /// <summary>One of <see cref="RecordTypes.WithFields"/>.</summary>
    public string EntityType { get; set; } = string.Empty;

    public Guid RecordId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>The colours a tag can have. The client draws each from its own palette.</summary>
public static class TagColors
{
    public const string Slate = "slate";

    public static readonly IReadOnlyList<string> All =
    [
        Slate, "red", "orange", "amber", "lime", "green", "teal", "sky", "blue", "indigo", "violet", "pink",
    ];

    public static bool IsColor(string? value) => value != null && All.Contains(value, StringComparer.Ordinal);
}
