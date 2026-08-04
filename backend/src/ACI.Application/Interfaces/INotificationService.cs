using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

/// <summary>
/// Notifications surfaced in the header bell, plus the server-side reminder
/// generator that keeps them current.
/// </summary>
public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetForUserAsync(Guid userId, Guid? organizationId, int take = 50, CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    Task<NotificationDto?> CreateAsync(Guid userId, Guid? organizationId, CreateNotificationRequest request, CancellationToken ct = default);

    Task<bool> MarkReadAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    Task<int> MarkAllReadAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Turns the user's overdue / due-today tasks into notifications, deduped by
    /// SourceKey. Safe to call on every list request: re-running raises nothing new.
    /// </summary>
    Task SyncTaskRemindersAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
}
