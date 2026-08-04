using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _db;

    public NotificationRepository(AppDbContext db) => _db = db;

    /// <summary>
    /// Always scoped to the recipient — unlike Activities, a notification is never
    /// visible to other members of the organization, so the org id narrows the
    /// query further rather than replacing the user check.
    /// </summary>
    private static IQueryable<Notification> ForUser(IQueryable<Notification> q, Guid userId, Guid? organizationId) =>
        organizationId == null
            ? q.Where(n => n.UserId == userId && n.OrganizationId == null)
            : q.Where(n => n.UserId == userId && n.OrganizationId == organizationId);

    public async Task<IReadOnlyList<Notification>> GetForUserAsync(Guid userId, Guid? organizationId, int take, CancellationToken ct = default)
    {
        return await ForUser(_db.Notifications.AsNoTracking(), userId, organizationId)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(take)
            .ToListAsync(ct);
    }

    public Task<int> GetUnreadCountAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        ForUser(_db.Notifications.AsNoTracking(), userId, organizationId).CountAsync(n => !n.Read, ct);

    public async Task<Notification> AddAsync(Notification notification, CancellationToken ct = default)
    {
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync(ct);
        return notification;
    }

    public async Task<bool> MarkReadAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var entity = await ForUser(_db.Notifications, userId, organizationId).FirstOrDefaultAsync(n => n.Id == id, ct);
        if (entity == null) return false;
        if (entity.Read) return true;   // already read — idempotent, not an error
        entity.Read = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> MarkAllReadAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var unread = await ForUser(_db.Notifications, userId, organizationId).Where(n => !n.Read).ToListAsync(ct);
        if (unread.Count == 0) return 0;
        foreach (var n in unread) n.Read = true;
        await _db.SaveChangesAsync(ct);
        return unread.Count;
    }

    public async Task<IReadOnlyCollection<string>> GetExistingSourceKeysAsync(Guid userId, IEnumerable<string> candidates, CancellationToken ct = default)
    {
        var wanted = candidates.Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
        if (wanted.Count == 0) return Array.Empty<string>();
        return await _db.Notifications.AsNoTracking()
            .Where(n => n.UserId == userId && n.SourceKey != null && wanted.Contains(n.SourceKey))
            .Select(n => n.SourceKey!)
            .ToListAsync(ct);
    }
}
