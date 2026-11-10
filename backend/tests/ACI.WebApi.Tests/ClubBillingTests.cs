using System.Globalization;
using System.Net;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Subscriptions over HTTP: billed by calendar period, only once a period has started, and
/// never twice. The daily run and what follows from it are in ClubBillingRunTests.
/// </summary>
/// <remarks>
/// Dates are worked out from today, so every test reads the same on any day it runs: a
/// subscription "due" here started its first period last month.
/// </remarks>
public class ClubBillingTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<string> Refused(HttpResponseMessage res, HttpStatusCode status, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(status, $"{step} should be refused — got {(int)res.StatusCode}: {body}");
        return body;
    }

    private static async Task<(HttpClient Client, Guid UserId, Guid Org)> ClubAsync(CustomWebApplicationFactory factory, string email = "manager@example.ch")
    {
        var (client, userId) = await SignInAsync(factory, email);
        var org = await CreateAsync(factory, userId, "Pavillon46");
        client.In(org);
        return (client, userId, org);
    }

    private static async Task<HttpClient> JoinAsync(CustomWebApplicationFactory factory, HttpClient owner, Guid org, string email, string preset)
    {
        var (client, userId) = await SignInAsync(factory, email);
        await AddMemberAsync(factory, org, userId, OrgMemberRole.Member);
        await Json(await owner.PutAsync($"/api/organizations/{org}/members/{userId}/permissions", Body(new { preset })), $"make {email} {preset}");
        return client.In(org);
    }

    private static async Task<Guid> MemberAsync(HttpClient client, string first = "Jean", string? email = null) =>
        (await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = "Dupont", email = email ?? $"{first.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
        })), "add member")).GetProperty("id").GetGuid();

    /// <summary>The first day of the month <paramref name="monthsAgo"/> months back, at midnight UTC, as a screen sends a picked date.</summary>
    private static DateTime MonthStart(int monthsAgo)
    {
        var today = DateTime.UtcNow;
        return new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-monthsAgo);
    }

    private static string Day(DateTime d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task<JsonElement> SubscriptionAsync(HttpClient client, Guid memberId, DateTime firstPeriod, string cadence = "Monthly", decimal amount = 299, string name = "Gold dues") =>
        await Json(await client.PostAsync("/api/subscriptions", Body(new { memberId, name, amount, cadence, nextChargeAtUtc = Day(firstPeriod) })), "add subscription");

    private static async Task<List<JsonElement>> ChargesAsync(HttpClient client, Guid memberId) =>
        (await Json(await client.GetAsync($"/api/charges?memberId={memberId}"), "charges")).EnumerateArray()
            .OrderBy(c => c.TryGetProperty("billingPeriodStartUtc", out var p) ? p.GetDateTime() : c.GetProperty("postedAtUtc").GetDateTime())
            .ToList();

    private static DateTime Utc(JsonElement value) => DateTime.Parse(value.GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

    // ── subscriptions by calendar period ─────────────────────────────────────

    [Fact]
    public async Task ASubscriptionBillsEachCalendarPeriodOnce_OnlyOnceItHasStarted_AndEarlyAtMostOnePeriodAhead()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        await Json(await client.PutAsync("/api/hospitality/settings", Body(new { billing = new { paymentTermsDays = 30 } })), "30 days to pay");
        var memberId = await MemberAsync(client);
        var first = MonthStart(1);
        var sub = await SubscriptionAsync(client, memberId, first, name: "Locker", amount: 25);
        var id = sub.GetProperty("id").GetGuid();

        var billed = await Json(await client.PostAsync($"/api/subscriptions/{id}/bill", null), "bill last month");
        var second = BillingPeriods.Next(first, "Monthly", 1);
        Utc(billed.GetProperty("nextChargeAtUtc")).Should().Be(second, "a month on, to the day: not thirty days");
        var charge = (await ChargesAsync(client, memberId)).Single();
        charge.GetProperty("description").GetString().Should().Be($"Locker · {BillingPeriods.Label(first, second)}");
        Utc(charge.GetProperty("billingPeriodStartUtc")).Should().Be(first);
        Utc(charge.GetProperty("dueAtUtc")).Should().BeCloseTo(DateTime.UtcNow.AddDays(30), TimeSpan.FromMinutes(5), "due the club's payment terms after billing");

        await Json(await client.PostAsync($"/api/subscriptions/{id}/bill", null), "bill this month");
        var third = BillingPeriods.Next(second, "Monthly", 1);
        (await Refused(await client.PostAsync($"/api/subscriptions/{id}/bill", null), HttpStatusCode.Conflict, "billing next month before it starts"))
            .Should().Contain("Bill it early");

        var early = await Json(await client.PostAsync($"/api/subscriptions/{id}/bill", Body(new { early = true })), "bill next month early");
        Utc(early.GetProperty("nextChargeAtUtc")).Should().Be(BillingPeriods.Next(third, "Monthly", 1));
        (await Refused(await client.PostAsync($"/api/subscriptions/{id}/bill", Body(new { early = true })), HttpStatusCode.Conflict, "a second early bill"))
            .Should().Contain("one period ahead");
        (await ChargesAsync(client, memberId)).Select(c => Utc(c.GetProperty("billingPeriodStartUtc"))).Should().Equal(first, second, third);
    }

    [Fact]
    public async Task ResumingAPausedSubscriptionPicksUpAtTheNextPeriod_AndDoesNotBillThePause()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client);
        var sub = await SubscriptionAsync(client, memberId, MonthStart(3));
        var id = sub.GetProperty("id").GetGuid();
        await Json(await client.PutAsync($"/api/subscriptions/{id}", Body(new { status = "Paused" })), "pause");

        var resumed = await Json(await client.PutAsync($"/api/subscriptions/{id}", Body(new { status = "Active" })), "resume");
        // The first of next month, or today when today is the first.
        var next = BillingPeriods.FirstOnOrAfter(MonthStart(3), "Monthly", 1, DateTime.UtcNow.Date);
        next.Should().BeOnOrAfter(DateTime.UtcNow.Date);
        Utc(resumed.GetProperty("nextChargeAtUtc")).Should().Be(next, "the months it was paused are not billed: it picks up at the next period");

        await Json(await client.PostAsync($"/api/subscriptions/{id}/bill", Body(new { early = true })), "bill the next period");
        (await ChargesAsync(client, memberId)).Select(c => Utc(c.GetProperty("billingPeriodStartUtc"))).Should().Equal(next);
    }

    [Fact]
    public async Task ASubscriptionStartedAgainAfterItWasCancelled_DoesNotBillTheMonthsItWasCancelled()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await ClubAsync(factory);
        var memberId = await MemberAsync(client);
        var sub = await SubscriptionAsync(client, memberId, MonthStart(3));
        var id = sub.GetProperty("id").GetGuid();
        await Json(await client.PutAsync($"/api/subscriptions/{id}", Body(new { status = "Cancelled" })), "cancel");

        // The member comes back: the subscription runs again, from the next period, as one
        // resumed after a pause does. Left at its old date, the daily run would have billed
        // every month since it was cancelled.
        var started = await Json(await client.PutAsync($"/api/subscriptions/{id}", Body(new { status = "Active" })), "start it again");
        var next = BillingPeriods.FirstOnOrAfter(MonthStart(3), "Monthly", 1, DateTime.UtcNow.Date);
        Utc(started.GetProperty("nextChargeAtUtc")).Should().Be(next);
        var preview = await Json(await client.GetAsync("/api/billing/preview"), "what billing now would post");
        preview.GetProperty("periods").GetInt32().Should().Be(next <= DateTime.UtcNow ? 1 : 0, "only a period that starts after it runs again");
    }
}
