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
                Priced("to be agreed", "CHF"));
            await db.SaveChangesAsync();
        }

        var res = await owner.GetAsync("/api/companies/stats");
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var stats = JsonDocument.Parse(body).RootElement.EnumerateArray().Single(s => s.GetProperty("companyId").GetGuid() == company);
        stats.GetProperty("dealCount").GetInt32().Should().Be(4);
        var totals = stats.GetProperty("dealTotals").EnumerateArray()
            .ToDictionary(t => t.GetProperty("currency").GetString()!, t => t.GetProperty("value").GetDecimal());
        totals.Should().Equal(new Dictionary<string, decimal> { ["CHF"] = 41234.56m, ["EUR"] = 30000m },
            "francs and euros came out as one figure, \"$70K\"");
    }
}
