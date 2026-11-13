using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface ICustomFieldRepository
{
    /// <summary>The caller's workspace's definitions, optionally for one kind of record, in display order.</summary>
    Task<IReadOnlyList<CustomFieldDefinition>> ListDefinitionsAsync(
        Guid userId, Guid? organizationId, string? entityType, CancellationToken ct = default);

    Task<CustomFieldDefinition?> FindDefinitionAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    Task AddDefinitionAsync(CustomFieldDefinition definition, CancellationToken ct = default);

    /// <summary>Persist changes made to definitions loaded by <see cref="FindDefinitionAsync"/> or for tracking.</summary>
    Task SaveDefinitionAsync(CustomFieldDefinition definition, CancellationToken ct = default);

    /// <summary>The definitions of one kind of record, tracked, so their order can be changed and saved.</summary>
    Task<IReadOnlyList<CustomFieldDefinition>> TrackedDefinitionsAsync(
        Guid userId, Guid? organizationId, string entityType, CancellationToken ct = default);

    /// <summary>Removes the field and every value entered for it.</summary>
    Task DeleteDefinitionAsync(CustomFieldDefinition definition, CancellationToken ct = default);

    /// <summary>Whether any record has a value for the field.</summary>
    Task<bool> HasValuesAsync(Guid fieldId, CancellationToken ct = default);

    /// <summary>The typed values of these records, of the caller's workspace.</summary>
    Task<IReadOnlyList<CustomFieldEntry>> EntriesAsync(
        string entityType, IReadOnlyCollection<Guid> recordIds, CancellationToken ct = default);

    /// <summary>
    /// Makes the record's rows for each field in <paramref name="wanted"/> the ones given (none
    /// empties the field), changing rows in place where it can, in one save. False when
    /// another save of the same record and field got there first.
    /// </summary>
    Task<bool> ApplyEntriesAsync(
        string entityType, Guid recordId, Guid? organizationId, Guid ownerUserId,
        IReadOnlyDictionary<CustomFieldDefinition, List<CustomFieldEntry>> wanted, CancellationToken ct = default);
}
