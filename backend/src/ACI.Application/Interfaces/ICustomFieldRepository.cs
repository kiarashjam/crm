using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface ICustomFieldRepository
{
    /// <summary>The caller's workspace's definitions, optionally for one kind of record, in display order.</summary>
    Task<IReadOnlyList<CustomFieldDefinition>> ListDefinitionsAsync(
        Guid userId, Guid? organizationId, string? entityType, CancellationToken ct = default);

    Task<CustomFieldDefinition?> FindDefinitionAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    Task AddDefinitionAsync(CustomFieldDefinition definition, CancellationToken ct = default);

    /// <summary>Persist changes made to a definition loaded by <see cref="FindDefinitionAsync"/>.</summary>
    Task SaveDefinitionAsync(CustomFieldDefinition definition, CancellationToken ct = default);

    Task DeleteDefinitionAsync(CustomFieldDefinition definition, CancellationToken ct = default);

    Task<CustomFieldValueSet?> FindValuesAsync(
        Guid userId, Guid? organizationId, string entityType, Guid recordId, CancellationToken ct = default);

    /// <summary>Insert the set, or persist changes to one loaded by <see cref="FindValuesAsync"/>.</summary>
    Task SaveValuesAsync(CustomFieldValueSet values, bool isNew, CancellationToken ct = default);
}
