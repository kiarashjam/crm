using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class LeadStatusRepository : ILeadStatusRepository
{
    private readonly AppDbContext _db;

    public LeadStatusRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<LeadStatus>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.LeadStatuses
            .Where(s => s.OrganizationId == organizationId)
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.Name)
            .ToListAsync(ct);

    public async Task<LeadStatus?> GetByIdAsync(Guid id, Guid organizationId, CancellationToken ct = default) =>
        await _db.LeadStatuses.FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organizationId, ct);

    public async Task<LeadStatus> AddAsync(LeadStatus entity, CancellationToken ct = default)
    {
        _db.LeadStatuses.Add(entity);
        await _db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<LeadStatus?> UpdateAsync(LeadStatus entity, CancellationToken ct = default)
    {
        var existing = await _db.LeadStatuses.FirstOrDefaultAsync(s => s.Id == entity.Id && s.OrganizationId == entity.OrganizationId, ct);
        if (existing == null) return null;
        existing.Name = entity.Name;
        existing.DisplayOrder = entity.DisplayOrder;
        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default)
    {
        var entity = await _db.LeadStatuses.FirstOrDefaultAsync(s => s.Id == id && s.OrganizationId == organizationId, ct);
        if (entity == null) return false;
        _db.LeadStatuses.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
