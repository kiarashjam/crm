using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using TaskStatusEnum = ACI.Domain.Enums.TaskStatus;

namespace ACI.WebApi.Services;

/// <summary>
/// Background service that checks for tasks with overdue reminders and logs them.
/// In production, this would send emails or push notifications. For now it marks reminders as "sent"
/// so the system is ready for notification delivery when an email service is integrated.
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

        var now = DateTime.UtcNow;
        var dueTasks = await db.TaskItems
            .Include(t => t.User)
            .Where(t => t.ReminderDateUtc != null
                && t.ReminderDateUtc <= now
                && t.ReminderSentAtUtc == null
                && t.Status != TaskStatusEnum.Completed
                && t.Status != TaskStatusEnum.Cancelled)
            .Take(100) // Process in batches
            .ToListAsync(ct);

        if (dueTasks.Count == 0) return;

        _logger.LogInformation("Processing {Count} task reminders", dueTasks.Count);

        foreach (var task in dueTasks)
        {
            // TODO: When an IEmailService is registered, send actual email here:
            // var settings = await db.UserSettings.FirstOrDefaultAsync(s => s.UserId == task.UserId, ct);
            // if (settings?.EmailOnTaskDue == true)
            //     await emailService.SendTaskDueReminderAsync(task, task.User);

            task.ReminderSentAtUtc = now;
            _logger.LogInformation(
                "Reminder processed for task {TaskId} '{Title}' (user {UserId})",
                task.Id, task.Title, task.UserId);
        }

        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Processed {Count} task reminders", dueTasks.Count);
    }
}
