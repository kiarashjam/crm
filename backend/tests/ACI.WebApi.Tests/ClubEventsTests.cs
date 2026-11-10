using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Club events and who is coming, kept by the server: the capacity and the waitlist, tickets on
/// house accounts through the ledger, the door, and the emails a seat or a cancellation sends.
/// </summary>
/// <remarks>
/// The list used to live in each browser and then in documents with the rules in the page: two
/// people could take the last seat, a quick RSVP named nobody, a cancellation moved nobody off
/// the waitlist, and no ticket was ever charged. Two people taking the last seat at the same
/// moment is shown on SQL Server (ClubEventsOnSqlServerTests), which has the locks.
/// </remarks>
public class ClubEventsTests
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

    private static async Task<(HttpClient Client, Guid UserId, Guid Org)> ClubAsync(CustomWebApplicationFactory factory, string email = "events@pavillon46.ch")
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

    private static async Task<Guid> MemberAsync(HttpClient client, string first, string tier = "Gold") =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Dupont", email = $"{first.ToLowerInvariant()}@example.ch", tier, status = "Active",
        })), $"add {first}")).GetProperty("id").GetGuid();

    /// <summary>An event a week from now, or one that started an hour ago.</summary>
    private static async Task<Guid> EventAsync(HttpClient client, int capacity, decimal memberPrice = 0, decimal guestPrice = 0, decimal noShowFee = 0, bool started = false)
    {
        var start = started ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddDays(7);
        return (await Json(await client.PostAsync("/api/events", Body(new
        {
            name = "Wine night", location = "Library Bar", startAtUtc = start, endAtUtc = start.AddHours(3), capacity, memberPrice, guestPrice, noShowFee,
            status = "Published",
        })), "add event")).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> EventOf(HttpClient client, Guid id) =>
        await Json(await client.GetAsync($"/api/events/{id}"), "read event");

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, Guid eventId, Guid memberId) =>
        client.PostAsync($"/api/events/{eventId}/attendees", Body(new { memberId, ticketTier = "Member" }));

    private static Task<HttpResponseMessage> AddGuestAsync(HttpClient client, Guid eventId, string name, Guid? hostMemberId = null, string? email = null) =>
        client.PostAsync($"/api/events/{eventId}/attendees", Body(new { memberName = name, ticketTier = "Guest", hostMemberId, email }));

    private static Task<HttpResponseMessage> SetStatusAsync(HttpClient client, Guid eventId, JsonElement attendee, string status, string? reason = null) =>
        client.PutAsync($"/api/events/{eventId}/attendees/{attendee.GetProperty("id").GetString()}", Body(new { status, reason }));

    private static async Task<JsonElement> ChargeAsync(HttpClient client, string? id) =>
        (await Json(await client.GetAsync("/api/charges"), "charges")).EnumerateArray().Single(c => c.GetProperty("id").GetString() == id);

    private static async Task<decimal> BalanceAsync(HttpClient client, Guid memberId) =>
        (await Json(await client.GetAsync("/api/members"), "members")).EnumerateArray()
            .Single(m => m.GetProperty("id").GetGuid() == memberId).GetProperty("houseAccountBalance").GetDecimal();

    [Fact]
    public async Task TheDoorHostSeesTheListTheEventsManagerEntered()
    {
        using var factory = new CustomWebApplicationFactory();
        var (manager, owner, org) = await ClubAsync(factory);
        var doorHost = await JoinAsync(factory, manager, org, "door@pavillon46.ch", "front_desk");
        var eventId = await EventAsync(manager, capacity: 40);

        await Json(await AddGuestAsync(manager, eventId, "Claire Martin"), "add attendee");

        var list = await Json(await doorHost.GetAsync($"/api/events/{eventId}/attendees"), "door host lists attendees");
        list.GetArrayLength().Should().Be(1);
        list[0].GetProperty("memberName").GetString().Should().Be("Claire Martin");
        list[0].GetProperty("status").GetString().Should().Be("Reserved");
        var checkedIn = await Json(await SetStatusAsync(doorHost, eventId, list[0], "CheckedIn"), "door host checks in");
        checkedIn.GetProperty("status").GetString().Should().Be("CheckedIn");
        checkedIn.GetProperty("checkedInByName").GetString().Should().NotBeNullOrEmpty();
        var ev = await EventOf(manager, eventId);
        ev.GetProperty("registeredCount").GetInt32().Should().Be(1, "a check-in is still a seat");
        ev.GetProperty("checkedInCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task TheFrontDeskRunsTheDoorButOnlyTheMembershipTeamEditsEvents()
    {
        using var factory = new CustomWebApplicationFactory();
        var (manager, _, org) = await ClubAsync(factory);
        var desk = await JoinAsync(factory, manager, org, "desk@pavillon46.ch", "front_desk");
        var eventId = await EventAsync(manager, capacity: 10);
        var jean = await MemberAsync(manager, "Jean");

        var walkIn = await Json(await desk.PostAsync($"/api/events/{eventId}/attendees/walk-in", Body(new { memberId = jean })), "the desk takes a walk-in");
        walkIn.GetProperty("status").GetString().Should().Be("CheckedIn");
        await Refused(await desk.PutAsync($"/api/events/{eventId}", Body(new { capacity = 99 })), HttpStatusCode.Forbidden, "the desk editing the event");
        await Refused(await desk.PostAsync("/api/events", Body(new { name = "Mine" })), HttpStatusCode.Forbidden, "the desk adding an event");
    }

    [Fact]
    public async Task ACheckInIsMadeOnce_AndGivesTheMemberTheirPointsOnce()
    {
        // Two door hosts share the list. The second one's screen can still show the member as
        // reserved, and the page used to give the points for every check-in it saw succeed.
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var eventId = await EventAsync(client, capacity: 40);
        var jean = await MemberAsync(client, "Jean", tier: "Gold");
        var attendee = await Json(await RegisterAsync(client, eventId, jean), "register Jean");

        var first = await Json(await SetStatusAsync(client, eventId, attendee, "CheckedIn"), "first check-in");
        var again = await Refused(await SetStatusAsync(client, eventId, attendee, "CheckedIn"), HttpStatusCode.Conflict, "a second check-in");

        again.Should().Contain("already checked in");
        first.GetProperty("pointsAwarded").GetInt32().Should().Be(75, "50 points at Gold's 1.5");
        var points = await Json(await client.GetAsync("/api/loyalty/ledger"), "points");
        points.EnumerateArray().Where(p => p.GetProperty("reason").GetString() == "EventAttendance")
            .Select(p => p.GetProperty("points").GetInt32()).Should().Equal(75);
    }

    [Fact]
    public async Task AFullEventWaitlistsTheNext_AndACancellationGivesThemTheSeat_TheirTicket_AndAnEmail()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, org) = await ClubAsync(factory);
        var eventId = await EventAsync(client, capacity: 1, memberPrice: 35);
        var jean = await MemberAsync(client, "Jean");
        var marie = await MemberAsync(client, "Marie");

        var first = await Json(await RegisterAsync(client, eventId, jean), "register Jean");
        var second = await Json(await RegisterAsync(client, eventId, marie), "register Marie");

        first.GetProperty("status").GetString().Should().Be("Reserved");
        first.GetProperty("chargeState").GetString().Should().Be("charged");
        (await ChargeAsync(client, first.GetProperty("chargeId").GetString())).GetProperty("amount").GetDecimal().Should().Be(35);
        second.GetProperty("status").GetString().Should().Be("Waitlisted", "the one seat is taken");
        second.GetProperty("waitlistPosition").GetInt32().Should().Be(1);
        second.GetProperty("chargeState").GetString().Should().Be("waiting", "a place in line is not charged");
        second.TryGetProperty("chargeId", out _).Should().BeFalse();
        await Refused(await SetStatusAsync(client, eventId, second, "Reserved"), HttpStatusCode.Conflict, "a seat while the event is full");

        var cancelled = await Json(await SetStatusAsync(client, eventId, first, "Cancelled", "Ill"), "Jean cancels");

        cancelled.GetProperty("chargeState").GetString().Should().Be("reversed");
        (await ChargeAsync(client, first.GetProperty("chargeId").GetString())).GetProperty("status").GetString().Should().Be("Voided",
            "a cancellation takes the ticket back with a void, never a delete");
        (await BalanceAsync(client, jean)).Should().Be(0);
        var list = await Json(await client.GetAsync($"/api/events/{eventId}/attendees"), "list");
        var promoted = list.EnumerateArray().Single(a => a.GetProperty("memberId").GetGuid() == marie);
        promoted.GetProperty("status").GetString().Should().Be("Reserved", "the first in line gets the seat that came free");
        promoted.GetProperty("chargeState").GetString().Should().Be("charged");
        (await BalanceAsync(client, marie)).Should().Be(-35);

        var mail = await factory.Services.Read(db => db.OutboundMessages.Where(m => m.OrganizationId == org).ToList());
        mail.Should().ContainSingle("Marie is told; Jean, who cancelled, is not");
        mail[0].To.Should().Be("marie@example.ch");
        mail[0].Source.Should().Be(OutboundSources.Event);
        mail[0].Purpose.Should().Be(ConsentPurposes.Service, "a seat is a booking message, not marketing");
        mail[0].Subject.Should().Be("A place for you at Wine night");
        mail[0].Body.Should().Contain("CHF 35.00").And.Contain("house account");
        var logged = await factory.Services.Read(db => db.MemberMessages.Where(m => m.OrganizationId == org).ToList());
        logged.Should().ContainSingle().Which.Category.Should().Be("Event", "it shows in Marie's communications");
        var ev = await EventOf(client, eventId);
        ev.GetProperty("registeredCount").GetInt32().Should().Be(1);
        ev.GetProperty("waitlistCount").GetInt32().Should().Be(0);
        ev.GetProperty("ticketRevenue").GetDecimal().Should().Be(35, "only the ticket that stands counts");
    }

    [Fact]
    public async Task ATicketPaidAndThenCancelledIsHouseCreditAgain()
    {
        // The ledger refuses to void a charge with payments against it. A cancelled seat must
        // not wait for someone who can void money: its money comes back as house credit.
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var eventId = await EventAsync(client, capacity: 10, memberPrice: 40);
        var jean = await MemberAsync(client, "Jean");
        var attendee = await Json(await RegisterAsync(client, eventId, jean), "register Jean");
        var chargeId = attendee.GetProperty("chargeId").GetString();
        await Json(await client.PostAsync("/api/payments", Body(new { amount = 40, method = "Cash", chargeId })), "Jean pays his ticket");
        (await BalanceAsync(client, jean)).Should().Be(0);

        await Json(await SetStatusAsync(client, eventId, attendee, "Cancelled"), "Jean cancels");

        var charge = await ChargeAsync(client, chargeId);
        charge.GetProperty("status").GetString().Should().Be("Voided");
        (await BalanceAsync(client, jean)).Should().Be(40, "what he paid is credit on his account");
        var payments = await Json(await client.GetAsync("/api/payments"), "payments");
        payments.GetArrayLength().Should().Be(1, "the payment stands; paying the credit out is a refund for someone allowed to");
        payments[0].TryGetProperty("status", out _).Should().BeFalse();
        var balance = await Json(await client.GetAsync($"/api/members/{jean}/balance"), "balance");
        balance.GetProperty("credit").GetDecimal().Should().Be(40, "the credit can pay another charge");
    }

    [Fact]
    public async Task AGuestTicketGoesOnTheHostsAccount_AndOneNobodyBringsIsToCollect()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var eventId = await EventAsync(client, capacity: 10, memberPrice: 35, guestPrice: 55);
        var jean = await MemberAsync(client, "Jean");

        var brought = await Json(await AddGuestAsync(client, eventId, "Claire Martin", hostMemberId: jean), "Jean brings Claire");
        var alone = await Json(await AddGuestAsync(client, eventId, "Paul Weber"), "a guest on their own");

        brought.GetProperty("hostName").GetString().Should().Be("Jean Dupont");
        brought.GetProperty("ticketPrice").GetDecimal().Should().Be(55);
        (await ChargeAsync(client, brought.GetProperty("chargeId").GetString())).GetProperty("memberId").GetGuid().Should().Be(jean);
        (await BalanceAsync(client, jean)).Should().Be(-55);
        alone.GetProperty("chargeState").GetString().Should().Be("to-collect", "there is no house account to put it on");
        alone.TryGetProperty("chargeId", out _).Should().BeFalse();
    }

    [Fact]
    public async Task AMemberIsOnTheListOnce()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var eventId = await EventAsync(client, capacity: 10, memberPrice: 35);
        var jean = await MemberAsync(client, "Jean");
        await Json(await RegisterAsync(client, eventId, jean), "register Jean");

        (await Refused(await RegisterAsync(client, eventId, jean), HttpStatusCode.Conflict, "Jean again"))
            .Should().Contain("already on this event's list");
        (await Json(await client.GetAsync("/api/charges"), "charges")).GetArrayLength().Should().Be(1, "no second ticket");
    }

    [Fact]
    public async Task AWalkInTakesAFreeSeat_AndIsRefusedWhenTheEventIsFull()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var eventId = await EventAsync(client, capacity: 1, guestPrice: 20, started: true);
        var jean = await MemberAsync(client, "Jean");

        var walkIn = await Json(await client.PostAsync($"/api/events/{eventId}/attendees/walk-in", Body(new { memberName = "Ana Silva", ticketTier = "Guest", hostMemberId = jean })), "a walk-in");
        walkIn.GetProperty("status").GetString().Should().Be("CheckedIn");
        walkIn.GetProperty("walkIn").GetBoolean().Should().BeTrue();
        walkIn.GetProperty("chargeState").GetString().Should().Be("charged");

        (await Refused(await client.PostAsync($"/api/events/{eventId}/attendees/walk-in", Body(new { memberName = "Late", ticketTier = "Guest" })),
            HttpStatusCode.Conflict, "a walk-in when the event is full")).Should().Contain("full");
        (await EventOf(client, eventId)).GetProperty("walkInCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task ANoShowKeepsTheTicketAndPostsTheFee_AndComingLateTakesTheFeeBack()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var eventId = await EventAsync(client, capacity: 10, memberPrice: 35, noShowFee: 20, started: true);
        var jean = await MemberAsync(client, "Jean");
        var attendee = await Json(await RegisterAsync(client, eventId, jean), "register Jean");

        var noShow = await Json(await SetStatusAsync(client, eventId, attendee, "NoShow"), "mark Jean a no-show");

        noShow.GetProperty("noShowFee").GetDecimal().Should().Be(20);
        noShow.GetProperty("noShowFeeState").GetString().Should().Be("charged");
        noShow.GetProperty("chargeState").GetString().Should().Be("charged", "the ticket stands");
        (await BalanceAsync(client, jean)).Should().Be(-55);

        var late = await Json(await SetStatusAsync(client, eventId, attendee, "CheckedIn"), "Jean arrives after all");
        late.GetProperty("noShowFeeState").GetString().Should().Be("reversed");
        (await ChargeAsync(client, noShow.GetProperty("noShowChargeId").GetString())).GetProperty("status").GetString().Should().Be("Voided");
        (await BalanceAsync(client, jean)).Should().Be(-35);
    }

    [Fact]
    public async Task NobodyIsMarkedANoShowBeforeTheEventStarts()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var eventId = await EventAsync(client, capacity: 10, noShowFee: 20);
        var attendee = await Json(await AddGuestAsync(client, eventId, "Claire"), "add Claire");

        (await Refused(await SetStatusAsync(client, eventId, attendee, "NoShow"), HttpStatusCode.Conflict, "a no-show ahead of time"))
            .Should().Contain("hasn't started");
    }

    [Fact]
    public async Task CancellingTheEventCancelsEveryone_TakesTheirTicketsBack_AndTellsThem()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, org) = await ClubAsync(factory);
        var eventId = await EventAsync(client, capacity: 2, memberPrice: 35);
        var jean = await MemberAsync(client, "Jean");
        var marie = await MemberAsync(client, "Marie");
        var paul = await MemberAsync(client, "Paul");
        foreach (var member in new[] { jean, marie, paul })
            await Json(await RegisterAsync(client, eventId, member), "register");

        var ev = await Json(await client.PutAsync($"/api/events/{eventId}", Body(new { status = "Cancelled" })), "cancel the event");

        ev.GetProperty("status").GetString().Should().Be("Cancelled");
        ev.GetProperty("registeredCount").GetInt32().Should().Be(0);
        ev.GetProperty("ticketRevenue").GetDecimal().Should().Be(0);
        var list = await Json(await client.GetAsync($"/api/events/{eventId}/attendees"), "list");
        list.EnumerateArray().Select(a => a.GetProperty("status").GetString()).Should().AllBe("Cancelled");
        (await Json(await client.GetAsync("/api/charges"), "charges")).EnumerateArray()
            .Select(c => c.GetProperty("status").GetString()).Should().BeEquivalentTo(new[] { "Voided", "Voided" });
        foreach (var member in new[] { jean, marie, paul })
            (await BalanceAsync(client, member)).Should().Be(0);
        var mail = await factory.Services.Read(db => db.OutboundMessages.Where(m => m.OrganizationId == org).Select(m => m.To).ToList());
        mail.Should().BeEquivalentTo(new[] { "jean@example.ch", "marie@example.ch", "paul@example.ch" }, "the waitlisted are told too");
        (await Refused(await client.PutAsync($"/api/events/{eventId}", Body(new { status = "Published" })), HttpStatusCode.Conflict, "un-cancelling"))
            .Should().Contain("stays cancelled");
    }

    [Fact]
    public async Task AnEventWithTicketsOnAccountsIsCancelledNotDeleted_AndAFreeOneTakesItsListWithIt()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, org) = await ClubAsync(factory);
        var paid = await EventAsync(client, capacity: 10, memberPrice: 35);
        var free = await EventAsync(client, capacity: 10);
        var jean = await MemberAsync(client, "Jean");
        await Json(await RegisterAsync(client, paid, jean), "register for the paid one");
        await Json(await AddGuestAsync(client, free, "Claire Martin"), "a guest at the free one");

        (await Refused(await client.DeleteAsync($"/api/events/{paid}"), HttpStatusCode.Conflict, "deleting the paid one")).Should().Contain("Cancel it instead");
        (await client.DeleteAsync($"/api/events/{free}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var left = await factory.Services.Read(db => db.ClubEventRegistrations.Where(r => r.OrganizationId == org).Select(r => r.EventId).ToList());
        left.Should().Equal(paid);
    }

    [Fact]
    public async Task MoreSeatsAndReleasedUnnamedOnesGoToTheWaitlist_AndTheCapacityCannotDropBelowTheSeatsTaken()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, org) = await ClubAsync(factory);
        var eventId = await EventAsync(client, capacity: 2);
        // One seat the old events list counted without a name.
        await factory.Services.Read(db =>
        {
            db.ClubEvents.Single(e => e.Id == eventId).UnnamedSeats = 1;
            return db.SaveChanges();
        });
        await Json(await AddGuestAsync(client, eventId, "Anne"), "Anne takes the last seat");
        var bruno = await Json(await AddGuestAsync(client, eventId, "Bruno"), "Bruno");
        var chloe = await Json(await AddGuestAsync(client, eventId, "Chloé"), "Chloé");
        bruno.GetProperty("status").GetString().Should().Be("Waitlisted");
        chloe.GetProperty("waitlistPosition").GetInt32().Should().Be(2);

        (await Refused(await client.PutAsync($"/api/events/{eventId}", Body(new { capacity = 1 })), HttpStatusCode.Conflict, "fewer seats than are taken"))
            .Should().Contain("2 seats are taken");
        (await Refused(await client.PutAsync($"/api/events/{eventId}", Body(new { unnamedSeats = 5 })), HttpStatusCode.BadRequest, "more unnamed seats"))
            .Should().Contain("released");

        await Json(await client.PutAsync($"/api/events/{eventId}", Body(new { unnamedSeats = 0 })), "release the unnamed seat");
        var list = await Json(await client.GetAsync($"/api/events/{eventId}/attendees"), "list");
        list.EnumerateArray().Single(a => a.GetProperty("memberName").GetString() == "Bruno").GetProperty("status").GetString().Should().Be("Reserved");

        await Json(await client.PutAsync($"/api/events/{eventId}", Body(new { capacity = 3 })), "one more seat");
        list = await Json(await client.GetAsync($"/api/events/{eventId}/attendees"), "list");
        list.EnumerateArray().Single(a => a.GetProperty("memberName").GetString() == "Chloé").GetProperty("status").GetString().Should().Be("Reserved");
        (await EventOf(client, eventId)).GetProperty("seatsLeft").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task AnEditFromAnOlderCopyIsRefused_ButARegistrationDoesNotMakeTheCopyOld()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var eventId = await EventAsync(client, capacity: 10);
        var read = await EventOf(client, eventId);
        var version = read.GetProperty("version").GetString();

        // Someone registers meanwhile: the events manager's copy of the details is still current.
        await Json(await AddGuestAsync(client, eventId, "Claire"), "a registration");
        var edited = await Json(await client.PutAsync($"/api/events/{eventId}", Body(new { description = "Loire wines", version })), "edit the description");
        edited.GetProperty("description").GetString().Should().Be("Loire wines");

        (await Refused(await client.PutAsync($"/api/events/{eventId}", Body(new { name = "Old copy", version })), HttpStatusCode.Conflict, "an edit from before that edit"))
            .Should().Contain("at the same moment");
        var cleared = await Json(await client.PutAsync($"/api/events/{eventId}", Body(new { description = "" })), "clear the description");
        cleared.TryGetProperty("description", out _).Should().BeFalse("empty text clears it");
    }

    [Fact]
    public async Task AQuickRsvpWithoutANameIsRefusedWithAReason()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var eventId = await EventAsync(client, capacity: 10);

        (await Refused(await client.PostAsync($"/api/events/{eventId}/rsvp", null), HttpStatusCode.BadRequest, "an RSVP with no name"))
            .Should().Contain("needs a name");
        (await EventOf(client, eventId)).GetProperty("registeredCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task AnEventNeedsANameAndTimesThatMakeSense()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var start = DateTime.UtcNow.AddDays(3);

        await Refused(await client.PostAsync("/api/events", Body(new { name = " ", startAtUtc = start, endAtUtc = start.AddHours(1) })), HttpStatusCode.BadRequest, "no name");
        await Refused(await client.PostAsync("/api/events", Body(new { name = "Gala", startAtUtc = start, endAtUtc = start.AddHours(-1) })), HttpStatusCode.BadRequest, "ends before it starts");
        await Refused(await client.PostAsync("/api/events", Body(new { name = "Gala", startAtUtc = start, endAtUtc = start.AddHours(1), memberPrice = -5 })), HttpStatusCode.BadRequest, "a negative price");
        var ev = await Json(await client.PostAsync("/api/events", Body(new { name = "Gala", startAtUtc = start, endAtUtc = start.AddHours(1), capacity = 0 })), "an event with no limit");
        ev.GetProperty("currency").GetString().Should().Be("CHF");
        ev.GetProperty("status").GetString().Should().Be("Draft");
        ev.TryGetProperty("seatsLeft", out _).Should().BeFalse("no limit");
    }

    [Fact]
    public async Task AMemberIsLookedUpInThisWorkspace()
    {
        using var factory = new CustomWebApplicationFactory();
        var (mine, _, _) = await ClubAsync(factory);
        var (theirs, _, _) = await ClubAsync(factory, "other@example.ch");
        var theirMember = await MemberAsync(theirs, "Zoé");
        var myMember = await MemberAsync(mine, "Jean");
        var eventId = await EventAsync(mine, capacity: 10);

        await Refused(await RegisterAsync(mine, eventId, theirMember), HttpStatusCode.NotFound, "another club's member");
        await Refused(await AddGuestAsync(mine, eventId, "Claire", hostMemberId: theirMember), HttpStatusCode.NotFound, "another club's member as host");

        var added = await Json(await mine.PostAsync($"/api/events/{eventId}/attendees",
            Body(new { memberId = myMember, memberName = "Typed by hand", ticketTier = "Member" })), "add my member");
        added.GetProperty("memberName").GetString().Should().Be("Jean Dupont", "the name comes from the member record");
        added.GetProperty("email").GetString().Should().Be("jean@example.ch");
        await Refused(await AddGuestAsync(mine, eventId, " "), HttpStatusCode.BadRequest, "a guest with no name");
    }

    [Fact]
    public async Task AnotherClubCannotReadOrChangeTheEventOrItsList()
    {
        using var factory = new CustomWebApplicationFactory();
        var (mine, _, _) = await ClubAsync(factory);
        var eventId = await EventAsync(mine, capacity: 10);
        var attendee = await Json(await AddGuestAsync(mine, eventId, "Claire"), "add attendee");
        var url = $"/api/events/{eventId}/attendees/{attendee.GetProperty("id").GetString()}";
        var (theirs, _, _) = await ClubAsync(factory, "other@example.ch");

        (await Json(await theirs.GetAsync("/api/events"), "their events")).GetArrayLength().Should().Be(0);
        await Refused(await theirs.GetAsync($"/api/events/{eventId}"), HttpStatusCode.NotFound, "reading my event");
        var read = await theirs.GetAsync($"/api/events/{eventId}/attendees");
        read.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await read.Content.ReadAsStringAsync()).Should().NotBeEmpty("a 404 with no body reads as 'no such feature' to the app");
        await Refused(await theirs.PutAsync($"/api/events/{eventId}", Body(new { name = "Hijacked" })), HttpStatusCode.NotFound, "editing my event");
        await Refused(await theirs.DeleteAsync($"/api/events/{eventId}"), HttpStatusCode.NotFound, "deleting my event");
        await Refused(await theirs.PutAsync(url, Body(new { status = "Cancelled" })), HttpStatusCode.NotFound, "cancelling my attendee");
        await Refused(await theirs.DeleteAsync(url), HttpStatusCode.NotFound, "removing my attendee");
        await Refused(await AddGuestAsync(theirs, eventId, "Intruder"), HttpStatusCode.NotFound, "adding to my list");

        var otherEvent = await EventAsync(mine, capacity: 10);
        await Refused(await mine.PutAsync($"/api/events/{otherEvent}/attendees/{attendee.GetProperty("id").GetString()}", Body(new { status = "Cancelled" })),
            HttpStatusCode.NotFound, "an attendee through another event");
        (await Json(await mine.GetAsync($"/api/events/{eventId}/attendees"), "my list"))[0].GetProperty("status").GetString().Should().Be("Reserved");
    }

    [Fact]
    public async Task ABrowsersOldListComesOverOnce_AndTakesTheUnnamedSeatItWasCountedIn()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var eventId = await EventAsync(client, capacity: 40, guestPrice: 55);
        // What the browser did when it kept the list: it raised the server's count, which the
        // move kept as an unnamed seat.
        await factory.Services.Read(db =>
        {
            db.ClubEvents.Single(e => e.Id == eventId).UnnamedSeats = 1;
            return db.SaveChanges();
        });
        var row = new
        {
            localId = "att-1730000000000-ab12cd", memberName = "Claire Martin", email = "claire@example.ch",
            ticketTier = "Guest", status = "CheckedIn", rsvpAtUtc = "2026-10-01T18:00:00Z", checkedInAtUtc = "2026-10-03T19:05:00Z",
        };

        var imported = await Json(await client.PostAsync($"/api/events/{eventId}/attendees/import", Body(row)), "import");
        var again = await Json(await client.PostAsync($"/api/events/{eventId}/attendees/import", Body(row)), "import retried");

        imported.GetProperty("status").GetString().Should().Be("CheckedIn");
        imported.GetProperty("rsvpAtUtc").GetString().Should().StartWith("2026-10-01T18:00:00");
        imported.GetProperty("chargeState").GetString().Should().Be("not-charged");
        again.GetProperty("id").GetString().Should().Be(imported.GetProperty("id").GetString());
        (await Json(await client.GetAsync($"/api/events/{eventId}/attendees"), "list")).GetArrayLength().Should().Be(1);
        var ev = await EventOf(client, eventId);
        ev.GetProperty("registeredCount").GetInt32().Should().Be(1, "it was counted when the browser added it");
        ev.GetProperty("unnamedSeats").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task SomeoneWhoseTicketIsOnAnAccountIsCancelledNotRemoved_AndARemovalGivesTheSeatToTheNext()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var eventId = await EventAsync(client, capacity: 1, memberPrice: 35);
        var jean = await MemberAsync(client, "Jean");
        var paid = await Json(await RegisterAsync(client, eventId, jean), "Jean's paid seat");
        var free = await EventAsync(client, capacity: 1);
        var anne = await Json(await AddGuestAsync(client, free, "Anne"), "Anne");
        await Json(await AddGuestAsync(client, free, "Bruno"), "Bruno waits");

        (await Refused(await client.DeleteAsync($"/api/events/{eventId}/attendees/{paid.GetProperty("id").GetString()}"), HttpStatusCode.Conflict, "removing Jean"))
            .Should().Contain("Cancel the registration instead");
        (await client.DeleteAsync($"/api/events/{free}/attendees/{anne.GetProperty("id").GetString()}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await Json(await client.GetAsync($"/api/events/{free}/attendees"), "list");
        list.EnumerateArray().Single().GetProperty("status").GetString().Should().Be("Reserved", "Bruno gets Anne's seat");
    }

    [Fact]
    public async Task MergingTwoMembersMovesTheirPlacesOnListsAndTheirBookings()
    {
        // A booking found its member by email text, so merging two records left the
        // duplicate's bookings with nobody; registrations name the member by id.
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var eventId = await EventAsync(client, capacity: 10);
        var keep = await MemberAsync(client, "Jean");
        var drop = await MemberAsync(client, "Johnny");
        await Json(await RegisterAsync(client, eventId, drop), "the duplicate registers");
        await Json(await AddGuestAsync(client, eventId, "Claire", hostMemberId: drop), "the duplicate brings a guest");
        var court = (await Json(await client.PostAsync("/api/resources", Body(new { name = "Court", kind = "Court", capacity = 4 })), "add a court")).GetProperty("id").GetString();
        await Json(await client.PostAsync("/api/reservations", Body(new
        {
            resourceId = court, memberId = drop, startAtUtc = DateTime.UtcNow.AddDays(2), endAtUtc = DateTime.UtcNow.AddDays(2).AddHours(1),
        })), "the duplicate books the court");

        (await client.PostAsync($"/api/members/{keep}/merge", Body(new { otherMemberId = drop }))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var registrations = await Json(await client.GetAsync($"/api/events/registrations?memberId={keep}"), "the kept member's registrations");
        registrations.EnumerateArray().Select(r => r.GetProperty("memberName").GetString()).Should().BeEquivalentTo(new[] { "Jean Dupont", "Claire" });
        registrations.EnumerateArray().Single(r => r.GetProperty("memberName").GetString() == "Claire").GetProperty("hostName").GetString().Should().Be("Jean Dupont");
        var bookings = await Json(await client.GetAsync($"/api/reservations?memberId={keep}"), "the kept member's bookings");
        bookings.GetArrayLength().Should().Be(1);
        bookings[0].GetProperty("memberEmail").GetString().Should().Be("jean@example.ch");
    }

    [Fact]
    public async Task AMembersRegistrationsAreListedForTheirPage()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var eventId = await EventAsync(client, capacity: 10);
        var jean = await MemberAsync(client, "Jean");
        var marie = await MemberAsync(client, "Marie");
        await Json(await RegisterAsync(client, eventId, jean), "Jean");
        await Json(await AddGuestAsync(client, eventId, "Claire", hostMemberId: jean), "Jean's guest");
        await Json(await RegisterAsync(client, eventId, marie), "Marie");

        var mine = await Json(await client.GetAsync($"/api/events/registrations?memberId={jean}"), "Jean's registrations");

        mine.EnumerateArray().Select(r => r.GetProperty("memberName").GetString()).Should().BeEquivalentTo(new[] { "Jean Dupont", "Claire" });
        mine[0].GetProperty("eventName").GetString().Should().Be("Wine night");
    }
}
