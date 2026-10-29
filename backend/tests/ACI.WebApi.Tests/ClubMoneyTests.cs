using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The club's money records on the server: charges, payments, gift cards, tabs and
/// subscriptions, through the same HTTP calls the screens make.
/// </summary>
/// <remarks>
/// These records are JSON documents whose shape the browser used to own, and the
/// server took the browser's word for the parts that are money: a status, a balance,
/// a paid amount. Every test here is a request a front-desk account could send with
/// curl, and the answer the club's books need.
/// </remarks>
public class ClubMoneyTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<string> Refused(HttpResponseMessage res, HttpStatusCode status, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(status, $"{step} should be refused — got {(int)res.StatusCode}: {body}");
        return body;
    }

    private static async Task<(HttpClient Client, Guid UserId, Guid Org)> ClubAsync(CustomWebApplicationFactory factory, string email = "kia@pavillon46.ch")
    {
        var (client, userId) = await SignInAsync(factory, email);
        var org = await CreateAsync(factory, userId, "Pavillon46");
        client.In(org);
        return (client, userId, org);
    }

    private static async Task<Guid> MemberAsync(HttpClient client, string first = "Jean", string tier = "Gold") =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Dupont", email = $"{first.ToLowerInvariant()}@example.ch", tier, status = "Active",
        })), "add member")).GetProperty("id").GetGuid();

    private static async Task<decimal> BalanceAsync(HttpClient client, Guid memberId) =>
        (await Json(await client.GetAsync("/api/members"), "list members")).EnumerateArray()
            .Single(m => m.GetProperty("id").GetGuid() == memberId).GetProperty("houseAccountBalance").GetDecimal();

    // ── the server owns the money fields ────────────────────────────────────

    [Fact]
    public async Task AChargeArrivesUnpaid_WhateverTheRequestSays()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client);

        var charge = await Json(await client.PostAsync("/api/charges", Body(new
        {
            memberId, memberName = "Someone Else", kind = "Bar", description = "Bar tab", amount = 500,
            status = "Paid", paidAmount = 500, paidAtUtc = DateTime.UtcNow, postedAtUtc = "2020-01-01T00:00:00Z",
        })), "post a charge marked paid");

        charge.GetProperty("status").GetString().Should().Be("Pending", "a charge is paid by a payment, not by saying so");
        charge.GetProperty("paidAmount").GetDecimal().Should().Be(0);
        charge.TryGetProperty("paidAtUtc", out _).Should().BeFalse();
        charge.GetProperty("memberName").GetString().Should().Be("Jean Dupont", "the name comes from the member");
        DateTime.Parse(charge.GetProperty("postedAtUtc").GetString()!).ToUniversalTime()
            .Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
        (await BalanceAsync(client, memberId)).Should().Be(-500, "the member owes it until someone pays");
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(0)]
    [InlineData(1_000_000_000)]
    public async Task AChargeMustBeForSomeMoney(decimal amount)
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client);

        var body = await Refused(await client.PostAsync("/api/charges", Body(new { memberId, kind = "Dining", description = "Dinner", amount })),
            HttpStatusCode.BadRequest, $"a charge of {amount}");

        body.Should().Contain("more than 0");
        (await BalanceAsync(client, memberId)).Should().Be(0, "a negative charge used to wipe a balance");
        (await Json(await client.GetAsync("/api/charges"), "charges")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task MoneyThatIsNotANumberOrNotAMembersIsRefused()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client);

        await Refused(await client.PostAsync("/api/charges", Body(new { memberId, kind = "Dining", description = "Dinner", amount = "lots" })),
            HttpStatusCode.BadRequest, "an amount that is not a number");
        await Refused(await client.PostAsync("/api/charges", Body(new { memberId = Guid.NewGuid(), kind = "Dining", description = "Dinner", amount = 20 })),
            HttpStatusCode.NotFound, "a charge for nobody in this club");
        await Refused(await client.PostAsync("/api/charges", Body(new { memberId, kind = "Refund", description = "Dinner", amount = 20 })),
            HttpStatusCode.BadRequest, "a kind of charge the club does not have");
        await Refused(await client.PostAsync("/api/gift-cards", Body(new { purchaserName = "Ana", recipientName = "Bea", faceValue = -50 })),
            HttpStatusCode.BadRequest, "a gift card worth less than nothing");
        await Refused(await client.PostAsync("/api/subscriptions", Body(new { memberId, name = "Locker", amount = -100, cadence = "Monthly" })),
            HttpStatusCode.BadRequest, "negative dues");
        await Refused(await client.PostAsync("/api/donations", Body(new { memberId, cause = "General", amount = 0 })),
            HttpStatusCode.BadRequest, "a donation of nothing");
    }

    [Fact]
    public async Task AGiftCardHoldsItsFaceValueAndTheServersCode()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);

        var card = await Json(await client.PostAsync("/api/gift-cards", Body(new
        {
            purchaserName = "Ana", recipientName = "Bea", faceValue = 10,
            remainingValue = 5000, code = "GIFT-MINE-MINE-MINE", status = "Redeemed",
        })), "issue a gift card");

        card.GetProperty("remainingValue").GetDecimal().Should().Be(10, "a card for 10 used to be issued with 5,000 on it");
        card.GetProperty("status").GetString().Should().Be("Active");
        card.GetProperty("code").GetString().Should().StartWith("GIFT-").And.NotBe("GIFT-MINE-MINE-MINE", "the code is the server's");
        await Refused(await client.PostAsync("/api/gift-cards", Body(new { purchaserName = "Ana", recipientName = "Bea", faceValue = 10, expiresAtUtc = DateTime.UtcNow.AddDays(-1) })),
            HttpStatusCode.BadRequest, "a card that has already expired");
    }

    [Fact]
    public async Task ATabOpensEmptyAtTheMembersOwnTier()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client, tier: "Gold");

        var tab = await Json(await client.PostAsync("/api/tabs", Body(new
        {
            memberId, venue = "Bar", memberTier = "Platinum", status = "Closed", total = 999, pointsAwarded = 5000,
            items = new[] { new { id = Guid.NewGuid(), name = "Champagne", quantity = 1, unitPrice = -500 } },
        })), "open a tab");

        tab.GetProperty("memberTier").GetString().Should().Be("Gold", "the tier sets the points multiplier, so it is the member's");
        tab.GetProperty("status").GetString().Should().Be("Open");
        tab.GetProperty("items").GetArrayLength().Should().Be(0);
        tab.GetProperty("total").GetDecimal().Should().Be(0);
        tab.TryGetProperty("pointsAwarded", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ASubscriptionEditChangesOnlyWhatAPersonMay()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client);
        var other = await MemberAsync(client, "Marie");
        var sub = await Json(await client.PostAsync("/api/subscriptions", Body(new
        {
            memberId, name = "Locker", amount = 25, cadence = "Quarterly", status = "Cancelled", lastChargedAtUtc = DateTime.UtcNow,
        })), "add subscription");
        sub.GetProperty("status").GetString().Should().Be("Active");
        sub.TryGetProperty("lastChargedAtUtc", out _).Should().BeFalse();
        var id = sub.GetProperty("id").GetString();

        await Refused(await client.PutAsync($"/api/subscriptions/{id}", Body(new { amount = -100 })), HttpStatusCode.BadRequest, "negative dues");
        await Refused(await client.PutAsync($"/api/subscriptions/{id}", Body(new { status = "PastDue" })), HttpStatusCode.BadRequest, "a status only billing sets");
        var edited = await Json(await client.PutAsync($"/api/subscriptions/{id}", Body(new
        {
            status = "Paused", amount = 30, memberId = other, startedAtUtc = "2001-01-01T00:00:00Z", lastChargedAtUtc = DateTime.UtcNow,
        })), "pause and re-price");

        edited.GetProperty("status").GetString().Should().Be("Paused");
        edited.GetProperty("amount").GetDecimal().Should().Be(30);
        edited.GetProperty("memberId").GetString().Should().Be(memberId.ToString(), "a PUT cannot move someone's dues to another member");
        edited.GetProperty("startedAtUtc").GetString().Should().Be(sub.GetProperty("startedAtUtc").GetString());
        edited.TryGetProperty("lastChargedAtUtc", out _).Should().BeFalse();
    }

    // ── gift cards ───────────────────────────────────────────────────────────

    private static async Task<string> CardAsync(HttpClient client, decimal faceValue, DateTime? expiresAtUtc = null) =>
        (await Json(await client.PostAsync("/api/gift-cards", Body(new
        {
            purchaserName = "Ana", recipientName = "Bea", faceValue, expiresAtUtc,
        })), "issue a gift card")).GetProperty("id").GetString()!;

    private static HttpRequestMessage Redeem(string cardId, decimal amount, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/gift-cards/{cardId}/redeem")
        {
            Content = Body(new { amount, redeemedByName = "Front desk" }),
        };
        if (key != null) request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    private static async Task<decimal> RemainingAsync(HttpClient client, string cardId) =>
        (await Json(await client.GetAsync("/api/gift-cards"), "list cards")).EnumerateArray()
            .Single(c => c.GetProperty("id").GetString() == cardId).GetProperty("remainingValue").GetDecimal();

    [Fact]
    public async Task TwoRedeemsAtOnceCannotSpendOneBalanceTwice()
    {
        using var factory = new RaceFactory();
        var (client, _, _) = await ClubAsync(factory);
        var card = await CardAsync(client, 50);
        // Both terminals read the card, with 50 on it, before either saves.
        factory.Gate.Arm("gift-cards");

        var answers = await Task.WhenAll(client.SendAsync(Redeem(card, 50)), client.SendAsync(Redeem(card, 50)));

        answers.Select(a => a.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict },
            "the second redeem read a balance that was already spent");
        (await answers.Single(a => a.StatusCode == HttpStatusCode.Conflict).Content.ReadAsStringAsync())
            .Should().Contain("at the same moment");
        (await Json(await client.GetAsync("/api/gift-cards/redemptions"), "redemptions")).GetArrayLength()
            .Should().Be(1, "100 used to be redeemed from a 50 card");
        (await RemainingAsync(client, card)).Should().Be(0);
    }

    [Fact]
    public async Task ARetriedRedeemSpendsTheCardOnce()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var card = await CardAsync(client, 50);
        var key = Guid.NewGuid().ToString();

        var first = await Json(await client.SendAsync(Redeem(card, 20, key)), "redeem");
        var retry = await Json(await client.SendAsync(Redeem(card, 20, key)), "the same redeem, sent again after a timeout");

        retry.GetProperty("id").GetString().Should().Be(first.GetProperty("id").GetString(), "it is the same redemption");
        (await RemainingAsync(client, card)).Should().Be(30);
        await Json(await client.SendAsync(Redeem(card, 20, Guid.NewGuid().ToString())), "a new redeem has a new key");
        (await RemainingAsync(client, card)).Should().Be(10);
        (await Json(await client.GetAsync("/api/gift-cards/redemptions"), "redemptions")).GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task AnExpiredCardIsNeitherRedeemableNorOutstanding()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var card = await CardAsync(client, 100, DateTime.UtcNow.AddDays(30));
        // Time passes: the card's expiry is now behind it, and nothing has saved that.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = db.ClubDocuments.Single(d => d.Id == Guid.Parse(card));
            var payload = JsonNode.Parse(row.Payload)!.AsObject();
            payload["expiresAtUtc"] = DateTime.UtcNow.AddDays(-1).ToString("o");
            row.Payload = payload.ToJsonString();
            await db.SaveChangesAsync();
        }

        var listed = (await Json(await client.GetAsync("/api/gift-cards"), "list cards"))[0];
        listed.GetProperty("status").GetString().Should().Be("Expired", "the outstanding-balance figure counts Active cards only");

        var body = await Refused(await client.SendAsync(Redeem(card, 40)), HttpStatusCode.Conflict, "redeeming an expired card");
        body.Should().Contain("expired on");
        (await RemainingAsync(client, card)).Should().Be(100);
        (await Json(await client.GetAsync("/api/gift-cards/redemptions"), "redemptions")).GetArrayLength().Should().Be(0);
    }
}
