using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.Infrastructure.Services.SiteAnalytics;
using ACI.WebApi.Services;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static ACI.WebApi.Tests.Helpers.SiteAnalyticsWorld;

namespace ACI.WebApi.Tests;

/// <summary>
/// The daily site analytics email: from 07:30 Zurich, yesterday's figures against the day before
/// and the same weekday a week earlier, to every platform admin through the outbox, once a day;
/// and the daily clean-up of events older than 400 days.
/// </summary>
public class SiteAnalyticsDigestTests
{
    /// <summary>07:31 in Zurich (CET) on Wednesday 25 November 2026: due, about Tuesday the 24th.</summary>
    private static readonly DateTime Morning = new(2026, 11, 25, 6, 31, 0, DateTimeKind.Utc);

    private static readonly DateOnly Yesterday = new(2026, 11, 24);

    private static async Task<int> SendAsync(CustomWebApplicationFactory app, DateTime now)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISiteAnalyticsDigest>().SendDueAsync(now);
    }

    private static Task<List<OutboundMessage>> DigestsAsync(CustomWebApplicationFactory app) =>
        app.Services.Read(db => db.OutboundMessages.Where(m => m.Source == OutboundSources.SiteAnalyticsDigest).OrderBy(m => m.To).ToList());

    private static async Task SeedWeekAsync(AnalyticsApp app)
    {
        await app.SeedAsync(
            // Yesterday: three visitors, four page views, one sign-up.
            Event(NoonOf(Yesterday), "v1", "s1", path: "/", source: "google"),
            Event(NoonOf(Yesterday, 1), "v1", "s1", path: "/pricing", source: "google"),
            Event(NoonOf(Yesterday, 2), "v1", "s1", SiteEventTypes.Event, "/login", "signup_completed", source: "google"),
            Event(NoonOf(Yesterday, 30), "v2", "s2", path: "/", source: "linkedin"),
            Event(NoonOf(Yesterday, 60), "v3", "s3", path: "/blog"),
            // The day before: two visitors, two page views.
            Event(NoonOf(Yesterday.AddDays(-1)), "v4", "s4", path: "/"),
            Event(NoonOf(Yesterday.AddDays(-1), 5), "v5", "s5", path: "/"),
            // Tuesday a week before: one.
            Event(NoonOf(Yesterday.AddDays(-7)), "v6", "s6", path: "/"),
            // Today: not in yesterday's email.
            Event(Morning.AddMinutes(-5), "v7", "s7", path: "/today"));
    }

    [Fact]
    public void It_is_due_from_half_past_seven_in_Zurich_and_reports_on_the_day_before()
    {
        SiteAnalyticsDigestService.DayFor(new DateTime(2026, 11, 25, 6, 29, 0, DateTimeKind.Utc)).Should().BeNull("07:29 in Zurich");
        SiteAnalyticsDigestService.DayFor(new DateTime(2026, 11, 25, 6, 30, 0, DateTimeKind.Utc)).Should().Be(Yesterday);
        SiteAnalyticsDigestService.DayFor(new DateTime(2026, 11, 25, 22, 59, 0, DateTimeKind.Utc)).Should().Be(Yesterday, "23:59 in Zurich");
        // Summer time: 07:30 in Zurich is 05:30 UTC.
        SiteAnalyticsDigestService.DayFor(new DateTime(2026, 7, 1, 5, 30, 0, DateTimeKind.Utc)).Should().Be(new DateOnly(2026, 6, 30));
    }

    [Fact]
    public async Task Each_platform_admin_gets_yesterdays_figures_once_through_the_outbox()
    {
        await using var app = new AnalyticsApp();
        await SeedWeekAsync(app);

        (await SendAsync(app, Morning.AddMinutes(-2))).Should().Be(0, "07:29 in Zurich: not yet");
        (await app.Services.Read(db => db.SiteAnalyticsDigests.Count())).Should().Be(0);

        (await SendAsync(app, Morning)).Should().Be(2);

        var digests = await DigestsAsync(app);
        digests.Select(d => d.To).Should().Equal(AnalyticsApp.AdminEmail, AnalyticsApp.SecondAdminConfigured.ToLowerInvariant());
        digests.Should().OnlyContain(d => d.OrganizationId == null && d.CreatedByUserId == null && d.Status == OutboundStatuses.Queued
            && d.Channel == OutboundChannels.Email && d.UnsubscribeUrl == null);
        var digest = digests[0];
        digest.Subject.Should().Be("cadencee.ai yesterday: 3 visitors, 4 page views, 1 sign-up");
        digest.TextBody.Should().Contain("cadencee.ai on Tuesday 24 November 2026 (Europe/Zurich)")
            .And.Contain("Visitors: 3   (+1 (+50%) vs the day before, +2 (+200%) vs last Tuesday)")
            .And.Contain("Sessions: 3   (+1 (+50%) vs the day before, +2 (+200%) vs last Tuesday)")
            .And.Contain("Page views: 4   (+2 (+100%) vs the day before, +3 (+300%) vs last Tuesday)")
            .And.Contain("Sign-ups: 1   (+1 (new) vs the day before, +1 (new) vs last Tuesday)")
            .And.Contain("Last 7 days (visitors): ▁▁▁▁▁▆█")
            .And.Contain("1. /  2 views, 2 visitors")
            .And.Contain("1. direct  1 session, 1 visitor")
            .And.Contain($"{AnalyticsApp.FrontendBaseUrl}/admin/analytics")
            .And.NotContain("/today");
        digest.HtmlBody.Should().Contain($"href=\"{AnalyticsApp.FrontendBaseUrl}/admin/analytics\"").And.Contain("<table");

        var claim = (await app.Services.Read(db => db.SiteAnalyticsDigests.ToList())).Should().ContainSingle().Subject;
        (claim.Day, claim.Recipients).Should().Be((Yesterday, 2));

        // Later the same morning, and after a restart (a new job and a new scope): no second one.
        (await SendAsync(app, Morning.AddHours(3))).Should().Be(0);
        var restarted = new SiteAnalyticsDigestBackgroundService(app.Services.GetRequiredService<IServiceScopeFactory>(), new JobHeartbeats(),
            NullLogger<SiteAnalyticsDigestBackgroundService>.Instance);
        (await restarted.RunAsync(Morning.AddHours(4), default)).Should().Be(0);
        (await DigestsAsync(app)).Should().HaveCount(2);

        // The outbox sends them as it sends everything, with no workspace behind them.
        await app.DispatchOutboxAsync();
        app.Mail.SentTo.Should().BeEquivalentTo(AnalyticsApp.AdminEmail, AnalyticsApp.SecondAdminConfigured.ToLowerInvariant());
        (await DigestsAsync(app)).Should().OnlyContain(d => d.Status == OutboundStatuses.Sent);

        // The next morning: the next day's.
        (await SendAsync(app, Morning.AddDays(1))).Should().Be(2);
    }

    [Fact]
    public async Task It_counts_the_websites_visitors_and_their_sign_ups_not_the_apps_signed_in_use()
    {
        await using var app = new AnalyticsApp(admins: AnalyticsApp.AdminEmail);
        await app.SeedAsync(
            // A visitor reads the home page, signs up (recorded once signed in) and lands in the app.
            Event(NoonOf(Yesterday), "v1", "s1", path: "/", source: "google"),
            Event(NoonOf(Yesterday, 1), "v1", "s1", SiteEventTypes.Event, "/login", "signup_completed", source: "google", signedIn: true),
            Event(NoonOf(Yesterday, 2), "v1", "s1", path: "/dashboard", source: "google", signedIn: true),
            Event(NoonOf(Yesterday, 30), "v2", "s2", path: "/pricing"),
            // The owner working in the CRM, signed in: not a visitor of the website.
            Event(NoonOf(Yesterday, 60), "v9", "s9", path: "/deals", signedIn: true),
            Event(NoonOf(Yesterday, 61), "v9", "s9", path: "/leads/:id", signedIn: true),
            Event(NoonOf(Yesterday.AddDays(-1)), "v8", "s8", path: "/dashboard", signedIn: true));

        (await SendAsync(app, Morning)).Should().Be(1);

        var digest = (await DigestsAsync(app)).Should().ContainSingle().Subject;
        digest.Subject.Should().Be("cadencee.ai yesterday: 2 visitors, 2 page views, 1 sign-up");
        digest.TextBody.Should().Contain("Visitors: 2   (+2 (new) vs the day before, +2 (new) vs last Tuesday)")
            .And.Contain("Sign-ups: 1")
            .And.Contain("1. /  1 view, 1 visitor")
            .And.Contain("2. /pricing  1 view, 1 visitor")
            .And.Contain("Website visitors only")
            .And.NotContain("/deals").And.NotContain("/leads/:id").And.NotContain("/dashboard");
        digest.HtmlBody.Should().Contain("Website visitors only").And.NotContain("/deals");
    }

    [Fact]
    public async Task A_quiet_day_is_reported_too()
    {
        await using var app = new AnalyticsApp(admins: AnalyticsApp.AdminEmail);

        (await SendAsync(app, Morning)).Should().Be(1);

        var digest = (await DigestsAsync(app)).Should().ContainSingle().Subject;
        digest.Subject.Should().Be("cadencee.ai yesterday: 0 visitors, 0 page views");
        digest.TextBody.Should().Contain("Visitors: 0   (no change vs the day before, no change vs last Tuesday)")
            .And.Contain("Last 7 days (visitors): ▁▁▁▁▁▁▁");
    }

    [Fact]
    public async Task With_no_platform_admin_nothing_is_sent_and_the_day_is_not_taken()
    {
        await using var app = new AnalyticsApp(admins: "");
        await SeedWeekAsync(app);

        (await SendAsync(app, Morning)).Should().Be(0);

        (await DigestsAsync(app)).Should().BeEmpty();
        (await app.Services.Read(db => db.SiteAnalyticsDigests.Count())).Should().Be(0, "an admin added later that morning still gets the day's");
    }

    [Theory]
    [InlineData(3, 2, "+1 (+50%)")]
    [InlineData(2, 4, "-2 (-50%)")]
    [InlineData(5, 0, "+5 (new)")]
    [InlineData(0, 3, "-3 (-100%)")]
    [InlineData(4, 4, "no change")]
    [InlineData(1, 3, "-2 (-67%)")]
    public void A_change_reads_as_a_difference_and_a_share(int now, int before, string change)
    {
        SiteAnalyticsDigestService.Change(now, before).Should().Be(change);
    }

    [Fact]
    public void The_week_is_a_line_of_bars_scaled_to_its_busiest_day()
    {
        SiteAnalyticsDigestService.Sparkline([0, 1, 2, 3, 4, 5, 7]).Should().Be("▁▂▃▄▅▆█");
        SiteAnalyticsDigestService.Sparkline([0, 0, 0, 0, 0, 0, 0]).Should().Be("▁▁▁▁▁▁▁");
        SiteAnalyticsDigestService.Sparkline([10, 10]).Should().Be("██");
    }

    [Fact]
    public async Task Events_are_deleted_400_days_after_their_day_and_robots_counts_and_claims_with_them()
    {
        await using var app = new AnalyticsApp();
        var now = Morning;
        var today = SiteAnalyticsRules.DayOf(now);
        await app.SeedAsync(
            Event(NoonOf(today.AddDays(-401)), "v1", "s1", path: "/gone"),
            Event(NoonOf(today.AddDays(-400)), "v2", "s2", path: "/kept"),
            Event(NoonOf(today.AddDays(-1)), "v3", "s3", path: "/recent"));
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SiteBotHits.AddRange(new SiteBotHit { Day = today.AddDays(-401), Bot = "googlebot", Hits = 3 }, new SiteBotHit { Day = today, Bot = "googlebot", Hits = 1 });
            db.SiteAnalyticsDigests.Add(new SiteAnalyticsDigest { Day = today.AddDays(-402), SentAtUtc = now.AddDays(-401), Recipients = 1 });
            await db.SaveChangesAsync();
        }

        // Through the daily job, which cleans up on its first pass of the day.
        var job = new SiteAnalyticsDigestBackgroundService(app.Services.GetRequiredService<IServiceScopeFactory>(), new JobHeartbeats(),
            NullLogger<SiteAnalyticsDigestBackgroundService>.Instance);
        await job.RunAsync(now.AddHours(-5), default);

        (await app.Services.SiteEventsAsync()).Select(e => e.Path).Should().Equal("/kept", "/recent");
        (await app.Services.BotHitsAsync()).Select(h => h.Day).Should().Equal(today);
        (await app.Services.Read(db => db.SiteAnalyticsDigests.Count())).Should().Be(0);

        using var again = app.Services.CreateScope();
        (await again.ServiceProvider.GetRequiredService<ISiteAnalytics>().PurgeAsync(now)).Should().Be(0, "nothing older is left");
    }

    [Fact]
    public async Task The_job_says_it_is_alive_as_a_daily_job_named_site_analytics_digest()
    {
        await using var app = new AnalyticsApp();
        var heartbeats = new JobHeartbeats();
        var job = new SiteAnalyticsDigestBackgroundService(app.Services.GetRequiredService<IServiceScopeFactory>(), heartbeats,
            NullLogger<SiteAnalyticsDigestBackgroundService>.Instance);

        await job.StartAsync(CancellationToken.None);
        await job.StopAsync(CancellationToken.None);

        var beat = heartbeats.Snapshot().Should().ContainSingle().Subject;
        (beat.Job, beat.Interval).Should().Be(("site-analytics-digest", TimeSpan.FromHours(24)));
        SiteAnalyticsDigestBackgroundService.Job.Should().Be("site-analytics-digest");
    }
}
