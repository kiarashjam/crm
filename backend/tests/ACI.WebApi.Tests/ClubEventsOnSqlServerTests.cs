using System.Net;
using System.Text.Json;
using ACI.Application.Services;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Events and bookings on a real SQL Server: two changes that read the same list or the same
/// slot and then both try to take it, and the whole of a change (a seat, its ticket on the
/// ledger, the email to the next in line) as one transaction.
/// </summary>
/// <remarks>
/// The in-memory provider the HTTP tests run on has no transactions or locks. Here a change to
/// an event's list takes the event's row first, and a booking its resource's, so the second of
/// two changes at once waits for the first, is refused, and is decided again on what the first
/// saved; the gate makes both read before either writes, every time.
/// </remarks>
public class ClubEventsOnSqlServerTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>A club on its own database, with one member, and the gate the services report their reads to.</summary>
    private static async Task RunAsync(Func<HttpClient, RaceFactory.ReadGate, Guid, string, Task> test)
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            var gate = new RaceFactory.ReadGate();
            using var app = SqlServerApp.Start(database, services: services => services.AddSingleton<ILedgerReadObserver>(gate));
            var (client, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var org = (await Json(await client.PostAsync("/api/organizations", Body(new { name = "Pavillon46" })), "create the club")).GetProperty("id").GetGuid();
            client.In(org);
            await test(client, gate, org, database);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    private static async Task<Guid> MemberAsync(HttpClient client, string first) =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Dupont", email = $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
        })), $"add {first}")).GetProperty("id").GetGuid();

    private static async Task<Guid> EventAsync(HttpClient client, int capacity, decimal memberPrice)
    {
        var start = DateTime.UtcNow.AddDays(7);
        return (await Json(await client.PostAsync("/api/events", Body(new
        {
            name = "Wine night", startAtUtc = start, endAtUtc = start.AddHours(3), capacity, memberPrice, status = "Published",
        })), "add event")).GetProperty("id").GetGuid();
    }

    [SqlServerFact]
    public Task TwoPeopleTakingTheLastSeatAtOnce_OneGetsItAndTheOtherTheWaitlist() => RunAsync(async (client, gate, _, _) =>
    {
        var eventId = await EventAsync(client, capacity: 1, memberPrice: 35);
        var jean = await MemberAsync(client, "Jean");
        var marie = await MemberAsync(client, "Marie");
        gate.Arm("events");

        var answers = await Task.WhenAll(
            client.PostAsync($"/api/events/{eventId}/attendees", Body(new { memberId = jean })),
            client.PostAsync($"/api/events/{eventId}/attendees", Body(new { memberId = marie })));

        answers.Select(a => a.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.OK, "the second is decided again on what the first saved, not refused");
        var statuses = new List<string?>();
        foreach (var answer in answers)
            statuses.Add((await Json(answer, "register")).GetProperty("status").GetString());
        statuses.Should().BeEquivalentTo(new[] { "Reserved", "Waitlisted" }, "both read one free seat; only one of them gets it");
        (await Json(await client.GetAsync("/api/charges"), "charges")).GetArrayLength().Should().Be(1, "one ticket, for the one seat");
        var ev = await Json(await client.GetAsync($"/api/events/{eventId}"), "event");
        ev.GetProperty("registeredCount").GetInt32().Should().Be(1);
        ev.GetProperty("waitlistCount").GetInt32().Should().Be(1);
    });

    [SqlServerFact]
    public Task TwoBookingsOfOneSlotAtOnce_OneIsMadeAndTheOtherToldWhatIsInTheWay() => RunAsync(async (client, gate, _, _) =>
    {
        var spa = (await Json(await client.PostAsync("/api/resources", Body(new { name = "Spa Suite", kind = "Spa", capacity = 1 })), "add the spa"))
            .GetProperty("id").GetString();
        var start = DateTime.UtcNow.Date.AddDays(2).AddHours(13);
        gate.Arm("reservations");

        var answers = await Task.WhenAll(
            client.PostAsync("/api/reservations", Body(new { resourceId = spa, memberName = "Claire Martin", startAtUtc = start, endAtUtc = start.AddHours(1) })),
            client.PostAsync("/api/reservations", Body(new { resourceId = spa, memberName = "Paul Weber", startAtUtc = start.AddMinutes(30), endAtUtc = start.AddMinutes(90) })));

        answers.Select(a => a.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict },
            "both read a free slot; the second is checked again on what the first saved");
        (await answers.Single(a => a.StatusCode == HttpStatusCode.Conflict).Content.ReadAsStringAsync()).Should().Contain("Spa Suite is already booked");
        (await Json(await client.GetAsync("/api/reservations"), "bookings")).GetArrayLength().Should().Be(1);
    });

    [SqlServerFact]
    public Task ACancellationTakesTheTicketBack_AndTheNextInLineGetsTheSeatTheirTicketAndAnEmail_InOneGo() => RunAsync(async (client, _, org, database) =>
    {
        var eventId = await EventAsync(client, capacity: 1, memberPrice: 35);
        var jean = await MemberAsync(client, "Jean");
        var marie = await MemberAsync(client, "Marie");
        var first = await Json(await client.PostAsync($"/api/events/{eventId}/attendees", Body(new { memberId = jean })), "Jean");
        await Json(await client.PostAsync("/api/payments", Body(new { amount = 35, method = "Cash", chargeId = first.GetProperty("chargeId").GetString() })), "Jean pays");
        await Json(await client.PostAsync($"/api/events/{eventId}/attendees", Body(new { memberId = marie })), "Marie waits");

        await Json(await client.PutAsync($"/api/events/{eventId}/attendees/{first.GetProperty("id").GetString()}", Body(new { status = "Cancelled" })), "Jean cancels");

        var list = await Json(await client.GetAsync($"/api/events/{eventId}/attendees"), "list");
        list.EnumerateArray().Single(a => a.GetProperty("memberId").GetGuid() == marie).GetProperty("chargeState").GetString().Should().Be("charged");
        var members = await Json(await client.GetAsync("/api/members"), "members");
        members.EnumerateArray().Single(m => m.GetProperty("id").GetGuid() == jean).GetProperty("houseAccountBalance").GetDecimal().Should().Be(35, "his payment is credit again");
        members.EnumerateArray().Single(m => m.GetProperty("id").GetGuid() == marie).GetProperty("houseAccountBalance").GetDecimal().Should().Be(-35);
        (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [OutboundMessages] WHERE [OrganizationId] = '{org}' AND [To] = N'marie@example.ch'"))
            .Should().Be(1);
    });

    [SqlServerFact]
    public Task ADepositPaidAtTheDeskAndThenCancelledIsCreditAgain() => RunAsync(async (client, _, _, _) =>
    {
        var room = (await Json(await client.PostAsync("/api/resources", Body(new { name = "Private Dining Room", kind = "Dining", capacity = 12, depositAmount = 100 })), "add the room"))
            .GetProperty("id").GetString();
        var jean = await MemberAsync(client, "Jean");
        var start = DateTime.UtcNow.Date.AddDays(3).AddHours(18);
        var booking = await Json(await client.PostAsync("/api/reservations", Body(new
        {
            resourceId = room, memberId = jean, partySize = 8, startAtUtc = start, endAtUtc = start.AddHours(3), deposit = "paid", depositMethod = "CardOnFile",
        })), "book with the deposit paid");
        booking.GetProperty("depositState").GetString().Should().Be("paid");

        var cancelled = await Json(await client.PutAsync($"/api/reservations/{booking.GetProperty("id").GetString()}", Body(new { status = "Cancelled" })), "cancel");

        cancelled.GetProperty("depositState").GetString().Should().Be("released");
        var member = (await Json(await client.GetAsync("/api/members"), "members"))[0];
        member.GetProperty("houseAccountBalance").GetDecimal().Should().Be(100);
    });
}
