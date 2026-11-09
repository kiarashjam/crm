using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The board from the server: a pipeline's stages with their first cards, each stage's
/// count, money per currency, weighted value and stale deals, and the pipeline's figures.
/// </summary>
/// <remarks>
/// The board used to download every deal and work all of this out in the browser,
/// adding currencies together on the way. The same data is checked here on the
/// in-memory host and, in <see cref="TheBoardIsWorkedOutInSql"/>, on SQL Server, where
/// every query has to translate.
/// </remarks>
public class DealBoardTests
{
    /// <summary>What <see cref="SeedAsync"/> made, by name.</summary>
    private sealed record Seeded(Guid Memberships, Guid Events, Guid Enquiry, Guid Tour, Guid Committee, Guid Signed, Guid Declined, Dictionary<string, Guid> Deals);

    private static async Task<JsonElement> Json(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, body);
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static JsonElement Column(JsonElement board, string name) =>
        board.GetProperty("columns").EnumerateArray().Single(c => c.GetProperty("name").GetString() == name);

    private static decimal Total(JsonElement rows, string currency) =>
        rows.EnumerateArray().Where(r => r.GetProperty("currency").GetString() == currency).Sum(r => r.GetProperty("value").GetDecimal());

    private static JsonElement Row(JsonElement rows, string currency) =>
        rows.EnumerateArray().Single(r => r.GetProperty("currency").GetString() == currency);

    private static IEnumerable<string?> Names(JsonElement column) =>
        column.GetProperty("deals").EnumerateArray().Select(d => d.GetProperty("name").GetString());

    /// <summary>
    /// A membership pipeline with five stages and deals in every state, a second pipeline,
    /// and two deals from before deals carried stage ids.
    /// </summary>
    private static async Task<Seeded> SeedAsync(IServiceProvider services, Guid ownerId, Guid repId, Guid org)
    {
        var now = DateTime.UtcNow;
        var today = WorkspaceToday;
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var memberships = new Pipeline { Id = Guid.NewGuid(), OrganizationId = org, Name = "Memberships", DisplayOrder = 0 };
        var events = new Pipeline { Id = Guid.NewGuid(), OrganizationId = org, Name = "Events", DisplayOrder = 1 };
        DealStage Stage(Pipeline p, string name, int order, int? probability = null, bool won = false, bool lost = false) =>
            new() { Id = Guid.NewGuid(), PipelineId = p.Id, Name = name, DisplayOrder = order, Probability = probability, IsWon = won, IsLost = lost };
        var enquiry = Stage(memberships, "Enquiry", 1, probability: 10);
        var tour = Stage(memberships, "Tour", 2);
        var committee = Stage(memberships, "Committee", 3, probability: 60);
        var signed = Stage(memberships, "Signed", 4, won: true);
        var declined = Stage(memberships, "Declined", 5, lost: true);
        var proposal = Stage(events, "Proposal", 1);
        db.Pipelines.AddRange(memberships, events);
        db.DealStages.AddRange(enquiry, tour, committee, signed, declined, proposal);

        var deals = new Dictionary<string, Guid>();
        Deal Add(string name, DealStage? stage, decimal? amount, string currency = "CHF", int? probability = null, DateTime? closes = null,
            int createdDaysAgo = 0, Guid? assignee = null, bool? won = null, bool isPrivate = false, string? legacyStage = null)
        {
            var deal = new Deal
            {
                Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Name = name,
                Value = amount?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "to be agreed", Amount = amount,
                Currency = currency, Probability = probability, ExpectedCloseDateUtc = closes, AssigneeId = assignee, IsWon = won,
                IsPrivate = isPrivate, PipelineId = stage?.PipelineId, DealStageId = stage?.Id, Stage = stage?.Name ?? legacyStage,
                CreatedAtUtc = now.AddDays(-createdDaysAgo), ClosedAtUtc = won == null ? null : now.AddDays(-1),
            };
            db.Deals.Add(deal);
            deals[name] = deal.Id;
            return deal;
        }
        void Entered(Deal deal, DealStage stage, int daysAgo, DealStage? from = null) => db.DealStageChanges.Add(new DealStageChange
        {
            Id = Guid.NewGuid(), DealId = deal.Id, FromDealStageId = from?.Id, FromStageName = from?.Name,
            ToDealStageId = stage.Id, ToStageName = stage.Name, ChangedByUserId = ownerId, ChangedAtUtc = now.AddDays(-daysAgo),
        });

        var alpine = Add("Alpine Club", enquiry, 1000m, closes: today.AddDays(3), createdDaysAgo: 2, assignee: ownerId);
        Entered(alpine, enquiry, 2);
        // From before the history was kept: no line says when it came into its stage.
        Add("Bernese Bank", enquiry, 2000m, currency: "EUR", probability: 50, closes: today.AddDays(-1), createdDaysAgo: 40, assignee: repId);
        Add("Jura Foundation", enquiry, 5000m, isPrivate: true);
        var club = Add("Club dinner", tour, 500m, createdDaysAgo: 40);
        Entered(club, enquiry, 40);
        Entered(club, tour, 20, from: enquiry);
        db.Activities.Add(new Activity { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, DealId = club.Id, Type = "call", CreatedAtUtc = now.AddDays(-3) });
        Add("Karaoke night", tour, 200m);
        Add("Kayak race", tour, 800m, probability: 25, closes: today.AddDays(10));
        Add("Kirsch tasting", tour, 300m, closes: today.AddDays(5));
        Add("Dinner dance", committee, null, createdDaysAgo: 1);
        Add("Geneva Society", null, 400m, createdDaysAgo: 10, legacyStage: " committee");
        Add("Hotel group", null, 100m, createdDaysAgo: 1, legacyStage: "Whatever");
        Add("Engadin Estate", signed, 3000m, createdDaysAgo: 60, won: true);
        Add("Fondue evening", declined, 700m, won: false);
        Add("Ice gala", proposal, 999m);
        await db.SaveChangesAsync();
        return new Seeded(memberships.Id, events.Id, enquiry.Id, tour.Id, committee.Id, signed.Id, declined.Id, deals);
    }

    private static async Task<(CustomWebApplicationFactory Factory, HttpClient Owner, HttpClient Rep, Guid Org, Seeded Seeded)> BoardAsync()
    {
        var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        var (rep, repId) = await SignInAsync(factory, "rep@example.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        rep.In(org);
        var seeded = await SeedAsync(factory.Services, ownerId, repId, org);
        return (factory, owner, rep, org, seeded);
    }

    /// <summary>What the seeded board shows the owner without filters; the same on both hosts.</summary>
    private static void ShouldBeTheSeededBoard(JsonElement board, Seeded seeded)
    {
        board.GetProperty("pipelineId").GetGuid().Should().Be(seeded.Memberships, "the workspace's first pipeline, when none is named");
        board.GetProperty("staleAfterDays").GetInt32().Should().Be(30);
        board.GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("name").GetString())
            .Should().Equal("Enquiry", "Tour", "Committee", "Signed", "Declined", "No stage");

        var enquiry = Column(board, "Enquiry");
        enquiry.GetProperty("key").GetString().Should().Be(seeded.Enquiry.ToString());
        enquiry.GetProperty("count").GetInt32().Should().Be(3);
        Total(enquiry.GetProperty("totals"), "CHF").Should().Be(6000m);
        Total(enquiry.GetProperty("totals"), "EUR").Should().Be(2000m, "francs and euros are never added together");
        // Alpine and Jura have no probability of their own: the stage's 10%. Bernese has its own 50%.
        Row(enquiry.GetProperty("weighted"), "CHF").GetProperty("value").GetDecimal().Should().Be(600m);
        Row(enquiry.GetProperty("weighted"), "EUR").GetProperty("value").GetDecimal().Should().Be(1000m);
        Row(enquiry.GetProperty("weighted"), "CHF").GetProperty("missing").GetInt32().Should().Be(0);
        enquiry.GetProperty("stale").GetInt32().Should().Be(1, "Bernese Bank has had nothing for 40 days");

        var tour = Column(board, "Tour");
        tour.GetProperty("count").GetInt32().Should().Be(4);
        // The stage has no probability: only Kayak race, with its own 25%, is weighted.
        Row(tour.GetProperty("weighted"), "CHF").GetProperty("value").GetDecimal().Should().Be(200m);
        Row(tour.GetProperty("weighted"), "CHF").GetProperty("covered").GetInt32().Should().Be(1);
        Row(tour.GetProperty("weighted"), "CHF").GetProperty("missing").GetInt32().Should().Be(3);
        tour.GetProperty("stale").GetInt32().Should().Be(0, "Club dinner had a call three days ago");
        Names(tour).Should().Equal(new[] { "Kirsch tasting", "Kayak race", "Club dinner", "Karaoke night" },
            "soonest expected close first, then those without a date by name");

        var committee = Column(board, "Committee");
        Names(committee).Should().BeEquivalentTo(new[] { "Dinner dance", "Geneva Society" },
            "a deal known only by its stage's name is in that stage, whatever its case");
        Row(committee.GetProperty("totals"), "CHF").GetProperty("unreadable").GetInt32().Should().Be(1);
        Row(committee.GetProperty("weighted"), "CHF").GetProperty("value").GetDecimal().Should().Be(240m);

        Column(board, "Signed").GetProperty("weighted").GetArrayLength().Should().Be(0, "a won stage has nothing left to weigh");
        Column(board, "No stage").GetProperty("key").GetString().Should().Be("unset");
        Names(Column(board, "No stage")).Should().Equal("Hotel group");
        board.GetProperty("count").GetInt32().Should().Be(12);

        var summary = board.GetProperty("summary");
        summary.GetProperty("total").GetInt32().Should().Be(12);
        summary.GetProperty("open").GetInt32().Should().Be(10);
        summary.GetProperty("won").GetInt32().Should().Be(1);
        summary.GetProperty("lost").GetInt32().Should().Be(1);
        summary.GetProperty("winRate").GetDouble().Should().Be(0.5);
        Row(summary.GetProperty("openTotals"), "CHF").GetProperty("value").GetDecimal().Should().Be(8300m);
        Row(summary.GetProperty("openTotals"), "CHF").GetProperty("unreadable").GetInt32().Should().Be(1);
        Row(summary.GetProperty("openTotals"), "EUR").GetProperty("value").GetDecimal().Should().Be(2000m);
        Row(summary.GetProperty("wonTotals"), "CHF").GetProperty("value").GetDecimal().Should().Be(3000m);
        Row(summary.GetProperty("weighted"), "CHF").GetProperty("value").GetDecimal().Should().Be(1040m);
        Row(summary.GetProperty("weighted"), "CHF").GetProperty("missing").GetInt32().Should().Be(4);
        summary.GetProperty("overdue").GetInt32().Should().Be(1);
        summary.GetProperty("closingThisWeek").GetInt32().Should().Be(2);
        summary.GetProperty("stale").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task TheBoardHasEachStagesCardsCountsMoneyAndWeightedValue()
    {
        var (factory, owner, _, _, seeded) = await BoardAsync();
        using var _f = factory;

        ShouldBeTheSeededBoard(await Json(await owner.GetAsync("/api/deals/board")), seeded);
    }

    [Fact]
    public async Task ACardSaysHowLongItHasBeenInItsStageAndWhetherItIsStale()
    {
        var (factory, owner, _, _, seeded) = await BoardAsync();
        using var _f = factory;
        var board = await Json(await owner.GetAsync("/api/deals/board"));
        JsonElement Card(string name) => board.GetProperty("columns").EnumerateArray()
            .SelectMany(c => c.GetProperty("deals").EnumerateArray()).Single(d => d.GetProperty("name").GetString() == name);

        var club = Card("Club dinner");
        club.GetProperty("stageEnteredAtUtc").GetDateTime().Should().BeCloseTo(DateTime.UtcNow.AddDays(-20), TimeSpan.FromMinutes(5),
            "it came into Tour twenty days ago");
        club.GetProperty("idleSinceUtc").GetDateTime().Should().BeCloseTo(DateTime.UtcNow.AddDays(-3), TimeSpan.FromMinutes(5),
            "a call three days ago is the last thing that happened to it");
        club.GetProperty("isStale").GetBoolean().Should().BeFalse();

        var bernese = Card("Bernese Bank");
        bernese.GetProperty("isStale").GetBoolean().Should().BeTrue();
        bernese.GetProperty("staleAfterDays").GetInt32().Should().Be(30);
        bernese.GetProperty("stageEnteredAtUtc").ValueKind.Should().Be(JsonValueKind.Null,
            "no history says when it came into its stage, and its creation would be a guess");
        Card("Engadin Estate").GetProperty("isStale").GetBoolean().Should().BeFalse("a won deal is done, not stale");

        // The deal page and the full list say the same.
        var page = await Json(await owner.GetAsync($"/api/deals/{seeded.Deals["Bernese Bank"]}"));
        page.GetProperty("isStale").GetBoolean().Should().BeTrue();
        var all = await Json(await owner.GetAsync("/api/deals/all"));
        var listed = all.EnumerateArray().Single(d => d.GetProperty("name").GetString() == "Club dinner");
        listed.GetProperty("stageEnteredAtUtc").GetDateTime().Should().BeCloseTo(DateTime.UtcNow.AddDays(-20), TimeSpan.FromMinutes(5));
        listed.GetProperty("isStale").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task AColumnIsReadAPageAtATime()
    {
        var (factory, owner, _, _, seeded) = await BoardAsync();
        using var _f = factory;

        var board = await Json(await owner.GetAsync("/api/deals/board?take=2"));
        var tour = Column(board, "Tour");
        Names(tour).Should().Equal("Kirsch tasting", "Kayak race");
        tour.GetProperty("hasMore").GetBoolean().Should().BeTrue();
        tour.GetProperty("count").GetInt32().Should().Be(4, "the count is the column's, not the page's");

        var next = await Json(await owner.GetAsync($"/api/deals/board/column?column={seeded.Tour}&skip=2&take=2"));
        next.GetProperty("deals").EnumerateArray().Select(d => d.GetProperty("name").GetString()).Should().Equal("Club dinner", "Karaoke night");
        next.GetProperty("hasMore").GetBoolean().Should().BeFalse();

        var unset = await Json(await owner.GetAsync("/api/deals/board/column?column=unset&skip=0"));
        unset.GetProperty("deals").EnumerateArray().Select(d => d.GetProperty("name").GetString()).Should().Equal("Hotel group");

        var byValue = await Json(await owner.GetAsync($"/api/deals/board/column?column={seeded.Tour}&sort=value"));
        byValue.GetProperty("deals").EnumerateArray().Select(d => d.GetProperty("name").GetString())
            .Should().Equal("Kayak race", "Club dinner", "Kirsch tasting", "Karaoke night");
    }

    [Fact]
    public async Task FiltersNarrowTheColumnsAndLeaveThePipelineFigures()
    {
        var (factory, owner, _, _, seeded) = await BoardAsync();
        using var _f = factory;
        var today = WorkspaceToday;

        async Task<JsonElement> Board(string query) => await Json(await owner.GetAsync($"/api/deals/board?{query}"));

        var mine = await Board("mine=true");
        Names(Column(mine, "Enquiry")).Should().BeEquivalentTo(new[] { "Alpine Club", "Jura Foundation" },
            "mine is owned by me, or owned by nobody and created by me, as the reports count it");
        mine.GetProperty("summary").GetProperty("total").GetInt32().Should().Be(12, "the figures above the board are the pipeline's");
        mine.GetProperty("count").GetInt32().Should().Be(11);

        (await Board("search=karaoke")).GetProperty("count").GetInt32().Should().Be(1);
        var repId = (await Json(await owner.GetAsync($"/api/deals/{seeded.Deals["Bernese Bank"]}"))).GetProperty("assigneeId").GetGuid();
        Names(Column(await Board($"assigneeId={repId}"), "Enquiry")).Should().Equal("Bernese Bank");
        (await Board("unassigned=true")).GetProperty("count").GetInt32().Should().Be(10);
        var range = await Board("minAmount=900&maxAmount=2500");
        range.GetProperty("count").GetInt32().Should().Be(2, "Alpine Club and Bernese Bank; a deal with no amount is in no range");
        var closing = await Board($"closeFrom={today:yyyy-MM-dd}&closeTo={today.AddDays(7):yyyy-MM-dd}");
        closing.GetProperty("columns").EnumerateArray().SelectMany(c => c.GetProperty("deals").EnumerateArray())
            .Select(d => d.GetProperty("name").GetString()).Should().BeEquivalentTo("Alpine Club", "Kirsch tasting");
        var stale = await Board("staleOnly=true");
        stale.GetProperty("count").GetInt32().Should().Be(1);
        Column(stale, "Enquiry").GetProperty("stale").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task APipelineSetsItsOwnStaleDaysOrNone()
    {
        var (factory, owner, _, org, seeded) = await BoardAsync();
        using var _f = factory;

        await Json(await owner.PutAsync($"/api/pipelines/{seeded.Memberships}", Body(new { staleDealDays = 60 })));
        var slower = await Json(await owner.GetAsync("/api/deals/board"));
        slower.GetProperty("staleAfterDays").GetInt32().Should().Be(60);
        slower.GetProperty("summary").GetProperty("stale").GetInt32().Should().Be(0, "forty quiet days are not sixty");

        await Json(await owner.PutAsync($"/api/pipelines/{seeded.Memberships}", Body(new { staleDealDays = 0 })));
        (await Json(await owner.GetAsync("/api/deals/board"))).GetProperty("summary").GetProperty("stale").GetInt32().Should().Be(0);

        // Back to the workspace's, which a manager makes shorter.
        await Json(await owner.PutAsync($"/api/pipelines/{seeded.Memberships}", Body(new { followWorkspaceStaleDays = true })));
        await Json(await owner.PutAsync($"/api/organizations/{org}/settings", Body(new { staleDealDays = 9 })));
        var quicker = await Json(await owner.GetAsync("/api/deals/board"));
        quicker.GetProperty("staleAfterDays").GetInt32().Should().Be(9);
        quicker.GetProperty("summary").GetProperty("stale").GetInt32().Should().Be(2, "Bernese Bank, and Geneva Society after ten quiet days");
    }

    [Fact]
    public async Task AnotherPipelineHasItsOwnDealsOnly()
    {
        var (factory, owner, _, _, seeded) = await BoardAsync();
        using var _f = factory;

        var events = await Json(await owner.GetAsync($"/api/deals/board?pipelineId={seeded.Events}"));
        events.GetProperty("pipelineName").GetString().Should().Be("Events");
        events.GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("name").GetString()).Should().Equal("Proposal");
        Names(Column(events, "Proposal")).Should().Equal("Ice gala");
        events.GetProperty("summary").GetProperty("total").GetInt32().Should().Be(1,
            "deals without a pipeline belong to the first one, as the board has always shown them");
    }

    [Fact]
    public async Task APrivateDealIsLeftOutForWhoeverCannotSeeIt()
    {
        var (factory, _, rep, _, _) = await BoardAsync();
        using var _f = factory;

        var board = await Json(await rep.GetAsync("/api/deals/board"));
        Names(Column(board, "Enquiry")).Should().NotContain("Jura Foundation");
        Total(Column(board, "Enquiry").GetProperty("totals"), "CHF").Should().Be(1000m);
        board.GetProperty("summary").GetProperty("total").GetInt32().Should().Be(11);
    }

    [Fact]
    public async Task AWorkspaceWithoutPipelinesHasTheDefaultStagesByName()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var (name, stage) in new[] { ("Wine club", "Qualification"), ("Jazz night", "proposal "), ("Old win", "Closed Won"), ("No idea", (string?)null) })
            {
                db.Deals.Add(new Deal
                {
                    Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Name = name, Value = "100", Amount = 100m,
                    Currency = "CHF", Stage = stage, CreatedAtUtc = DateTime.UtcNow,
                });
            }
            await db.SaveChangesAsync();
        }

        var board = await Json(await owner.GetAsync("/api/deals/board"));

        board.GetProperty("pipelineId").ValueKind.Should().Be(JsonValueKind.Null);
        board.GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("key").GetString())
            .Should().Equal("Qualification", "Proposal", "Negotiation", "Contract Sent", "Contract Signed", "Closed Won", "Closed Lost", "unset");
        Names(Column(board, "Proposal")).Should().Equal("Jazz night");
        Column(board, "Closed Won").GetProperty("isWon").GetBoolean().Should().BeTrue();
        board.GetProperty("summary").GetProperty("won").GetInt32().Should().Be(1, "a deal in a won stage is won");
        Names(Column(board, "No stage")).Should().Equal("No idea");
        var more = await Json(await owner.GetAsync("/api/deals/board/column?column=proposal"));
        more.GetProperty("deals").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task WhatIsNotOnThisBoardIsRefused()
    {
        var (factory, owner, _, _, _) = await BoardAsync();
        using var _f = factory;
        var (stranger, strangerId) = await SignInAsync(factory, "stranger@example.ch");
        var theirs = await CreateAsync(factory, strangerId, "Elsewhere");
        var foreignPipeline = (await Json(await stranger.In(theirs).PostAsync("/api/pipelines", Body(new { name = "Theirs" })))).GetProperty("id").GetGuid();

        await Json(await owner.GetAsync($"/api/deals/board?pipelineId={foreignPipeline}"), HttpStatusCode.NotFound);
        await Json(await owner.GetAsync($"/api/deals/board/column?column={Guid.NewGuid()}"), HttpStatusCode.NotFound);
        await Json(await owner.GetAsync("/api/deals/board?sort=price"), HttpStatusCode.BadRequest);
    }

    /// <summary>The same board, on SQL Server: every query the board runs has to translate, and add up the same.</summary>
    [SqlServerFact]
    public async Task TheBoardIsWorkedOutInSql()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, ownerId) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var (rep, repId) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var created = await owner.PostAsync("/api/organizations", Body(new { name = "Pavillon" }));
            created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
            var org = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
            owner.In(org);
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.OrganizationMembers.Add(new OrganizationMember { OrganizationId = org, UserId = repId, Role = OrgMemberRole.Member, JoinedAtUtc = DateTime.UtcNow });
                await db.SaveChangesAsync();
            }
            rep.In(org);
            var seeded = await SeedAsync(app.Services, ownerId, repId, org);

            ShouldBeTheSeededBoard(await Json(await owner.GetAsync("/api/deals/board")), seeded);

            var today = WorkspaceToday;
            foreach (var query in new[]
            {
                "mine=true", "unassigned=true", $"assigneeId={repId}", "search=club", "minAmount=900&maxAmount=2500",
                $"closeFrom={today:yyyy-MM-dd}&closeTo={today.AddDays(7):yyyy-MM-dd}", "staleOnly=true",
                "sort=value", "sort=created", "sort=name", "take=1", $"pipelineId={seeded.Events}",
            })
            {
                var board = await Json(await owner.GetAsync($"/api/deals/board?{query}"));
                board.GetProperty("summary").GetProperty("total").GetInt32().Should().BeGreaterThan(0, query);
            }
            (await Json(await owner.GetAsync("/api/deals/board?staleOnly=true"))).GetProperty("count").GetInt32().Should().Be(1);
            var next = await Json(await owner.GetAsync($"/api/deals/board/column?column={seeded.Tour}&skip=2&take=2"));
            next.GetProperty("deals").EnumerateArray().Select(d => d.GetProperty("name").GetString()).Should().Equal("Club dinner", "Karaoke night");
            (await Json(await owner.GetAsync("/api/deals/board/column?column=unset"))).GetProperty("deals").GetArrayLength().Should().Be(1);
            (await Json(await rep.GetAsync("/api/deals/board"))).GetProperty("summary").GetProperty("total").GetInt32().Should().Be(11);
            var all = await Json(await owner.GetAsync("/api/deals/all"));
            all.EnumerateArray().Single(d => d.GetProperty("name").GetString() == "Bernese Bank").GetProperty("isStale").GetBoolean().Should().BeTrue();
            (await Json(await owner.GetAsync($"/api/deals/{seeded.Deals["Club dinner"]}"))).GetProperty("stageEnteredAtUtc").GetDateTime()
                .Should().BeCloseTo(DateTime.UtcNow.AddDays(-20), TimeSpan.FromMinutes(5));
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
