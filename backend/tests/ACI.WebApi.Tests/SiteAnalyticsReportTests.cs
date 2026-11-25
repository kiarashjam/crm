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

    /// <summary>
    /// One day of the website and the app at once: a visitor who reads two pages, clicks, signs up
    /// (the conversion is recorded just after signing in) and lands in the app; another who
    /// bounces; and the owner working in the CRM signed in. The day before: one of each.
    /// </summary>
    private static async Task SeedAudiencesAsync(AnalyticsApp app)
    {
        await app.SeedAsync(
            Event(NoonOf(From), "v1", "s1", path: "/", source: "google", referrer: "google.com"),
            Event(NoonOf(From, 1), "v1", "s1", path: "/pricing", source: "google", referrer: "google.com"),
            Event(NoonOf(From, 2), "v1", "s1", SiteEventTypes.Click, "/pricing", "cta-signup", source: "google", referrer: "google.com"),
            Event(NoonOf(From, 3), "v1", "s1", SiteEventTypes.Event, "/login", "signup_completed", source: "google", referrer: "google.com", signedIn: true),
            Event(NoonOf(From, 4), "v1", "s1", path: "/dashboard", source: "google", referrer: "google.com", signedIn: true),
            Event(NoonOf(From, 60), "v2", "s2", path: "/", device: "mobile"),
            Event(NoonOf(From, 120), "v9", "s9", path: "/leads/:id", signedIn: true),
            Event(NoonOf(From, 121), "v9", "s9", path: "/deals", signedIn: true),
            Event(NoonOf(From, 122), "v9", "s9", SiteEventTypes.Click, "/deals", "save-deal", signedIn: true),
            Event(NoonOf(From.AddDays(-1)), "v5", "s5", path: "/pricing"),
            Event(NoonOf(From.AddDays(-1), 30), "v8", "s8", path: "/dashboard", signedIn: true));
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.SiteBotHits.Add(new SiteBotHit { Day = From, Bot = "googlebot", Hits = 4 });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task The_summary_counts_the_websites_visitors_by_default_and_signed_in_use_or_everyone_on_request()
    {
        await using var app = new AnalyticsApp();
        var admin = await AdminAsync(app);
        await SeedAudiencesAsync(app);
        var day = $"from={From:yyyy-MM-dd}&to={From:yyyy-MM-dd}";
        async Task<SiteSummaryDto> SummaryAsync(string audience) =>
            await ReadAsync<SiteSummaryDto>(await admin.GetAsync($"/api/site-analytics/summary?{day}{audience}"));

        // The website's visitors: what was done signed out, and the sign-up recorded once signed in.
        var visitors = await SummaryAsync("&audience=visitors");
        visitors.Totals.Should().Be(new SiteTotalsDto(
            Visitors: 2, Sessions: 2, PageViews: 3, BounceRate: 0.5, PagesPerSession: 1.5, AvgSessionSeconds: 90, Signups: 1, BotHits: 4));
        visitors.Previous.Should().Be(new SiteTotalsDto(1, 1, 1, 1, 1, 0, 0, 0));
        visitors.Daily.Should().Equal(new SiteDailyDto("2026-10-05", 2, 2, 3, 1));
        visitors.TopPages.Should().Equal(new SitePageDto("/", 2, 2), new SitePageDto("/pricing", 1, 1));
        visitors.EntryPages.Select(p => p.Path).Should().Equal("/");
        visitors.ExitPages.Select(p => p.Path).Should().Equal("/", "/pricing");
        visitors.Sources.Should().Equal(new SiteSourceDto("direct", 1, 1), new SiteSourceDto("google", 1, 1));
        visitors.Events.Should().Equal(new SiteEventCountDto("cta-signup", 1), new SiteEventCountDto("signup_completed", 1));

        // Asked for nothing, or in another case: the same.
        (await SummaryAsync("")).Should().BeEquivalentTo(visitors);
        (await SummaryAsync("&audience=Visitors")).Should().BeEquivalentTo(visitors);

        // The app's signed-in users: the new account's first page and its sign-up, and the owner at work.
        var signedIn = await SummaryAsync("&audience=signed-in");
        signedIn.Totals.Should().Be(new SiteTotalsDto(
            Visitors: 2, Sessions: 2, PageViews: 3, BounceRate: 0, PagesPerSession: 1.5, AvgSessionSeconds: 90, Signups: 1, BotHits: 4));
        signedIn.Previous.Should().Be(new SiteTotalsDto(1, 1, 1, 1, 1, 0, 0, 0));
        signedIn.TopPages.Select(p => p.Path).Should().Equal("/dashboard", "/deals", "/leads/:id");
        signedIn.Events.Should().Equal(new SiteEventCountDto("save-deal", 1), new SiteEventCountDto("signup_completed", 1));

        // Everyone: all of it, the sign-up once.
        var everyone = await SummaryAsync("&audience=everyone");
        everyone.Totals.Should().Be(new SiteTotalsDto(
            Visitors: 3, Sessions: 3, PageViews: 6, BounceRate: 0.3333, PagesPerSession: 2, AvgSessionSeconds: 120, Signups: 1, BotHits: 4));
        everyone.Previous.Should().Be(new SiteTotalsDto(2, 2, 2, 1, 1, 0, 0, 0));
        everyone.TopPages.Should().HaveCount(5);
    }

    [Fact]
    public async Task Live_and_the_journeys_follow_the_audience_and_a_journey_is_shown_whole()
    {
        await using var app = new AnalyticsApp();
        var admin = await AdminAsync(app);
        var now = DateTime.UtcNow;
        await app.SeedAsync(
            Event(now.AddMinutes(-1), "v1", "s1", path: "/pricing"),
            Event(now.AddMinutes(-2), "v9", "s9", path: "/deals", signedIn: true),
            Event(now.AddSeconds(-90), "v3", "s3", path: "/login"),
            Event(now.AddSeconds(-30), "v3", "s3", SiteEventTypes.Event, "/login", "login", signedIn: true),
            Event(now.AddSeconds(-20), "v3", "s3", path: "/dashboard", signedIn: true));

        var visitors = await ReadAsync<SiteLiveDto>(await admin.GetAsync("/api/site-analytics/live"));
        (visitors.VisitorsNow, visitors.SessionsNow).Should().Be((2, 2));
        visitors.Pages.Should().Equal(new SiteLivePageDto("/login", 1), new SiteLivePageDto("/pricing", 1));
        visitors.Recent.Select(r => (r.Type, r.Path)).Should().Equal(
            (SiteEventTypes.Event, "/login"), (SiteEventTypes.PageView, "/pricing"), (SiteEventTypes.PageView, "/login"));

        var signedIn = await ReadAsync<SiteLiveDto>(await admin.GetAsync("/api/site-analytics/live?audience=signed-in"));
        (signedIn.VisitorsNow, signedIn.SessionsNow).Should().Be((2, 2));
        signedIn.Pages.Should().Equal(new SiteLivePageDto("/dashboard", 1), new SiteLivePageDto("/deals", 1));
        signedIn.Recent.Select(r => r.Path).Should().Equal("/dashboard", "/login", "/deals");

        var everyone = await ReadAsync<SiteLiveDto>(await admin.GetAsync("/api/site-analytics/live?audience=everyone"));
        (everyone.VisitorsNow, everyone.SessionsNow, everyone.Recent.Count).Should().Be((3, 3, 5));

        // The journeys of a day: the audience picks the visits, each is shown with all its steps.
        var day = new DateOnly(2026, 11, 3);
        var morning = NoonOf(day, -180);
        await app.SeedAsync(
            Event(morning, "v1", "j1", path: "/"),
            Event(morning.AddSeconds(60), "v1", "j1", SiteEventTypes.Event, "/login", "signup_completed", signedIn: true),
            Event(morning.AddSeconds(90), "v1", "j1", path: "/dashboard", signedIn: true),
            Event(NoonOf(day, -60), "v2", "j2", path: "/pricing"),
            Event(NoonOf(day, -30), "v9", "j9", path: "/deals", signedIn: true));
        async Task<List<SiteSessionDto>> JourneysAsync(string audience) =>
            await ReadAsync<List<SiteSessionDto>>(await admin.GetAsync($"/api/site-analytics/sessions?day={day:yyyy-MM-dd}{audience}"));

        var websites = await JourneysAsync("");
        websites.Select(s => s.Steps[0].Path).Should().Equal("/pricing", "/");
        websites[1].Steps.Select(s => s.Path).Should().Equal("/", "/login", "/dashboard");
        websites[1].Should().BeEquivalentTo(new { SignedIn = true, Converted = true, PageViews = 2, DurationSeconds = 90 });

        var apps = await JourneysAsync("&audience=signed-in");
        apps.Select(s => s.Steps[0].Path).Should().Equal("/deals", "/");
        apps[1].Steps.Should().HaveCount(3, "the visit is shown whole, from its first page signed out");

        (await JourneysAsync("&audience=everyone")).Select(s => s.Steps[0].Path).Should().Equal("/deals", "/pricing", "/");
    }

    [Theory]
    [InlineData("/api/site-analytics/summary")]
    [InlineData("/api/site-analytics/live")]
    [InlineData("/api/site-analytics/sessions")]
    public async Task An_audience_other_than_visitors_signed_in_or_everyone_is_refused(string read)
    {
        await using var app = new AnalyticsApp();
        var admin = await AdminAsync(app);

        foreach (var audience in new[] { "bots", "signedin", "signed_in", "all", "visitors,everyone" })
        {
            var res = await admin.GetAsync($"{read}?audience={Uri.EscapeDataString(audience)}");
            res.StatusCode.Should().Be(HttpStatusCode.BadRequest, audience);
            using var problem = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
            problem.RootElement.GetProperty("title").GetString().Should().Be("Not an audience");
            problem.RootElement.GetProperty("detail").GetString().Should()
                .Be("The audience is \"visitors\" (the default), \"signed-in\" or \"everyone\".");
        }
        foreach (var audience in new[] { "", "visitors", "signed-in", "everyone", "Everyone" })
            (await admin.GetAsync($"{read}?audience={audience}")).StatusCode.Should().Be(HttpStatusCode.OK, audience);
    }

    [Theory]
    [InlineData(null, SiteAudience.Visitors)]
    [InlineData("", SiteAudience.Visitors)]
    [InlineData(" visitors ", SiteAudience.Visitors)]
    [InlineData("signed-in", SiteAudience.SignedIn)]
    [InlineData("SIGNED-IN", SiteAudience.SignedIn)]
    [InlineData("everyone", SiteAudience.Everyone)]
    public void An_audience_is_read_whatever_its_case_and_nothing_is_the_websites_visitors(string? text, SiteAudience audience)
    {
        SiteAudiences.TryParse(text, out var read).Should().BeTrue();
        read.Should().Be(audience);
        SiteAudiences.TryParse("signed in", out _).Should().BeFalse();
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

        foreach (var audience in Enum.GetValues<SiteAudience>())
        {
            var queries = new Dictionary<string, string>
            {
                ["Daily"] = SiteAnalyticsQueries.Daily(db, day, day.AddDays(6), audience).ToQueryString(),
                ["Sessions"] = SiteAnalyticsQueries.Sessions(db, day, day, audience).Take(100).ToQueryString(),
                ["TopPages"] = SiteAnalyticsQueries.TopPages(db, day, day, audience).ToQueryString(),
                ["EntryPages"] = SiteAnalyticsQueries.EntryPages(db, day, day, audience).ToQueryString(),
                ["ExitPages"] = SiteAnalyticsQueries.ExitPages(db, day, day, audience).ToQueryString(),
                ["Events"] = SiteAnalyticsQueries.Events(db, day, day, audience).ToQueryString(),
                ["Since"] = SiteAnalyticsQueries.Since(db, DateTime.UtcNow, 10, audience).ToQueryString(),
                ["Latest"] = SiteAnalyticsQueries.Latest(db, 30, audience).ToQueryString(),
                ["LatestSessions"] = SiteAnalyticsQueries.LatestSessions(db, day, 50, audience).ToQueryString(),
            };
            queries["Daily"].Should().Contain("COUNT(DISTINCT").And.Contain("GROUP BY");
            queries["Sessions"].Should().Contain("MAX(");
            queries["TopPages"].Should().Contain("TOP(");
            queries["EntryPages"].Should().Contain("JOIN").And.Contain("MIN(");
            queries["ExitPages"].Should().Contain("JOIN").And.Contain("MAX(");
            queries["Events"].Should().Contain("GROUP BY");
            queries["Since"].Should().Contain("ORDER BY");
            queries["Latest"].Should().Contain("ORDER BY");
            queries["LatestSessions"].Should().Contain("GROUP BY");
            foreach (var (name, sql) in queries)
            {
                // The audience is a WHERE clause in SQL, never a filter in memory.
                if (audience == SiteAudience.Everyone) sql.Should().NotContain("[SignedIn]", $"{name} for everyone");
                else sql.Should().Contain("[SignedIn]", $"{name} for {audience}");
                if (audience == SiteAudience.Visitors) sql.Should().Contain("= 'event'", $"{name} keeps every custom event for the website's visitors");
            }
        }
        // A journey is shown whole, whatever the audience that picked it.
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
