using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.Infrastructure.Services.SiteAnalytics;
using ACI.WebApi.Controllers;
using ACI.WebApi.SiteAnalytics;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using static ACI.WebApi.Tests.Helpers.SiteAnalyticsWorld;

namespace ACI.WebApi.Tests;

/// <summary>
/// The tracker's posts (POST /api/site-analytics/events): anonymous, rate limited per address and
/// for everyone, size limited, capped per day, every field checked and clipped, robots counted
/// and dropped, Do Not Track and Global Privacy Control honoured, and nothing kept that names
/// anyone or opens anything: no address, no session id, no query string, no token, nothing of
/// the app's signed-in use but sign-ups, no customer's page; and salts no one can make again.
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
            new { type = "page_view", path = $"/leads/{Guid1}?name=Jane%20Doe", title = "Jane Doe » Leads » CadenceE", sessionId = SessionId },
            new { type = "page_view", path = $"/sign/{SigningToken}", title = "Sign your contract", sessionId = SessionId },
            new { type = "click", name = "CTA Signup", path = "/", sessionId = SessionId },
            new { type = "click", name = "outbound", path = "https://www.linkedin.com/company/cadencee?trk=x", sessionId = SessionId },
            new { type = "event", name = "signup_completed", path = "/login", sessionId = SessionId, signedIn = true }));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var rows = await app.Services.SiteEventsAsync();
        // The signing page is a customer's: never kept.
        rows.Select(r => (r.Type, r.Name, r.Path)).Should().Equal(
            (SiteEventTypes.PageView, null, "/pricing"),
            (SiteEventTypes.PageView, null, "/leads/:id"),
            (SiteEventTypes.Click, "cta-signup", "/"),
            (SiteEventTypes.Click, "outbound", "linkedin.com"),
            (SiteEventTypes.Event, "signup_completed", "/login"));

        var landing = rows[0];
        landing.ReferrerHost.Should().Be("google.com");
        landing.Source.Should().Be("newsletter", "utm_source wins over the referrer");
        (landing.UtmSource, landing.UtmMedium, landing.UtmCampaign).Should().Be(("newsletter", "email", "launch week"));
        (landing.Device, landing.Browser, landing.Os).Should().Be(("mobile", "Safari", "iOS"));
        landing.Language.Should().Be("de");
        landing.SignedIn.Should().BeFalse();
        landing.OccurredAtUtc.Should().BeCloseTo(DateTime.Parse(at).ToUniversalTime(), TimeSpan.FromMilliseconds(1));
        landing.Day.Should().Be(SiteAnalyticsRules.DayOf(landing.OccurredAtUtc));
        // An event without a language takes the browser's Accept-Language.
        rows[1].Language.Should().Be("fr");
        rows[4].SignedIn.Should().BeTrue("a sign-up recorded just after signing in is kept, as signed in");

        // One visitor, one session: hashes, the same for every event of the post.
        rows.Select(r => r.VisitorHash).Distinct().Should().ContainSingle().Which.Should().MatchRegex("^[0-9a-f]{32}$");
        rows.Select(r => r.SessionKey).Distinct().Should().ContainSingle().Which.Should().MatchRegex("^[0-9a-f]{32}$");
        rows[0].SessionKey.Should().NotBe(SessionId);

        // Nothing that names anyone or opens anything; no title, screen, utm_term or utm_content.
        var stored = JsonSerializer.Serialize(rows);
        foreach (var secret in new[] { "198.51.100.23", SessionId, SigningToken, Guid1, "jane", "Jane", "trk=", "utm_source=", "search?q",
                     "Pricing — CadenceE", "Sign your contract", "844", "hero", "\"crm\"" })
            stored.Should().NotContain(secret);
        typeof(SiteEvent).GetProperties().Select(p => p.Name).Should().NotContain(new[] { "Title", "ScreenBucket", "UtmTerm", "UtmContent" });
    }

    [Fact]
    public async Task Signed_in_only_a_sign_up_event_is_kept_never_a_page_view_a_click_or_a_sign_in()
    {
        await using var app = new AnalyticsApp();

        var res = await app.Browser().PostAsync(Endpoint, Events(
            new { type = "page_view", path = $"/leads/{Guid1}", sessionId = SessionId, signedIn = true },
            new { type = "click", name = "save-lead", path = "/leads/:id", sessionId = SessionId, signedIn = true },
            new { type = "click", name = "outbound", path = "https://www.linkedin.com/in/jane", sessionId = SessionId, signedIn = true },
            new { type = "event", name = "login", path = "/login", sessionId = SessionId, signedIn = true },
            new { type = "event", name = "login", path = "/login", sessionId = SessionId },
            new { type = "event", name = "refresh", path = "/dashboard", sessionId = SessionId, signedIn = true },
            new { type = "event", name = "signup_started", path = "/login", sessionId = SessionId },
            new { type = "event", name = "signup_completed", path = "/login", sessionId = SessionId, signedIn = true }));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await app.Services.SiteEventsAsync()).Select(r => (r.Type, r.Name, r.SignedIn)).Should().Equal(
            (SiteEventTypes.Event, "signup_started", false),
            (SiteEventTypes.Event, "signup_completed", true));

        // The service drops them too, whoever calls it.
        using (var scope = app.Services.CreateScope())
        {
            var visit = new SiteVisit("198.51.100.23", Chrome, null, "desktop", "Chrome", "Windows", "de");
            await scope.ServiceProvider.GetRequiredService<ISiteAnalytics>().IngestAsync(visit,
            [
                new SiteEventInput { Type = SiteEventTypes.PageView, Path = "/deals", Source = "direct", SessionId = SessionId, SignedIn = true },
                new SiteEventInput { Type = SiteEventTypes.Event, Name = "login", Path = "/login", Source = "direct", SessionId = SessionId, SignedIn = true },
            ], DateTime.UtcNow);
        }
        (await app.Services.SiteEventsAsync()).Should().HaveCount(2);
    }

    [Theory]
    [InlineData("/sign/" + SigningToken)]
    [InlineData("/sign")]
    [InlineData("/SIGN/abc/pdf")]
    [InlineData("/%73ign/abc")]
    [InlineData("/pay/" + SigningToken + "/done")]
    [InlineData("/unsubscribe/abc")]
    [InlineData("/capture/form-key")]
    [InlineData("/apply/form-key")]
    [InlineData("/member-portal/pavillon/sign-in")]
    [InlineData("https://www.cadencee.ai/member-portal/pavillon")]
    public async Task A_customers_page_is_never_kept_whatever_the_client_posts(string path)
    {
        await using var app = new AnalyticsApp();

        var res = await app.Browser().PostAsync(Endpoint, Events(
            new { type = "page_view", path, sessionId = SessionId },
            new { type = "click", name = "pay-now", path, sessionId = SessionId },
            new { type = "event", name = "signup_started", path, sessionId = SessionId },
            new { type = "page_view", path = "/pricing", sessionId = SessionId }));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await app.Services.SiteEventsAsync()).Select(r => r.Path).Should().Equal("/pricing");
    }

    [Theory]
    [InlineData("Sec-GPC", "1")]
    [InlineData("DNT", "1")]
    public async Task A_browser_that_asks_not_to_be_tracked_is_answered_204_and_not_read(string header, string value)
    {
        await using var app = new AnalyticsApp();
        var client = app.Browser();
        client.DefaultRequestHeaders.Add(header, value);

        (await client.PostAsync(Endpoint, Events(new { type = "page_view", path = "/pricing", sessionId = SessionId }))).StatusCode
            .Should().Be(HttpStatusCode.NoContent);
        // Not even read: a body over the limit is not refused.
        (await client.PostAsync(Endpoint, new StringContent(new string(' ', SiteAnalyticsController.MaxBodyBytes + 10), Encoding.UTF8, "text/plain")))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await app.Services.SiteEventsAsync()).Should().BeEmpty();
        (await app.Services.BotHitsAsync()).Should().BeEmpty();

        // "0" asks nothing.
        var plain = app.Browser();
        plain.DefaultRequestHeaders.Add(header, "0");
        await plain.PostAsync(Endpoint, Events(new { type = "page_view", path = "/pricing", sessionId = SessionId }));
        (await app.Services.SiteEventsAsync()).Should().ContainSingle();
    }

    [Fact]
    public async Task Text_that_cannot_be_read_costs_its_own_event_alone_and_controls_are_stripped()
    {
        await using var app = new AnalyticsApp();

        // JSON can carry a lone surrogate (\ud800), which a .NET string cannot be read from.
        const string body = "{\"events\":["
            + "{\"type\":\"page_view\",\"path\":\"/a\",\"sessionId\":\"a1b2c3d4e5f6\"},"
            + "{\"type\":\"page_view\",\"path\":\"/b\\ud800\",\"sessionId\":\"a1b2c3d4e5f6\"},"
            + "{\"type\":\"page_view\",\"path\":\"/c\",\"sessionId\":\"a1b2c3d4e5f6\",\"lang\":\"\\udc00\",\"referrer\":\"\\ud83d\","
            + "\"utm\":{\"source\":\"x\\ud800y\",\"campaign\":\"Spring\\u202eLaunch\\u200b\\u0007\"}},"
            + "{\"type\":\"click\",\"name\":\"cta\\u200b-\\u0000signup\",\"path\":\"/d\\u202e\\u0001e\",\"sessionId\":\"a1b2c3d4e5f6\"}"
            + "]}";

        var res = await app.Browser().PostAsync(Endpoint, new StringContent(body, Encoding.UTF8, "text/plain"));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var rows = await app.Services.SiteEventsAsync();
        rows.Select(r => r.Path).Should().Equal("/a", "/c", "/de");
        rows[1].UtmSource.Should().BeNull();
        rows[1].UtmCampaign.Should().Be("springlaunch");
        rows[1].Language.Should().Be("fr", "an unreadable language gives way to the browser's");
        rows[2].Name.Should().Be("cta-signup");
        rows.SelectMany(r => new[] { r.Path, r.Name, r.UtmCampaign, r.Source }).OfType<string>().SelectMany(t => t)
            .Should().NotContain(c => char.GetUnicodeCategory(c) == UnicodeCategory.Control || char.GetUnicodeCategory(c) == UnicodeCategory.Format);
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
        (row.Path, row.ReferrerHost, row.UtmSource, row.SignedIn, row.Source)
            .Should().Be(("/", (string?)null, (string?)null, false, "direct"));
        row.Language.Should().Be("fr", "an unreadable language gives way to the browser's");
        row.OccurredAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Every_field_is_clipped_and_a_post_takes_at_most_25_events()
    {
        await using var app = new AnalyticsApp();
        var long1 = new string('a', 1200);
        var longWords = string.Join(' ', Enumerable.Repeat("spring launch", 100));
        var events = Enumerable.Range(0, 3).Select(i => (object)new
        {
            type = "click",
            name = "n" + new string('x', 500),
            path = "/" + string.Join('/', Enumerable.Range(0, 30).Select(_ => "segment")),
            title = long1,
            sessionId = SessionId,
            utm = new { source = longWords, medium = longWords, campaign = longWords, term = long1, content = long1 },
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
            && r.UtmSource!.Length <= SiteTraffic.MaxUtm && r.UtmCampaign!.Length <= SiteTraffic.MaxUtm && r.Source.Length <= 100);
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
    public async Task One_address_may_post_30_times_a_minute_and_another_address_is_not_held_up()
    {
        await using var app = new AnalyticsApp();
        var client = app.Browser(address: "203.0.113.50");
        var empty = () => Events();

        for (var i = 0; i < SiteAnalyticsController.PerMinute; i++)
            (await client.PostAsync(Endpoint, empty())).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsync(Endpoint, empty())).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        (await app.Browser(address: "203.0.113.51").PostAsync(Endpoint, empty())).StatusCode.Should().Be(HttpStatusCode.NoContent);
        SiteAnalyticsController.PerMinute.Should().Be(30);
    }

    [Fact]
    public async Task Everyone_together_may_post_600_times_a_minute_whatever_their_addresses()
    {
        await using var app = new AnalyticsApp();
        var empty = () => Events();
        var perAddress = SiteAnalyticsController.PerMinute;
        var addresses = SiteAnalyticsController.EveryonePerMinute / perAddress;

        for (var a = 0; a < addresses; a++)
        {
            var client = app.Browser(address: $"198.18.{a}.1");
            for (var i = 0; i < perAddress; i++)
                (await client.PostAsync(Endpoint, empty())).StatusCode.Should().Be(HttpStatusCode.NoContent, $"address {a}, post {i}");
        }

        var refused = await app.Browser(address: "198.19.0.1").PostAsync(Endpoint, empty());
        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests, "a new address finds everyone's 600 used");
        refused.Headers.RetryAfter.Should().NotBeNull();
        // The rest of the API is not held up by the tracker's limits.
        (await app.CreateClient().GetAsync("/ping")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Past_the_days_cap_posts_are_answered_204_and_dropped_and_it_is_logged_once()
    {
        await using var app = new AnalyticsApp();
        var log = new CapturingLogger<SiteAnalyticsService>();
        using var capped = app.WithWebHostBuilder(b =>
        {
            b.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?> { ["SiteAnalytics:DailyEventCap"] = "3" }));
            b.ConfigureTestServices(services => services.AddSingleton<ILogger<SiteAnalyticsService>>(log));
        });
        var client = capped.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", Chrome);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.9");
        var two = () => Events(
            new { type = "page_view", path = "/one", sessionId = SessionId },
            new { type = "page_view", path = "/two", sessionId = SessionId });

        for (var i = 0; i < 3; i++) (await client.PostAsync(Endpoint, two())).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await capped.Services.SiteEventsAsync()).Select(r => r.Path).Should().Equal("/one", "/two", "/one");
        log.Entries.Where(e => e.Level == LogLevel.Warning).Should().ContainSingle()
            .Which.Template.Should().Contain("events of the day");
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
        var otherNetwork = await keys.VisitorAsync(monday, "198.51.101.23", Chrome);

        morning.Should().Be(evening).And.MatchRegex("^[0-9a-f]{32}$");
        new[] { tuesday, otherBrowser, otherNetwork }.Should().OnlyContain(h => h != morning);
        // The address is shortened before it is hashed: the same /24 (IPv4) or /48 (IPv6) is the same visitor.
        (await keys.VisitorAsync(monday, "198.51.100.200", Chrome)).Should().Be(morning);
        (await keys.VisitorAsync(monday, "::ffff:198.51.100.7", Chrome)).Should().Be(morning);
        (await keys.VisitorAsync(monday, "2001:db8:1:2::1", Chrome)).Should().Be(await keys.VisitorAsync(monday, "2001:db8:1:ffff::abcd", Chrome))
            .And.NotBe(await keys.VisitorAsync(monday, "2001:db8:2::1", Chrome));
        // The session is salted with the day's salt too.
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

    [Theory]
    [InlineData("198.51.100.23", "198.51.100.0")]
    [InlineData("198.51.100.255", "198.51.100.0")]
    [InlineData("::ffff:198.51.100.23", "198.51.100.0")]
    [InlineData("2001:db8:1234:5678:9abc:def0:1234:5678", "2001:db8:1234::")]
    [InlineData("2001:db8::1", "2001:db8::")]
    [InlineData("unknown", "unknown")]
    public void The_address_is_shortened_to_its_network_before_it_is_hashed(string address, string shortened)
    {
        SiteAnalyticsKeys.ShortenedAddress(address).Should().Be(shortened);
    }

    [Fact]
    public async Task A_days_salt_is_random_kept_protected_once_for_every_server_and_never_derived_from_a_secret()
    {
        await using var app = new AnalyticsApp();
        var scopes = app.Services.GetRequiredService<IServiceScopeFactory>();
        var keys = app.Services.GetRequiredService<SiteAnalyticsKeys>();
        var day = new DateOnly(2026, 11, 23);
        var first = await keys.VisitorAsync(day, "198.51.100.23", Chrome);
        var salt = await keys.SaltAsync(day);

        salt.Should().HaveCount(SiteAnalyticsKeys.SaltBytes);
        var stored = (await app.Services.Read(db => db.SiteAnalyticsSalts.ToList())).Should().ContainSingle().Subject;
        stored.Day.Should().Be(day);
        stored.ProtectedSalt.Should().NotContain(Convert.ToBase64String(salt), "it is kept protected");
        using (var scope = app.Services.CreateScope())
            Convert.FromBase64String(scope.ServiceProvider.GetRequiredService<ISecretProtector>().Unprotect(stored.ProtectedSalt)).Should().Equal(salt);

        // Another server on the same database (or this one after a restart) reads the same salt.
        var again = new SiteAnalyticsKeys(scopes, NullLogger<SiteAnalyticsKeys>.Instance);
        (await again.VisitorAsync(day, "198.51.100.23", Chrome)).Should().Be(first);

        // Each day's is its own random salt: nothing it is made from, so nothing to make it again from.
        var next = await keys.SaltAsync(day.AddDays(1));
        next.Should().NotEqual(salt);
        HMACSHA256.HashData(next, Encoding.UTF8.GetBytes("site-analytics:" + SiteAnalyticsRules.Format(day))).Should().NotEqual(salt);
        (await app.Services.Read(db => db.SiteAnalyticsSalts.Count())).Should().Be(2);
        typeof(SiteAnalyticsSettings).GetProperty("Secret").Should().BeNull("no secret feeds the salts");
    }

    [Fact]
    public async Task Two_servers_making_the_days_salt_at_once_end_with_one()
    {
        await using var app = new AnalyticsApp();
        var scopes = app.Services.GetRequiredService<IServiceScopeFactory>();
        var day = new DateOnly(2026, 11, 23);
        var servers = Enumerable.Range(0, 6).Select(_ => new SiteAnalyticsKeys(scopes, NullLogger<SiteAnalyticsKeys>.Instance)).ToList();

        var hashes = await Task.WhenAll(servers.Select(s => Task.Run(() => s.VisitorAsync(day, "198.51.100.23", Chrome))));

        hashes.Distinct().Should().ContainSingle("every server hashes the day with the one salt saved");
        (await app.Services.Read(db => db.SiteAnalyticsSalts.Count())).Should().Be(1);
    }

    [Fact]
    public async Task A_salt_that_cannot_be_read_is_replaced_by_a_new_random_one()
    {
        await using var app = new AnalyticsApp();
        var day = new DateOnly(2026, 11, 23);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.SiteAnalyticsSalts.Add(new SiteAnalyticsSalt { Day = day, ProtectedSalt = "protected with keys that are gone", CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var salt = await new SiteAnalyticsKeys(app.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<SiteAnalyticsKeys>.Instance).SaltAsync(day);

        salt.Should().HaveCount(SiteAnalyticsKeys.SaltBytes);
        var stored = (await app.Services.Read(db => db.SiteAnalyticsSalts.ToList())).Should().ContainSingle().Subject;
        stored.ProtectedSalt.Should().NotBe("protected with keys that are gone");
    }

    [Fact]
    public async Task The_daily_clean_up_destroys_every_salt_but_todays_and_yesterdays_so_an_old_hash_cannot_be_made_again()
    {
        await using var app = new AnalyticsApp();
        var keys = app.Services.GetRequiredService<SiteAnalyticsKeys>();
        var today = SiteAnalyticsRules.DayOf(DateTime.UtcNow);
        var old = today.AddDays(-2);
        var oldHash = await keys.VisitorAsync(old, "198.51.100.23", Chrome);
        await keys.SaltAsync(today.AddDays(-1));
        await keys.SaltAsync(today);

        using (var scope = app.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ISiteAnalytics>().PurgeAsync(DateTime.UtcNow);

        (await app.Services.Read(db => db.SiteAnalyticsSalts.Select(s => s.Day).OrderBy(d => d).ToList()))
            .Should().Equal(today.AddDays(-1), today);
        // The same visitor, the same address, that day again: the salt is gone, from the database
        // and from memory, so the hash it gave cannot be made again.
        (await keys.VisitorAsync(old, "198.51.100.23", Chrome)).Should().NotBe(oldHash);
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
