using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Every deal carries its value as a number, so reports can add deals up in SQL
/// instead of loading every row to read its free text.
/// </summary>
public class DealAmountTests
{
    private static async Task<decimal?> AmountOf(IServiceProvider services, Guid dealId)
    {
        using var scope = services.CreateScope();
        using var db = scope.ServiceProvider.EveryWorkspace();
        return (await db.Deals.AsNoTracking().SingleAsync(d => d.Id == dealId)).Amount;
    }

    [Fact]
    public async Task TheNumberFollowsTheValueThroughTheApi()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        owner.In(await CreateAsync(factory, ownerId, "Pavillon46"));

        var created = await owner.PostAsync("/api/deals", Body(new { name = "Fondation Léman", value = "85500", currency = "CHF" }));
        var body = await created.Content.ReadAsStringAsync();
        created.IsSuccessStatusCode.Should().BeTrue(body);
        var id = JsonDocument.Parse(body).RootElement.GetProperty("id").GetGuid();
        (await AmountOf(factory.Services, id)).Should().Be(85500m);

        var edited = await owner.PutAsync($"/api/deals/{id}", Body(new { value = "90000.50" }));
        edited.IsSuccessStatusCode.Should().BeTrue(await edited.Content.ReadAsStringAsync());
        (await AmountOf(factory.Services, id)).Should().Be(90000.50m, "an edit of the value moves the number with it");
    }

    [Fact]
    public async Task AnyWriterGetsTheNumberAndUnreadableTextGetsNone()
    {
        // Lead conversion, a merge and the cloner write deals without DealService, so
        // the number is set where every save goes, not in one service.
        using var factory = new CustomWebApplicationFactory();
        var (_, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var legacy = Guid.NewGuid();
        var unreadable = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            using var db = scope.ServiceProvider.EveryWorkspace();
            db.Deals.AddRange(
                new Deal { Id = legacy, UserId = ownerId, Name = "Old", Value = "CHF 85,500", CreatedAtUtc = DateTime.UtcNow },
                new Deal { Id = unreadable, UserId = ownerId, Name = "Vague", Value = "to be agreed", CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        (await AmountOf(factory.Services, legacy)).Should().Be(85500m);
        (await AmountOf(factory.Services, unreadable)).Should().BeNull("no number is better than an invented zero");
    }

    /// <summary>
    /// A database from before the column: it arrives on start, and the deals already
    /// there are given their numbers once, read the way the app reads them.
    /// </summary>
    [SqlServerFact]
    public async Task AnExistingDatabaseGetsTheColumnAndItsDealsTheirNumbers()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            Guid userId;
            using (var app = SqlServerApp.Start(database))
                (_, userId) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");

            // As production is before this deploy: no column, no ledger line, and
            // deals typed in whatever notation their writer used.
            await SqlServerApp.ExecuteAsync(database, "ALTER TABLE [Deals] DROP COLUMN [Amount]");
            await SqlServerApp.ExecuteAsync(database, $"DELETE FROM [AppliedDataFixes] WHERE [Name] = N'{DataFixes.FillDealAmountsName}'");
            var swiss = Guid.NewGuid();
            var grouped = Guid.NewGuid();
            var vague = Guid.NewGuid();
            foreach (var (id, value) in new[] { (swiss, "CHF 85,500"), (grouped, "1.234"), (vague, "to be agreed") })
            {
                await SqlServerApp.ExecuteAsync(database,
                    $"INSERT INTO [Deals] ([Id], [UserId], [Name], [Value], [CreatedAtUtc]) VALUES ('{id}', '{userId}', N'Deal', N'{value}', SYSUTCDATETIME())");
            }

            using (SqlServerApp.Start(database))
            {
                (await SqlServerApp.ScalarAsync(database, $"SELECT [Amount] FROM [Deals] WHERE [Id] = '{swiss}'")).Should().Be(85500m);
                (await SqlServerApp.ScalarAsync(database, $"SELECT [Amount] FROM [Deals] WHERE [Id] = '{grouped}'"))
                    .Should().Be(1234m, "the app reads 1.234 as twelve hundred, where TRY_CAST would read one point two");
                (await SqlServerApp.ScalarAsync(database, $"SELECT [Amount] FROM [Deals] WHERE [Id] = '{vague}'")).Should().BeNull();
                (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [AppliedDataFixes] WHERE [Name] = N'{DataFixes.FillDealAmountsName}'"))
                    .Should().Be(1);
            }
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
