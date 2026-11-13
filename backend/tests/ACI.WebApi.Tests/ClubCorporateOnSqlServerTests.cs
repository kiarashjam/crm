using System.Net;
using System.Text.Json;
using ACI.Application.Services;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Corporate memberships, private events and client accounts on a real SQL Server: the last
/// seat asked for twice at once, one member seated on two companies at once, a party and a
/// member booking the same room at once, a party moved onto a booking, and a company and a
/// contact nothing keeps deleted.
/// </summary>
/// <remarks>
/// The in-memory provider the HTTP tests run on has no transactions, locks or filtered unique
/// indexes, and cannot run the set-based updates a company's or contact's delete makes. Here a
/// seat change takes the membership's row first and the one-seat-per-member index is enforced; a
/// hold takes the room's row as a booking does; and a party whose new time clashes is rolled
/// back with its holds. The gate makes both requests read before either writes, every time.
/// </remarks>
public class ClubCorporateOnSqlServerTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>A club on its own database, and the gate the services report their reads to.</summary>
    private static async Task RunAsync(Func<HttpClient, RaceFactory.ReadGate, string, Task> test)
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
            await test(client, gate, database);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    private static Guid Id(JsonElement e) => e.GetProperty("id").GetGuid();

    private static async Task<Guid> CompanyAsync(HttpClient client, string name) =>
        Id(await Json(await client.PostAsync("/api/companies", Body(new { name })), $"add {name}"));

    private static async Task<Guid> MemberAsync(HttpClient client, string first) =>
        Id(await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Dupont", email = $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
        })), $"add {first}"));

    private static async Task<Guid> CorporateAsync(HttpClient client, Guid company, int seats) =>
        Id(await Json(await client.PostAsync("/api/corporate-memberships", Body(new
        {
            companyId = company, name = "Corporate", seats, nomineeCharges = "company", startsAtUtc = DateTime.UtcNow.Date.ToString("O"),
        })), "make the corporate membership"));

    private static async Task<int> CountAsync(string database, string sql) => Convert.ToInt32(await SqlServerApp.ScalarAsync(database, sql));

    [SqlServerFact]
    public Task TwoNomineesForTheLastSeatAtOnce_OneIsSeatedAndTheOtherToldItIsTaken() => RunAsync(async (client, gate, database) =>
    {
        var membership = await CorporateAsync(client, await CompanyAsync(client, "Acme AG"), seats: 1);
        var jean = await MemberAsync(client, "Jean");
        var marie = await MemberAsync(client, "Marie");
        gate.Arm("corporate");

        var answers = await Task.WhenAll(
            client.PostAsync($"/api/corporate-memberships/{membership}/nominees", Body(new { memberId = jean })),
            client.PostAsync($"/api/corporate-memberships/{membership}/nominees", Body(new { memberId = marie })));

        answers.Select(a => a.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict },
            "both read a free seat; the second is decided again on what the first saved");
        (await answers.Single(a => a.StatusCode == HttpStatusCode.Conflict).Content.ReadAsStringAsync()).Should().Contain("Its one seat is taken");
        (await CountAsync(database, $"SELECT COUNT(*) FROM [ClubCorporateNominees] WHERE [CorporateMembershipId] = '{membership}' AND [RemovedAtUtc] IS NULL"))
            .Should().Be(1);
    });

    [SqlServerFact]
    public Task OneMemberSeatedOnTwoCompaniesAtOnce_HoldsOneSeat() => RunAsync(async (client, gate, database) =>
    {
        var acme = await CorporateAsync(client, await CompanyAsync(client, "Acme AG"), seats: 2);
        var bolt = await CorporateAsync(client, await CompanyAsync(client, "Bolt GmbH"), seats: 2);
        var jean = await MemberAsync(client, "Jean");
        gate.Arm("corporate");

        var answers = await Task.WhenAll(
            client.PostAsync($"/api/corporate-memberships/{acme}/nominees", Body(new { memberId = jean })),
            client.PostAsync($"/api/corporate-memberships/{bolt}/nominees", Body(new { memberId = jean })));

        answers.Select(a => a.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict },
            "two memberships' rows do not stop each other; the one-seat-per-member index does");
        (await CountAsync(database, $"SELECT COUNT(*) FROM [ClubCorporateNominees] WHERE [MemberId] = '{jean}' AND [RemovedAtUtc] IS NULL")).Should().Be(1);
    });

    [SqlServerFact]
    public Task APartyAndAMembersBookingOfTheSameRoomAtOnce_OneGetsTheRoom() => RunAsync(async (client, gate, database) =>
    {
        var acme = await CompanyAsync(client, "Acme AG");
        var room = Id(await Json(await client.PostAsync("/api/resources", Body(new { name = "Library", kind = "Meeting", capacity = 20 })), "add the library"));
        var jean = await MemberAsync(client, "Jean");
        var start = DateTime.UtcNow.Date.AddDays(20).AddHours(17);
        gate.Arm("reservations");

        var answers = await Task.WhenAll(
            client.PostAsync("/api/private-events", Body(new
            {
                title = "Acme party", companyId = acme, startAtUtc = start.ToString("O"), endAtUtc = start.AddHours(4).ToString("O"), resourceIds = new[] { room },
            })),
            client.PostAsync("/api/reservations", Body(new
            {
                resourceId = room, memberId = jean, partySize = 2, startAtUtc = start.AddHours(1), endAtUtc = start.AddHours(2),
            })));

        answers.Select(a => a.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict },
            "both read a free room; the second is checked again on what the first saved");
        (await CountAsync(database, $"SELECT COUNT(*) FROM [ClubReservations] WHERE [ResourceId] = '{room}' AND [Status] IN ('Pending', 'Confirmed')"))
            .Should().Be(1, "the room is either the party's or Jean's");
        if (answers[0].StatusCode == HttpStatusCode.Conflict)
            (await CountAsync(database, "SELECT COUNT(*) FROM [ClubPrivateEvents]")).Should().Be(0, "a party refused its room is not left half made");
    });

    [SqlServerFact]
    public Task MovingAPartyOntoABookingIsRefused_AndRollsBackLeavingThePartyAndItsHoldWhereTheyWere() => RunAsync(async (client, _, database) =>
    {
        var acme = await CompanyAsync(client, "Acme AG");
        var room = Id(await Json(await client.PostAsync("/api/resources", Body(new { name = "Library", kind = "Meeting", capacity = 20 })), "add the library"));
        var jean = await MemberAsync(client, "Jean");
        var start = DateTime.UtcNow.Date.AddDays(20).AddHours(12);
        var party = await Json(await client.PostAsync("/api/private-events", Body(new
        {
            title = "Acme lunch", companyId = acme, startAtUtc = start.ToString("O"), endAtUtc = start.AddHours(3).ToString("O"), resourceIds = new[] { room },
        })), "make the party");
        await Json(await client.PostAsync("/api/reservations", Body(new
        {
            resourceId = room, memberId = jean, partySize = 2, startAtUtc = start.AddHours(5), endAtUtc = start.AddHours(6),
        })), "Jean books the evening");

        var moved = await client.PutAsync($"/api/private-events/{Id(party)}", Body(new
        {
            title = "Acme dinner", startAtUtc = start.AddHours(4).ToString("O"), endAtUtc = start.AddHours(7).ToString("O"),
            version = party.GetProperty("version").GetGuid(),
        }));
        moved.StatusCode.Should().Be(HttpStatusCode.Conflict, await moved.Content.ReadAsStringAsync());

        var after = await Json(await client.GetAsync($"/api/private-events/{Id(party)}"), "the party, after");
        (after.GetProperty("title").GetString(), after.GetProperty("startAtUtc").GetDateTime(), after.GetProperty("version").GetGuid())
            .Should().Be(("Acme lunch", start, party.GetProperty("version").GetGuid()), "the event's own change was rolled back with its spaces");
        (await CountAsync(database, $"SELECT COUNT(*) FROM [ClubReservations] WHERE [PrivateEventId] = '{Id(party)}' AND [Status] = 'Pending' AND [StartAtUtc] = '{start:yyyy-MM-ddTHH:mm:ss}'"))
            .Should().Be(1, "its hold is where it was");
    });

    [SqlServerFact]
    public Task TwoCompaniesWhoseStatementsForTheSameMonthWerePrinted_CanBeMerged_AndTheDuplicatesSlipStillPays() => RunAsync(async (client, _, database) =>
    {
        // A statement has one QR reference per account and period (a filtered unique index): the
        // month's statements of both records were printed, so moving the duplicate's onto the
        // company kept used to break that index and the merge failed.
        await Json(await client.PutAsync("/api/payment-settings", Body(new
        {
            iban = "CH5800791123000889012", qrIban = "CH4431999123000889012",
            creditorName = "Pavillon 46", creditorPostalCode = "2501", creditorTown = "Biel", creditorCountry = "CH",
        })), "set the bank");
        var from = Uri.EscapeDataString(DateTime.UtcNow.Date.AddDays(-7).ToString("o"));
        var to = Uri.EscapeDataString(DateTime.UtcNow.Date.AddDays(7).ToString("o"));
        var acme = await CompanyAsync(client, "Acme AG");
        var duplicate = await CompanyAsync(client, "ACME");
        foreach (var company in new[] { acme, duplicate })
        {
            await Json(await client.PostAsync($"/api/club-accounts/companies/{company}", null), "open the account");
            await Json(await client.PostAsync("/api/charges", Body(new { memberId = company, kind = "Event", description = "Summer party", amount = 1200 })), "charge it");
            var statement = await client.GetAsync($"/api/club-accounts/companies/{company}/statement.pdf?from={from}&to={to}");
            statement.StatusCode.Should().Be(HttpStatusCode.OK, await statement.Content.ReadAsStringAsync());
        }
        var duplicateReference = (string)(await SqlServerApp.ScalarAsync(database, $"SELECT [QrReference] FROM [ClubPaymentReferences] WHERE [MemberId] = '{duplicate}'"))!;
        // Part of the duplicate's statement was paid by transfer before anyone saw it was a duplicate.
        (await Json(await BankFileTests.ImportAsync(client, BankFileTests.Camt054(("TX-1", 200m, duplicateReference)), "first.xml"), "import the first transfer"))
            .GetProperty("matched").GetInt32().Should().Be(1);

        var merged = await client.PostAsync($"/api/companies/{acme}/merge", Body(new { otherCompanyId = duplicate }));
        merged.IsSuccessStatusCode.Should().BeTrue(await merged.Content.ReadAsStringAsync());

        (await CountAsync(database, $"SELECT COUNT(*) FROM [ClubPaymentReferences] WHERE [MemberId] = '{acme}'"))
            .Should().Be(2, "the duplicate's printed reference is Acme's now, beside its own");
        (await CountAsync(database, $"SELECT COUNT(*) FROM [ClubBankTransactions] WHERE [MemberId] = '{acme}'"))
            .Should().Be(1, "the transfer recorded as the duplicate's is Acme's, as its payment is");
        // The rest of the duplicate's slip, paid after the merge, lands on Acme's account.
        (await Json(await BankFileTests.ImportAsync(client, BankFileTests.Camt054(("TX-2", 1000m, duplicateReference)), "second.xml"), "import the second transfer"))
            .GetProperty("matched").GetInt32().Should().Be(1);
        (await CountAsync(database, $"SELECT COUNT(*) FROM [ClubPayments] WHERE [MemberId] = '{acme}' AND [Provider] = N'bank'")).Should().Be(2);
        (await CountAsync(database, $"SELECT COUNT(*) FROM [ClubPayments] WHERE [MemberId] = '{duplicate}'")).Should().Be(0);
    });

    [SqlServerFact]
    public Task ACompanyAndAContactNothingKeepsAreDeleted_WithTheirEmptyAccounts_AndPastEventsStopNamingThem() => RunAsync(async (client, _, database) =>
    {
        var bolt = await CompanyAsync(client, "Bolt GmbH");
        await Json(await client.PostAsync($"/api/club-accounts/companies/{bolt}", null), "open Bolt's account");
        var start = DateTime.UtcNow.Date.AddDays(20).AddHours(18);
        var dinner = await Json(await client.PostAsync("/api/private-events", Body(new
        {
            title = "Bolt dinner", companyId = bolt, startAtUtc = start.ToString("O"), endAtUtc = start.AddHours(3).ToString("O"),
        })), "Bolt's dinner");
        await Json(await client.PostAsync($"/api/private-events/{Id(dinner)}/cancel", Body(new { reason = "Called off" })), "cancel it");
        var lena = Id(await Json(await client.PostAsync("/api/contacts", Body(new { name = "Lena Graf", email = "lena@graf.ch" })), "add Lena"));
        await Json(await client.PostAsync($"/api/club-accounts/contacts/{lena}", null), "open Lena's account");

        (await client.DeleteAsync($"/api/companies/{bolt}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.DeleteAsync($"/api/contacts/{lena}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await CountAsync(database, $"SELECT COUNT(*) FROM [ClubHouseAccounts] WHERE [MemberId] IN ('{bolt}', '{lena}')")).Should().Be(0);
        (await CountAsync(database, $"SELECT COUNT(*) FROM [ClubPrivateEvents] WHERE [Id] = '{Id(dinner)}' AND [CompanyId] IS NULL")).Should().Be(1);
    });
}
