using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The Reports page's figures, computed on the server for a period, a scope, a
/// pipeline and a currency.
/// </summary>
public class ReportSummaryTests
{
    private static readonly DateTime October = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime November = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
    private const string OctoberQuery = "from=2026-10-01T00:00:00Z&to=2026-11-01T00:00:00Z";

    private sealed record Org(CustomWebApplicationFactory Factory, HttpClient Owner, Guid OwnerId, HttpClient Rep, Guid RepId, Guid Id);

    private static async Task<Org> OrgAsync(CustomWebApplicationFactory factory)
    {
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        var (rep, repId) = await SignInAsync(factory, "rep@example.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        rep.In(org);
        return new Org(factory, owner, ownerId, rep, repId, org);
    }

    private static async Task SeedAsync(Org org, Action<AppDbContext> seed)
    {
        using var scope = org.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }

    private static Deal Deal(Org org, string value, string currency, Guid? assignee = null, bool? won = null,
        DateTime? closedAt = null, DateTime? created = null, int? probability = null, DateTime? closes = null,
        bool isPrivate = false, Guid? pipelineId = null, Guid? stageId = null) => new()
    {
        Id = Guid.NewGuid(), UserId = org.OwnerId, OrganizationId = org.Id, AssigneeId = assignee, Name = "Deal",
        Value = value, Currency = currency, IsWon = won, ClosedAtUtc = closedAt, CreatedAtUtc = created ?? October.AddDays(-60),
        Probability = probability, ExpectedCloseDateUtc = closes, IsPrivate = isPrivate, PipelineId = pipelineId, DealStageId = stageId,
    };

    private static async Task<JsonElement> GetJson(HttpClient client, string url)
    {
        var res = await client.GetAsync(url);
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{url}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static decimal ValueIn(JsonElement figures, string currency) =>
        figures.GetProperty("byCurrency").EnumerateArray()
            .Where(r => r.GetProperty("currency").GetString() == currency)
            .Sum(r => r.GetProperty("value").GetDecimal());

    [Fact]
    public async Task WonAndLostCountOnlyInThePeriodTheyClosedIn()
    {
        using var factory = new CustomWebApplicationFactory();
        var org = await OrgAsync(factory);
        await SeedAsync(org, db => db.Deals.AddRange(
            Deal(org, "10000", "CHF", won: true, closedAt: October.AddDays(5), created: October.AddDays(-15)),
            Deal(org, "30000", "CHF", won: true, closedAt: October.AddDays(-3)),
            Deal(org, "5000", "CHF", won: false, closedAt: October.AddDays(20)),
            Deal(org, "7000", "CHF", created: October.AddDays(2)),
            Deal(org, "2000", "CHF")));

        var summary = await GetJson(org.Owner, $"/api/reporting/summary?{OctoberQuery}");

        summary.GetProperty("won").GetProperty("count").GetInt32().Should().Be(1, "the September win is not October's");
        ValueIn(summary.GetProperty("won"), "CHF").Should().Be(10000m);
        summary.GetProperty("lost").GetProperty("count").GetInt32().Should().Be(1);
        summary.GetProperty("winRate").GetDouble().Should().Be(0.5);
        summary.GetProperty("averageSalesCycleDays").GetDouble().Should().Be(20);
        summary.GetProperty("created").GetProperty("count").GetInt32().Should().Be(1);
        // Open is a state, not an event: both open deals count whatever the period.
        summary.GetProperty("open").GetProperty("count").GetInt32().Should().Be(2);
        ValueIn(summary.GetProperty("open"), "CHF").Should().Be(9000m);
    }

    [Fact]
    public async Task MoneyIsNeverAddedAcrossCurrenciesAndTheHeadlineFollowsTheOpenPipeline()
    {
        // Two years of won EUR deals and a live CHF pipeline: picking the headline from
        // every deal made the page read "€0 open" while CHF 300,000 was in play.
        using var factory = new CustomWebApplicationFactory();
        var org = await OrgAsync(factory);
        await SeedAsync(org, db => db.Deals.AddRange(
            Deal(org, "400000", "EUR", won: true, closedAt: October.AddDays(-200)),
            Deal(org, "300000", "CHF", probability: 50),
            Deal(org, "1000", "EUR", probability: 10),
            Deal(org, "to be agreed", "CHF")));

        var summary = await GetJson(org.Owner, $"/api/reporting/summary?{OctoberQuery}");

        summary.GetProperty("headlineCurrency").GetString().Should().Be("CHF");
        var open = summary.GetProperty("open");
        ValueIn(open, "CHF").Should().Be(300000m);
        ValueIn(open, "EUR").Should().Be(1000m);
        open.GetProperty("byCurrency").EnumerateArray().Single(r => r.GetProperty("currency").GetString() == "CHF")
            .GetProperty("unreadable").GetInt32().Should().Be(1);
        var weighted = summary.GetProperty("weighted").EnumerateArray().ToDictionary(r => r.GetProperty("currency").GetString()!);
        weighted["CHF"].GetProperty("value").GetDecimal().Should().Be(150000m);
        weighted["CHF"].GetProperty("missing").GetInt32().Should().Be(1, "the deal with no probability is left out, not given one");
        weighted["EUR"].GetProperty("value").GetDecimal().Should().Be(100m);

        var chfOnly = await GetJson(org.Owner, $"/api/reporting/summary?{OctoberQuery}&currency=chf");
        chfOnly.GetProperty("open").GetProperty("count").GetInt32().Should().Be(2);
        chfOnly.GetProperty("open").GetProperty("byCurrency").GetArrayLength().Should().Be(1);
        chfOnly.GetProperty("currencies").EnumerateArray().Select(c => c.GetString()).Should().BeEquivalentTo(new[] { "CHF", "EUR" },
            "the picker still offers every currency");
    }

    [Fact]
    public async Task MeAndTeamNarrowToWhoseDealsTheyAre()
    {
        using var factory = new CustomWebApplicationFactory();
        var org = await OrgAsync(factory);
        var (_, otherId) = await SignInAsync(factory, "other@example.ch");
        await AddMemberAsync(factory, org.Id, otherId, OrgMemberRole.Member);
        var teamId = Guid.NewGuid();
        await SeedAsync(org, db =>
        {
            db.SalesTeams.Add(new SalesTeam { Id = teamId, OrganizationId = org.Id, Name = "Geneva", CreatedAtUtc = DateTime.UtcNow });
            db.SalesTeamMembers.Add(new SalesTeamMember { TeamId = teamId, UserId = org.RepId });
            db.SalesTeamMembers.Add(new SalesTeamMember { TeamId = teamId, UserId = otherId });
            db.Deals.AddRange(
                Deal(org, "100", "CHF", assignee: org.RepId),
                Deal(org, "200", "CHF", assignee: otherId),
                Deal(org, "400", "CHF", assignee: org.OwnerId));
            db.Leads.Add(new Lead { Id = Guid.NewGuid(), UserId = org.OwnerId, OrganizationId = org.Id, AssignedToUserId = org.RepId, Name = "L", Email = "l@example.ch", CreatedAtUtc = October.AddDays(1) });
        });

        ValueIn((await GetJson(org.Rep, $"/api/reporting/summary?{OctoberQuery}&scope=me")).GetProperty("open"), "CHF").Should().Be(100m);
        var team = await GetJson(org.Rep, $"/api/reporting/summary?{OctoberQuery}&scope=team");
        ValueIn(team.GetProperty("open"), "CHF").Should().Be(300m, "the rep's own team, picked because none was named");
        team.GetProperty("period").GetProperty("teamName").GetString().Should().Be("Geneva");
        ValueIn((await GetJson(org.Rep, $"/api/reporting/summary?{OctoberQuery}&scope=org")).GetProperty("open"), "CHF").Should().Be(700m);
        (await GetJson(org.Rep, $"/api/reporting/summary?{OctoberQuery}&scope=me")).GetProperty("leads").GetProperty("created").GetInt32().Should().Be(1);
        (await GetJson(org.Owner, $"/api/reporting/summary?{OctoberQuery}&scope=me")).GetProperty("leads").GetProperty("created").GetInt32()
            .Should().Be(0, "the owner created the lead but it is assigned to the rep");

        (await org.Owner.GetAsync($"/api/reporting/summary?{OctoberQuery}&scope=team")).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "the owner is in no team and named none");
        (await org.Owner.GetAsync($"/api/reporting/summary?{OctoberQuery}&scope=team&teamId={Guid.NewGuid()}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await org.Owner.GetAsync($"/api/reporting/summary?{OctoberQuery}&scope=everyone")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await org.Owner.GetAsync("/api/reporting/summary?from=2026-11-01T00:00:00Z&to=2026-10-01T00:00:00Z")).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PrivateDealsAndWhatWasLoggedOnThemStayHiddenFromTheRep()
    {
        using var factory = new CustomWebApplicationFactory();
        var org = await OrgAsync(factory);
        var secret = Deal(org, "50000", "CHF", assignee: org.OwnerId, isPrivate: true);
        await SeedAsync(org, db =>
        {
            db.Deals.AddRange(secret, Deal(org, "1000", "CHF", assignee: org.OwnerId));
            db.Activities.Add(new Activity { Id = Guid.NewGuid(), UserId = org.OwnerId, OrganizationId = org.Id, Type = "call", DealId = secret.Id, CreatedAtUtc = October.AddDays(3) });
            db.Activities.Add(new Activity { Id = Guid.NewGuid(), UserId = org.OwnerId, OrganizationId = org.Id, Type = "call", CreatedAtUtc = October.AddDays(3) });
        });

        var rep = await GetJson(org.Rep, $"/api/reporting/summary?{OctoberQuery}");
        ValueIn(rep.GetProperty("open"), "CHF").Should().Be(1000m);
        rep.GetProperty("byOwner").EnumerateArray().Sum(r => r.GetProperty("openValue").GetDecimal()).Should().Be(1000m);
        rep.GetProperty("activities").GetProperty("total").GetInt32().Should().Be(1);

        var owner = await GetJson(org.Owner, $"/api/reporting/summary?{OctoberQuery}");
        ValueIn(owner.GetProperty("open"), "CHF").Should().Be(51000m);
        owner.GetProperty("activities").GetProperty("total").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task APipelineNarrowsTheDealsAndItsStagesComeInTheirOrder()
    {
        using var factory = new CustomWebApplicationFactory();
        var org = await OrgAsync(factory);
        var sales = Guid.NewGuid();
        var other = Guid.NewGuid();
        var proposal = Guid.NewGuid();
        var discovery = Guid.NewGuid();
        await SeedAsync(org, db =>
        {
            db.Pipelines.Add(new Pipeline { Id = sales, OrganizationId = org.Id, Name = "Memberships", DisplayOrder = 0 });
            db.Pipelines.Add(new Pipeline { Id = other, OrganizationId = org.Id, Name = "Events", DisplayOrder = 1 });
            db.DealStages.Add(new DealStage { Id = proposal, PipelineId = sales, Name = "Proposal", DisplayOrder = 2 });
            db.DealStages.Add(new DealStage { Id = discovery, PipelineId = sales, Name = "Discovery", DisplayOrder = 1 });
            db.Deals.AddRange(
                Deal(org, "100", "CHF", pipelineId: sales, stageId: proposal),
                Deal(org, "200", "CHF", pipelineId: sales, stageId: discovery),
                Deal(org, "999", "CHF", pipelineId: other));
        });

        var summary = await GetJson(org.Owner, $"/api/reporting/summary?{OctoberQuery}&pipelineId={sales}");

        ValueIn(summary.GetProperty("open"), "CHF").Should().Be(300m);
        summary.GetProperty("byStage").EnumerateArray().Select(r => r.GetProperty("stageName").GetString())
            .Should().Equal(new[] { "Discovery", "Proposal" }, "a funnel reads in stage order, not alphabetically by luck");
        (await org.Owner.GetAsync($"/api/reporting/summary?{OctoberQuery}&pipelineId={Guid.NewGuid()}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A stage's default probability weights the deals in it that have none of their own,
    /// as the board weighs them; a deal's own probability still comes first.
    /// </summary>
    [Fact]
    public async Task ADealWithoutAProbabilityIsWeightedByItsStages()
    {
        using var factory = new CustomWebApplicationFactory();
        var org = await OrgAsync(factory);
        var sales = Guid.NewGuid();
        var tour = Guid.NewGuid();
        var unsure = Guid.NewGuid();
        var month = new DateTime(WorkspaceToday.Year, WorkspaceToday.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        await SeedAsync(org, db =>
        {
            db.Pipelines.Add(new Pipeline { Id = sales, OrganizationId = org.Id, Name = "Memberships" });
            db.DealStages.Add(new DealStage { Id = tour, PipelineId = sales, Name = "Tour", DisplayOrder = 1, Probability = 40 });
            db.DealStages.Add(new DealStage { Id = unsure, PipelineId = sales, Name = "Unsure", DisplayOrder = 2 });
            db.Deals.AddRange(
                Deal(org, "1000", "CHF", pipelineId: sales, stageId: tour, closes: month.AddMonths(1).AddDays(-1)),
                Deal(org, "2000", "CHF", pipelineId: sales, stageId: tour, probability: 10, closes: month.AddMonths(1).AddDays(-1)),
                Deal(org, "4000", "CHF", pipelineId: sales, stageId: unsure, closes: month.AddMonths(1).AddDays(-1)));
        });

        var summary = await GetJson(org.Owner, $"/api/reporting/summary?{OctoberQuery}");
        var weighted = summary.GetProperty("weighted").EnumerateArray().Single(r => r.GetProperty("currency").GetString() == "CHF");
        weighted.GetProperty("value").GetDecimal().Should().Be(600m, "1000 at the stage's 40%, and 2000 at its own 10%");
        weighted.GetProperty("covered").GetInt32().Should().Be(2);
        weighted.GetProperty("missing").GetInt32().Should().Be(1, "neither the deal nor its stage has a probability");
        summary.GetProperty("byStage").EnumerateArray().Single(r => r.GetProperty("stageName").GetString() == "Tour")
            .GetProperty("weighted").GetDecimal().Should().Be(600m);

        var forecast = await GetJson(org.Owner, $"/api/reporting/forecast?from={month:yyyy-MM}&months=1");
        forecast.GetProperty("rows")[0].GetProperty("weighted").GetDecimal().Should().Be(600m, "the forecast weighs deals the same way");
    }

    [Fact]
    public async Task LeadsAreCountedByTheirCohortAndActiveLeadsLeaveOutConvertedOnes()
    {
        using var factory = new CustomWebApplicationFactory();
        var org = await OrgAsync(factory);
        Lead L(string status, DateTime created, bool converted = false, DateTime? convertedAt = null, int? score = null, Guid? deal = null) => new()
        {
            Id = Guid.NewGuid(), UserId = org.OwnerId, OrganizationId = org.Id, Name = status, Email = $"{Guid.NewGuid():N}@example.ch",
            Status = status, Source = "website", CreatedAtUtc = created, IsConverted = converted, ConvertedAtUtc = convertedAt,
            LeadScore = score, ConvertedToDealId = deal,
        };
        await SeedAsync(org, db => db.Leads.AddRange(
            L("New", October.AddDays(1), score: 40),
            L("Qualified", October.AddDays(2), score: 60),
            L("Won", October.AddDays(3), converted: true, convertedAt: October.AddDays(9), deal: Guid.NewGuid()),
            L("Won", October.AddDays(-40), converted: true, convertedAt: October.AddDays(4)),
            L("New", October.AddDays(-90))));

        // October in UTC, counted in UTC: without a zone the workspace's (Zurich) is used,
        // whose days do not line up with this period's edges.
        var leads = (await GetJson(org.Owner, $"/api/reporting/summary?{OctoberQuery}&tz=UTC")).GetProperty("leads");

        leads.GetProperty("active").GetInt32().Should().Be(3);
        leads.GetProperty("created").GetInt32().Should().Be(3);
        leads.GetProperty("createdConverted").GetInt32().Should().Be(1);
        leads.GetProperty("converted").GetInt32().Should().Be(2, "converted in October, whenever they came in");
        leads.GetProperty("convertedToDeal").GetInt32().Should().Be(1);
        leads.GetProperty("averageScore").GetDouble().Should().Be(50);
        leads.GetProperty("scored").GetInt32().Should().Be(2);
        leads.GetProperty("bySource")[0].GetProperty("count").GetInt32().Should().Be(3);
        leads.GetProperty("createdSeries").GetArrayLength().Should().Be(31, "one bucket per October day, empty ones included");
    }

    [Fact]
    public async Task DealsClosingInThePeriodAreMatchedByTheirDayOnTheViewersCalendar()
    {
        // A close date is a day, stored as its UTC midnight. New York's October runs
        // from 04:00 UTC on the 1st to 04:00 UTC on 1 November, so compared as instants
        // the deal due on the 1st was left out and the one due on 1 November counted.
        using var factory = new CustomWebApplicationFactory();
        var org = await OrgAsync(factory);
        await SeedAsync(org, db => db.Deals.AddRange(
            Deal(org, "100", "CHF", closes: October),
            Deal(org, "200", "CHF", closes: November.AddDays(-1)),
            Deal(org, "400", "CHF", closes: November),
            Deal(org, "800", "CHF", closes: October.AddDays(-1))));

        var newYork = await GetJson(org.Owner,
            "/api/reporting/summary?from=2026-10-01T04:00:00Z&to=2026-11-01T04:00:00Z&tz=America/New_York");
        ValueIn(newYork.GetProperty("closingInPeriod"), "CHF").Should().Be(300m, "the deals due on 1 and 31 October");

        var zurich = await GetJson(org.Owner,
            "/api/reporting/summary?from=2026-09-30T22:00:00Z&to=2026-10-31T23:00:00Z&tz=Europe/Zurich");
        ValueIn(zurich.GetProperty("closingInPeriod"), "CHF").Should().Be(300m);
    }

    [Fact]
    public async Task ActivityIsCountedOnTheViewersCalendarDay()
    {
        // 23:30 UTC on 10 October is half past one on the 11th in Zurich.
        using var factory = new CustomWebApplicationFactory();
        var org = await OrgAsync(factory);
        await SeedAsync(org, db =>
        {
            db.Activities.Add(new Activity { Id = Guid.NewGuid(), UserId = org.OwnerId, OrganizationId = org.Id, Type = "Call", CreatedAtUtc = new DateTime(2026, 10, 10, 23, 30, 0, DateTimeKind.Utc) });
            db.Activities.Add(new Activity { Id = Guid.NewGuid(), UserId = org.OwnerId, OrganizationId = org.Id, Type = "system", CreatedAtUtc = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc) });
        });

        var zurich = (await GetJson(org.Owner, "/api/reporting/summary?from=2026-09-30T22:00:00Z&to=2026-10-31T23:00:00Z&tz=Europe/Zurich"))
            .GetProperty("activities");

        zurich.GetProperty("total").GetInt32().Should().Be(1, "the app's own system lines are not work anyone did");
        zurich.GetProperty("byType")[0].GetProperty("key").GetString().Should().Be("call");
        var days = zurich.GetProperty("series").EnumerateArray().ToDictionary(p => p.GetProperty("start").GetString()!, p => p.GetProperty("count").GetInt32());
        days.Should().ContainKey("2026-10-01").And.ContainKey("2026-10-31");
        days["2026-10-11"].Should().Be(1);
        days["2026-10-10"].Should().Be(0);
    }

    [Fact]
    public async Task WithNoZoneNamedTheDaysAreTheWorkspacesOwn()
    {
        // 23:30 UTC on 10 October is the 11th in Zurich and still the 10th in New York.
        // With no zone in the request the days were UTC's.
        using var factory = new CustomWebApplicationFactory();
        var org = await OrgAsync(factory);
        await SeedAsync(org, db => db.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(), UserId = org.OwnerId, OrganizationId = org.Id, Type = "call",
            CreatedAtUtc = new DateTime(2026, 10, 10, 23, 30, 0, DateTimeKind.Utc),
        }));
        Dictionary<string, int> Days(JsonElement summary) => summary.GetProperty("activities").GetProperty("series").EnumerateArray()
            .ToDictionary(p => p.GetProperty("start").GetString()!, p => p.GetProperty("count").GetInt32());

        var zurich = await GetJson(org.Owner, "/api/reporting/summary?from=2026-09-30T22:00:00Z&to=2026-10-31T23:00:00Z");
        zurich.GetProperty("period").GetProperty("timeZone").GetString().Should().Be("Europe/Zurich");
        Days(zurich).Keys.First().Should().Be("2026-10-01", "Zurich's October, not UTC's 30 September");
        Days(zurich)["2026-10-11"].Should().Be(1);

        (await org.Owner.PutAsync($"/api/organizations/{org.Id}/settings", Body(new { timeZone = "America/New_York" })))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        var newYork = await GetJson(org.Owner, "/api/reporting/summary?from=2026-10-01T04:00:00Z&to=2026-11-01T04:00:00Z");
        newYork.GetProperty("period").GetProperty("timeZone").GetString().Should().Be("America/New_York");
        Days(newYork)["2026-10-10"].Should().Be(1);
    }

    [Fact]
    public async Task AWeeklyChartKeepsWhatHappenedInThePeriodsFirstHour()
    {
        // Twelve weeks from Monday 3 August in Zurich, which starts at 22:00 UTC on
        // Sunday the 2nd. Weekly charts count by UTC day, and a call at half past
        // midnight on the Monday fell in the week of 27 July, which is not drawn.
        using var factory = new CustomWebApplicationFactory();
        var org = await OrgAsync(factory);
        await SeedAsync(org, db => db.Activities.Add(new Activity
        {
            Id = Guid.NewGuid(), UserId = org.OwnerId, OrganizationId = org.Id, Type = "call",
            CreatedAtUtc = new DateTime(2026, 8, 2, 22, 30, 0, DateTimeKind.Utc),
        }));

        var activities = (await GetJson(org.Owner, "/api/reporting/summary?from=2026-08-02T22:00:00Z&to=2026-10-25T23:00:00Z&tz=Europe/Zurich"))
            .GetProperty("activities");

        activities.GetProperty("total").GetInt32().Should().Be(1);
        var series = activities.GetProperty("series").EnumerateArray().ToList();
        series[0].GetProperty("start").GetString().Should().Be("2026-08-03");
        series[0].GetProperty("count").GetInt32().Should().Be(1, "the chart shows what the total counts");
        series.Sum(p => p.GetProperty("count").GetInt32()).Should().Be(1);
    }
}
