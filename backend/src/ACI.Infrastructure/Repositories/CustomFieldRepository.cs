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
        // The definitions and the values carry the same two scoping columns.
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

    public async Task<IReadOnlyList<CustomFieldDefinition>> TrackedDefinitionsAsync(
        Guid userId, Guid? organizationId, string entityType, CancellationToken ct = default) =>
        await InWorkspace(_db.CustomFieldDefinitions, userId, organizationId)
            .Where(d => d.EntityType == entityType)
            .OrderBy(d => d.Order).ThenBy(d => d.CreatedAtUtc)
            .ToListAsync(ct);

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
        // The database deletes them with the field too; removing them here keeps the in-memory
        // provider the tests use, which cascades only what it tracks, telling the same story.
        var values = await _db.CustomFieldEntries.IgnoreQueryFilters().Where(e => e.FieldId == definition.Id).ToListAsync(ct);
        _db.CustomFieldEntries.RemoveRange(values);
        _db.CustomFieldDefinitions.Remove(definition);
        await _db.SaveChangesAsync(ct);
    }

    public Task<bool> HasValuesAsync(Guid fieldId, CancellationToken ct = default) =>
        _db.CustomFieldEntries.AnyAsync(e => e.FieldId == fieldId, ct);

    public async Task<IReadOnlyList<CustomFieldEntry>> EntriesAsync(
        string entityType, IReadOnlyCollection<Guid> recordIds, CancellationToken ct = default)
    {
        if (recordIds.Count == 0) return Array.Empty<CustomFieldEntry>();
        var ids = recordIds.Distinct().ToList();
        return await _db.CustomFieldEntries.AsNoTracking()
            .Where(e => e.EntityType == entityType && ids.Contains(e.RecordId))
            .ToListAsync(ct);
    }

    public async Task<bool> ApplyEntriesAsync(
        string entityType, Guid recordId, Guid? organizationId, Guid ownerUserId,
        IReadOnlyDictionary<CustomFieldDefinition, List<CustomFieldEntry>> wanted, CancellationToken ct = default)
    {
        var fieldIds = wanted.Keys.Select(f => f.Id).ToList();
        var existing = await _db.CustomFieldEntries
            .Where(e => e.RecordId == recordId && fieldIds.Contains(e.FieldId))
            .ToListAsync(ct);
        var now = DateTime.UtcNow;

        foreach (var (field, rows) in wanted)
        {
            var had = existing.Where(e => e.FieldId == field.Id).ToList();
            foreach (var row in rows)
            {
                row.RecordId = recordId;
                row.EntityType = entityType;
                row.OrganizationId = organizationId;
                row.OwnerUserId = ownerUserId;
                row.UpdatedAtUtc = now;
            }

            if (field.Type == CustomFieldTypes.MultiSelect)
            {
                // One row per option: the ones no longer chosen go, new ones are added, and the
                // ones kept stay as they are, so the unique (record, field, option) index never
                // sees the same option twice in one save.
                var wantedKeys = rows.Select(r => r.TextKey!).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var hadKeys = had.Where(h => h.TextKey != null).Select(h => h.TextKey!).ToHashSet(StringComparer.OrdinalIgnoreCase);
                _db.CustomFieldEntries.RemoveRange(had.Where(h => h.TextKey == null || !wantedKeys.Contains(h.TextKey)));
                _db.CustomFieldEntries.AddRange(rows.Where(r => !hadKeys.Contains(r.TextKey!)));
                continue;
            }

            // Every other kind holds one row: changed in place, added, or removed when emptied.
            var keep = had.FirstOrDefault();
            _db.CustomFieldEntries.RemoveRange(had.Skip(keep == null ? 0 : 1));
            var next = rows.FirstOrDefault();
            if (next == null)
            {
                if (keep != null) _db.CustomFieldEntries.Remove(keep);
                continue;
            }
            if (keep == null)
            {
                _db.CustomFieldEntries.Add(next);
                continue;
            }
            keep.TextValue = next.TextValue;
            keep.TextKey = next.TextKey;
            keep.NumberValue = next.NumberValue;
            keep.Currency = next.Currency;
            keep.DateValue = next.DateValue;
            keep.BoolValue = next.BoolValue;
            keep.LinkId = next.LinkId;
            keep.UpdatedAtUtc = now;
        }

        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            // Another save of the same record wrote its row first (the unique index) or removed
            // one this save changed: nothing of this one was written.
            foreach (var entry in _db.ChangeTracker.Entries<CustomFieldEntry>().ToList())
                entry.State = EntityState.Detached;
            return false;
        }
    }
}
