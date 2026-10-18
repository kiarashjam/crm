namespace ACI.Domain.Entities;

/// <summary>
/// A file attached to a lead, contact, company or deal.
/// </summary>
/// <remarks>
/// The bytes live in this row, in the same database as the record, so they are
/// backed up and deleted with it. Listing never reads <see cref="Content"/>: the
/// repository projects it out, and downloads stream it on their own.
/// </remarks>
public class Attachment : Common.BaseEntity
{
    /// <summary>The workspace of the record it belongs to; null for a personal workspace.</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>Who uploaded it. Scopes a personal workspace's attachments to that user.</summary>
    public Guid OwnerUserId { get; set; }

    /// <summary>One of <see cref="RecordTypes.All"/>.</summary>
    public string EntityType { get; set; } = string.Empty;

    public Guid RecordId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";

    public long Size { get; set; }

    public byte[] Content { get; set; } = Array.Empty<byte>();

    public string UploadedByName { get; set; } = string.Empty;

    public DateTime UploadedAtUtc { get; set; }
}

/// <summary>
/// The records that can carry attachments and custom fields. They match the
/// client's <c>AttachmentEntity</c> and <c>CustomFieldEntity</c> types.
/// </summary>
public static class RecordTypes
{
    public const string Lead = "lead";
    public const string Contact = "contact";
    public const string Company = "company";
    public const string Deal = "deal";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Lead, Contact, Company, Deal,
    };
}
