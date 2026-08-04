using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TaskStatusEnum = ACI.Domain.Enums.TaskStatus;

namespace ACI.WebApi.Services;

/// <summary>
/// Emails task reminders once their reminder time has passed.
///
/// A task is picked up when ReminderDateUtc has arrived, it is still open, and
/// ReminderSentAtUtc is null — that last column is what stops a reminder being sent
/// twice, since this runs every few minutes.
/// </summary>
public class TaskReminderBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TaskReminderBackgroundService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

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
        var emailSettings = scope.ServiceProvider.GetRequiredService<IOptions<EmailSettings>>().Value;

        var now = DateTime.UtcNow;
        var dueTasks = await db.TaskItems
            .Include(t => t.User)
            .Include(t => t.Assignee)
            .Where(t => t.ReminderDateUtc != null
                && t.ReminderDateUtc <= now
                && t.ReminderSentAtUtc == null
                && t.Status != TaskStatusEnum.Completed
                && t.Status != TaskStatusEnum.Cancelled)
            .Take(100) // Process in batches
            .ToListAsync(ct);

        if (dueTasks.Count == 0) return;

        _logger.LogInformation("Processing {Count} task reminders", dueTasks.Count);

        // One query for everyone's notification preferences rather than one per task.
        var recipientIds = dueTasks.Select(RecipientId).Distinct().ToList();
        var settingsByUserId = await db.UserSettings
            .Where(s => recipientIds.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId, ct);

        var baseUrl = (emailSettings.FrontendBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        var sent = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var task in dueTasks)
        {
            var recipient = task.Assignee ?? task.User;

            // No settings row means defaults, which have reminders on.
            var wantsEmail = !settingsByUserId.TryGetValue(RecipientId(task), out var settings)
                || (settings.EmailNotificationsEnabled && settings.EmailOnTaskDue);

            if (!wantsEmail || string.IsNullOrWhiteSpace(recipient.Email))
            {
                skipped++;
            }
            else
            {
                var taskUrl = baseUrl.Length == 0 ? $"/tasks/{task.Id}" : $"{baseUrl}/tasks/{task.Id}";
                var ok = await emailSender.SendTaskReminderEmailAsync(
                    recipient.Email, recipient.Name, task.Title, task.DueDateUtc, taskUrl, ct);
                if (ok) sent++; else failed++;
            }

            // Marked either way: a reminder is a point-in-time nudge, so retrying it every
            // few minutes for as long as the task lives would be worse than missing one.
            // Failures are logged by the sender and counted below.
            task.ReminderSentAtUtc = now;
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Processed {Count} task reminders: {Sent} emailed, {Skipped} skipped (notifications off or no address), {Failed} failed to send",
            dueTasks.Count, sent, skipped, failed);
    }

    /// <summary>Reminders go to whoever the task is assigned to, else its owner.</summary>
    private static Guid RecipientId(TaskItem task) => task.AssigneeId ?? task.UserId;
}
