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

    private static IQueryable<Deal> IncludeRelated(IQueryable<Deal> q) =>
        q.Include(d => d.Assignee)
         .Include(d => d.Company)
         .Include(d => d.Contact)
         .Include(d => d.Pipeline)
         .Include(d => d.DealStage);

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
        Guid? companyId = null,
        Guid? contactId = null,
        CancellationToken ct = default)
    {
        var query = FilterByUserAndOrg(_db.Deals, userId, organizationId);
        if (companyId.HasValue) query = query.Where(d => d.CompanyId == companyId.Value);
        if (contactId.HasValue) query = query.Where(d => d.ContactId == contactId.Value);
        query = ApplySearch(query, search);
        
        var totalCount = await query.CountAsync(ct);
        var items = await IncludeRelated(query)
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
        await IncludeRelated(FilterByUserAndOrg(_db.Deals, userId, organizationId)).OrderBy(d => d.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Deal>> SearchAsync(Guid userId, Guid? organizationId, string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return await GetByUserIdAsync(userId, organizationId, ct);
        var q = query.Trim().ToLowerInvariant();
        return await IncludeRelated(FilterByUserAndOrg(_db.Deals, userId, organizationId)
            .Where(d => d.Name.ToLower().Contains(q) || d.Value.ToLower().Contains(q)))
            .OrderBy(d => d.Name)
            .ToListAsync(ct);
    }

    public async Task<Deal?> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await IncludeRelated(FilterByUserAndOrg(_db.Deals, userId, organizationId)).FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<Deal> AddAsync(Deal deal, CancellationToken ct = default)
    {
        _db.Deals.Add(deal);
        await _db.SaveChangesAsync(ct);
        return deal;
    }

    public async Task<Deal?> UpdateAsync(Deal deal, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var existing = await IncludeRelated(FilterByUserAndOrg(_db.Deals, userId, organizationId)).FirstOrDefaultAsync(d => d.Id == deal.Id, ct);
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
        existing.Description = deal.Description;
        existing.Probability = deal.Probability;
        existing.ClosedReason = deal.ClosedReason;
        existing.ClosedAtUtc = deal.ClosedAtUtc;
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

    /// <summary>HP-11: Record a deal stage change.</summary>
    public async Task AddStageChangeAsync(DealStageChange change, CancellationToken ct = default)
    {
        _db.DealStageChanges.Add(change);
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>HP-11: Get stage change history for a deal.</summary>
    public async Task<IReadOnlyList<DealStageChange>> GetStageChangesAsync(
        Guid dealId, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        // Validate deal belongs to user
        var dealExists = await FilterByUserAndOrg(_db.Deals, userId, organizationId)
            .AnyAsync(d => d.Id == dealId, ct);
        if (!dealExists) return Array.Empty<DealStageChange>();

        return await _db.DealStageChanges
            .Where(sc => sc.DealId == dealId)
            .Include(sc => sc.ChangedByUser)
            .OrderByDescending(sc => sc.ChangedAtUtc)
            .ToListAsync(ct);
    }
}
