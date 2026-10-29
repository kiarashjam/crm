using System.Net;
using System.Text.Json;
using ACI.Application.Services;
using ACI.Domain.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// What a sequence's steps do to real people, run through the real repositories.
/// </summary>
public class SequenceOutreachTests
{
    /// <summary>The app, with due steps run by the test rather than by the minute-by-minute runner.</summary>
    private sealed class App : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                var runner = services.Single(d => d.ImplementationType == typeof(ACI.WebApi.Services.LeadFollowUpBackgroundService));
                services.Remove(runner);
            });
        }

        public async Task RunDueStepsAsync()
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ISequenceService>().RunDueAsync();
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<Guid> SequenceAsync(HttpClient client, object step) =>
        (await Json(await client.PostAsync("/api/sequences", Body(new { name = "New lead outreach", status = "active", steps = new[] { step } })), "create sequence"))
            .GetProperty("id").GetGuid();

    private static async Task<Guid> LeadAsync(HttpClient client, string email = "jean@example.ch") =>
        (await Json(await client.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email, status = "New" })), "create lead"))
            .GetProperty("id").GetGuid();

    private static Task<HttpResponseMessage> EnrollAsync(HttpClient client, Guid sequenceId, Guid leadId) =>
        client.PostAsync("/api/sequences/enroll", Body(new { sequenceId, targetType = "lead", targetId = leadId, targetName = "Jean Dupont" }));

    [Fact]
    public async Task ACallStepGoesToWhoeverWorksTheLead()
    {
        // The owner enrolls a lead that was routed to a rep. The call step used to be
        // created for the owner, so the rep never saw it.
        await using var app = new App();
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon46");
        owner.In(org);
        var (_, repId) = await SignInAsync(app, "rep@example.ch");
        await AddMemberAsync(app, org, repId, OrgMemberRole.Member);
        var sequenceId = await SequenceAsync(owner, new { type = "call", dayOffset = 0, taskTitle = "Call Jean" });
        var leadId = await LeadAsync(owner);
        await Json(await owner.PutAsync($"/api/leads/{leadId}/assign", Body(new { assignedToUserId = repId })), "assign to the rep");
        await Json(await EnrollAsync(owner, sequenceId, leadId), "enroll");

        await app.RunDueStepsAsync();

        var task = (await Json(await owner.GetAsync($"/api/tasks/by-lead/{leadId}"), "tasks")).EnumerateArray().Single();
        task.GetProperty("title").GetString().Should().Be("Call Jean");
        task.GetProperty("assigneeId").GetGuid().Should().Be(repId);
    }

    [Fact]
    public async Task ACallStepForAnUnassignedLeadStaysWithWhoeverEnrolledThem()
    {
        await using var app = new App();
        var (owner, ownerId) = await SignInAsync(app, "owner@example.ch");
        owner.In(await CreateAsync(app, ownerId, "Pavillon46"));
        var sequenceId = await SequenceAsync(owner, new { type = "call", dayOffset = 0, taskTitle = "Call Jean" });
        var leadId = await LeadAsync(owner);
        await Json(await EnrollAsync(owner, sequenceId, leadId), "enroll");

        await app.RunDueStepsAsync();

        var task = await app.Services.Read(db => db.TaskItems.Single(t => t.LeadId == leadId));
        task.AssigneeId.Should().BeNull();
        task.UserId.Should().Be(ownerId);
    }
}
