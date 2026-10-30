using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>The deals saved before every deal had a currency are given one, once.</summary>
public class DealCurrencyFixTests
{
    /// <summary>
    /// A database from before every deal had a currency: on start, once, each deal with
    /// none gets its workspace's, a code written in another case becomes the code, and
    /// what is not a code is left as it was.
    /// </summary>
    [SqlServerFact]
    public async Task AnExistingDatabasesDealsGetTheirWorkspacesCurrencyOnce()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            Guid userId;
            using (var app = SqlServerApp.Start(database))
                (_, userId) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");

            // As production is before this deploy: no ledger line, and deals saved with no
            // currency, a blank one, a lower-case code, a symbol, and in a personal workspace.
            await SqlServerApp.ExecuteAsync(database, $"DELETE FROM [AppliedDataFixes] WHERE [Name] = N'{DataFixes.FillDealCurrenciesName}'");
            var org = Guid.NewGuid();
            await SqlServerApp.ExecuteAsync(database,
                $"INSERT INTO [Organizations] ([Id], [Name], [OwnerUserId], [CreatedAtUtc], [Currency]) VALUES ('{org}', N'Club', '{userId}', SYSUTCDATETIME(), N'EUR')");
            async Task<Guid> DealAsync(Guid? orgId, string? currency)
            {
                var id = Guid.NewGuid();
                var orgSql = orgId is Guid o ? $"'{o}'" : "NULL";
                var currencySql = currency == null ? "NULL" : $"N'{currency}'";
                await SqlServerApp.ExecuteAsync(database,
                    $"INSERT INTO [Deals] ([Id], [UserId], [OrganizationId], [Name], [Value], [Currency], [CreatedAtUtc]) VALUES ('{id}', '{userId}', {orgSql}, N'Deal', N'100', {currencySql}, SYSUTCDATETIME())");
                return id;
            }
            var none = await DealAsync(org, null);
            var blank = await DealAsync(org, " ");
            var lower = await DealAsync(org, "chf ");
            var symbol = await DealAsync(org, "€");
            var personal = await DealAsync(null, null);
            Task<object?> CurrencyOf(Guid id) => SqlServerApp.ScalarAsync(database, $"SELECT [Currency] FROM [Deals] WHERE [Id] = '{id}'");

            using (SqlServerApp.Start(database))
            {
                (await CurrencyOf(none)).Should().Be("EUR", "the workspace's currency, where it used to count as USD");
                (await CurrencyOf(blank)).Should().Be("EUR");
                (await CurrencyOf(lower)).Should().Be("CHF");
                (await CurrencyOf(symbol)).Should().Be("€", "what a symbol meant is not for a data fix to guess");
                (await CurrencyOf(personal)).Should().Be("CHF", "a personal workspace has the default");
                (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [AppliedDataFixes] WHERE [Name] = N'{DataFixes.FillDealCurrenciesName}'"))
                    .Should().Be(1);
            }

            // Recorded, so a later start leaves the deals alone.
            var later = await DealAsync(org, null);
            using (SqlServerApp.Start(database))
                (await CurrencyOf(later)).Should().BeNull();
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
