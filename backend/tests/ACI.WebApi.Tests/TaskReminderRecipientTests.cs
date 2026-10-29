using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A task's reminder goes to someone still in its workspace.
/// </summary>
/// <remarks>
/// Writes now refuse an assignee from outside the workspace, but the job emailed
/// whoever a task was already assigned to: a rep who had left the club, or anyone a
/// task had been pointed at before assignees were checked. They kept getting the
/// task's title, and a repeating task handed them on to each next occurrence.
/// </remarks>
public class TaskReminderRecipientTests
{
    /// <summary>The app with a mail server that remembers who it sent task reminders to.</summary>
    private sealed class WithMailServer : CustomWebApplicationFactory
    {
        public readonly List<(string To, string Title)> Sent = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                var sender = new Mock<IEmailSender>();
                sender.Setup(s => s.SendTaskReminderEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Callback((string to, string _, string title, DateTime? _, string _, CancellationToken _) => Sent.Add((to, title)))
                    .ReturnsAsync(true);
                services.AddSingleton(sender.Object);
            });
        }
    }

    private static Task RunTheJobAsync(CustomWebApplicationFactory factory) =>
        new TaskReminderBackgroundService(factory.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<TaskReminderBackgroundService>.Instance)
            .ProcessRemindersAsync(CancellationToken.None);

    private static TaskItem DueTask(Guid owner, Guid? org, Guid? assignee, string title) => new()
    {
        Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, AssigneeId = assignee, Title = title,
        ReminderDateUtc = DateTime.UtcNow.AddMinutes(-5), DueDateUtc = DateTime.UtcNow.AddHours(2), CreatedAtUtc = DateTime.UtcNow,
    };

    [Fact]
    public async Task NobodyOutsideTheWorkspaceIsReminded()
    {
        using var factory = new WithMailServer();
        var (_, ownerId) = await SignInAsync(factory, "owner@pavillon46.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        var (_, repId) = await SignInAsync(factory, "rep@pavillon46.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        var (_, leftId) = await SignInAsync(factory, "left@pavillon46.ch");
        await AddMemberAsync(factory, org, leftId, OrgMemberRole.Member);
        var (_, outsiderId) = await SignInAsync(factory, "outsider@rival.ch");
        var (_, soloId) = await SignInAsync(factory, "solo@example.ch");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.TaskItems.AddRange(
                DueTask(ownerId, org, repId, "Call the Müller family"),
                DueTask(ownerId, org, leftId, "Renew the Banque Léman sponsorship"),
                DueTask(ownerId, org, outsiderId, "Send the Gold membership offer"),
                DueTask(soloId, null, outsiderId, "Book the board dinner"));
            // A rep who has since left the club.
            db.OrganizationMembers.Remove(await db.OrganizationMembers.SingleAsync(m => m.OrganizationId == org && m.UserId == leftId));
            await db.SaveChangesAsync();
        }

        await RunTheJobAsync(factory);

        factory.Sent.Should().BeEquivalentTo(new[]
        {
            ("rep@pavillon46.ch", "Call the Müller family"),
            ("owner@pavillon46.ch", "Renew the Banque Léman sponsorship"),
            ("owner@pavillon46.ch", "Send the Gold membership offer"),
            ("solo@example.ch", "Book the board dinner"),
        }, "a task whose assignee is not in its workspace is its owner's to chase");
        var bells = await factory.Services.Read(db => db.Notifications.Select(n => n.UserId).ToList());
        bells.Should().NotContain(new[] { leftId, outsiderId });
    }

    [Fact]
    public async Task WithNobodyLeftInTheWorkspaceTheReminderGoesNowhere()
    {
        using var factory = new WithMailServer();
        var (_, ownerId) = await SignInAsync(factory, "owner@pavillon46.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        var (_, formerId) = await SignInAsync(factory, "former@pavillon46.ch");
        Guid taskId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var task = DueTask(formerId, org, formerId, "Chase the unpaid invoice");
            db.TaskItems.Add(task);
            await db.SaveChangesAsync();
            taskId = task.Id;
        }

        await RunTheJobAsync(factory);

        factory.Sent.Should().BeEmpty("the task's owner and assignee both left the workspace");
        (await factory.Services.Read(db => db.TaskItems.Single(t => t.Id == taskId).ReminderSentAtUtc))
            .Should().NotBeNull("it is not retried every few minutes");
    }
}
