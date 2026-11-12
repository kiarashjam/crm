using System.Net;
using System.Text.Json;
using ACI.Application.Services;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The ledger's races on a real SQL Server: two writes that both read the same money and
/// then both try to spend it.
/// </summary>
/// <remarks>
/// The HTTP tests show this on the in-memory provider, which has no transactions or locks.
/// Here each write's first save takes the row it decides on (the member's house account,
/// the card, the tab) in a transaction, so the second write waits for the first and is then
/// refused; the gate makes both read before either writes, every time.
/// </remarks>
public class ClubLedgerOnSqlServerTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task RaceAsync(string kind, Func<HttpClient, Guid, Task<Func<Task<HttpResponseMessage>>>> arrange, Func<HttpClient, Task> check)
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            var gate = new RaceFactory.ReadGate();
            using var app = SqlServerApp.Start(database, services: services => services.AddSingleton<ILedgerReadObserver>(gate));
            var (client, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var created = await client.PostAsync("/api/organizations", Body(new { name = "Pavillon46" }));
            var org = (await Json(created, "create the club")).GetProperty("id").GetGuid();
            client.In(org);
            var memberId = (await Json(await client.PostAsync("/api/members", Body(new { firstName = "Jean", lastName = "Dupont", email = "jean@example.ch", tier = "Gold", status = "Active" })), "add member"))
                .GetProperty("id").GetGuid();
            var write = await arrange(client, memberId);
            gate.Arm(kind);

            var answers = await Task.WhenAll(write(), write());

            answers.Select(a => a.StatusCode).Should().BeEquivalentTo(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict },
                "the second write read what the first one changed");
            await check(client);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public Task TwoPaymentsOfOneChargeAtOnce_PayItOnce() => RaceAsync("charges",
        async (client, memberId) =>
        {
            var charge = (await Json(await client.PostAsync("/api/charges", Body(new { memberId, kind = "Dining", description = "Dinner", amount = 150 })), "post charge"))
                .GetProperty("id").GetString();
            return () => client.PostAsync("/api/payments", Body(new { amount = 150, method = "Cash", chargeId = charge }));
        },
        async client =>
        {
            (await Json(await client.GetAsync("/api/payments"), "payments")).GetArrayLength().Should().Be(1);
            var member = (await Json(await client.GetAsync("/api/members"), "members"))[0];
            member.GetProperty("houseAccountBalance").GetDecimal().Should().Be(0);
        });

    [SqlServerFact]
    public Task TwoRedeemsOfOneCardAtOnce_SpendItOnce() => RaceAsync("gift-cards",
        async (client, _) =>
        {
            var card = (await Json(await client.PostAsync("/api/gift-cards", Body(new { purchaserName = "Ana", recipientName = "Bea", faceValue = 50 })), "issue"))
                .GetProperty("id").GetString();
            return () => client.PostAsync($"/api/gift-cards/{card}/redeem", Body(new { amount = 50 }));
        },
        async client =>
        {
            (await Json(await client.GetAsync("/api/gift-cards/redemptions"), "redemptions")).GetArrayLength().Should().Be(1, "100 used to be spent from a 50 card");
            (await Json(await client.GetAsync("/api/gift-cards"), "cards"))[0].GetProperty("remainingValue").GetDecimal().Should().Be(0);
        });

    [SqlServerFact]
    public Task TwoClosesOfOneTabAtOnce_PostItOnce() => RaceAsync("tabs",
        async (client, memberId) =>
        {
            var tab = (await Json(await client.PostAsync("/api/tabs", Body(new { memberId, venue = "Bar" })), "open tab")).GetProperty("id").GetString();
            await Json(await client.PostAsync($"/api/tabs/{tab}/items", Body(new { name = "Negroni", quantity = 2, unitPrice = 15 })), "add item");
            return () => client.PostAsync($"/api/tabs/{tab}/close", null);
        },
        async client =>
        {
            (await Json(await client.GetAsync("/api/charges"), "charges")).GetArrayLength().Should().Be(1);
            (await Json(await client.GetAsync("/api/loyalty/ledger"), "points")).GetArrayLength().Should().Be(1);
            (await Json(await client.GetAsync("/api/members"), "members"))[0].GetProperty("houseAccountBalance").GetDecimal().Should().Be(-30);
        });
}
