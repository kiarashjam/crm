using System.Net;
using System.Text.Json;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>What the club tests of sales and the POS share: a club, its members and its menu.</summary>
public static class Club
{
    public static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    public static async Task<string> Refused(HttpResponseMessage res, HttpStatusCode status, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(status, $"{step} should be refused — got {(int)res.StatusCode}: {body}");
        return body;
    }

    public static async Task<(HttpClient Client, Guid UserId, Guid Org)> CreateAsync(CustomWebApplicationFactory factory, string email = "manager@example.ch", string name = "Pavillon46")
    {
        var (client, userId) = await SignInAsync(factory, email);
        var org = await Workspaces.CreateAsync(factory, userId, name);
        client.In(org);
        return (client, userId, org);
    }

    public static async Task<HttpClient> JoinAsync(CustomWebApplicationFactory factory, HttpClient owner, Guid org, string email, string preset)
    {
        var (client, userId) = await SignInAsync(factory, email);
        await AddMemberAsync(factory, org, userId, OrgMemberRole.Member);
        await Json(await owner.PutAsync($"/api/organizations/{org}/members/{userId}/permissions", Body(new { preset })), $"make {email} {preset}");
        return client.In(org);
    }

    public static async Task<Guid> MemberAsync(HttpClient client, string first = "Jean", string tier = "Gold", string? email = null) =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Dupont", email = email ?? $"{first.ToLowerInvariant()}@example.ch", tier, status = "Active",
        })), "add member")).GetProperty("id").GetGuid();

    public static async Task<Guid> ItemAsync(HttpClient client, string kind, string name, decimal price, decimal vatRate, string chargeKind) =>
        (await Json(await client.PostAsync("/api/catalog", Body(new { kind, name, price, vatRate, chargeKind })), $"add {name} to the catalogue"))
            .GetProperty("id").GetGuid();

    public static async Task<List<JsonElement>> ChargesAsync(HttpClient client, Guid memberId) =>
        (await Json(await client.GetAsync($"/api/charges?memberId={memberId}"), "list the member's charges")).EnumerateArray().ToList();

    public static async Task<decimal> BalanceAsync(HttpClient client, Guid memberId) =>
        (await Json(await client.GetAsync($"/api/members/{memberId}/balance"), "read the balance")).GetProperty("balance").GetDecimal();

    public static async Task<int> PointsAsync(HttpClient client, Guid memberId) =>
        (await Json(await client.GetAsync("/api/loyalty/ledger"), "read the loyalty ledger")).EnumerateArray()
            .Where(e => e.GetProperty("memberId").GetGuid() == memberId).Sum(e => e.GetProperty("points").GetInt32());
}

/// <summary>
/// The house-charge screen's sales: catalogue lines with their VAT, the way the member paid,
/// a reason for any discount, the points by the club's settings, all through the ledger.
/// </summary>
/// <remarks>
/// The POS screen posted one charge typed in by hand: no lines, no VAT, nothing to say a bill
/// was paid at the table, and a comp was a smaller number with no reason. Each test here rings
/// up a sale and reads back what the house account, the statement's VAT and the points say.
/// </remarks>
public class ClubSalesTests
{
    private static async Task<(HttpClient Client, Guid Org, Guid Member, Guid Negroni, Guid Book)> ShopAsync(CustomWebApplicationFactory factory)
    {
        var (client, _, org) = await Club.CreateAsync(factory);
        var member = await Club.MemberAsync(client, tier: "Gold");
        var negroni = await Club.ItemAsync(client, "MenuItem", "Negroni", 18.50m, 8.1m, "Bar");
        var book = await Club.ItemAsync(client, "Product", "Club history", 40m, 2.6m, "Retail");
        return (client, org, member, negroni, book);
    }

    private static object Sale(Guid member, string tender, params (Guid Item, decimal Quantity)[] lines) => new
    {
        memberId = member, venue = "Bar", tender,
        lines = lines.Select(l => new { catalogItemId = l.Item, quantity = l.Quantity }).ToArray(),
    };

    [Fact]
    public async Task ASaleOnTheHouseAccount_PostsAChargePerVatRate_LeavesItOwing_AndEarnsPointsByTheSettings()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, member, negroni, book) = await ShopAsync(factory);

        var sale = await Club.Json(await client.PostAsync("/api/sales", Body(Sale(member, "HouseAccount", (negroni, 2), (book, 1)))), "ring up the sale");

        sale.GetProperty("total").GetDecimal().Should().Be(77.00m);
        sale.GetProperty("owedAmount").GetDecimal().Should().Be(77.00m);
        sale.GetProperty("paidAmount").GetDecimal().Should().Be(0m);
        // 77 spent, 10 points a franc by default, Gold's 1.5 from the catalogue.
        sale.GetProperty("pointsAwarded").GetInt32().Should().Be(1155);
        sale.GetProperty("lines").EnumerateArray().Select(l => (l.GetProperty("name").GetString(), l.GetProperty("total").GetDecimal(), l.GetProperty("vatRate").GetDecimal()))
            .Should().Equal(("Negroni", 37.00m, 8.1m), ("Club history", 40.00m, 2.6m));

        // One charge per kind and VAT rate, so the statement's VAT summary and the journal see both.
        var charges = await Club.ChargesAsync(client, member);
        charges.Select(c => (c.GetProperty("kind").GetString(), c.GetProperty("amount").GetDecimal(), c.GetProperty("vatRate").GetDecimal(), c.GetProperty("status").GetString()))
            .Should().BeEquivalentTo([("Bar", 37.00m, 8.1m, "Pending"), ("Retail", 40.00m, 2.6m, "Pending")]);
        charges.Single(c => c.GetProperty("kind").GetString() == "Bar").GetProperty("description").GetString().Should().Be("Library Bar: 2 × Negroni");
        (await Club.BalanceAsync(client, member)).Should().Be(-77.00m);
        (await Club.PointsAsync(client, member)).Should().Be(1155);

        var statement = await Club.Json(await client.GetAsync(
            $"/api/members/{member}/statement?from={DateTime.UtcNow.AddDays(-1):O}&to={DateTime.UtcNow.AddDays(1):O}"), "read the statement");
        statement.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("vatRate").GetDecimal()).Should().BeEquivalentTo([8.1m, 2.6m]);
    }

    [Fact]
    public async Task ASalePaidByCardAtTheTill_IsRecordedAsPaid_LeavesTheBalanceAlone_AndStillEarnsPoints()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, member, negroni, book) = await ShopAsync(factory);

        var sale = await Club.Json(await client.PostAsync("/api/sales", Body(Sale(member, "Card", (negroni, 1), (book, 1)))), "ring up the sale paid by card");

        sale.GetProperty("paidAmount").GetDecimal().Should().Be(58.50m);
        sale.GetProperty("owedAmount").GetDecimal().Should().Be(0m);
        sale.GetProperty("paymentIds").GetArrayLength().Should().Be(1);
        (await Club.ChargesAsync(client, member)).Select(c => c.GetProperty("status").GetString()).Should().AllBe("Paid");
        var payments = (await Club.Json(await client.GetAsync($"/api/payments?memberId={member}"), "list payments")).EnumerateArray().ToList();
        payments.Should().ContainSingle();
        payments[0].GetProperty("method").GetString().Should().Be("Card");
        payments[0].GetProperty("amount").GetDecimal().Should().Be(58.50m);
        (await Club.BalanceAsync(client, member)).Should().Be(0m);
        (await Club.PointsAsync(client, member)).Should().Be(877, "58.50 × 10 × 1.5, rounded down");
    }

    [Fact]
    public async Task ADiscountNeedsAReason_AndIsSpreadOverTheLinesKeepingEachLinesVat()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, member, negroni, book) = await ShopAsync(factory);

        var noReason = await Club.Refused(await client.PostAsync("/api/sales", Body(new
        {
            memberId = member, venue = "Dining", tender = "HouseAccount", discountPercent = 10,
            lines = new[] { new { catalogItemId = negroni, quantity = 2m }, new { catalogItemId = book, quantity = 1m } },
        })), HttpStatusCode.BadRequest, "a discount with no reason");
        noReason.Should().Contain("Say why the discount was given");
        (await Club.ChargesAsync(client, member)).Should().BeEmpty("a refused sale posts nothing");

        var sale = await Club.Json(await client.PostAsync("/api/sales", Body(new
        {
            memberId = member, venue = "Dining", tender = "HouseAccount", discountPercent = 10, discountReason = "Birthday",
            lines = new[] { new { catalogItemId = negroni, quantity = 2m }, new { catalogItemId = book, quantity = 1m } },
        })), "a discount with a reason");

        sale.GetProperty("subtotal").GetDecimal().Should().Be(77.00m);
        sale.GetProperty("discount").GetDecimal().Should().Be(7.70m);
        sale.GetProperty("total").GetDecimal().Should().Be(69.30m);
        sale.GetProperty("discountReason").GetString().Should().Be("Birthday");
        sale.GetProperty("lines").EnumerateArray().Select(l => l.GetProperty("discount").GetDecimal()).Sum().Should().Be(7.70m);
        var charges = await Club.ChargesAsync(client, member);
        charges.Sum(c => c.GetProperty("amount").GetDecimal()).Should().Be(69.30m);
        charges.Select(c => c.GetProperty("vatRate").GetDecimal()).Should().BeEquivalentTo([8.1m, 2.6m]);
        charges.Select(c => c.GetProperty("reason").GetString()).Should().AllBe("Discount: Birthday");
        (await Club.PointsAsync(client, member)).Should().Be(1039, "points on what was charged: 69.30 × 15, rounded down");
    }

    [Fact]
    public async Task TheHouseChargeScreenRingsUpTheCatalogueOnly_AtItsPrices()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, member, negroni, _) = await ShopAsync(factory);

        var typed = await Club.Refused(await client.PostAsync("/api/sales", Body(new
        {
            memberId = member, venue = "Bar", tender = "Cash", lines = new object[] { new { name = "Champagne", unitPrice = 1, quantity = 1 } },
        })), HttpStatusCode.BadRequest, "a line typed in by hand");
        typed.Should().Contain("comes from the price catalogue");

        await Club.Json(await client.PutAsync($"/api/catalog/{negroni}", Body(new { isActive = false })), "take the Negroni off sale");
        await Club.Refused(await client.PostAsync("/api/sales", Body(Sale(member, "Cash", (negroni, 1)))), HttpStatusCode.Conflict, "an item off sale");
        await Club.Refused(await client.PostAsync("/api/sales", Body(new { memberId = member, venue = "Bar", tender = "Voucher", lines = Array.Empty<object>() })),
            HttpStatusCode.BadRequest, "no lines");
        (await Club.ChargesAsync(client, member)).Should().BeEmpty();
    }

    [Fact]
    public async Task TheSameIdempotencyKeyPostsTheSaleOnce()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, member, negroni, _) = await ShopAsync(factory);

        async Task<JsonElement> Post()
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/sales") { Content = Body(Sale(member, "HouseAccount", (negroni, 1))) };
            request.Headers.Add("Idempotency-Key", "till-1:sale-42");
            return await Club.Json(await client.SendAsync(request), "ring up the sale");
        }

        var first = await Post();
        var again = await Post();
        again.GetProperty("id").GetGuid().Should().Be(first.GetProperty("id").GetGuid());
        (await Club.ChargesAsync(client, member)).Should().ContainSingle();
        (await Club.BalanceAsync(client, member)).Should().Be(-18.50m);
        (await Club.PointsAsync(client, member)).Should().Be(277);
    }

    [Fact]
    public async Task ATabWithItemsAtTwoVatRates_ClosesIntoAChargePerRate_AndStillPostsOnce()
    {
        // A tab used to post one charge, with no VAT rate unless every item had the same one,
        // so a tab of drinks and a book left both out of the statement's VAT summary.
        using var factory = new CustomWebApplicationFactory();
        var (client, _, member, negroni, book) = await ShopAsync(factory);
        var tab = (await Club.Json(await client.PostAsync("/api/tabs", Body(new { memberId = member, venue = "Bar" })), "open a tab")).GetProperty("id").GetGuid();
        await Club.Json(await client.PostAsync($"/api/tabs/{tab}/items", Body(new { catalogItemId = negroni, quantity = 2 })), "two Negronis");
        await Club.Json(await client.PostAsync($"/api/tabs/{tab}/items", Body(new { catalogItemId = book, quantity = 1 })), "the book");
        await Club.Json(await client.PostAsync($"/api/tabs/{tab}/items", Body(new { name = "Olives", quantity = 1, unitPrice = 6.5 })), "olives typed in");

        var closed = await Club.Json(await client.PostAsync($"/api/tabs/{tab}/close", null), "close the tab");
        closed.GetProperty("total").GetDecimal().Should().Be(83.50m);

        var charges = await Club.ChargesAsync(client, member);
        charges.Select(c => (c.GetProperty("amount").GetDecimal(), c.TryGetProperty("vatRate", out var vat) ? vat.GetDecimal() : (decimal?)null))
            .Should().BeEquivalentTo(new (decimal, decimal?)[] { (37.00m, 8.1m), (40.00m, 2.6m), (6.50m, null) });
        charges.Count(c => c.TryGetProperty("tabId", out _)).Should().Be(1, "the tab's own key is on one charge, the guard against posting it twice");
        charges.Single(c => c.TryGetProperty("tabId", out _)).GetProperty("id").GetGuid().Should().Be(closed.GetProperty("chargeId").GetGuid());
        (await Club.BalanceAsync(client, member)).Should().Be(-83.50m);
        (await Club.PointsAsync(client, member)).Should().Be(1252, "83.50 × 10 × 1.5, rounded down, once");

        await Club.Json(await client.PostAsync($"/api/tabs/{tab}/close", null), "close it again");
        (await Club.ChargesAsync(client, member)).Should().HaveCount(3);
        (await Club.BalanceAsync(client, member)).Should().Be(-83.50m);
    }

    [Fact]
    public async Task TheFrontDeskRingsUpSales_ASalesRepAndAnAccountantCannot()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, org, member, negroni, _) = await ShopAsync(factory);
        var desk = await Club.JoinAsync(factory, owner, org, "desk@example.ch", "front_desk");
        var rep = await Club.JoinAsync(factory, owner, org, "rep@example.ch", "sales_rep");
        var accountant = await Club.JoinAsync(factory, owner, org, "books@example.ch", "read_only_accountant");

        await Club.Json(await desk.PostAsync("/api/sales", Body(Sale(member, "Twint", (negroni, 1)))), "the front desk rings up a sale");
        await Club.Refused(await rep.PostAsync("/api/sales", Body(Sale(member, "Cash", (negroni, 1)))), HttpStatusCode.Forbidden, "a sales rep");
        await Club.Refused(await accountant.PostAsync("/api/sales", Body(Sale(member, "Cash", (negroni, 1)))), HttpStatusCode.Forbidden, "an accountant");
        (await Club.Json(await accountant.GetAsync($"/api/sales?memberId={member}"), "the accountant reads the sales")).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task AnotherWorkspaceCanNeitherSellToTheMemberNorSeeTheSales()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, member, negroni, _) = await ShopAsync(factory);
        await Club.Json(await client.PostAsync("/api/sales", Body(Sale(member, "HouseAccount", (negroni, 1)))), "ring up the sale");
        var (other, _, _) = await Club.CreateAsync(factory, "other@example.ch", "Other club");
        var otherItem = await Club.ItemAsync(other, "MenuItem", "Spritz", 12m, 8.1m, "Bar");

        await Club.Refused(await other.PostAsync("/api/sales", Body(Sale(member, "HouseAccount", (otherItem, 1)))), HttpStatusCode.NotFound,
            "a sale to another workspace's member");
        await Club.Refused(await other.PostAsync("/api/sales", Body(Sale(Guid.NewGuid(), "HouseAccount", (negroni, 1)))), HttpStatusCode.NotFound,
            "another workspace's menu item");
        (await Club.Json(await other.GetAsync("/api/sales"), "the other workspace's sales")).GetArrayLength().Should().Be(0);
        (await Club.Json(await other.GetAsync($"/api/sales?memberId={member}"), "the member's sales from the other workspace")).GetArrayLength().Should().Be(0);
        (await Club.ChargesAsync(client, member)).Should().ContainSingle();
    }
}
