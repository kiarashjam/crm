using System.Text.Json;

namespace ACI.Application.DTOs;

/// <summary>
/// A custom field definition. Property names match the client's CustomFieldDef
/// (src/app/api/customFields.ts) so no client-side mapping is needed.
/// </summary>
public record CustomFieldDefinitionDto(
    Guid Id,
    string EntityType,
    string Key,
    string Label,
    string Type,
    IReadOnlyList<string>? Options,
    bool Required,
    int Order);

public record CreateCustomFieldRequest(
    string EntityType,
    string Label,
    string Type,
    IReadOnlyList<string>? Options,
    bool? Required);

/// <summary>A partial update: only the properties sent are changed.</summary>
public record UpdateCustomFieldRequest(
    string? EntityType,
    string? Label,
    string? Type,
    IReadOnlyList<string>? Options,
    bool? Required);

/// <summary>A record's values, keyed by field key. Each is a string, number, boolean or null.</summary>
public record SaveCustomFieldValuesRequest(
    string EntityType,
    Guid RecordId,
    Dictionary<string, JsonElement> Values);
