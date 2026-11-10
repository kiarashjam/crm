using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The club's price catalogue and the settings that drive the server: tiers a club
/// configures, prices the server takes from the catalogue, and the points rules it applies.
/// </summary>
/// <remarks>
/// The tiers, dues and points rules used to be a table in the browser and a settings
/// document nothing on the server read: a manager who set Platinum to 3x saw every tab
/// still earn 2x, and new members got the browser's dues. Each test here changes a setting
/// or a price and reads back what the server then did.
/// </remarks>
public class ClubCatalogTests
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

    private static async Task<JsonElement> TierAsync(HttpClient client, string name) =>
        (await Json(await client.GetAsync("/api/catalog?kind=Membership"), "list tiers")).EnumerateArray()
            .Single(t => t.GetProperty("name").GetString() == name);

    private static async Task<JsonElement> ItemAsync(HttpClient client, object item) =>
        await Json(await client.PostAsync("/api/catalog", Body(item)), "add a catalogue item");

    private static async Task<Guid> MemberAsync(HttpClient client, string first = "Jean", string tier = "Gold") =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Dupont", email = $"{first.ToLowerInvariant()}@example.ch", tier, status = "Active",
        })), "add member")).GetProperty("id").GetGuid();

    // ── tiers ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AWorkspaceFindsTheTiersItHasAlwaysShown_InOrder_WithTheirDuesFeesAndMultipliers()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);

        var tiers = (await Json(await client.GetAsync("/api/catalog?kind=Membership"), "list tiers")).EnumerateArray().ToList();
        tiers.Select(t => (t.GetProperty("name").GetString(), t.GetProperty("price").GetDecimal(), t.GetProperty("initiationFee").GetDecimal(),
                t.GetProperty("loyaltyMultiplier").GetDecimal(), t.GetProperty("cadence").GetString(), t.GetProperty("currency").GetString()))
            .Should().Equal(
                ("Bronze", 49m, 0m, 1m, "Monthly", "CHF"),
                ("Silver", 129m, 250m, 1.25m, "Monthly", "CHF"),
                ("Gold", 299m, 1000m, 1.5m, "Monthly", "CHF"),
                ("Platinum", 599m, 2500m, 2m, "Monthly", "CHF"));
        tiers[2].GetProperty("benefits").EnumerateArray().Select(b => b.GetString()).Should().Contain("Tennis court");

        // Read again, still four: a workspace is given its tiers once.
        (await Json(await client.GetAsync("/api/catalog?kind=Membership"), "list tiers again")).GetArrayLength().Should().Be(4);
    }

    [Fact]
    public async Task AWorkspaceWhoseSettingsListedItsOwnTiersGetsThose()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, org) = await ClubAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ClubDocuments.Add(new ClubDocument
            {
                Id = Guid.NewGuid(), OrganizationId = org, Kind = "hospitality-settings", UpdatedAtUtc = DateTime.UtcNow,
                Payload = """{"currency":"CHF","tiers":[{"tier":"Founder","monthlyDues":450,"initiationFee":5000,"loyaltyMultiplier":3,"benefits":["Everything"]},{"tier":"Junior","monthlyDues":80,"initiationFee":0,"loyaltyMultiplier":1}]}""",
            });
            await db.SaveChangesAsync();
        }

        var tiers = (await Json(await client.GetAsync("/api/catalog?kind=Membership"), "list tiers")).EnumerateArray().ToList();
        tiers.Select(t => (t.GetProperty("name").GetString(), t.GetProperty("price").GetDecimal(), t.GetProperty("loyaltyMultiplier").GetDecimal()))
            .Should().Equal(("Founder", 450m, 3m), ("Junior", 80m, 1m));
    }

    [Fact]
    public async Task ATierCanBeAddedRenamedAndTakenOffSale_AndMembersKeepTheirTierThroughARename()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client, tier: "Gold");

        var junior = await ItemAsync(client, new { kind = "Membership", name = "Junior", price = 79, cadence = "Quarterly", initiationFee = 100, loyaltyMultiplier = 1.1 });
        junior.GetProperty("sortOrder").GetInt32().Should().Be(4, "a new tier goes below the others");
        await Refused(await client.PostAsync("/api/catalog", Body(new { kind = "Membership", name = "gold", price = 1 })), HttpStatusCode.Conflict,
            "a second tier called gold");

        var gold = await TierAsync(client, "Gold");
        var renamed = await Json(await client.PutAsync($"/api/catalog/{gold.GetProperty("id").GetGuid()}",
            Body(new { name = "Or", version = gold.GetProperty("version").GetGuid() })), "rename Gold");
        renamed.GetProperty("name").GetString().Should().Be("Or");
        var member = (await Json(await client.GetAsync("/api/members"), "members")).EnumerateArray().Single(m => m.GetProperty("id").GetGuid() == memberId);
        member.GetProperty("tier").GetString().Should().Be("Or", "the member keeps the tier, under its new name");

        // A save from the copy read before the rename is refused rather than undoing it.
        await Refused(await client.PutAsync($"/api/catalog/{gold.GetProperty("id").GetGuid()}",
            Body(new { price = 1, version = gold.GetProperty("version").GetGuid() })), HttpStatusCode.Conflict, "a save from a stale copy");

        var off = await Json(await client.PutAsync($"/api/catalog/{junior.GetProperty("id").GetGuid()}", Body(new { isActive = false })), "take Junior off sale");
        off.GetProperty("isActive").GetBoolean().Should().BeFalse();
        await Refused(await client.PostAsync("/api/members", Body(new { firstName = "Ana", lastName = "Silva", email = "ana@example.ch", tier = "Junior" })),
            HttpStatusCode.BadRequest, "a new member on a tier no longer offered");
        await Refused(await client.PostAsync("/api/members", Body(new { firstName = "Ana", lastName = "Silva", email = "ana@example.ch", tier = "Diamond" })),
            HttpStatusCode.BadRequest, "a new member on a tier the club does not have");
        (await client.DeleteAsync($"/api/catalog/{junior.GetProperty("id").GetGuid()}")).StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed,
            "items are taken off sale, never deleted: members and charges name them");
    }

    [Fact]
    public async Task ANewMemberStartsWithTheirTiersDues_AndCanBeBilledTheirDuesAndInitiationFeeOnJoining()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, org) = await ClubAsync(factory);
        var gold = await TierAsync(client, "Gold");
        await Json(await client.PutAsync($"/api/catalog/{gold.GetProperty("id").GetGuid()}", Body(new { price = 350, vatRate = 8.1 })), "raise Gold's dues");

        var plain = await Json(await client.PostAsync("/api/members", Body(new { firstName = "Lea", lastName = "Meier", email = "lea@example.ch", tier = "Gold" })), "add a member");
        plain.GetProperty("duesAmount").GetDecimal().Should().Be(350, "a new member's dues are their tier's in the catalogue");
        plain.GetProperty("duesFrequency").GetString().Should().Be("Monthly");

        var joined = await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = "Marc", lastName = "Weber", email = "marc@example.ch", tier = "Gold", startDues = true, chargeInitiationFee = true,
        })), "add a member who starts paying at once");
        var memberId = joined.GetProperty("id").GetGuid();
        var charges = (await Json(await client.GetAsync($"/api/charges?memberId={memberId}"), "charges")).EnumerateArray().ToList();
        charges.Should().ContainSingle();
        charges[0].GetProperty("description").GetString().Should().Be("Initiation fee, Gold");
        charges[0].GetProperty("amount").GetDecimal().Should().Be(1000);
        charges[0].GetProperty("vatRate").GetDecimal().Should().Be(8.1m, "the fee carries the tier's VAT rate");
        var sub = (await Json(await client.GetAsync("/api/subscriptions"), "subscriptions")).EnumerateArray().Single(s => s.GetProperty("memberId").GetGuid() == memberId);
        sub.GetProperty("name").GetString().Should().Be("Gold dues");
        sub.GetProperty("amount").GetDecimal().Should().Be(350);
        sub.GetProperty("catalogItemId").GetGuid().Should().Be(gold.GetProperty("id").GetGuid());
        DateTime.Parse(sub.GetProperty("nextChargeAtUtc").GetString()!).ToUniversalTime().Date.Should().Be(DateTime.UtcNow.Date,
            "dues are billed in advance: the first period starts today");

        // Starting someone's dues is money: a membership director adds members, not charges.
        var director = await JoinAsync(factory, client, org, "director@example.ch", "membership_director");
        await Refused(await director.PostAsync("/api/members", Body(new { firstName = "Tom", lastName = "Graf", email = "tom@example.ch", tier = "Gold", chargeInitiationFee = true })),
            HttpStatusCode.Forbidden, "a membership director charging an initiation fee");
        await Json(await director.PostAsync("/api/members", Body(new { firstName = "Tom", lastName = "Graf", email = "tom@example.ch", tier = "Gold" })),
            "a membership director adding a member");
    }

    // ── prices the server takes from the catalogue ───────────────────────────

    [Fact]
    public async Task AChargeRungUpFromTheCatalogueTakesItsPriceNameAndVat_WhateverAmountIsSent()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client);
        var guestFee = await ItemAsync(client, new { kind = "Fee", name = "Guest fee", price = 25, vatRate = 8.1 });
        guestFee.GetProperty("chargeKind").GetString().Should().Be("Other");

        var charge = await Json(await client.PostAsync("/api/charges", Body(new
        {
            memberId, catalogItemId = guestFee.GetProperty("id").GetGuid(), quantity = 2, amount = 1, kind = "Spa", description = "",
        })), "charge two guest fees");
        charge.GetProperty("amount").GetDecimal().Should().Be(50, "the catalogue's price, twice, not the 1 that was sent");
        charge.GetProperty("description").GetString().Should().Be("Guest fee × 2");
        charge.GetProperty("kind").GetString().Should().Be("Other");
        charge.GetProperty("vatRate").GetDecimal().Should().Be(8.1m);

        await Json(await client.PutAsync($"/api/catalog/{guestFee.GetProperty("id").GetGuid()}", Body(new { isActive = false })), "take the fee off sale");
        await Refused(await client.PostAsync("/api/charges", Body(new { memberId, catalogItemId = guestFee.GetProperty("id").GetGuid() })),
            HttpStatusCode.Conflict, "charging an item no longer on sale");
        var gold = await TierAsync(client, "Gold");
        await Refused(await client.PostAsync("/api/charges", Body(new { memberId, catalogItemId = gold.GetProperty("id").GetGuid() })),
            HttpStatusCode.BadRequest, "charging a tier as if it were a one-off item");
    }

    [Fact]
    public async Task ATabRingsUpAMenuItemAtItsCataloguePrice_AndASubscriptionCopiesItsPriceSoALaterChangeLeavesItAlone()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client);
        var negroni = await ItemAsync(client, new { kind = "MenuItem", name = "Negroni", price = 18.5, vatRate = 8.1, category = "Bar" });
        negroni.GetProperty("chargeKind").GetString().Should().Be("Bar");

        var tab = await Json(await client.PostAsync("/api/tabs", Body(new { memberId, venue = "Bar" })), "open a tab");
        var withItem = await Json(await client.PostAsync($"/api/tabs/{tab.GetProperty("id").GetGuid()}/items",
            Body(new { catalogItemId = negroni.GetProperty("id").GetGuid(), quantity = 2, name = "Water", unitPrice = 0 })), "ring up two Negronis");
        var item = withItem.GetProperty("items")[0];
        (item.GetProperty("name").GetString(), item.GetProperty("unitPrice").GetDecimal(), item.GetProperty("vatRate").GetDecimal())
            .Should().Be(("Negroni", 18.5m, 8.1m));
        var closed = await Json(await client.PostAsync($"/api/tabs/{tab.GetProperty("id").GetGuid()}/close", null), "close the tab");
        var charge = (await Json(await client.GetAsync($"/api/charges?memberId={memberId}"), "charges")).EnumerateArray()
            .Single(c => c.GetProperty("id").GetGuid() == closed.GetProperty("chargeId").GetGuid());
        charge.GetProperty("amount").GetDecimal().Should().Be(37);
        charge.GetProperty("vatRate").GetDecimal().Should().Be(8.1m, "every item on the tab was at 8.1%");

        var locker = await ItemAsync(client, new { kind = "Dues", name = "Locker", price = 30, cadence = "Quarterly" });
        var sub = await Json(await client.PostAsync("/api/subscriptions", Body(new { memberId, catalogItemId = locker.GetProperty("id").GetGuid() })), "rent a locker");
        (sub.GetProperty("name").GetString(), sub.GetProperty("amount").GetDecimal(), sub.GetProperty("cadence").GetString())
            .Should().Be(("Locker", 30m, "Quarterly"));
        await Json(await client.PutAsync($"/api/catalog/{locker.GetProperty("id").GetGuid()}", Body(new { price = 45 })), "raise the locker price");
        (await Json(await client.GetAsync("/api/subscriptions"), "subscriptions")).EnumerateArray()
            .Single(s => s.GetProperty("id").GetGuid() == sub.GetProperty("id").GetGuid()).GetProperty("amount").GetDecimal()
            .Should().Be(30, "a subscription keeps the price it was started at");
    }

    [Fact]
    public async Task TheCatalogueChecksWhatItIsSent()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        await Refused(await client.PostAsync("/api/catalog", Body(new { kind = "Gadget", name = "X", price = 1 })), HttpStatusCode.BadRequest, "an unknown kind");
        await Refused(await client.PostAsync("/api/catalog", Body(new { kind = "Fee", name = " ", price = 1 })), HttpStatusCode.BadRequest, "no name");
        await Refused(await client.PostAsync("/api/catalog", Body(new { kind = "Fee", name = "X", price = -1 })), HttpStatusCode.BadRequest, "a negative price");
        await Refused(await client.PostAsync("/api/catalog", Body(new { kind = "Fee", name = "X" })), HttpStatusCode.BadRequest, "no price");
        await Refused(await client.PostAsync("/api/catalog", Body(new { kind = "Fee", name = "X", price = 1, vatRate = 108 })), HttpStatusCode.BadRequest, "VAT over 100%");
        await Refused(await client.PostAsync("/api/catalog", Body(new { kind = "Dues", name = "X", price = 1, cadence = "Weekly" })), HttpStatusCode.BadRequest, "a weekly cadence");
        await Refused(await client.PostAsync("/api/catalog", Body(new { kind = "Membership", name = "X", price = 1, loyaltyMultiplier = 0 })), HttpStatusCode.BadRequest,
            "a multiplier of nothing");
        var fee = await ItemAsync(client, new { kind = "Fee", name = "Late fee", price = 20 });
        await Refused(await client.PutAsync($"/api/catalog/{fee.GetProperty("id").GetGuid()}", Body(new { kind = "Product" })), HttpStatusCode.BadRequest,
            "changing an item's kind");
    }

    // ── who may change it, and whose it is ───────────────────────────────────

    [Fact]
    public async Task EveryoneReadsTheCatalogue_OnlySettingsOrMoneyManagersChangeIt_AndAnotherClubCannotSeeOrTouchIt()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, org) = await ClubAsync(factory);
        var fee = await ItemAsync(client, new { kind = "Fee", name = "Guest fee", price = 25 });
        var feeId = fee.GetProperty("id").GetGuid();

        var desk = await JoinAsync(factory, client, org, "desk@example.ch", "front_desk");
        (await Json(await desk.GetAsync("/api/catalog"), "the front desk reads the catalogue")).GetArrayLength().Should().Be(5);
        await Refused(await desk.PostAsync("/api/catalog", Body(new { kind = "Fee", name = "Mine", price = 1 })), HttpStatusCode.Forbidden, "the front desk adding a price");
        await Refused(await desk.PutAsync($"/api/catalog/{feeId}", Body(new { price = 1 })), HttpStatusCode.Forbidden, "the front desk changing a price");
        var finance = await JoinAsync(factory, client, org, "finance@example.ch", "finance");
        await Json(await finance.PutAsync($"/api/catalog/{feeId}", Body(new { price = 30 })), "finance changing a price");

        var (theirs, _, _) = await ClubAsync(factory, "other@example.ch");
        (await Json(await theirs.GetAsync("/api/catalog"), "another club's catalogue")).EnumerateArray()
            .Should().NotContain(i => i.GetProperty("id").GetGuid() == feeId);
        await Refused(await theirs.GetAsync($"/api/catalog/{feeId}"), HttpStatusCode.NotFound, "another club reading the fee");
        await Refused(await theirs.PutAsync($"/api/catalog/{feeId}", Body(new { price = 0 })), HttpStatusCode.NotFound, "another club changing the fee");
        var theirMember = await MemberAsync(theirs, "Paul");
        await Refused(await theirs.PostAsync("/api/charges", Body(new { memberId = theirMember, catalogItemId = feeId })), HttpStatusCode.NotFound,
            "another club charging from this club's catalogue");
        (await factory.Services.Read(db => db.ClubCatalogItems.Single(i => i.Id == feeId).Price)).Should().Be(30);
    }

    // ── the settings that drive the server ───────────────────────────────────

    [Fact]
    public async Task ClosingATabEarnsTheClubsBasePointsTimesTheTiersMultiplier_AsSetInTheSettingsAndCatalogue()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client, tier: "Platinum");
        var platinum = await TierAsync(client, "Platinum");
        await Json(await client.PutAsync($"/api/catalog/{platinum.GetProperty("id").GetGuid()}", Body(new { loyaltyMultiplier = 3 })), "Platinum to 3x");
        await Json(await client.PutAsync("/api/hospitality/settings", Body(new { basePointsPerDollar = 4 })), "4 points per franc");

        var tab = await Json(await client.PostAsync("/api/tabs", Body(new { memberId, venue = "Dining" })), "open a tab");
        var tabId = tab.GetProperty("id").GetGuid();
        await Json(await client.PostAsync($"/api/tabs/{tabId}/items", Body(new { name = "Dinner", quantity = 1, unitPrice = 50 })), "add dinner");
        var closed = await Json(await client.PostAsync($"/api/tabs/{tabId}/close", null), "close");
        closed.GetProperty("pointsAwarded").GetInt32().Should().Be(600, "50 francs × 4 points × Platinum's 3, not the old 10 × 2");
    }

    [Fact]
    public async Task ACheckInEarnsTheVenuesPointsTimesTheTiersMultiplier_WorkedOutAndWrittenByTheServerOnce()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client, tier: "Gold");
        await Json(await client.PutAsync("/api/hospitality/settings", Body(new { visitPointsByVenue = new Dictionary<string, int> { ["Spa"] = 80 } })), "spa visits earn 80");

        var visit = await Json(await client.PostAsync("/api/visits", Body(new { memberId, venue = "Spa", pointsAwarded = 99999 })), "check in at the spa");
        visit.GetProperty("pointsAwarded").GetInt32().Should().Be(120, "80 × Gold's 1.5, not the figure the browser sent");
        var ledger = (await Json(await client.GetAsync("/api/loyalty/ledger"), "loyalty ledger")).EnumerateArray().ToList();
        ledger.Should().ContainSingle("the visit's points are one entry, written with the visit");
        (ledger[0].GetProperty("reason").GetString(), ledger[0].GetProperty("points").GetInt32()).Should().Be(("Visit", 120));

        var none = await Json(await client.PostAsync("/api/visits", Body(new { memberId, venue = "Bar", awardPoints = false })), "a visit that earns nothing");
        none.GetProperty("pointsAwarded").GetInt32().Should().Be(0);
        (await Json(await client.GetAsync("/api/loyalty/ledger"), "loyalty ledger")).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task TheSettingsAnswerWhatTheServerApplies_RefuseNonsense_AndNoLongerKeepWhatNothingUses()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var settings = await Json(await client.GetAsync("/api/hospitality/settings"), "read the settings");
        settings.GetProperty("basePointsPerDollar").GetDecimal().Should().Be(10);
        settings.GetProperty("eventCheckInPoints").GetInt32().Should().Be(50);
        var billing = settings.GetProperty("billing");
        billing.GetProperty("automaticBilling").GetBoolean().Should().BeFalse("nothing bills on its own until the club turns it on");
        billing.GetProperty("paymentTermsDays").GetInt32().Should().Be(14);
        billing.GetProperty("reminderDays").EnumerateArray().Select(d => d.GetInt32()).Should().Equal(3, 14, 30);

        await Refused(await client.PutAsync("/api/hospitality/settings", Body(new { basePointsPerDollar = -5 })), HttpStatusCode.BadRequest, "negative points");
        await Refused(await client.PutAsync("/api/hospitality/settings", Body(new { billing = new { gracePeriodDays = 0 } })), HttpStatusCode.BadRequest, "no grace at all");
        await Refused(await client.PutAsync("/api/hospitality/settings", Body(new { billing = new { reminderDays = new[] { 14, 3 } } })), HttpStatusCode.BadRequest,
            "reminders out of order");
        await Refused(await client.PutAsync("/api/hospitality/settings", Body(new { billing = new { statementDay = 31 } })), HttpStatusCode.BadRequest,
            "a statement day some months lack");

        var saved = await Json(await client.PutAsync("/api/hospitality/settings", Body(new
        {
            referralBonus = 1000, anniversaryBonus = 500, pointsPerDollarRedemption = 100, tiers = Array.Empty<object>(),
            billing = new { automaticBilling = true, paymentTermsDays = 30, reminderDays = new[] { 7, 21 } },
        })), "save");
        saved.TryGetProperty("referralBonus", out _).Should().BeFalse("nothing awarded it, so it is no longer kept");
        saved.TryGetProperty("tiers", out _).Should().BeFalse("the tiers are the catalogue's");
        saved.GetProperty("billing").GetProperty("automaticBilling").GetBoolean().Should().BeTrue();
        saved.GetProperty("billing").GetProperty("paymentTermsDays").GetInt32().Should().Be(30);
        (await Json(await client.GetAsync("/api/catalog?kind=Membership"), "tiers")).GetArrayLength().Should().Be(4, "an old page sending tiers changes no tier");
    }
}
