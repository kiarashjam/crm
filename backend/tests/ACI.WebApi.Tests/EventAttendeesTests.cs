using System.Net;
using System.Text.Json;
using ACI.Domain.Enums;
using FluentAssertions;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// An event's attendee list lives on the server, for the whole workspace.
/// </summary>
/// <remarks>
/// It used to be kept only in each browser. The events manager registered forty members
/// on a laptop, the door host opened the same event on an iPad and saw nobody to check
/// in, and clearing the browser lost the list. Only the counts reached the server, as
/// "the count I last read, plus one", so capacity was checked against whatever that
/// one browser happened to hold.
/// </remarks>
public class EventAttendeesTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(HttpClient Client, Guid UserId, Guid Org)> ClubAsync(CustomWebApplicationFactory factory, string email = "events@pavillon46.ch")
    {
        var (client, userId) = await SignInAsync(factory, email);
        var org = await CreateAsync(factory, userId, "Pavillon46");
        client.In(org);
        return (client, userId, org);
    }

    private static async Task<Guid> EventAsync(HttpClient client, int capacity) =>
        (await Json(await client.PostAsync("/api/events", Body(new { name = "Wine night", capacity })), "add event"))
            .GetProperty("id").GetGuid();

    private static async Task<JsonElement> EventOf(HttpClient client, Guid id) =>
        (await Json(await client.GetAsync("/api/events"), "list events")).EnumerateArray()
            .Single(e => e.GetProperty("id").GetGuid() == id);

    private static Task<HttpResponseMessage> AddGuestAsync(HttpClient client, Guid eventId, string name) =>
        client.PostAsync($"/api/events/{eventId}/attendees", Body(new { memberName = name, ticketTier = "Guest" }));

    [Fact]
    public async Task TheDoorHostSeesTheListTheEventsManagerEntered()
    {
        using var factory = new CustomWebApplicationFactory();
        var (manager, _, org) = await ClubAsync(factory);
        var (doorHost, doorHostId) = await SignInAsync(factory, "door@pavillon46.ch");
        await AddMemberAsync(factory, org, doorHostId, OrgMemberRole.Member);
        doorHost.In(org);
        var eventId = await EventAsync(manager, capacity: 40);

        await Json(await AddGuestAsync(manager, eventId, "Claire Martin"), "add attendee");

        var list = await Json(await doorHost.GetAsync($"/api/events/{eventId}/attendees"), "door host lists attendees");
        list.GetArrayLength().Should().Be(1);
        var attendee = list[0];
        attendee.GetProperty("memberName").GetString().Should().Be("Claire Martin");
        attendee.GetProperty("status").GetString().Should().Be("Reserved");

        var checkedIn = await Json(await doorHost.PutAsync(
            $"/api/events/{eventId}/attendees/{attendee.GetProperty("id").GetString()}",
            Body(new { status = "CheckedIn" })), "door host checks in");
        checkedIn.GetProperty("status").GetString().Should().Be("CheckedIn");
        checkedIn.TryGetProperty("checkedInAtUtc", out _).Should().BeTrue();
        (await EventOf(manager, eventId)).GetProperty("registeredCount").GetInt32().Should().Be(1, "a check-in is still a seat");
    }

    [Fact]
    public async Task SomeoneAlreadyCheckedInIsNotCheckedInAgain()
    {
        // Two door hosts share the list now. The second one's screen can still show the
        // member as reserved, and each check-in the page sees succeed gives the member
        // their event points, so a repeat has to be refused rather than answered as done.
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var eventId = await EventAsync(client, capacity: 40);
        var attendee = await Json(await AddGuestAsync(client, eventId, "Claire Martin"), "add attendee");
        var url = $"/api/events/{eventId}/attendees/{attendee.GetProperty("id").GetString()}";

        var first = await Json(await client.PutAsync(url, Body(new { status = "CheckedIn" })), "first check-in");
        var again = await client.PutAsync(url, Body(new { status = "CheckedIn" }));

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await again.Content.ReadAsStringAsync()).Should().Contain("already checked in");
        var list = await Json(await client.GetAsync($"/api/events/{eventId}/attendees"), "list");
        list[0].GetProperty("checkedInAtUtc").GetString().Should().Be(first.GetProperty("checkedInAtUtc").GetString());
        (await EventOf(client, eventId)).GetProperty("registeredCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task TheServerKeepsCapacityAndTheCounts()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var eventId = await EventAsync(client, capacity: 1);

        var first = await Json(await AddGuestAsync(client, eventId, "Anne"), "first attendee");
        var second = await Json(await AddGuestAsync(client, eventId, "Bruno"), "second attendee");
        first.GetProperty("status").GetString().Should().Be("Reserved");
        second.GetProperty("status").GetString().Should().Be("Waitlisted", "the one seat is taken");
        var ev = await EventOf(client, eventId);
        ev.GetProperty("registeredCount").GetInt32().Should().Be(1);
        ev.GetProperty("waitlistCount").GetInt32().Should().Be(1);

        var secondUrl = $"/api/events/{eventId}/attendees/{second.GetProperty("id").GetString()}";
        var promoteWhileFull = await client.PutAsync(secondUrl, Body(new { status = "Reserved" }));
        promoteWhileFull.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await promoteWhileFull.Content.ReadAsStringAsync()).Should().Contain("full");

        await Json(await client.PutAsync($"/api/events/{eventId}/attendees/{first.GetProperty("id").GetString()}",
            Body(new { status = "Cancelled" })), "cancel the first");
        await Json(await client.PutAsync(secondUrl, Body(new { status = "Reserved" })), "promote the second");
        ev = await EventOf(client, eventId);
        ev.GetProperty("registeredCount").GetInt32().Should().Be(1);
        ev.GetProperty("waitlistCount").GetInt32().Should().Be(0);

        (await client.DeleteAsync(secondUrl)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await EventOf(client, eventId)).GetProperty("registeredCount").GetInt32().Should().Be(0);
        (await Json(await client.GetAsync($"/api/events/{eventId}/attendees"), "list")).GetArrayLength().Should().Be(1, "the cancelled one stays on the list");
    }

    [Fact]
    public async Task AMemberIsLookedUpInThisWorkspace()
    {
        using var factory = new CustomWebApplicationFactory();
        var (mine, _, _) = await ClubAsync(factory);
        var (theirs, _, _) = await ClubAsync(factory, "other@example.ch");
        var theirMember = (await Json(await theirs.PostAsync("/api/members", Body(new
        {
            firstName = "Zoé", lastName = "Keller", email = "zoe@example.ch", tier = "Gold", status = "Active",
        })), "their member")).GetProperty("id").GetGuid();
        var myMember = (await Json(await mine.PostAsync("/api/members", Body(new
        {
            firstName = "Jean", lastName = "Dupont", email = "jean@example.ch", tier = "Gold", status = "Active",
        })), "my member")).GetProperty("id").GetGuid();
        var eventId = await EventAsync(mine, capacity: 10);

        var foreign = await mine.PostAsync($"/api/events/{eventId}/attendees", Body(new { memberId = theirMember, memberName = "Anyone", ticketTier = "Member" }));
        foreign.StatusCode.Should().Be(HttpStatusCode.NotFound, "another club's member cannot be put on this list");

        var added = await Json(await mine.PostAsync($"/api/events/{eventId}/attendees",
            Body(new { memberId = myMember, memberName = "Typed by hand", ticketTier = "Member" })), "add my member");
        added.GetProperty("memberName").GetString().Should().Be("Jean Dupont", "the name comes from the member record");
        added.GetProperty("email").GetString().Should().Be("jean@example.ch");

        (await mine.PostAsync($"/api/events/{eventId}/attendees", Body(new { memberName = " ", ticketTier = "Guest" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AnotherClubCannotReadOrChangeTheList()
    {
        using var factory = new CustomWebApplicationFactory();
        var (mine, _, _) = await ClubAsync(factory);
        var eventId = await EventAsync(mine, capacity: 10);
        var attendee = await Json(await AddGuestAsync(mine, eventId, "Claire"), "add attendee");
        var url = $"/api/events/{eventId}/attendees/{attendee.GetProperty("id").GetString()}";
        var (theirs, _, _) = await ClubAsync(factory, "other@example.ch");

        var read = await theirs.GetAsync($"/api/events/{eventId}/attendees");
        read.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await read.Content.ReadAsStringAsync()).Should().NotBeEmpty("a 404 with no body reads as 'no such feature' to the app, which then saves in the browser");
        (await theirs.PutAsync(url, Body(new { status = "Cancelled" }))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await theirs.DeleteAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AddGuestAsync(theirs, eventId, "Intruder")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var otherEvent = await EventAsync(mine, capacity: 10);
        (await mine.PutAsync($"/api/events/{otherEvent}/attendees/{attendee.GetProperty("id").GetString()}", Body(new { status = "Cancelled" })))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "an attendee belongs to one event");
        (await Json(await mine.GetAsync($"/api/events/{eventId}/attendees"), "my list"))[0]
            .GetProperty("status").GetString().Should().Be("Reserved");
    }

    [Fact]
    public async Task ABrowsersOldListComesOverOnceAndIsNotCountedAgain()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var eventId = await EventAsync(client, capacity: 40);
        // What the browser did when it kept the list: it bumped the server's count.
        await Json(await client.PutAsync($"/api/events/{eventId}", Body(new { registeredCount = 1 })), "the browser's count");
        var row = new
        {
            localId = "att-1730000000000-ab12cd", memberName = "Claire Martin", email = "claire@example.ch",
            ticketTier = "Guest", status = "CheckedIn", rsvpAtUtc = "2026-10-01T18:00:00Z", checkedInAtUtc = "2026-10-03T19:05:00Z",
        };

        var imported = await Json(await client.PostAsync($"/api/events/{eventId}/attendees/import", Body(row)), "import");
        var again = await Json(await client.PostAsync($"/api/events/{eventId}/attendees/import", Body(row)), "import retried");

        imported.GetProperty("status").GetString().Should().Be("CheckedIn");
        imported.GetProperty("rsvpAtUtc").GetString().Should().StartWith("2026-10-01T18:00:00");
        again.GetProperty("id").GetString().Should().Be(imported.GetProperty("id").GetString());
        (await Json(await client.GetAsync($"/api/events/{eventId}/attendees"), "list")).GetArrayLength().Should().Be(1);
        (await EventOf(client, eventId)).GetProperty("registeredCount").GetInt32().Should().Be(1, "it was counted when the browser added it");
    }

    [Fact]
    public async Task DeletingAnEventDeletesItsGuestList()
    {
        // Nothing shows a deleted event's attendees, so their names and emails would only
        // have stayed in the table.
        using var factory = new CustomWebApplicationFactory();
        var (client, _, org) = await ClubAsync(factory);
        var eventId = await EventAsync(client, capacity: 40);
        var keptId = await EventAsync(client, capacity: 40);
        await Json(await AddGuestAsync(client, eventId, "Claire Martin"), "add attendee");
        await Json(await AddGuestAsync(client, keptId, "Paul Weber"), "add attendee to the other event");

        (await client.DeleteAsync($"/api/events/{eventId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var left = await factory.Services.Read(db => db.ClubDocuments
            .Where(d => d.OrganizationId == org && d.Kind == "event-attendees").Select(d => d.Payload).ToList());
        left.Should().ContainSingle().Which.Should().Contain("Paul Weber");
    }
}

