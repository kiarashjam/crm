using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface ICustomFieldService
{
    Task<Result<IReadOnlyList<CustomFieldDefinitionDto>>> ListDefinitionsAsync(
        Guid userId, Guid? organizationId, string? entityType, CancellationToken ct = default);

    Task<Result<CustomFieldDefinitionDto>> CreateDefinitionAsync(
        Guid userId, Guid? organizationId, CreateCustomFieldRequest request, CancellationToken ct = default);

    Task<Result<CustomFieldDefinitionDto>> UpdateDefinitionAsync(
        Guid userId, Guid? organizationId, Guid id, UpdateCustomFieldRequest request, CancellationToken ct = default);

    /// <summary>Puts one kind of record's fields in the order given.</summary>
    Task<Result<IReadOnlyList<CustomFieldDefinitionDto>>> ReorderAsync(
        Guid userId, Guid? organizationId, ReorderCustomFieldsRequest request, CancellationToken ct = default);

    Task<Result> DeleteDefinitionAsync(Guid userId, Guid? organizationId, Guid id, CancellationToken ct = default);

    Task<Result<IReadOnlyDictionary<string, JsonElement>>> GetValuesAsync(
        Guid userId, Guid? organizationId, string? entityType, Guid recordId, CancellationToken ct = default);

    /// <summary>The values of many records at once, for a list's columns: only records the caller can see answer.</summary>
    Task<Result<IReadOnlyDictionary<Guid, IReadOnlyDictionary<string, JsonElement>>>> GetValuesBatchAsync(
        Guid userId, Guid? organizationId, CustomFieldValuesBatchRequest request, CancellationToken ct = default);

    Task<Result> SaveValuesAsync(
        Guid userId, Guid? organizationId, SaveCustomFieldValuesRequest request, CancellationToken ct = default);
}
