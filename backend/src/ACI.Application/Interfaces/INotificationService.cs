using ACI.Application.DTOs;
using ACI.Domain.Entities;

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
    /// Raises the in-app reminder for a due/overdue task, unless one with the same
    /// SourceKey already exists. Returns true when a notification was created.
    ///
    /// Called only by TaskReminderBackgroundService, which owns reminder timing for
    /// both channels — see TaskReminderPolicy.
    /// </summary>
    Task<bool> CreateTaskReminderAsync(
        Guid recipientId,
        Guid? organizationId,
        TaskItem task,
        bool overdue,
        CancellationToken ct = default);
}
