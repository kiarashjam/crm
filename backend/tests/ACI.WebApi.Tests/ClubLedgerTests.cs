using System.Net;
using System.Text.Json;
using ACI.Application.Services;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The club's money ledger over HTTP: balances at a date, statements, refunds, payments of
/// several charges, house credit, balances set by hand, gift card top-ups and expiry,
/// donations voided instead of deleted, and one club unable to touch another's money.
/// </summary>
/// <remarks>
/// Entries are only ever added: every test here reads the result back the way a screen or
/// a statement would, and checks that what was taken back is still on the record.
/// </remarks>
public class ClubLedgerTests
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

    private static async Task<(HttpClient Client, Guid UserId, Guid Org)> ClubAsync(CustomWebApplicationFactory factory, string email = "manager@example.ch")
    {
        var (client, userId) = await SignInAsync(factory, email);
        var org = await CreateAsync(factory, userId, "Pavillon46");
        client.In(org);
        return (client, userId, org);
    }

    private static async Task<HttpClient> JoinAsync(CustomWebApplicationFactory factory, HttpClient owner, Guid org, string email, string preset)
    {
        var (client, userId) = await SignInAsync(factory, email);
        await AddMemberAsync(factory, org, userId, OrgMemberRole.Member);
        await Json(await owner.PutAsync($"/api/organizations/{org}/members/{userId}/permissions", Body(new { preset })), $"make {email} {preset}");
        return client.In(org);
    }

    private static async Task<Guid> MemberAsync(HttpClient client, string first = "Jean", decimal? houseAccountBalance = null) =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Dupont", email = $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active", houseAccountBalance,
        })), "add member")).GetProperty("id").GetGuid();

    private static async Task<string> ChargeAsync(HttpClient client, Guid memberId, decimal amount, string description = "Dinner") =>
        (await Json(await client.PostAsync("/api/charges", Body(new { memberId, kind = "Dining", description, amount })), "post charge"))
            .GetProperty("id").GetString()!;

    private static async Task<string> PayAsync(HttpClient client, string chargeId, decimal amount, string method = "CardOnFile") =>
        (await Json(await client.PostAsync("/api/payments", Body(new { amount, method, chargeId })), "record payment"))
            .GetProperty("id").GetString()!;

    private static async Task<decimal> BalanceAsync(HttpClient client, Guid memberId, DateTime? asOf = null) =>
        (await Json(await client.GetAsync($"/api/members/{memberId}/balance{(asOf is { } a ? $"?asOf={Uri.EscapeDataString(a.ToString("o"))}" : "")}"), "read balance"))
            .GetProperty("balance").GetDecimal();

    private static async Task<decimal> ListedBalanceAsync(HttpClient client, Guid memberId) =>
        (await Json(await client.GetAsync("/api/members"), "list members")).EnumerateArray()
            .Single(m => m.GetProperty("id").GetGuid() == memberId).GetProperty("houseAccountBalance").GetDecimal();

    private static async Task<JsonElement> ChargeOfAsync(HttpClient client, string chargeId) =>
        (await Json(await client.GetAsync("/api/charges"), "list charges")).EnumerateArray().Single(c => c.GetProperty("id").GetString() == chargeId);

    private static async Task<JsonElement> PaymentOfAsync(HttpClient client, string paymentId) =>
        (await Json(await client.GetAsync("/api/payments"), "list payments")).EnumerateArray().Single(p => p.GetProperty("id").GetString() == paymentId);

    // ── balances and statements ──────────────────────────────────────────────

    [Fact]
    public async Task ABalanceAtADateCountsTheEntriesUpToThatDate_AndAStatementShowsEachWithTheBalanceAfterIt()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var before = DateTime.UtcNow.AddSeconds(-1);
        var memberId = await MemberAsync(client, houseAccountBalance: -100);
        await Task.Delay(20);
        var afterOpening = DateTime.UtcNow;
        await Task.Delay(20);
        var charge = await ChargeAsync(client, memberId, 50, "Wine dinner");
        await PayAsync(client, charge, 30, "Cash");

        (await BalanceAsync(client, memberId)).Should().Be(-120);
        (await BalanceAsync(client, memberId, afterOpening)).Should().Be(-100, "the dinner and its payment came after");
        (await BalanceAsync(client, memberId, before)).Should().Be(0, "the member had no account yet");
        (await ListedBalanceAsync(client, memberId)).Should().Be(-120, "the members list shows what the ledger adds up to");

        var statement = await Json(await client.GetAsync(
            $"/api/members/{memberId}/statement?from={Uri.EscapeDataString(before.ToString("o"))}&to={Uri.EscapeDataString(DateTime.UtcNow.AddMinutes(1).ToString("o"))}"), "statement");
        statement.GetProperty("openingBalance").GetDecimal().Should().Be(0);
        statement.GetProperty("closingBalance").GetDecimal().Should().Be(-120);
        var lines = statement.GetProperty("lines").EnumerateArray()
            .Select(l => (l.GetProperty("entryType").GetString(), l.GetProperty("description").GetString(), l.GetProperty("debit").GetDecimal(),
                l.GetProperty("credit").GetDecimal(), l.GetProperty("balance").GetDecimal()))
            .ToList();
        lines.Should().Equal(
            ("Opening", "Opening balance", 100m, 0m, -100m),
            ("Charge", "Wine dinner", 50m, 0m, -150m),
            ("Payment", "Payment (Cash)", 0m, 30m, -120m));

        var later = await Json(await client.GetAsync(
            $"/api/members/{memberId}/statement?from={Uri.EscapeDataString(afterOpening.ToString("o"))}&to={Uri.EscapeDataString(DateTime.UtcNow.AddMinutes(1).ToString("o"))}"), "statement after the opening");
        later.GetProperty("openingBalance").GetDecimal().Should().Be(-100, "a period's opening balance is everything before it");
        later.GetProperty("lines").GetArrayLength().Should().Be(2);
        await Refused(await client.GetAsync($"/api/members/{memberId}/statement?from=2026-02-01&to=2026-01-01"), HttpStatusCode.BadRequest, "a period that ends before it starts");
    }

    // ── payments ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task OnePaymentCanSettleSeveralCharges_ButOnlyOneMembersAndNeverMoreThanTheyOwe()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client);
        var other = await MemberAsync(client, "Marie");
        var dinner = await ChargeAsync(client, memberId, 30);
        var dues = await ChargeAsync(client, memberId, 70, "Dues");
        var hers = await ChargeAsync(client, other, 10);

        await Refused(await client.PostAsync("/api/payments", Body(new { amount = 100, method = "Wire", allocations = new[] { new { chargeId = dinner, amount = 30 }, new { chargeId = dues, amount = 60 } } })),
            HttpStatusCode.BadRequest, "allocations that do not add up to the payment");
        await Refused(await client.PostAsync("/api/payments", Body(new { amount = 40, method = "Wire", allocations = new[] { new { chargeId = dinner, amount = 30 }, new { chargeId = hers, amount = 10 } } })),
            HttpStatusCode.BadRequest, "one payment for two members' charges");
        (await Refused(await client.PostAsync("/api/payments", Body(new { amount = 110, method = "Wire", allocations = new[] { new { chargeId = dinner, amount = 40 }, new { chargeId = dues, amount = 70 } } })),
            HttpStatusCode.BadRequest, "more than the dinner owes")).Should().Contain("30.00 left to pay");

        var payment = await Json(await client.PostAsync("/api/payments", Body(new
        {
            amount = 100, method = "Wire", reference = "Bank 2611", allocations = new[] { new { chargeId = dinner, amount = 30 }, new { chargeId = dues, amount = 70 } },
        })), "pay both");

        payment.GetProperty("appliedToChargeIds").GetArrayLength().Should().Be(2);
        (await ChargeOfAsync(client, dinner)).GetProperty("status").GetString().Should().Be("Paid");
        (await ChargeOfAsync(client, dues)).GetProperty("status").GetString().Should().Be("Paid");
        (await BalanceAsync(client, memberId)).Should().Be(0);
    }

    [Fact]
    public async Task ARefundGivesMoneyBack_TheChargeOwesItAgain_AndTheRecordsStay()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var memberId = await MemberAsync(owner);
        var charge = await ChargeAsync(owner, memberId, 100);
        var payment = await PayAsync(owner, charge, 100);
        var frontDesk = await JoinAsync(factory, owner, org, "desk@pavillon46.ch", "front_desk");

        await Refused(await frontDesk.PostAsync($"/api/payments/{payment}/refund", Body(new { amount = 40, reason = "Overcharged" })), HttpStatusCode.Forbidden, "the front desk refunding");
        await Refused(await owner.PostAsync($"/api/payments/{payment}/refund", Body(new { amount = 40 })), HttpStatusCode.BadRequest, "a refund with no reason");
        var refunded = await Json(await owner.PostAsync($"/api/payments/{payment}/refund", Body(new { amount = 40, reason = "Charged twice on the card" })), "refund part");

        refunded.GetProperty("status").GetString().Should().Be("PartiallyRefunded");
        refunded.GetProperty("refundedAmount").GetDecimal().Should().Be(40);
        refunded.GetProperty("amount").GetDecimal().Should().Be(100, "the payment stays as it was received");
        var owing = await ChargeOfAsync(owner, charge);
        owing.GetProperty("paidAmount").GetDecimal().Should().Be(60);
        owing.GetProperty("status").GetString().Should().Be("PartiallyPaid");
        (await BalanceAsync(owner, memberId)).Should().Be(-40);
        (await Refused(await owner.PostAsync($"/api/payments/{payment}/refund", Body(new { amount = 70, reason = "Again" })), HttpStatusCode.BadRequest, "refunding more than is left"))
            .Should().Contain("60.00 of this payment is left to refund");

        // The rest is voided: the void takes back what the refund left.
        var voided = await Json(await owner.PostAsync($"/api/payments/{payment}/void", Body(new { reason = "Card disputed" })), "void the rest");
        voided.GetProperty("status").GetString().Should().Be("Voided");
        (await ChargeOfAsync(owner, charge)).GetProperty("paidAmount").GetDecimal().Should().Be(0);
        (await BalanceAsync(owner, memberId)).Should().Be(-100);
        await Refused(await owner.PostAsync($"/api/payments/{payment}/refund", Body(new { amount = 1, reason = "More" })), HttpStatusCode.Conflict, "refunding a voided payment");

        var trail = (await Json(await owner.GetAsync($"/api/audit?entityType=payment&entityId={payment}"), "payment trail")).EnumerateArray()
            .Select(r => (r.GetProperty("action").GetString(), r.GetProperty("summary").GetString())).ToList();
        trail.Should().Equal(("voided", "Voided: Card disputed"), ("updated", "Refunded 40.00: Charged twice on the card"), ("created", "Payment of 100.00 recorded"));
    }

    [Fact]
    public async Task HouseCreditPaysACharge_WithoutCountingAsNewMoney()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        // Brought over 50 in credit.
        var memberId = await MemberAsync(client, houseAccountBalance: 50);
        var lunch = await ChargeAsync(client, memberId, 30, "Lunch");
        (await BalanceAsync(client, memberId)).Should().Be(20);

        await Json(await client.PostAsync("/api/payments", Body(new { amount = 30, method = "AppliedCredit", chargeId = lunch })), "pay the lunch from credit");

        (await ChargeOfAsync(client, lunch)).GetProperty("status").GetString().Should().Be("Paid");
        (await BalanceAsync(client, memberId)).Should().Be(20, "house credit used to count as a payment, so the member kept the 50 and the lunch was paid");
        (await Json(await client.GetAsync("/api/payments"), "payments")).GetArrayLength().Should().Be(0, "no money came in");
        var dinner = await ChargeAsync(client, memberId, 40);
        (await Refused(await client.PostAsync("/api/payments", Body(new { amount = 40, method = "AppliedCredit", chargeId = dinner })), HttpStatusCode.BadRequest, "more than the credit"))
            .Should().Contain("has 20.00 of house credit");
    }

    [Fact]
    public async Task ARetriedPostWithTheSameIdempotencyKeyPostsOnce()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client);
        HttpRequestMessage Post(string path, object body, string key)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = Body(body) };
            request.Headers.Add("Idempotency-Key", key);
            return request;
        }

        var first = await Json(await client.SendAsync(Post("/api/charges", new { memberId, kind = "Bar", description = "Bar tab", amount = 45 }, "pos-77")), "post at the till");
        var retry = await Json(await client.SendAsync(Post("/api/charges", new { memberId, kind = "Bar", description = "Bar tab", amount = 45 }, "pos-77")), "the till retries after a timeout");
        retry.GetProperty("id").GetString().Should().Be(first.GetProperty("id").GetString());
        var paid = await Json(await client.SendAsync(Post("/api/payments", new { amount = 45, method = "Cash", chargeId = first.GetProperty("id").GetString() }, "pay-77")), "pay");
        var paidAgain = await Json(await client.SendAsync(Post("/api/payments", new { amount = 45, method = "Cash", chargeId = first.GetProperty("id").GetString() }, "pay-77")), "pay again with the same key");

        paidAgain.GetProperty("id").GetString().Should().Be(paid.GetProperty("id").GetString());
        (await Json(await client.GetAsync("/api/charges"), "charges")).GetArrayLength().Should().Be(1);
        (await Json(await client.GetAsync("/api/payments"), "payments")).GetArrayLength().Should().Be(1);
        (await BalanceAsync(client, memberId)).Should().Be(0);
    }

    // ── balances set by hand ─────────────────────────────────────────────────

    [Fact]
    public async Task ABalanceSetByHandIsAnEntryOfItsOwn_AndTakesTheMoneyPermission()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var memberId = await MemberAsync(owner);
        var director = await JoinAsync(factory, owner, org, "director@pavillon46.ch", "membership_director");

        (await Refused(await director.PutAsync($"/api/members/{memberId}", Body(new { houseAccountBalance = 500 })), HttpStatusCode.Forbidden, "a membership director setting a balance"))
            .Should().Contain("set a member's house balance");
        await Refused(await director.PostAsync("/api/members", Body(new { firstName = "Luc", lastName = "Martin", email = "luc@example.ch", houseAccountBalance = -80 })),
            HttpStatusCode.Forbidden, "a membership director opening a member with a balance");
        await Json(await director.PutAsync($"/api/members/{memberId}", Body(new { phone = "+41 21 555 00 00" })), "a membership director editing the rest");
        (await BalanceAsync(owner, memberId)).Should().Be(0);

        await Json(await owner.PutAsync($"/api/members/{memberId}", Body(new { houseAccountBalance = 25 })), "the owner corrects the balance");

        (await BalanceAsync(owner, memberId)).Should().Be(25);
        var statement = await Json(await owner.GetAsync(
            $"/api/members/{memberId}/statement?from={Uri.EscapeDataString(DateTime.UtcNow.AddDays(-1).ToString("o"))}&to={Uri.EscapeDataString(DateTime.UtcNow.AddMinutes(1).ToString("o"))}"), "statement");
        var line = statement.GetProperty("lines").EnumerateArray().Single();
        line.GetProperty("entryType").GetString().Should().Be("Adjustment");
        line.GetProperty("description").GetString().Should().Be("House balance changed by hand from 0.00 to 25.00");
        line.GetProperty("credit").GetDecimal().Should().Be(25);
    }

    // ── gift cards ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ACardCanBeToppedUpAndFoundByItsCode()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var card = await Json(await client.PostAsync("/api/gift-cards", Body(new { purchaserName = "Ana", recipientName = "Bea", faceValue = 50 })), "issue");
        var id = card.GetProperty("id").GetString();
        var code = card.GetProperty("code").GetString()!;

        var topped = await Json(await client.PostAsync($"/api/gift-cards/{id}/top-up", Body(new { amount = 25, reason = "Birthday" })), "top up");
        topped.GetProperty("remainingValue").GetDecimal().Should().Be(75);
        topped.GetProperty("toppedUpValue").GetDecimal().Should().Be(25);
        topped.GetProperty("faceValue").GetDecimal().Should().Be(50, "what it was issued for stays");

        var found = await Json(await client.GetAsync($"/api/gift-cards/lookup?code={Uri.EscapeDataString(code.ToLowerInvariant())}"), "look the card up by its code");
        found.GetProperty("id").GetString().Should().Be(id);
        found.GetProperty("remainingValue").GetDecimal().Should().Be(75);
        await Refused(await client.GetAsync("/api/gift-cards/lookup?code=GIFT-NOPE-NOPE-NOPE"), HttpStatusCode.NotFound, "a code nobody has");

        await Json(await client.PostAsync($"/api/gift-cards/{id}/void", Body(new { reason = "Lost" })), "void");
        await Refused(await client.PostAsync($"/api/gift-cards/{id}/top-up", Body(new { amount = 10 })), HttpStatusCode.Conflict, "topping up a voided card");
        var trail = (await Json(await client.GetAsync($"/api/audit?entityType=gift_card&entityId={id}"), "card trail")).EnumerateArray()
            .Select(r => r.GetProperty("summary").GetString()).ToList();
        trail.Should().Equal("Voided: Lost (75.00 written off)", "Topped up 25.00, 75.00 left", "Gift card issued for 50.00");
    }

    [Fact]
    public async Task AnExpiredCardIsWrittenOffOnce_DatedAtItsExpiry()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, org) = await ClubAsync(factory);
        var id = Guid.Parse((await Json(await client.PostAsync("/api/gift-cards", Body(new { purchaserName = "Ana", recipientName = "Bea", faceValue = 100, expiresAtUtc = DateTime.UtcNow.AddDays(30) })), "issue"))
            .GetProperty("id").GetString()!);
        // Time passes: the card was bought ten days ago and expired two days ago.
        var expiry = DateTime.UtcNow.AddDays(-2);
        using (var scope = factory.Services.CreateScope())
        {
            using var db = scope.ServiceProvider.EveryWorkspace();
            var row = db.ClubGiftCards.Single(c => c.Id == id);
            row.ExpiresAtUtc = expiry;
            row.IssuedAtUtc = DateTime.UtcNow.AddDays(-10);
            db.ClubGiftCardTransactions.Single(t => t.GiftCardId == id).AtUtc = row.IssuedAtUtc;
            await db.SaveChangesAsync();
        }

        int expired, again;
        using (var scope = factory.Services.CreateScope())
            expired = await scope.ServiceProvider.GetRequiredService<IClubLedger>().ExpireGiftCardsAsync(DateTime.UtcNow);
        using (var scope = factory.Services.CreateScope())
            again = await scope.ServiceProvider.GetRequiredService<IClubLedger>().ExpireGiftCardsAsync(DateTime.UtcNow);

        expired.Should().Be(1);
        again.Should().Be(0, "a card expires once");
        var card = (await Json(await client.GetAsync("/api/gift-cards"), "cards"))[0];
        card.GetProperty("status").GetString().Should().Be("Expired");
        card.GetProperty("remainingValue").GetDecimal().Should().Be(0);
        card.GetProperty("expiredValue").GetDecimal().Should().Be(100);
        using (var scope = factory.Services.CreateScope())
        {
            // As a request in the club would: a scope acts for no workspace until told to.
            using var acting = scope.ServiceProvider.GetRequiredService<ACI.Application.Interfaces.ITenantContext>().ActFor(org, null);
            var ledger = scope.ServiceProvider.GetRequiredService<IClubLedger>();
            (await ledger.GiftCardBalanceAsync(org, id, expiry.AddHours(-1))).Value.Should().Be(100, "the day before it expired, the full value was owed");
            (await ledger.GiftCardBalanceAsync(org, id, expiry.AddHours(1))).Value.Should().Be(0);
        }
    }

    // ── donations ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ADonationIsVoidedWithAReason_NeverDeleted()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var memberId = await MemberAsync(owner);
        var id = (await Json(await owner.PostAsync("/api/donations", Body(new { memberId, cause = "Scholarship", amount = 75 })), "record"))
            .GetProperty("id").GetString();
        var director = await JoinAsync(factory, owner, org, "finance@pavillon46.ch", "sales_rep");

        (await Refused(await owner.DeleteAsync($"/api/donations/{id}"), HttpStatusCode.BadRequest, "deleting a donation")).Should().Contain("can't be deleted");
        await Refused(await director.PostAsync($"/api/donations/{id}/void", Body(new { reason = "Entered twice" })), HttpStatusCode.Forbidden, "voiding without money.void");
        await Refused(await owner.PostAsync($"/api/donations/{id}/void", Body(new { reason = " " })), HttpStatusCode.BadRequest, "a void with no reason");
        var voided = await Json(await owner.PostAsync($"/api/donations/{id}/void", Body(new { reason = "Entered twice" })), "void");

        voided.GetProperty("status").GetString().Should().Be("Voided");
        voided.GetProperty("voidReason").GetString().Should().Be("Entered twice");
        var listed = (await Json(await owner.GetAsync("/api/donations"), "donations")).EnumerateArray().Should().ContainSingle().Subject;
        listed.GetProperty("amount").GetDecimal().Should().Be(75, "the donation stays on record");
        await Refused(await owner.PostAsync($"/api/donations/{id}/void", Body(new { reason = "Again" })), HttpStatusCode.Conflict, "voiding twice");
        await Refused(await owner.PostAsync($"/api/donations/{id}/acknowledge", null), HttpStatusCode.Conflict, "thanking for a voided gift");
    }

    // ── subscriptions and tabs ───────────────────────────────────────────────

    [Fact]
    public async Task ASubscriptionThatBilledIsCancelledNotDeleted_AndAPeriodIsBilledOnce()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client);
        var sub = await Json(await client.PostAsync("/api/subscriptions", Body(new { memberId, name = "Gold dues", amount = 299, cadence = "Monthly" })), "add subscription");
        var id = sub.GetProperty("id").GetString();
        var period = sub.GetProperty("nextChargeAtUtc").GetString();
        var unused = (await Json(await client.PostAsync("/api/subscriptions", Body(new { memberId, name = "Locker", amount = 25 })), "add another"))
            .GetProperty("id").GetString();

        await Json(await client.PostAsync($"/api/subscriptions/{id}/bill", null), "bill");
        (await Refused(await client.DeleteAsync($"/api/subscriptions/{id}"), HttpStatusCode.Conflict, "deleting a subscription that billed"))
            .Should().Contain("Cancel it instead");
        (await client.DeleteAsync($"/api/subscriptions/{unused}")).StatusCode.Should().Be(HttpStatusCode.NoContent, "one that never billed can go");

        // Someone moves the next billing date back onto the period already billed.
        await Json(await client.PutAsync($"/api/subscriptions/{id}", Body(new { nextChargeAtUtc = period })), "move the date back");
        (await Refused(await client.PostAsync($"/api/subscriptions/{id}/bill", null), HttpStatusCode.Conflict, "billing that period again"))
            .Should().Contain("billed already");
        (await Json(await client.GetAsync("/api/charges"), "charges")).GetArrayLength().Should().Be(1);
        (await BalanceAsync(client, memberId)).Should().Be(-299);
    }

    [Fact]
    public async Task ATabChangedFromAnOlderCopyIsRefused()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client);
        var opened = await client.PostAsync("/api/tabs", Body(new { memberId, venue = "Bar" }));
        var tab = await Json(opened, "open tab");
        var firstTag = opened.Headers.ETag!.Tag;
        firstTag.Should().Be($"\"{tab.GetProperty("version").GetString()}\"");

        var add = new HttpRequestMessage(HttpMethod.Post, $"/api/tabs/{tab.GetProperty("id").GetString()}/items") { Content = Body(new { name = "Negroni", quantity = 1, unitPrice = 15 }) };
        add.Headers.TryAddWithoutValidation("If-Match", firstTag);
        await Json(await client.SendAsync(add), "add from the copy on screen");
        var stale = new HttpRequestMessage(HttpMethod.Post, $"/api/tabs/{tab.GetProperty("id").GetString()}/items") { Content = Body(new { name = "Olives", quantity = 1, unitPrice = 6 }) };
        stale.Headers.TryAddWithoutValidation("If-Match", firstTag);

        (await Refused(await client.SendAsync(stale), HttpStatusCode.Conflict, "adding from a copy someone has changed since")).Should().Contain("at the same moment");
        (await Json(await client.GetAsync("/api/tabs"), "tabs"))[0].GetProperty("total").GetDecimal().Should().Be(15);
    }

    // ── members ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task AMemberWithMoneyStillWithTheClubIsNotDeleted()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client);
        var charge = await ChargeAsync(client, memberId, 20);

        (await Refused(await client.DeleteAsync($"/api/members/{memberId}"), HttpStatusCode.Conflict, "deleting a member who owes 20"))
            .Should().Contain("set the member Resigned or Lapsed instead");
        await PayAsync(client, charge, 20, "Cash");
        (await client.DeleteAsync($"/api/members/{memberId}")).StatusCode.Should().Be(HttpStatusCode.NoContent, "settled, they can go");
        (await ChargeOfAsync(client, charge)).GetProperty("memberName").GetString().Should().Be("Jean Dupont", "the books still say whose it was");
    }

    [Fact]
    public async Task MergingMembersMovesTheirLedger_AndTheBalancesAddUp()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var keep = await MemberAsync(client, "Jean", houseAccountBalance: -100);
        var drop = await MemberAsync(client, "Jeannot", houseAccountBalance: -50);
        var charge = await ChargeAsync(client, drop, 30);

        (await client.PostAsync($"/api/members/{keep}/merge", Body(new { otherMemberId = drop }))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await BalanceAsync(client, keep)).Should().Be(-180);
        (await ListedBalanceAsync(client, keep)).Should().Be(-180);
        var moved = await ChargeOfAsync(client, charge);
        moved.GetProperty("memberId").GetGuid().Should().Be(keep);
        moved.GetProperty("memberName").GetString().Should().Be("Jean Dupont");
        await PayAsync(client, charge, 30, "Cash");
        (await BalanceAsync(client, keep)).Should().Be(-150);
    }

    // ── workspaces ───────────────────────────────────────────────────────────

    [Fact]
    public async Task AnotherClubCanNeitherSeeNorTouchThisClubsMoney()
    {
        using var factory = new CustomWebApplicationFactory();
        var (mine, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(mine, houseAccountBalance: -10);
        var charge = await ChargeAsync(mine, memberId, 100);
        var payment = await PayAsync(mine, charge, 40);
        var card = (await Json(await mine.PostAsync("/api/gift-cards", Body(new { purchaserName = "Ana", recipientName = "Bea", faceValue = 50 })), "issue")).GetProperty("id").GetString();
        var tab = (await Json(await mine.PostAsync("/api/tabs", Body(new { memberId, venue = "Bar" })), "open tab")).GetProperty("id").GetString();
        var sub = (await Json(await mine.PostAsync("/api/subscriptions", Body(new { memberId, name = "Dues", amount = 99 })), "subscription")).GetProperty("id").GetString();
        var donation = (await Json(await mine.PostAsync("/api/donations", Body(new { memberId, cause = "Arts", amount = 20 })), "donation")).GetProperty("id").GetString();
        var (theirs, _, _) = await ClubAsync(factory, "other@example.ch");

        foreach (var path in new[] { "/api/charges", "/api/payments", "/api/gift-cards", "/api/gift-cards/redemptions", "/api/tabs", "/api/subscriptions", "/api/donations" })
            (await Json(await theirs.GetAsync(path), $"their {path}")).GetArrayLength().Should().Be(0, $"{path} is this club's alone");
        await Refused(await theirs.GetAsync($"/api/members/{memberId}/balance"), HttpStatusCode.NotFound, "reading another club's member's balance");
        await Refused(await theirs.GetAsync($"/api/members/{memberId}/statement?from=2026-01-01&to=2027-01-01"), HttpStatusCode.NotFound, "reading their statement");
        await Refused(await theirs.PostAsync("/api/charges", Body(new { memberId, kind = "Bar", description = "Bar", amount = 10 })), HttpStatusCode.NotFound, "charging their member");
        await Refused(await theirs.PostAsync($"/api/charges/{charge}/void", Body(new { reason = "Mine now" })), HttpStatusCode.NotFound, "voiding their charge");
        await Refused(await theirs.PostAsync("/api/payments", Body(new { amount = 10, method = "Cash", chargeId = charge })), HttpStatusCode.NotFound, "paying their charge");
        await Refused(await theirs.PostAsync($"/api/payments/{payment}/refund", Body(new { amount = 10, reason = "Mine now" })), HttpStatusCode.NotFound, "refunding their payment");
        await Refused(await theirs.PostAsync($"/api/payments/{payment}/void", Body(new { reason = "Mine now" })), HttpStatusCode.NotFound, "voiding their payment");
        await Refused(await theirs.PostAsync($"/api/gift-cards/{card}/redeem", Body(new { amount = 10 })), HttpStatusCode.NotFound, "spending their card");
        await Refused(await theirs.PostAsync($"/api/gift-cards/{card}/top-up", Body(new { amount = 10 })), HttpStatusCode.NotFound, "topping up their card");
        await Refused(await theirs.PostAsync($"/api/gift-cards/{card}/void", Body(new { reason = "Mine now" })), HttpStatusCode.NotFound, "voiding their card");
        await Refused(await theirs.PostAsync($"/api/tabs/{tab}/items", Body(new { name = "Negroni", quantity = 1, unitPrice = 15 })), HttpStatusCode.NotFound, "adding to their tab");
        await Refused(await theirs.PostAsync($"/api/tabs/{tab}/close", null), HttpStatusCode.NotFound, "closing their tab");
        await Refused(await theirs.PutAsync($"/api/subscriptions/{sub}", Body(new { amount = 1 })), HttpStatusCode.NotFound, "re-pricing their subscription");
        await Refused(await theirs.PostAsync($"/api/subscriptions/{sub}/bill", null), HttpStatusCode.NotFound, "billing their subscription");
        await Refused(await theirs.PostAsync($"/api/donations/{donation}/acknowledge", null), HttpStatusCode.NotFound, "thanking for their donation");
        await Refused(await theirs.PostAsync($"/api/donations/{donation}/void", Body(new { reason = "Mine now" })), HttpStatusCode.NotFound, "voiding their donation");

        (await BalanceAsync(mine, memberId)).Should().Be(-70, "nothing they tried reached this club's books");
        (await ChargeOfAsync(mine, charge)).GetProperty("paidAmount").GetDecimal().Should().Be(40);
        (await Json(await mine.GetAsync("/api/gift-cards"), "my cards"))[0].GetProperty("remainingValue").GetDecimal().Should().Be(50);
        (await Json(await mine.GetAsync("/api/tabs"), "my tabs"))[0].GetProperty("items").GetArrayLength().Should().Be(0);
        (await Json(await mine.GetAsync("/api/subscriptions"), "my subscriptions"))[0].GetProperty("amount").GetDecimal().Should().Be(99);
        (await factory.Services.Read(db => db.ClubDonations.Count(d => d.ReversesDonationId != null))).Should().Be(0);
    }
}
