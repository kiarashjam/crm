using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.WebApi.Controllers;
using ACI.WebApi.Middleware;

namespace ACI.WebApi.SiteAnalytics;

/// <summary>
/// What the site analytics keep of a request and of each event the tracker posts: every field
/// checked against what it may be, clipped, and stripped of anything that could name someone
/// or open something.
/// </summary>
/// <remarks>
/// <para>
/// A path keeps no query string or fragment (where reset, sign-in and invitation tokens travel)
/// and becomes its route's template: a GUID, a long number or a long hex string is ":id", the
/// variable segment of a signing, unsubscribe, pay, capture, application or member portal link
/// is ":token" (":key" for the portal), anything shaped like a token is ":token", and anything
/// with an @ is ":email". The secret paths the request log redacts (SecretPathRedactionMiddleware,
/// ClientErrorsController) are redacted first, the same way. The tracker
/// (src/app/lib/siteAnalytics.ts) templates the same way; the server does it again, as a posted
/// path is whatever the caller says.
/// </para>
/// <para>
/// A referrer keeps its host alone (its path can carry a search, an email, a token). A utm value
/// is lower case and plain, and none at all when it looks like an id, a token or an address. No
/// page title and no screen size are kept, nor utm_term or utm_content.
/// </para>
/// <para>
/// A customer's pages (signing, paying, unsubscribing, a capture or application form, a club's
/// member portal: <see cref="UntrackedRoots"/>) are never kept, whatever a client posts. Signed
/// in, only a sign-up event is kept (SiteAnalyticsRules.EventNames), and never a sign-in. Every
/// text is stripped of control and format characters and cut between code points, and one
/// event that cannot be read costs that event alone, never the rest of its post.
/// </para>
/// </remarks>
public static partial class SiteTraffic
{
    public const int MaxPath = 300;
    public const int MaxName = 64;
    public const int MaxUtm = 64;
    public const int MaxHost = 253;

    /// <summary>A segment longer than this is not a word anyone reads: a token.</summary>
    public const int MaxSegment = 80;

    /// <summary>Segments of a path kept; the rest are left off.</summary>
    public const int MaxSegments = 12;

    /// <summary>The longest raw value looked at; longer is cut before anything else is done with it.</summary>
    private const int MaxRaw = 2048;

    /// <summary>
    /// The app's routes whose second segment is a link's secret or a form's key (src/app/App.tsx):
    /// signing a contract, unsubscribing, paying, a public capture or application form, and a
    /// club's member portal.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> RouteTemplates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["sign"] = ":token",
        ["unsubscribe"] = ":token",
        ["pay"] = ":token",
        ["capture"] = ":token",
        ["apply"] = ":token",
        ["member-portal"] = ":key",
    };

    /// <summary>
    /// The first segments of the pages never recorded: a club's customers open them from a link
    /// or a form (signing a contract, paying, unsubscribing, a capture or application form, the
    /// member portal), so they are no part of the website's visits. The tracker leaves them out
    /// too; a stale or hostile client's posts of them are dropped here.
    /// </summary>
    public static readonly IReadOnlySet<string> UntrackedRoots = new HashSet<string>(
        ["sign", "pay", "unsubscribe", "capture", "apply", "member-portal", .. RouteTemplates.Keys],
        StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex("^:[a-z]{1,16}$")]
    private static partial Regex TemplateSegment();

    [GeneratedRegex("^[0-9]{4,}$")]
    private static partial Regex LongNumber();

    [GeneratedRegex("^[0-9a-fA-F]{16,}$")]
    private static partial Regex LongHex();

    [GeneratedRegex("^[A-Za-z0-9_\\-.~=+]{20,}$")]
    private static partial Regex TokenCharacters();

    [GeneratedRegex("^[A-Za-z0-9_-]{8,64}$")]
    private static partial Regex SessionId();

    [GeneratedRegex("[^a-z0-9_.:-]+")]
    private static partial Regex NameNoise();

    [GeneratedRegex("[^a-z0-9 _.+-]+")]
    private static partial Regex UtmNoise();

    /// <summary>What parts a utm value's words: spaces and the punctuation a tag is written with, never a GUID's hyphen.</summary>
    [GeneratedRegex("[\\s_.+/:;,=|]+")]
    private static partial Regex UtmWordBreak();

    /// <summary>A number long enough to be someone's: a subscriber, an order, a phone.</summary>
    [GeneratedRegex("^[0-9]{6,}$")]
    private static partial Regex IdNumber();

    [GeneratedRegex("\\s+")]
    private static partial Regex Whitespace();

    // ── Paths ────────────────────────────────────────────────────────────────

    /// <summary>The page's path as its route's template, or null when it is not a path.</summary>
    public static string? TemplatePath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var value = Bound(raw.Trim());
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return null;
            value = uri.AbsolutePath;
        }
        var cut = value.IndexOfAny(['?', '#']);
        if (cut >= 0) value = value[..cut];
        if (!value.StartsWith('/')) return null;

        // The secrets the request log takes out, taken out the same way first.
        value = ClientErrorsController.RedactPath(value);

        var segments = value.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var parts = new List<string>(Math.Min(segments.Length, MaxSegments));
        for (var i = 0; i < segments.Length && i < MaxSegments; i++)
        {
            parts.Add(i == 1 && RouteTemplates.TryGetValue(segments[0], out var template) ? template : Segment(segments[i]));
        }
        return Cut("/" + string.Join('/', parts), MaxPath);
    }

    /// <summary>
    /// Whether a page is one never recorded: its first segment is a customer's page
    /// (<see cref="UntrackedRoots"/>), whatever its case or encoding.
    /// </summary>
    public static bool IsUntracked(string path)
    {
        var first = path.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return first != null && (UntrackedRoots.Contains(first) || UntrackedRoots.Contains(Decode(first).Trim()));
    }

    private static string Segment(string segment)
    {
        if (segment == SecretPathRedactionMiddleware.Placeholder) return ":token";
        if (TemplateSegment().IsMatch(segment)) return segment;
        var decoded = Decode(segment);
        if (decoded.Contains('@')) return ":email";
        if (Guid.TryParse(decoded, out _) || LongNumber().IsMatch(decoded) || LongHex().IsMatch(decoded)) return ":id";
        if (decoded.Length > MaxSegment || LooksLikeToken(decoded)) return ":token";
        return segment;
    }

    /// <summary>
    /// Twenty or more token characters with letters and digits, and either both cases (base64)
    /// or no hyphen (hex, base32): a token. A slug ("crm-for-clubs-2026") is lower case with hyphens.
    /// </summary>
    private static bool LooksLikeToken(string segment)
    {
        if (!TokenCharacters().IsMatch(segment)) return false;
        var digit = segment.Any(char.IsAsciiDigit);
        var upper = segment.Any(char.IsAsciiLetterUpper);
        var lower = segment.Any(char.IsAsciiLetterLower);
        return digit && (upper || lower) && ((upper && lower) || !segment.Contains('-'));
    }

    private static string Decode(string segment)
    {
        try
        {
            return Uri.UnescapeDataString(segment);
        }
        catch (UriFormatException)
        {
            return segment;
        }
    }

    // ── Hosts, referrers and sources ─────────────────────────────────────────

    /// <summary>A host in lower case without "www." or a port, from a host or an address; null when it is not one.</summary>
    public static string? Host(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var value = Bound(raw.Trim());
        string host;
        if (value.Contains("://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host)) return null;
            host = uri.Host;
        }
        else
        {
            var end = value.IndexOfAny(['/', '?', '#', ':']);
            host = end < 0 ? value : value[..end];
        }
        host = host.Trim().TrimEnd('.').ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal)) host = host[4..];
        return SiteAnalyticsRules.IsHostName(host) ? host : null;
    }

    /// <summary>
    /// The host of the page the visit came from, or null for none, for this site itself
    /// (<paramref name="ownHost"/>), or for anything but a web or app address.
    /// </summary>
    public static string? ReferrerHost(string? referrer, string? ownHost)
    {
        if (string.IsNullOrWhiteSpace(referrer)) return null;
        var value = referrer.Trim();
        if (!(value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
              || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
              || value.StartsWith("android-app://", StringComparison.OrdinalIgnoreCase)))
            return null;
        var host = Host(value);
        return host == null || host == Host(ownHost) ? null : host;
    }

    private static readonly string[] EmailHosts =
    [
        "mail.google.com", "com.google.android.gm", "outlook.live.com", "outlook.office.com", "outlook.office365.com",
        "mail.yahoo.com", "mail.proton.me", "mail.aol.com", "mail.zoho.com", "mail.gmx.net", "com.microsoft.office.outlook",
    ];

    private static readonly string[] AiHosts =
    [
        "chatgpt.com", "chat.openai.com", "openai.com", "perplexity.ai", "gemini.google.com", "copilot.microsoft.com", "claude.ai",
    ];

    /// <summary>
    /// Where a visit came from: its utm_source when it has one, else what kind of site referred it
    /// (google, bing, duckduckgo, linkedin, facebook, instagram, x, ai, email), else that site's
    /// host, else "direct".
    /// </summary>
    public static string ClassifySource(string? utmSource, string? referrerHost)
    {
        if (Utm(utmSource) is { } tagged) return tagged;
        if (string.IsNullOrEmpty(referrerHost)) return SiteAnalyticsRules.Direct;
        var host = referrerHost.ToLowerInvariant();
        bool Is(string domain) => host == domain || host.EndsWith("." + domain, StringComparison.Ordinal);
        var labels = host.Split('.');

        if (EmailHosts.Any(Is) || host.StartsWith("mail.", StringComparison.Ordinal) || host.StartsWith("webmail.", StringComparison.Ordinal))
            return "email";
        if (AiHosts.Any(Is)) return "ai";
        if (labels.Contains("google")) return "google";
        if (labels.Contains("bing")) return "bing";
        if (Is("duckduckgo.com")) return "duckduckgo";
        if (Is("linkedin.com") || Is("lnkd.in") || host == "com.linkedin.android") return "linkedin";
        if (Is("instagram.com")) return "instagram";
        if (labels.Contains("facebook") || Is("fb.com") || Is("fb.me")) return "facebook";
        if (Is("x.com") || Is("twitter.com") || Is("t.co")) return "x";
        return host.Length <= 100 ? host : host[..100];
    }

    // ── The browser ──────────────────────────────────────────────────────────

    private static readonly (string Needle, string Bot)[] Bots =
    [
        ("googlebot", "googlebot"), ("google-inspectiontool", "google"), ("adsbot-google", "google"), ("mediapartners-google", "google"),
        ("google-read-aloud", "google"), ("storebot-google", "google"), ("googleother", "google"), ("google-extended", "google"),
        ("bingbot", "bingbot"), ("bingpreview", "bingbot"), ("adidxbot", "bingbot"), ("duckduckbot", "duckduckbot"), ("duckassistbot", "duckduckbot"),
        ("yandex", "yandex"), ("baiduspider", "baidu"), ("applebot", "applebot"), ("petalbot", "petalbot"), ("bytespider", "bytespider"),
        ("gptbot", "openai"), ("chatgpt-user", "openai"), ("oai-searchbot", "openai"), ("claudebot", "anthropic"), ("claude-user", "anthropic"),
        ("claude-searchbot", "anthropic"), ("anthropic-ai", "anthropic"), ("perplexitybot", "perplexity"), ("perplexity-user", "perplexity"),
        ("ccbot", "commoncrawl"), ("amazonbot", "amazon"), ("meta-externalagent", "meta"), ("meta-externalfetcher", "meta"),
        ("ahrefs", "ahrefs"), ("semrush", "semrush"), ("mj12bot", "majestic"), ("dotbot", "moz"), ("rogerbot", "moz"), ("screaming frog", "screamingfrog"),
        ("chrome-lighthouse", "lighthouse"), ("lighthouse", "lighthouse"), ("pagespeed", "lighthouse"), ("gtmetrix", "gtmetrix"),
        ("pingdom", "monitor"), ("uptimerobot", "monitor"), ("statuscake", "monitor"), ("site24x7", "monitor"), ("checkly", "monitor"),
        ("datadogsynthetics", "monitor"), ("newrelicpinger", "monitor"),
        ("headlesschrome", "headless"), ("headless", "headless"), ("phantomjs", "headless"), ("puppeteer", "headless"),
        ("playwright", "headless"), ("selenium", "headless"), ("webdriver", "headless"),
        ("facebookexternalhit", "preview"), ("facebookcatalog", "preview"), ("linkedinbot", "preview"), ("twitterbot", "preview"),
        ("slackbot", "preview"), ("slack-imgproxy", "preview"), ("discordbot", "preview"), ("telegrambot", "preview"), ("whatsapp", "preview"),
        ("skypeuripreview", "preview"), ("redditbot", "preview"), ("pinterest", "preview"), ("embedly", "preview"), ("iframely", "preview"),
        ("vkshare", "preview"), ("microsoftpreview", "preview"), ("snap url preview", "preview"), ("mastodon", "preview"),
        ("curl/", "tool"), ("wget/", "tool"), ("python-requests", "tool"), ("python-urllib", "tool"), ("python-httpx", "tool"),
        ("aiohttp", "tool"), ("httpclient", "tool"), ("java/", "tool"), ("okhttp", "tool"), ("go-http-client", "tool"), ("axios/", "tool"),
        ("node-fetch", "tool"), ("undici", "tool"), ("libwww-perl", "tool"), ("postmanruntime", "tool"), ("insomnia", "tool"),
        ("bot", "other"), ("crawler", "other"), ("crawl", "other"), ("spider", "other"), ("slurp", "other"), ("scraper", "other"),
        ("fetcher", "other"), ("preview", "preview"),
    ];

    /// <summary>Phones whose names contain a robot's word.</summary>
    private static readonly string[] NotBots = ["cubot"];

    /// <summary>
    /// The robot a user agent belongs to (crawlers, link previews, headless browsers, monitors,
    /// scripts), or null for a browser. None at all, or one that does not begin as every
    /// browser's does ("Mozilla/5.0"), is a robot too.
    /// </summary>
    public static string? BotName(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "unknown";
        var ua = userAgent.ToLowerInvariant();
        foreach (var (needle, bot) in Bots)
        {
            if (!ua.Contains(needle, StringComparison.Ordinal)) continue;
            if (bot == "other" && NotBots.Any(n => ua.Contains(n, StringComparison.Ordinal))) continue;
            return bot;
        }
        return ua.StartsWith("mozilla/", StringComparison.Ordinal) || ua.StartsWith("opera/", StringComparison.Ordinal) ? null : "tool";
    }

    /// <summary>mobile, tablet or desktop.</summary>
    public static string Device(string? userAgent)
    {
        var ua = (userAgent ?? "").ToLowerInvariant();
        if (ua.Contains("ipad") || ua.Contains("tablet") || ua.Contains("kindle") || ua.Contains("silk/") || ua.Contains("playbook")
            || (ua.Contains("android") && !ua.Contains("mobile")))
            return "tablet";
        if (ua.Contains("mobi") || ua.Contains("iphone") || ua.Contains("ipod") || ua.Contains("windows phone") || ua.Contains("blackberry")
            || ua.Contains("opera mini") || ua.Contains("android"))
            return "mobile";
        return "desktop";
    }

    /// <summary>The browser's family.</summary>
    public static string Browser(string? userAgent)
    {
        var ua = (userAgent ?? "").ToLowerInvariant();
        if (ua.Contains("edg/") || ua.Contains("edga/") || ua.Contains("edgios/")) return "Edge";
        if (ua.Contains("opr/") || ua.Contains("opera")) return "Opera";
        if (ua.Contains("samsungbrowser")) return "Samsung Internet";
        if (ua.Contains("firefox/") || ua.Contains("fxios/")) return "Firefox";
        if (ua.Contains("crios/") || ua.Contains("chrome/") || ua.Contains("chromium/")) return "Chrome";
        if (ua.Contains("safari/")) return "Safari";
        return "Other";
    }

    /// <summary>The operating system's family.</summary>
    public static string Os(string? userAgent)
    {
        var ua = (userAgent ?? "").ToLowerInvariant();
        if (ua.Contains("windows")) return "Windows";
        if (ua.Contains("iphone") || ua.Contains("ipad") || ua.Contains("ipod")) return "iOS";
        if (ua.Contains("cros ")) return "ChromeOS";
        if (ua.Contains("android")) return "Android";
        if (ua.Contains("mac os x") || ua.Contains("macintosh")) return "macOS";
        if (ua.Contains("linux")) return "Linux";
        return "Other";
    }

    /// <summary>The primary tag of a language ("de-CH" → "de"), or of the first in an Accept-Language list.</summary>
    public static string? Language(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var first = raw.Split([',', ';'], 2)[0].Trim();
        var primary = first.Split(['-', '_'], 2)[0].ToLowerInvariant();
        return primary.Length is >= 2 and <= 3 && primary.All(char.IsAsciiLetterLower) ? primary : null;
    }

    // ── Fields ───────────────────────────────────────────────────────────────

    /// <summary>A click's or an event's name: lower case, a word of letters, digits and _ . : -, at most 64.</summary>
    public static string? EventName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var name = NameNoise().Replace(Whitespace().Replace(Bound(raw.Trim()).ToLowerInvariant(), "-"), "");
        name = name.Trim('-', '.', ':');
        if (name.Length > MaxName) name = name[..MaxName];
        return name.Length == 0 ? null : name;
    }

    /// <summary>
    /// A utm value (source, medium or campaign): lower case, accents dropped, only letters a-z,
    /// digits, spaces and _ . + -, at most <see cref="MaxUtm"/>. None at all when it holds an @, or
    /// when it, or a word of it, looks like an id or a token (a GUID, a long number, a long hex
    /// string, a token's characters): a link made for one person is not counted as theirs.
    /// </summary>
    public static string? Utm(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var value = Whitespace().Replace(Printable(Decode(Bound(raw))).Trim(), " ");
        if (value.Contains('@') || NamesSomeone(value)) return null;
        var plain = UtmNoise().Replace(WithoutAccents(value).ToLowerInvariant(), "");
        plain = Whitespace().Replace(plain, " ").Trim();
        if (plain.Length > MaxUtm) plain = plain[..MaxUtm].TrimEnd();
        return plain.Length == 0 || NamesSomeone(plain) ? null : plain;
    }

    /// <summary>Whether a utm value, or any word of it, looks like an id or a token.</summary>
    private static bool NamesSomeone(string value)
    {
        if (IsId(value)) return true;
        foreach (var word in UtmWordBreak().Split(value))
            if (word.Length > 0 && (IsId(word) || IdNumber().IsMatch(word))) return true;
        return false;

        static bool IsId(string text) =>
            Guid.TryParse(text, out _) || LongNumber().IsMatch(text) || LongHex().IsMatch(text) || LooksLikeToken(text);
    }

    private static string WithoutAccents(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var kept = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) kept.Append(c);
        return kept.ToString();
    }

    /// <summary>
    /// The text without control (Cc) or format (Cf) characters: no line breaks, no invisible or
    /// direction-changing marks. A control that is a space (a tab, a line break) becomes one.
    /// </summary>
    public static string Printable(string text)
    {
        StringBuilder? kept = null;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (char.GetUnicodeCategory(c) is UnicodeCategory.Control or UnicodeCategory.Format)
            {
                kept ??= new StringBuilder(text, 0, i, text.Length);
                if (char.IsWhiteSpace(c)) kept.Append(' ');
                continue;
            }
            kept?.Append(c);
        }
        return kept?.ToString() ?? text;
    }

    /// <summary>At most <paramref name="max"/> UTF-16 units, cut between code points: never half of a surrogate pair.</summary>
    public static string Cut(string value, int max)
    {
        if (value.Length <= max) return value;
        return value[..(char.IsHighSurrogate(value[max - 1]) ? max - 1 : max)];
    }

    private static string Bound(string value) => Cut(value, MaxRaw);

    // ── The request ──────────────────────────────────────────────────────────

    /// <summary>
    /// The caller's address. The connection's, unless that is a private one (the platform's own
    /// proxy in front of the app): then the last address the proxy added to X-Forwarded-For.
    /// Used for the rate limit and, hashed, for the visitor; never stored.
    /// </summary>
    public static string ClientAddress(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress;
        if (remote is { IsIPv4MappedToIPv6: true }) remote = remote.MapToIPv4();
        if (remote == null || IsPrivate(remote))
        {
            var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
            var last = forwarded.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
            if (last != null && ParseAddress(last) is { } address) return address.ToString();
        }
        return remote?.ToString() ?? "unknown";
    }

    private static IPAddress? ParseAddress(string value)
    {
        if (IPAddress.TryParse(value, out var plain)) return plain;
        // "1.2.3.4:5678" or "[2001:db8::1]:443".
        if (IPEndPoint.TryParse(value, out var endpoint)) return endpoint.Address;
        return null;
    }

    private static bool IsPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return true;
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || (address.GetAddressBytes()[0] & 0xFE) == 0xFC;
        var b = address.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254)
            || (b[0] == 100 && b[1] >= 64 && b[1] <= 127);
    }

    /// <summary>What the request says of the browser: its address and agent (to hash), its robot if any, its families.</summary>
    public static SiteVisit Visit(HttpContext context)
    {
        var userAgent = context.Request.Headers.UserAgent.ToString();
        if (userAgent.Length > 512) userAgent = userAgent[..512];
        return new SiteVisit(
            ClientAddress(context),
            userAgent,
            BotName(userAgent),
            Device(userAgent),
            Browser(userAgent),
            Os(userAgent),
            Language(context.Request.Headers.AcceptLanguage.ToString()));
    }

    /// <summary>The site the events were sent from (the page's origin), so a referrer from it is not counted as another site.</summary>
    public static string? OwnHost(HttpRequest request) =>
        Host(request.Headers.Origin.ToString()) ?? Host(request.Headers.Referer.ToString());

    // ── The body ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The events of a posted body, <c>{ events: [...] }</c>, each checked and clipped; at most
    /// <paramref name="max"/>, and none of those that are not events. Never throws: junk gives
    /// none, and an event that cannot be read is left out alone.
    /// </summary>
    public static IReadOnlyList<SiteEventInput> Read(byte[] body, string? ownHost, int max)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 8 });
        }
        catch (JsonException)
        {
            return [];
        }
        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("events", out var list)
                || list.ValueKind != JsonValueKind.Array)
                return [];
            var events = new List<SiteEventInput>();
            var seen = 0;
            foreach (var item in list.EnumerateArray())
            {
                if (seen++ >= max) break;
                if (item.ValueKind != JsonValueKind.Object) continue;
                try
                {
                    if (ReadEvent(item, ownHost) is { } e) events.Add(e);
                }
                catch (Exception ex) when (ex is InvalidOperationException or FormatException or ArgumentException or OverflowException)
                {
                    // This event alone is junk; the rest of the post is kept.
                }
            }
            return events;
        }
    }

    private static SiteEventInput? ReadEvent(JsonElement item, string? ownHost)
    {
        var type = Text(item, "type");
        if (!SiteEventTypes.IsType(type)) return null;
        var sessionId = Text(item, "sessionId");
        if (sessionId == null || !SessionId().IsMatch(sessionId)) return null;

        // Signed in, the app's own use is not recorded: a sign-up event alone is kept.
        var signedIn = item.TryGetProperty("signedIn", out var flag) && flag.ValueKind == JsonValueKind.True;
        if (signedIn && type != SiteEventTypes.Event) return null;

        var name = type == SiteEventTypes.PageView ? null : EventName(Text(item, "name"));
        if (type != SiteEventTypes.PageView && name == null) return null;
        if (type == SiteEventTypes.Event && !SiteAnalyticsRules.EventNames.Contains(name!)) return null;
        var outbound = type == SiteEventTypes.Click && name == SiteAnalyticsRules.Outbound;
        var path = outbound ? Host(Text(item, "path")) : TemplatePath(Text(item, "path"));
        if (path == null || (!outbound && IsUntracked(path))) return null;

        var referrer = ReferrerHost(Text(item, "referrer"), ownHost);
        string? utmSource = null, utmMedium = null, utmCampaign = null;
        if (item.TryGetProperty("utm", out var utm) && utm.ValueKind == JsonValueKind.Object)
        {
            utmSource = Utm(Text(utm, "source"));
            utmMedium = Utm(Text(utm, "medium"));
            utmCampaign = Utm(Text(utm, "campaign"));
        }

        return new SiteEventInput
        {
            Type = type!,
            Name = name,
            Path = path,
            ReferrerHost = referrer,
            UtmSource = utmSource,
            UtmMedium = utmMedium,
            UtmCampaign = utmCampaign,
            Source = ClassifySource(utmSource, referrer),
            SessionId = sessionId,
            AtUtc = When(item),
            Language = Language(Text(item, "lang")),
            SignedIn = signedIn,
        };
    }

    /// <summary>The event's time: an ISO 8601 string, or milliseconds since 1970.</summary>
    private static DateTime? When(JsonElement item)
    {
        if (!item.TryGetProperty("at", out var at)) return null;
        if (at.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(Text(item, "at"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed))
            return parsed.UtcDateTime;
        if (at.ValueKind == JsonValueKind.Number && at.TryGetInt64(out var ms) && ms > 0 && ms < 253_402_300_799_000)
            return DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
        return null;
    }

    /// <summary>
    /// A string property, cut to a bound, without control or format characters, and trimmed;
    /// null when absent, not a string, or not text (a lone surrogate, "\ud800", which JSON can
    /// carry and a .NET string cannot be read from).
    /// </summary>
    private static string? Text(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String) return null;
        string? text;
        try
        {
            text = value.GetString();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = Printable(Bound(text)).Trim();
        return text.Length == 0 ? null : text;
    }
}
