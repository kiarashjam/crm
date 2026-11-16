using System.Net;
using System.Text.Json;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.ClubReportsTests;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Club Reports and Today at the Club on a real SQL Server. The in-memory provider runs any
/// LINQ in memory, so only SQL Server shows that every sum (grouped by the hour, with the
/// subqueries that tell a sale from an opening balance and a payment from a credit) turns
/// into SQL, and comes out as the in-memory tests expect.
/// </summary>
public class ClubReportsOnSqlServerTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static decimal[] Column(JsonElement flow, string field) =>
        flow.GetProperty("byPeriod").EnumerateArray().Select(p => p.GetProperty(field).GetDecimal()).ToArray();

    [SqlServerFact]
    public async Task TheClubsFiguresAreSummedInSql()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var created = await owner.PostAsync("/api/organizations", Body(new { name = "Pavillon" }));
            created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
            var org = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
            owner.In(org);

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                SeedRevenue(db, org);
                await db.SaveChangesAsync();
                SeedReceivables(db, org);
                await db.SaveChangesAsync();
                SeedStanding(db, org);
                SeedEvents(db, org);
                await db.SaveChangesAsync();
            }

            var from = Zurich(2025, 1, 1, 0);
            var to = Zurich(2025, 4, 1, 0);
            var report = await Json(await owner.GetAsync($"/api/club-reports?from={Q(from)}&to={Q(to)}&grain=month"), "the report");

            var cash = report.GetProperty("cash").EnumerateArray().Single();
            // The receivables' two payments (January) come on top of the revenue's.
            Column(cash, "in").Should().Equal(new[] { 250m, 180m, 100m });
            Column(cash, "out").Should().Equal(new[] { 0m, 80m, 20m });

            var accrual = report.GetProperty("accrual").EnumerateArray().Single();
            // Revenue: dues 300 and bar 100 in January, the bar voided in February. Receivables:
            // dues 120 and the evening 90 in January, a bar bill and its void in February, dinner,
            // spa and the shop in March (its void falls in April, after the report).
            Column(accrual, "in").Should().Equal(new[] { 610m, 15m, 155m });
            Column(accrual, "out").Should().Equal(new[] { 0m, 115m, 0m });

            var ageing = report.GetProperty("receivables").EnumerateArray().Single();
            // The revenue's own open entries: the bar bill was voided; the opening balance
            // (December, over 90 days) and the correction (March, 31 days) are owed.
            ageing.GetProperty("over90").GetDecimal().Should().Be(1000m);
            ageing.GetProperty("days31To60").GetDecimal().Should().Be(80m);
            ageing.GetProperty("days61To90").GetDecimal().Should().Be(60m);
            ageing.GetProperty("days1To30").GetDecimal().Should().Be(85m);
            ageing.GetProperty("current").GetDecimal().Should().Be(70m);

            report.GetProperty("giftCards").EnumerateArray().Single().GetProperty("outstanding").GetDecimal().Should().Be(70m);
            report.GetProperty("points").GetProperty("points").GetInt32().Should().Be(500);
            report.GetProperty("mrr").EnumerateArray().Single().GetProperty("monthly").GetDecimal().Should().Be(419m);
            var events = report.GetProperty("events");
            events.GetProperty("seatsTaken").GetInt32().Should().Be(7);
            events.GetProperty("noShowRate").GetDecimal().Should().Be(0.2m);

            var export = await owner.GetAsync($"/api/club-reports/export?from={Q(from)}&to={Q(to)}&grain=month");
            export.StatusCode.Should().Be(HttpStatusCode.OK);
            (await export.Content.ReadAsStringAsync()).Should().Contain("Cash received,2025-02-01,CHF,,180.00,");

            var today = await Json(await owner.GetAsync("/api/club-reports/today"), "today");
            today.GetProperty("pastDue").GetProperty("charges").GetInt32().Should().BeGreaterThan(0, "the dinner and the evening are long past due");
            today.GetProperty("openTabs").GetProperty("count").GetInt32().Should().Be(0);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
