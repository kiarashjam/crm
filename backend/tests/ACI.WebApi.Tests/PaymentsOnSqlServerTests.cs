using System.Net;
using System.Text.Json;
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
