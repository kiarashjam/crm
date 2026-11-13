using System.Net;
using System.Text.Json;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Memberships held by companies: seats their nominees fill, dues on the company's own house
/// account, the nominees' charges on it when the company pays them, one statement for the
/// company, and renewal on the company.
/// </summary>
public class ClubCorporateMembershipsTests
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

    private static async Task<(HttpClient Client, Guid UserId, Guid Org)> ClubAsync(CustomWebApplicationFactory factory, string email = "owner@pavillon46.ch")
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

    private static async Task<Guid> CompanyAsync(HttpClient client, string name = "Acme AG") =>
        (await Json(await client.PostAsync("/api/companies", Body(new { name })), $"add {name}")).GetProperty("id").GetGuid();

    private static async Task<Guid> MemberAsync(HttpClient client, string first, string status = "Active") =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Keller", email = $"{first.ToLowerInvariant()}@acme.ch", tier = "Gold", status,
        })), $"add {first}")).GetProperty("id").GetGuid();

    private static async Task<JsonElement> CorporateAsync(HttpClient client, Guid company, int seats = 2, string nomineeCharges = "company", object? dues = null) =>
        await Json(await client.PostAsync("/api/corporate-memberships", Body(new
        {
            companyId = company, name = "Acme corporate", seats, nomineeCharges, startsAtUtc = DateTime.UtcNow.Date.ToString("O"),
            billingEmail = "accounts@acme.ch", dues,
        })), "make the corporate membership");

    private static Guid Id(JsonElement e) => e.GetProperty("id").GetGuid();

    private static JsonElement Nominee(JsonElement membership, Guid memberId) =>
        membership.GetProperty("nominees").EnumerateArray().Single(n => n.GetProperty("memberId").GetGuid() == memberId);

    [Fact]
    public async Task NomineesFillTheSeats_AddSwapAndRemoveHoldToTheSeatCount_AndAMemberHoldsOneSeat()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var acme = await CompanyAsync(client);
        var membership = await CorporateAsync(client, acme, seats: 2);
        var id = Id(membership);
        membership.GetProperty("seatsUsed").GetInt32().Should().Be(0);
        membership.GetProperty("account").GetProperty("open").GetBoolean().Should().BeTrue("its nominees' charges have somewhere to go from the first day");

        var anna = await MemberAsync(client, "Anna");
        var ben = await MemberAsync(client, "Ben");
        var carla = await MemberAsync(client, "Carla");
        await Json(await client.PostAsync($"/api/corporate-memberships/{id}/nominees", Body(new { memberId = anna })), "seat Anna");
        var two = await Json(await client.PostAsync($"/api/corporate-memberships/{id}/nominees", Body(new { memberId = ben })), "seat Ben");
        two.GetProperty("seatsUsed").GetInt32().Should().Be(2);

        var full = await Refused(await client.PostAsync($"/api/corporate-memberships/{id}/nominees", Body(new { memberId = carla })), HttpStatusCode.Conflict, "a third nominee");
        full.Should().Contain("All 2 seats are taken");

        // A swap gives Ben's seat to Carla in one step: still two seats used, Ben in the history.
        var swapped = await Json(await client.PostAsync($"/api/corporate-memberships/{id}/nominees/{Id(Nominee(two, ben))}/swap",
            Body(new { memberId = carla, reason = "Ben left Acme" })), "swap Ben for Carla");
        swapped.GetProperty("seatsUsed").GetInt32().Should().Be(2);
        swapped.GetProperty("nominees").EnumerateArray().Select(n => n.GetProperty("memberId").GetGuid()).Should().BeEquivalentTo([anna, carla]);
        var former = swapped.GetProperty("formerNominees").EnumerateArray().Single();
        former.GetProperty("memberId").GetGuid().Should().Be(ben);
        former.GetProperty("replacedByMemberId").GetGuid().Should().Be(carla);
        former.GetProperty("removedReason").GetString().Should().Be("Ben left Acme");

        // One seat at a time: Anna cannot take a seat on another company's membership.
        var other = await CorporateAsync(client, await CompanyAsync(client, "Bolt GmbH"), seats: 3);
        await Refused(await client.PostAsync($"/api/corporate-memberships/{Id(other)}/nominees", Body(new { memberId = anna })), HttpStatusCode.Conflict, "Anna on a second membership");

        var removed = await Json(await client.PostAsync($"/api/corporate-memberships/{id}/nominees/{Id(Nominee(swapped, anna))}/remove", Body(new { reason = "Retired" })), "remove Anna");
        removed.GetProperty("seatsUsed").GetInt32().Should().Be(1);
        // Fewer seats than nominees holding one is refused; down to one is fine.
        await Json(await client.PutAsync($"/api/corporate-memberships/{id}", Body(new { seats = 1, version = removed.GetProperty("version").GetGuid() })), "one seat");
        await Json(await client.PostAsync($"/api/corporate-memberships/{Id(other)}/nominees", Body(new { memberId = anna })), "Anna, free again, on Bolt's");

        // A lapsed member holds no seat.
        var dora = await MemberAsync(client, "Dora", status: "Lapsed");
        await Refused(await client.PostAsync($"/api/corporate-memberships/{Id(other)}/nominees", Body(new { memberId = dora })), HttpStatusCode.BadRequest, "a lapsed nominee");

        var seat = await Json(await client.GetAsync($"/api/members/{carla}/corporate"), "Carla's seat");
        seat.GetProperty("companyName").GetString().Should().Be("Acme AG");
        (await client.GetAsync($"/api/members/{ben}/corporate")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task WhenTheCompanyPays_ANomineesChargesGoOnItsAccountSayingWho_TheirOwnDuesDoNot_AndTheStatementListsThem()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var acme = await CompanyAsync(client);
        var membership = await CorporateAsync(client, acme, seats: 3, nomineeCharges: "company");
        var anna = await MemberAsync(client, "Anna");
        await Json(await client.PostAsync($"/api/corporate-memberships/{Id(membership)}/nominees", Body(new { memberId = anna })), "seat Anna");

        var dinner = await Json(await client.PostAsync("/api/charges", Body(new { memberId = anna, kind = "Dining", description = "Client dinner", amount = 240m })), "Anna's dinner");
        dinner.GetProperty("memberId").GetGuid().Should().Be(acme, "the company pays what its nominees run up");
        dinner.GetProperty("accountType").GetString().Should().Be("company");
        dinner.GetProperty("incurredByMemberId").GetGuid().Should().Be(anna);
        dinner.GetProperty("incurredByName").GetString().Should().Be("Anna Keller");

        // Her own dues stay hers: the company pays for the seat, not her subscription.
        var dues = await Json(await client.PostAsync("/api/subscriptions", Body(new { memberId = anna, name = "Gold dues", amount = 129m, cadence = "Monthly" })), "Anna's own dues");
        await Json(await client.PostAsync($"/api/subscriptions/{Id(dues)}/bill", Body(new { })), "bill Anna's dues");
        (await Json(await client.GetAsync($"/api/members/{anna}/balance"), "Anna's balance")).GetProperty("balance").GetDecimal().Should().Be(-129m);
        (await Json(await client.GetAsync($"/api/club-accounts/companies/{acme}"), "Acme's account")).GetProperty("balance").GetDecimal().Should().Be(-240m);
        (await Json(await client.GetAsync($"/api/corporate-memberships/{Id(membership)}"), "the membership"))
            .GetProperty("nominees")[0].GetProperty("ownDuesRunning").GetBoolean().Should().BeTrue("the club sees that the nominee still pays dues of their own");

        var from = DateTime.UtcNow.AddDays(-1).ToString("O");
        var to = DateTime.UtcNow.AddDays(1).ToString("O");
        var pdf = await client.GetAsync($"/api/corporate-memberships/{Id(membership)}/statement.pdf?from={from}&to={to}");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK, await pdf.Content.ReadAsStringAsync());
        var text = PdfText.Of(await pdf.Content.ReadAsByteArrayAsync());
        text.Should().Contain("Company statement").And.Contain("Client dinner").And.Contain("Incurred by Anna Keller").And.Contain("Nominee")
            .And.Contain("accounts@acme.ch");

        // Each nominee pays their own from the moment the rule says so; what was posted stays where it was.
        var read = await Json(await client.GetAsync($"/api/corporate-memberships/{Id(membership)}"), "read");
        await Json(await client.PutAsync($"/api/corporate-memberships/{Id(membership)}", Body(new { nomineeCharges = "nominee", version = read.GetProperty("version").GetGuid() })), "nominees pay");
        var spa = await Json(await client.PostAsync("/api/charges", Body(new { memberId = anna, kind = "Spa", description = "Massage", amount = 90m })), "Anna's massage");
        spa.GetProperty("memberId").GetGuid().Should().Be(anna);
        (await Json(await client.GetAsync($"/api/club-accounts/companies/{acme}"), "Acme's account")).GetProperty("balance").GetDecimal().Should().Be(-240m);
    }

    [Fact]
    public async Task ANomineeWhoLeavesTheirSeat_PaysTheirOwnAgain()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var acme = await CompanyAsync(client);
        var membership = await CorporateAsync(client, acme);
        var anna = await MemberAsync(client, "Anna");
        var seated = await Json(await client.PostAsync($"/api/corporate-memberships/{Id(membership)}/nominees", Body(new { memberId = anna })), "seat Anna");
        await Json(await client.PostAsync($"/api/corporate-memberships/{Id(membership)}/nominees/{Id(Nominee(seated, anna))}/remove", null), "remove Anna");

        var bar = await Json(await client.PostAsync("/api/charges", Body(new { memberId = anna, kind = "Bar", description = "Negroni", amount = 18m })), "Anna's drink");
        bar.GetProperty("memberId").GetGuid().Should().Be(anna);
    }

    [Fact]
    public async Task TheDues_AreASubscriptionOnTheCompanysAccount_AndSettingThemTakesTheMoneyPermission()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var acme = await CompanyAsync(owner);
        var director = await JoinAsync(factory, owner, org, "director@pavillon46.ch", "membership_director");

        await Refused(await director.PostAsync("/api/corporate-memberships", Body(new { companyId = acme, name = "Acme corporate", seats = 4, dues = new { amount = 12000m } })),
            HttpStatusCode.Forbidden, "dues without money.manage");
        var made = await Json(await director.PostAsync("/api/corporate-memberships", Body(new { companyId = acme, name = "Acme corporate", seats = 4 })),
            "a membership without dues");
        made.TryGetProperty("dues", out _).Should().BeFalse();
        await Refused(await director.PutAsync($"/api/corporate-memberships/{Id(made)}/dues", Body(new { amount = 12000m })), HttpStatusCode.Forbidden, "dues set by the director");

        var withDues = await Json(await owner.PutAsync($"/api/corporate-memberships/{Id(made)}/dues", Body(new { amount = 12000m })), "dues set by the owner");
        var dues = withDues.GetProperty("dues");
        dues.GetProperty("amount").GetDecimal().Should().Be(12000m);
        dues.GetProperty("cadence").GetString().Should().Be("Annual");
        var subscription = dues.GetProperty("subscriptionId").GetGuid();
        (await factory.Services.Read(db => db.ClubSubscriptions.Single(s => s.Id == subscription).MemberId)).Should().Be(acme);

        // Billed like a member's dues, on the company's account.
        await Json(await owner.PostAsync($"/api/subscriptions/{subscription}/bill", Body(new { })), "bill the first year");
        var account = await Json(await owner.GetAsync($"/api/club-accounts/companies/{acme}"), "Acme's account");
        account.GetProperty("balance").GetDecimal().Should().Be(-12000m);

        // Changing the dues changes the subscription, not a second one.
        await Json(await owner.PutAsync($"/api/corporate-memberships/{Id(made)}/dues", Body(new { amount = 13000m })), "new dues");
        (await factory.Services.Read(db => db.ClubSubscriptions.Count(s => s.MemberId == acme))).Should().Be(1);
        (await factory.Services.Read(db => db.ClubSubscriptions.Single(s => s.Id == subscription).Amount)).Should().Be(13000m);
    }

    [Fact]
    public async Task ARenewalMovesTheDateOnATerm_NotRenewingIsSaid_AndNomineesLeaveTheMembersRenewalsBoard()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var acme = await CompanyAsync(client);
        var start = DateTime.UtcNow.Date.AddMonths(-11);
        var membership = await Json(await client.PostAsync("/api/corporate-memberships", Body(new
        {
            companyId = acme, name = "Acme corporate", seats = 2, startsAtUtc = start.ToString("O"), termMonths = 12,
        })), "a membership started eleven months ago");
        membership.GetProperty("renewalState").GetString().Should().Be("due");
        var renewsAt = membership.GetProperty("renewsAtUtc").GetDateTime();
        renewsAt.Should().Be(start.AddMonths(12));

        // A nominee whose own renewal date is near is not a renewal card of their own any more.
        var anna = (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = "Anna", lastName = "Keller", email = "anna@acme.ch", tier = "Gold", status = "Active",
            renewsAtUtc = DateTime.UtcNow.AddDays(10).ToString("O"),
        })), "add Anna")).GetProperty("id").GetGuid();
        var board = await Json(await client.GetAsync("/api/renewals?days=60"), "the renewals board");
        board.ToString().Should().Contain(anna.ToString());
        await Json(await client.PostAsync($"/api/corporate-memberships/{Id(membership)}/nominees", Body(new { memberId = anna })), "seat Anna");
        board = await Json(await client.GetAsync("/api/renewals?days=60"), "the renewals board");
        board.ToString().Should().NotContain(anna.ToString(), "a nominee renews with the company");

        var read = await Json(await client.GetAsync($"/api/corporate-memberships/{Id(membership)}"), "read");
        var notRenewing = await Json(await client.PostAsync($"/api/corporate-memberships/{Id(membership)}/not-renewing",
            Body(new { reason = "Moving to Zurich", version = read.GetProperty("version").GetGuid() })), "not renewing");
        notRenewing.GetProperty("renewalState").GetString().Should().Be("not_renewing");
        // A stale copy cannot renew it.
        await Refused(await client.PostAsync($"/api/corporate-memberships/{Id(membership)}/renew", Body(new { version = read.GetProperty("version").GetGuid() })),
            HttpStatusCode.Conflict, "a renewal from a stale copy");
        var renewed = await Json(await client.PostAsync($"/api/corporate-memberships/{Id(membership)}/renew",
            Body(new { version = notRenewing.GetProperty("version").GetGuid() })), "renew");
        renewed.GetProperty("renewsAtUtc").GetDateTime().Should().Be(renewsAt.AddMonths(12), "a term on from the date it was due");
        renewed.GetProperty("notRenewing").GetBoolean().Should().BeFalse();
        renewed.GetProperty("renewalState").GetString().Should().Be("current");

        var due = await Json(await client.GetAsync("/api/corporate-memberships?renewingWithinDays=60"), "renewals due");
        due.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task EndingAMembership_FreesItsSeats_StopsItsDues_AndTakesTheMoneyPermissionWhileTheyRun()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var acme = await CompanyAsync(owner);
        var membership = await CorporateAsync(owner, acme, dues: new { amount = 6000m });
        var anna = await MemberAsync(owner, "Anna");
        await Json(await owner.PostAsync($"/api/corporate-memberships/{Id(membership)}/nominees", Body(new { memberId = anna })), "seat Anna");
        var director = await JoinAsync(factory, owner, org, "director@pavillon46.ch", "membership_director");

        await Refused(await director.PostAsync($"/api/corporate-memberships/{Id(membership)}/end", Body(new { reason = "Contract ended" })),
            HttpStatusCode.Forbidden, "ending with dues running, without money.manage");
        var ended = await Json(await owner.PostAsync($"/api/corporate-memberships/{Id(membership)}/end", Body(new { reason = "Contract ended" })), "end it");
        ended.GetProperty("status").GetString().Should().Be("Ended");
        ended.GetProperty("seatsUsed").GetInt32().Should().Be(0);
        ended.GetProperty("dues").GetProperty("status").GetString().Should().Be("Cancelled");

        // Anna pays her own again, and the ended membership takes no one.
        (await Json(await owner.PostAsync("/api/charges", Body(new { memberId = anna, kind = "Bar", description = "Negroni", amount = 18m })), "Anna's drink"))
            .GetProperty("memberId").GetGuid().Should().Be(anna);
        await Refused(await owner.PostAsync($"/api/corporate-memberships/{Id(membership)}/nominees", Body(new { memberId = anna })), HttpStatusCode.Conflict, "a seat on an ended membership");
    }

    [Fact]
    public async Task AnotherWorkspaces_MembershipsCompaniesAndMembers_AreOutOfReach()
    {
        using var factory = new CustomWebApplicationFactory();
        var (ours, _, _) = await ClubAsync(factory);
        var acme = await CompanyAsync(ours);
        var membership = await CorporateAsync(ours, acme);
        var anna = await MemberAsync(ours, "Anna");

        var (theirs, _, _) = await ClubAsync(factory, "owner@elsewhere.ch");
        await Refused(await theirs.GetAsync($"/api/corporate-memberships/{Id(membership)}"), HttpStatusCode.NotFound, "reading our membership");
        await Refused(await theirs.PostAsync($"/api/corporate-memberships/{Id(membership)}/nominees", Body(new { memberId = anna })), HttpStatusCode.NotFound, "seating someone on ours");
        await Refused(await theirs.PostAsync("/api/corporate-memberships", Body(new { companyId = acme, name = "Stolen", seats = 1 })), HttpStatusCode.NotFound, "a membership for our company");
        (await Json(await theirs.GetAsync("/api/corporate-memberships"), "their list")).GetArrayLength().Should().Be(0);

        var theirMembership = await CorporateAsync(theirs, await CompanyAsync(theirs, "Theirs AG"));
        await Refused(await theirs.PostAsync($"/api/corporate-memberships/{Id(theirMembership)}/nominees", Body(new { memberId = anna })),
            HttpStatusCode.NotFound, "seating our member on theirs");
    }
}
