using System.Text.Json;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Enums;
using ACI.WebApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Task reminders end to end: tasks made, assigned, finished and preferences set through the
/// API, the reminder job run, and what each person then finds in their mail and their bell.
/// </summary>
/// <remarks>
/// The other reminder tests write tasks straight into the database. These start where a
/// rep starts, so a field the API drops on the way in (an assignee, a reminder time, a
/// status, a preference) shows up as a reminder that never comes or comes to the wrong person.
/// </remarks>
public class TaskRemindersOverHttpTests
{
    /// <summary>The app with a mail server that remembers each task reminder it sent, and to whom.</summary>
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

    private sealed record Club(WithMailServer Factory, HttpClient Owner, HttpClient Rep, Guid RepId) : IDisposable
    {
        public void Dispose() => Factory.Dispose();
    }

    private static async Task<Club> ClubAsync()
    {
        var factory = new WithMailServer();
        var (owner, ownerId) = await SignInAsync(factory, "owner@club.ch");
        var (rep, repId) = await SignInAsync(factory, "rep@club.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        return new Club(factory, owner.In(org), rep.In(org), repId);
    }

    private static Task RunTheJobAsync(CustomWebApplicationFactory factory) =>
        new TaskReminderBackgroundService(factory.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<TaskReminderBackgroundService>.Instance)
            .ProcessRemindersAsync(CancellationToken.None);

    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed, got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<Guid> TaskAsync(HttpClient client, string title, Guid? assigneeId, DateTime? reminderDateUtc, DateTime? dueDateUtc) =>
        (await Ok(await client.PostAsync("/api/tasks", Body(new { title, assigneeId, reminderDateUtc, dueDateUtc })), $"create {title}"))
            .GetProperty("id").GetGuid();

    // The reminders in someone's bell: the job's, not the note that a task was assigned.
    private static async Task<List<string?>> RemindersInBellAsync(HttpClient client) =>
        (await Ok(await client.GetAsync("/api/notifications"), "read the bell")).EnumerateArray()
            .Where(n => n.GetProperty("title").GetString() is var title
                && (title == TaskReminderPolicy.Title(overdue: false) || title == TaskReminderPolicy.Title(overdue: true)))
            .Select(n => n.GetProperty("message").GetString())
            .ToList();

    [Fact]
    public async Task ATaskForATeammateRemindsThemOnceByMailAndInTheBell()
    {
        using var club = await ClubAsync();
        var now = DateTime.UtcNow;
        await TaskAsync(club.Owner, "Call Banque Léman", club.RepId, reminderDateUtc: now.AddMinutes(-5), dueDateUtc: now.AddHours(2));

        await RunTheJobAsync(club.Factory);
        await RunTheJobAsync(club.Factory);

        club.Factory.Sent.Should().Equal(new[] { ("rep@club.ch", "Call Banque Léman") }, "the assignee is reminded, and once");
        (await RemindersInBellAsync(club.Rep)).Should().Equal("Call Banque Léman");
        (await RemindersInBellAsync(club.Owner)).Should().BeEmpty("whoever made the task for someone else is not the one to do it");
    }

    [Fact]
    public async Task AFinishedTaskIsNotReminded()
    {
        using var club = await ClubAsync();
        var now = DateTime.UtcNow;
        var id = await TaskAsync(club.Rep, "Send the proposal", club.RepId, reminderDateUtc: now.AddMinutes(-5), dueDateUtc: now.AddHours(2));
        await Ok(await club.Rep.PatchAsync($"/api/tasks/{id}/status", Body(new { status = "completed" })), "finish the task");

        await RunTheJobAsync(club.Factory);

        club.Factory.Sent.Should().BeEmpty();
        (await RemindersInBellAsync(club.Rep)).Should().BeEmpty();
    }

    [Fact]
    public async Task AReminderStillAheadWaitsForItsTime()
    {
        using var club = await ClubAsync();
        var now = DateTime.UtcNow;
        await TaskAsync(club.Rep, "Book the tasting", club.RepId, reminderDateUtc: now.AddHours(2), dueDateUtc: now.AddDays(3));

        await RunTheJobAsync(club.Factory);

        club.Factory.Sent.Should().BeEmpty();
        (await RemindersInBellAsync(club.Rep)).Should().BeEmpty();
    }

    [Fact]
    public async Task SomeoneWhoTurnedTaskMailOffIsRemindedInTheBellOnly()
    {
        using var club = await ClubAsync();
        await Ok(await club.Rep.PutAsync("/api/settings", Body(new { emailOnTaskDue = false })), "turn task mail off");
        var now = DateTime.UtcNow;
        await TaskAsync(club.Owner, "Confirm the menu", club.RepId, reminderDateUtc: now.AddMinutes(-5), dueDateUtc: now.AddHours(2));

        await RunTheJobAsync(club.Factory);

        club.Factory.Sent.Should().BeEmpty();
        (await RemindersInBellAsync(club.Rep)).Should().Equal("Confirm the menu");
    }
}
