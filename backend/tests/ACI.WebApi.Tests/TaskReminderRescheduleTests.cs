using System.Net;
using System.Text.Json;
using ACI.Application.Interfaces;
using ACI.WebApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A task's reminder that is moved goes out again at its new time.
/// </summary>
/// <remarks>
/// The job emails a reminder once and stamps ReminderSentAtUtc, and nothing cleared
/// the stamp. A rep who could not reach the client and moved the reminder to the next
/// morning was never reminded again.
/// </remarks>
public class TaskReminderRescheduleTests
{
    /// <summary>The app with a mail server that remembers which task reminders it sent.</summary>
    private sealed class WithMailServer : CustomWebApplicationFactory
    {
        public readonly List<string> Reminded = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                var sender = new Mock<IEmailSender>();
                sender.Setup(s => s.SendTaskReminderEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Callback((string _, string _, string title, DateTime? _, string _, CancellationToken _) => Reminded.Add(title))
                    .ReturnsAsync(true);
                services.AddSingleton(sender.Object);
            });
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static Task RunTheJobAsync(CustomWebApplicationFactory factory) =>
        new TaskReminderBackgroundService(factory.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<TaskReminderBackgroundService>.Instance)
            .ProcessRemindersAsync(CancellationToken.None);

    [Fact]
    public async Task AMovedReminderIsSentAgain_AndAnUnchangedOneIsNot()
    {
        using var factory = new WithMailServer();
        var (client, userId) = await SignInAsync(factory, "rep@example.ch");
        client.In(await CreateAsync(factory, userId, "Pavillon46"));
        var now = DateTime.UtcNow;
        var firstReminder = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc).AddMinutes(-10);

        var task = await Json(await client.PostAsync("/api/tasks", Body(new
        {
            title = "Call Banque Léman", reminderDateUtc = firstReminder, dueDateUtc = now.AddDays(3),
        })), "create");
        var id = task.GetProperty("id").GetGuid();
        await RunTheJobAsync(factory);
        factory.Reminded.Should().Equal("Call Banque Léman");

        // Could not reach them; the reminder moves on (here to a moment ago, so it is due now).
        var moved = firstReminder.AddMinutes(5);
        await Json(await client.PutAsync($"/api/tasks/{id}", Body(new { reminderDateUtc = moved })), "move the reminder");
        await RunTheJobAsync(factory);
        factory.Reminded.Should().HaveCount(2, "the moved reminder is a new one");

        // The edit dialog sends the reminder back unchanged with every save.
        await Json(await client.PutAsync($"/api/tasks/{id}", Body(new { title = "Call Banque Léman (CFO)", reminderDateUtc = moved })), "save another change");
        await RunTheJobAsync(factory);
        factory.Reminded.Should().HaveCount(2, "a reminder that did not move is not sent twice");
    }
}
