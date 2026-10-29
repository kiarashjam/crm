using System.Net;
using System.Text.Json;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The lead page writes "system" entries on the timeline: reassigned, a follow-up
/// done, a pipeline move.
/// </summary>
/// <remarks>
/// The type was not in the request pattern or the service's list, so every one of
/// them was refused with a 400 that the page swallowed. On the real backend the
/// lead's history had a gap wherever one should have been.
/// </remarks>
public class SystemTimelineEntryTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    [Fact]
    public async Task ASystemEntryIsSavedOnTheLeadsTimeline_AndIsNotAReply()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, userId) = await SignInAsync(factory, "rep@example.ch");
        client.In(await CreateAsync(factory, userId, "Pavillon46"));
        var lead = await Json(await client.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email = "jean@example.ch", status = "New" })), "create lead");
        var leadId = lead.GetProperty("id").GetGuid();

        var entry = await Json(await client.PostAsync("/api/activities", Body(new
        {
            type = "system", subject = "Update", body = "Lead reassigned to Anna", leadId,
        })), "log the update");

        entry.GetProperty("type").GetString().Should().Be("system");
        (await Json(await client.GetAsync($"/api/activities/lead/{leadId}"), "timeline")).EnumerateArray()
            .Select(a => a.GetProperty("type").GetString()).Should().Contain("system");
        // Not a response to the lead: the first-response clock keeps running.
        (await factory.Services.Read(db => db.Leads.Single(l => l.Id == leadId).FirstResponseAtUtc)).Should().BeNull();
        // And not work anyone logged.
        (await Json(await client.GetAsync("/api/activities/org-member-counts"), "member counts"))
            .EnumerateObject().Sum(p => p.Value.GetInt32()).Should().Be(0);
    }

    [Fact]
    public async Task AnEntryCanBeChangedToSystem()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, userId) = await SignInAsync(factory, "rep@example.ch");
        client.In(await CreateAsync(factory, userId, "Pavillon46"));
        var lead = await Json(await client.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email = "jean@example.ch", status = "New" })), "create lead");
        var note = await Json(await client.PostAsync("/api/activities", Body(new
        {
            type = "note", subject = "Update", leadId = lead.GetProperty("id").GetGuid(),
        })), "log a note");

        var changed = await Json(await client.PutAsync($"/api/activities/{note.GetProperty("id").GetGuid()}", Body(new { type = "system" })), "change type");

        changed.GetProperty("type").GetString().Should().Be("system");
    }
}
