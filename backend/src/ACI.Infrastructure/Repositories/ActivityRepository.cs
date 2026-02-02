using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class ActivityRepository : IActivityRepository
{
    private readonly AppDbContext _db;

    public ActivityRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Activity>> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await _db.Activities.Where(a => a.UserId == userId).OrderByDescending(a => a.CreatedAtUtc).ToListAsync(ct);

    public async Task<IReadOnlyList<Activity>> GetByContactIdAsync(Guid contactId, Guid userId, CancellationToken ct = default) =>
        await _db.Activities
            .Where(a => a.UserId == userId && a.ContactId == contactId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Activity>> GetByDealIdAsync(Guid dealId, Guid userId, CancellationToken ct = default) =>
        await _db.Activities
            .Where(a => a.UserId == userId && a.DealId == dealId)
            .OrderByDescending(a => a.CreatedAtUtc)
            .ToListAsync(ct);

    public async Task<Activity?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        await _db.Activities.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);

    public async Task<Activity> AddAsync(Activity activity, CancellationToken ct = default)
    {
        _db.Activities.Add(activity);
        await _db.SaveChangesAsync(ct);
        return activity;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var existing = await _db.Activities.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        if (existing == null) return false;
        _db.Activities.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyDictionary<Guid, DateTime>> GetLastActivityByContactIdsAsync(Guid userId, IEnumerable<Guid> contactIds, CancellationToken ct = default)
    {
        var ids = contactIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, DateTime>();
        var list = await _db.Activities
            .Where(a => a.UserId == userId && a.ContactId != null && ids.Contains(a.ContactId.Value))
            .GroupBy(a => a.ContactId!.Value)
            .Select(g => new { ContactId = g.Key, Last = g.Max(a => a.CreatedAtUtc) })
            .ToListAsync(ct);
        return list.ToDictionary(x => x.ContactId, x => x.Last);
    }

    public async Task<IReadOnlyDictionary<Guid, DateTime>> GetLastActivityByDealIdsAsync(Guid userId, IEnumerable<Guid> dealIds, CancellationToken ct = default)
    {
        var ids = dealIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, DateTime>();
        var list = await _db.Activities
            .Where(a => a.UserId == userId && a.DealId != null && ids.Contains(a.DealId.Value))
            .GroupBy(a => a.DealId!.Value)
            .Select(g => new { DealId = g.Key, Last = g.Max(a => a.CreatedAtUtc) })
            .ToListAsync(ct);
        return list.ToDictionary(x => x.DealId, x => x.Last);
    }
}
