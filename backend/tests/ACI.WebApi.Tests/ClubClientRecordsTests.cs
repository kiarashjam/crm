using System.Net;
using System.Text.Json;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The club's records of a company, a private client or a member who is deleted or merged into
/// another: a corporate membership and its seats, private events and their guest lists, and a
/// house account with every entry on it.
/// </summary>
public class ClubClientRecordsTests
{
    private static readonly DateTime Evening = DateTime.UtcNow.Date.AddDays(30).AddHours(17);

    private static string At(DateTime time) => time.ToString("O");

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task Done(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.NoContent, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
    }

    private static async Task<string> Refused(HttpResponseMessage res, HttpStatusCode status, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(status, $"{step} should be refused — got {(int)res.StatusCode}: {body}");
        return body;
    }

    private static async Task<HttpClient> ClubAsync(CustomWebApplicationFactory factory)
    {
        var (client, userId) = await SignInAsync(factory, "owner@pavillon46.ch");
        client.In(await CreateAsync(factory, userId, "Pavillon46"));
        return client;
    }

    private static Guid Id(JsonElement e) => e.GetProperty("id").GetGuid();

    private static async Task<Guid> CompanyAsync(HttpClient client, string name) =>
        Id(await Json(await client.PostAsync("/api/companies", Body(new { name })), $"add {name}"));

    private static async Task<Guid> ContactAsync(HttpClient client, string name, string email) =>
        Id(await Json(await client.PostAsync("/api/contacts", Body(new { name, email })), $"add {name}"));

    private static async Task<Guid> MemberAsync(HttpClient client, string first, string last = "Keller") =>
        Id(await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = last, email = $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
        })), $"add {first} {last}"));

    private static async Task<JsonElement> CorporateAsync(HttpClient client, Guid company, int seats = 3) =>
        await Json(await client.PostAsync("/api/corporate-memberships", Body(new
        {
            companyId = company, name = "Corporate", seats, nomineeCharges = "company", startsAtUtc = DateTime.UtcNow.Date.ToString("O"),
        })), "make the corporate membership");

    private static async Task<JsonElement> SeatAsync(HttpClient client, Guid membership, Guid member) =>
        await Json(await client.PostAsync($"/api/corporate-memberships/{membership}/nominees", Body(new { memberId = member })), "seat a nominee");

    private static async Task<JsonElement> EventAsync(HttpClient client, object request) =>
        await Json(await client.PostAsync("/api/private-events", Body(request)), "make the private event");

    [Fact]
    public async Task ACompanyTheClubKeepsIsNotDeleted_AndMergingItMovesItsMembershipEventsAndAccountToTheCompanyKept()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = await ClubAsync(factory);
        var acme = await CompanyAsync(client, "Acme AG");
        var duplicate = await CompanyAsync(client, "ACME (old record)");
        var membership = await CorporateAsync(client, duplicate);
        var anna = await MemberAsync(client, "Anna");
        await SeatAsync(client, Id(membership), anna);
        await Json(await client.PostAsync("/api/charges", Body(new { memberId = duplicate, kind = "Event", description = "Room hire", amount = 200m })), "charge the duplicate");
        var party = await EventAsync(client, new { title = "Acme party", companyId = duplicate, startAtUtc = At(Evening), endAtUtc = At(Evening.AddHours(4)) });

        (await Refused(await client.DeleteAsync($"/api/companies/{duplicate}"), HttpStatusCode.Conflict, "deleting a company the club keeps"))
            .Should().Contain("corporate membership");

        await Done(await client.PostAsync($"/api/companies/{acme}/merge", Body(new { otherCompanyId = duplicate })), "merge the duplicate into Acme");

        var moved = await Json(await client.GetAsync($"/api/corporate-memberships/{Id(membership)}"), "the membership, after");
        (moved.GetProperty("companyId").GetGuid(), moved.GetProperty("companyName").GetString()).Should().Be((acme, "Acme AG"));
        (await Json(await client.GetAsync($"/api/private-events/{Id(party)}"), "the event, after")).GetProperty("companyId").GetGuid().Should().Be(acme);
        var charge = (await Json(await client.GetAsync($"/api/charges?memberId={acme}"), "Acme's charges")).EnumerateArray()
            .Single(c => c.GetProperty("description").GetString() == "Room hire");
        charge.GetProperty("memberName").GetString().Should().Be("Acme AG");
        (await Json(await client.GetAsync($"/api/club-accounts/companies/{acme}"), "Acme's account")).GetProperty("owed").GetDecimal().Should().Be(200m);

        // Anna's charges go on the company that now holds her seat.
        var bar = await Json(await client.PostAsync("/api/charges", Body(new { memberId = anna, kind = "Bar", description = "Negroni", amount = 18m })), "Anna at the bar");
        (bar.GetProperty("memberId").GetGuid(), bar.GetProperty("accountType").GetString()).Should().Be((acme, "company"));
    }

    /// <remarks>
    /// A company the club keeps nothing for is deleted, with an empty account and its past events
    /// let go: ClubCorporateOnSqlServerTests, since deleting a company makes set-based updates the
    /// in-memory provider cannot run.
    /// </remarks>
    [Fact]
    public async Task ACompanyWithAPrivateEventStillToComeIsNotDeleted()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = await ClubAsync(factory);
        var cara = await CompanyAsync(client, "Cara AG");
        await EventAsync(client, new { title = "Cara launch", companyId = cara, startAtUtc = At(Evening.AddDays(1)), endAtUtc = At(Evening.AddDays(1).AddHours(3)) });
        (await Refused(await client.DeleteAsync($"/api/companies/{cara}"), HttpStatusCode.Conflict, "deleting a company with an event to come"))
            .Should().Contain("private event");
    }

    [Fact]
    public async Task AContactWithMoneyOnTheirAccountIsKept_AndMergingThemMovesTheirAccountEventsAndBilling()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = await ClubAsync(factory);
        var lena = await ContactAsync(client, "Lena Graf", "lena@graf.ch");
        var duplicate = await ContactAsync(client, "L. Graf", "l.graf@example.ch");
        var wedding = await EventAsync(client, new { title = "Graf wedding", contactId = duplicate, startAtUtc = At(Evening), endAtUtc = At(Evening.AddHours(8)) });
        await Json(await client.PostAsync($"/api/private-events/{Id(wedding)}/deposit", Body(new { amount = 1500m })), "the deposit, on the duplicate's account");
        var acme = await CompanyAsync(client, "Acme AG");
        var membership = await Json(await client.PostAsync("/api/corporate-memberships", Body(new
        {
            companyId = acme, name = "Acme corporate", seats = 2, nomineeCharges = "company", startsAtUtc = DateTime.UtcNow.Date.ToString("O"), billingContactId = duplicate,
        })), "a membership billed to the duplicate");

        (await Refused(await client.DeleteAsync($"/api/contacts/{duplicate}"), HttpStatusCode.Conflict, "deleting a contact with money on their account"))
            .Should().Contain("house account");

        await Done(await client.PostAsync($"/api/contacts/{lena}/merge", Body(new { otherContactId = duplicate })), "merge the duplicate into Lena");

        var moved = await Json(await client.GetAsync($"/api/private-events/{Id(wedding)}"), "the wedding, after");
        moved.GetProperty("contactId").GetGuid().Should().Be(lena);
        moved.GetProperty("money").GetProperty("billToAccountId").GetGuid().Should().Be(lena, "its money is on Lena's account now");
        var account = await Json(await client.GetAsync($"/api/club-accounts/contacts/{lena}"), "Lena's account");
        (account.GetProperty("open").GetBoolean(), account.GetProperty("owed").GetDecimal()).Should().Be((true, 1500m));
        (await Json(await client.GetAsync($"/api/charges?memberId={lena}"), "Lena's charges")).EnumerateArray().Single()
            .GetProperty("memberName").GetString().Should().Be("Lena Graf");
        var membershipId = Id(membership);
        (await factory.Services.Read(db => db.ClubCorporateMemberships.Single(m => m.Id == membershipId).BillingContactId)).Should().Be(lena);
    }

    [Fact]
    public async Task MergingMembersMovesASeatAndGuestPlaces_OneSeatEach_AndDeletingAMemberFreesTheirSeat()
    {
        using var factory = new CustomWebApplicationFactory();
        var client = await ClubAsync(factory);
        var acme = await CompanyAsync(client, "Acme AG");
        var membership = Id(await CorporateAsync(client, acme, seats: 4));
        var anna = await MemberAsync(client, "Anna");
        var annaAgain = await MemberAsync(client, "Anna", "Keller-Graf");
        var ben = await MemberAsync(client, "Ben");
        var benAgain = await MemberAsync(client, "Ben", "Meier");
        var dora = await MemberAsync(client, "Dora");
        await SeatAsync(client, membership, anna);
        await SeatAsync(client, membership, annaAgain);
        await SeatAsync(client, membership, benAgain);
        await SeatAsync(client, membership, dora);
        var party = Id(await EventAsync(client, new { title = "Acme party", companyId = acme, startAtUtc = At(Evening), endAtUtc = At(Evening.AddHours(4)) }));
        await Json(await client.PostAsync($"/api/private-events/{party}/guests", Body(new { memberId = annaAgain })), "Anna's other record on the list");
        await Json(await client.PostAsync($"/api/private-events/{party}/guests", Body(new { memberId = dora })), "Dora on the list");

        // Both of Anna's records held a seat: one person holds one, so one ends.
        await Done(await client.PostAsync($"/api/members/{anna}/merge", Body(new { otherMemberId = annaAgain })), "merge Anna's records");
        // Ben's other record held the seat: Ben holds it now.
        await Done(await client.PostAsync($"/api/members/{ben}/merge", Body(new { otherMemberId = benAgain })), "merge Ben's records");

        var after = await Json(await client.GetAsync($"/api/corporate-memberships/{membership}"), "the membership");
        after.GetProperty("nominees").EnumerateArray().Select(n => n.GetProperty("memberId").GetGuid()).Should().BeEquivalentTo([anna, ben, dora]);
        after.GetProperty("formerNominees").EnumerateArray().Single().GetProperty("removedReason").GetString().Should().Contain("Merged into Anna Keller");
        (await Json(await client.GetAsync($"/api/members/{ben}/corporate"), "Ben's seat")).GetProperty("companyName").GetString().Should().Be("Acme AG");
        var guests = (await Json(await client.GetAsync($"/api/private-events/{party}"), "the party")).GetProperty("guests").EnumerateArray().ToList();
        guests.Single(g => g.GetProperty("name").GetString() == "Anna Keller").GetProperty("memberId").GetGuid().Should().Be(anna);

        await Done(await client.DeleteAsync($"/api/members/{dora}"), "delete Dora");
        (await Json(await client.GetAsync($"/api/corporate-memberships/{membership}"), "the membership, after Dora"))
            .GetProperty("seatsUsed").GetInt32().Should().Be(2, "her seat is free for the company");
        var doraGuest = (await Json(await client.GetAsync($"/api/private-events/{party}"), "the party, after Dora")).GetProperty("guests").EnumerateArray()
            .Single(g => g.GetProperty("name").GetString() == "Dora Keller");
        doraGuest.TryGetProperty("memberId", out _).Should().BeFalse("she stays on the list by name, pointing at no member");
    }
}
