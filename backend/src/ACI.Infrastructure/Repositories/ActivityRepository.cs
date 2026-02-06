using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class ActivityRepository : IActivityRepository
{
    private readonly AppDbContext _db;

    public ActivityRepository(AppDbContext db) => _db = db;

    private static IQueryable<Activity> FilterByUserAndOrg(IQueryable<Activity> q, Guid userId, Guid? organizationId) =>
        q.Where(a => a.UserId == userId && (organizationId == null ? a.OrganizationId == null : a.OrganizationId == organizationId));

    private static IQueryable<Activity> ApplySearch(IQueryable<Activity> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var q = search.Trim().ToLowerInvariant();
        return query.Where(a => 
            (a.Subject != null && a.Subject.ToLower().Contains(q)) || 
            (a.Body != null && a.Body.ToLower().Contains(q)) ||
            a.Type.ToLower().Contains(q));
    }

    private static IQueryable<Activity> ApplyTypeFilter(IQueryable<Activity> query, string? activityType)
    {
        if (string.IsNullOrWhiteSpace(activityType)) return query;
        return query.Where(a => a.Type.ToLower() == activityType.Trim().ToLowerInvariant());
    }

    public async Task<(IReadOnlyList<Activity> Items, int TotalCount)> GetPagedAsync(
        Guid userId,
        Guid? organizationId,
        int skip,
        int take,
        string? search = null,
        string? activityType = null,
        CancellationToken ct = default)
    {
        var query = FilterByUserAndOrg(_db.Activities, userId, organizationId);
        query = ApplySearch(query, search);
        query = ApplyTypeFilter(query, activityType);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Activity>> GetByUserIdAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await FilterByUserAndOrg(_db.Activities, userId, organizationId).OrderByDescending(a => a.CreatedAtUtc).ToListAsync(ct);

    public async Task<IReadOnlyList<Activity>> GetByContactIdAsync(Guid contactId, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await FilterByUserAndOrg(_db.Activities, userId, organizationId)
            .Where(a => a.ContactId == contactId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Activity>> GetByDealIdAsync(Guid dealId, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await FilterByUserAndOrg(_db.Activities, userId, organizationId)
            .Where(a => a.DealId == dealId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Activity>> GetByLeadIdAsync(Guid leadId, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        // For lead activities, show all activities for the lead within the organization (not just current user's)
        // This allows team members to see each other's interactions with leads
        await _db.Activities
            .Where(a => a.LeadId == leadId && (organizationId == null ? a.OrganizationId == null : a.OrganizationId == organizationId))
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<Activity?> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await FilterByUserAndOrg(_db.Activities, userId, organizationId).FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<Activity> AddAsync(Activity activity, CancellationToken ct = default)
    {
        _db.Activities.Add(activity);
        await _db.SaveChangesAsync(ct);
        return activity;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var existing = await FilterByUserAndOrg(_db.Activities, userId, organizationId).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (existing == null) return false;
        _db.Activities.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyDictionary<Guid, DateTime>> GetLastActivityByContactIdsAsync(Guid userId, Guid? organizationId, IEnumerable<Guid> contactIds, CancellationToken ct = default)
    {
        var ids = contactIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, DateTime>();
        var list = await FilterByUserAndOrg(_db.Activities, userId, organizationId)
            .Where(a => a.ContactId != null && ids.Contains(a.ContactId.Value))
            .GroupBy(a => a.ContactId!.Value)
            .Select(g => new { ContactId = g.Key, Last = g.Max(a => a.CreatedAtUtc) })
            .ToListAsync(ct);
        return list.ToDictionary(x => x.ContactId, x => x.Last);
    }

    public async Task<IReadOnlyDictionary<Guid, DateTime>> GetLastActivityByDealIdsAsync(Guid userId, Guid? organizationId, IEnumerable<Guid> dealIds, CancellationToken ct = default)
    {
        var ids = dealIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, DateTime>();
        var list = await FilterByUserAndOrg(_db.Activities, userId, organizationId)
            .Where(a => a.DealId != null && ids.Contains(a.DealId.Value))
            .GroupBy(a => a.DealId!.Value)
            .Select(g => new { DealId = g.Key, Last = g.Max(a => a.CreatedAtUtc) })
            .ToListAsync(ct);
        return list.ToDictionary(x => x.DealId, x => x.Last);
    }
}
