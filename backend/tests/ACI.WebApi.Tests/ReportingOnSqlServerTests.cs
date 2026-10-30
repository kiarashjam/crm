using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The reporting queries on a real SQL Server. The in-memory provider runs any LINQ in
/// memory, so only SQL Server shows whether EF can turn each one into SQL at all, and
/// whether the sums come out the same once the database does them.
/// </summary>
public class ReportingOnSqlServerTests
{
    private static async Task<JsonElement> GetJson(HttpClient client, string url)
    {
        var res = await client.GetAsync(url);
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{url}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    [SqlServerFact]
    public async Task TheDashboardIsAddedUpInSql()
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

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                void Add(string value, string? currency, bool? won) => db.Deals.Add(new Deal
                {
                    Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, AssigneeId = ownerId, Name = "Deal",
                    Value = value, Currency = currency, IsWon = won, Stage = "Proposal", CreatedAtUtc = DateTime.UtcNow,
                });
                Add("CHF 85,500", "CHF", null);
                Add("14500", " chf ", null);
                Add("20000", null, null);
                Add("to be agreed", "CHF", null);
                Add("99000", "CHF", true);
                db.Leads.Add(new Lead { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Name = "A", Email = "a@example.ch", CreatedAtUtc = DateTime.UtcNow });
                db.Leads.Add(new Lead { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Name = "B", Email = "b@example.ch", IsConverted = true, CreatedAtUtc = DateTime.UtcNow });
                await db.SaveChangesAsync();
            }

            var stats = await GetJson(owner, "/api/reporting/dashboard");
            stats.GetProperty("activeLeadsCount").GetInt32().Should().Be(1);
            stats.GetProperty("activeDealsCount").GetInt32().Should().Be(4);
            stats.GetProperty("pipelineValue").GetDecimal().Should().Be(100000m);
            stats.GetProperty("unreadableValueCount").GetInt32().Should().Be(1);
            stats.GetProperty("pipelineByCurrency").EnumerateArray()
                .Single(r => r.GetProperty("currency").GetString() == "USD").GetProperty("value").GetDecimal()
                .Should().Be(20000m, "a deal with no currency is counted as USD, as everywhere else");

            var stages = await GetJson(owner, "/api/reporting/pipeline-by-stage");
            stages.EnumerateArray().Sum(r => r.GetProperty("dealCount").GetInt32()).Should().Be(4);
            var owners = await GetJson(owner, "/api/reporting/pipeline-by-assignee");
            owners.EnumerateArray().Where(r => r.GetProperty("currency").GetString() == "CHF")
                .Sum(r => r.GetProperty("value").GetDecimal()).Should().Be(100000m);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
