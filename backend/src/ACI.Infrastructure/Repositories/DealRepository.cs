using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class DealRepository : IDealRepository
{
    private readonly AppDbContext _db;

    public DealRepository(AppDbContext db) => _db = db;

    private static IQueryable<Deal> FilterByUserAndOrg(IQueryable<Deal> q, Guid userId, Guid? organizationId) =>
        q.Where(d => d.UserId == userId && (organizationId == null ? d.OrganizationId == null : d.OrganizationId == organizationId));

    private static IQueryable<Deal> ApplySearch(IQueryable<Deal> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var q = search.Trim().ToLowerInvariant();
        return query.Where(d => d.Name.ToLower().Contains(q) || d.Value.ToLower().Contains(q));
    }

    public async Task<(IReadOnlyList<Deal> Items, int TotalCount)> GetPagedAsync(
        Guid userId, 
        Guid? organizationId, 
        int skip, 
        int take, 
        string? search = null,
        CancellationToken ct = default)
    {
        var query = FilterByUserAndOrg(_db.Deals, userId, organizationId);
        query = ApplySearch(query, search);
        
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(d => d.Name)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);
        
        return (items, totalCount);
    }

    public async Task<int> CountAsync(Guid userId, Guid? organizationId, string? search = null, CancellationToken ct = default)
    {
        var query = FilterByUserAndOrg(_db.Deals, userId, organizationId);
        query = ApplySearch(query, search);
        return await query.CountAsync(ct);
    }

    public async Task<IReadOnlyList<Deal>> GetByUserIdAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await FilterByUserAndOrg(_db.Deals, userId, organizationId).OrderBy(d => d.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Deal>> SearchAsync(Guid userId, Guid? organizationId, string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetByUserIdAsync(userId, organizationId, ct);
        var q = query.Trim().ToLowerInvariant();
        return await FilterByUserAndOrg(_db.Deals, userId, organizationId)
            .Where(d => d.Name.ToLower().Contains(q) || d.Value.ToLower().Contains(q))
            .OrderBy(d => d.Name)
            .ToListAsync(ct);
    }

    public async Task<Deal?> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await FilterByUserAndOrg(_db.Deals, userId, organizationId).FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<Deal> AddAsync(Deal deal, CancellationToken ct = default)
    {
        _db.Deals.Add(deal);
        await _db.SaveChangesAsync(ct);
        return deal;
    }

    public async Task<Deal?> UpdateAsync(Deal deal, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var existing = await FilterByUserAndOrg(_db.Deals, userId, organizationId).FirstOrDefaultAsync(d => d.Id == deal.Id, ct);
        if (existing == null) return null;
        existing.Name = deal.Name;
        existing.Value = deal.Value;
        existing.Currency = deal.Currency;
        existing.Stage = deal.Stage;
        existing.PipelineId = deal.PipelineId;
        existing.DealStageId = deal.DealStageId;
        existing.CompanyId = deal.CompanyId;
        existing.ContactId = deal.ContactId;
        existing.AssigneeId = deal.AssigneeId;
        existing.ExpectedCloseDateUtc = deal.ExpectedCloseDateUtc;
        existing.IsWon = deal.IsWon;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        existing.UpdatedByUserId = userId;
        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var existing = await FilterByUserAndOrg(_db.Deals, userId, organizationId).FirstOrDefaultAsync(d => d.Id == id, ct);
        if (existing == null) return false;
        var linkedTasks = await _db.TaskItems.Where(t => t.DealId == id).ToListAsync(ct);
        foreach (var t in linkedTasks) t.DealId = null;
        var linkedActivities = await _db.Activities.Where(a => a.DealId == id).ToListAsync(ct);
        foreach (var a in linkedActivities) a.DealId = null;
        _db.Deals.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
