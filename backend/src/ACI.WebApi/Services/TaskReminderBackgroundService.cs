using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskStatusEnum = ACI.Domain.Enums.TaskStatus;

namespace ACI.WebApi.Services;

/// <summary>
/// The single reminder engine for tasks. Decides that a task needs chasing, then
/// delivers on both channels the user has enabled:
///
///   · Email  — when the explicit ReminderDateUtc has passed. Deduped by
///              ReminderSentAtUtc, since an email is a point-in-time nudge.
///   · In-app — when the task is overdue or falls due within a day. Deduped by the
///              notification's SourceKey, since the bell is a standing list.
///
/// The triggers differ deliberately (see TaskReminderPolicy) so email is not sent
/// for every due task and the bell is not left blank for tasks with no reminder
/// time. What is shared is the decision itself: previously the in-app side lived on
/// the notifications read path, which meant reminders only appeared if someone
/// opened the bell, went to whoever was looking rather than the assignee, and
/// ignored the user's in-app preference.
/// </summary>
public class TaskReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaskReminderBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private const int BatchSize = 200;

    // Tasks whose bell reminder and overdue automation are already done. Without
    // these, an overdue task stayed a candidate for as long as it stayed open, and
    // once 200 of them existed anywhere the oldest 200 filled every batch: newer
    // tasks never got a reminder or their task.overdue automation again. Kept in
    // memory, so after a restart each task is looked at once more; the notification
    // and automation keys make that a no-op.
    private readonly HashSet<Guid> _overdueDone = new();
    private readonly HashSet<Guid> _dueSoonDone = new();
    private const int MaxRemembered = 50_000;

    public TaskReminderBackgroundService(IServiceScopeFactory scopeFactory, ILogger<TaskReminderBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TaskReminderBackgroundService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRemindersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing task reminders");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    /// <summary>One pass over the tasks that need a reminder. Public for tests.</summary>
    public async Task ProcessRemindersAsync(CancellationToken ct)
    {
        if (_overdueDone.Count + _dueSoonDone.Count > MaxRemembered)
        {
            _overdueDone.Clear();
            _dueSoonDone.Clear();
        }
        var overdueDone = _overdueDone.ToList();
        var dueSoonDone = _dueSoonDone.ToList();

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var automations = scope.ServiceProvider.GetRequiredService<IAutomationEngine>();
        var emailSettings = scope.ServiceProvider.GetRequiredService<IOptions<EmailSettings>>().Value;

        var now = DateTime.UtcNow;
        var dueSoon = now.Add(TaskReminderPolicy.DueSoonWindow);

        // One query covering candidates for EITHER channel. The predicates are
        // inlined rather than calling TaskReminderPolicy because EF has to translate
        // them to SQL; the policy is then applied in memory below, so the two can
        // only disagree by being over-inclusive here, never under-inclusive.
        var candidates = await db.TaskItems
            .Include(t => t.User)
            .Include(t => t.Assignee)
            .Where(t => t.Status != TaskStatusEnum.Completed
                && t.Status != TaskStatusEnum.Cancelled
                && !t.Completed
                && ((t.ReminderDateUtc != null && t.ReminderDateUtc <= now && t.ReminderSentAtUtc == null)
                    || (t.DueDateUtc != null && t.DueDateUtc <= dueSoon
                        && !overdueDone.Contains(t.Id)
                        && !(dueSoonDone.Contains(t.Id) && t.DueDateUtc >= now))))
            // Deterministic order so a backlog larger than the batch drains steadily
            // instead of re-processing an arbitrary slice each pass.
            .OrderBy(t => t.ReminderDateUtc ?? t.DueDateUtc)
            .ThenBy(t => t.Id)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (candidates.Count == 0) return;

        // Who is still in each task's workspace: an organization's members, and in a
        // personal workspace only its owner (see TaskReminderPolicy.RecipientId).
        var orgIds = candidates.Where(t => t.OrganizationId != null).Select(t => t.OrganizationId!.Value).Distinct().ToList();
        var people = candidates.SelectMany(t => t.AssigneeId is Guid a ? new[] { a, t.UserId } : new[] { t.UserId }).Distinct().ToList();
        var memberships = orgIds.Count == 0
            ? new HashSet<(Guid, Guid)>()
            : (await db.OrganizationMembers
                .Where(m => orgIds.Contains(m.OrganizationId) && people.Contains(m.UserId))
                .Select(m => new { m.OrganizationId, m.UserId })
                .ToListAsync(ct))
                .Select(m => (m.OrganizationId, m.UserId))
                .ToHashSet();
        Guid? RecipientOf(TaskItem task) => TaskReminderPolicy.RecipientId(task, user =>
            task.OrganizationId is Guid org ? memberships.Contains((org, user)) : user == task.UserId);

        // One query for everyone's notification preferences rather than one per task.
        var recipientIds = candidates.Select(RecipientOf).OfType<Guid>().Distinct().ToList();
        var settingsByUserId = await db.UserSettings
            .Where(s => recipientIds.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId, ct);

        var baseUrl = (emailSettings.FrontendBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        var emailed = 0;
        var emailSkipped = 0;
        var emailFailed = 0;
        var raised = 0;
        var inAppSkipped = 0;

        foreach (var task in candidates)
        {
            // Null when neither the assignee nor the owner is still in the workspace.
            var recipientId = RecipientOf(task);
            var recipient = recipientId == null ? null : recipientId == task.AssigneeId ? task.Assignee : task.User;
            // No settings row means defaults, which have both channels on.
            UserSettings? settings = null;
            if (recipientId is Guid id) settingsByUserId.TryGetValue(id, out settings);

            var handled = true;

            // ── In-app ──
            if (TaskReminderPolicy.NeedsInAppReminder(task, now))
            {
                if (recipientId is not Guid inAppRecipient || (settings != null && !settings.InAppNotificationsEnabled))
                {
                    inAppSkipped++;
                }
                else
                {
                    try
                    {
                        var created = await notifications.CreateTaskReminderAsync(
                            inAppRecipient,
                            task.OrganizationId,
                            task,
                            TaskReminderPolicy.IsOverdue(task, now),
                            ct);
                        if (created) raised++;
                    }
                    catch (Exception ex)
                    {
                        // One bad notification must not stop the email side or the
                        // rest of the batch.
                        handled = false;
                        _logger.LogWarning(ex, "Failed to raise in-app reminder for task {TaskId}", task.Id);
                    }
                }
            }

            if (TaskReminderPolicy.IsOverdue(task, now))
            {
                try
                {
                    await automations.DispatchAsync("task.overdue", new AutomationEvent(
                        task.OrganizationId,
                        recipientId ?? task.UserId,
                        task.Id.ToString(),
                        task.Title,
                        LeadId: task.LeadId,
                        DealId: task.DealId,
                        TaskId: task.Id,
                        AssigneeId: recipientId), ct);
                }
                catch (Exception ex)
                {
                    handled = false;
                    _logger.LogWarning(ex, "Overdue automation failed for task {TaskId}", task.Id);
                }
            }

            // Retried next pass if something failed; otherwise this state is done.
            if (handled && TaskReminderPolicy.NeedsInAppReminder(task, now))
            {
                if (TaskReminderPolicy.IsOverdue(task, now)) _overdueDone.Add(task.Id);
                else _dueSoonDone.Add(task.Id);
            }

            // ── Email ──
            if (!TaskReminderPolicy.NeedsEmailReminder(task, now)) continue;

            var wantsEmail = settings == null
                || (settings.EmailNotificationsEnabled && settings.EmailOnTaskDue);

            if (recipient == null || !wantsEmail || string.IsNullOrWhiteSpace(recipient.Email))
            {
                emailSkipped++;
            }
            else
            {
                var taskUrl = baseUrl.Length == 0 ? $"/tasks/{task.Id}" : $"{baseUrl}/tasks/{task.Id}";
                var ok = await emailSender.SendTaskReminderEmailAsync(
                    recipient.Email, recipient.Name, task.Title, task.DueDateUtc, taskUrl, ct);
                if (ok) emailed++; else emailFailed++;
            }

            // Marked either way: a reminder is a point-in-time nudge, so retrying it every
            // few minutes for as long as the task lives would be worse than missing one.
            // Failures are logged by the sender and counted below.
            task.ReminderSentAtUtc = now;
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Task reminders: {Raised} in-app raised, {InAppSkipped} in-app skipped (preference off); "
            + "{Emailed} emailed, {EmailSkipped} email skipped (preference off or no address), {EmailFailed} email failed",
            raised, inAppSkipped, emailed, emailSkipped, emailFailed);
    }
}
