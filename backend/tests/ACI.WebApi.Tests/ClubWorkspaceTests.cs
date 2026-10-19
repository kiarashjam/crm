using System.Net;
using System.Text.Json;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The club screens on the server: members, visits, loyalty, and the record kinds kept
/// as documents (charges, payments, tabs, households, subscriptions, campaigns, …).
/// </summary>
/// <remarks>
/// They used to live in each browser, and the browser did the follow-on work: a charge
/// moved the member's balance, a payment settled its charge, a new household linked
/// its primary member. On the server that work has to happen here, or it does not
/// happen at all; the bodies below are the ones the app sends (src/app/api/*.ts).
/// </remarks>
public class ClubWorkspaceTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(HttpClient Client, Guid UserId, Guid Org)> ClubAsync(CustomWebApplicationFactory factory, string email = "kia@pavillon46.ch")
    {
        var (client, userId) = await SignInAsync(factory, email);
        var org = await CreateAsync(factory, userId, "Pavillon46");
        client.In(org);
        return (client, userId, org);
    }

    private static async Task<JsonElement> MemberAsync(HttpClient client, string first = "Jean") =>
        await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Dupont", email = $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
        })), "add member");

    private static async Task<decimal> BalanceAsync(HttpClient client, Guid memberId) =>
        (await Json(await client.GetAsync("/api/members"), "list members")).EnumerateArray()
            .Single(m => m.GetProperty("id").GetGuid() == memberId).GetProperty("houseAccountBalance").GetDecimal();

    [Theory]
    [InlineData("/api/members")]
    [InlineData("/api/visits")]
    [InlineData("/api/loyalty/ledger")]
    [InlineData("/api/events")]
    [InlineData("/api/charges")]
    [InlineData("/api/gift-cards")]
    [InlineData("/api/hospitality/settings")]
    [InlineData("/api/automations")]
    [InlineData("/api/teams")]
    public async Task WithoutAnOrganizationThereIsNoSharedBucket(string path)
    {
        // Without X-Organization-Id these tables used to read and write "no organization",
        // one bucket that every such user shared — members' emails and balances included.
        using var factory = new CustomWebApplicationFactory();
        var (first, _) = await SignInAsync(factory, "first@example.ch");
        var (second, _) = await SignInAsync(factory, "second@example.ch");

        var write = await first.PostAsync(path, Body(new { firstName = "Jean", lastName = "Dupont", email = "jean@example.ch", name = "x" }));
        write.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.MethodNotAllowed);
        var read = await second.GetAsync(path);
        read.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await read.Content.ReadAsStringAsync()).Should().Contain("belongs to an organization");
    }

    [Fact]
    public async Task OneClubCannotSeeOrTouchAnothersMembersOrRecords()
    {
        using var factory = new CustomWebApplicationFactory();
        var (mine, _, _) = await ClubAsync(factory);
        var member = await MemberAsync(mine);
        var memberId = member.GetProperty("id").GetGuid();
        var evt = await Json(await mine.PostAsync("/api/events", Body(new { name = "Wine night", capacity = 20 })), "add event");
        var eventId = evt.GetProperty("id").GetGuid();
        var (theirs, _, _) = await ClubAsync(factory, "other@example.ch");

        (await Json(await theirs.GetAsync("/api/members"), "their members")).GetArrayLength().Should().Be(0);
        (await Json(await theirs.GetAsync("/api/events"), "their events")).GetArrayLength().Should().Be(0);
        (await theirs.PutAsync($"/api/members/{memberId}", Body(new { notes = "mine now" }))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await theirs.DeleteAsync($"/api/events/{eventId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await theirs.PostAsync("/api/visits", Body(new { memberId, venue = "Lounge" }))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await theirs.PostAsync("/api/loyalty/ledger", Body(new { memberId, memberName = "Jean Dupont", points = 500 })))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "points go to a member of this club, not to any id with a name attached");
    }

    [Fact]
    public async Task ARecordsIdIsTheServers_SoNoOneCanSaveOverAnothersRecord()
    {
        using var factory = new CustomWebApplicationFactory();
        var (mine, _, _) = await ClubAsync(factory);
        var evt = await Json(await mine.PostAsync("/api/events", Body(new { name = "Wine night" })), "add event");
        var id = evt.GetProperty("id").GetString();
        var (theirs, _, _) = await ClubAsync(factory, "other@example.ch");

        var copy = await Json(await theirs.PostAsync("/api/events", Body(new { id, name = "Hijacked" })), "post with someone else's id");

        copy.GetProperty("id").GetString().Should().NotBe(id);
        (await Json(await mine.GetAsync("/api/events"), "my events"))[0].GetProperty("name").GetString().Should().Be("Wine night");
    }

    [Fact]
    public async Task ChargesAndPaymentsKeepTheHouseBalance()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = (await MemberAsync(client)).GetProperty("id").GetGuid();

        var charge = await Json(await client.PostAsync("/api/charges", Body(new
        {
            memberId, memberName = "Jean Dupont", kind = "Dining", description = "Dinner", amount = 100,
        })), "post charge");
        charge.GetProperty("status").GetString().Should().Be("Pending");
        (await BalanceAsync(client, memberId)).Should().Be(-100, "the browser used to write zero here: it summed its own, empty, copy of the charges");

        var payment = await Json(await client.PostAsync("/api/payments", Body(new
        {
            memberId, memberName = "Jean Dupont", amount = 40, method = "Cash", chargeId = charge.GetProperty("id").GetString(),
        })), "record payment");
        payment.GetProperty("appliedToChargeIds").EnumerateArray().Select(e => e.GetString())
            .Should().Equal(charge.GetProperty("id").GetString());
        var settled = (await Json(await client.GetAsync("/api/charges"), "list charges"))[0];
        settled.GetProperty("paidAmount").GetDecimal().Should().Be(40);
        settled.GetProperty("status").GetString().Should().Be("PartiallyPaid");
        (await BalanceAsync(client, memberId)).Should().Be(-60);

        (await client.DeleteAsync($"/api/charges/{charge.GetProperty("id").GetString()}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await BalanceAsync(client, memberId)).Should().Be(0);
    }

    [Fact]
    public async Task ClosingABarTabPostsABarChargeAndPoints()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = (await MemberAsync(client)).GetProperty("id").GetGuid();
        var tab = await Json(await client.PostAsync("/api/tabs", Body(new { memberId, venue = "Bar", serverName = "Ana" })), "open tab");
        var tabId = tab.GetProperty("id").GetString();
        await Json(await client.PostAsync($"/api/tabs/{tabId}/items", Body(new { name = "Negroni", quantity = 2, unitPrice = 15 })), "add item");
        var gone = await Json(await client.PostAsync($"/api/tabs/{tabId}/items", Body(new { name = "Olives", quantity = 1, unitPrice = 6 })), "add item");
        var olives = gone.GetProperty("items")[1].GetProperty("id").GetString();
        (await Json(await client.DeleteAsync($"/api/tabs/{tabId}/items/{olives}"), "remove item")).GetProperty("total").GetDecimal().Should().Be(30);

        var closed = await Json(await client.PostAsync($"/api/tabs/{tabId}/close", null), "close tab");

        closed.GetProperty("status").GetString().Should().Be("Closed");
        closed.GetProperty("pointsAwarded").GetInt32().Should().Be(450, "$30 × 10 points × Gold's 1.5");
        var charge = (await Json(await client.GetAsync("/api/charges"), "charges"))[0];
        charge.GetProperty("kind").GetString().Should().Be("Bar", "every tab used to post as Dining");
        charge.GetProperty("description").GetString().Should().Be("Library Bar tab (1 items)");
        (await BalanceAsync(client, memberId)).Should().Be(-30);
        (await Json(await client.GetAsync("/api/loyalty/ledger"), "ledger"))[0].GetProperty("reason").GetString().Should().Be("BarPurchase");
    }

    [Fact]
    public async Task ANewHouseholdLinksItsPrimaryMember_AndDeletingItTakesTheLinks()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = (await MemberAsync(client)).GetProperty("id").GetString();

        var household = await Json(await client.PostAsync("/api/households", Body(new { name = "Dupont family", primaryMemberId = memberId })), "add household");

        var link = (await Json(await client.GetAsync("/api/household-members"), "links")).EnumerateArray().Should().ContainSingle().Subject;
        link.GetProperty("memberId").GetString().Should().Be(memberId);
        link.GetProperty("role").GetString().Should().Be("Primary");
        (await client.DeleteAsync($"/api/households/{household.GetProperty("id").GetString()}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Json(await client.GetAsync("/api/household-members"), "links after delete")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task ServerStampsWhatTheScreensRelyOn()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = (await MemberAsync(client)).GetProperty("id").GetString();

        var sub = await Json(await client.PostAsync("/api/subscriptions", Body(new { memberId, memberName = "Jean Dupont", name = "Locker", amount = 25, cadence = "Quarterly" })), "add subscription");
        DateTime.Parse(sub.GetProperty("nextChargeAtUtc").GetString()!).ToUniversalTime()
            .Should().BeCloseTo(DateTime.UtcNow.AddDays(91), TimeSpan.FromMinutes(5), "the screen showed Invalid Date without it");

        var scheduled = await Json(await client.PostAsync("/api/campaigns", Body(new { name = "Autumn", subject = "Hi", body = "Hello", scheduledAtUtc = DateTime.UtcNow.AddDays(2) })), "schedule campaign");
        scheduled.GetProperty("status").GetString().Should().Be("Scheduled");
        scheduled.TryGetProperty("createdAtUtc", out _).Should().BeTrue();

        var resource = (await Json(await client.GetAsync("/api/resources"), "resources"))[0];
        var reservation = await Json(await client.PostAsync("/api/reservations", Body(new
        {
            resourceId = resource.GetProperty("id").GetString(), memberId, startAtUtc = DateTime.UtcNow.AddDays(1),
        })), "reserve");
        reservation.GetProperty("resourceName").GetString().Should().Be(resource.GetProperty("name").GetString());

        var note = await Json(await client.PostAsync("/api/communications", Body(new { memberId, channel = "Email", subject = "Welcome" })), "log message");
        note.GetProperty("status").GetString().Should().Be("Sent");
    }

    [Fact]
    public async Task LongValuesAreRefusedWithAReason()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);

        var res = await client.PostAsync("/api/members", Body(new { firstName = new string('J', 101), lastName = "Dupont", email = "j@example.ch" }));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("at most 100 characters");
    }

    [Fact]
    public async Task OnlyAManagerChangesTheHospitalitySettings()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, org) = await ClubAsync(factory);
        var (member, memberId) = await SignInAsync(factory, "staff@pavillon46.ch");
        await AddMemberAsync(factory, org, memberId, OrgMemberRole.Member);
        member.In(org);

        var refused = await member.PutAsync("/api/hospitality/settings", Body(new { clubName = "Mine" }));
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Only the owner or a manager can change the hospitality settings.");
        (await member.PostAsync("/api/hospitality/settings/reset", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Json(await owner.PutAsync("/api/hospitality/settings", Body(new { clubName = "Pavillon46" })), "owner saves"))
            .GetProperty("clubName").GetString().Should().Be("Pavillon46");
        await Json(await member.GetAsync("/api/hospitality/settings"), "staff can still read them");
    }
}
