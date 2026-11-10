using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>A company's deals are totalled per currency, from the stored amounts.</summary>
public class CompanyDealTotalsTests
{
    private static void AddCompanyWithDeals(AppDbContext db, Guid company, Guid ownerId, Guid org)
    {
        db.Companies.Add(new Company { Id = company, UserId = ownerId, OrganizationId = org, Name = "Fondation Léman", CreatedAtUtc = DateTime.UtcNow });
        Deal Priced(string value, string? currency) => new()
        {
            Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, CompanyId = company, Name = "Deal",
            Value = value, Currency = currency, CreatedAtUtc = DateTime.UtcNow,
        };
        db.Deals.AddRange(
            Priced("40000", "CHF"),
            // Typed before amounts were numbers: the stats read it as 1.23456.
            Priced("1.234,56", "chf"),
            Priced("30000", "EUR"),
            Priced("to be agreed", "CHF"),
            // Saved without a currency: in the workspace's, not in dollars.
            Priced("500", null));
    }

    private static async Task<JsonElement> StatsOf(HttpClient client, Guid company)
    {
        var res = await client.GetAsync("/api/companies/stats");
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.EnumerateArray().Single(s => s.GetProperty("companyId").GetGuid() == company).Clone();
    }

    private static Dictionary<string, decimal> Totals(JsonElement stats) => stats.GetProperty("dealTotals").EnumerateArray()
        .ToDictionary(t => t.GetProperty("currency").GetString()!, t => t.GetProperty("value").GetDecimal());

    [Fact]
    public async Task FrancsAndEurosAreTotalledApartAndEuropeanNotationIsReadOnce()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        var company = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            AddCompanyWithDeals(db, company, ownerId, org);
            await db.SaveChangesAsync();
        }

        var stats = await StatsOf(owner, company);
        stats.GetProperty("dealCount").GetInt32().Should().Be(5);
        Totals(stats).Should().Equal(new Dictionary<string, decimal> { ["CHF"] = 41734.56m, ["EUR"] = 30000m },
            "francs and euros came out as one figure, \"$70K\"");
    }

    /// <summary>
    /// The same totals, added up by SQL Server. The in-memory provider runs any LINQ in
    /// memory, so only a real server shows that the grouping by company and currency
    /// becomes SQL at all.
    /// </summary>
    [SqlServerFact]
    public async Task TheTotalsAreAddedUpInSql()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, ownerId) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var created = await owner.PostAsync("/api/organizations", Body(new { name = "Pavillon" }));
            created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
            var org = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
            owner.In(org);
            var company = Guid.NewGuid();
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                AddCompanyWithDeals(db, company, ownerId, org);
                await db.SaveChangesAsync();
            }

            var stats = await StatsOf(owner, company);
            stats.GetProperty("dealCount").GetInt32().Should().Be(5);
            Totals(stats).Should().Equal(new Dictionary<string, decimal> { ["CHF"] = 41734.56m, ["EUR"] = 30000m });
            stats.GetProperty("dealTotals")[0].GetProperty("dealCount").GetInt32().Should().Be(3, "the deal with no number in it adds nothing");
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
