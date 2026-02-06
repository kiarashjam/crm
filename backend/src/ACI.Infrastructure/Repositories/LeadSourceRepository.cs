using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class LeadSourceRepository : ILeadSourceRepository
{
    private readonly AppDbContext _db;

    public LeadSourceRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<LeadSource>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.LeadSources
            .Where(s => s.OrganizationId == organizationId)
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Name)
            .ToListAsync(ct);

    public async Task<LeadSource?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken ct = default) =>
        await _db.LeadSources.FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organizationId, ct);

    public async Task<LeadSource> AddAsync(LeadSource entity, CancellationToken ct = default)
    {
        _db.LeadSources.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<LeadSource?> UpdateAsync(LeadSource entity, CancellationToken ct = default)
    {
        var existing = await _db.LeadSources.FirstOrDefaultAsync(s => s.Id == entity.Id && s.OrganizationId == entity.OrganizationId, ct);
        if (existing == null) return null;
        existing.Name = entity.Name;
        existing.DisplayOrder = entity.DisplayOrder;
        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default)
    {
        var entity = await _db.LeadSources.FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organizationId, ct);
        if (entity == null) return false;
        _db.LeadSources.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
