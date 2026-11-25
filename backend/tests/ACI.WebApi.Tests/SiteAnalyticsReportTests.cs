using System.Net;
using System.Reflection;
using System.Text.Json;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.Infrastructure.Services.SiteAnalytics;
using ACI.WebApi.Controllers;
using ACI.WebApi.Filters;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.SiteAnalyticsWorld;

namespace ACI.WebApi.Tests;

/// <summary>
/// What platform admins read back (GET /api/site-analytics/summary, /live, /sessions): the figures
/// counted right on seeded events, every day present, the period before compared, and each query
/// one SQL Server can run.
/// </summary>
public class SiteAnalyticsReportTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // A Monday to a Wednesday in October (summer time in Zurich), and the three days before.
    private static readonly DateOnly From = new(2026, 10, 5);
    private static readonly DateOnly Quiet = new(2026, 10, 6);
    private static readonly DateOnly To = new(2026, 10, 7);

    private static async Task<HttpClient> AdminAsync(AnalyticsApp app) => (await app.ConfirmedAsync(AnalyticsApp.AdminEmail)).Client;

    private static async Task<T> ReadAsync<T>(HttpResponseMessage res)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return JsonSerializer.Deserialize<T>(body, Json)!;
    }

    private static async Task SeedPeriodAsync(AnalyticsApp app)
    {
        await app.SeedAsync(
            // From: a visitor reads two pages, clicks the sign-up button and signs up (from Google).
            Event(NoonOf(From), "v1", "s1", path: "/", source: "google", referrer: "google.com"),
            Event(NoonOf(From, 1), "v1", "s1", path: "/pricing", source: "google", referrer: "google.com"),
            Event(NoonOf(From, 2), "v1", "s1", SiteEventTypes.Click, "/pricing", "cta-signup", source: "google", referrer: "google.com"),
            Event(NoonOf(From, 3), "v1", "s1", SiteEventTypes.Event, "/login", "signup_completed", source: "google", referrer: "google.com"),
            // Another visitor opens one page on a phone and leaves: a bounce.
            Event(NoonOf(From, 60), "v2", "s2", path: "/", device: "mobile", browser: "Safari", os: "iOS", language: "fr"),
            // The first visitor comes back the same afternoon from the newsletter: a second session.
            Event(NoonOf(From, 180), "v1", "s3", path: "/blog", source: "newsletter", campaign: "launch", utmSource: "newsletter", utmMedium: "email"),
            Event(NoonOf(From, 180).AddSeconds(30), "v1", "s3", path: "/", source: "newsletter", campaign: "launch", utmSource: "newsletter", utmMedium: "email"),
            // To: someone from LinkedIn, one page (the first visitor's hash is another on another day).
            Event(NoonOf(To), "v4", "s4", path: "/", source: "linkedin", referrer: "linkedin.com"),
            // The period before: one bounce.
            Event(NoonOf(From.AddDays(-2)), "v5", "s5", path: "/pricing"),
            // Outside both: not counted.
            Event(NoonOf(To.AddDays(1)), "v6", "s6", path: "/"),
            Event(NoonOf(From.AddDays(-4)), "v7", "s7", path: "/"));
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.SiteBotHits.AddRange(
            new SiteBotHit { Day = To, Bot = "googlebot", Hits = 5 },
            new SiteBotHit { Day = To.AddDays(1), Bot = "googlebot", Hits = 50 });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task The_summary_counts_visitors_sessions_bounces_and_sources_and_compares_the_period_before()
    {
        await using var app = new AnalyticsApp();
        var admin = await AdminAsync(app);
        await SeedPeriodAsync(app);

        var summary = await ReadAsync<SiteSummaryDto>(await admin.GetAsync($"/api/site-analytics/summary?from={From:yyyy-MM-dd}&to={To:yyyy-MM-dd}"));

        (summary.From, summary.To).Should().Be(("2026-10-05", "2026-10-07"));
        // Visitors: two on the 5th (the first visitor's two sessions are one visitor that day), one on the 7th.
        summary.Totals.Should().Be(new SiteTotalsDto(
            Visitors: 3, Sessions: 4, PageViews: 6, BounceRate: 0.5, PagesPerSession: 1.5, AvgSessionSeconds: 52.5, Signups: 1, BotHits: 5));
        summary.Previous.Should().Be(new SiteTotalsDto(1, 1, 1, 1, 1, 0, 0, 0));
        summary.Daily.Should().Equal(
            new SiteDailyDto("2026-10-05", 2, 3, 5, 1),
            new SiteDailyDto("2026-10-06", 0, 0, 0, 0),
            new SiteDailyDto("2026-10-07", 1, 1, 1, 0));

        summary.TopPages.Should().Equal(new SitePageDto("/", 4, 3), new SitePageDto("/blog", 1, 1), new SitePageDto("/pricing", 1, 1));
        summary.EntryPages.Should().Equal(new SiteEntryPageDto("/", 3, 3, 3), new SiteEntryPageDto("/blog", 1, 1, 1));
        summary.ExitPages.Should().Equal(new SiteEntryPageDto("/", 3, 3, 3), new SiteEntryPageDto("/pricing", 1, 1, 1));
        summary.Sources.Should().Equal(
            new SiteSourceDto("direct", 1, 1), new SiteSourceDto("google", 1, 1), new SiteSourceDto("linkedin", 1, 1), new SiteSourceDto("newsletter", 1, 1));
        summary.Referrers.Should().Equal(new SiteReferrerDto("google.com", 1), new SiteReferrerDto("linkedin.com", 1));
        summary.Campaigns.Should().Equal(new SiteCampaignDto("launch", "newsletter", "email", 1));
        summary.Devices.Should().Equal(new SiteNamedCountDto("desktop", 3), new SiteNamedCountDto("mobile", 1));
        summary.Browsers.Should().Equal(new SiteNamedCountDto("Chrome", 3), new SiteNamedCountDto("Safari", 1));
        summary.Os.Should().Equal(new SiteNamedCountDto("Windows", 3), new SiteNamedCountDto("iOS", 1));
        summary.Languages.Should().Equal(new SiteNamedCountDto("de", 3), new SiteNamedCountDto("fr", 1));
        summary.Events.Should().Equal(new SiteEventCountDto("cta-signup", 1), new SiteEventCountDto("signup_completed", 1));
    }

    [Fact]
    public async Task The_summary_answers_in_the_agreed_shape()
    {
        await using var app = new AnalyticsApp();
        var admin = await AdminAsync(app);
        await SeedPeriodAsync(app);

        var res = await admin.GetAsync($"/api/site-analytics/summary?from={From:yyyy-MM-dd}&to={To:yyyy-MM-dd}");
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        root.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            "from", "to", "totals", "previous", "daily", "topPages", "entryPages", "exitPages", "sources", "referrers", "campaigns",
            "devices", "browsers", "os", "languages", "events");
        root.GetProperty("totals").EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo(
            "visitors", "sessions", "pageViews", "bounceRate", "pagesPerSession", "avgSessionSeconds", "signups", "botHits");
        root.GetProperty("daily")[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("date", "visitors", "sessions", "pageViews", "signups");
        root.GetProperty("topPages")[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("path", "views", "visitors");
        root.GetProperty("entryPages")[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("path", "sessions", "visitors", "views");
        root.GetProperty("sources")[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("source", "sessions", "visitors");
        root.GetProperty("referrers")[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("host", "sessions");
        root.GetProperty("campaigns")[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("campaign", "source", "medium", "sessions");
        root.GetProperty("devices")[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("name", "sessions");
        root.GetProperty("events")[0].EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("name", "count");
    }

    [Fact]
    public async Task The_summary_covers_the_last_30_days_by_default_and_refuses_a_period_it_cannot_read()
    {
        await using var app = new AnalyticsApp();
        var admin = await AdminAsync(app);

        var summary = await ReadAsync<SiteSummaryDto>(await admin.GetAsync("/api/site-analytics/summary"));
        var today = SiteAnalyticsRules.DayOf(DateTime.UtcNow);
        summary.To.Should().Be(SiteAnalyticsRules.Format(today));
        summary.From.Should().Be(SiteAnalyticsRules.Format(today.AddDays(-29)));
        summary.Daily.Should().HaveCount(30).And.OnlyContain(d => d.Visitors == 0 && d.PageViews == 0);
        summary.Totals.Should().Be(new SiteTotalsDto(0, 0, 0, 0, 0, 0, 0, 0));

        foreach (var query in new[] { "?from=yesterday", "?to=2026-13-01", "?from=2026-10-07&to=2026-10-05", "?from=2024-01-01&to=2026-01-01" })
            (await admin.GetAsync("/api/site-analytics/summary" + query)).StatusCode.Should().Be(HttpStatusCode.BadRequest, query);
    }

    [Fact]
    public async Task Live_says_who_is_on_the_site_now_where_and_the_last_events()
    {
        await using var app = new AnalyticsApp();
        var admin = await AdminAsync(app);
        var now = DateTime.UtcNow;
        await app.SeedAsync(
            Event(now.AddMinutes(-10), "v3", "s3", path: "/old"),
            Event(now.AddMinutes(-2), "v2", "s2", path: "/", device: "mobile"),
            Event(now.AddMinutes(-1), "v1", "s1", path: "/pricing", source: "google"),
            Event(now.AddSeconds(-30), "v1", "s1", SiteEventTypes.Click, "linkedin.com", "outbound", source: "google"));

        var live = await ReadAsync<SiteLiveDto>(await admin.GetAsync("/api/site-analytics/live"));

        (live.VisitorsNow, live.SessionsNow).Should().Be((2, 2));
        // Where each is: the page of their latest event, not the site they clicked away to.
        live.Pages.Should().Equal(new SiteLivePageDto("/", 1), new SiteLivePageDto("/pricing", 1));
        live.Recent.Select(r => (r.Type, r.Path, r.Name, r.Source, r.Device)).Should().Equal(
            (SiteEventTypes.Click, "linkedin.com", "outbound", "google", "desktop"),
            (SiteEventTypes.PageView, "/pricing", null, "google", "desktop"),
            (SiteEventTypes.PageView, "/", null, "direct", "mobile"),
            (SiteEventTypes.PageView, "/old", null, "direct", "desktop"));
        live.Recent.Select(r => r.At).Should().BeInDescendingOrder();
    }

    [Fact]
    public async Task Sessions_lists_a_days_visits_newest_first_with_their_steps()
    {
        await using var app = new AnalyticsApp();
        var admin = await AdminAsync(app);
        var day = new DateOnly(2026, 11, 3);
        var morning = NoonOf(day, -180);
        await app.SeedAsync(
            Event(morning, "v1", "s1", path: "/", source: "google", referrer: "google.com"),
            Event(morning.AddSeconds(20), "v1", "s1", SiteEventTypes.Click, "/", "cta-signup", source: "google", referrer: "google.com"),
            Event(morning.AddSeconds(95), "v1", "s1", SiteEventTypes.Event, "/login", "signup_completed", source: "google", referrer: "google.com", signedIn: true),
            Event(NoonOf(day, -60), "v2", "s2", path: "/pricing", source: "newsletter", campaign: "launch", language: "fr"),
            Event(NoonOf(day.AddDays(1)), "v3", "s3", path: "/"));

        var sessions = await ReadAsync<List<SiteSessionDto>>(await admin.GetAsync($"/api/site-analytics/sessions?day={day:yyyy-MM-dd}"));

        sessions.Should().HaveCount(2);
        var (latest, first) = (sessions[0], sessions[1]);
        latest.Should().BeEquivalentTo(new
        {
            Source = "newsletter", ReferrerHost = (string?)null, Campaign = "launch", Language = "fr", SignedIn = false, PageViews = 1,
            Converted = false, DurationSeconds = 0,
        });
        first.Should().BeEquivalentTo(new
        {
            Source = "google", ReferrerHost = "google.com", Campaign = (string?)null, Device = "desktop", Browser = "Chrome", Language = "de",
            SignedIn = true, PageViews = 1, Converted = true, DurationSeconds = 95,
        });
        first.StartedAt.Should().Be(morning);
        first.EndedAt.Should().Be(morning.AddSeconds(95));
        first.Steps.Select(s => (s.Type, s.Path, s.Name)).Should().Equal(
            (SiteEventTypes.PageView, "/", null),
            (SiteEventTypes.Click, "/", "cta-signup"),
            (SiteEventTypes.Event, "/login", "signup_completed"));
        // Short and opaque, not the stored key.
        sessions.Should().OnlyContain(s => s.Id.Length == 12);
        sessions.Select(s => s.Id).Should().OnlyHaveUniqueItems();

        (await ReadAsync<List<SiteSessionDto>>(await admin.GetAsync($"/api/site-analytics/sessions?day={day:yyyy-MM-dd}&limit=1")))
            .Should().ContainSingle().Which.Source.Should().Be("newsletter");
        (await ReadAsync<List<SiteSessionDto>>(await admin.GetAsync("/api/site-analytics/sessions"))).Should().BeEmpty("nothing today");
        (await admin.GetAsync("/api/site-analytics/sessions?day=3.11.2026")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_post_from_the_tracker_shows_in_the_summary_the_live_view_and_the_sessions()
    {
        await using var app = new AnalyticsApp();
        var admin = await AdminAsync(app);
        var browser = app.Browser();

        (await browser.PostAsync(Endpoint, Events(
            new { type = "page_view", path = "/", sessionId = "abcdefgh12345678", referrer = "https://www.linkedin.com/feed/" },
            new { type = "click", name = "cta-signup", path = "/", sessionId = "abcdefgh12345678", referrer = "https://www.linkedin.com/feed/" }))).StatusCode
            .Should().Be(HttpStatusCode.NoContent);

        var today = SiteAnalyticsRules.DayOf(DateTime.UtcNow);
        var summary = await ReadAsync<SiteSummaryDto>(await admin.GetAsync($"/api/site-analytics/summary?from={today:yyyy-MM-dd}&to={today:yyyy-MM-dd}"));
        summary.Totals.Should().BeEquivalentTo(new { Visitors = 1, Sessions = 1, PageViews = 1, BounceRate = 0.0 });
        summary.Sources.Should().Equal(new SiteSourceDto("linkedin", 1, 1));
        (await ReadAsync<SiteLiveDto>(await admin.GetAsync("/api/site-analytics/live"))).VisitorsNow.Should().Be(1);
        (await ReadAsync<List<SiteSessionDto>>(await admin.GetAsync("/api/site-analytics/sessions"))).Should().ContainSingle()
            .Which.Steps.Should().HaveCount(2);
    }

    [Fact]
    public void Every_read_is_for_platform_admins_and_only_the_trackers_post_is_anonymous()
    {
        var actions = typeof(SiteAnalyticsController).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).ToList();
        actions.Select(a => a.Name).Should().BeEquivalentTo("Events", "Summary", "Live", "Sessions");

        foreach (var read in actions.Where(a => a.Name != "Events"))
        {
            read.GetCustomAttributes<HttpGetAttribute>().Should().ContainSingle(read.Name);
            read.GetCustomAttribute<PlatformAdminAttribute>().Should().NotBeNull($"{read.Name} shows the site's visits");
            read.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull(read.Name);
            read.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull(read.Name);
        }
        var ingest = actions.Single(a => a.Name == "Events");
        ingest.GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();
        ingest.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName.Should().Be(SiteAnalyticsController.RateLimitPolicy);
        ingest.GetCustomAttribute<RequestSizeLimitAttribute>().Should().NotBeNull();
    }

    [Fact]
    public void Every_query_translates_to_SQL_Server()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=not-connected;Database=none;").Options);
        var day = new DateOnly(2026, 10, 5);

        var daily = SiteAnalyticsQueries.Daily(db, day, day.AddDays(6)).ToQueryString();
        daily.Should().Contain("COUNT(DISTINCT").And.Contain("GROUP BY");
        SiteAnalyticsQueries.Sessions(db, day, day).Take(100).ToQueryString().Should().Contain("MAX(");
        SiteAnalyticsQueries.TopPages(db, day, day).ToQueryString().Should().Contain("TOP(");
        SiteAnalyticsQueries.EntryPages(db, day, day).ToQueryString().Should().Contain("JOIN").And.Contain("MIN(");
        SiteAnalyticsQueries.ExitPages(db, day, day).ToQueryString().Should().Contain("JOIN").And.Contain("MAX(");
        SiteAnalyticsQueries.Events(db, day, day).ToQueryString().Should().Contain("GROUP BY");
        SiteAnalyticsQueries.Since(db, DateTime.UtcNow, 10).ToQueryString().Should().Contain("ORDER BY");
        SiteAnalyticsQueries.Latest(db, 30).ToQueryString().Should().Contain("ORDER BY");
        SiteAnalyticsQueries.LatestSessions(db, day, 50).ToQueryString().Should().Contain("GROUP BY");
        SiteAnalyticsQueries.Steps(db, day, ["a", "b"]).ToQueryString().Should().Contain("ORDER BY");

        // The site analytics are in no workspace: no workspace filter is on their tables.
        foreach (var type in new[] { typeof(SiteEvent), typeof(SiteBotHit), typeof(SiteAnalyticsSecret), typeof(SiteAnalyticsDigest) })
        {
            var entity = db.Model.FindEntityType(type)!;
            entity.GetQueryFilter().Should().BeNull(type.Name);
            entity.FindProperty("OrganizationId").Should().BeNull(type.Name);
        }
    }
}
