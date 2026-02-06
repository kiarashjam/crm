using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class LeadRepository : ILeadRepository
{
    private readonly AppDbContext _db;

    public LeadRepository(AppDbContext db) => _db = db;

    private static IQueryable<Lead> FilterByUserAndOrg(IQueryable<Lead> q, Guid userId, Guid? organizationId) =>
        q.Where(l => l.UserId == userId && (organizationId == null ? l.OrganizationId == null : l.OrganizationId == organizationId));

    private static IQueryable<Lead> ApplySearch(IQueryable<Lead> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var q = search.Trim().ToLowerInvariant();
        return query.Where(l => l.Name.ToLower().Contains(q) || l.Email.ToLower().Contains(q));
    }

    public async Task<(IReadOnlyList<Lead> Items, int TotalCount)> GetPagedAsync(
        Guid userId, 
        Guid? organizationId, 
        int skip, 
        int take, 
        string? search = null,
        CancellationToken ct = default)
    {
        var query = FilterByUserAndOrg(_db.Leads, userId, organizationId);
        query = ApplySearch(query, search);
        
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(l => l.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
        
        return (items, totalCount);
    }

    public async Task<int> CountAsync(Guid userId, Guid? organizationId, string? search = null, CancellationToken ct = default)
    {
        var query = FilterByUserAndOrg(_db.Leads, userId, organizationId);
        query = ApplySearch(query, search);
        return await query.CountAsync(ct);
    }

    public async Task<IReadOnlyList<Lead>> GetByUserIdAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await FilterByUserAndOrg(_db.Leads, userId, organizationId).OrderBy(l => l.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Lead>> SearchAsync(Guid userId, Guid? organizationId, string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetByUserIdAsync(userId, organizationId, ct);
        var q = query.Trim().ToLowerInvariant();
        return await FilterByUserAndOrg(_db.Leads, userId, organizationId)
            .Where(l => l.Name.ToLower().Contains(q) || l.Email.ToLower().Contains(q))
            .OrderBy(l => l.Name)
            .ToListAsync(ct);
    }

    public async Task<Lead?> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await FilterByUserAndOrg(_db.Leads, userId, organizationId).FirstOrDefaultAsync(l => l.Id == id, ct);

    public async Task<Lead> AddAsync(Lead lead, CancellationToken ct = default)
    {
        _db.Leads.Add(lead);
        await _db.SaveChangesAsync(ct);
        return lead;
    }

    public async Task<Lead?> UpdateAsync(Lead lead, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var existing = await FilterByUserAndOrg(_db.Leads, userId, organizationId).FirstOrDefaultAsync(l => l.Id == lead.Id, ct);
        if (existing == null) return null;
        existing.Name = lead.Name;
        existing.Email = lead.Email;
        existing.Phone = lead.Phone;
        existing.CompanyId = lead.CompanyId;
        existing.Source = lead.Source;
        existing.Status = lead.Status;
        existing.LeadSourceId = lead.LeadSourceId;
        existing.LeadStatusId = lead.LeadStatusId;
        existing.LeadScore = lead.LeadScore;
        existing.LastContactedAt = lead.LastContactedAt;
        existing.Description = lead.Description;
        existing.LifecycleStage = lead.LifecycleStage;
        existing.IsConverted = lead.IsConverted;
        existing.ConvertedAtUtc = lead.ConvertedAtUtc;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        existing.UpdatedByUserId = userId;
        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var existing = await FilterByUserAndOrg(_db.Leads, userId, organizationId).FirstOrDefaultAsync(l => l.Id == id, ct);
        if (existing == null) return false;
        var linkedTasks = await _db.TaskItems.Where(t => t.LeadId == id).ToListAsync(ct);
        foreach (var t in linkedTasks) t.LeadId = null;
        _db.Leads.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
