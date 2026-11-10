using System.Net;
using System.Text.Json;
using ACI.Infrastructure.Services.Payments;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ACI.WebApi.Tests;

/// <summary>
/// Payments from outside on a real SQL Server: the unique keys that make a bank transfer
/// recorded once and a statement's reference issued once, with two requests racing for them.
/// </summary>
/// <remarks>
/// The in-memory provider enforces no unique index; only a real server shows that the second
/// of two imports of one file, or of two prints of one statement, finds the first's row
/// rather than making its own. The gate holds both requests at the point they have checked
/// and not yet written, every time.
/// </remarks>
public class PaymentsOnSqlServerTests
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(HttpClient Client, Guid Org)> ClubAsync(SqlServerApp app)
    {
        var (client, _) = await Workspaces.SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
        var org = (await Ok(await client.PostAsync("/api/organizations", Workspaces.Body(new { name = "Pavillon46" })), "create the club"))
            .GetProperty("id").GetGuid();
        client.In(org);
        await Ok(await client.PutAsync("/api/payment-settings", Workspaces.Body(new
        {
            iban = "CH5800791123000889012", qrIban = "CH4431999123000889012",
            creditorName = "Pavillon 46", creditorPostalCode = "2501", creditorTown = "Biel", creditorCountry = "CH",
        })), "set the bank");
        return (client, org);
    }

    private static async Task<Guid> OwingMemberAsync(HttpClient client, string first)
    {
        var id = (await Ok(await client.PostAsync("/api/members", Workspaces.Body(new
        {
            firstName = first, lastName = "Dupont", email = $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
        })), $"add {first}")).GetProperty("id").GetGuid();
        await Ok(await client.PostAsync("/api/charges", Workspaces.Body(new { memberId = id, kind = "Dues", description = "Gold dues", amount = 299 })), $"charge {first}");
        return id;
    }

    private static Task<HttpResponseMessage> StatementAsync(HttpClient client, Guid memberId)
    {
        // One fixed period, so two prints are of the same statement.
        var from = Uri.EscapeDataString(DateTime.UtcNow.Date.AddDays(-7).ToString("o"));
        var to = Uri.EscapeDataString(DateTime.UtcNow.Date.AddDays(7).ToString("o"));
        return client.GetAsync($"/api/members/{memberId}/statement.pdf?from={from}&to={to}");
    }

    [SqlServerFact]
    public async Task TwoImportsOfOneBankFileAtOnce_RecordTheTransferOnce()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            var gate = new RaceFactory.ReadGate();
            using var app = SqlServerApp.Start(database, services: services => services.AddSingleton<ACI.Application.Services.ILedgerReadObserver>(gate));
            var (client, org) = await ClubAsync(app);
            var memberId = await OwingMemberAsync(client, "Jean");
            (await StatementAsync(client, memberId)).StatusCode.Should().Be(HttpStatusCode.OK);
            var reference = (string)(await SqlServerApp.ScalarAsync(database, $"SELECT [QrReference] FROM [ClubPaymentReferences] WHERE [MemberId] = '{memberId}'"))!;
            var file = BankFileTests.Camt054(("TX-1", 299m, reference));
            gate.Arm("bank-files");

            var answers = await Task.WhenAll(BankFileTests.ImportAsync(client, file), BankFileTests.ImportAsync(client, file));

            var imports = new List<JsonElement>();
            foreach (var answer in answers) imports.Add(await Ok(answer, "an import"));
            imports.Sum(i => i.GetProperty("matched").GetInt32()).Should().Be(1, "one of the two records the transfer");
            imports.Sum(i => i.GetProperty("alreadyImported").GetInt32()).Should().Be(1, "the other finds it recorded");
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubBankTransactions] WHERE [OrganizationId] = '{org}'")).Should().Be(1);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubPayments] WHERE [MemberId] = '{memberId}' AND [Provider] = N'bank'")).Should().Be(1);
            (await SqlServerApp.ScalarAsync(database, $"SELECT [HouseAccountBalance] FROM [ClubMembers] WHERE [Id] = '{memberId}'")).Should().Be(0m);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task AMemberWithManyOpenChargesPaysTheirAccountOnline_AndHasTheCardChargedForThem()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            var stripe = new FakeStripe().AcceptingKeys();
            stripe.On("POST", "/v1/customers", new { id = "cus_many", @object = "customer" });
            stripe.On("POST", "/v1/checkout/sessions", r => (HttpStatusCode.OK, new
            {
                id = "cs_many", @object = "checkout.session", mode = "payment", url = "https://checkout.stripe.com/c/pay/cs_many", status = "open",
                expires_at = long.Parse(r.Form["expires_at"]), metadata = new Dictionary<string, string>(),
            }));
            stripe.On("POST", "/v1/payment_intents", r => (HttpStatusCode.OK, new
            {
                id = "pi_many", @object = "payment_intent", status = "succeeded", amount = long.Parse(r.Form["amount"]),
                amount_received = long.Parse(r.Form["amount"]), currency = "chf", payment_method_types = new[] { "card" }, metadata = new Dictionary<string, string>(),
            }));
            using var app = SqlServerApp.Start(database, services: services =>
                services.AddHttpClient(StripePaymentProvider.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => stripe));
            var (client, _) = await ClubAsync(app);
            await Ok(await client.PutAsync("/api/payment-settings", Workspaces.Body(new
            {
                stripeSecretKey = "sk_test_good_4242424242abcd", stripeWebhookSecret = "whsec_test_signing_secret",
                iban = "CH5800791123000889012", qrIban = "CH4431999123000889012",
                creditorName = "Pavillon 46", creditorPostalCode = "2501", creditorTown = "Biel", creditorCountry = "CH",
            })), "set Stripe up");

            // A season of bar tabs: more open charges than a list of their ids fits in.
            var memberId = (await Ok(await client.PostAsync("/api/members", Workspaces.Body(new
            {
                firstName = "Jean", lastName = "Dupont", email = "jean@example.ch", tier = "Gold", status = "Active",
            })), "add Jean")).GetProperty("id").GetGuid();
            for (var i = 0; i < 150; i++)
                await Ok(await client.PostAsync("/api/charges", Workspaces.Body(new { memberId, kind = "Bar", description = $"Bar tab {i + 1}", amount = 2 })), "a bar tab");

            var link = await Ok(await client.PostAsync($"/api/members/{memberId}/pay-link", Workspaces.Body(new { purpose = "account" })), "a link for what is owed");
            var url = link.GetProperty("url").GetString()!;
            var token = url[(url.IndexOf("/pay/", StringComparison.Ordinal) + 5)..];
            var pay = new HttpRequestMessage(HttpMethod.Post, "/api/public/pay/checkout");
            pay.Headers.Add("X-Pay-Token", token);
            await Ok(await app.CreateClient().SendAsync(pay), "the member presses Pay");

            // Pressing Pay made the member's customer at Stripe; they save a card on it.
            await SqlServerApp.ExecuteAsync(database, $@"UPDATE [ClubPaymentProfiles] SET [PaymentMethodId] = N'pm_many', [PaymentMethodType] = N'card',
                [Brand] = N'visa', [Last4] = N'4242', [SavedAtUtc] = SYSUTCDATETIME() WHERE [MemberId] = '{memberId}' AND [CustomerId] = N'cus_many'");
            var charged = await Ok(await client.PostAsync($"/api/members/{memberId}/card/charge", Workspaces.Body(new { })), "charge the card for everything open");
            charged.GetProperty("status").GetString().Should().Be("succeeded");
            charged.GetProperty("amount").GetDecimal().Should().Be(300m);
            (await SqlServerApp.ScalarAsync(database, $"SELECT [HouseAccountBalance] FROM [ClubMembers] WHERE [Id] = '{memberId}'")).Should().Be(0m,
                "the card's payment settled every tab");
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task TwoMembersWhoseStatementsForTheSameMonthWerePrinted_CanBeMerged_AndBothReferencesStillPay()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (client, org) = await ClubAsync(app);
            // The month's statement run prints every member's statement, the duplicate's too.
            var keep = await OwingMemberAsync(client, "Jean");
            var drop = await OwingMemberAsync(client, "Jeannot");
            (await StatementAsync(client, keep)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await StatementAsync(client, drop)).StatusCode.Should().Be(HttpStatusCode.OK);
            var dropReference = (string)(await SqlServerApp.ScalarAsync(database, $"SELECT [QrReference] FROM [ClubPaymentReferences] WHERE [MemberId] = '{drop}'"))!;

            var merged = await client.PostAsync($"/api/members/{keep}/merge", Workspaces.Body(new { otherMemberId = drop }));
            merged.StatusCode.Should().Be(HttpStatusCode.NoContent, await merged.Content.ReadAsStringAsync());

            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubPaymentReferences] WHERE [MemberId] = '{keep}'")).Should().Be(2,
                "the duplicate's printed reference is the kept member's now, beside their own");
            (await StatementAsync(client, keep)).StatusCode.Should().Be(HttpStatusCode.OK, "the kept member's statement prints again with its own reference");
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubPaymentReferences] WHERE [OrganizationId] = '{org}'")).Should().Be(2);

            // The duplicate's slip, paid after the merge, lands on the kept member's account.
            var import = await Ok(await BankFileTests.ImportAsync(client, BankFileTests.Camt054(("TX-1", 299m, dropReference))), "import the transfer");
            import.GetProperty("matched").GetInt32().Should().Be(1);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubPayments] WHERE [MemberId] = '{keep}' AND [Provider] = N'bank'")).Should().Be(1);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task OneStatementPrintedTwiceAtOnce_KeepsOneReference()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            var gate = new RaceFactory.ReadGate();
            using var app = SqlServerApp.Start(database, services: services => services.AddSingleton<ACI.Application.Services.ILedgerReadObserver>(gate));
            var (client, _) = await ClubAsync(app);
            var jean = await OwingMemberAsync(client, "Jean");
            gate.Arm("payment-references");

            var prints = await Task.WhenAll(StatementAsync(client, jean), StatementAsync(client, jean));

            prints.Should().OnlyContain(p => p.StatusCode == HttpStatusCode.OK, "the print that lost the race reads the winner's reference");
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubPaymentReferences] WHERE [MemberId] = '{jean}'")).Should().Be(1);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task TwoStatementsPrintedAtOnce_GetTwoNumbers()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            var gate = new RaceFactory.ReadGate();
            using var app = SqlServerApp.Start(database, services: services => services.AddSingleton<ACI.Application.Services.ILedgerReadObserver>(gate));
            var (client, org) = await ClubAsync(app);
            var jean = await OwingMemberAsync(client, "Jean");
            var ana = await OwingMemberAsync(client, "Ana");
            gate.Arm("payment-references");

            var prints = await Task.WhenAll(StatementAsync(client, jean), StatementAsync(client, ana));

            prints.Should().OnlyContain(p => p.StatusCode == HttpStatusCode.OK, "the print that lost the number takes the next one");
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(DISTINCT [Number]) FROM [ClubPaymentReferences] WHERE [OrganizationId] = '{org}'")).Should().Be(2);
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(DISTINCT [QrReference]) FROM [ClubPaymentReferences] WHERE [OrganizationId] = '{org}'")).Should().Be(2);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
