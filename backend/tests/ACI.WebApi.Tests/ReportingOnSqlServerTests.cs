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
            stats.GetProperty("pipelineValue").GetDecimal().Should().Be(120000m);
            stats.GetProperty("unreadableValueCount").GetInt32().Should().Be(1);
            stats.GetProperty("pipelineByCurrency").EnumerateArray().Select(r => r.GetProperty("currency").GetString())
                .Should().Equal(new[] { "CHF" }, "a deal with no currency is in the workspace's, as new deals are, not in USD");

            var stages = await GetJson(owner, "/api/reporting/pipeline-by-stage");
            stages.EnumerateArray().Sum(r => r.GetProperty("dealCount").GetInt32()).Should().Be(4);
            var owners = await GetJson(owner, "/api/reporting/pipeline-by-assignee");
            owners.EnumerateArray().Where(r => r.GetProperty("currency").GetString() == "CHF")
                .Sum(r => r.GetProperty("value").GetDecimal()).Should().Be(120000m);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    private static async Task<(SqlServerApp App, HttpClient Owner, Guid OwnerId, Guid Org)> StartWithOrgAsync(string database)
    {
        var app = SqlServerApp.Start(database);
        var (owner, ownerId) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
        var created = await owner.PostAsync("/api/organizations", Body(new { name = "Pavillon" }));
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var org = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
        owner.In(org);
        return (app, owner, ownerId, org);
    }

    [SqlServerFact]
    public async Task TheReportsAndTheForecastAreComputedInSql()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            var (app, owner, ownerId, org) = await StartWithOrgAsync(database);
            using var _ = app;
            var now = DateTime.UtcNow;
            // The month it is in Zurich, the workspace's time zone, which "this month" is counted in.
            var month = new DateTime(WorkspaceToday.Year, WorkspaceToday.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var teamId = Guid.NewGuid();
            var pipelineId = Guid.NewGuid();
            var stageId = Guid.NewGuid();
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.SalesTeams.Add(new SalesTeam { Id = teamId, OrganizationId = org, Name = "Geneva", CreatedAtUtc = now });
                db.SalesTeamMembers.Add(new SalesTeamMember { TeamId = teamId, UserId = ownerId });
                db.Pipelines.Add(new Pipeline { Id = pipelineId, OrganizationId = org, Name = "Memberships" });
                db.DealStages.Add(new DealStage { Id = stageId, PipelineId = pipelineId, Name = "Proposal", DisplayOrder = 1 });
                // Weighted by its stage's 50%, as a deal with no probability of its own is.
                var tourId = Guid.NewGuid();
                db.DealStages.Add(new DealStage { Id = tourId, PipelineId = pipelineId, Name = "Tour", DisplayOrder = 2, Probability = 50 });
                db.Deals.Add(new Deal
                {
                    Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, AssigneeId = ownerId, Name = "Tour deal", Value = "600",
                    Currency = "CHF", ExpectedCloseDateUtc = month.AddMonths(1).AddDays(-1), PipelineId = pipelineId, DealStageId = tourId,
                    CreatedAtUtc = month.AddDays(-10),
                });
                Deal D(string value, string? currency, bool? won = null, DateTime? closedAt = null, DateTime? closes = null, int? probability = null) => new()
                {
                    Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, AssigneeId = ownerId, Name = "Deal", Value = value,
                    Currency = currency, IsWon = won, ClosedAtUtc = closedAt, ExpectedCloseDateUtc = closes, Probability = probability,
                    PipelineId = pipelineId, DealStageId = stageId, CreatedAtUtc = month.AddDays(-10),
                };
                db.Deals.AddRange(
                    D("10000", "CHF", closes: month.AddMonths(1).AddDays(-1), probability: 50),
                    D("2000", " eur ", closes: month.AddMonths(2), probability: 25),
                    D("4000", "CHF", won: true, closedAt: month.AddHours(3)),
                    D("1000", "CHF", won: false, closedAt: month.AddHours(4)),
                    D("to be agreed", "CHF"));
                db.Leads.Add(new Lead { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Name = "A", Email = "a@example.ch", Status = "New", Source = "website", LeadScore = 30, CreatedAtUtc = month.AddHours(1) });
                db.Leads.Add(new Lead { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Name = "B", Email = "b@example.ch", Status = "Won", IsConverted = true, ConvertedAtUtc = month.AddHours(2), CreatedAtUtc = month.AddHours(1) });
                db.Activities.Add(new Activity { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Type = "Call", CreatedAtUtc = month.AddHours(23).AddMinutes(30) });
                db.Activities.Add(new Activity { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Type = "system", CreatedAtUtc = month.AddHours(2) });
                await db.SaveChangesAsync();
            }

            var period = $"from={month:yyyy-MM-dd}T00:00:00Z&to={month.AddMonths(1):yyyy-MM-dd}T00:00:00Z";
            foreach (var query in new[] { "", "&scope=me", "&scope=team", $"&scope=team&teamId={teamId}", $"&pipelineId={pipelineId}", "&currency=CHF", "&tz=Europe/Zurich" })
            {
                var summary = await GetJson(owner, $"/api/reporting/summary?{period}{query}");
                summary.GetProperty("won").GetProperty("count").GetInt32().Should().Be(1, query);
                summary.GetProperty("winRate").GetDouble().Should().Be(0.5, query);
                summary.GetProperty("byStage")[0].GetProperty("stageName").GetString().Should().Be("Proposal", query);
                summary.GetProperty("weighted").EnumerateArray().Single(r => r.GetProperty("currency").GetString() == "CHF")
                    .GetProperty("value").GetDecimal().Should().Be(5300m, query);
                summary.GetProperty("leads").GetProperty("active").GetInt32().Should().Be(1, query);
                summary.GetProperty("leads").GetProperty("averageScore").GetDouble().Should().Be(30, query);
                summary.GetProperty("activities").GetProperty("total").GetInt32().Should().Be(1, query);
            }
            var zurich = await GetJson(owner, $"/api/reporting/summary?{period}&tz=Europe/Zurich");
            zurich.GetProperty("activities").GetProperty("series").EnumerateArray()
                .Single(p => p.GetProperty("count").GetInt32() == 1).GetProperty("start").GetString()
                .Should().Be($"{month.AddDays(1):yyyy-MM-dd}", "half past eleven UTC on the 1st is the 2nd in Zurich");
            var year = await GetJson(owner, $"/api/reporting/summary?from={month.AddMonths(-11):yyyy-MM-dd}T00:00:00Z&to={month.AddMonths(1):yyyy-MM-dd}T00:00:00Z");
            year.GetProperty("period").GetProperty("bucket").GetString().Should().Be("week");
            year.GetProperty("leads").GetProperty("createdSeries").EnumerateArray().Sum(p => p.GetProperty("count").GetInt32()).Should().Be(2);

            var forecast = await GetJson(owner, $"/api/reporting/forecast?from={month:yyyy-MM}&months=3");
            var rows = forecast.GetProperty("rows").EnumerateArray().ToList();
            rows.Single(r => r.GetProperty("month").GetString() == $"{month:yyyy-MM}" && r.GetProperty("currency").GetString() == "CHF")
                .GetProperty("weighted").GetDecimal().Should().Be(5300m);
            rows.Single(r => r.GetProperty("month").GetString() == $"{month:yyyy-MM}" && r.GetProperty("currency").GetString() == "CHF")
                .GetProperty("wonValue").GetDecimal().Should().Be(4000m);
            rows.Single(r => r.GetProperty("month").GetString() == $"{month.AddMonths(2):yyyy-MM}" && r.GetProperty("currency").GetString() == "EUR")
                .GetProperty("weighted").GetDecimal().Should().Be(500m);
            forecast.GetProperty("noCloseDate")[0].GetProperty("unreadable").GetInt32().Should().Be(1);

            var saved = await owner.PostAsync($"/api/reporting/forecast-snapshot?year={month.Year}&month={month.Month}", null);
            saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
            (await owner.PostAsync($"/api/reporting/forecast-snapshot?year={month.Year}&month={month.Month}", null)).StatusCode.Should().Be(HttpStatusCode.OK);
            var saves = await GetJson(owner, $"/api/reporting/forecast-snapshots?year={month.Year}&month={month.Month}");
            saves.GetProperty("saves").GetArrayLength().Should().Be(2);
            saves.GetProperty("rows").EnumerateArray().Single(r => r.GetProperty("currency").GetString() == "CHF")
                .GetProperty("weightedValue").GetDecimal().Should().Be(5300m);
            saves.GetProperty("actual").EnumerateArray().Single().GetProperty("wonValue").GetDecimal().Should().Be(4000m);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    /// <summary>
    /// Saved forecasts from before saves were kept: a month held one save, its lines
    /// sharing when and by whom they were saved. On start they are grouped into that save.
    /// </summary>
    [SqlServerFact]
    public async Task SavedForecastsFromBeforeAreGroupedIntoTheirSaves()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            var (app, owner, ownerId, org) = await StartWithOrgAsync(database);
            app.Dispose();

            // The table as production has it before this deploy.
            await SqlServerApp.DropColumnsAsync(database, "ForecastSnapshots",
                "SnapshotId", "ByCloseDate", "OverdueCount", "OverdueValue", "NoCloseDateCount", "UnreadableCount");
            var now = DateTime.UtcNow;
            var month = new DateTime(now.Year, now.Month, 1);
            var savedAt = month.AddHours(9);
            foreach (var (name, currency, monthUtc, at) in new[]
            {
                ("Anna", "CHF", month, savedAt), ("Beat", "CHF", month, savedAt), ("Anna", "CHF", month.AddMonths(-1), savedAt.AddMonths(-1)),
            })
            {
                await SqlServerApp.ExecuteAsync(database, $@"
                    INSERT INTO [ForecastSnapshots] ([Id], [OrganizationId], [SavedByUserId], [MonthUtc], [Currency], [AssigneeId], [AssigneeName],
                        [WeightedValue], [OpenValue], [Covered], [Missing], [SavedAtUtc])
                    VALUES (NEWID(), '{org}', '{ownerId}', '{monthUtc:yyyy-MM-dd}', N'{currency}', NULL, N'{name}', 100, 200, 1, 0, '{at:yyyy-MM-ddTHH:mm:ss}')");
            }

            using (var restarted = SqlServerApp.Start(database))
            {
                (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(DISTINCT [SnapshotId]) FROM [ForecastSnapshots]")).Should().Be(2,
                    "the two lines saved together are one save, and last month's is another");
                (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [ForecastSnapshots] WHERE [ByCloseDate] = 1")).Should().Be(0,
                    "those saves counted every open deal, whatever its close date");
                var client = restarted.CreateClient();
                client.DefaultRequestHeaders.Authorization = owner.DefaultRequestHeaders.Authorization;
                client.In(org);
                var set = await GetJson(client, $"/api/reporting/forecast-snapshots?year={month.Year}&month={month.Month}");
                set.GetProperty("saves").GetArrayLength().Should().Be(1);
                set.GetProperty("saves")[0].GetProperty("byCloseDate").GetBoolean().Should().BeFalse();
                set.GetProperty("rows").GetArrayLength().Should().Be(2);
            }
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
