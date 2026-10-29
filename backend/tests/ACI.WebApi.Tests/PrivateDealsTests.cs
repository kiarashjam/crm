using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A private deal is hidden from the other salespeople everywhere, not only in the
/// deal list, and saved forecasts — which count private deals — are a manager's.
/// </summary>
public class PrivateDealsTests
{
    private sealed record Team(CustomWebApplicationFactory Factory, HttpClient Owner, Guid OwnerId, HttpClient Rep, Guid RepId, Guid Org);

    private static async Task<Team> TeamAsync(CustomWebApplicationFactory factory)
    {
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        var (rep, repId) = await SignInAsync(factory, "rep@example.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        rep.In(org);
        return new Team(factory, owner, ownerId, rep, repId, org);
    }

    private static async Task<Guid> DealAsync(Team team, bool isPrivate, string value, Guid? companyId = null)
    {
        using var scope = team.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deal = new Deal
        {
            Id = Guid.NewGuid(), UserId = team.OwnerId, OrganizationId = team.Org, AssigneeId = team.OwnerId, Name = isPrivate ? "Secret" : "Open",
            Value = value, Currency = "CHF", Probability = 50, IsPrivate = isPrivate, CompanyId = companyId, CreatedAtUtc = DateTime.UtcNow,
        };
        db.Deals.Add(deal);
        await db.SaveChangesAsync();
        return deal.Id;
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static decimal PipelineTotal(JsonElement rows) => rows.EnumerateArray().Sum(r => r.GetProperty("value").GetDecimal());

    [Fact]
    public async Task ReportsAndCompanyTotalsLeaveOutWhatTheRepCannotSee()
    {
        using var factory = new CustomWebApplicationFactory();
        var team = await TeamAsync(factory);
        Guid companyId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var company = new Company { Id = Guid.NewGuid(), Name = "Acme", UserId = team.OwnerId, OrganizationId = team.Org, CreatedAtUtc = DateTime.UtcNow };
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            companyId = company.Id;
        }
        await DealAsync(team, isPrivate: false, "1000", companyId);
        await DealAsync(team, isPrivate: true, "50000", companyId);

        PipelineTotal(await Json(await team.Owner.GetAsync("/api/reporting/pipeline-by-assignee"), "owner's report")).Should().Be(51000);
        PipelineTotal(await Json(await team.Rep.GetAsync("/api/reporting/pipeline-by-assignee"), "rep's report"))
            .Should().Be(1000, "the report read deals directly and counted the private one");
        var repCompany = (await Json(await team.Rep.GetAsync("/api/companies/stats"), "rep's company stats"))[0];
        repCompany.GetProperty("dealCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task WhatHangsOffAPrivateDealIsAsHiddenAsTheDeal()
    {
        using var factory = new CustomWebApplicationFactory();
        var team = await TeamAsync(factory);
        var deal = await DealAsync(team, isPrivate: true, "50000");
        await Json(await team.Owner.PostAsync($"/api/deals/{deal}/line-items", Body(new { name = "Plan", quantity = 1, unitPrice = 50000 })), "owner adds a line");

        (await team.Rep.GetAsync($"/api/deals/{deal}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await team.Rep.GetAsync($"/api/deals/{deal}/line-items")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await team.Rep.GetAsync($"/api/attachments?entityType=deal&recordId={deal}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await team.Rep.GetAsync($"/api/custom-fields/values?entityType=deal&recordId={deal}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ItsTasksAndActivitiesAreAsHiddenAsTheDeal()
    {
        // They carry the deal's name and what was said, and the rep's Activities,
        // Tasks, Recent Activity and Reports chart all listed them.
        using var factory = new CustomWebApplicationFactory();
        var team = await TeamAsync(factory);
        var deal = await DealAsync(team, isPrivate: true, "50000");
        var lead = (await Json(await team.Owner.PostAsync("/api/leads", Body(new { name = "Müller family", email = "mueller@example.ch", status = "New" })), "owner adds a lead"))
            .GetProperty("id").GetGuid();
        var activity = (await Json(await team.Owner.PostAsync("/api/activities", Body(new { type = "call", subject = "Agreed 20% discount", dealId = deal, leadId = lead })), "owner logs a call"))
            .GetProperty("id").GetGuid();
        var task = (await Json(await team.Owner.PostAsync("/api/tasks", Body(new { title = "Send the Müller offer", dealId = deal })), "owner adds a task"))
            .GetProperty("id").GetGuid();

        (await Json(await team.Rep.GetAsync($"/api/activities/deal/{deal}"), "rep's deal activities")).GetArrayLength().Should().Be(0);
        (await Json(await team.Rep.GetAsync($"/api/activities/lead/{lead}"), "rep's lead activities")).GetArrayLength().Should().Be(0);
        (await Json(await team.Rep.GetAsync($"/api/tasks/by-deal/{deal}"), "rep's deal tasks")).GetArrayLength().Should().Be(0);
        foreach (var list in new[] { "/api/activities", "/api/activities/all", "/api/tasks", "/api/tasks/all", "/api/search?q=offer", "/api/search?q=discount" })
        {
            var body = await (await team.Rep.GetAsync(list)).Content.ReadAsStringAsync();
            body.Should().NotContain("Agreed 20% discount", list).And.NotContain("Send the Müller offer", list).And.NotContain("Secret", list);
        }
        (await team.Rep.GetAsync($"/api/tasks/{task}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await team.Rep.GetAsync($"/api/tasks/{task}/comments")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await team.Rep.PutAsync($"/api/tasks/{task}", Body(new { title = "Mine now" }))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await team.Rep.DeleteAsync($"/api/activities/{activity}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await Json(await team.Owner.GetAsync($"/api/activities/deal/{deal}"), "owner's deal activities")).GetArrayLength().Should().Be(1);
        (await Json(await team.Owner.GetAsync($"/api/tasks/by-deal/{deal}"), "owner's deal tasks")).GetArrayLength().Should().Be(1);
        (await Json(await team.Owner.GetAsync($"/api/tasks/{task}"), "owner's task")).GetProperty("dealName").GetString().Should().Be("Secret");
    }

    [Fact]
    public async Task OnlyTheCreatorOwnerOrAManagerMakesADealPrivate()
    {
        using var factory = new CustomWebApplicationFactory();
        var team = await TeamAsync(factory);
        var deal = await DealAsync(team, isPrivate: false, "1000");

        var refused = await team.Rep.PutAsync($"/api/deals/{deal}", Body(new { isPrivate = true }));
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden, "a rep could otherwise hide a colleague's deal from the team");
        (await Json(await team.Rep.PutAsync($"/api/deals/{deal}", Body(new { description = "Called them" })), "other edits still work"))
            .GetProperty("isPrivate").GetBoolean().Should().BeFalse();
        (await Json(await team.Owner.PutAsync($"/api/deals/{deal}", Body(new { isPrivate = true })), "the owner can"))
            .GetProperty("isPrivate").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task SavedForecastsAreAManagers()
    {
        using var factory = new CustomWebApplicationFactory();
        var team = await TeamAsync(factory);
        await DealAsync(team, isPrivate: true, "50000");

        (await team.Rep.PostAsync("/api/reporting/forecast-snapshot", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var saved = await Json(await team.Owner.PostAsync("/api/reporting/forecast-snapshot", null), "owner saves");
        saved[0].GetProperty("openValue").GetDecimal().Should().Be(50000);
        var refused = await team.Rep.GetAsync("/api/reporting/forecast-snapshot");
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden, "the saved rows carry the private deal's amount and owner");
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Only the owner or a manager can see saved forecasts.");
    }

    [Fact]
    public async Task PersonalForecastsAreEachPersonsOwn()
    {
        // With no organization, "OrganizationId IS NULL" was everyone's: saving one
        // person's forecast deleted every other personal user's for the month.
        using var factory = new CustomWebApplicationFactory();
        var (first, firstId) = await SignInAsync(factory, "first@example.ch");
        var (second, _) = await SignInAsync(factory, "second@example.ch");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Deals.Add(new Deal { Id = Guid.NewGuid(), UserId = firstId, Name = "Mine", Value = "700", Currency = "CHF", Probability = 50, CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        await Json(await first.PostAsync("/api/reporting/forecast-snapshot", null), "first saves");
        await Json(await second.PostAsync("/api/reporting/forecast-snapshot", null), "second saves");

        (await Json(await first.GetAsync("/api/reporting/forecast-snapshot"), "first reads"))[0]
            .GetProperty("openValue").GetDecimal().Should().Be(700);
        (await Json(await second.GetAsync("/api/reporting/forecast-snapshot"), "second reads")).GetArrayLength().Should().Be(0);
    }
}
