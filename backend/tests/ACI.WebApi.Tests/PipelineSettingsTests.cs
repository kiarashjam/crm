using System.Net;
using System.Text.Json;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// What a manager sets for how the board judges deals: the probability a stage gives
/// the deals in it that have none of their own, and after how many quiet days an open
/// deal is stale, for the workspace and per pipeline.
/// </summary>
public class PipelineSettingsTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(CustomWebApplicationFactory Factory, HttpClient Owner, Guid Org)> WorkspaceAsync()
    {
        var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        return (factory, owner, org);
    }

    [Fact]
    public async Task AStageCarriesADefaultProbabilityThatCanBeChangedAndTakenAway()
    {
        var (factory, owner, _) = await WorkspaceAsync();
        using var _f = factory;
        var pipeline = await Json(await owner.PostAsync("/api/pipelines", Body(new { name = "Memberships" })));
        var pipelineId = pipeline.GetProperty("id").GetGuid();

        var stage = await Json(await owner.PostAsync("/api/dealstages", Body(new { pipelineId, name = "Tour booked", displayOrder = 1, probability = 40 })));
        var stageId = stage.GetProperty("id").GetGuid();
        stage.GetProperty("probability").GetInt32().Should().Be(40);

        (await Json(await owner.PutAsync($"/api/dealstages/{stageId}", Body(new { probability = 60 }))))
            .GetProperty("probability").GetInt32().Should().Be(60);
        (await Json(await owner.PutAsync($"/api/dealstages/{stageId}", Body(new { name = "Tour done" }))))
            .GetProperty("probability").GetInt32().Should().Be(60, "an edit that leaves it out keeps it");

        var listed = await Json(await owner.GetAsync("/api/pipelines"));
        listed[0].GetProperty("dealStages")[0].GetProperty("probability").GetInt32()
            .Should().Be(60, "the board reads the stages from the pipelines");

        (await Json(await owner.PutAsync($"/api/dealstages/{stageId}", Body(new { clearProbability = true }))))
            .GetProperty("probability").ValueKind.Should().Be(JsonValueKind.Null);
    }

    /// <summary>
    /// Settings can make a stage the won (or lost) stage. The deals already in it follow,
    /// as if each had been moved there: the board counts an open deal in a won stage as
    /// won and the reports read the deal's own outcome, so they disagreed about every deal
    /// in the stage. Made open again, the stage reopens the deals it had closed.
    /// </summary>
    [Fact]
    public async Task MakingAStageTheWonStageClosesTheOpenDealsInIt()
    {
        var (factory, owner, _) = await WorkspaceAsync();
        using var _f = factory;
        await DealsFollowTheirStagesOutcomeAsync(owner);
    }

    /// <summary>The same on SQL Server, where finding the deals that are still open has to translate.</summary>
    [SqlServerFact]
    public async Task MakingAStageTheWonStageClosesTheOpenDealsInItOnSqlServer()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var org = await Json(await owner.PostAsync("/api/organizations", Body(new { name = "Pavillon" })), HttpStatusCode.Created);
            owner.In(org.GetProperty("id").GetGuid());
            await DealsFollowTheirStagesOutcomeAsync(owner);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    private static async Task DealsFollowTheirStagesOutcomeAsync(HttpClient owner)
    {
        var pipelineId = (await Json(await owner.PostAsync("/api/pipelines", Body(new { name = "Memberships" })))).GetProperty("id").GetGuid();
        var signed = (await Json(await owner.PostAsync("/api/dealstages", Body(new { pipelineId, name = "Signed", displayOrder = 1 })))).GetProperty("id").GetGuid();
        async Task<Guid> DealIn(string name) =>
            (await Json(await owner.PostAsync("/api/deals", Body(new { name, amount = 1000, dealStageId = signed })))).GetProperty("id").GetGuid();
        var waiting = await DealIn("Waiting for the committee");
        // Lost on its own page; the pipeline has no lost stage to move it to, so it stays.
        var declined = await DealIn("Declined");
        await Json(await owner.PutAsync($"/api/deals/{declined}", Body(new { isWon = false })));

        await Json(await owner.PutAsync($"/api/dealstages/{signed}", Body(new { isWon = true, isLost = false })));

        var won = await Json(await owner.GetAsync($"/api/deals/{waiting}"));
        won.GetProperty("isWon").GetBoolean().Should().BeTrue("a deal in the won stage is won, in the reports as on the board");
        won.GetProperty("closedAtUtc").ValueKind.Should().NotBe(JsonValueKind.Null);
        (await Json(await owner.GetAsync($"/api/deals/{declined}"))).GetProperty("isWon").GetBoolean()
            .Should().BeFalse("it was lost by a person, not by the stage");
        (await Json(await owner.GetAsync("/api/reporting/dashboard"))).GetProperty("dealsWonCount").GetInt32().Should().Be(1);
        var board = await Json(await owner.GetAsync($"/api/deals/board?pipelineId={pipelineId}"));
        board.GetProperty("summary").GetProperty("won").GetInt32().Should().Be(1);
        board.GetProperty("summary").GetProperty("lost").GetInt32().Should().Be(1);

        await Json(await owner.PutAsync($"/api/dealstages/{signed}", Body(new { isWon = false, isLost = false })));

        var reopened = await Json(await owner.GetAsync($"/api/deals/{waiting}"));
        reopened.GetProperty("isWon").ValueKind.Should().Be(JsonValueKind.Null, "the stage that closed it no longer does");
        reopened.GetProperty("closedAtUtc").ValueKind.Should().Be(JsonValueKind.Null);
        (await Json(await owner.GetAsync($"/api/deals/{declined}"))).GetProperty("isWon").GetBoolean().Should().BeFalse();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task AProbabilityOutsideZeroToAHundredIsRefused(int probability)
    {
        var (factory, owner, _) = await WorkspaceAsync();
        using var _f = factory;
        var pipelineId = (await Json(await owner.PostAsync("/api/pipelines", Body(new { name = "Sales" })))).GetProperty("id").GetGuid();

        await Json(await owner.PostAsync("/api/dealstages", Body(new { pipelineId, name = "Proposal", probability })), HttpStatusCode.BadRequest);

        var stageId = (await Json(await owner.PostAsync("/api/dealstages", Body(new { pipelineId, name = "Proposal", probability = 30 })))).GetProperty("id").GetGuid();
        await Json(await owner.PutAsync($"/api/dealstages/{stageId}", Body(new { probability })), HttpStatusCode.BadRequest);
        (await Json(await owner.GetAsync($"/api/dealstages/{stageId}"))).GetProperty("probability").GetInt32().Should().Be(30);
    }

    [Fact]
    public async Task APipelineFollowsTheWorkspaceUntilItSetsItsOwnStaleDays()
    {
        var (factory, owner, org) = await WorkspaceAsync();
        using var _f = factory;

        var workspace = await Json(await owner.GetAsync($"/api/organizations/{org}"));
        workspace.GetProperty("staleDealDays").GetInt32().Should().Be(30, "a workspace flags deals after 30 quiet days until a manager says otherwise");

        var pipeline = await Json(await owner.PostAsync("/api/pipelines", Body(new { name = "Memberships" })));
        var pipelineId = pipeline.GetProperty("id").GetGuid();
        pipeline.GetProperty("staleDealDays").ValueKind.Should().Be(JsonValueKind.Null, "null follows the workspace");

        (await Json(await owner.PutAsync($"/api/pipelines/{pipelineId}", Body(new { staleDealDays = 60 }))))
            .GetProperty("staleDealDays").GetInt32().Should().Be(60);
        (await Json(await owner.PutAsync($"/api/pipelines/{pipelineId}", Body(new { name = "Club memberships" }))))
            .GetProperty("staleDealDays").GetInt32().Should().Be(60, "an edit that leaves it out keeps it");
        (await Json(await owner.PutAsync($"/api/pipelines/{pipelineId}", Body(new { staleDealDays = 0 }))))
            .GetProperty("staleDealDays").GetInt32().Should().Be(0, "0 never flags this pipeline's deals");
        (await Json(await owner.PutAsync($"/api/pipelines/{pipelineId}", Body(new { followWorkspaceStaleDays = true }))))
            .GetProperty("staleDealDays").ValueKind.Should().Be(JsonValueKind.Null);

        await Json(await owner.PutAsync($"/api/pipelines/{pipelineId}", Body(new { staleDealDays = 366 })), HttpStatusCode.BadRequest);
        await Json(await owner.PostAsync("/api/pipelines", Body(new { name = "Events", staleDealDays = -3 })), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AManagerSetsTheWorkspaceStaleDaysAndARepCannot()
    {
        var (factory, owner, org) = await WorkspaceAsync();
        using var _f = factory;
        var (rep, repId) = await SignInAsync(factory, "rep@example.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        rep.In(org);

        await Json(await rep.PutAsync($"/api/organizations/{org}/settings", Body(new { staleDealDays = 7 })), HttpStatusCode.Forbidden);
        await Json(await owner.PutAsync($"/api/organizations/{org}/settings", Body(new { staleDealDays = 400 })), HttpStatusCode.BadRequest);

        (await Json(await owner.PutAsync($"/api/organizations/{org}/settings", Body(new { staleDealDays = 14 }))))
            .GetProperty("staleDealDays").GetInt32().Should().Be(14);
        // The currency and the rest are left as they were.
        var seen = await Json(await rep.GetAsync($"/api/organizations/{org}"));
        seen.GetProperty("staleDealDays").GetInt32().Should().Be(14);
        seen.GetProperty("currency").GetString().Should().Be("CHF");
        (await Json(await owner.PutAsync($"/api/organizations/{org}/settings", Body(new { currency = "EUR" }))))
            .GetProperty("staleDealDays").GetInt32().Should().Be(14, "a save that leaves it out keeps it");
    }

    /// <summary>
    /// A database from before the columns: on start they are added, every workspace there
    /// flags deals after the default 30 days, and no pipeline or stage has a setting.
    /// </summary>
    [SqlServerFact]
    public async Task AnExistingDatabaseGetsTheColumnsWithTheDefaults()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            Guid userId;
            using (var app = SqlServerApp.Start(database))
                (_, userId) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");

            // As production is before this deploy.
            await SqlServerApp.ExecuteAsync(database, "ALTER TABLE [Organizations] DROP CONSTRAINT [DF_Organizations_StaleDealDays]");
            await SqlServerApp.ExecuteAsync(database, "ALTER TABLE [Organizations] DROP COLUMN [StaleDealDays]");
            await SqlServerApp.ExecuteAsync(database, "ALTER TABLE [Pipelines] DROP COLUMN [StaleDealDays]");
            await SqlServerApp.ExecuteAsync(database, "ALTER TABLE [DealStages] DROP COLUMN [Probability]");
            var org = Guid.NewGuid();
            var pipeline = Guid.NewGuid();
            var stage = Guid.NewGuid();
            await SqlServerApp.ExecuteAsync(database,
                $"INSERT INTO [Organizations] ([Id], [Name], [OwnerUserId], [CreatedAtUtc]) VALUES ('{org}', N'Pavillon46', '{userId}', SYSUTCDATETIME())");
            await SqlServerApp.ExecuteAsync(database,
                $"INSERT INTO [Pipelines] ([Id], [OrganizationId], [Name], [DisplayOrder]) VALUES ('{pipeline}', '{org}', N'Sales', 0)");
            await SqlServerApp.ExecuteAsync(database,
                $"INSERT INTO [DealStages] ([Id], [PipelineId], [Name], [DisplayOrder], [IsWon], [IsLost]) VALUES ('{stage}', '{pipeline}', N'Proposal', 1, 0, 0)");

            using (SqlServerApp.Start(database))
            {
                (await SqlServerApp.ScalarAsync(database, $"SELECT [StaleDealDays] FROM [Organizations] WHERE [Id] = '{org}'")).Should().Be(30);
                (await SqlServerApp.ScalarAsync(database, $"SELECT [StaleDealDays] FROM [Pipelines] WHERE [Id] = '{pipeline}'")).Should().BeNull();
                (await SqlServerApp.ScalarAsync(database, $"SELECT [Probability] FROM [DealStages] WHERE [Id] = '{stage}'")).Should().BeNull();
            }
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
