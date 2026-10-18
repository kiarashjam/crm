namespace ACI.Domain.Entities;

/// <summary>
/// A field a workspace adds to one kind of record (lead, contact, company, deal).
/// </summary>
public class CustomFieldDefinition : Common.BaseEntity
{
    /// <summary>The workspace it belongs to; null for a personal workspace.</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>Who created it. Scopes a personal workspace's fields to that user.</summary>
    public Guid OwnerUserId { get; set; }

    /// <summary>One of <see cref="RecordTypes.All"/>.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Stable key the values are stored under; never changes after creation.</summary>
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>One of <see cref="CustomFieldTypes.All"/>.</summary>
    public string Type { get; set; } = CustomFieldTypes.Text;

    /// <summary>The choices for a <see cref="CustomFieldTypes.Select"/> field, as a JSON array.</summary>
    public string? OptionsJson { get; set; }

    public bool Required { get; set; }

    public int Order { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// The custom field values of one record, as a JSON object keyed by field key.
/// </summary>
/// <remarks>
/// One row per record, because the client reads and saves a record's values as a
/// single object. The service validates every value against its definition before
/// it is written, so the JSON only ever holds keys and types the workspace defined.
/// </remarks>
public class CustomFieldValueSet : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }

    public Guid OwnerUserId { get; set; }

    public string EntityType { get; set; } = string.Empty;

    public Guid RecordId { get; set; }

    public string ValuesJson { get; set; } = "{}";

    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>The kinds of custom field. They match the client's <c>CustomFieldType</c>.</summary>
public static class CustomFieldTypes
{
    public const string Text = "text";
    public const string Number = "number";
    public const string Date = "date";
    public const string Select = "select";
    public const string Checkbox = "checkbox";
    public const string Url = "url";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Text, Number, Date, Select, Checkbox, Url,
    };
}
