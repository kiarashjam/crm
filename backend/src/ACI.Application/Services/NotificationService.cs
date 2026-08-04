using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// Notifications for the header bell.
///
/// Reminder TIMING is not decided here. TaskReminderBackgroundService owns it for
/// both channels (email and in-app) so one place decides that a task needs
/// chasing; this service only persists and reads the in-app side. Generating
/// reminders on the read path — as an earlier version did — meant the bell had to
/// be opened for anything to appear, notified whoever happened to be looking
/// rather than the task's assignee, and ignored the user's in-app preference.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;
    private readonly ILogger<NotificationService> _logger;

    /// <summary>Matches the frontend's NotificationType union.</summary>
    private static readonly string[] ValidTypes = { "task", "deal", "lead", "mention", "system" };

    private const int DefaultTake = 50;

    public NotificationService(
        INotificationRepository repository,
        ILogger<NotificationService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    private static NotificationDto ToDto(Notification n) => new(
        n.Id,
        n.Type,
        n.Title,
        n.Message,
        n.Link,
        n.Read,
        n.CreatedAtUtc,
        n.SourceKey);

    public async Task<IReadOnlyList<NotificationDto>> GetForUserAsync(
        Guid userId, Guid? organizationId, int take = DefaultTake, CancellationToken ct = default)
    {
        // A pure read. Reminders are produced by the background service, so listing
        // never writes and the bell is cheap to poll.
        var items = await _repository.GetForUserAsync(userId, organizationId, Math.Clamp(take, 1, 200), ct);
        return items.Select(ToDto).ToList();
    }

    public Task<int> GetUnreadCountAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        _repository.GetUnreadCountAsync(userId, organizationId, ct);

    public async Task<NotificationDto?> CreateAsync(
        Guid userId, Guid? organizationId, CreateNotificationRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title)) return null;

        var type = (request.Type ?? string.Empty).Trim().ToLowerInvariant();
        if (!ValidTypes.Contains(type)) type = "system";

        var entity = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            Type = type,
            Title = request.Title.Trim(),
            Message = request.Message,
            Link = request.Link,
            Read = request.Read,
            CreatedAtUtc = request.CreatedAtUtc ?? DateTime.UtcNow,
            SourceKey = string.IsNullOrWhiteSpace(request.SourceKey) ? null : request.SourceKey.Trim(),
        };

        var saved = await _repository.AddAsync(entity, ct);
        return ToDto(saved);
    }

    public Task<bool> MarkReadAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        _repository.MarkReadAsync(id, userId, organizationId, ct);

    public Task<int> MarkAllReadAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        _repository.MarkAllReadAsync(userId, organizationId, ct);

    public async Task<bool> CreateTaskReminderAsync(
        Guid recipientId,
        Guid? organizationId,
        TaskItem task,
        bool overdue,
        CancellationToken ct = default)
    {
        var sourceKey = TaskReminderPolicy.SourceKey(task, overdue);

        // Cheap pre-check so the common case does not rely on catching a unique-index
        // violation; the catch below still covers a concurrent insert.
        var existing = await _repository.GetExistingSourceKeysAsync(recipientId, new[] { sourceKey }, ct);
        if (existing.Count > 0) return false;

        var entity = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = recipientId,
            OrganizationId = organizationId,
            Type = "task",
            Title = TaskReminderPolicy.Title(overdue),
            Message = task.Title,
            Link = TaskReminderPolicy.Link(task),
            Read = false,
            CreatedAtUtc = DateTime.UtcNow,
            SourceKey = sourceKey,
        };

        try
        {
            await _repository.AddAsync(entity, ct);
            return true;
        }
        catch (Exception ex)
        {
            // Another run inserted the same SourceKey between our check and write.
            // The unique index rejecting it is the intended outcome, not an error.
            _logger.LogDebug(ex, "Skipped duplicate task reminder {SourceKey}", sourceKey);
            return false;
        }
    }
}
