using System.Net;
using System.Text.Json;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>A task that repeats opens its next one when it is done, once.</summary>
public class RepeatingTasksTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static Task<HttpResponseMessage> SetStatusAsync(HttpClient client, Guid id, string status) =>
        client.PatchAsync($"/api/tasks/{id}/status", Body(new { status }));

    [Fact]
    public async Task CompletingAWeeklyTaskOpensNextWeeks_AndOnlyOnce()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, userId) = await SignInAsync(factory, "kia@example.ch");
        client.In(await CreateAsync(factory, userId, "Pavillon46"));
        var due = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);

        var task = await Json(await client.PostAsync("/api/tasks", Body(new { title = "Weekly pipeline review", dueDateUtc = due, recurrence = "weekly" })), "create");
        task.GetProperty("recurrence").GetString().Should().Be("weekly", "the screen has to be able to show that it repeats");
        var id = task.GetProperty("id").GetGuid();

        await Json(await SetStatusAsync(client, id, "completed"), "complete");
        await Json(await SetStatusAsync(client, id, "todo"), "reopen");
        await Json(await SetStatusAsync(client, id, "completed"), "complete again");

        var next = (await factory.Services.Read(db => db.TaskItems.Where(t => t.Title == "Weekly pipeline review" && t.Id != id).ToList()))
            .Should().ContainSingle("reopening and completing it again used to open a second copy").Subject;
        next.DueDateUtc.Should().Be(due.AddDays(7));
        next.Recurrence.Should().Be("weekly", "the series carries on");
    }
}
