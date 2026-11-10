using System.Net;
using System.Text.Json;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.FakeLightspeed;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Sales and the POS import on a real SQL Server: the migration's tables and unique keys, two
/// sales racing for one member's account, and two imports racing for one workspace.
/// </summary>
/// <remarks>
/// The in-memory provider has no transactions, no unique indexes and no conditional UPDATE;
/// only a real server shows that the second of two sales with one Idempotency-Key finds the
/// first, that the lease lets one import run, and that a ticket's id is held once.
/// </remarks>
public class ClubPosOnSqlServerTests
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private sealed record World(SqlServerApp App, HttpClient Client, Guid Org, Guid Member, Guid Negroni, RaceFactory.ReadGate Gate, FakeLightspeed Lightspeed);

    private static async Task RunAsync(Func<World, Task> test)
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            var gate = new RaceFactory.ReadGate();
            var lightspeed = new FakeLightspeed();
            using var app = SqlServerApp.Start(database, services: services =>
            {
                services.AddSingleton<ILedgerReadObserver>(gate);
                lightspeed.Install(services);
            });
            var (client, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var org = (await Ok(await client.PostAsync("/api/organizations", Body(new { name = "Pavillon46" })), "create the club")).GetProperty("id").GetGuid();
            client.In(org);
            var member = (await Ok(await client.PostAsync("/api/members", Body(new
            {
                firstName = "Jean", lastName = "Dupont", email = "jean@example.ch", tier = "Gold", status = "Active",
            })), "add Jean")).GetProperty("id").GetGuid();
            var negroni = (await Ok(await client.PostAsync("/api/catalog", Body(new { kind = "MenuItem", name = "Negroni", price = 18.50m, vatRate = 8.1m, chargeKind = "Bar" })),
                "add the Negroni")).GetProperty("id").GetGuid();
            await test(new World(app, client, org, member, negroni, gate, lightspeed));
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    private static HttpRequestMessage SaleRequest(World w, string? key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/sales")
        {
            Content = Body(new { memberId = w.Member, venue = "Bar", tender = "HouseAccount", lines = new[] { new { catalogItemId = w.Negroni, quantity = 1m } } }),
        };
        if (key != null) request.Headers.Add("Idempotency-Key", key);
        return request;
    }

    private static async Task<decimal> BalanceAsync(World w) =>
        (await Ok(await w.Client.GetAsync($"/api/members/{w.Member}/balance"), "read the balance")).GetProperty("balance").GetDecimal();

    [SqlServerFact]
    public Task TwoSalesWithOneIdempotencyKeyAtOnce_PostOnce() => RunAsync(async w =>
    {
        w.Gate.Arm("sales");
        var answers = await Task.WhenAll(w.Client.SendAsync(SaleRequest(w, "till-2:sale-7")), w.Client.SendAsync(SaleRequest(w, "till-2:sale-7")));

        var sales = await Task.WhenAll(answers.Select(a => Ok(a, "a sale with the key")));
        sales[1].GetProperty("id").GetGuid().Should().Be(sales[0].GetProperty("id").GetGuid(), "the second found the sale the first posted");
        (await Ok(await w.Client.GetAsync($"/api/charges?memberId={w.Member}"), "charges")).GetArrayLength().Should().Be(1);
        (await BalanceAsync(w)).Should().Be(-18.50m);
    });

    [SqlServerFact]
    public Task TwoSalesForOneMemberAtOnce_AreBothPosted() => RunAsync(async w =>
    {
        // Two tills ring up the same member at the same moment: the account row lets one write at
        // a time, and a sale that lost the race is posted again rather than refused.
        w.Gate.Arm("sales");
        var answers = await Task.WhenAll(w.Client.SendAsync(SaleRequest(w, null)), w.Client.SendAsync(SaleRequest(w, null)));

        answers.Select(a => a.StatusCode).Should().AllBeEquivalentTo(HttpStatusCode.OK);
        (await Ok(await w.Client.GetAsync($"/api/charges?memberId={w.Member}"), "charges")).GetArrayLength().Should().Be(2);
        (await BalanceAsync(w)).Should().Be(-37.00m);
        var member = (await Ok(await w.Client.GetAsync("/api/members"), "members"))[0];
        member.GetProperty("houseAccountBalance").GetDecimal().Should().Be(-37.00m, "the list's balance follows both");
    });

    [SqlServerFact]
    public Task TwoImportsAtOnce_ImportOnce_AndATicketsIdIsHeldOnce() => RunAsync(async w =>
    {
        await Ok(await w.Client.PostAsync($"/api/members/{w.Member}/codes", Body(new { code = "1042" })), "give Jean his code");
        var url = (await Ok(await w.Client.PostAsync("/api/pos/connect", null), "start connecting")).GetProperty("url").GetString()!;
        var state = System.Web.HttpUtility.ParseQueryString(new Uri(url).Query)["state"]!;
        using (var browser = w.App.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }))
            (await browser.GetAsync($"/api/public/pos/lightspeed/callback?code=c&state={Uri.EscapeDataString(state)}")).Headers.Location!.ToString()
                .Should().EndWith("connected=1");
        await Ok(await w.Client.PutAsync("/api/pos/settings", Body(new
        {
            locationId = "46100001", enabled = true, importFromUtc = DateTime.UtcNow.AddHours(-1).ToString("O"),
        })), "turn the import on");
        w.Lightspeed.Sales.Add(Sale("R1", DateTime.UtcNow.AddMinutes(-5), [Line("Negroni", 1, 18.50m, 8.1m)],
            [Payment("ACCOUNT", "House account", 18.50m)], accountName: "1042"));
        w.Lightspeed.HoldSales = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = w.Client.PostAsync("/api/pos/import", null);
        await w.Lightspeed.SalesAsked.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var second = await w.Client.PostAsync("/api/pos/import", null);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict, "the first import holds the workspace");
        // The scheduled run finds the workspace claimed too.
        using (var scope = w.App.Services.CreateScope())
            (await scope.ServiceProvider.GetRequiredService<IClubPos>().ImportDueAsync(DateTime.UtcNow)).Should().Be(0);
        w.Lightspeed.HoldSales.SetResult();
        (await Ok(await first, "the first import")).GetProperty("posted").GetInt32().Should().Be(1);
        (await Ok(await w.Client.PostAsync("/api/pos/import", null), "import again")).GetProperty("imported").GetInt32().Should().Be(0);
        (await BalanceAsync(w)).Should().Be(-18.50m);

        // The database holds a ticket's id once per workspace, whatever the code checks first.
        using var check = w.App.Services.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<AppDbContext>();
        using var acting = db.ActFor(w.Org, null);
        db.ClubPosTickets.Add(new ClubPosTicket
        {
            Id = Guid.NewGuid(), OrganizationId = w.Org, Provider = PosProviders.LightspeedK, ExternalId = "46100001:R1", ClosedAtUtc = DateTime.UtcNow,
            Currency = "CHF", Status = PosTicketStatuses.Unmatched, ImportedAtUtc = DateTime.UtcNow,
        });
        await FluentActions.Invoking(() => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateException>();
    });
}
