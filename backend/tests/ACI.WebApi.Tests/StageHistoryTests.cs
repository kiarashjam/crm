using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Every time a deal comes into a stage it is written down, however it got there, and
/// the deal page reads the history back.
/// </summary>
/// <remarks>
/// Only a move made by stage id was written. A deal created in a stage, a move by name
/// (every move in a workspace without pipelines) and a close from the deal page left no
/// line, so there was no telling how long a deal had sat where it was.
/// </remarks>
public class StageHistoryTests
{
    private sealed record Board(CustomWebApplicationFactory Factory, HttpClient Owner, Guid OwnerId, Guid Org, Guid Pipeline,
        Guid Qualification, Guid Proposal, Guid Won, Guid Lost) : IDisposable
    {
        public void Dispose() => Factory.Dispose();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, body);
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<Board> BoardAsync()
    {
        var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        var pipeline = (await Json(await owner.PostAsync("/api/pipelines", Body(new { name = "Memberships" })))).GetProperty("id").GetGuid();
        async Task<Guid> Stage(string name, int order, bool isWon = false, bool isLost = false) =>
            (await Json(await owner.PostAsync("/api/dealstages", Body(new { pipelineId = pipeline, name, displayOrder = order, isWon, isLost }))))
                .GetProperty("id").GetGuid();
        return new Board(factory, owner, ownerId, org, pipeline,
            await Stage("Qualification", 1), await Stage("Proposal", 2), await Stage("Signed", 3, isWon: true), await Stage("No budget", 4, isLost: true));
    }

    private static async Task<Guid> CreateDealAsync(HttpClient client, object body) =>
        (await Json(await client.PostAsync("/api/deals", Body(body)))).GetProperty("id").GetGuid();

    private static async Task<List<JsonElement>> HistoryAsync(HttpClient client, Guid dealId) =>
        (await Json(await client.GetAsync($"/api/deals/{dealId}/stage-history"))).EnumerateArray().ToList();

    [Fact]
    public async Task ADealStartsItsHistoryInTheStageItWasCreatedIn()
    {
        using var board = await BoardAsync();
        // The stage alone: its pipeline is the stage's.
        var deal = await CreateDealAsync(board.Owner, new { name = "Fondation Léman", amount = 12000, dealStageId = board.Qualification });

        var history = await HistoryAsync(board.Owner, deal);
        history.Should().ContainSingle();
        history[0].GetProperty("isCreation").GetBoolean().Should().BeTrue();
        history[0].GetProperty("toStageId").GetGuid().Should().Be(board.Qualification);
        history[0].GetProperty("toStageName").GetString().Should().Be("Qualification");
        history[0].GetProperty("leftAtUtc").ValueKind.Should().Be(JsonValueKind.Null, "it is still there");
        history[0].GetProperty("changedByName").GetString().Should().Be("owner@example.ch");

        var saved = await Json(await board.Owner.GetAsync($"/api/deals/{deal}"));
        saved.GetProperty("pipelineId").GetGuid().Should().Be(board.Pipeline);
        saved.GetProperty("stage").GetString().Should().Be("Qualification");
    }

    [Fact]
    public async Task EachMoveIsWrittenAndTheStageBeforeItEndsWhenItIsMade()
    {
        using var board = await BoardAsync();
        var deal = await CreateDealAsync(board.Owner, new { name = "Corporate gala", amount = 5000, pipelineId = board.Pipeline, dealStageId = board.Qualification, stage = "Qualification" });

        await Json(await board.Owner.PutAsync($"/api/deals/{deal}", Body(new { dealStageId = board.Proposal, stage = "Proposal" })));
        // The edit dialog sends the stage back with every save: not a move.
        await Json(await board.Owner.PutAsync($"/api/deals/{deal}", Body(new { name = "Corporate gala 2027", dealStageId = board.Proposal, stage = "Proposal" })));

        var history = await HistoryAsync(board.Owner, deal);
        history.Should().HaveCount(2);
        var (move, created) = (history[0], history[1]);
        move.GetProperty("fromStageId").GetGuid().Should().Be(board.Qualification);
        move.GetProperty("fromStageName").GetString().Should().Be("Qualification");
        move.GetProperty("toStageName").GetString().Should().Be("Proposal");
        move.GetProperty("isCreation").GetBoolean().Should().BeFalse();
        created.GetProperty("leftAtUtc").GetDateTime().Should().Be(move.GetProperty("changedAtUtc").GetDateTime());
    }

    [Fact]
    public async Task AMoveByNameGoesToThePipelinesStageOfThatName()
    {
        using var board = await BoardAsync();
        var deal = await CreateDealAsync(board.Owner, new { name = "Member referral", amount = 3000, pipelineId = board.Pipeline, stage = "qualification" });

        var created = await Json(await board.Owner.GetAsync($"/api/deals/{deal}"));
        created.GetProperty("dealStageId").GetGuid().Should().Be(board.Qualification, "the pipeline has a stage by that name");

        // An older client that knows stages only by name.
        var moved = await Json(await board.Owner.PutAsync($"/api/deals/{deal}", Body(new { stage = "Signed" })));
        moved.GetProperty("dealStageId").GetGuid().Should().Be(board.Won);
        moved.GetProperty("isWon").GetBoolean().Should().BeTrue("Signed is the pipeline's won stage");

        var history = await HistoryAsync(board.Owner, deal);
        history.Select(h => h.GetProperty("toStageName").GetString()).Should().Equal("Signed", "Qualification");
        history[0].GetProperty("toStageId").GetGuid().Should().Be(board.Won);
    }

    [Fact]
    public async Task AWorkspaceWithoutPipelinesHasItsMovesByNameWrittenToo()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        owner.In(await CreateAsync(factory, ownerId, "Pavillon46"));
        var deal = await CreateDealAsync(owner, new { name = "Wine tasting", amount = 800, stage = "Qualification" });

        await Json(await owner.PutAsync($"/api/deals/{deal}", Body(new { stage = "Proposal" })));
        await Json(await owner.PutAsync($"/api/deals/{deal}", Body(new { stage = "Proposal ", name = "Wine tasting for 40" })));

        var history = await HistoryAsync(owner, deal);
        history.Select(h => h.GetProperty("toStageName").GetString()).Should().Equal("Proposal", "Qualification");
        history[0].GetProperty("fromStageName").GetString().Should().Be("Qualification");
        history[0].GetProperty("toStageId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WonOrLostFromTheDealPageMovesTheDealToThatStage(bool won)
    {
        using var board = await BoardAsync();
        var deal = await CreateDealAsync(board.Owner, new { name = "Annual dinner", amount = 9000, pipelineId = board.Pipeline, dealStageId = board.Proposal });

        var closed = await Json(await board.Owner.PutAsync($"/api/deals/{deal}", Body(new { isWon = won, closedReason = "Decided" })));

        closed.GetProperty("dealStageId").GetGuid().Should().Be(won ? board.Won : board.Lost,
            "the board shows the deal where the reports count it");
        closed.GetProperty("isWon").GetBoolean().Should().Be(won);
        var history = await HistoryAsync(board.Owner, deal);
        history[0].GetProperty("fromStageName").GetString().Should().Be("Proposal");
        history[0].GetProperty("toStageName").GetString().Should().Be(won ? "Signed" : "No budget");
    }

    [Fact]
    public async Task ClosingADealWhosePipelineHasNoWonStageLeavesItWhereItIs()
    {
        using var board = await BoardAsync();
        var plain = (await Json(await board.Owner.PostAsync("/api/pipelines", Body(new { name = "Events" })))).GetProperty("id").GetGuid();
        var enquiry = (await Json(await board.Owner.PostAsync("/api/dealstages", Body(new { pipelineId = plain, name = "Enquiry" })))).GetProperty("id").GetGuid();
        var deal = await CreateDealAsync(board.Owner, new { name = "Wedding", amount = 15000, dealStageId = enquiry });

        var closed = await Json(await board.Owner.PutAsync($"/api/deals/{deal}", Body(new { isWon = true })));

        closed.GetProperty("dealStageId").GetGuid().Should().Be(enquiry);
        closed.GetProperty("isWon").GetBoolean().Should().BeTrue();
        (await HistoryAsync(board.Owner, deal)).Should().ContainSingle("nothing moved");
    }

    [Fact]
    public async Task ADealCreatedInAWonStageIsWon()
    {
        using var board = await BoardAsync();
        var deal = await Json(await board.Owner.PostAsync("/api/deals", Body(new { name = "Past win", amount = 2000, dealStageId = board.Won })));

        deal.GetProperty("isWon").GetBoolean().Should().BeTrue("a deal in a won stage is won, wherever it was put there");
        deal.GetProperty("closedAtUtc").ValueKind.Should().NotBe(JsonValueKind.Null);
        var dashboard = await Json(await board.Owner.GetAsync("/api/reporting/dashboard"));
        dashboard.GetProperty("dealsWonCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ALeadConvertedIntoAStageStartsItsDealsHistoryThere()
    {
        using var board = await BoardAsync();
        var lead = (await Json(await board.Owner.PostAsync("/api/leads", Body(new { name = "Anna Muster", email = "anna@example.ch", source = "Website", status = "New" })))).GetProperty("id").GetGuid();

        var converted = await Json(await board.Owner.PostAsync($"/api/leads/{lead}/convert",
            Body(new { createContact = true, createDeal = true, dealName = "Anna's membership", dealValue = "2400", dealStageId = board.Proposal })));
        var deal = converted.GetProperty("dealId").GetGuid();

        var saved = await Json(await board.Owner.GetAsync($"/api/deals/{deal}"));
        saved.GetProperty("pipelineId").GetGuid().Should().Be(board.Pipeline);
        saved.GetProperty("stage").GetString().Should().Be("Proposal");
        var history = await HistoryAsync(board.Owner, deal);
        history.Should().ContainSingle();
        history[0].GetProperty("isCreation").GetBoolean().Should().BeTrue();
        history[0].GetProperty("toStageId").GetGuid().Should().Be(board.Proposal);
    }

    [Fact]
    public async Task APrivateDealsHistoryIsHiddenWithIt()
    {
        using var board = await BoardAsync();
        var (rep, repId) = await SignInAsync(board.Factory, "rep@example.ch");
        await AddMemberAsync(board.Factory, board.Org, repId, OrgMemberRole.Member);
        rep.In(board.Org);
        var deal = await CreateDealAsync(board.Owner, new { name = "Confidential", amount = 1, dealStageId = board.Qualification });
        await Json(await board.Owner.PutAsync($"/api/deals/{deal}", Body(new { isPrivate = true })));

        await Json(await rep.GetAsync($"/api/deals/{deal}/stage-history"), HttpStatusCode.NotFound);
        (await HistoryAsync(board.Owner, deal)).Should().ContainSingle();
    }

    [Fact]
    public async Task TheLinesAreStoredWithTheDealsTheyDescribe()
    {
        // Written in the deal's own save, each with an id of its own.
        using var board = await BoardAsync();
        var deal = await CreateDealAsync(board.Owner, new { name = "Gala", amount = 100, dealStageId = board.Qualification });
        await Json(await board.Owner.PutAsync($"/api/deals/{deal}", Body(new { dealStageId = board.Proposal })));

        using var scope = board.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lines = await db.DealStageChanges.AsNoTracking().Where(c => c.DealId == deal).OrderBy(c => c.ChangedAtUtc).ToListAsync();
        lines.Select(l => l.ToDealStageId).Should().Equal(board.Qualification, board.Proposal);
        lines.Select(l => l.ChangedByUserId).Should().AllBeEquivalentTo(board.OwnerId);
        lines.Should().OnlyContain(l => l.Id != Guid.Empty);
    }
}
