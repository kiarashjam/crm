using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

using ACI.WebApi.Tests.Helpers;

namespace ACI.WebApi.Tests;

/// <summary>
/// Forecasts go by each deal's expected close month, and a month's saved forecasts
/// are kept, not replaced.
/// </summary>
public class ForecastTests
{
    // The month it is now in Zurich, the workspace's time zone, which the server counts
    // "this month" in. UTC's month differs from it for an hour or two at every month's end.
    private static DateTime ThisMonth => new(WorkspaceToday.Year, WorkspaceToday.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    private static async Task<(CustomWebApplicationFactory Factory, HttpClient Owner, Guid OwnerId, Guid Org)> OrgAsync()
    {
        var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        return (factory, owner, ownerId, org);
    }

    private static async Task AddDealsAsync(CustomWebApplicationFactory factory, params Deal[] deals)
    {
        using var scope = factory.Services.CreateScope();
        using var db = scope.ServiceProvider.EveryWorkspace();
        db.Deals.AddRange(deals);
        await db.SaveChangesAsync();
    }

    private static Deal Open(Guid ownerId, Guid org, string value, DateTime? closes, int? probability = 50) => new()
    {
        Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, AssigneeId = ownerId, Name = "Deal", Value = value,
        Currency = "CHF", Probability = probability, ExpectedCloseDateUtc = closes, CreatedAtUtc = DateTime.UtcNow.AddDays(-30),
    };

    private static async Task<JsonElement> Json(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    [Fact]
    public async Task EachMonthHoldsOnlyTheDealsExpectedToCloseInIt()
    {
        var (factory, owner, ownerId, org) = await OrgAsync();
        using var _ = factory;
        var month = ThisMonth;
        await AddDealsAsync(factory,
            // The month's last day, so it is never past its date while the test runs.
            Open(ownerId, org, "10000", month.AddMonths(1).AddDays(-1)),
            Open(ownerId, org, "60000", month.AddMonths(5).AddDays(2), probability: 60),
            Open(ownerId, org, "8000", month.AddMonths(-2)),
            Open(ownerId, org, "3000", null),
            new Deal
            {
                Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, AssigneeId = ownerId, Name = "Won", Value = "4000",
                Currency = "CHF", IsWon = true, ClosedAtUtc = month.AddHours(30), CreatedAtUtc = month.AddDays(-40),
            });

        var forecast = await Json(await owner.GetAsync($"/api/reporting/forecast?from={month:yyyy-MM}&months=6"));

        var rows = forecast.GetProperty("rows").EnumerateArray().ToDictionary(r => r.GetProperty("month").GetString()!);
        rows.Should().HaveCount(6, "every month of the window, empty ones included");
        var first = rows[$"{month:yyyy-MM}"];
        first.GetProperty("count").GetInt32().Should().Be(1, "a deal expected in five months is not this month's forecast");
        first.GetProperty("value").GetDecimal().Should().Be(10000m);
        first.GetProperty("weighted").GetDecimal().Should().Be(5000m);
        first.GetProperty("wonCount").GetInt32().Should().Be(1);
        first.GetProperty("wonValue").GetDecimal().Should().Be(4000m);
        rows[$"{month.AddMonths(5):yyyy-MM}"].GetProperty("weighted").GetDecimal().Should().Be(36000m);
        rows[$"{month.AddMonths(1):yyyy-MM}"].GetProperty("count").GetInt32().Should().Be(0);
        forecast.GetProperty("overdue")[0].GetProperty("value").GetDecimal().Should().Be(8000m);
        forecast.GetProperty("noCloseDate")[0].GetProperty("count").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task SavingAgainKeepsTheEarlierForecast()
    {
        var (factory, owner, ownerId, org) = await OrgAsync();
        using var _ = factory;
        var month = ThisMonth;
        var deal = Open(ownerId, org, "10000", month.AddDays(10));
        await AddDealsAsync(factory, deal, Open(ownerId, org, "90000", month.AddMonths(3)), Open(ownerId, org, "500", null),
            Open(ownerId, org, "700", month.AddMonths(-1)));

        var first = await Json(await owner.PostAsync($"/api/reporting/forecast-snapshot?year={month.Year}&month={month.Month}", null));
        var firstId = first.GetProperty("snapshotId").GetGuid();
        var line = first.GetProperty("rows")[0];
        line.GetProperty("openValue").GetDecimal().Should().Be(10000m, "only the deal expected this month");
        line.GetProperty("weightedValue").GetDecimal().Should().Be(5000m);
        line.GetProperty("noCloseDateCount").GetInt32().Should().Be(1);
        line.GetProperty("overdueCount").GetInt32().Should().Be(1);
        line.GetProperty("overdueValue").GetDecimal().Should().Be(700m);
        line.GetProperty("byCloseDate").GetBoolean().Should().BeTrue();

        // The deal grows, and another manager saves again on the 30th.
        using (var scope = factory.Services.CreateScope())
        {
            using var db = scope.ServiceProvider.EveryWorkspace();
            var tracked = await db.Deals.SingleAsync(d => d.Id == deal.Id);
            tracked.Value = "25000";
            await db.SaveChangesAsync();
        }
        var second = await Json(await owner.PostAsync($"/api/reporting/forecast-snapshot?year={month.Year}&month={month.Month}", null));
        second.GetProperty("rows")[0].GetProperty("openValue").GetDecimal().Should().Be(25000m);

        var all = await Json(await owner.GetAsync($"/api/reporting/forecast-snapshots?year={month.Year}&month={month.Month}"));
        all.GetProperty("saves").GetArrayLength().Should().Be(2, "saving again used to delete the month's earlier save");
        all.GetProperty("snapshotId").GetGuid().Should().Be(second.GetProperty("snapshotId").GetGuid(), "the latest is shown first");
        var earlier = await Json(await owner.GetAsync($"/api/reporting/forecast-snapshots?year={month.Year}&month={month.Month}&snapshotId={firstId}"));
        earlier.GetProperty("rows")[0].GetProperty("openValue").GetDecimal().Should().Be(10000m, "what was forecast first is still there");
        (await owner.GetAsync($"/api/reporting/forecast-snapshots?year={month.Year}&month={month.Month}&snapshotId={Guid.NewGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        // The old endpoint still answers with one save's lines, not both saves' mixed.
        (await Json(await owner.GetAsync("/api/reporting/forecast-snapshot"))).GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task AMonthIsSavedBeforeOrDuringItNeverAfter()
    {
        var (factory, owner, ownerId, org) = await OrgAsync();
        using var _ = factory;
        var month = ThisMonth;
        await AddDealsAsync(factory, Open(ownerId, org, "10000", month.AddMonths(1).AddDays(3)));

        var past = month.AddMonths(-2);
        (await owner.PostAsync($"/api/reporting/forecast-snapshot?year={past.Year}&month={past.Month}", null)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "a forecast written once the month is over is hindsight");
        var next = month.AddMonths(1);
        var saved = await Json(await owner.PostAsync($"/api/reporting/forecast-snapshot?year={next.Year}&month={next.Month}", null));
        saved.GetProperty("month").GetString().Should().Be($"{next:yyyy-MM}");
        saved.GetProperty("rows")[0].GetProperty("openValue").GetDecimal().Should().Be(10000m);
        var far = month.AddMonths(14);
        (await owner.PostAsync($"/api/reporting/forecast-snapshot?year={far.Year}&month={far.Month}", null)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
        (await owner.PostAsync("/api/reporting/forecast-snapshot?year=2026&month=13", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task TheSavedForecastIsSetAgainstWhatWasWon()
    {
        var (factory, owner, ownerId, org) = await OrgAsync();
        using var _ = factory;
        var (_, repId) = await SignInAsync(factory, "rep@example.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        var month = ThisMonth;
        await AddDealsAsync(factory,
            Open(ownerId, org, "10000", month.AddDays(10)),
            new Deal
            {
                Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, AssigneeId = repId, Name = "Won", Value = "6000",
                Currency = "CHF", IsWon = true, ClosedAtUtc = month.AddHours(5), CreatedAtUtc = month.AddDays(-20),
            });

        var saved = await Json(await owner.PostAsync("/api/reporting/forecast-snapshot", null));

        var actual = saved.GetProperty("actual").EnumerateArray().Single();
        actual.GetProperty("assigneeName").GetString().Should().Be("rep@example.ch");
        actual.GetProperty("wonValue").GetDecimal().Should().Be(6000m);
        saved.GetProperty("saves")[0].GetProperty("savedByName").GetString().Should().Be("owner@example.ch");
    }

    [Fact]
    public async Task AWinCountsInTheMonthItHappenedInOnTheWorkspacesCalendar()
    {
        // 23:30 UTC on 31 October is half past midnight on 1 November in Zurich, and
        // still October in London. Read in UTC it was October's everywhere.
        var (factory, owner, ownerId, org) = await OrgAsync();
        using var _ = factory;
        await AddDealsAsync(factory, new Deal
        {
            Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, AssigneeId = ownerId, Name = "Won late", Value = "4000",
            Currency = "CHF", IsWon = true, ClosedAtUtc = new DateTime(2026, 10, 31, 23, 30, 0, DateTimeKind.Utc),
            CreatedAtUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        });
        async Task<Dictionary<string, int>> WonByMonth()
        {
            var forecast = await Json(await owner.GetAsync("/api/reporting/forecast?from=2026-10&months=2"));
            return forecast.GetProperty("rows").EnumerateArray()
                .ToDictionary(r => r.GetProperty("month").GetString()!, r => r.GetProperty("wonCount").GetInt32());
        }

        (await WonByMonth()).Should().Equal(new Dictionary<string, int> { ["2026-10"] = 0, ["2026-11"] = 1 });
        var november = await Json(await owner.GetAsync("/api/reporting/forecast-snapshots?year=2026&month=11"));
        november.GetProperty("actual")[0].GetProperty("wonValue").GetDecimal().Should().Be(4000m);
        var october = await Json(await owner.GetAsync("/api/reporting/forecast-snapshots?year=2026&month=10"));
        october.GetProperty("actual").GetArrayLength().Should().Be(0);

        (await owner.PutAsync($"/api/organizations/{org}/settings", Body(new { timeZone = "Europe/London" })))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await WonByMonth()).Should().Equal(new Dictionary<string, int> { ["2026-10"] = 1, ["2026-11"] = 0 });
    }

    [Fact]
    public async Task AWinJustAfterMidnightCountsInItsMonthInAZoneHalfAnHourOffUtc()
    {
        // India is five and a half hours ahead of UTC: 18:40 UTC on 31 October is ten past
        // midnight on 1 November there. Counted by the UTC hour, 18:00 to 19:00, the win was
        // put in the month of 23:30 local time, October.
        var (factory, owner, ownerId, org) = await OrgAsync();
        using var _ = factory;
        (await owner.PutAsync($"/api/organizations/{org}/settings", Body(new { timeZone = "Asia/Kolkata" })))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        Deal Won(DateTime at, string value) => new()
        {
            Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, AssigneeId = ownerId, Name = "Won", Value = value,
            Currency = "CHF", IsWon = true, ClosedAtUtc = at, CreatedAtUtc = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        await AddDealsAsync(factory,
            Won(new DateTime(2026, 10, 31, 18, 40, 0, DateTimeKind.Utc), "4000"),
            // 23:50 on 31 October in India, in the same UTC hour: still October's.
            Won(new DateTime(2026, 10, 31, 18, 20, 0, DateTimeKind.Utc), "1000"));

        var forecast = await Json(await owner.GetAsync("/api/reporting/forecast?from=2026-10&months=2"));
        var won = forecast.GetProperty("rows").EnumerateArray()
            .ToDictionary(r => r.GetProperty("month").GetString()!, r => r.GetProperty("wonValue").GetDecimal());
        won.Should().Equal(new Dictionary<string, decimal> { ["2026-10"] = 1000m, ["2026-11"] = 4000m });
    }

    [Fact]
    public async Task ThereIsNothingToSaveWithNoOpenDeals()
    {
        var (factory, owner, _, _) = await OrgAsync();
        using var _ = factory;
        (await owner.PostAsync("/api/reporting/forecast-snapshot", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
