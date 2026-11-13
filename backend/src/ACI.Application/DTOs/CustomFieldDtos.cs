using System.Text.Json;

namespace ACI.Application.DTOs;

/// <summary>
/// A custom field definition. Property names match the client's CustomFieldDef
/// (src/app/api/customFields.ts) so no client-side mapping is needed.
/// </summary>
/// <param name="DefaultValue">What a new record starts with, written as a value of the field is (see CustomFieldValues); null for none.</param>
/// <param name="Currency">A money field's currency.</param>
/// <param name="LinkEntityType">The kind of record a link field points at.</param>
public record CustomFieldDefinitionDto(
    Guid Id,
    string EntityType,
    string Key,
    string Label,
    string Type,
    IReadOnlyList<string>? Options,
    bool Required,
    int Order,
    JsonElement? DefaultValue = null,
    string? Currency = null,
    string? LinkEntityType = null);

public record CreateCustomFieldRequest(
    string EntityType,
    string Label,
    string Type,
    IReadOnlyList<string>? Options,
    bool? Required,
    JsonElement? DefaultValue = null,
    string? Currency = null,
    string? LinkEntityType = null);

/// <summary>A partial update: only the properties sent are changed. A default sent as null is taken off.</summary>
public class UpdateCustomFieldRequest : Common.IMergePatchRequest
{
    public string? EntityType { get; set; }
    public string? Label { get; set; }
    public string? Type { get; set; }
    public IReadOnlyList<string>? Options { get; set; }
    public bool? Required { get; set; }
    public JsonElement? DefaultValue { get; set; }
    public string? Currency { get; set; }
    public string? LinkEntityType { get; set; }

    public ISet<string> NullsSent { get; } = new HashSet<string>(StringComparer.Ordinal);
}

/// <summary>The fields of one kind of record, in the order they are to be shown.</summary>
public record ReorderCustomFieldsRequest(string EntityType, IReadOnlyList<Guid> Ids);

/// <summary>A record's values, keyed by field key; see CustomFieldValues for how each kind is written. Null empties a field.</summary>
public record SaveCustomFieldValuesRequest(
    string EntityType,
    Guid RecordId,
    Dictionary<string, JsonElement> Values);

/// <summary>The records of one kind a list shows, whose values it wants for its columns.</summary>
public record CustomFieldValuesBatchRequest(string EntityType, IReadOnlyList<Guid> RecordIds);
