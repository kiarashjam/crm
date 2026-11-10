using System.Globalization;
using System.Net;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Bookings of the club's resources, checked by the server: clashes and capacity answered with
/// a 409 that names what is in the way, a booking for a member by id, and deposits and no-show
/// fees on the member's house account through the ledger.
/// </summary>
/// <remarks>
/// The reservations page checked nothing the server would hold to: two front desks booked the
/// one spa suite for the same hour, a party of 14 went into a room for 10, and a booking found
/// its member by email text. Two bookings of one slot at the same moment are shown on SQL
/// Server (ClubEventsOnSqlServerTests), which has the locks.
/// </remarks>
public class ClubReservationsTests
{
    private static readonly TimeZoneInfo Zurich = RegionalSettings.ZoneOf(RegionalSettings.DefaultTimeZone);

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

    private static async Task<(HttpClient Client, Guid Org)> ClubAsync(CustomWebApplicationFactory factory, string email = "desk@pavillon46.ch")
    {
        var (client, userId) = await SignInAsync(factory, email);
        var org = await CreateAsync(factory, userId, "Pavillon46");
        client.In(org);
        return (client, org);
    }

    private static async Task<HttpClient> JoinAsync(CustomWebApplicationFactory factory, HttpClient owner, Guid org, string email, string preset)
    {
        var (client, userId) = await SignInAsync(factory, email);
        await AddMemberAsync(factory, org, userId, OrgMemberRole.Member);
        await Json(await owner.PutAsync($"/api/organizations/{org}/members/{userId}/permissions", Body(new { preset })), $"make {email} {preset}");
        return client.In(org);
    }

    private static async Task<Guid> MemberAsync(HttpClient client, string first, string? email = null) =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Dupont", email = email ?? $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
        })), $"add {first}")).GetProperty("id").GetGuid();

    private static async Task<JsonElement> ResourceAsync(HttpClient client, string name, string kind, int capacity, string? mode = null, decimal deposit = 0, decimal noShowFee = 0) =>
        await Json(await client.PostAsync("/api/resources", Body(new { name, kind, capacity, bookingMode = mode, depositAmount = deposit, noShowFee })), $"add {name}");

    /// <summary>Tomorrow at an hour in UTC, so the club's own times in a refusal can be worked out.</summary>
    private static DateTime Tomorrow(double hourUtc) => DateTime.UtcNow.Date.AddDays(2).AddHours(hourUtc);

    private static Task<HttpResponseMessage> BookAsync(HttpClient client, JsonElement resource, DateTime start, DateTime end, int party = 1,
        Guid? memberId = null, string? guest = null, string? deposit = null, string? depositMethod = null, string? depositReason = null) =>
        client.PostAsync("/api/reservations", Body(new
        {
            resourceId = resource.GetProperty("id").GetString(), memberId, memberName = guest, partySize = party,
            startAtUtc = start, endAtUtc = end, status = "Confirmed", deposit, depositMethod, depositReason,
        }));

    private static Task<HttpResponseMessage> SetStatusAsync(HttpClient client, JsonElement booking, string status) =>
        client.PutAsync($"/api/reservations/{booking.GetProperty("id").GetString()}", Body(new { status }));

    private static string Local(DateTime utc, string format) =>
        TimeZoneInfo.ConvertTimeFromUtc(utc, Zurich).ToString(format, CultureInfo.GetCultureInfo("en-GB"));

    private static async Task<decimal> BalanceAsync(HttpClient client, Guid memberId) =>
        (await Json(await client.GetAsync("/api/members"), "members")).EnumerateArray()
            .Single(m => m.GetProperty("id").GetGuid() == memberId).GetProperty("houseAccountBalance").GetDecimal();

    private static async Task<JsonElement> ChargeAsync(HttpClient client, string? id) =>
        (await Json(await client.GetAsync("/api/charges"), "charges")).EnumerateArray().Single(c => c.GetProperty("id").GetString() == id);

    [Fact]
    public async Task AResourceBookedOneAtATimeRefusesAnOverlap_AndSaysWhichBookingIsInTheWay()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _) = await ClubAsync(factory);
        var spa = await ResourceAsync(client, "Spa Suite", "Spa", 1);
        var jean = await MemberAsync(client, "Jean");
        var start = Tomorrow(13);
        await Json(await BookAsync(client, spa, start, start.AddHours(1), memberId: jean), "Jean's massage");

        var clash = await Refused(await BookAsync(client, spa, start.AddMinutes(30), start.AddMinutes(90), guest: "Claire Martin"), HttpStatusCode.Conflict, "an overlapping booking");

        clash.Should().Contain($"Spa Suite is already booked on {Local(start, "ddd d MMM")} from {Local(start, "HH:mm")} to {Local(start.AddHours(1), "HH:mm")} by Jean Dupont (confirmed)");
        await Json(await BookAsync(client, spa, start.AddHours(1), start.AddHours(2), guest: "Claire Martin"), "the next hour, which only touches it");
        spa.GetProperty("bookingMode").GetString().Should().Be("Exclusive", "a spa is booked one at a time unless said otherwise");
    }

    [Fact]
    public async Task ASharedResourceTakesBookingsUpToItsCapacity_AndSaysHowFullItIs()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _) = await ClubAsync(factory);
        var dining = await ResourceAsync(client, "Main Dining Room", "Dining", 10);
        var start = Tomorrow(18);
        await Json(await BookAsync(client, dining, start, start.AddHours(2), party: 6, guest: "Table one"), "a table of 6");
        await Json(await BookAsync(client, dining, start.AddHours(1), start.AddHours(3), party: 4, guest: "Table two"), "a table of 4 that overlaps it");

        var full = await Refused(await BookAsync(client, dining, start.AddMinutes(90), start.AddMinutes(150), party: 1, guest: "One more"), HttpStatusCode.Conflict, "one more while both are seated");

        full.Should().Contain("Main Dining Room holds 10, and 10 are already booked at").And.Contain("(2 bookings)").And.Contain("0 places are free then");
        await Json(await BookAsync(client, dining, start.AddHours(2), start.AddHours(3), party: 6, guest: "Late table"), "a table of 6 once the first has left");
    }

    [Fact]
    public async Task APartyBiggerThanTheResourceIsRefused()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _) = await ClubAsync(factory);
        var boardroom = await ResourceAsync(client, "Boardroom", "Meeting", 10);

        (await Refused(await BookAsync(client, boardroom, Tomorrow(9), Tomorrow(11), party: 14, guest: "Offsite"), HttpStatusCode.Conflict, "a party of 14"))
            .Should().Contain("Boardroom holds 10, so a party of 14 can't be booked into it");
    }

    [Fact]
    public async Task ACancelledBookingGivesItsTimeBack_AndMovingABookingChecksItsNewTime()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _) = await ClubAsync(factory);
        var court = await ResourceAsync(client, "Tennis Court", "Court", 4);
        var first = await Json(await BookAsync(client, court, Tomorrow(8), Tomorrow(9), party: 2, guest: "Early pair"), "8 o'clock");
        var second = await Json(await BookAsync(client, court, Tomorrow(10), Tomorrow(11), party: 2, guest: "Late pair"), "10 o'clock");

        await Refused(await client.PutAsync($"/api/reservations/{second.GetProperty("id").GetString()}", Body(new { startAtUtc = Tomorrow(8).AddMinutes(30), endAtUtc = Tomorrow(9).AddMinutes(30) })),
            HttpStatusCode.Conflict, "moving onto the 8 o'clock");
        await Json(await SetStatusAsync(client, first, "Cancelled"), "the 8 o'clock cancels");
        var moved = await Json(await client.PutAsync($"/api/reservations/{second.GetProperty("id").GetString()}", Body(new { startAtUtc = Tomorrow(8), endAtUtc = Tomorrow(9) })), "moving onto the freed time");
        moved.GetProperty("startAtUtc").GetDateTime().ToUniversalTime().Should().Be(Tomorrow(8));
    }

    [Fact]
    public async Task ABookingIsForAMemberById_NotByMatchingTheirEmail()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _) = await ClubAsync(factory);
        var lounge = await ResourceAsync(client, "Lounge", "Lounge", 20);
        var jean = await MemberAsync(client, "Jean");
        var namesake = await MemberAsync(client, "Jeanne", email: "jean@example.ch");

        var booking = await Json(await client.PostAsync("/api/reservations", Body(new
        {
            resourceId = lounge.GetProperty("id").GetString(), memberId = jean, memberName = "Typed by hand", partySize = 3,
            startAtUtc = Tomorrow(17), endAtUtc = Tomorrow(19),
        })), "book for Jean");

        booking.GetProperty("memberName").GetString().Should().Be("Jean Dupont", "the name comes from the member record");
        booking.GetProperty("memberEmail").GetString().Should().Be("jean@example.ch");
        booking.GetProperty("status").GetString().Should().Be("Pending");
        (await Json(await client.GetAsync($"/api/reservations?memberId={jean}"), "Jean's bookings")).GetArrayLength().Should().Be(1);
        (await Json(await client.GetAsync($"/api/reservations?memberId={namesake}"), "the namesake's bookings")).GetArrayLength()
            .Should().Be(0, "an email two members share is no way to find whose booking it is");
    }

    [Fact]
    public async Task ADepositGoesOnTheAccount_IsGivenBackWhenTheBookingIsKept_AndIsKeptWithTheFeeOnANoShow()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _) = await ClubAsync(factory);
        var room = await ResourceAsync(client, "Private Dining Room", "Dining", 12, mode: "Exclusive", deposit: 100, noShowFee: 50);
        var jean = await MemberAsync(client, "Jean");
        var marie = await MemberAsync(client, "Marie");

        var kept = await Json(await BookAsync(client, room, Tomorrow(18), Tomorrow(21), party: 8, memberId: jean), "Jean's dinner");
        // Today's lunch, begun an hour ago: a no-show waits for the booking's start.
        var missed = await Json(await BookAsync(client, room, DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1), party: 6, memberId: marie), "Marie's lunch");

        kept.GetProperty("depositAmount").GetDecimal().Should().Be(100);
        kept.GetProperty("depositState").GetString().Should().Be("charged");
        (await ChargeAsync(client, kept.GetProperty("depositChargeId").GetString())).GetProperty("kind").GetString().Should().Be("Booking");
        (await BalanceAsync(client, jean)).Should().Be(-100);

        var noShow = await Json(await SetStatusAsync(client, missed, "NoShow"), "Marie does not come");
        noShow.GetProperty("depositState").GetString().Should().Be("kept");
        noShow.GetProperty("noShowFee").GetDecimal().Should().Be(50);
        noShow.GetProperty("noShowFeeState").GetString().Should().Be("charged");
        (await BalanceAsync(client, marie)).Should().Be(-150, "the deposit she forfeits and the room's no-show fee");

        var checkedIn = await Json(await SetStatusAsync(client, kept, "CheckedIn"), "Jean arrives");
        checkedIn.GetProperty("depositState").GetString().Should().Be("released");
        (await ChargeAsync(client, kept.GetProperty("depositChargeId").GetString())).GetProperty("status").GetString().Should().Be("Voided");
        (await BalanceAsync(client, jean)).Should().Be(0, "the deposit secured the booking, and the booking was kept");

        var late = await Json(await SetStatusAsync(client, missed, "CheckedIn"), "Marie arrives after all");
        late.GetProperty("noShowFeeState").GetString().Should().Be("reversed");
        late.GetProperty("depositState").GetString().Should().Be("released");
        (await BalanceAsync(client, marie)).Should().Be(0);
    }

    [Fact]
    public async Task ABookingTakenAtTheDoorAsksNoDeposit()
    {
        // A deposit secures a booking until the member comes, and is given back when they are
        // checked in. A booking made already checked in (the page offers it, for a table taken at
        // the door) was charged the deposit all the same, and nothing ever gave it back.
        using var factory = new CustomWebApplicationFactory();
        var (client, _) = await ClubAsync(factory);
        var room = await ResourceAsync(client, "Private Dining Room", "Dining", 12, mode: "Exclusive", deposit: 100);
        var jean = await MemberAsync(client, "Jean");

        var atTheDoor = await Json(await client.PostAsync("/api/reservations", Body(new
        {
            resourceId = room.GetProperty("id").GetString(), memberId = jean, partySize = 4,
            startAtUtc = DateTime.UtcNow.AddMinutes(-5), endAtUtc = DateTime.UtcNow.AddHours(2), status = "CheckedIn",
        })), "a table taken at the door");

        atTheDoor.GetProperty("status").GetString().Should().Be("CheckedIn");
        atTheDoor.GetProperty("depositAmount").GetDecimal().Should().Be(0);
        atTheDoor.GetProperty("depositState").GetString().Should().Be("none");
        (await Json(await client.GetAsync("/api/charges"), "charges")).GetArrayLength().Should().Be(0);
        (await BalanceAsync(client, jean)).Should().Be(0);
    }

    [Fact]
    public async Task ABookingOfAMemberWhoWasDeleted_CanStillBeMarkedANoShow()
    {
        // The booking kept the deleted member's id, so the no-show fee went to an account nobody
        // has, the ledger refused it, and with it the status change.
        using var factory = new CustomWebApplicationFactory();
        var (client, _) = await ClubAsync(factory);
        var court = await ResourceAsync(client, "Tennis Court", "Court", 4, noShowFee: 25);
        var jean = await MemberAsync(client, "Jean");
        var booking = await Json(await BookAsync(client, court, DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow.AddMinutes(30), party: 2, memberId: jean), "Jean books the court");
        (await client.DeleteAsync($"/api/members/{jean}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var bookings = await Json(await client.GetAsync("/api/reservations"), "bookings");
        bookings[0].TryGetProperty("memberId", out _).Should().BeFalse("the member it pointed at is gone");
        bookings[0].GetProperty("memberName").GetString().Should().Be("Jean Dupont", "the booking stays under the name it was made in");

        var noShow = await Json(await client.PutAsync($"/api/reservations/{booking.GetProperty("id").GetString()}",
            Body(new { status = "NoShow" })), "he does not come");
        noShow.GetProperty("status").GetString().Should().Be("NoShow");
        noShow.TryGetProperty("noShowChargeId", out _).Should().BeFalse("there is no member account left to post the fee to");
        (await Json(await client.GetAsync("/api/charges"), "charges")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task NobodyIsMarkedANoShowBeforeTheirBookingStarts()
    {
        // A no-show posts the resource's fee and keeps the deposit. An event refuses one before
        // it starts; a booking took one at any time, so a slip on tomorrow's booking in the
        // status list charged the member for missing it already.
        using var factory = new CustomWebApplicationFactory();
        var (client, _) = await ClubAsync(factory);
        var room = await ResourceAsync(client, "Private Dining Room", "Dining", 12, mode: "Exclusive", deposit: 100, noShowFee: 50);
        var jean = await MemberAsync(client, "Jean");
        var tomorrow = await Json(await BookAsync(client, room, Tomorrow(18), Tomorrow(21), party: 6, memberId: jean), "tomorrow's dinner");

        (await Refused(await SetStatusAsync(client, tomorrow, "NoShow"), HttpStatusCode.Conflict, "a no-show before the booking starts"))
            .Should().Contain("hasn't started");
        (await BalanceAsync(client, jean)).Should().Be(-100, "the deposit alone: no fee was posted");
        var bookings = await Json(await client.GetAsync("/api/reservations"), "bookings");
        bookings[0].GetProperty("status").GetString().Should().Be("Confirmed");
        bookings[0].GetProperty("depositState").GetString().Should().Be("charged");
    }

    [Fact]
    public async Task ADepositPaidAtTheDeskTakesTheMoneyPermission_AndComesBackAsCreditOnACancellation()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, org) = await ClubAsync(factory, "owner@pavillon46.ch");
        var desk = await JoinAsync(factory, owner, org, "desk@pavillon46.ch", "front_desk");
        var room = await ResourceAsync(owner, "Private Dining Room", "Dining", 12, deposit: 100);
        var jean = await MemberAsync(owner, "Jean");

        (await Refused(await BookAsync(desk, room, Tomorrow(18), Tomorrow(21), memberId: jean, deposit: "paid", depositMethod: "Cash"),
            HttpStatusCode.Forbidden, "the front desk recording money")).Should().Contain("deposit as paid");
        var charged = await Json(await BookAsync(desk, room, Tomorrow(18), Tomorrow(21), memberId: jean), "the front desk charging the deposit to the account");
        charged.GetProperty("depositState").GetString().Should().Be("charged");
        await Json(await SetStatusAsync(desk, charged, "Cancelled"), "cancel");

        var paid = await Json(await BookAsync(owner, room, Tomorrow(18), Tomorrow(21), memberId: jean, deposit: "paid", depositMethod: "Cash"), "a deposit paid in cash");
        paid.GetProperty("depositState").GetString().Should().Be("paid");
        (await BalanceAsync(owner, jean)).Should().Be(0);

        var cancelled = await Json(await SetStatusAsync(owner, paid, "Cancelled"), "Jean cancels");
        cancelled.GetProperty("depositState").GetString().Should().Be("released");
        (await BalanceAsync(owner, jean)).Should().Be(100, "the cash he paid is credit on his account, to spend or be refunded");
        (await Json(await owner.GetAsync("/api/payments"), "payments")).GetArrayLength().Should().Be(1, "the payment stands");
    }

    [Fact]
    public async Task AGuestBookingOfADepositResourceNeedsAWaiver_WithAReason()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _) = await ClubAsync(factory, "owner@pavillon46.ch");
        var room = await ResourceAsync(owner, "Private Dining Room", "Dining", 12, deposit: 100);

        (await Refused(await BookAsync(owner, room, Tomorrow(18), Tomorrow(21), guest: "Outside party"), HttpStatusCode.Conflict, "a guest booking with a deposit"))
            .Should().Contain("goes on a member's house account");
        await Refused(await BookAsync(owner, room, Tomorrow(18), Tomorrow(21), guest: "Outside party", deposit: "waive"), HttpStatusCode.BadRequest, "a waiver with no reason");
        var waived = await Json(await BookAsync(owner, room, Tomorrow(18), Tomorrow(21), guest: "Outside party", deposit: "waive", depositReason: "Corporate account, invoiced"), "waived");

        waived.GetProperty("depositState").GetString().Should().Be("waived");
        waived.GetProperty("depositWaivedReason").GetString().Should().Be("Corporate account, invoiced");
        (await Json(await owner.GetAsync("/api/charges"), "charges")).GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task ABookingWithMoneyIsCancelledNotDeleted_AndOneWithoutIsDeleted()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _) = await ClubAsync(factory);
        var room = await ResourceAsync(client, "Private Dining Room", "Dining", 12, deposit: 100);
        var court = await ResourceAsync(client, "Tennis Court", "Court", 4);
        var jean = await MemberAsync(client, "Jean");
        var withDeposit = await Json(await BookAsync(client, room, Tomorrow(18), Tomorrow(21), memberId: jean), "with a deposit");
        var plain = await Json(await BookAsync(client, court, Tomorrow(8), Tomorrow(9), guest: "Pair"), "without");

        (await Refused(await client.DeleteAsync($"/api/reservations/{withDeposit.GetProperty("id").GetString()}"), HttpStatusCode.Conflict, "deleting it"))
            .Should().Contain("Cancel it instead");
        (await client.DeleteAsync($"/api/reservations/{plain.GetProperty("id").GetString()}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AStatusMoveThatMakesNoSenseIsRefused_AndAnEditFromAnOlderCopyToo()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _) = await ClubAsync(factory);
        var court = await ResourceAsync(client, "Tennis Court", "Court", 4);
        var booking = await Json(await BookAsync(client, court, Tomorrow(8), Tomorrow(9), guest: "Pair"), "book");
        var version = booking.GetProperty("version").GetString();

        var edited = await Json(await client.PutAsync($"/api/reservations/{booking.GetProperty("id").GetString()}", Body(new { notes = "Bring balls", version })), "edit the notes");
        edited.GetProperty("notes").GetString().Should().Be("Bring balls");
        (await Refused(await client.PutAsync($"/api/reservations/{booking.GetProperty("id").GetString()}", Body(new { partySize = 3, version })), HttpStatusCode.Conflict, "an edit from before"))
            .Should().Contain("at the same moment");

        await Json(await SetStatusAsync(client, booking, "Cancelled"), "cancel");
        (await Refused(await SetStatusAsync(client, booking, "Confirmed"), HttpStatusCode.Conflict, "un-cancelling")).Should().Contain("Book again instead");
    }

    [Fact]
    public async Task TheFrontDeskBooks_ButOnlyTheMembershipTeamManagesResources()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, org) = await ClubAsync(factory, "owner@pavillon46.ch");
        var desk = await JoinAsync(factory, owner, org, "desk@pavillon46.ch", "front_desk");
        var court = await ResourceAsync(owner, "Tennis Court", "Court", 4);

        await Refused(await desk.PostAsync("/api/resources", Body(new { name = "Mine", kind = "Court", capacity = 2 })), HttpStatusCode.Forbidden, "the desk adding a resource");
        await Refused(await desk.PutAsync($"/api/resources/{court.GetProperty("id").GetString()}", Body(new { depositAmount = 0 })), HttpStatusCode.Forbidden, "the desk changing a deposit");
        await Json(await BookAsync(desk, court, Tomorrow(8), Tomorrow(9), guest: "Pair"), "the desk booking");
    }

    [Fact]
    public async Task AResourceWithBookingsIsMadeNotBookableRatherThanDeleted()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _) = await ClubAsync(factory);
        var court = await ResourceAsync(client, "Tennis Court", "Court", 4);
        await Json(await BookAsync(client, court, Tomorrow(8), Tomorrow(9), guest: "Pair"), "book");
        var id = court.GetProperty("id").GetString();

        (await Refused(await client.DeleteAsync($"/api/resources/{id}"), HttpStatusCode.Conflict, "deleting it")).Should().Contain("not bookable instead");
        var closed = await Json(await client.PutAsync($"/api/resources/{id}", Body(new { active = false, version = court.GetProperty("version").GetString() })), "close it");
        closed.GetProperty("active").GetBoolean().Should().BeFalse();
        (await Refused(await BookAsync(client, court, Tomorrow(10), Tomorrow(11), guest: "Another pair"), HttpStatusCode.Conflict, "booking it")).Should().Contain("not taking bookings");
    }

    [Fact]
    public async Task TheUsualResourcesAreAddedOnce_AndABookingDoesNotMakeAResourceEditOld()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _) = await ClubAsync(factory);
        (await Json(await client.GetAsync("/api/resources"), "none yet")).GetArrayLength().Should().Be(0, "reading the list adds nothing");

        var first = await Json(await client.PostAsync("/api/resources/defaults", null), "the usual spaces");
        var again = await Json(await client.PostAsync("/api/resources/defaults", null), "asked again");

        first.GetArrayLength().Should().Be(7);
        again.GetArrayLength().Should().Be(7);
        var spa = first.EnumerateArray().Single(r => r.GetProperty("name").GetString() == "Spa Suite");
        await Json(await BookAsync(client, spa, Tomorrow(9), Tomorrow(10), guest: "Claire"), "a booking meanwhile");
        var edited = await Json(await client.PutAsync($"/api/resources/{spa.GetProperty("id").GetString()}",
            Body(new { noShowFee = 30, version = spa.GetProperty("version").GetString() })), "an edit from the copy read before the booking");
        edited.GetProperty("noShowFee").GetDecimal().Should().Be(30);
    }
}
