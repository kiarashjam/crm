using System.Text.Json;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// "My work": a rep's own tasks, leads and deals, served by the server, with the team's
/// one switch away.
/// </summary>
/// <remarks>
/// Every list was the whole workspace's. The Tasks page showed everyone's tasks, and the
/// assistant's queue told every rep to do every other rep's work. A record is a rep's when
/// it is given to them, or given to nobody and made by them (see WorkScope).
/// </remarks>
public class MyWorkTests
{
    private sealed record Team(CustomWebApplicationFactory Factory, Guid Org, HttpClient Marc, Guid MarcId, HttpClient Anna, Guid AnnaId, HttpClient Luca, Guid LucaId)
        : IDisposable
    {
        public void Dispose() => Factory.Dispose();
    }

    private static async Task<Team> TeamAsync()
    {
        var factory = new CustomWebApplicationFactory();
        var (marc, marcId) = await SignInAsync(factory, "marc@pavillon46.ch");
        var org = await CreateAsync(factory, marcId, "Pavillon46");
        var (anna, annaId) = await SignInAsync(factory, "anna@pavillon46.ch");
        var (luca, lucaId) = await SignInAsync(factory, "luca@pavillon46.ch");
        await AddMemberAsync(factory, org, annaId, OrgMemberRole.Member);
        await AddMemberAsync(factory, org, lucaId, OrgMemberRole.Member);
        return new Team(factory, org, marc.In(org), marcId, anna.In(org), annaId, luca.In(org), lucaId);
    }

    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed, got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<Guid> TaskAsync(HttpClient client, string title, Guid? assigneeId = null, string? status = null) =>
        (await Ok(await client.PostAsync("/api/tasks", Body(new { title, assigneeId, status })), $"create {title}")).GetProperty("id").GetGuid();

    private static async Task<Guid> LeadAsync(HttpClient client, string name) =>
        (await Ok(await client.PostAsync("/api/leads", Body(new { name, email = $"{name.Replace(' ', '.').ToLowerInvariant()}@example.ch", status = "New" })), $"create {name}"))
            .GetProperty("id").GetGuid();

    private static async Task<Guid> DealAsync(HttpClient client, string name, Guid? assigneeId = null) =>
        (await Ok(await client.PostAsync("/api/deals", Body(new { name, amount = 1000, currency = "CHF", assigneeId })), $"create {name}"))
            .GetProperty("id").GetGuid();

    private static IEnumerable<string?> Titles(JsonElement list, string field = "title") =>
        list.EnumerateArray().Select(t => t.GetProperty(field).GetString());

    [Fact]
    public async Task MyTasksAreTheOnesGivenToMeOrMadeByMeAndGivenToNobody()
    {
        using var team = await TeamAsync();
        await TaskAsync(team.Marc, "Marc gives Anna", team.AnnaId);
        await TaskAsync(team.Anna, "Anna's own, unassigned");
        await TaskAsync(team.Anna, "Anna gives Luca", team.LucaId);
        await TaskAsync(team.Luca, "Luca's own, unassigned");
        await TaskAsync(team.Marc, "Marc's own, unassigned");

        var mine = await Ok(await team.Anna.GetAsync("/api/tasks/all?scope=mine"), "my tasks");
        var everyone = await Ok(await team.Anna.GetAsync("/api/tasks/all"), "the team's tasks");
        var paged = await Ok(await team.Anna.GetAsync("/api/tasks?scope=mine&pageSize=50"), "my tasks, paged");
        var stats = await Ok(await team.Anna.GetAsync("/api/tasks/stats?scope=mine"), "my figures");

        Titles(mine).Should().BeEquivalentTo("Marc gives Anna", "Anna's own, unassigned");
        everyone.GetArrayLength().Should().Be(5, "without a scope the list is the team's, as it always was");
        Titles(paged.GetProperty("items")).Should().BeEquivalentTo("Marc gives Anna", "Anna's own, unassigned");
        paged.GetProperty("totalCount").GetInt32().Should().Be(2);
        stats.GetProperty("total").GetInt32().Should().Be(2);
        (await Ok(await team.Anna.GetAsync("/api/tasks/stats"), "team figures")).GetProperty("total").GetInt32().Should().Be(5);
    }

    [Fact]
    public async Task MyWorkQueueHoldsMyOpenTasksLeadsAndDealsAndNothingFinished()
    {
        using var team = await TeamAsync();
        await TaskAsync(team.Marc, "Call the Müllers", team.AnnaId);
        await TaskAsync(team.Anna, "Sent the welcome pack", team.AnnaId, status: "completed");
        await TaskAsync(team.Marc, "Luca's call", team.LucaId);

        var given = await LeadAsync(team.Marc, "Given Lead");
        await Ok(await team.Marc.PutAsync($"/api/leads/{given}/assign", Body(new { assignedToUserId = team.AnnaId })), "give Anna a lead");
        await LeadAsync(team.Anna, "Own Lead");
        var converted = await LeadAsync(team.Anna, "Converted Lead");
        await LeadAsync(team.Marc, "Marc Lead");
        using (var scope = team.Factory.Services.CreateScope())
        {
            using var db = scope.ServiceProvider.EveryWorkspace();
            db.Leads.Single(l => l.Id == converted).IsConverted = true;
            await db.SaveChangesAsync();
        }

        await DealAsync(team.Marc, "Open deal", team.AnnaId);
        var won = await DealAsync(team.Anna, "Won deal");
        var lostNoReason = await DealAsync(team.Anna, "Lost, no reason yet");
        var lostWithReason = await DealAsync(team.Anna, "Lost, reason recorded");
        await DealAsync(team.Luca, "Luca's deal");
        await Ok(await team.Anna.PutAsync($"/api/deals/{won}", Body(new { isWon = true })), "win one");
        await Ok(await team.Anna.PutAsync($"/api/deals/{lostNoReason}", Body(new { isWon = false })), "lose one");
        await Ok(await team.Anna.PutAsync($"/api/deals/{lostWithReason}", Body(new { isWon = false, closedReason = "Went with another club" })), "lose one with a reason");

        var queue = await Ok(await team.Anna.GetAsync("/api/work-queue"), "my work");

        queue.GetProperty("scope").GetString().Should().Be("mine", "the queue is the rep's own unless the team is asked for");
        Titles(queue.GetProperty("tasks")).Should().Equal("Call the Müllers");
        Titles(queue.GetProperty("leads"), "name").Should().BeEquivalentTo("Given Lead", "Own Lead");
        Titles(queue.GetProperty("deals"), "name").Should().BeEquivalentTo("Open deal", "Lost, no reason yet");
        queue.GetProperty("truncated").GetBoolean().Should().BeFalse();

        var teamQueue = await Ok(await team.Anna.GetAsync("/api/work-queue?scope=team"), "the team's work");
        teamQueue.GetProperty("scope").GetString().Should().Be("team");
        Titles(teamQueue.GetProperty("tasks")).Should().BeEquivalentTo("Call the Müllers", "Luca's call");
        Titles(teamQueue.GetProperty("leads"), "name").Should().BeEquivalentTo("Given Lead", "Own Lead", "Marc Lead");
        Titles(teamQueue.GetProperty("deals"), "name").Should().BeEquivalentTo("Open deal", "Lost, no reason yet", "Luca's deal");
    }

    [Fact]
    public async Task TheTeamsWorkLeavesOutPrivateDealsICannotOpen()
    {
        using var team = await TeamAsync();
        var secret = await DealAsync(team.Marc, "Board member's renewal");
        await Ok(await team.Marc.PutAsync($"/api/deals/{secret}", Body(new { isPrivate = true })), "make it private");

        var queue = await Ok(await team.Anna.GetAsync("/api/work-queue?scope=team"), "the team's work");

        Titles(queue.GetProperty("deals"), "name").Should().NotContain("Board member's renewal");
    }

    [Fact]
    public async Task AListCutShortSaysSo()
    {
        using var team = await TeamAsync();
        await TaskAsync(team.Anna, "First");
        await TaskAsync(team.Anna, "Second");

        var queue = await Ok(await team.Anna.GetAsync("/api/work-queue?limit=1"), "my work, one of each");

        queue.GetProperty("tasks").GetArrayLength().Should().Be(1);
        queue.GetProperty("truncated").GetBoolean().Should().BeTrue();
    }

    [SqlServerFact]
    public async Task TheWorkQueueReadsOnSqlServer()
    {
        // The open-work filters and their order are SQL there: a lost deal's blank reason,
        // a close date or due date that may be missing, a lead never touched since made.
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (rep, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            await TaskAsync(rep, "Open task");
            await TaskAsync(rep, "Done task", status: "completed");
            await LeadAsync(rep, "Open Lead");
            await DealAsync(rep, "Open deal");
            var lost = await DealAsync(rep, "Lost with a blank reason");
            await Ok(await rep.PutAsync($"/api/deals/{lost}", Body(new { isWon = false, closedReason = "   " })), "lose it");

            var queue = await Ok(await rep.GetAsync("/api/work-queue?scope=mine&limit=10"), "my work");

            Titles(queue.GetProperty("tasks")).Should().Equal("Open task");
            Titles(queue.GetProperty("leads"), "name").Should().Equal("Open Lead");
            Titles(queue.GetProperty("deals"), "name").Should().BeEquivalentTo("Open deal", "Lost with a blank reason");
            (await Ok(await rep.GetAsync("/api/tasks/all?scope=mine"), "my tasks")).GetArrayLength().Should().Be(2);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task InAPersonalWorkspaceMyWorkIsAllOfIt()
    {
        using var factory = new CustomWebApplicationFactory();
        var (solo, _) = await SignInAsync(factory, "solo@example.ch");
        await TaskAsync(solo, "Solo task");
        await LeadAsync(solo, "Solo Lead");

        var queue = await Ok(await solo.GetAsync("/api/work-queue"), "my work");

        Titles(queue.GetProperty("tasks")).Should().Equal("Solo task");
        Titles(queue.GetProperty("leads"), "name").Should().Equal("Solo Lead");
    }
}
