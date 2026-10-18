using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class CustomFieldRepository : ICustomFieldRepository
{
    private readonly AppDbContext _db;

    public CustomFieldRepository(AppDbContext db)
    {
        _db = db;
    }

    private static IQueryable<T> InWorkspace<T>(IQueryable<T> q, Guid userId, Guid? organizationId)
        where T : class
    {
        // Both entities carry the same two scoping columns.
        return organizationId is { } orgId
            ? q.Where(e => EF.Property<Guid?>(e, "OrganizationId") == orgId)
            : q.Where(e => EF.Property<Guid?>(e, "OrganizationId") == null && EF.Property<Guid>(e, "OwnerUserId") == userId);
    }

    public async Task<IReadOnlyList<CustomFieldDefinition>> ListDefinitionsAsync(
        Guid userId, Guid? organizationId, string? entityType, CancellationToken ct = default)
    {
        var q = InWorkspace(_db.CustomFieldDefinitions.AsNoTracking(), userId, organizationId);
        if (entityType != null) q = q.Where(d => d.EntityType == entityType);
        return await q.OrderBy(d => d.EntityType).ThenBy(d => d.Order).ThenBy(d => d.CreatedAtUtc).ToListAsync(ct);
    }

    public Task<CustomFieldDefinition?> FindDefinitionAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        InWorkspace(_db.CustomFieldDefinitions, userId, organizationId).FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task AddDefinitionAsync(CustomFieldDefinition definition, CancellationToken ct = default)
    {
        _db.CustomFieldDefinitions.Add(definition);
        await _db.SaveChangesAsync(ct);
    }

    public Task SaveDefinitionAsync(CustomFieldDefinition definition, CancellationToken ct = default) =>
        _db.SaveChangesAsync(ct);

    public async Task DeleteDefinitionAsync(CustomFieldDefinition definition, CancellationToken ct = default)
    {
        _db.CustomFieldDefinitions.Remove(definition);
        await _db.SaveChangesAsync(ct);
    }

    public Task<CustomFieldValueSet?> FindValuesAsync(
        Guid userId, Guid? organizationId, string entityType, Guid recordId, CancellationToken ct = default) =>
        InWorkspace(_db.CustomFieldValues, userId, organizationId)
            .FirstOrDefaultAsync(v => v.EntityType == entityType && v.RecordId == recordId, ct);

    public async Task SaveValuesAsync(CustomFieldValueSet values, bool isNew, CancellationToken ct = default)
    {
        if (isNew) _db.CustomFieldValues.Add(values);
        await _db.SaveChangesAsync(ct);
    }
}
