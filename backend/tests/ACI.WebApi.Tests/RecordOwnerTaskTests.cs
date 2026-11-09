using System.Net;
using System.Text.Json;
using ACI.Application.Services;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The tasks that automations and sequences make go to whoever works the record, who is
/// told the rule or sequence gave it to them; with nobody in the workspace working it,
/// whoever set it off keeps it.
/// </summary>
/// <remarks>
/// Phase 0 moved these tasks off whoever set the rule off or enrolled the lead, and
/// tested a lead's rule and a sequence's call. These check what it left untested: a
/// deal's rule, what the rep is told, and a lead whose rep has since left.
/// </remarks>
public class RecordOwnerTaskTests
{
    /// <summary>The app, with due sequence steps run by the test rather than the minute-by-minute runner.</summary>
    private sealed class App : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
                services.Remove(services.Single(d => d.ImplementationType == typeof(ACI.WebApi.Services.LeadFollowUpBackgroundService))));
        }

        public async Task RunDueStepsAsync()
        {
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ISequenceService>().RunDueAsync();
        }
    }

    private sealed record Team(App App, Guid Org, HttpClient Marc, Guid MarcId, HttpClient Anna, Guid AnnaId) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => App.DisposeAsync();
    }

    /// <summary>Marc owns the workspace; Anna Keller is a rep in it.</summary>
    private static async Task<Team> TeamAsync()
    {
        var app = new App();
        var (marc, marcId) = await SignInAsync(app, "marc@pavillon46.ch");
        var org = await CreateAsync(app, marcId, "Pavillon46");
        var (anna, annaId) = await SignInAsync(app, "anna@pavillon46.ch");
        await AddMemberAsync(app, org, annaId, OrgMemberRole.Member);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Single(u => u.Id == marcId).Name = "Marc Weber";
            db.Users.Single(u => u.Id == annaId).Name = "Anna Keller";
            await db.SaveChangesAsync();
        }
        return new Team(app, org, marc.In(org), marcId, anna.In(org), annaId);
    }

    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed, got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task RuleAsync(HttpClient client, string name, string trigger, string title) =>
        await Ok(await client.PostAsync("/api/automations", Body(new
        {
            name, trigger, enabled = true,
            actions = new[] { new { type = "create_task", config = new { title, dueInDays = "0" } } },
        })), $"add the rule {name}");

    private static async Task<Guid> LeadForAnnaAsync(Team team)
    {
        var leadId = (await Ok(await team.Marc.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email = "jean@example.ch", status = "New" })), "create lead"))
            .GetProperty("id").GetGuid();
        await Ok(await team.Marc.PutAsync($"/api/leads/{leadId}/assign", Body(new { assignedToUserId = team.AnnaId })), "give it to Anna");
        return leadId;
    }

    private static async Task AnnaLeavesAsync(Team team) =>
        (await team.Marc.DeleteAsync($"/api/organizations/{team.Org}/members/{team.AnnaId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task<List<JsonElement>> TasksOnTheBellAsync(HttpClient client) =>
        (await Ok(await client.GetAsync("/api/notifications"), "read the bell")).EnumerateArray()
            .Where(n => n.GetProperty("type").GetString() == "task").ToList();

    private static async Task<Guid> CallSequenceAsync(HttpClient client) =>
        (await Ok(await client.PostAsync("/api/sequences", Body(new
        {
            name = "New lead outreach", status = "active",
            steps = new[] { new { type = "call", dayOffset = 0, taskTitle = "Call Jean" } },
        })), "create sequence")).GetProperty("id").GetGuid();

    private static async Task<Guid> EnrollAsync(HttpClient client, Guid sequenceId, Guid leadId) =>
        (await Ok(await client.PostAsync("/api/sequences/enroll", Body(new { sequenceId, targetType = "lead", targetId = leadId, targetName = "Jean Dupont" })), "enroll"))
            .GetProperty("id").GetGuid();

    [Fact]
    public async Task ADealsRuleGivesItsTaskToWhoeverWorksTheDeal_AndTellsThemTheRuleDidIt()
    {
        await using var team = await TeamAsync();
        await RuleAsync(team.Marc, "Welcome pack", "deal.won", "Send the welcome pack");
        var dealId = (await Ok(await team.Marc.PostAsync("/api/deals", Body(new { name = "Family membership", amount = 4800, currency = "CHF", assigneeId = team.AnnaId })), "create deal"))
            .GetProperty("id").GetGuid();

        await Ok(await team.Marc.PutAsync($"/api/deals/{dealId}", Body(new { isWon = true })), "Marc marks it won");

        var task = await team.App.Services.Read(db => db.TaskItems.Single(t => t.DealId == dealId));
        task.Title.Should().Be("Send the welcome pack");
        task.AssigneeId.Should().Be(team.AnnaId, "Anna works the deal, though Marc closed it");
        var bell = await TasksOnTheBellAsync(team.Anna);
        bell.Should().ContainSingle();
        bell[0].GetProperty("title").GetString().Should().Be("The automation \"Welcome pack\" gave you a task");
        bell[0].GetProperty("message").GetString().Should().Be("Send the welcome pack");
    }

    [Fact]
    public async Task ARulesTaskForALeadWhoseRepHasLeftStaysWithWhoeverSetItOff()
    {
        // The lead still names Anna, who has left: a task for her could not be opened by
        // anyone, and the workspace would refuse to make it.
        await using var team = await TeamAsync();
        await RuleAsync(team.Marc, "Follow up", "lead.status_changed", "Follow up on the new status");
        var leadId = await LeadForAnnaAsync(team);
        await AnnaLeavesAsync(team);

        await Ok(await team.Marc.PutAsync($"/api/leads/{leadId}", Body(new { status = "Contacted" })), "Marc moves the lead on");

        var task = await team.App.Services.Read(db => db.TaskItems.Single(t => t.LeadId == leadId));
        task.AssigneeId.Should().BeNull();
        task.UserId.Should().Be(team.MarcId);
    }

    [Fact]
    public async Task ASequencesCallTellsTheRepItGoesToThatTheSequenceGaveIt()
    {
        await using var team = await TeamAsync();
        var sequenceId = await CallSequenceAsync(team.Marc);
        var leadId = await LeadForAnnaAsync(team);
        await EnrollAsync(team.Marc, sequenceId, leadId);

        await team.App.RunDueStepsAsync();

        var task = await team.App.Services.Read(db => db.TaskItems.Single(t => t.LeadId == leadId));
        task.AssigneeId.Should().Be(team.AnnaId);
        var bell = await TasksOnTheBellAsync(team.Anna);
        bell.Should().ContainSingle();
        bell[0].GetProperty("title").GetString().Should().Be("The sequence \"New lead outreach\" gave you a task");
        bell[0].GetProperty("message").GetString().Should().Be("Call Jean");
        bell[0].GetProperty("link").GetString().Should().Be($"/tasks/{task.Id}");
    }

    [Fact]
    public async Task ASequencesCallForALeadWhoseRepHasLeftStaysWithWhoeverEnrolledThem_AndTheSequenceMovesOn()
    {
        await using var team = await TeamAsync();
        var sequenceId = await CallSequenceAsync(team.Marc);
        var leadId = await LeadForAnnaAsync(team);
        await AnnaLeavesAsync(team);
        var enrollmentId = await EnrollAsync(team.Marc, sequenceId, leadId);

        await team.App.RunDueStepsAsync();

        var task = await team.App.Services.Read(db => db.TaskItems.Single(t => t.LeadId == leadId));
        task.AssigneeId.Should().BeNull();
        task.UserId.Should().Be(team.MarcId);
        // Not held back to retry a task for someone who is gone.
        (await team.App.Services.Read(db => db.OutreachEnrollments.Single(e => e.Id == enrollmentId).Status)).Should().Be("completed");
    }
}
