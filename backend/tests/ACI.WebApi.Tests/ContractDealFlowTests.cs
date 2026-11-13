using System.Net;
using System.Text.Json;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A deal's contract: drafted with the deal's value and line items merged in (a fee table in the
/// deal's currency, VAT included), listed on the deal, and limited to who may see the deal.
/// </summary>
/// <remarks>
/// Contracts lived only on leads and knew nothing of the deal: the fee was typed by hand into
/// the contract, after it had been typed into the deal's line items, and the two could differ.
/// </remarks>
public class ContractDealFlowTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(expected, $"{step} should answer {(int)expected} — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(HttpClient Client, Guid UserId, Guid Org)> ClubAsync(CustomWebApplicationFactory factory, string email = "owner@pavillon46.ch")
    {
        var (client, userId) = await SignInAsync(factory, email);
        var org = await CreateAsync(factory, userId, "Pavillon 46");
        client.In(org);
        return (client, userId, org);
    }

    /// <summary>A CHF deal for Anna Muster (a contact) with two lines, one of them at the reduced rate.</summary>
    private static async Task<Guid> DealWithLinesAsync(HttpClient client, string email = "anna@example.ch")
    {
        var contact = (await Json(await client.PostAsync("/api/contacts", Body(new { name = "Anna Muster", email })), "add contact")).GetProperty("id").GetGuid();
        var deal = (await Json(await client.PostAsync("/api/deals", Body(new { name = "Gold for Anna", amount = 556.5m, currency = "CHF", contactId = contact })), "add deal"))
            .GetProperty("id").GetGuid();
        await Json(await client.PostAsync($"/api/deals/{deal}/line-items", Body(new { name = "Gold membership, annual", quantity = 1, unitPrice = 480m, vatRate = 8.1m })), "add a line");
        await Json(await client.PostAsync($"/api/deals/{deal}/line-items", Body(new { name = "Wine tasting", quantity = 3, unitPrice = 25.5m, vatRate = 2.6m })), "add another");
        return deal;
    }

    [Fact]
    public async Task ALineItemCarriesItsVatRate_WhichCanBeChangedOrTakenOff_AndANonsenseRateIsRefused()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var deal = (await Json(await client.PostAsync("/api/deals", Body(new { name = "Gold", amount = 480m, currency = "CHF" })), "add deal")).GetProperty("id").GetGuid();

        var line = await Json(await client.PostAsync($"/api/deals/{deal}/line-items", Body(new { name = "Gold membership", quantity = 1, unitPrice = 480m, vatRate = 8.1m })), "add a line");
        line.GetProperty("vatRate").GetDecimal().Should().Be(8.1m);
        var id = line.GetProperty("id").GetGuid();

        (await Json(await client.PutAsync($"/api/deals/line-items/{id}", Body(new { vatRate = 2.6m })), "change the rate")).GetProperty("vatRate").GetDecimal().Should().Be(2.6m);
        (await Json(await client.PutAsync($"/api/deals/line-items/{id}", Body(new { quantity = 2 })), "change only the quantity"))
            .GetProperty("vatRate").GetDecimal().Should().Be(2.6m, "a rate left out of the change is kept");
        (await Json(await client.PutAsync($"/api/deals/line-items/{id}", Body(new { vatRate = (decimal?)null })), "take the rate off"))
            .GetProperty("vatRate").ValueKind.Should().Be(JsonValueKind.Null);

        await Json(await client.PutAsync($"/api/deals/line-items/{id}", Body(new { vatRate = 8.125m })), "three decimals", HttpStatusCode.BadRequest);
        await Json(await client.PostAsync($"/api/deals/{deal}/line-items", Body(new { name = "x", quantity = 1, unitPrice = 1m, vatRate = 81m })), "a price in the rate box", HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ADraftOnADeal_PrintsItsLinesAsTheFeeTable_AndTheirTotalAsWhatTheMemberPays()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var deal = await DealWithLinesAsync(client);

        var draft = await Json(await client.PostAsync("/api/contracts/draft", Body(new { dealId = deal })), "draft on the deal");

        draft.GetProperty("dealId").GetGuid().Should().Be(deal);
        (draft.GetProperty("counterpartyName").GetString(), draft.GetProperty("counterpartyEmail").GetString())
            .Should().Be(("Anna Muster", "anna@example.ch"), "a deal that came from no lead is with its contact");
        var body = draft.GetProperty("body").GetString()!;
        body.Should().Contain("Gold membership, annual").And.Contain("Wine tasting").And.Contain("| 8.1% |").And.Contain("| 2.6% |")
            .And.Contain("Unit price (CHF)").And.Contain("Prices include VAT at the rates shown.");
        body.Should().Contain("The Member agrees to pay CHF 556.50");
        var unresolved = draft.GetProperty("unresolvedFields").EnumerateArray().Select(f => f.GetString()).ToList();
        unresolved.Should().NotContain(f => f!.StartsWith("deal.") || f == "contract.fee", "everything the deal knows is filled in");

        // The PDF sets the same table as columns: each figure is there, and no bar is printed.
        var pdf = await client.GetAsync($"/api/contracts/{draft.GetProperty("id").GetGuid()}/pdf");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        var text = PdfText.Of(await pdf.Content.ReadAsByteArrayAsync());
        text.Should().Contain("Gold membership, annual").And.Contain("Unit price (CHF)").And.Contain("76.50").And.Contain("556.50").And.Contain("VAT 8.1% included");
        text.Should().NotContain("| 8.1%");
    }

    [Fact]
    public async Task ADealWithoutLines_HasItsValueAsTheContractFee()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var deal = (await Json(await client.PostAsync("/api/deals", Body(new { name = "Silver", amount = 950m, currency = "CHF" })), "add deal")).GetProperty("id").GetGuid();

        var draft = await Json(await client.PostAsync("/api/contracts/draft", Body(new { dealId = deal })), "draft");

        draft.GetProperty("body").GetString().Should().Contain("The Member agrees to pay CHF 950.00");
    }

    [Fact]
    public async Task ADealsContracts_IncludeThoseDraftedOnTheLeadItCameFrom()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var lead = (await Json(await client.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email = "jean@example.ch", status = "New" })), "lead")).GetProperty("id").GetGuid();
        var onLead = (await Json(await client.PostAsync("/api/contracts/draft", Body(new { leadId = lead })), "draft on the lead")).GetProperty("id").GetGuid();
        var converted = await Json(await client.PostAsync($"/api/leads/{lead}/convert", Body(new { createContact = true, createDeal = true, dealName = "Membership", dealAmount = 1200m })), "convert");
        var deal = converted.GetProperty("dealId").GetGuid();

        var onDeal = await Json(await client.PostAsync("/api/contracts/draft", Body(new { dealId = deal })), "draft on the deal");
        onDeal.GetProperty("leadId").GetGuid().Should().Be(lead, "a deal's contract is with the lead it came from");

        var listed = await Json(await client.GetAsync($"/api/contracts/for-deal/{deal}"), "the deal's contracts");
        listed.EnumerateArray().Select(c => c.GetProperty("id").GetGuid()).Should().BeEquivalentTo([onLead, onDeal.GetProperty("id").GetGuid()]);
    }

    [Fact]
    public async Task APrivateDeal_ShowsNoContractsTo_AndCannotBeDraftedOnBy_SomeoneWhoMayNotOpenIt()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var deal = await DealWithLinesAsync(owner);
        await Json(await owner.PutAsync($"/api/deals/{deal}", Body(new { isPrivate = true })), "make it private");
        await Json(await owner.PostAsync("/api/contracts/draft", Body(new { dealId = deal })), "the owner drafts");
        var (rep, repId) = await SignInAsync(factory, "rep@pavillon46.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        rep.In(org);

        await Json(await rep.GetAsync($"/api/contracts/for-deal/{deal}"), "a rep lists the private deal's contracts", HttpStatusCode.NotFound);
        await Json(await rep.PostAsync("/api/contracts/draft", Body(new { dealId = deal })), "a rep drafts on it", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AMembershipContractNamesOneOfTheClubsTiersOnSale_InAWorkspaceWithTheClubOn()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, org) = await ClubAsync(factory);
        var deal = await DealWithLinesAsync(client);

        var draft = await Json(await client.PostAsync("/api/contracts/draft", Body(new { dealId = deal, membershipTier = "gold" })), "a Gold membership contract");
        draft.GetProperty("membershipTier").GetString().Should().Be("Gold", "spelled as the club's catalogue spells it");
        var id = draft.GetProperty("id").GetGuid();

        await Json(await client.PostAsync("/api/contracts/draft", Body(new { dealId = deal, membershipTier = "Diamond" })), "a tier the club has not got", HttpStatusCode.BadRequest);
        (await Json(await client.PutAsync($"/api/contracts/{id}", Body(new { membershipTier = (string?)null })), "take the tier off"))
            .GetProperty("membershipTier").ValueKind.Should().Be(JsonValueKind.Null);

        await Json(await client.PutAsync($"/api/organizations/{org}/modules", Body(new { club = false })), "turn the club off");
        await Json(await client.PutAsync($"/api/contracts/{id}", Body(new { membershipTier = "Gold" })), "a tier with the club off", HttpStatusCode.BadRequest);
    }
}
