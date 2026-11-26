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
/// and the same weekday a week earlier, to every platform admin through the outbox, once a day,
/// with what visitors sent defanged; and the daily clean-up of events older than 400 days and of
/// salts older than yesterday's.
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
        digest.TextBody.Should().StartWith("Yesterday on cadencee.ai\n\nTuesday 24 November 2026 (Europe/Zurich)")
            .And.Contain("Visitors: 3\nPage views: 4\nSign-ups: 1\n")
            .And.Contain("- Visitors: Yesterday 3, vs the day before +1 (+50%), vs last Tuesday +2 (+200%)")
            .And.Contain("- Sessions: Yesterday 3, vs the day before +1 (+50%), vs last Tuesday +2 (+200%)")
            .And.Contain("- Page views: Yesterday 4, vs the day before +2 (+100%), vs last Tuesday +3 (+300%)")
            .And.Contain("- Sign-ups: Yesterday 1, vs the day before +1 (new), vs last Tuesday +1 (new)")
            .And.Contain("Visitors, day by day: ▁▁▁▁▁▆█")
            .And.Contain("- Tue 24 Nov: Visitors 3, Page views 4, Sign-ups 1")
            .And.Contain("- /: Views 2, Visitors 2")
            .And.Contain("- direct: Sessions 1, Visitors 1")
            .And.Contain($"{AnalyticsApp.FrontendBaseUrl}/admin/analytics")
            .And.NotContain("/today");
        // CadenceE's design: the figures large, then tables with their headers.
        digest.HtmlBody.Should().Contain($"href=\"{AnalyticsApp.FrontendBaseUrl}/admin/analytics\"").And.Contain("<table")
            .And.Contain("class=\"em-stat\"").And.Contain(">vs last Tuesday<").And.Contain(">Open the dashboard<").And.Contain("&#10022;");

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
        digest.TextBody.Should().Contain("- Visitors: Yesterday 2, vs the day before +2 (new), vs last Tuesday +2 (new)")
            .And.Contain("Sign-ups: 1")
            .And.Contain("- /: Views 1, Visitors 1\n- /pricing: Views 1, Visitors 1")
            .And.Contain("Website visitors only")
            .And.NotContain("/deals").And.NotContain("/leads/:id").And.NotContain("/dashboard");
        digest.HtmlBody.Should().Contain("Website visitors only").And.NotContain("/deals");
    }

    [Fact]
    public async Task What_visitors_sent_is_defanged_and_a_source_is_a_kind_a_host_or_other()
    {
        await using var app = new AnalyticsApp(admins: AnalyticsApp.AdminEmail);
        await app.SeedAsync(
            Event(NoonOf(Yesterday), "v1", "s1", path: "/go/evil.example", source: "visit evil.example now"),
            Event(NoonOf(Yesterday, 1), "v2", "s2", path: "/", source: "pavillon46.ch", referrer: "pavillon46.ch"),
            Event(NoonOf(Yesterday, 2), "v3", "s3", path: "/", source: "newsletter"),
            Event(NoonOf(Yesterday, 3), "v4", "s4", path: "/", source: "linkedin"));

        (await SendAsync(app, Morning)).Should().Be(1);

        var digest = (await DigestsAsync(app)).Should().ContainSingle().Subject;
        foreach (var body in new[] { digest.TextBody, digest.HtmlBody! })
        {
            body.Should().Contain("/go/evil[.]example").And.Contain("pavillon46[.]ch").And.Contain("linkedin");
            body.Should().NotContain("evil.example").And.NotContain("pavillon46.ch").And.NotContain("newsletter").And.NotContain("visit evil");
        }
        digest.TextBody.Should().Contain("- other: Sessions 2, Visitors 2\n- linkedin: Sessions 1, Visitors 1\n- pavillon46[.]ch: Sessions 1, Visitors 1");
        // Never something to click: a table's cells are text.
        digest.HtmlBody.Should().NotContain("href=\"http://evil").And.NotContain("href=\"/go");
        digest.HtmlBody.Should().Contain($"href=\"{AnalyticsApp.FrontendBaseUrl}/admin/analytics\"", "the dashboard's own link is not defanged");
    }

    [Theory]
    [InlineData("example.com", "example[.]com")]
    [InlineData("/blog/crm.for.clubs", "/blog/crm[.]for[.]clubs")]
    [InlineData("/pricing", "/pricing")]
    public void A_dot_is_written_so_no_mail_client_links_it(string text, string defanged)
    {
        SiteAnalyticsDigestService.Defang(text).Should().Be(defanged);
    }

    [Fact]
    public void The_email_shows_a_kind_or_a_host_as_it_is_and_any_other_source_as_other()
    {
        SiteAnalyticsDigestService.ShownSources([
            new SiteSourceDto("google", 5, 4),
            new SiteSourceDto("spring sale", 3, 3),
            new SiteSourceDto("pavillon46.ch", 2, 2),
            new SiteSourceDto("newsletter", 2, 1),
            new SiteSourceDto("direct", 1, 1),
        ]).Should().Equal(
            new SiteSourceDto("google", 5, 4),
            new SiteSourceDto("other", 5, 4),
            new SiteSourceDto("pavillon46.ch", 2, 2),
            new SiteSourceDto("direct", 1, 1));
    }

    [Fact]
    public async Task A_quiet_day_is_reported_too()
    {
        await using var app = new AnalyticsApp(admins: AnalyticsApp.AdminEmail);

        (await SendAsync(app, Morning)).Should().Be(1);

        var digest = (await DigestsAsync(app)).Should().ContainSingle().Subject;
        digest.Subject.Should().Be("cadencee.ai yesterday: 0 visitors, 0 page views");
        digest.TextBody.Should().Contain("- Visitors: Yesterday 0, vs the day before no change, vs last Tuesday no change")
            .And.Contain("Visitors, day by day: ▁▁▁▁▁▁▁")
            .And.Contain("Top pages\n---------\nNone yesterday.");
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
            foreach (var day in new[] { today, today.AddDays(-1), today.AddDays(-2), today.AddDays(-30) })
                db.SiteAnalyticsSalts.Add(new SiteAnalyticsSalt { Day = day, ProtectedSalt = "x", CreatedAtUtc = now });
            await db.SaveChangesAsync();
        }

        // Through the daily job, which cleans up on its first pass of the day.
        var job = new SiteAnalyticsDigestBackgroundService(app.Services.GetRequiredService<IServiceScopeFactory>(), new JobHeartbeats(),
            NullLogger<SiteAnalyticsDigestBackgroundService>.Instance);
        await job.RunAsync(now.AddHours(-5), default);

        (await app.Services.SiteEventsAsync()).Select(e => e.Path).Should().Equal("/kept", "/recent");
        (await app.Services.BotHitsAsync()).Select(h => h.Day).Should().Equal(today);
        (await app.Services.Read(db => db.SiteAnalyticsDigests.Count())).Should().Be(0);
        (await app.Services.Read(db => db.SiteAnalyticsSalts.Select(s => s.Day).OrderBy(d => d).ToList()))
            .Should().Equal([today.AddDays(-1), today], "only today's and yesterday's salts are kept");

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
