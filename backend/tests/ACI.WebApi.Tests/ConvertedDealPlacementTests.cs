using System.Net;
using System.Text.Json;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Where a lead's deal goes when it is converted, and how the lead leads to it.
/// </summary>
/// <remarks>
/// The convert dialog showed a pipeline and a stage but sent neither, so the deal went on
/// no pipeline at all and a workspace working on another one never found it. Any client
/// that names no stage (an older one, the API, the assistant) now gets the workspace's
/// pipeline: the one it names, else the first, at the stage of the name it gives, else
/// the first open stage. And the converted lead names its deal and contact, which it did
/// not, so its page could not lead there.
/// </remarks>
public class ConvertedDealPlacementTests
{
    private sealed record Club(CustomWebApplicationFactory Factory, HttpClient Owner, Guid Sales, Guid Qualification, Guid Membership, Guid Enquiry, Guid Visit) : IDisposable
    {
        public void Dispose() => Factory.Dispose();
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, body);
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<Club> ClubAsync()
    {
        var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        async Task<Guid> Pipeline(string name, int order) =>
            (await Json(await owner.PostAsync("/api/pipelines", Body(new { name, displayOrder = order })))).GetProperty("id").GetGuid();
        async Task<Guid> Stage(Guid pipeline, string name, int order, bool isWon = false, bool isLost = false) =>
            (await Json(await owner.PostAsync("/api/dealstages", Body(new { pipelineId = pipeline, name, displayOrder = order, isWon, isLost }))))
                .GetProperty("id").GetGuid();
        var sales = await Pipeline("Sales", 0);
        var qualification = await Stage(sales, "Qualification", 1);
        await Stage(sales, "Won", 2, isWon: true);
        var membership = await Pipeline("Membership", 1);
        // A lost stage first: a new deal does not start there.
        await Stage(membership, "Declined", 0, isLost: true);
        var enquiry = await Stage(membership, "Enquiry", 1);
        var visit = await Stage(membership, "Visit", 2);
        return new Club(factory, owner, sales, qualification, membership, enquiry, visit);
    }

    private static async Task<(Guid Lead, JsonElement Deal)> ConvertAsync(Club club, object request)
    {
        var lead = (await Json(await club.Owner.PostAsync("/api/leads", Body(new { name = "Anna Muster", email = $"anna.{Guid.NewGuid():N}@example.ch", status = "Qualified" }))))
            .GetProperty("id").GetGuid();
        var converted = await Json(await club.Owner.PostAsync($"/api/leads/{lead}/convert", Body(request)));
        var deal = await Json(await club.Owner.GetAsync($"/api/deals/{converted.GetProperty("dealId").GetGuid()}"));
        return (lead, deal);
    }

    [Fact]
    public async Task ADealConvertedWithNoStageNamedGoesOnTheFirstPipelinesFirstOpenStage()
    {
        using var club = await ClubAsync();
        var (_, deal) = await ConvertAsync(club, new { createContact = true, createDeal = true, dealName = "Anna's membership" });

        deal.GetProperty("pipelineId").GetGuid().Should().Be(club.Sales);
        deal.GetProperty("dealStageId").GetGuid().Should().Be(club.Qualification);
        deal.GetProperty("stage").GetString().Should().Be("Qualification");
    }

    [Fact]
    public async Task ADealConvertedOntoAPipelineGoesToItsFirstOpenStage()
    {
        using var club = await ClubAsync();
        var (_, deal) = await ConvertAsync(club, new { createContact = true, createDeal = true, pipelineId = club.Membership });

        deal.GetProperty("pipelineId").GetGuid().Should().Be(club.Membership);
        deal.GetProperty("dealStageId").GetGuid().Should().Be(club.Enquiry, "the lost stage before it is no place to start");
    }

    [Fact]
    public async Task AStageNamedByItsNameIsTheOneOnThatPipeline()
    {
        using var club = await ClubAsync();
        var (_, deal) = await ConvertAsync(club, new { createContact = true, createDeal = true, pipelineId = club.Membership, dealStage = "visit" });

        deal.GetProperty("dealStageId").GetGuid().Should().Be(club.Visit);
        deal.GetProperty("stage").GetString().Should().Be("Visit");
    }

    [Fact]
    public async Task AStageNamedByItsIdIsKept()
    {
        using var club = await ClubAsync();
        var (_, deal) = await ConvertAsync(club, new { createContact = true, createDeal = true, dealStageId = club.Visit });

        deal.GetProperty("pipelineId").GetGuid().Should().Be(club.Membership);
        deal.GetProperty("dealStageId").GetGuid().Should().Be(club.Visit);
    }

    [Fact]
    public async Task AConvertedLeadNamesTheDealAndContactItBecame()
    {
        using var club = await ClubAsync();
        var (lead, deal) = await ConvertAsync(club, new { createContact = true, createDeal = true });

        var read = await Json(await club.Owner.GetAsync($"/api/leads/{lead}"));
        read.GetProperty("isConverted").GetBoolean().Should().BeTrue();
        read.GetProperty("convertedToDealId").GetGuid().Should().Be(deal.GetProperty("id").GetGuid());
        read.GetProperty("convertedToContactId").GetGuid().Should().Be(deal.GetProperty("contactId").GetGuid());
    }

    [Fact]
    public async Task AWorkspaceWithoutPipelinesKeepsTheStageByName()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "solo@example.ch");
        owner.In(await CreateAsync(factory, ownerId, "No pipelines"));
        var lead = (await Json(await owner.PostAsync("/api/leads", Body(new { name = "Marc Keller", email = "marc@example.ch", status = "Qualified" })))).GetProperty("id").GetGuid();

        var converted = await Json(await owner.PostAsync($"/api/leads/{lead}/convert", Body(new { createContact = true, createDeal = true, dealStage = "Proposal" })));
        var deal = await Json(await owner.GetAsync($"/api/deals/{converted.GetProperty("dealId").GetGuid()}"));

        deal.GetProperty("stage").GetString().Should().Be("Proposal");
        deal.GetProperty("pipelineId").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
