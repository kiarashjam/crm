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

    Task<Result> DeleteDefinitionAsync(Guid userId, Guid? organizationId, Guid id, CancellationToken ct = default);

    Task<Result<IReadOnlyDictionary<string, JsonElement>>> GetValuesAsync(
        Guid userId, Guid? organizationId, string? entityType, Guid recordId, CancellationToken ct = default);

    Task<Result> SaveValuesAsync(
        Guid userId, Guid? organizationId, SaveCustomFieldValuesRequest request, CancellationToken ct = default);
}
