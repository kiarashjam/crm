using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface INotificationRepository
{
    /// <summary>Newest first, capped by <paramref name="take"/>.</summary>
    Task<IReadOnlyList<Notification>> GetForUserAsync(Guid userId, Guid? organizationId, int take, CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    Task<Notification> AddAsync(Notification notification, CancellationToken ct = default);

    /// <summary>Marks one notification read. Returns false when it does not exist or belongs to someone else.</summary>
    Task<bool> MarkReadAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>Marks every unread notification read. Returns how many changed.</summary>
    Task<int> MarkAllReadAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// SourceKeys already present for this user, so a reminder generator can skip
    /// events it has already raised without a round trip per candidate.
    /// </summary>
    Task<IReadOnlyCollection<string>> GetExistingSourceKeysAsync(Guid userId, IEnumerable<string> candidates, CancellationToken ct = default);
}
