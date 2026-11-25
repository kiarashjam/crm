using System.Net;
using ACI.Application.Services;
using ACI.WebApi.Controllers;
using ACI.WebApi.SiteAnalytics;
using Microsoft.AspNetCore.Http;

namespace ACI.WebApi.Tests;

/// <summary>
/// What the site analytics keep of a path, a referrer and a browser (SiteTraffic): route
/// templates and no tokens, hosts only, families only.
/// </summary>
public class SiteTrafficTests
{
    private const string Token = "kZ9xQ2hhbmdlTWVQbGVhc2UxMjM0NTY3ODkw";

    [Theory]
    [InlineData("/", "/")]
    [InlineData("/pricing", "/pricing")]
    [InlineData("/pricing/", "/pricing")]
    [InlineData("/leads/3fa85f64-5717-4562-b3fc-2c963f66afa6", "/leads/:id")]
    [InlineData("/deals/3FA85F6457174562B3FC2C963F66AFA6/edit", "/deals/:id/edit")]
    [InlineData("/tasks/1234567", "/tasks/:id")]
    [InlineData("/blog/2026", "/blog/:id")]
    [InlineData("/files/0123456789abcdef0123", "/files/:id")]
    [InlineData("/leads?name=Jane%20Doe&section=Leads#top", "/leads")]
    [InlineData("/reset-password?token=" + Token, "/reset-password")]
    [InlineData("/verify-email?token=" + Token, "/verify-email")]
    [InlineData("/invites/accept?token=" + Token, "/invites/accept")]
    [InlineData("/sign/" + Token, "/sign/:token")]
    [InlineData("/SIGN/" + Token + "/pdf", "/SIGN/:token/pdf")]
    [InlineData("/unsubscribe/abc", "/unsubscribe/:token")]
    [InlineData("/pay/" + Token + "/done", "/pay/:token/done")]
    [InlineData("/capture/form-key", "/capture/:token")]
    [InlineData("/apply/form-key", "/apply/:token")]
    [InlineData("/member-portal/pavillon/sign-in", "/member-portal/:key/sign-in")]
    [InlineData("/api/public/contracts/" + Token + "/sign", "/api/public/contracts/:token/sign")]
    [InlineData("/api/public/unsubscribe/" + Token + "/one-click", "/api/public/unsubscribe/:token/one-click")]
    [InlineData("/people/jane.doe%40example.ch", "/people/:email")]
    [InlineData("/people/jane.doe@example.ch/notes", "/people/:email/notes")]
    [InlineData("/x/" + Token, "/x/:token")]
    [InlineData("/x/abcdefghijklmnopqrstuvwxyz0123456789", "/x/:token")]
    [InlineData("/blog/crm-for-swiss-clubs-2026", "/blog/crm-for-swiss-clubs-2026")]
    [InlineData("/leads/:id", "/leads/:id")]
    [InlineData("https://www.cadencee.ai/pricing?utm_source=x", "/pricing")]
    [InlineData("//pricing//plans", "/pricing/plans")]
    public void A_path_becomes_its_routes_template(string raw, string templated)
    {
        SiteTraffic.TemplatePath(raw).Should().Be(templated);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("pricing")]
    [InlineData("javascript:alert(1)")]
    [InlineData("?only=query")]
    public void What_is_not_a_path_is_none(string? raw)
    {
        SiteTraffic.TemplatePath(raw).Should().BeNull();
    }

    [Fact]
    public void A_path_is_held_to_its_limits()
    {
        var many = "/" + string.Join('/', Enumerable.Range(0, 40).Select(i => "part" + (char)('a' + i % 26)));
        SiteTraffic.TemplatePath(many)!.Split('/', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(SiteTraffic.MaxSegments);
        SiteTraffic.TemplatePath("/" + new string('w', 5000))!.Should().Be("/:token");
        SiteTraffic.TemplatePath("/" + string.Join('/', Enumerable.Range(0, 12).Select(_ => new string('w', 60) + "-x")))!.Length
            .Should().BeLessThanOrEqualTo(SiteTraffic.MaxPath);
    }

    [Theory]
    // Every secret the request log and the crash reports take out of a page's address is taken out here too.
    [InlineData("/sign/" + Token)]
    [InlineData("/unsubscribe/" + Token)]
    [InlineData("/pay/" + Token)]
    [InlineData("/api/public/contracts/" + Token + "/decline")]
    [InlineData("/api/attachments/3fa85f64-5717-4562-b3fc-2c963f66afa6/content/" + Token)]
    [InlineData("/api/public/email/inbound/" + Token)]
    [InlineData("/api/public/unsubscribe/" + Token)]
    public void No_secret_the_logs_redact_is_kept(string path)
    {
        ClientErrorsController.RedactPath(path).Should().Contain("[redacted]", "the logs' own list covers it");
        SiteTraffic.TemplatePath(path).Should().NotContain(Token).And.Contain(":token");
    }

    [Theory]
    [InlineData("https://www.google.com/search?q=jane", null, "google.com")]
    [InlineData("https://news.google.ch/", null, "news.google.ch")]
    [InlineData("android-app://com.google.android.gm/", null, "com.google.android.gm")]
    [InlineData("https://www.cadencee.ai/pricing", "https://www.cadencee.ai", null)]
    [InlineData("https://cadencee.ai/pricing", "https://www.cadencee.ai", null)]
    [InlineData("https://LinkedIn.com:443/feed", null, "linkedin.com")]
    [InlineData("ftp://example.org/x", null, null)]
    [InlineData("javascript:alert(1)", null, null)]
    [InlineData("not a url", null, null)]
    [InlineData("", null, null)]
    public void A_referrer_keeps_its_host_alone_and_none_for_this_site(string referrer, string? own, string? host)
    {
        SiteTraffic.ReferrerHost(referrer, own).Should().Be(host);
    }

    [Theory]
    [InlineData("Newsletter", "google.com", "newsletter")]
    [InlineData(null, null, "direct")]
    [InlineData(null, "google.com", "google")]
    [InlineData(null, "google.co.uk", "google")]
    [InlineData(null, "com.google.android.googlequicksearchbox", "google")]
    [InlineData(null, "bing.com", "bing")]
    [InlineData(null, "cn.bing.com", "bing")]
    [InlineData(null, "duckduckgo.com", "duckduckgo")]
    [InlineData(null, "linkedin.com", "linkedin")]
    [InlineData(null, "lnkd.in", "linkedin")]
    [InlineData(null, "com.linkedin.android", "linkedin")]
    [InlineData(null, "l.facebook.com", "facebook")]
    [InlineData(null, "m.facebook.com", "facebook")]
    [InlineData(null, "instagram.com", "instagram")]
    [InlineData(null, "l.instagram.com", "instagram")]
    [InlineData(null, "x.com", "x")]
    [InlineData(null, "t.co", "x")]
    [InlineData(null, "twitter.com", "x")]
    [InlineData(null, "chatgpt.com", "ai")]
    [InlineData(null, "chat.openai.com", "ai")]
    [InlineData(null, "perplexity.ai", "ai")]
    [InlineData(null, "gemini.google.com", "ai")]
    [InlineData(null, "mail.google.com", "email")]
    [InlineData(null, "com.google.android.gm", "email")]
    [InlineData(null, "outlook.live.com", "email")]
    [InlineData(null, "webmail.bluewin.ch", "email")]
    [InlineData(null, "pavillon46.ch", "pavillon46.ch")]
    public void A_source_is_the_utm_source_else_the_kind_of_site_that_referred_it(string? utmSource, string? referrer, string source)
    {
        SiteTraffic.ClassifySource(utmSource, referrer).Should().Be(source);
        // What a referrer is classified as is a kind the daily email names as it is, or a host.
        if (utmSource == null)
            (SiteAnalyticsRules.SourceKinds.Contains(source) || SiteAnalyticsRules.IsHostName(source)).Should().BeTrue(source);
    }

    [Theory]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36", "desktop", "Chrome", "Windows")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 Edg/131.0.0.0", "desktop", "Edge", "Windows")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Safari/605.1.15", "desktop", "Safari", "macOS")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10.15; rv:133.0) Gecko/20100101 Firefox/133.0", "desktop", "Firefox", "macOS")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.1 Mobile/15E148 Safari/604.1", "mobile", "Safari", "iOS")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) CriOS/131.0.6778.73 Mobile/15E148 Safari/604.1", "mobile", "Chrome", "iOS")]
    [InlineData("Mozilla/5.0 (iPad; CPU OS 17_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Mobile/15E148 Safari/604.1", "tablet", "Safari", "iOS")]
    [InlineData("Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Mobile Safari/537.36", "mobile", "Chrome", "Android")]
    [InlineData("Mozilla/5.0 (Linux; Android 13; SM-X700) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36", "tablet", "Chrome", "Android")]
    [InlineData("Mozilla/5.0 (Linux; Android 14; SAMSUNG SM-S918B) AppleWebKit/537.36 (KHTML, like Gecko) SamsungBrowser/26.0 Chrome/122.0.0.0 Mobile Safari/537.36", "mobile", "Samsung Internet", "Android")]
    [InlineData("Mozilla/5.0 (X11; CrOS x86_64 14541.0.0) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36", "desktop", "Chrome", "ChromeOS")]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36 OPR/115.0.0.0", "desktop", "Opera", "Linux")]
    public void A_browser_is_known_by_its_families(string userAgent, string device, string browser, string os)
    {
        SiteTraffic.BotName(userAgent).Should().BeNull();
        SiteTraffic.Device(userAgent).Should().Be(device);
        SiteTraffic.Browser(userAgent).Should().Be(browser);
        SiteTraffic.Os(userAgent).Should().Be(os);
    }

    [Theory]
    [InlineData("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)", "googlebot")]
    [InlineData("Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; bingbot/2.0; +http://www.bing.com/bingbot.htm) Chrome/116.0 Safari/537.36", "bingbot")]
    [InlineData("Mozilla/5.0 (compatible; AhrefsBot/7.0; +http://ahrefs.com/robot/)", "ahrefs")]
    [InlineData("Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; GPTBot/1.2; +https://openai.com/gptbot)", "openai")]
    [InlineData("Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; ClaudeBot/1.0; +claudebot@anthropic.com)", "anthropic")]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) HeadlessChrome/131.0.0.0 Safari/537.36", "headless")]
    [InlineData("Mozilla/5.0 (Linux; Android 11; moto g power (2022)) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Mobile Safari/537.36 Chrome-Lighthouse", "lighthouse")]
    [InlineData("facebookexternalhit/1.1 (+http://www.facebook.com/externalhit_uatext.php)", "preview")]
    [InlineData("LinkedInBot/1.0 (compatible; Mozilla/5.0; Apache-HttpClient +http://www.linkedin.com)", "preview")]
    [InlineData("WhatsApp/2.23.20.0", "preview")]
    [InlineData("Mozilla/5.0 (compatible; SomeNewCrawler/1.0)", "other")]
    [InlineData("python-requests/2.32.3", "tool")]
    [InlineData("Go-http-client/2.0", "tool")]
    [InlineData("SomethingWeird/1.0", "tool")]
    [InlineData("", "unknown")]
    [InlineData(null, "unknown")]
    public void A_robot_is_known_by_its_user_agent(string? userAgent, string bot)
    {
        SiteTraffic.BotName(userAgent).Should().Be(bot);
    }

    [Fact]
    public void A_phone_whose_name_holds_a_robots_word_is_still_a_phone()
    {
        SiteTraffic.BotName("Mozilla/5.0 (Linux; Android 10; CUBOT X30) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Mobile Safari/537.36").Should().BeNull();
    }

    [Theory]
    [InlineData("de-CH", "de")]
    [InlineData("fr-CH,fr;q=0.9,en;q=0.8", "fr")]
    [InlineData("EN_us", "en")]
    [InlineData("gsw", "gsw")]
    [InlineData("*", null)]
    [InlineData("english", null)]
    [InlineData(null, null)]
    public void A_language_is_its_primary_tag(string? raw, string? language)
    {
        SiteTraffic.Language(raw).Should().Be(language);
    }

    [Theory]
    [InlineData("CTA Signup", "cta-signup")]
    [InlineData("signup_completed", "signup_completed")]
    [InlineData("  <script>x</script> ", "scriptxscript")]
    [InlineData("!!!", null)]
    public void An_events_name_is_one_plain_word(string raw, string? name)
    {
        SiteTraffic.EventName(raw).Should().Be(name);
    }

    [Theory]
    [InlineData("Spring Launch", "spring launch")]
    [InlineData("  Spring\n\tLaunch  ", "spring launch")]
    [InlineData("LinkedIn", "linkedin")]
    [InlineData("newsletter_2026-11", "newsletter_2026-11")]
    [InlineData("black-friday-2026", "black-friday-2026")]
    [InlineData("google+ads", "google+ads")]
    [InlineData("v1.2", "v1.2")]
    [InlineData("Été à Zürich", "ete a zurich")]
    [InlineData("<script>alert(1)</script>", "scriptalert1script")]
    [InlineData("Launch Week! (DE)", "launch week de")]
    [InlineData("Visit evil.com/x?y=1", "visit evil.comxy1")]
    [InlineData("Spring\u202eLaunch\u200b", "springlaunch")]
    // Someone's address, or what is shaped like an id or a token, whole or in part: none at all.
    [InlineData("jane@example.ch", null)]
    [InlineData("jane%40example.ch", null)]
    [InlineData("3fa85f64-5717-4562-b3fc-2c963f66afa6", null)]
    [InlineData("newsletter 3fa85f64-5717-4562-b3fc-2c963f66afa6", null)]
    [InlineData("newsletter_3fa85f64-5717-4562-b3fc-2c963f66afa6", null)]
    [InlineData("1234", null)]
    [InlineData("subscriber_483920", null)]
    [InlineData("0791234567", null)]
    [InlineData("deadbeefdeadbeef00", null)]
    [InlineData("kZ9xQ2hhbmdlTWVQbGVhc2UxMjM0NTY3ODkw", null)]
    [InlineData("campaign kZ9xQ2hhbmdlTWVQbGVhc2UxMjM0NTY3ODkw", null)]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U", null)]
    [InlineData("   ", null)]
    [InlineData("!!!", null)]
    public void A_utm_value_is_one_plain_lower_case_line_and_never_an_address_an_id_or_a_token(string raw, string? value)
    {
        SiteTraffic.Utm(raw).Should().Be(value);
    }

    [Fact]
    public void A_utm_value_is_held_to_its_limit()
    {
        SiteTraffic.Utm(string.Join(' ', Enumerable.Repeat("spring", 40)))!.Length.Should().BeLessThanOrEqualTo(SiteTraffic.MaxUtm);
        SiteTraffic.Utm(string.Join(' ', Enumerable.Repeat("spring", 40)))!.Should().NotEndWith(" ");
        SiteTraffic.MaxUtm.Should().Be(64);
    }

    [Theory]
    [InlineData("/sign/:token", true)]
    [InlineData("/sign", true)]
    [InlineData("/SIGN/:token/pdf", true)]
    [InlineData("/%73ign/abc", true)]
    [InlineData("/pay/:token/done", true)]
    [InlineData("/unsubscribe/:token", true)]
    [InlineData("/capture/:token", true)]
    [InlineData("/apply/:token", true)]
    [InlineData("/member-portal/:key/sign-in", true)]
    [InlineData("/", false)]
    [InlineData("/pricing", false)]
    [InlineData("/signup-guide", false)]
    [InlineData("/blog/sign", false)]
    [InlineData("/api/public/contracts/:token/sign", false)]
    public void A_customers_page_is_never_tracked(string path, bool untracked)
    {
        SiteTraffic.IsUntracked(path).Should().Be(untracked);
    }

    [Fact]
    public void Every_route_whose_segment_is_a_secret_is_untracked()
    {
        SiteTraffic.UntrackedRoots.Should().BeEquivalentTo(["sign", "pay", "unsubscribe", "capture", "apply", "member-portal"]);
        SiteTraffic.RouteTemplates.Keys.Should().OnlyContain(k => SiteTraffic.UntrackedRoots.Contains(k));
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("a\u0000b\u0007c", "abc")]
    [InlineData("line\nbreak\ttab", "line break tab")]
    [InlineData("right\u202eto\u2066left\u200b\ufeff", "righttoleft")]
    [InlineData("soft\u00adhyphen", "softhyphen")]
    [InlineData("émoji 😀 kept", "émoji 😀 kept")]
    public void Control_and_format_characters_are_stripped(string raw, string printable)
    {
        SiteTraffic.Printable(raw).Should().Be(printable);
    }

    [Fact]
    public void Text_is_cut_between_code_points()
    {
        const string text = "ab😀😀";
        SiteTraffic.Cut(text, 10).Should().Be(text);
        SiteTraffic.Cut(text, 4).Should().Be("ab😀");
        SiteTraffic.Cut(text, 3).Should().Be("ab", "half of the emoji is not kept");
        SiteTraffic.Cut(text, 2).Should().Be("ab");
        // A path whose limit falls inside an emoji keeps none of it.
        var segment = new string('w', 79);
        var path = $"/{segment}/{segment}/{segment}/{new string('w', 58)}😀{new string('x', 10)}";
        char.IsHighSurrogate(path[SiteTraffic.MaxPath - 1]).Should().BeTrue();
        SiteTraffic.TemplatePath(path).Should().Be(path[..(SiteTraffic.MaxPath - 1)]);
    }

    [Theory]
    // A public address on the connection is the caller's own: a forwarded header is not believed.
    [InlineData("198.51.100.9", "203.0.113.7", "198.51.100.9")]
    // Behind the platform's proxy (a private address), the address it added last is the caller's.
    [InlineData("10.0.0.4", "203.0.113.7", "203.0.113.7")]
    [InlineData("10.0.0.4", "1.1.1.1, 203.0.113.7:51234", "203.0.113.7")]
    [InlineData("::ffff:172.16.3.4", "[2001:db8::1]:443", "2001:db8::1")]
    [InlineData("10.0.0.4", "garbage", "10.0.0.4")]
    [InlineData(null, null, "unknown")]
    public void The_callers_address_is_the_connections_or_the_proxys(string? remote, string? forwarded, string address)
    {
        var context = new DefaultHttpContext();
        if (remote != null) context.Connection.RemoteIpAddress = IPAddress.Parse(remote);
        if (forwarded != null) context.Request.Headers["X-Forwarded-For"] = forwarded;

        SiteTraffic.ClientAddress(context).Should().Be(address);
    }
}
