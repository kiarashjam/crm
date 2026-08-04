using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// Notifications for the header bell.
///
/// The reminder generator lives here rather than in a background job so the
/// feature needs no scheduler to be useful: listing notifications first brings
/// task reminders up to date. That is safe because every generated row carries a
/// SourceKey and duplicates are rejected by a unique index, so the operation is
/// idempotent no matter how often the bell is opened.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly INotificationRepository _repository;
    private readonly ITaskRepository _taskRepository;
    private readonly ILogger<NotificationService> _logger;

    /// <summary>Matches the frontend's NotificationType union.</summary>
    private static readonly string[] ValidTypes = { "task", "deal", "lead", "mention", "system" };

    private const int DefaultTake = 50;

    public NotificationService(
        INotificationRepository repository,
        ITaskRepository taskRepository,
        ILogger<NotificationService> logger)
    {
        _repository = repository;
        _taskRepository = taskRepository;
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
        // Bring reminders up to date before listing, so opening the bell always
        // reflects current task state. Failure here must not blank the list.
        try
        {
            await SyncTaskRemindersAsync(userId, organizationId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Task reminder sync failed for user {UserId}; returning stored notifications", userId);
        }

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

    public async Task SyncTaskRemindersAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var tasks = await _taskRepository.GetByUserIdAsync(userId, organizationId, ct);
        if (tasks.Count == 0) return;

        var now = DateTime.UtcNow;
        var dayAhead = now.AddDays(1);

        // Build the candidate set first, then filter against the keys already
        // stored — one query instead of one per task.
        var candidates = new List<Notification>();
        foreach (var t in tasks)
        {
            if (t.Status == Domain.Enums.TaskStatus.Completed) continue;
            if (t.Status == Domain.Enums.TaskStatus.Cancelled) continue;
            if (t.Completed) continue;
            if (t.DueDateUtc == null) continue;

            var due = t.DueDateUtc.Value;
            var overdue = due < now;
            var dueToday = !overdue && due <= dayAhead;
            if (!overdue && !dueToday) continue;

            candidates.Add(new Notification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrganizationId = organizationId,
                Type = "task",
                Title = overdue ? "Task overdue" : "Task due today",
                Message = t.Title,
                Link = LinkForTask(t),
                Read = false,
                CreatedAtUtc = now,
                // Includes the overdue/due distinction, so a task that slips from
                // "due today" to "overdue" legitimately raises a second reminder.
                SourceKey = $"task-{(overdue ? "overdue" : "due")}-{t.Id}",
            });
        }

        if (candidates.Count == 0) return;

        var existing = await _repository.GetExistingSourceKeysAsync(
            userId, candidates.Select(c => c.SourceKey!), ct);
        var existingSet = new HashSet<string>(existing, StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            if (existingSet.Contains(candidate.SourceKey!)) continue;
            try
            {
                await _repository.AddAsync(candidate, ct);
                existingSet.Add(candidate.SourceKey!);
            }
            catch (Exception ex)
            {
                // A concurrent request may have inserted the same SourceKey between
                // our read and write; the unique index rejects it, which is the
                // intended outcome. Log and carry on with the remaining candidates.
                _logger.LogDebug(ex, "Skipped duplicate reminder {SourceKey}", candidate.SourceKey);
            }
        }
    }

    private static string LinkForTask(TaskItem t)
    {
        if (t.LeadId != null) return $"/leads/{t.LeadId}";
        if (t.DealId != null) return $"/deals/{t.DealId}";
        if (t.ContactId != null) return $"/contacts/{t.ContactId}";
        return "/tasks";
    }
}
