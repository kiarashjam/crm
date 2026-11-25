using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using ACI.Domain.Entities;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Member audiences and event invitations on a real SQL Server: the audience's queries as SQL
/// Server runs them, two sends or two reminders at the same moment, and an answer on the reply
/// page as one transaction with the registration and its ticket.
/// </summary>
/// <remarks>
/// The in-memory provider the HTTP tests run on enforces no unique index and has no
/// transactions or conditional updates; here the database decides: one invitation per member
/// and event (a unique index), one reminder per invitation (a conditional update), one send per
/// send id (its key).
/// </remarks>
public class EventInvitationsOnSqlServerTests
{
    private static readonly Regex ReplyLink = new(@"/invitation/(?<token>[A-Za-z0-9_-]{43})");

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task RunAsync(Func<SqlServerApp, HttpClient, Guid, string, Task> test)
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (client, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var org = (await Json(await client.PostAsync("/api/organizations", Body(new { name = "Pavillon 46" })), "create the club")).GetProperty("id").GetGuid();
            client.In(org);
            await test(app, client, org, database);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    private static async Task<Guid> MemberAsync(HttpClient client, string first)
    {
        var id = (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Keller", email = $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
        })), $"add {first}")).GetProperty("id").GetGuid();
        await Json(await client.PostAsync($"/api/members/{id}/consent", Body(new { channel = "email", purpose = "marketing", granted = true, source = "form" })), $"{first} agrees");
        return id;
    }

    private static async Task<Guid> EventAsync(HttpClient client, string name, int capacity = 0, decimal memberPrice = 0, bool started = false)
    {
        var start = started ? DateTime.UtcNow.AddHours(-2) : DateTime.UtcNow.AddDays(7);
        return (await Json(await client.PostAsync("/api/events", Body(new
        {
            name, startAtUtc = start, endAtUtc = start.AddHours(3), capacity, memberPrice, status = "Published",
        })), "add the event")).GetProperty("id").GetGuid();
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, Guid eventId, Guid sendId) =>
        client.PostAsync($"/api/events/{eventId}/invitations/send", Body(new { sendId, audience = new { statuses = new[] { "Active" } } }));

    private static async Task<int> CountAsync(string database, string sql) =>
        Convert.ToInt32(await SqlServerApp.ScalarAsync(database, sql));

    [SqlServerFact]
    public Task TheAudiencesFiltersAndHabitsRunOnSqlServer() => RunAsync(async (_, client, _, _) =>
    {
        await Json(await client.PostAsync("/api/custom-fields", Body(new { entityType = "member", label = "Interests", type = "multiselect", options = new[] { "Wine", "Art" } })), "the field");
        var vip = (await Json(await client.PostAsync("/api/tags", Body(new { name = "VIP" })), "the tag")).GetProperty("id").GetGuid();
        var anna = await MemberAsync(client, "Anna");
        var bruno = await MemberAsync(client, "Bruno");
        await MemberAsync(client, "Clara");
        (await client.PutAsync("/api/custom-fields/values", Body(new { entityType = "member", recordId = anna, values = new { interests = new[] { "Wine" } } }))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PutAsync("/api/custom-fields/values", Body(new { entityType = "member", recordId = bruno, values = new { interests = new[] { "Art", "Wine" } } }))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PutAsync("/api/tags/records", Body(new { entityType = "member", recordId = anna, tagIds = new[] { vip } }))).StatusCode.Should().Be(HttpStatusCode.OK);
        var tasting = await EventAsync(client, "Tasting", started: true);
        foreach (var member in new[] { anna, bruno })
        {
            var place = await Json(await client.PostAsync($"/api/events/{tasting}/attendees", Body(new { memberId = member, ticketTier = "Member" })), "register");
            await Json(await client.PutAsync($"/api/events/{tasting}/attendees/{place.GetProperty("id").GetString()}", Body(new { status = "CheckedIn" })), "check in");
        }
        await Json(await client.PostAsync("/api/visits", Body(new { memberId = anna, venue = "Bar" })), "Anna visits");
        await Json(await client.PostAsync("/api/visits", Body(new { memberId = anna, venue = "Bar" })), "and again");

        async Task<List<string>> Reached(object segment) =>
            (await Json(await client.PostAsync("/api/campaigns/audience", Body(new { segment, purpose = "marketing" })), "count")).GetProperty("recipients")
                .EnumerateArray().Select(r => r.GetProperty("name").GetString()!.Split(' ')[0]).OrderBy(n => n).ToList();
        (await Reached(new { fields = new[] { "interests:any:Wine" }, attendedEventIds = new[] { tasting }, minEvents = 1, eventMonths = 3 })).Should().Equal("Anna", "Bruno");
        (await Reached(new { tags = new[] { vip }, minVisits = 2, visitDays = 7 })).Should().Equal("Anna");
        (await Reached(new { notVisitedDays = 7, statuses = new[] { "Active" } })).Should().Equal("Bruno", "Clara");
    });

    [SqlServerFact]
    public Task TwoSendsAtTheSameMomentInviteEachMemberOnce_AndTheSameSendIdSendsOnce() => RunAsync(async (_, client, _, database) =>
    {
        foreach (var name in new[] { "Anna", "Bruno", "Clara", "Dario" }) await MemberAsync(client, name);
        var eventId = await EventAsync(client, "Gala");

        var sendId = Guid.NewGuid();
        var answers = await Task.WhenAll(SendAsync(client, eventId, sendId), SendAsync(client, eventId, sendId), SendAsync(client, eventId, Guid.NewGuid()));
        answers.Take(2).Select(a => a.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.OK, "the same press twice answers with the one send");
        // The other send either invited nobody new (the first got there before it counted) or
        // lost each member it tried to the first: never a second invitation.
        if (answers[2].StatusCode != HttpStatusCode.OK)
            (await answers[2].Content.ReadAsStringAsync()).Should().Contain("Invitation.Nobody");

        (await CountAsync(database, "SELECT COUNT(*) FROM [ClubEventInvitations]")).Should().Be(4, "one invitation per member and event, whichever send got there first");
        (await CountAsync(database, $"SELECT COUNT(*) FROM [OutboundMessages] WHERE [Source] = N'{OutboundSources.Invitation}'")).Should().Be(4, "an email only with an invitation behind it");
        (await CountAsync(database, "SELECT COUNT(*) FROM [ClubEventInvitationSends]")).Should().BeLessThanOrEqualTo(2, "the same send id is one send");
        (await CountAsync(database, "SELECT COUNT(*) FROM [MemberMessages] WHERE [Category] = N'Event'")).Should().Be(4, "a line in each member's communications, and no more");
    });

    [SqlServerFact]
    public Task TwoRemindersAtTheSameMomentRemindEachMemberOnce() => RunAsync(async (_, client, _, database) =>
    {
        foreach (var name in new[] { "Anna", "Bruno", "Clara" }) await MemberAsync(client, name);
        var eventId = await EventAsync(client, "Gala");
        await Json(await SendAsync(client, eventId, Guid.NewGuid()), "send");

        var answers = await Task.WhenAll(
            client.PostAsync($"/api/events/{eventId}/invitations/remind", null),
            client.PostAsync($"/api/events/{eventId}/invitations/remind", null));
        var reminded = 0;
        foreach (var answer in answers) reminded += (await Json(answer, "remind")).GetProperty("reminded").GetInt32();
        reminded.Should().Be(3, "each member is reminded once, by whichever press claimed them");
        (await CountAsync(database, $"SELECT COUNT(*) FROM [OutboundMessages] WHERE [Source] = N'{OutboundSources.Invitation}'")).Should().Be(6, "three invitations and three reminders");
        (await CountAsync(database, "SELECT COUNT(*) FROM [ClubEventInvitations] WHERE [RemindedAtUtc] IS NOT NULL AND [ReminderTokenHash] IS NOT NULL")).Should().Be(3);
        (await Json(await client.PostAsync($"/api/events/{eventId}/invitations/remind", null), "again")).GetProperty("reminded").GetInt32().Should().Be(0);
    });

    [SqlServerFact]
    public Task AnAnswerOnTheReplyPageRegistersWithItsTicket_AndAChangeOfMindTakesItBack() => RunAsync(async (app, client, _, database) =>
    {
        var anna = await MemberAsync(client, "Anna");
        var eventId = await EventAsync(client, "Gala", capacity: 10, memberPrice: 45);
        await Json(await SendAsync(client, eventId, Guid.NewGuid()), "send");
        var body = (string)(await SqlServerApp.ScalarAsync(database, $"SELECT TOP 1 [Body] FROM [OutboundMessages] WHERE [MemberId] = '{anna}' AND [Source] = N'{OutboundSources.Invitation}'"))!;
        var token = ReplyLink.Match(body).Groups["token"].Value;
        var visitor = app.CreateClient();

        (await Json(await visitor.PostAsync($"/api/public/invitations/{token}", Body(new { answer = "coming" })), "Anna comes")).GetProperty("place").GetString().Should().Be("seat");
        (await CountAsync(database, $"SELECT COUNT(*) FROM [ClubCharges] WHERE [MemberId] = '{anna}' AND [Amount] = 45")).Should().Be(1, "her ticket, on her account");
        (await Json(await visitor.PostAsync($"/api/public/invitations/{token}", Body(new { answer = "declined" })), "Anna can't")).GetProperty("answer").GetString().Should().Be("declined");
        (await CountAsync(database, $"SELECT COUNT(*) FROM [ClubEventRegistrations] WHERE [MemberId] = '{anna}' AND [Status] = N'Cancelled' AND [ChargeReversedAtUtc] IS NOT NULL")).Should().Be(1);
        (await CountAsync(database, $"SELECT COUNT(*) FROM [ClubEventInvitations] WHERE [MemberId] = '{anna}' AND [Answer] = N'declined'")).Should().Be(1);
    });
}
