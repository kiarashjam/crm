namespace ACI.Domain.Entities;

/// <summary>
/// A field a workspace adds to one kind of record (lead, contact, company, deal, member).
/// </summary>
public class CustomFieldDefinition : Common.BaseEntity
{
    /// <summary>The workspace it belongs to; null for a personal workspace.</summary>
    public Guid? OrganizationId { get; set; }

    /// <summary>Who created it. Scopes a personal workspace's fields to that user.</summary>
    public Guid OwnerUserId { get; set; }

    /// <summary>One of <see cref="RecordTypes.WithFields"/>.</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Stable key the field is known by in filters, imports and automations; never changes after creation.</summary>
    public string Key { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>One of <see cref="CustomFieldTypes.All"/>.</summary>
    public string Type { get; set; } = CustomFieldTypes.Text;

    /// <summary>The choices for a single or multi select field, as a JSON array.</summary>
    public string? OptionsJson { get; set; }

    public bool Required { get; set; }

    /// <summary>
    /// What a new record of this kind starts with, as the JSON a value is written in
    /// ("Gold", 5000, true, ["Golf","Tennis"]); null for none. Checked against the field
    /// like any value, so a default can never be something the field refuses.
    /// </summary>
    public string? DefaultValueJson { get; set; }

    /// <summary>For a <see cref="CustomFieldTypes.Money"/> field: the currency its amounts are in unless one says otherwise.</summary>
    public string? Currency { get; set; }

    /// <summary>For a <see cref="CustomFieldTypes.Link"/> field: the kind of record it points at, one of <see cref="RecordTypes.WithFields"/>.</summary>
    public string? LinkEntityType { get; set; }

    public int Order { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// The custom field values of one record, as a JSON object keyed by field key.
/// </summary>
/// <remarks>
/// How values were kept before they had typed rows of their own (<see cref="CustomFieldEntry"/>).
/// Its values were copied into those rows by the AddFlexibleData migration, and nothing
/// reads or writes it since. The rows stay, read-only, until a later release removes the
/// table: they are still deleted with their record and by an erasure, since they hold
/// what was typed about a person.
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

/// <summary>
/// One value of one custom field on one record, in the column its type is kept in, so
/// lists can filter and sort by it in the database. A multi select has one row per
/// option chosen; every other field has at most one row per record.
/// </summary>
/// <remarks>
/// <para>
/// Filtering by a field is a semi-join on this table: "the contacts with a row for field F
/// whose TextKey is 'Gold'". Each typed column has an index that starts with the workspace
/// and the field, then the value, and carries the record's id, so the database answers
/// from the index alone, for tens of thousands of records, whichever field is asked for.
/// Sorting reads the one row per record and field through the (RecordId, FieldId) index.
/// </para>
/// <para>
/// Text can be up to 2,000 characters, more than an index key holds, so its first 200
/// characters are kept again in <see cref="TextKey"/>, which is what equality, sorting and
/// select options use. An option is at most 120 characters, so for them the key is the value.
/// </para>
/// </remarks>
public class CustomFieldEntry : Common.BaseEntity
{
    /// <summary>Characters of a text value kept in <see cref="TextKey"/>.</summary>
    public const int TextKeyLength = 200;

    public Guid? OrganizationId { get; set; }

    /// <summary>Who wrote it. Scopes a personal workspace's values to that user.</summary>
    public Guid OwnerUserId { get; set; }

    /// <summary>The kind of record, as the field's <see cref="CustomFieldDefinition.EntityType"/>.</summary>
    public string EntityType { get; set; } = string.Empty;

    public Guid RecordId { get; set; }

    /// <summary>The field (<see cref="CustomFieldDefinition"/>). A field deleted takes its rows with it.</summary>
    public Guid FieldId { get; set; }

    /// <summary>Text, a web address, or the option chosen.</summary>
    public string? TextValue { get; set; }

    /// <summary>The start of <see cref="TextValue"/> (<see cref="TextKeyLength"/> characters), for equality and sorting.</summary>
    public string? TextKey { get; set; }

    /// <summary>A number, or a money field's amount (rounded to cents).</summary>
    public decimal? NumberValue { get; set; }

    /// <summary>A money field's currency.</summary>
    public string? Currency { get; set; }

    public DateOnly? DateValue { get; set; }

    public bool? BoolValue { get; set; }

    /// <summary>A link field's record.</summary>
    public Guid? LinkId { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>The kinds of custom field. They match the client's <c>CustomFieldType</c>.</summary>
public static class CustomFieldTypes
{
    public const string Text = "text";
    public const string Number = "number";
    /// <summary>An amount and its currency.</summary>
    public const string Money = "money";
    public const string Date = "date";
    /// <summary>One of a list of options.</summary>
    public const string Select = "select";
    /// <summary>Any number of a list of options.</summary>
    public const string MultiSelect = "multiselect";
    public const string Checkbox = "checkbox";
    public const string Url = "url";
    /// <summary>Another record of the workspace: a contact, a company, a deal…</summary>
    public const string Link = "link";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Text, Number, Money, Date, Select, MultiSelect, Checkbox, Url, Link,
    };

    /// <summary>The kinds with a list of options.</summary>
    public static bool HasOptions(string type) => type is Select or MultiSelect;
}
