using System.Net;
using System.Text.Json;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.FakeLightspeed;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The POS, connected: Lightspeed K-Series' closed tickets imported once each, posted to the
/// members whose code is on them (owing when put on the house account, paid when paid at the
/// POS), with the points by the club's settings, and the ones that need a member waiting for
/// staff to match, retry or set aside.
/// </summary>
/// <remarks>
/// Lightspeed is played by <see cref="FakeLightspeed"/>, answering as its documentation says.
/// </remarks>
public class ClubPosTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    /// <summary>A club connected to Lightspeed, importing the restaurant's tickets, with Jean (Gold) carrying the code 1042.</summary>
    private static async Task<(HttpClient Client, Guid Org, Guid Jean)> ConnectedClubAsync(PosApp app)
    {
        var (client, _, org) = await Club.CreateAsync(app);
        var jean = await Club.MemberAsync(client, "Jean", "Gold");
        await Club.Json(await client.PostAsync($"/api/members/{jean}/codes", Body(new { code = "1042", label = "Member card" })), "give Jean the code 1042");
        await ConnectAsync(app, client);
        await Club.Json(await client.PutAsync("/api/pos/settings", Body(new
        {
            locationId = "46100001", locationName = "Pavillon 46 Restaurant", enabled = true, importFromUtc = Now.AddHours(-3).ToString("O"),
        })), "choose the location and turn the import on");
        return (client, org, jean);
    }

    /// <summary>
    /// Connects the club's Lightspeed account as a browser would: to Lightspeed's page, back to the
    /// POS tickets page with a code, which the page passes on.
    /// </summary>
    private static async Task ConnectAsync(PosApp app, HttpClient client)
    {
        var state = await StartConnectingAsync(client);
        var connected = await Club.Json(await client.PostAsync("/api/pos/connect/complete", Body(new { code = "the-code", state })), "finish connecting");
        connected.GetProperty("connected").GetBoolean().Should().BeTrue();
    }

    /// <summary>Starts connecting, and returns the state Lightspeed is to send back with the code.</summary>
    private static async Task<string> StartConnectingAsync(HttpClient client)
    {
        var url = (await Club.Json(await client.PostAsync("/api/pos/connect", null), "start connecting")).GetProperty("url").GetString()!;
        return System.Web.HttpUtility.ParseQueryString(new Uri(url).Query)["state"]!;
    }

    private static async Task<JsonElement> ImportAsync(HttpClient client) =>
        await Club.Json(await client.PostAsync("/api/pos/import", null), "import from the POS");

    private static async Task<List<JsonElement>> TicketsAsync(HttpClient client, string status = "all") =>
        (await Club.Json(await client.GetAsync($"/api/pos/tickets?status={status}"), "list the POS tickets")).EnumerateArray().ToList();

    private static JsonElement Ticket(List<JsonElement> tickets, string receipt) =>
        tickets.Single(t => t.GetProperty("receiptNumber").GetString() == receipt);

    private static object OnAccount(decimal amount, decimal tip = 0) => Payment("ACCOUNT", "House account", amount, tip);

    // ── connecting ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ConnectingSendsTheBrowserToLightspeed_KeepsTheTokens_AndNeverShowsThem()
    {
        using var app = new PosApp();
        var (client, _, _) = await Club.CreateAsync(app);

        var url = new Uri((await Club.Json(await client.PostAsync("/api/pos/connect", null), "start connecting")).GetProperty("url").GetString()!);
        url.GetLeftPart(UriPartial.Path).Should().Be("https://lightspeed.test/oauth/authorize");
        var query = System.Web.HttpUtility.ParseQueryString(url.Query);
        query["response_type"].Should().Be("code");
        query["client_id"].Should().Be(ClientId);
        query["scope"].Should().Be("financial-api");
        query["redirect_uri"].Should().EndWith("/pos-tickets", "Lightspeed sends the browser back to the POS tickets page, which passes the code on signed in");

        // A code with a state Cadence did not sign connects nothing.
        await Club.Refused(await client.PostAsync("/api/pos/connect/complete", Body(new { code = "x", state = "made-up" })),
            HttpStatusCode.BadRequest, "finish connecting with a state Cadence did not sign");
        app.Lightspeed.Sent("POST", "/oauth/token").Should().BeEmpty();
        (await Club.Json(await client.GetAsync("/api/pos/settings"), "read the settings")).GetProperty("connected").GetBoolean().Should().BeFalse();

        await Club.Json(await client.PostAsync("/api/pos/connect/complete", Body(new { code = "the-code", state = query["state"] })), "finish connecting");
        var exchange = app.Lightspeed.Sent("POST", "/oauth/token").Single();
        exchange.Form["grant_type"].Should().Be("authorization_code");
        exchange.Form["code"].Should().Be("the-code");
        exchange.Form["redirect_uri"].Should().Be(query["redirect_uri"], "the code is exchanged for the address it was sent to");

        var body = await (await client.GetAsync("/api/pos/settings")).Content.ReadAsStringAsync();
        body.Should().Contain("\"connected\":true").And.NotContain("access-1").And.NotContain("refresh-1");
        var status = await Club.Json(await client.GetAsync("/api/pos/status"), "read the status");
        status.GetProperty("connected").GetBoolean().Should().BeTrue();
        status.GetProperty("importing").GetBoolean().Should().BeFalse("no location is chosen yet");

        var locations = (await Club.Json(await client.GetAsync("/api/pos/locations"), "list the locations")).EnumerateArray().ToList();
        locations.Select(l => (l.GetProperty("id").GetString(), l.GetProperty("name").GetString()))
            .Should().Equal(("46100001", "Pavillon 46 Restaurant"), ("46100002", "Pavillon 46 Bar"));
    }

    [Fact]
    public async Task OnlyWhoeverStartedConnecting_FinishesIt_InTheWorkspaceTheyStartedIn()
    {
        using var app = new PosApp();
        var (club, _, clubOrg) = await Club.CreateAsync(app);
        var (other, _, otherOrg) = await Club.CreateAsync(app, "someone@example.org", "Another club");
        var finance = await Club.JoinAsync(app, club, clubOrg, "finance@example.ch", "finance");

        // The club's manager starts connecting; someone else signs in to Lightspeed with the link
        // and sends the code to their own workspace, or a colleague tries to finish it: neither
        // takes the club's connection.
        var clubState = await StartConnectingAsync(club);
        await Club.Refused(await other.PostAsync("/api/pos/connect/complete", Body(new { code = "their-code", state = clubState })),
            HttpStatusCode.BadRequest, "finish someone else's connection in another workspace");
        await Club.Refused(await finance.PostAsync("/api/pos/connect/complete", Body(new { code = "their-code", state = clubState })),
            HttpStatusCode.BadRequest, "finish a colleague's connection");

        // The other way round: a state made in another workspace, sent with the club's own code
        // (a link someone else made), does not connect that workspace to the club's account.
        var otherState = await StartConnectingAsync(other);
        var refused = await Club.Refused(await club.PostAsync("/api/pos/connect/complete", Body(new { code = "the-clubs-code", state = otherState })),
            HttpStatusCode.BadRequest, "finish a connection started in another workspace");
        refused.Should().Contain("started by someone else, or in another workspace");

        app.Lightspeed.Sent("POST", "/oauth/token").Should().BeEmpty("no code was exchanged for anyone");
        (await Club.Json(await club.GetAsync("/api/pos/settings"), "the club's settings")).GetProperty("connected").GetBoolean().Should().BeFalse();
        (await Club.Json(await other.GetAsync("/api/pos/settings"), "the other settings")).GetProperty("connected").GetBoolean().Should().BeFalse();

        await Club.Json(await club.PostAsync("/api/pos/connect/complete", Body(new { code = "the-clubs-code", state = clubState })), "finish connecting");
        (await Club.Json(await club.GetAsync("/api/pos/settings"), "the club's settings")).GetProperty("connected").GetBoolean().Should().BeTrue();
        (await Club.Json(await other.GetAsync("/api/pos/settings"), "the other settings")).GetProperty("connected").GetBoolean().Should().BeFalse();
        otherOrg.Should().NotBe(clubOrg);
    }

    [Fact]
    public async Task WithoutCadencesLightspeedClient_ConnectingSaysSo()
    {
        using var app = new CustomWebApplicationFactory();
        var (client, _, _) = await Club.CreateAsync(app);
        var refused = await Club.Refused(await client.PostAsync("/api/pos/connect", null), HttpStatusCode.ServiceUnavailable, "connect with no client id");
        refused.Should().Contain("Lightspeed is not set up on this server");
        (await Club.Json(await client.GetAsync("/api/pos/settings"), "read the settings")).GetProperty("serverConfigured").GetBoolean().Should().BeFalse();
        await Club.Refused(await client.PutAsync("/api/pos/settings", Body(new { enabled = true })), HttpStatusCode.BadRequest, "turn on an import with nothing connected");
    }

    // ── importing ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClosedTicketsAreImportedOnce_AndPostedToTheMemberWhoseCodeIsOnThem()
    {
        using var app = new PosApp();
        var (client, _, jean) = await ConnectedClubAsync(app);
        app.Lightspeed.Sales.AddRange(
        [
            // On the house account, Jean's code typed as the bill's name: owing, VAT per line.
            Sale("R1", Now.AddMinutes(-50), [Line("Tartare", 1, 32.00m, 8.1m), Line("Wine", 2, 24.00m, 8.1m), Line("Club history", 1, 40.00m, 2.6m)],
                [OnAccount(96.00m)], accountName: "1042"),
            // Paid by card at the table, Jean's card scanned onto the customer: recorded as paid.
            Sale("R2", Now.AddMinutes(-40), [Line("Negroni", 2, 37.00m, 8.1m)], [Payment("VISA", "Visa", 37.00m, tip: 3.00m)],
                consumer: new { id = 501, firstName = "Jean", lastName = "Dupont", cardNumber = "1042" }),
            // Paid in cash by someone nobody knows: not a member's.
            Sale("R3", Now.AddMinutes(-30), [Line("Espresso", 1, 4.50m, 8.1m)], [Payment("CASH", "Cash", 4.50m)]),
            // Put on the house account with nobody's code on it: waits for staff.
            Sale("R4", Now.AddMinutes(-20), [Line("Club sandwich", 1, 28.00m, 8.1m)], [OnAccount(28.00m)],
                consumer: new { id = 777, firstName = "Anna", lastName = "Meier" }),
            // Cancelled at the POS.
            Sale("R5", Now.AddMinutes(-10), [Line("Spritz", 1, 14.00m, 8.1m)], [OnAccount(14.00m)], accountName: "1042", cancelled: true),
        ]);

        var first = await ImportAsync(client);
        first.GetProperty("imported").GetInt32().Should().Be(5);
        first.GetProperty("posted").GetInt32().Should().Be(2);
        first.GetProperty("unmatched").GetInt32().Should().Be(1);
        first.GetProperty("skipped").GetInt32().Should().Be(2);

        var tickets = await TicketsAsync(client);
        Ticket(tickets, "R1").GetProperty("status").GetString().Should().Be("posted");
        Ticket(tickets, "R1").GetProperty("matchedBy").GetString().Should().Be("code");
        Ticket(tickets, "R2").GetProperty("status").GetString().Should().Be("posted");
        Ticket(tickets, "R3").GetProperty("status").GetString().Should().Be("skipped");
        Ticket(tickets, "R4").GetProperty("status").GetString().Should().Be("unmatched");
        Ticket(tickets, "R5").GetProperty("status").GetString().Should().Be("skipped");
        (await TicketsAsync(client, "attention")).Select(t => t.GetProperty("receiptNumber").GetString()).Should().Equal("R4");

        var charges = await Club.ChargesAsync(client, jean);
        charges.Select(c => (c.GetProperty("amount").GetDecimal(), c.GetProperty("vatRate").GetDecimal(), c.GetProperty("status").GetString()))
            .Should().BeEquivalentTo([(56.00m, 8.1m, "Pending"), (40.00m, 2.6m, "Pending"), (37.00m, 8.1m, "Paid")]);
        charges.Should().OnlyContain(c => c.GetProperty("reference").GetString()!.StartsWith('R'));
        (await Club.BalanceAsync(client, jean)).Should().Be(-96.00m, "R1 is owed; R2 was paid at the table and its tip is the staff's");
        (await Club.PointsAsync(client, jean)).Should().Be(1995, "(96 + 37) × 10 × 1.5");

        // Read again by the next import, as each one reads back a while: nothing twice.
        var again = await ImportAsync(client);
        again.GetProperty("read").GetInt32().Should().Be(5);
        again.GetProperty("imported").GetInt32().Should().Be(0);
        (await TicketsAsync(client)).Should().HaveCount(5);
        (await Club.ChargesAsync(client, jean)).Should().HaveCount(3);
        (await Club.PointsAsync(client, jean)).Should().Be(1995);
    }

    [Fact]
    public async Task ATicketSettledTwoWays_IsOwedForItsHouseAccountPart_AndATipOnTheAccountIsOwedWithIt()
    {
        using var app = new PosApp();
        var (client, _, jean) = await ConnectedClubAsync(app);
        app.Lightspeed.Sales.Add(Sale("R10", Now.AddMinutes(-5), [Line("Dinner for two", 1, 150.00m, 8.1m, discount: 15.00m, discountName: "Staff 10%")],
            [OnAccount(100.00m, tip: 10.00m), Payment("TWINT", "TWINT", 50.00m)], accountName: "1042"));

        (await ImportAsync(client)).GetProperty("posted").GetInt32().Should().Be(1);

        var sale = (await Club.Json(await client.GetAsync($"/api/sales?memberId={jean}"), "read Jean's sales")).EnumerateArray().Single();
        sale.GetProperty("source").GetString().Should().Be("lightspeed-k");
        sale.GetProperty("tender").GetString().Should().Be("Mixed");
        sale.GetProperty("total").GetDecimal().Should().Be(160.00m, "the 150 dinner and the 10 tip on the account");
        sale.GetProperty("owedAmount").GetDecimal().Should().Be(110.00m);
        sale.GetProperty("paidAmount").GetDecimal().Should().Be(50.00m);
        sale.GetProperty("discount").GetDecimal().Should().Be(15.00m);
        sale.GetProperty("discountReason").GetString().Should().Be("Given at the POS: Staff 10%");
        sale.GetProperty("pointsAwarded").GetInt32().Should().Be(2250, "points on the dinner, not the tip: 150 × 15");
        sale.GetProperty("staffName").GetString().Should().Be("Mariah");
        sale.GetProperty("tableName").GetString().Should().Be("12");
        (await Club.BalanceAsync(client, jean)).Should().Be(-110.00m);
        var payments = (await Club.Json(await client.GetAsync($"/api/payments?memberId={jean}"), "list payments")).EnumerateArray().ToList();
        payments.Single().GetProperty("method").GetString().Should().Be("Twint");
    }

    [Fact]
    public async Task CashWithChangeGivenBack_IsRecordedAsWhatTheTicketCameTo_AndCashRoundingIsEvenedOut()
    {
        using var app = new PosApp();
        var (client, _, jean) = await ConnectedClubAsync(app);
        app.Lightspeed.Sales.AddRange(
        [
            // 50 handed over for a 42 dinner, 8 given back: the POS lists the change as a negative cash payment.
            Sale("R12", Now.AddMinutes(-6), [Line("Dinner", 1, 42.00m, 8.1m)], [Payment("CASH", "Bargeld", 50.00m), Payment("CASH", "Bargeld", -8.00m)], accountName: "1042"),
            // Lines of 32.47 paid with 32.45 in cash: five-centime rounding.
            Sale("R13", Now.AddMinutes(-5), [Line("Lunch", 1, 32.47m, 8.1m)], [Payment("CASH", "Bargeld", 32.45m)], accountName: "1042"),
        ]);

        var result = await ImportAsync(client);
        result.GetProperty("posted").GetInt32().Should().Be(2);

        var payments = (await Club.Json(await client.GetAsync($"/api/payments?memberId={jean}"), "list payments")).EnumerateArray()
            .Select(p => (p.GetProperty("method").GetString(), p.GetProperty("amount").GetDecimal())).ToList();
        payments.Should().BeEquivalentTo([("Cash", 42.00m), ("Cash", 32.47m)]);
        (await Club.ChargesAsync(client, jean)).Select(c => c.GetProperty("amount").GetDecimal()).Should().BeEquivalentTo([42.00m, 32.47m]);
        (await Club.BalanceAsync(client, jean)).Should().Be(0m, "both were paid at the POS");
    }

    [Fact]
    public async Task ALineTakenBack_ComesOffWhatWasBought_NotTheTipOnTheAccount()
    {
        using var app = new PosApp();
        var (client, _, jean) = await ConnectedClubAsync(app);
        // A 100 dinner with a 20 voucher line taken off it, put on the account with a 10 tip.
        app.Lightspeed.Sales.Add(Sale("R14", Now.AddMinutes(-5), [Line("Dinner", 1, 100.00m, 8.1m), Line("Voucher", 1, -20.00m, 8.1m)],
            [OnAccount(80.00m, tip: 10.00m)], accountName: "1042"));

        (await ImportAsync(client)).GetProperty("posted").GetInt32().Should().Be(1);

        var charges = await Club.ChargesAsync(client, jean);
        charges.Select(c => (c.GetProperty("amount").GetDecimal(), c.TryGetProperty("vatRate", out var rate) ? rate.GetDecimal() : (decimal?)null))
            .Should().BeEquivalentTo([(80.00m, (decimal?)8.1m), (10.00m, (decimal?)null)],
                "the voucher comes off the dinner, as the POS charged its VAT; the tip, which carries none, stays whole");
        (await Club.BalanceAsync(client, jean)).Should().Be(-90.00m);
        (await Club.PointsAsync(client, jean)).Should().Be(1200, "80 spent on the dinner × 10 × 1.5; the tip earns none");
    }

    [Fact]
    public async Task OneImportAtATime_ASecondIsRefusedWhileTheFirstRuns()
    {
        using var app = new PosApp();
        var (client, _, _) = await ConnectedClubAsync(app);
        app.Lightspeed.Sales.Add(Sale("R20", Now.AddMinutes(-5), [Line("Negroni", 1, 18.50m, 8.1m)], [OnAccount(18.50m)], accountName: "1042"));
        app.Lightspeed.HoldSales = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var first = client.PostAsync("/api/pos/import", null);
        await app.Lightspeed.SalesAsked.Task.WaitAsync(TimeSpan.FromSeconds(30));
        var second = await Club.Refused(await client.PostAsync("/api/pos/import", null), HttpStatusCode.Conflict, "a second import while the first runs");
        second.Should().Contain("already running");

        app.Lightspeed.HoldSales.SetResult();
        (await Club.Json(await first, "the first import")).GetProperty("posted").GetInt32().Should().Be(1);
        (await ImportAsync(client)).GetProperty("imported").GetInt32().Should().Be(0, "the claim is let go when an import ends");
    }

    [Fact]
    public async Task TheScheduledImport_ImportsEveryWorkspaceThatTurnedItOn_AndRefreshesAnExpiredToken()
    {
        using var app = new PosApp();
        var (client, org, jean) = await ConnectedClubAsync(app);
        var (idle, _, _) = await Club.CreateAsync(app, "idle@example.ch", "Not connected");
        app.Lightspeed.Sales.Add(Sale("R30", Now.AddMinutes(-5), [Line("Negroni", 1, 18.50m, 8.1m)], [OnAccount(18.50m)], accountName: "1042"));
        // The access token from connecting has run out: the import asks for a new one.
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ACI.Infrastructure.Persistence.AppDbContext>();
            using var acting = db.ActFor(org, null);
            var row = db.ClubPosSettings.Single();
            row.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }

        using (var scope = app.Services.CreateScope())
            (await scope.ServiceProvider.GetRequiredService<IClubPos>().ImportDueAsync(DateTime.UtcNow)).Should().Be(1);

        var refresh = app.Lightspeed.Sent("POST", "/oauth/token").Last();
        refresh.Form["grant_type"].Should().Be("refresh_token");
        refresh.Form["refresh_token"].Should().Be("refresh-1");
        app.Lightspeed.Sent("GET", "/f/v2/business-location/").Single().Authorization.Should().Be("Bearer access-2");
        (await Club.BalanceAsync(client, jean)).Should().Be(-18.50m);
        (await TicketsAsync(idle)).Should().BeEmpty();

        // Just run, it is not due again for a few minutes.
        using (var scope = app.Services.CreateScope())
            (await scope.ServiceProvider.GetRequiredService<IClubPos>().ImportDueAsync(DateTime.UtcNow)).Should().Be(0);
        app.Lightspeed.Sent("GET", "/f/v2/business-location/").Should().HaveCount(1);
    }

    [Fact]
    public async Task TheClubIsNotTurnedOffWhileTheImportIsOn_AndAClubTurnedOffAnywayIsNotImportedUntilItIsBack()
    {
        using var app = new PosApp();
        var (client, org, jean) = await ConnectedClubAsync(app);
        app.Lightspeed.Sales.Add(Sale("R95", Now.AddMinutes(-5), [Line("Negroni", 1, 18.50m, 8.1m)], [OnAccount(18.50m)], accountName: "1042"));

        // The import posts tickets to members' house accounts: with the club's pages gone, nobody
        // could see those charges or stop it, so the club stays on until the import is turned off.
        var refused = await Club.Refused(await client.PutAsync($"/api/organizations/{org}/modules", Body(new { club = false })),
            HttpStatusCode.Conflict, "turn the club off while the import is on");
        refused.Should().Contain("import of POS tickets");

        // Turned off anyway, by two people at once (one turning the import on as the other turns
        // the club off), the scheduled import leaves the club alone and reads nothing...
        async Task SetClubOffAsync(bool off)
        {
            using var scope = app.Services.CreateScope();
            await using var db = scope.ServiceProvider.EveryWorkspace();
            db.Organizations.Single(o => o.Id == org).ClubModuleOff = off;
            await db.SaveChangesAsync();
        }
        await SetClubOffAsync(true);
        using (var scope = app.Services.CreateScope())
            (await scope.ServiceProvider.GetRequiredService<IClubPos>().ImportDueAsync(DateTime.UtcNow)).Should().Be(0);
        app.Lightspeed.Sent("GET", "/f/v2/business-location/").Should().BeEmpty("a club that is off is not read");

        // ...and takes its tickets up from where it was once the club is on again.
        await SetClubOffAsync(false);
        using (var scope = app.Services.CreateScope())
            (await scope.ServiceProvider.GetRequiredService<IClubPos>().ImportDueAsync(DateTime.UtcNow)).Should().Be(1);
        (await Club.BalanceAsync(client, jean)).Should().Be(-18.50m);

        await Club.Json(await client.PutAsync("/api/pos/settings", Body(new { enabled = false })), "turn the import off");
        await Club.Json(await client.PutAsync($"/api/organizations/{org}/modules", Body(new { club = false })), "turn the club off now");
    }

    [Fact]
    public async Task AnImportCutShort_PassesOverNoTicket_TheNextReadsAgainTheDayItDidNotFinish()
    {
        using var app = new PosApp();
        var (client, _, jean) = await ConnectedClubAsync(app);
        await Club.Json(await client.PutAsync("/api/pos/settings", Body(new { importFromUtc = Now.AddDays(-2).ToString("O") })), "import from two days ago");
        // Two days of tickets, read a ticket a page and newest first; the POS stops answering after the first page.
        app.Lightspeed.PageSize = 1;
        app.Lightspeed.NewestFirst = true;
        app.Lightspeed.Sales.AddRange(
        [
            Sale("R60", Now.AddHours(-47), [Line("Lunch", 1, 30.00m, 8.1m)], [OnAccount(30.00m)], accountName: "1042"),
            Sale("R61", Now.AddHours(-40), [Line("Dinner", 1, 60.00m, 8.1m)], [OnAccount(60.00m)], accountName: "1042"),
            Sale("R62", Now.AddHours(-2), [Line("Negroni", 1, 18.50m, 8.1m)], [OnAccount(18.50m)], accountName: "1042"),
        ]);
        app.Lightspeed.FailSalesFrom = 2;

        await Club.Refused(await client.PostAsync("/api/pos/import", null), HttpStatusCode.BadGateway, "an import the POS stops answering");

        // The next import reads again from where the last one had read in full, not from the
        // newest ticket it happened to see: the older tickets of the day it did not finish come in.
        app.Lightspeed.FailSalesFrom = null;
        await ImportAsync(client);
        var tickets = await TicketsAsync(client);
        tickets.Select(t => t.GetProperty("receiptNumber").GetString()).Should().BeEquivalentTo(["R60", "R61", "R62"]);
        tickets.Should().OnlyContain(t => t.GetProperty("status").GetString() == "posted");
        (await Club.BalanceAsync(client, jean)).Should().Be(-108.50m);
    }

    [Fact]
    public async Task ImportingBeforeADateToImportFromIsSet_BillsNothingClosedBeforeTheFirstImport()
    {
        using var app = new PosApp();
        var (client, _, org) = await Club.CreateAsync(app);
        var jean = await Club.MemberAsync(client, "Jean", "Gold");
        await Club.Json(await client.PostAsync($"/api/members/{jean}/codes", Body(new { code = "1042" })), "give Jean the code 1042");
        await ConnectAsync(app, client);
        // The club connected yesterday and has rung its bills up by hand since, as the import is not on yet.
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ACI.Infrastructure.Persistence.AppDbContext>();
            using var acting = db.ActFor(org, null);
            db.ClubPosSettings.Single().ConnectedAtUtc = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();
        }
        await Club.Json(await client.PutAsync("/api/pos/settings", Body(new { locationId = "46100001" })), "choose the location, the import still off");
        app.Lightspeed.Sales.Add(Sale("R140", Now.AddHours(-2), [Line("Dinner", 1, 60.00m, 8.1m)], [OnAccount(60.00m)], accountName: "1042"));

        // "Import now", to see that it works: what was closed before it is not billed a second time.
        (await ImportAsync(client)).GetProperty("imported").GetInt32().Should().Be(0);
        (await Club.BalanceAsync(client, jean)).Should().Be(0m);
        var settings = await Club.Json(await client.GetAsync("/api/pos/settings"), "read the settings");
        settings.GetProperty("importFromUtc").GetDateTime().Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1),
            "the first import says from when it imports, as turning the import on does");

        // What is closed after it comes in.
        app.Lightspeed.Sales.Add(Sale("R141", DateTime.UtcNow, [Line("Negroni", 1, 18.50m, 8.1m)], [OnAccount(18.50m)], accountName: "1042"));
        await Task.Delay(1100);
        await ImportAsync(client);
        (await TicketsAsync(client)).Select(t => t.GetProperty("receiptNumber").GetString()).Should().Equal("R141");
        (await Club.BalanceAsync(client, jean)).Should().Be(-18.50m);
    }

    [Fact]
    public async Task AnotherLocationChosen_IsReadFromTheDateToImportFrom()
    {
        using var app = new PosApp();
        var (client, _, jean) = await ConnectedClubAsync(app);
        await Club.Json(await client.PutAsync("/api/pos/settings", Body(new { importFromUtc = Now.AddDays(-2).ToString("O") })), "import from two days ago");
        // The wrong location was chosen first: the import reads it, finds nothing, and has read up to now.
        await ImportAsync(client);

        // The right one put a dinner on Jean's account yesterday.
        app.Lightspeed.Sales.Add(Sale("R110", Now.AddHours(-30), [Line("Dinner", 1, 60.00m, 8.1m)], [OnAccount(60.00m)], accountName: "1042"));
        await Club.Json(await client.PutAsync("/api/pos/settings", Body(new { locationId = "46100002", locationName = "Pavillon 46 Bar" })),
            "choose the right location");
        await ImportAsync(client);

        var ticket = Ticket(await TicketsAsync(client), "R110");
        ticket.GetProperty("externalId").GetString().Should().Be("46100002:R110");
        ticket.GetProperty("status").GetString().Should().Be("posted");
        (await Club.BalanceAsync(client, jean)).Should().Be(-60.00m);
    }

    [Fact]
    public async Task AnEarlierDateToImportFrom_BringsInWhatWasClosedSinceThen()
    {
        using var app = new PosApp();
        var (client, _, jean) = await ConnectedClubAsync(app);
        app.Lightspeed.Sales.Add(Sale("R120", Now.AddHours(-30), [Line("Dinner", 1, 60.00m, 8.1m)], [OnAccount(60.00m)], accountName: "1042"));
        await ImportAsync(client);
        (await TicketsAsync(client)).Should().BeEmpty("it was closed before the date to import from");

        await Club.Json(await client.PutAsync("/api/pos/settings", Body(new { importFromUtc = Now.AddDays(-2).ToString("O") })), "import from two days ago");
        await ImportAsync(client);

        Ticket(await TicketsAsync(client), "R120").GetProperty("status").GetString().Should().Be("posted");
        (await Club.BalanceAsync(client, jean)).Should().Be(-60.00m);
    }

    [Fact]
    public async Task ALocationChosenWhileAnImportRuns_IsStillReadFromTheDateToImportFrom()
    {
        using var app = new PosApp();
        var (client, _, _) = await ConnectedClubAsync(app);
        await Club.Json(await client.PutAsync("/api/pos/settings", Body(new { importFromUtc = Now.AddDays(-2).ToString("O") })), "import from two days ago");
        await ImportAsync(client);

        // An import is under way, reading the old location, when someone chooses another.
        var asked = app.Lightspeed.Sent("GET", "/f/v2/business-location/").Count;
        app.Lightspeed.HoldSales = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = client.PostAsync("/api/pos/import", null);
        for (var i = 0; i < 300 && app.Lightspeed.Sent("GET", "/f/v2/business-location/").Count == asked; i++) await Task.Delay(100);
        app.Lightspeed.Sent("GET", "/f/v2/business-location/").Count.Should().BeGreaterThan(asked, "the import is reading the old location");
        await Club.Json(await client.PutAsync("/api/pos/settings", Body(new { locationId = "46100002" })), "choose another location while it runs");
        app.Lightspeed.Sales.Add(Sale("R130", Now.AddHours(-30), [Line("Dinner", 1, 60.00m, 8.1m)], [OnAccount(60.00m)], accountName: "1042"));
        app.Lightspeed.HoldSales.SetResult();
        await Club.Json(await running, "the import that was under way");

        // How far that run read the old location says nothing of the new one.
        await ImportAsync(client);
        Ticket(await TicketsAsync(client), "R130").GetProperty("externalId").GetString().Should().Be("46100002:R130");
    }

    [Fact]
    public async Task ANewTicketIsPostedFirst_HoweverManyWaitForStaff()
    {
        using var app = new PosApp();
        var (client, org, jean) = await ConnectedClubAsync(app);
        // A thousand tickets from yesterday wait for staff: customers the POS knows, whom no member's code names.
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ACI.Infrastructure.Persistence.AppDbContext>();
            using var acting = db.ActFor(org, null);
            var data = JsonSerializer.Serialize(new PosTicketData("46100001:W", "W", "46100001", Now.AddDays(-1), "CHF", false,
                [new PosTicketLine("Coffee", null, 1, 5.00m, 8.1m, 0, null)], [], null, null, "c-9", "A guest", null, []),
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
            for (var i = 0; i < 1000; i++)
                db.ClubPosTickets.Add(new ClubPosTicket
                {
                    Id = Guid.NewGuid(), OrganizationId = org, Provider = PosProviders.LightspeedK, ExternalId = $"46100001:W{i}", ReceiptNumber = $"W{i}",
                    ClosedAtUtc = Now.AddDays(-1).AddSeconds(i), Currency = "CHF", Total = 5.00m, CustomerId = "c-9", CustomerName = "A guest", DataJson = data,
                    Status = PosTicketStatuses.Unmatched, Attempts = 1, ImportedAtUtc = Now.AddDays(-1),
                    Note = "The POS put a customer on it that no member's code names. Say whose it is, or set it aside.",
                });
            await db.SaveChangesAsync();
        }
        app.Lightspeed.Sales.Add(Sale("R150", Now.AddMinutes(-5), [Line("Negroni", 1, 18.50m, 8.1m)], [OnAccount(18.50m)], accountName: "1042"));

        (await ImportAsync(client)).GetProperty("posted").GetInt32().Should().Be(1);
        (await Club.BalanceAsync(client, jean)).Should().Be(-18.50m, "a new ticket is worked out before the ones already waiting are tried again");
    }

    [Fact]
    public async Task AScheduledImportLateInItsRound_StillHoldsTheWorkspaceForAWholeLease()
    {
        using var app = new PosApp();
        var (client, _, _) = await ConnectedClubAsync(app);
        app.Lightspeed.HoldSales = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // The round of scheduled imports began twenty minutes ago (the workspaces before this one
        // took that long), and only now reaches this one.
        using var scope = app.Services.CreateScope();
        var scheduled = scope.ServiceProvider.GetRequiredService<IClubPos>().ImportDueAsync(DateTime.UtcNow.AddMinutes(-20));
        try
        {
            await app.Lightspeed.SalesAsked.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var now = await client.PostAsync("/api/pos/import", null).WaitAsync(TimeSpan.FromSeconds(20));
            now.StatusCode.Should().Be(HttpStatusCode.Conflict, "the scheduled import holds the workspace until it is done");
        }
        finally
        {
            app.Lightspeed.HoldSales.SetResult();
        }
        (await scheduled).Should().Be(0);
    }

    // ── staff deal with what waits ───────────────────────────────────────────

    [Fact]
    public async Task ATicketWaitingForStaff_KeepsItsVersionThroughImports_SoAMatchFromAPageReadBeforeThemGoesThrough()
    {
        using var app = new PosApp();
        var (client, _, _) = await ConnectedClubAsync(app);
        var anna = await Club.MemberAsync(client, "Anna", "Silver");
        app.Lightspeed.Sales.Add(Sale("R42", Now.AddMinutes(-20), [Line("Club sandwich", 1, 28.00m, 8.1m)], [OnAccount(28.00m)]));
        await ImportAsync(client);
        var read = Ticket(await TicketsAsync(client), "R42");

        // Every import tries the waiting tickets again; this one comes out as it was, so nobody
        // dealt with it and its version stands.
        await ImportAsync(client);
        await ImportAsync(client);
        Ticket(await TicketsAsync(client), "R42").GetProperty("version").GetGuid().Should().Be(read.GetProperty("version").GetGuid());

        var matched = await Club.Json(await client.PostAsync($"/api/pos/tickets/{read.GetProperty("id").GetGuid()}/match",
            Body(new { memberId = anna, version = read.GetProperty("version").GetGuid() })), "match it with the version read before the imports");
        matched.GetProperty("status").GetString().Should().Be("posted");
        (await Club.BalanceAsync(client, anna)).Should().Be(-28.00m);
    }

    [Fact]
    public async Task ATicketPartlyOnTheHouseAccount_IsNotSetAsideForAMethodThatIsNotAMemberSale()
    {
        using var app = new PosApp();
        var (client, _, jean) = await ConnectedClubAsync(app);
        await Club.Json(await client.PutAsync("/api/pos/settings", Body(new { methodMap = new Dictionary<string, string> { ["STAFF"] = "Skip" } })),
            "say staff meals are not members' sales");
        app.Lightspeed.Sales.AddRange(
        [
            // A staff meal: not a member's sale.
            Sale("R70", Now.AddMinutes(-5), [Line("Staff lunch", 1, 15.00m, 8.1m)], [Payment("STAFF", "Staff meal", 15.00m)]),
            // Half on Jean's account, half on the staff meal method: Jean owes his half, so staff decide.
            Sale("R71", Now.AddMinutes(-4), [Line("Dinner", 1, 80.00m, 8.1m)], [OnAccount(40.00m), Payment("STAFF", "Staff meal", 40.00m)],
                accountName: "1042"),
        ]);

        await ImportAsync(client);
        var tickets = await TicketsAsync(client);
        Ticket(tickets, "R70").GetProperty("status").GetString().Should().Be("skipped");
        var mixed = Ticket(tickets, "R71");
        mixed.GetProperty("status").GetString().Should().Be("failed", "part of it is owed on a house account, which nobody decided to drop");
        mixed.GetProperty("note").GetString().Should().Contain("Staff meal");
        (await TicketsAsync(client, "attention")).Select(t => t.GetProperty("receiptNumber").GetString()).Should().Equal("R71");
        (await Club.ChargesAsync(client, jean)).Should().BeEmpty("nothing is posted until staff decide");
    }

    [Fact]
    public async Task ATicketTheLedgerPostedBeforeItCouldSaySo_IsMarkedPosted_AndIsNeitherSetAsideNorPostedAgain()
    {
        using var app = new PosApp();
        var (client, org, jean) = await ConnectedClubAsync(app);
        app.Lightspeed.Sales.Add(Sale("R80", Now.AddMinutes(-5), [Line("Lunch", 1, 30.00m, 8.1m)], [OnAccount(30.00m)], accountName: "STRANGER"));
        await ImportAsync(client);
        var waiting = Ticket(await TicketsAsync(client), "R80");
        waiting.GetProperty("status").GetString().Should().Be("unmatched");
        var ticketId = waiting.GetProperty("id").GetGuid();

        // The ledger posts the ticket's sale to Jean, and the run stops before the ticket says so.
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ACI.Infrastructure.Persistence.AppDbContext>();
            using var acting = db.ActFor(org, null);
            var posted = await scope.ServiceProvider.GetRequiredService<IClubLedger>().PostSaleAsync(org, new SaleRequest
            {
                MemberId = jean, Venue = "Dining", Tender = "HouseAccount", AllowLinesOutsideCatalog = true, Source = PosProviders.LightspeedK,
                PosTicketId = ticketId, Currency = "CHF", Lines = [new SaleLineRequest { Name = "Lunch", UnitPrice = 30.00m, VatRate = 8.1m }],
            });
            posted.IsSuccess.Should().BeTrue();
        }

        // Setting it aside now would leave it saying "set aside" while Jean owes for it.
        var refused = await Club.Refused(await client.PostAsync($"/api/pos/tickets/{ticketId}/ignore",
            Body(new { reason = "Not a member", version = waiting.GetProperty("version").GetGuid() })), HttpStatusCode.Conflict, "set aside a ticket the ledger posted");
        refused.Should().Contain("Jean Dupont");
        var now = Ticket(await TicketsAsync(client), "R80");
        now.GetProperty("status").GetString().Should().Be("posted");
        now.GetProperty("memberName").GetString().Should().Be("Jean Dupont");
        await Club.Refused(await client.PostAsync($"/api/pos/tickets/{ticketId}/match", Body(new { memberId = jean })), HttpStatusCode.Conflict, "match it again");
        await ImportAsync(client);
        (await Club.ChargesAsync(client, jean)).Should().HaveCount(1);
        (await Club.BalanceAsync(client, jean)).Should().Be(-30.00m);
    }

    [Fact]
    public async Task StaffMatchATicketToAMember_WhoseNextTicketsFindThemByTheCustomer()
    {
        using var app = new PosApp();
        var (client, _, jean) = await ConnectedClubAsync(app);
        var anna = await Club.MemberAsync(client, "Anna", "Silver");
        app.Lightspeed.Sales.Add(Sale("R40", Now.AddMinutes(-20), [Line("Club sandwich", 1, 28.00m, 8.1m)], [OnAccount(28.00m)],
            consumer: new { id = 777, firstName = "Anna", lastName = "Meier" }));
        await ImportAsync(client);
        var waiting = Ticket(await TicketsAsync(client), "R40");

        var matched = await Club.Json(await client.PostAsync($"/api/pos/tickets/{waiting.GetProperty("id").GetGuid()}/match",
            Body(new { memberId = anna, remember = true, version = waiting.GetProperty("version").GetGuid() })), "match the ticket to Anna");
        matched.GetProperty("status").GetString().Should().Be("posted");
        matched.GetProperty("matchedBy").GetString().Should().Be("staff");
        matched.GetProperty("memberName").GetString().Should().Be("Anna Dupont");
        (await Club.BalanceAsync(client, anna)).Should().Be(-28.00m);
        var codes = (await Club.Json(await client.GetAsync($"/api/members/{anna}/codes"), "Anna's codes")).EnumerateArray().ToList();
        codes.Single().GetProperty("kind").GetString().Should().Be("pos-customer");
        codes.Single().GetProperty("code").GetString().Should().Be("777");

        // Matched once, posted once: a ticket on someone's account is not matched again.
        await Club.Refused(await client.PostAsync($"/api/pos/tickets/{waiting.GetProperty("id").GetGuid()}/match", Body(new { memberId = jean })),
            HttpStatusCode.Conflict, "matching a posted ticket again");
        await Club.Refused(await client.PostAsync($"/api/pos/tickets/{waiting.GetProperty("id").GetGuid()}/ignore", Body(new { reason = "test" })),
            HttpStatusCode.Conflict, "setting aside a posted ticket");

        app.Lightspeed.Sales.Add(Sale("R41", Now.AddMinutes(-2), [Line("Spritz", 1, 14.00m, 8.1m)], [OnAccount(14.00m)],
            consumer: new { id = 777, firstName = "Anna", lastName = "Meier" }));
        await ImportAsync(client);
        var next = Ticket(await TicketsAsync(client), "R41");
        next.GetProperty("status").GetString().Should().Be("posted");
        next.GetProperty("matchedBy").GetString().Should().Be("customer");
        (await Club.BalanceAsync(client, anna)).Should().Be(-42.00m);
    }

    [Fact]
    public async Task AnUnknownPaymentMethodFails_UntilTheSettingsSayWhatItIs_ThenARetryPostsIt()
    {
        using var app = new PosApp();
        var (client, _, jean) = await ConnectedClubAsync(app);
        app.Lightspeed.Sales.Add(Sale("R50", Now.AddMinutes(-5), [Line("Negroni", 1, 18.50m, 8.1m)], [Payment("RCH", "Rechnung", 18.50m)], accountName: "1042"));
        await ImportAsync(client);
        var failed = Ticket(await TicketsAsync(client), "R50");
        failed.GetProperty("status").GetString().Should().Be("failed");
        failed.GetProperty("note").GetString().Should().Contain("'Rechnung'");
        failed.GetProperty("memberName").GetString().Should().Be("Jean Dupont");
        (await Club.ChargesAsync(client, jean)).Should().BeEmpty();

        await Club.Json(await client.PutAsync("/api/pos/settings", Body(new { methodMap = new Dictionary<string, string> { ["RCH"] = "HouseAccount" } })),
            "say Rechnung puts the bill on the house account");
        var retried = await Club.Json(await client.PostAsync($"/api/pos/tickets/{failed.GetProperty("id").GetGuid()}/retry", null), "try the ticket again");
        retried.GetProperty("status").GetString().Should().Be("posted");
        retried.GetProperty("attempts").GetInt32().Should().Be(2);
        (await Club.BalanceAsync(client, jean)).Should().Be(-18.50m);
    }

    [Fact]
    public async Task SettingATicketAsideNeedsAReason_AndKeepsItFromBeingPostedByTheNextImport()
    {
        using var app = new PosApp();
        var (client, _, _) = await ConnectedClubAsync(app);
        app.Lightspeed.Sales.Add(Sale("R60", Now.AddMinutes(-5), [Line("Staff lunch", 1, 18.00m, 8.1m)], [OnAccount(18.00m)]));
        await ImportAsync(client);
        var id = Ticket(await TicketsAsync(client), "R60").GetProperty("id").GetGuid();

        await Club.Refused(await client.PostAsync($"/api/pos/tickets/{id}/ignore", Body(new { reason = " " })), HttpStatusCode.BadRequest, "no reason");
        var ignored = await Club.Json(await client.PostAsync($"/api/pos/tickets/{id}/ignore", Body(new { reason = "The chef's own lunch" })), "set it aside");
        ignored.GetProperty("status").GetString().Should().Be("ignored");
        ignored.GetProperty("note").GetString().Should().Be("The chef's own lunch");
        (await TicketsAsync(client, "attention")).Should().BeEmpty();
        await ImportAsync(client);
        Ticket(await TicketsAsync(client), "R60").GetProperty("status").GetString().Should().Be("ignored");
    }

    [Fact]
    public async Task ATicketSetAsideBeforeAnImportGotToIt_StaysSetAside()
    {
        using var app = new PosApp();
        var (client, _, jean) = await ConnectedClubAsync(app);
        // Two tickets, a page each; Lightspeed stops answering after the first page, so the run has
        // saved the first ticket and stops before working out whose it is.
        app.Lightspeed.PageSize = 1;
        app.Lightspeed.Sales.AddRange(
        [
            Sale("R100", Now.AddMinutes(-30), [Line("Staff lunch", 1, 30.00m, 8.1m)], [OnAccount(30.00m)], accountName: "1042"),
            Sale("R101", Now.AddMinutes(-20), [Line("Coffee", 1, 5.00m, 8.1m)], [OnAccount(5.00m)], accountName: "1042"),
        ]);
        app.Lightspeed.FailSalesFrom = 2;
        await Club.Refused(await client.PostAsync("/api/pos/import", null), HttpStatusCode.BadGateway, "an import the POS stops answering");
        var waiting = Ticket(await TicketsAsync(client), "R100");
        waiting.GetProperty("attempts").GetInt32().Should().Be(0, "the run stopped before it got to the ticket");

        // Staff know it is the chef's lunch, put on Jean's account by mistake, and set it aside.
        await Club.Json(await client.PostAsync($"/api/pos/tickets/{waiting.GetProperty("id").GetGuid()}/ignore",
            Body(new { reason = "The chef's own lunch", version = waiting.GetProperty("version").GetGuid() })), "set it aside");

        app.Lightspeed.FailSalesFrom = null;
        await ImportAsync(client);
        var tickets = await TicketsAsync(client);
        Ticket(tickets, "R100").GetProperty("status").GetString().Should().Be("ignored", "staff set it aside, and no import overrules them");
        Ticket(tickets, "R101").GetProperty("status").GetString().Should().Be("posted");
        (await Club.BalanceAsync(client, jean)).Should().Be(-5.00m, "only the coffee is Jean's");
    }

    [Fact]
    public async Task OnlySomeoneWhoTakesPaymentsSetsUpThePosAndDealsWithItsTickets()
    {
        using var app = new PosApp();
        var (owner, org, jean) = await ConnectedClubAsync(app);
        app.Lightspeed.Sales.Add(Sale("R70", Now.AddMinutes(-5), [Line("Club sandwich", 1, 28.00m, 8.1m)], [OnAccount(28.00m)]));
        await ImportAsync(owner);
        var id = Ticket(await TicketsAsync(owner), "R70").GetProperty("id").GetGuid();
        var desk = await Club.JoinAsync(app, owner, org, "desk@example.ch", "front_desk");

        await Club.Refused(await desk.GetAsync("/api/pos/settings"), HttpStatusCode.Forbidden, "the front desk reads the settings");
        await Club.Refused(await desk.PutAsync("/api/pos/settings", Body(new { enabled = false })), HttpStatusCode.Forbidden, "the front desk changes the settings");
        await Club.Refused(await desk.PostAsync("/api/pos/import", null), HttpStatusCode.Forbidden, "the front desk imports");
        await Club.Refused(await desk.PostAsync($"/api/pos/tickets/{id}/match", Body(new { memberId = jean })), HttpStatusCode.Forbidden, "the front desk matches");
        await Club.Refused(await desk.PostAsync("/api/pos/connect", null), HttpStatusCode.Forbidden, "the front desk connects");
        (await Club.Json(await desk.GetAsync("/api/pos/status"), "the front desk reads the status")).GetProperty("needsAttention").GetInt32().Should().Be(1);
        (await TicketsAsync(desk, "attention")).Should().HaveCount(1);
    }

    [Fact]
    public async Task AnotherWorkspaceSeesNoneOfTheTickets_NorTheConnection_AndCannotMatchThem()
    {
        using var app = new PosApp();
        var (client, _, jean) = await ConnectedClubAsync(app);
        app.Lightspeed.Sales.Add(Sale("R80", Now.AddMinutes(-5), [Line("Club sandwich", 1, 28.00m, 8.1m)], [OnAccount(28.00m)]));
        await ImportAsync(client);
        var id = Ticket(await TicketsAsync(client), "R80").GetProperty("id").GetGuid();
        var (other, _, _) = await Club.CreateAsync(app, "other@example.ch", "Other club");
        var theirs = await Club.MemberAsync(other, "Paul", "Gold");

        (await TicketsAsync(other)).Should().BeEmpty();
        (await Club.Json(await other.GetAsync("/api/pos/settings"), "their settings")).GetProperty("connected").GetBoolean().Should().BeFalse();
        await Club.Refused(await other.PostAsync($"/api/pos/tickets/{id}/match", Body(new { memberId = theirs })), HttpStatusCode.NotFound, "matching another workspace's ticket");
        await Club.Refused(await other.PostAsync($"/api/pos/tickets/{id}/retry", null), HttpStatusCode.NotFound, "retrying another workspace's ticket");
        await Club.Refused(await client.PostAsync($"/api/pos/tickets/{id}/match", Body(new { memberId = theirs })), HttpStatusCode.NotFound,
            "matching a ticket to another workspace's member");
        Ticket(await TicketsAsync(client), "R80").GetProperty("status").GetString().Should().Be("unmatched");
        await Club.Refused(await other.GetAsync($"/api/members/{jean}/codes"), HttpStatusCode.NotFound, "another workspace's member's codes");
    }

    // ── merging and deleting members ─────────────────────────────────────────

    [Fact]
    public async Task AMemberMerge_MovesTheDuplicatesCodesSalesAndTicketsToTheMemberKept()
    {
        using var app = new PosApp();
        var (client, _, jean) = await ConnectedClubAsync(app);
        var duplicate = await Club.MemberAsync(client, "Jeannot", "Gold");
        await Club.Json(await client.PostAsync($"/api/members/{duplicate}/codes", Body(new { code = "2042" })), "the duplicate's code");
        await Club.Json(await client.PostAsync($"/api/members/{duplicate}/codes", Body(new { code = "1042-B" })), "another of the duplicate's codes");
        app.Lightspeed.Sales.Add(Sale("R90", Now.AddMinutes(-5), [Line("Negroni", 1, 18.50m, 8.1m)], [OnAccount(18.50m)], accountName: "2042"));
        await ImportAsync(client);
        Ticket(await TicketsAsync(client), "R90").GetProperty("memberId").GetGuid().Should().Be(duplicate);

        (await client.PostAsync($"/api/members/{jean}/merge", Body(new { otherMemberId = duplicate }))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var codes = (await Club.Json(await client.GetAsync($"/api/members/{jean}/codes"), "Jean's codes")).EnumerateArray()
            .Select(c => c.GetProperty("code").GetString()).ToList();
        codes.Should().BeEquivalentTo(["1042", "2042", "1042-B"]);
        var ticket = Ticket(await TicketsAsync(client), "R90");
        ticket.GetProperty("memberId").GetGuid().Should().Be(jean);
        ticket.GetProperty("memberName").GetString().Should().Be("Jean Dupont");
        (await Club.Json(await client.GetAsync($"/api/sales?memberId={jean}"), "Jean's sales")).GetArrayLength().Should().Be(1);

        // A card of the duplicate's scanned later finds the member kept.
        app.Lightspeed.Sales.Add(Sale("R91", Now.AddMinutes(-1), [Line("Spritz", 1, 14.00m, 8.1m)], [OnAccount(14.00m)], accountName: "2042"));
        await ImportAsync(client);
        Ticket(await TicketsAsync(client), "R91").GetProperty("memberId").GetGuid().Should().Be(jean);
        (await Club.BalanceAsync(client, jean)).Should().Be(-32.50m);
    }

    [Fact]
    public async Task ADeletedMembersCodeNamesNobody_SoTheirNextTicketWaitsForStaff()
    {
        using var app = new PosApp();
        var (client, _, _) = await ConnectedClubAsync(app);
        var leaving = await Club.MemberAsync(client, "Paul", "Silver");
        await Club.Json(await client.PostAsync($"/api/members/{leaving}/codes", Body(new { code = "3042" })), "Paul's code");

        (await client.DeleteAsync($"/api/members/{leaving}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        app.Lightspeed.Sales.Add(Sale("R95", Now.AddMinutes(-1), [Line("Spritz", 1, 14.00m, 8.1m)], [OnAccount(14.00m)], accountName: "3042"));
        await ImportAsync(client);

        Ticket(await TicketsAsync(client), "R95").GetProperty("status").GetString().Should().Be("unmatched");
        // The code is free for someone else now.
        var anna = await Club.MemberAsync(client, "Anna", "Gold");
        await Club.Json(await client.PostAsync($"/api/members/{anna}/codes", Body(new { code = "3042" })), "Paul's old code for Anna");
    }

    // ── members' codes ───────────────────────────────────────────────────────

    [Fact]
    public async Task AMemberCodeNamesOneMember_ANewOneHasACheckDigit_AndCarriesItsQrCode()
    {
        using var app = new PosApp();
        var (client, _, org) = await Club.CreateAsync(app);
        var jean = await Club.MemberAsync(client, "Jean");
        var anna = await Club.MemberAsync(client, "Anna");

        var typed = await Club.Json(await client.PostAsync($"/api/members/{jean}/codes", Body(new { code = " m-1042 " })), "give Jean his card's code");
        typed.GetProperty("code").GetString().Should().Be("M-1042");
        typed.GetProperty("qrRows").GetArrayLength().Should().Be(21, "a short code fits the smallest QR code, 21 modules a side");
        (await Club.Json(await client.PostAsync($"/api/members/{jean}/codes", Body(new { code = "M-1042" })), "the same code again"))
            .GetProperty("id").GetGuid().Should().Be(typed.GetProperty("id").GetGuid());
        var taken = await Club.Refused(await client.PostAsync($"/api/members/{anna}/codes", Body(new { code = "m-1042" })), HttpStatusCode.Conflict,
            "Jean's code for Anna");
        taken.Should().Contain("Jean Dupont");
        await Club.Refused(await client.PostAsync($"/api/members/{anna}/codes", Body(new { code = "a" })), HttpStatusCode.BadRequest, "a code too short");

        var made = await Club.Json(await client.PostAsync($"/api/members/{anna}/codes", null), "make Anna a code");
        var digits = made.GetProperty("code").GetString()!;
        digits.Should().MatchRegex("^[0-9]{9}$");
        ACI.Infrastructure.Services.Pos.ClubPosService.Luhn(digits[..8]).Should().Be(digits);

        var desk = await Club.JoinAsync(app, client, org, "desk@example.ch", "front_desk");
        await Club.Refused(await desk.PostAsync($"/api/members/{anna}/codes", Body(new { code = "5555" })), HttpStatusCode.Forbidden, "the front desk gives codes");
        (await Club.Json(await desk.GetAsync($"/api/members/{anna}/codes"), "the front desk reads Anna's codes")).GetArrayLength().Should().Be(1);
        (await client.DeleteAsync($"/api/members/{jean}/codes/{typed.GetProperty("id").GetGuid()}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await Club.Json(await client.GetAsync($"/api/members/{jean}/codes"), "Jean's codes")).GetArrayLength().Should().Be(0);
    }
}
