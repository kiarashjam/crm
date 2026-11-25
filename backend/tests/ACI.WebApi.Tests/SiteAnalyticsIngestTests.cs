using System.Net;
using System.Text;
using System.Text.Json;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Services.SiteAnalytics;
using ACI.WebApi.Controllers;
using ACI.WebApi.SiteAnalytics;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.SiteAnalyticsWorld;

namespace ACI.WebApi.Tests;

/// <summary>
/// The tracker's posts (POST /api/site-analytics/events): anonymous, rate limited, size limited,
/// every field checked and clipped, robots counted and dropped, and nothing kept that names
/// anyone or opens anything: no address, no session id, no query string, no token.
/// </summary>
public class SiteAnalyticsIngestTests
{
    private const string Guid1 = "3fa85f64-5717-4562-b3fc-2c963f66afa6";
    private const string SigningToken = "kZ9xQ2hhbmdlTWVQbGVhc2UxMjM0NTY3ODkw";
    private const string SessionId = "a1b2c3d4e5f60718293a4b5c6d7e8f90";

    [Fact]
    public async Task A_post_is_kept_as_templated_events_with_hashes_in_place_of_the_address_and_the_session()
    {
        await using var app = new AnalyticsApp();
        var client = app.Browser(IPhone, "198.51.100.23");
        var at = DateTime.UtcNow.AddSeconds(-3).ToString("o");

        var res = await client.PostAsync(Endpoint, Events(
            new
            {
                type = "page_view", path = "/pricing?utm_source=newsletter&email=jane@example.ch#plans", title = "Pricing — CadenceE",
                referrer = "https://www.google.com/search?q=jane+doe", sessionId = SessionId, at,
                screen = new { w = 390, h = 844 }, lang = "de-CH", signedIn = false,
                utm = new { source = "Newsletter", medium = "email", campaign = "Launch Week", term = "crm", content = "hero" },
            },
            new { type = "page_view", path = $"/leads/{Guid1}?name=Jane%20Doe", title = "Jane Doe » Leads » CadenceE", sessionId = SessionId, signedIn = true },
            new { type = "page_view", path = $"/sign/{SigningToken}", title = "Sign your contract", sessionId = SessionId },
            new { type = "click", name = "CTA Signup", path = "/", sessionId = SessionId },
            new { type = "click", name = "outbound", path = "https://www.linkedin.com/company/cadencee?trk=x", sessionId = SessionId },
            new { type = "event", name = "signup_completed", path = "/login", sessionId = SessionId, signedIn = true }));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var rows = await app.Services.SiteEventsAsync();
        rows.Select(r => (r.Type, r.Name, r.Path)).Should().Equal(
            (SiteEventTypes.PageView, null, "/pricing"),
            (SiteEventTypes.PageView, null, "/leads/:id"),
            (SiteEventTypes.PageView, null, "/sign/:token"),
            (SiteEventTypes.Click, "cta-signup", "/"),
            (SiteEventTypes.Click, "outbound", "linkedin.com"),
            (SiteEventTypes.Event, "signup_completed", "/login"));

        var landing = rows[0];
        landing.Title.Should().Be("Pricing — CadenceE");
        landing.ReferrerHost.Should().Be("google.com");
        landing.Source.Should().Be("newsletter", "utm_source wins over the referrer");
        (landing.UtmSource, landing.UtmMedium, landing.UtmCampaign, landing.UtmTerm, landing.UtmContent)
            .Should().Be(("newsletter", "email", "launch week", "crm", "hero"));
        (landing.Device, landing.Browser, landing.Os).Should().Be(("mobile", "Safari", "iOS"));
        landing.Language.Should().Be("de");
        landing.ScreenBucket.Should().Be("<576");
        landing.SignedIn.Should().BeFalse();
        landing.OccurredAtUtc.Should().BeCloseTo(DateTime.Parse(at).ToUniversalTime(), TimeSpan.FromMilliseconds(1));
        landing.Day.Should().Be(SiteAnalyticsRules.DayOf(landing.OccurredAtUtc));
        // An event without a language takes the browser's Accept-Language.
        rows[1].Language.Should().Be("fr");
        // No title where a record's name could be, nor on a templated page.
        rows[1].Title.Should().BeNull();
        rows[2].Title.Should().BeNull();
        rows[1].SignedIn.Should().BeTrue();

        // One visitor, one session: hashes, the same for every event of the post.
        rows.Select(r => r.VisitorHash).Distinct().Should().ContainSingle().Which.Should().MatchRegex("^[0-9a-f]{32}$");
        rows.Select(r => r.SessionKey).Distinct().Should().ContainSingle().Which.Should().MatchRegex("^[0-9a-f]{32}$");
        rows[0].SessionKey.Should().NotBe(SessionId);

        // Nothing that names anyone or opens anything.
        var stored = JsonSerializer.Serialize(rows);
        foreach (var secret in new[] { "198.51.100.23", SessionId, SigningToken, Guid1, "jane", "Jane", "trk=", "utm_source=", "search?q" })
            stored.Should().NotContain(secret);
    }

    [Fact]
    public async Task It_takes_application_json_too_and_answers_without_a_session()
    {
        await using var app = new AnalyticsApp();

        var res = await app.Browser().PostAsync(Endpoint, Beacon(new { events = new[] { new { type = "page_view", path = "/", sessionId = SessionId } } }, "application/json"));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await app.Services.SiteEventsAsync()).Should().ContainSingle();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json at all")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("null")]
    [InlineData("{\"events\":5}")]
    [InlineData("{\"events\":[1,\"two\",null,[]]}")]
    [InlineData("{\"events\":[{\"type\":\"page_view\",\"path\":\"/\"}]}")]
    [InlineData("{\"events\":[{\"type\":\"page_view\",\"path\":\"/\",\"sessionId\":\"short\"}]}")]
    [InlineData("{\"events\":[{\"type\":\"page_view\",\"path\":\"/\",\"sessionId\":\"has spaces in it!!\"}]}")]
    [InlineData("{\"events\":[{\"type\":\"purchase\",\"path\":\"/\",\"sessionId\":\"a1b2c3d4e5f6\"}]}")]
    [InlineData("{\"events\":[{\"type\":\"page_view\",\"path\":\"leads\",\"sessionId\":\"a1b2c3d4e5f6\"}]}")]
    [InlineData("{\"events\":[{\"type\":\"page_view\",\"path\":42,\"sessionId\":\"a1b2c3d4e5f6\"}]}")]
    [InlineData("{\"events\":[{\"type\":\"click\",\"path\":\"/\",\"sessionId\":\"a1b2c3d4e5f6\"}]}")]
    [InlineData("{\"events\":[{\"type\":\"event\",\"name\":\"!!!\",\"path\":\"/\",\"sessionId\":\"a1b2c3d4e5f6\"}]}")]
    [InlineData("{\"events\":[{\"type\":\"click\",\"name\":\"outbound\",\"path\":\"not a host\",\"sessionId\":\"a1b2c3d4e5f6\"}]}")]
    [InlineData("{\"events\":[{\"type\":\"page_view\",\"path\":\"/\",\"sessionId\":\"a1b2c3d4e5f6\"")]
    public async Task Junk_is_answered_204_and_nothing_is_kept(string body)
    {
        await using var app = new AnalyticsApp();

        var res = await app.Browser().PostAsync(Endpoint, new StringContent(body, Encoding.UTF8, "text/plain"));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await app.Services.SiteEventsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Fields_of_the_wrong_kind_are_left_out_and_the_event_kept()
    {
        await using var app = new AnalyticsApp();

        var res = await app.Browser().PostAsync(Endpoint, new StringContent(
            "{\"events\":[{\"type\":\"page_view\",\"path\":\"/\",\"sessionId\":\"a1b2c3d4e5f6\",\"title\":7,\"referrer\":[],\"utm\":\"x\","
            + "\"screen\":{\"w\":\"wide\"},\"lang\":\"english please\",\"signedIn\":\"yes\",\"at\":\"yesterday\",\"extra\":{\"deep\":{\"x\":1}}}]}",
            Encoding.UTF8, "text/plain"));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var row = (await app.Services.SiteEventsAsync()).Should().ContainSingle().Subject;
        (row.Path, row.Title, row.ReferrerHost, row.UtmSource, row.ScreenBucket, row.SignedIn, row.Source)
            .Should().Be(("/", (string?)null, (string?)null, (string?)null, (string?)null, false, "direct"));
        row.Language.Should().Be("fr", "an unreadable language gives way to the browser's");
        row.OccurredAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Every_field_is_clipped_and_a_post_takes_at_most_25_events()
    {
        await using var app = new AnalyticsApp();
        var long1 = new string('a', 1200);
        var events = Enumerable.Range(0, 3).Select(i => (object)new
        {
            type = "event",
            name = "n" + new string('x', 500),
            path = "/" + string.Join('/', Enumerable.Range(0, 30).Select(_ => "segment")),
            title = long1,
            sessionId = SessionId,
            utm = new { source = long1, medium = long1, campaign = long1, term = long1, content = long1 },
        }).ToArray();

        var res = await app.Browser().PostAsync(Endpoint, Events(events));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var rows = await app.Services.SiteEventsAsync();
        rows.Should().HaveCount(3);

        var many = Enumerable.Range(0, 40).Select(i => (object)new { type = "page_view", path = $"/p{i}", sessionId = SessionId }).ToArray();
        (await app.Browser().PostAsync(Endpoint, Events(many))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await app.Services.SiteEventsAsync()).Where(r => r.Type == SiteEventTypes.PageView).Select(r => r.Path)
            .Should().Equal(Enumerable.Range(0, SiteAnalyticsController.MaxEvents).Select(i => $"/p{i}"), "the first 25 are taken, the rest left");

        rows.Should().OnlyContain(r => r.Name!.Length <= SiteTraffic.MaxName
            && r.Path.Length <= SiteTraffic.MaxPath && r.Path.Split('/', StringSplitOptions.RemoveEmptyEntries).Length == SiteTraffic.MaxSegments
            && r.UtmSource!.Length <= SiteTraffic.MaxUtm && r.UtmCampaign!.Length <= SiteTraffic.MaxUtm && r.Source.Length <= 100
            && r.Title == null);
    }

    [Fact]
    public async Task A_body_over_32KB_is_refused_and_nothing_is_kept()
    {
        await using var app = new AnalyticsApp();
        var client = app.Browser();
        var big = Events(new { type = "page_view", path = "/", sessionId = SessionId, title = new string('x', SiteAnalyticsController.MaxBodyBytes) });

        (await client.PostAsync(Endpoint, big)).StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);

        // Sent without a length, so only reading it finds it too long.
        var streamed = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(new string(' ', 40_000))));
        streamed.Headers.ContentLength = null;
        var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = streamed };
        request.Headers.TransferEncodingChunked = true;
        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);

        (await app.Services.SiteEventsAsync()).Should().BeEmpty();
    }

    [Theory]
    [InlineData(Googlebot, "googlebot")]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/131.0.0.0 Safari/537.36", "headless")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36 Chrome-Lighthouse", "lighthouse")]
    [InlineData("facebookexternalhit/1.1 (+http://www.facebook.com/externalhit_uatext.php)", "preview")]
    [InlineData("Slackbot-LinkExpanding 1.0 (+https://api.slack.com/robots)", "preview")]
    [InlineData("curl/8.4.0", "tool")]
    [InlineData("", "unknown")]
    public async Task A_robot_is_counted_and_not_recorded(string userAgent, string bot)
    {
        await using var app = new AnalyticsApp();
        var client = app.Browser(userAgent);

        for (var i = 0; i < 2; i++)
        {
            var res = await client.PostAsync(Endpoint, Events(
                new { type = "page_view", path = "/", sessionId = SessionId },
                new { type = "page_view", path = "/pricing", sessionId = SessionId }));
            res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        (await app.Services.SiteEventsAsync()).Should().BeEmpty();
        var hits = (await app.Services.BotHitsAsync()).Should().ContainSingle().Subject;
        (hits.Bot, hits.Hits, hits.Day).Should().Be((bot, 4, SiteAnalyticsRules.DayOf(DateTime.UtcNow)));
    }

    [Fact]
    public async Task One_address_may_post_120_times_a_minute_and_another_address_is_not_held_up()
    {
        await using var app = new AnalyticsApp();
        var client = app.Browser(address: "203.0.113.50");
        var empty = () => Events();

        for (var i = 0; i < SiteAnalyticsController.PerMinute; i++)
            (await client.PostAsync(Endpoint, empty())).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsync(Endpoint, empty())).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        (await app.Browser(address: "203.0.113.51").PostAsync(Endpoint, empty())).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Nothing_is_kept_while_the_analytics_are_turned_off()
    {
        await using var app = new AnalyticsApp();
        using var off = app.WithWebHostBuilder(b => b.ConfigureAppConfiguration(config =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["SiteAnalytics:Enabled"] = "false" })));
        var client = off.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", Chrome);

        (await client.PostAsync(Endpoint, Events(new { type = "page_view", path = "/", sessionId = SessionId }))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await off.Services.SiteEventsAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task The_visitor_hash_is_the_same_all_day_changes_the_next_day_and_never_holds_the_address()
    {
        await using var app = new AnalyticsApp();
        var keys = app.Services.GetRequiredService<SiteAnalyticsKeys>();
        var monday = new DateOnly(2026, 11, 23);
        const string address = "198.51.100.23";

        var morning = await keys.VisitorAsync(monday, address, Chrome);
        var evening = await keys.VisitorAsync(monday, address, Chrome);
        var tuesday = await keys.VisitorAsync(monday.AddDays(1), address, Chrome);
        var otherBrowser = await keys.VisitorAsync(monday, address, IPhone);
        var otherAddress = await keys.VisitorAsync(monday, "198.51.100.24", Chrome);

        morning.Should().Be(evening).And.MatchRegex("^[0-9a-f]{32}$");
        new[] { tuesday, otherBrowser, otherAddress }.Should().OnlyContain(h => h != morning);
        (await keys.SessionAsync(monday, SessionId)).Should().NotBe(await keys.SessionAsync(monday.AddDays(1), SessionId));

        // Through the service on two days: two visitors, and the address nowhere in the rows.
        using (var scope = app.Services.CreateScope())
        {
            var analytics = scope.ServiceProvider.GetRequiredService<ISiteAnalytics>();
            var visit = new SiteVisit(address, Chrome, null, "desktop", "Chrome", "Windows", "de");
            var input = new SiteEventInput { Type = SiteEventTypes.PageView, Path = "/", Source = "direct", SessionId = SessionId };
            await analytics.IngestAsync(visit, [input], NoonOf(monday));
            await analytics.IngestAsync(visit, [input], NoonOf(monday, 60));
            await analytics.IngestAsync(visit, [input], NoonOf(monday.AddDays(1)));
        }
        var rows = await app.Services.SiteEventsAsync();
        rows.Select(r => r.Day).Should().Equal(monday, monday, monday.AddDays(1));
        rows[0].VisitorHash.Should().Be(morning).And.Be(rows[1].VisitorHash);
        rows[2].VisitorHash.Should().Be(tuesday);
        JsonSerializer.Serialize(rows).Should().NotContain(address).And.NotContain("198.51.100");
    }

    [Fact]
    public async Task The_secret_is_generated_once_kept_protected_and_used_again_after_a_restart()
    {
        await using var app = new AnalyticsApp();
        var day = new DateOnly(2026, 11, 23);
        var first = await app.Services.GetRequiredService<SiteAnalyticsKeys>().VisitorAsync(day, "198.51.100.23", Chrome);

        var stored = await app.Services.Read(db => db.SiteAnalyticsSecrets.ToList());
        stored.Should().ContainSingle().Which.Id.Should().Be(1);
        stored[0].ProtectedSecret.Should().NotBeNullOrWhiteSpace();

        // Another instance on the same database reads the same secret.
        var again = new SiteAnalyticsKeys(app.Services.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(new ACI.Application.Configuration.SiteAnalyticsSettings()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SiteAnalyticsKeys>.Instance);
        (await again.VisitorAsync(day, "198.51.100.23", Chrome)).Should().Be(first);

        // A configured secret is used instead, and nothing more is stored.
        var configured = new SiteAnalyticsKeys(app.Services.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(new ACI.Application.Configuration.SiteAnalyticsSettings { Secret = "a-configured-secret-of-enough-length" }),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SiteAnalyticsKeys>.Instance);
        (await configured.VisitorAsync(day, "198.51.100.23", Chrome)).Should().NotBe(first);
        (await app.Services.Read(db => db.SiteAnalyticsSecrets.Count())).Should().Be(1);
    }

    [Fact]
    public void A_browsers_time_is_taken_within_a_few_minutes_of_the_servers_and_a_batch_keeps_its_order()
    {
        var now = new DateTime(2026, 11, 25, 10, 0, 0, DateTimeKind.Utc);

        SiteAnalyticsService.WhenOf(now.AddSeconds(-4), now, 0, 1).Should().Be(now.AddSeconds(-4));
        SiteAnalyticsService.WhenOf(now.AddMinutes(1), now, 0, 1).Should().Be(now, "a clock a little ahead is held to now");
        SiteAnalyticsService.WhenOf(now.AddHours(-3), now, 0, 1).Should().Be(now);
        SiteAnalyticsService.WhenOf(null, now, 0, 3).Should().BeBefore(SiteAnalyticsService.WhenOf(null, now, 1, 3));
        SiteAnalyticsService.WhenOf(null, now, 2, 3).Should().Be(now);
    }
}
