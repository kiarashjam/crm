using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>The dashboard's headline figures, now added up by the database.</summary>
public class DashboardFiguresTests
{
    private static async Task<JsonElement> GetJson(HttpClient client, string url)
    {
        var res = await client.GetAsync(url);
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    [Fact]
    public async Task ActiveLeadsLeavesOutConvertedLeadsAsTheLeadsPageDoes()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var (name, converted) in new[] { ("Anna", false), ("Beat", false), ("Chloé", true) })
            {
                db.Leads.Add(new Lead
                {
                    Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Name = name, Email = $"{name}@example.ch",
                    IsConverted = converted, CreatedAtUtc = DateTime.UtcNow,
                });
            }
            await db.SaveChangesAsync();
        }

        var stats = await GetJson(owner, "/api/reporting/dashboard");

        stats.GetProperty("activeLeadsCount").GetInt32().Should().Be(2, "Chloé is a customer now, not a lead to work");
    }

    [Fact]
    public async Task OpenPipelineIsAddedUpPerCurrencyAndDisclosesWhatItCouldNotRead()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            void Add(string value, string? currency, bool? won) => db.Deals.Add(new Deal
            {
                Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Name = "Deal", Value = value, Currency = currency,
                IsWon = won, CreatedAtUtc = DateTime.UtcNow,
            });
            Add("CHF 85,500", "CHF", null);
            Add("14500", " chf ", null);
            Add("20000", "EUR", null);
            Add("to be agreed", "CHF", null);
            Add("99000", "CHF", true);
            Add("5000", "CHF", false);
            await db.SaveChangesAsync();
        }

        var stats = await GetJson(owner, "/api/reporting/dashboard");

        stats.GetProperty("activeDealsCount").GetInt32().Should().Be(4);
        stats.GetProperty("pipelineCurrency").GetString().Should().Be("CHF");
        stats.GetProperty("pipelineValue").GetDecimal().Should().Be(100000m, "a currency typed in lower case with spaces is still CHF");
        stats.GetProperty("unreadableValueCount").GetInt32().Should().Be(1);
        stats.GetProperty("dealsWonCount").GetInt32().Should().Be(1);
        stats.GetProperty("dealsLostCount").GetInt32().Should().Be(1);
        var byCurrency = stats.GetProperty("pipelineByCurrency").EnumerateArray()
            .ToDictionary(r => r.GetProperty("currency").GetString()!, r => r.GetProperty("value").GetDecimal());
        byCurrency.Should().BeEquivalentTo(new Dictionary<string, decimal> { ["CHF"] = 100000m, ["EUR"] = 20000m });

        var stages = await GetJson(owner, "/api/reporting/pipeline-by-stage");
        stages.EnumerateArray().Sum(r => r.GetProperty("dealCount").GetInt32()).Should().Be(4);
        stages.EnumerateArray().Where(r => r.GetProperty("currency").GetString() == "CHF")
            .Sum(r => r.GetProperty("value").GetDecimal()).Should().Be(100000m);
    }
}
