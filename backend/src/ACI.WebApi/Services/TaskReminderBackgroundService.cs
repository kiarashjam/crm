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
                await ProcessReminders(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing task reminders");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessReminders(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
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
                    || (t.DueDateUtc != null && t.DueDateUtc <= dueSoon)))
            // Deterministic order so a backlog larger than the batch drains steadily
            // instead of re-processing an arbitrary slice each pass.
            .OrderBy(t => t.ReminderDateUtc ?? t.DueDateUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (candidates.Count == 0) return;

        // One query for everyone's notification preferences rather than one per task.
        var recipientIds = candidates.Select(TaskReminderPolicy.RecipientId).Distinct().ToList();
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
            var recipientId = TaskReminderPolicy.RecipientId(task);
            var recipient = task.Assignee ?? task.User;
            // No settings row means defaults, which have both channels on.
            settingsByUserId.TryGetValue(recipientId, out var settings);

            // ── In-app ──
            if (TaskReminderPolicy.NeedsInAppReminder(task, now))
            {
                if (settings != null && !settings.InAppNotificationsEnabled)
                {
                    inAppSkipped++;
                }
                else
                {
                    try
                    {
                        var created = await notifications.CreateTaskReminderAsync(
                            recipientId,
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
                        _logger.LogWarning(ex, "Failed to raise in-app reminder for task {TaskId}", task.Id);
                    }
                }
            }

            // ── Email ──
            if (!TaskReminderPolicy.NeedsEmailReminder(task, now)) continue;

            var wantsEmail = settings == null
                || (settings.EmailNotificationsEnabled && settings.EmailOnTaskDue);

            if (!wantsEmail || string.IsNullOrWhiteSpace(recipient.Email))
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
