using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A backlog of old overdue tasks does not stop newer tasks getting their reminder.
/// </summary>
/// <remarks>
/// Each pass took the 200 open tasks with the oldest dates, and an overdue task stays
/// in that set for as long as it is open. Once 200 old ones existed anywhere in the
/// database, the same 200 filled every pass and no other task got a reminder or its
/// task.overdue automation again.
/// </remarks>
public class TaskReminderBacklogTests
{
    [Fact]
    public async Task ANewOverdueTaskIsRemindedDespiteTwoHundredOlderOnes()
    {
        using var factory = new CustomWebApplicationFactory();
        var (_, userId) = await SignInAsync(factory, "rep@example.ch");
        var org = await CreateAsync(factory, userId, "Club Nautique");
        var now = DateTime.UtcNow;

        Guid recent;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 0; i < 250; i++)
            {
                db.TaskItems.Add(new TaskItem
                {
                    Id = Guid.NewGuid(), UserId = userId, OrganizationId = org, Title = $"Old {i}",
                    DueDateUtc = now.AddDays(-300 + i), CreatedAtUtc = now.AddDays(-400),
                });
            }
            var task = new TaskItem
            {
                Id = Guid.NewGuid(), UserId = userId, OrganizationId = org, Title = "Call back Banque Léman",
                DueDateUtc = now.AddHours(-2), CreatedAtUtc = now.AddDays(-1),
            };
            db.TaskItems.Add(task);
            recent = task.Id;
            await db.SaveChangesAsync();
        }

        var service = new TaskReminderBackgroundService(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<TaskReminderBackgroundService>.Instance);
        await service.ProcessRemindersAsync(CancellationToken.None);
        await service.ProcessRemindersAsync(CancellationToken.None);

        using var check = factory.Services.CreateScope();
        var notifications = check.ServiceProvider.GetRequiredService<AppDbContext>().Notifications.AsNoTracking();
        (await notifications.AnyAsync(n => n.SourceKey == $"task-overdue-{recent}"))
            .Should().BeTrue("the second pass must reach past the first 200 tasks");
        (await notifications.CountAsync(n => n.SourceKey!.StartsWith("task-overdue-")))
            .Should().Be(251, "every overdue task is reminded exactly once");
    }
}
