using System.Net;
using System.Text.Json;
using ACI.Domain.Enums;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Working the deals board over HTTP: a pipeline set up in Settings, deals added, cards
/// moved the way a drag moves them, and the board read back after each move.
/// </summary>
/// <remarks>
/// DealBoardTests checks the board against deals written straight into the database. These
/// make every change through the API, so a move that the board does not show, money that
/// stays in the column a card left, or a win that is still counted as open shows here.
/// </remarks>
public class DealBoardMovesTests
{
    private sealed record Board(CustomWebApplicationFactory Factory, HttpClient Owner, HttpClient Rep,
        Guid Pipeline, Guid Qualified, Guid Proposal, Guid Won) : IDisposable
    {
        public void Dispose() => Factory.Dispose();
    }

    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed, got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<Board> BoardAsync()
    {
        var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@club.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        var (rep, repId) = await SignInAsync(factory, "rep@club.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        owner.In(org);
        rep.In(org);

        var pipeline = (await Ok(await owner.PostAsync("/api/pipelines", Body(new { name = "Memberships" })), "add the pipeline"))
            .GetProperty("id").GetGuid();
        async Task<Guid> StageAsync(string name, int order, int? probability = null, bool isWon = false) =>
            (await Ok(await owner.PostAsync("/api/dealstages", Body(new { pipelineId = pipeline, name, displayOrder = order, probability, isWon })),
                $"add the {name} stage")).GetProperty("id").GetGuid();
        var qualified = await StageAsync("Qualified", 1, probability: 20);
        var proposal = await StageAsync("Proposal", 2, probability: 50);
        var won = await StageAsync("Signed", 3, isWon: true);
        return new Board(factory, owner, rep, pipeline, qualified, proposal, won);
    }

    private static async Task<Guid> DealAsync(HttpClient client, Board board, string name, decimal amount, Guid stage) =>
        (await Ok(await client.PostAsync("/api/deals", Body(new { name, amount, currency = "CHF", pipelineId = board.Pipeline, dealStageId = stage })),
            $"add {name}")).GetProperty("id").GetGuid();

    // A drag on the board sends the new stage and nothing else.
    private static async Task MoveAsync(HttpClient client, Guid deal, Guid stage) =>
        await Ok(await client.PutAsync($"/api/deals/{deal}", Body(new { dealStageId = stage })), "move the card");

    private static async Task<JsonElement> ReadAsync(Board board) =>
        await Ok(await board.Owner.GetAsync($"/api/deals/board?pipelineId={board.Pipeline}"), "read the board");

    private static JsonElement Column(JsonElement board, Guid stage) =>
        board.GetProperty("columns").EnumerateArray().Single(c => c.GetProperty("key").GetString() == stage.ToString());

    private static IEnumerable<string?> Cards(JsonElement column) =>
        column.GetProperty("deals").EnumerateArray().Select(d => d.GetProperty("name").GetString());

    private static decimal Chf(JsonElement rows) =>
        rows.EnumerateArray().Where(r => r.GetProperty("currency").GetString() == "CHF").Sum(r => r.GetProperty("value").GetDecimal());

    [Fact]
    public async Task AMovedCardTakesItsMoneyToTheColumnItWasDroppedIn()
    {
        using var board = await BoardAsync();
        var gala = await DealAsync(board.Owner, board, "Winter gala", 1000m, board.Qualified);
        await DealAsync(board.Rep, board, "Summer fete", 500m, board.Qualified);

        var before = await ReadAsync(board);
        Cards(Column(before, board.Qualified)).Should().BeEquivalentTo("Winter gala", "Summer fete");
        Chf(Column(before, board.Qualified).GetProperty("totals")).Should().Be(1500m);
        Chf(Column(before, board.Qualified).GetProperty("weighted")).Should().Be(300m, "the stage's 20% of both");

        await MoveAsync(board.Owner, gala, board.Proposal);

        var after = await ReadAsync(board);
        var qualified = Column(after, board.Qualified);
        var proposal = Column(after, board.Proposal);
        Cards(qualified).Should().Equal("Summer fete");
        qualified.GetProperty("count").GetInt32().Should().Be(1);
        Chf(qualified.GetProperty("totals")).Should().Be(500m);
        Chf(qualified.GetProperty("weighted")).Should().Be(100m);
        Cards(proposal).Should().Equal("Winter gala");
        proposal.GetProperty("count").GetInt32().Should().Be(1);
        Chf(proposal.GetProperty("totals")).Should().Be(1000m);
        Chf(proposal.GetProperty("weighted")).Should().Be(500m, "the 50% of the stage it is in now");

        var summary = after.GetProperty("summary");
        summary.GetProperty("open").GetInt32().Should().Be(2);
        Chf(summary.GetProperty("openTotals")).Should().Be(1500m, "a move changes no money, only where it is");
        Chf(summary.GetProperty("weighted")).Should().Be(600m);
    }

    [Fact]
    public async Task ACardDroppedInTheWonStageIsWonAndLeavesTheOpenFigures()
    {
        using var board = await BoardAsync();
        var gala = await DealAsync(board.Owner, board, "Winter gala", 1000m, board.Proposal);
        await DealAsync(board.Owner, board, "Summer fete", 500m, board.Qualified);

        await MoveAsync(board.Owner, gala, board.Won);

        (await Ok(await board.Owner.GetAsync($"/api/deals/{gala}"), "open the deal")).GetProperty("isWon").GetBoolean()
            .Should().BeTrue("the stage says it is won");
        var after = await ReadAsync(board);
        Cards(Column(after, board.Won)).Should().Equal("Winter gala");
        Column(after, board.Won).GetProperty("weighted").GetArrayLength().Should().Be(0, "a won deal has nothing left to weigh");
        var summary = after.GetProperty("summary");
        summary.GetProperty("open").GetInt32().Should().Be(1);
        summary.GetProperty("won").GetInt32().Should().Be(1);
        Chf(summary.GetProperty("openTotals")).Should().Be(500m);
        Chf(summary.GetProperty("wonTotals")).Should().Be(1000m);
        Chf(summary.GetProperty("weighted")).Should().Be(100m, "only the open deal is weighted");

        // Dragged back, it is open again.
        await MoveAsync(board.Owner, gala, board.Proposal);
        var reopened = (await ReadAsync(board)).GetProperty("summary");
        reopened.GetProperty("open").GetInt32().Should().Be(2);
        reopened.GetProperty("won").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task AnEditMadeFromBeforeAMoveIsRefusedAndTheBoardKeepsTheMove()
    {
        using var board = await BoardAsync();
        var gala = await DealAsync(board.Owner, board, "Winter gala", 1000m, board.Qualified);
        // The rep opens the deal to edit it...
        var read = await Ok(await board.Rep.GetAsync($"/api/deals/{gala}"), "open the deal");
        var version = read.GetProperty("version").GetGuid();

        // ...while the owner moves its card.
        await MoveAsync(board.Owner, gala, board.Proposal);

        var stale = await board.Rep.PutAsync($"/api/deals/{gala}",
            Body(new { name = "Winter gala", amount = 1200m, dealStageId = board.Qualified, version }));
        stale.StatusCode.Should().Be(HttpStatusCode.Conflict, await stale.Content.ReadAsStringAsync());

        var after = await ReadAsync(board);
        Cards(Column(after, board.Proposal)).Should().Equal("Winter gala");
        Chf(Column(after, board.Proposal).GetProperty("totals")).Should().Be(1000m, "the refused edit changed nothing");
        Cards(Column(after, board.Qualified)).Should().BeEmpty();
    }
}
