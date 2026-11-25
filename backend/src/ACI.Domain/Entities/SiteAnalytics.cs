namespace ACI.Domain.Entities;

/// <summary>
/// One thing a visitor did on the public site (cadencee.ai): a page they opened, a tracked
/// button or outbound link they clicked, or a step of the sign-up the app reported. First-party
/// and cookieless; written by POST /api/site-analytics/events.
/// </summary>
/// <remarks>
/// <para>
/// Global, not a workspace's: there is no OrganizationId, so no workspace filter applies and
/// only platform admins read it (SiteAnalyticsController).
/// </para>
/// <para>
/// Nothing here names a person. The visitor is a hash of the day's random salt, the shortened
/// address and the browser, so the same person is a different visitor tomorrow and, once the
/// salt is destroyed, cannot be found again; the session is a hash of a random id the tab keeps
/// until it closes, with the same salt. The path is the route's template (/leads/:id) with no
/// query string and no token; the referrer is a host name. Signed in, only the sign-up events
/// are kept, as "signed in". Rows are deleted 400 days after their day (SiteAnalyticsDigestService).
/// </para>
/// </remarks>
public class SiteEvent
{
    public long Id { get; set; }

    /// <summary>When it happened, as the browser said within a few minutes of the server's clock, else when it arrived.</summary>
    public DateTime OccurredAtUtc { get; set; }

    /// <summary>The calendar day it happened on in Europe/Zurich: what every report counts by.</summary>
    public DateOnly Day { get; set; }

    /// <summary><see cref="SiteEventTypes"/>.</summary>
    public string Type { get; set; } = SiteEventTypes.PageView;

    /// <summary>For a click or an event: its name ("cta-signup", "outbound", "signup_completed"). Null for a page view.</summary>
    public string? Name { get; set; }

    /// <summary>The page, as its route's template; for an outbound click, the host it led to.</summary>
    public string Path { get; set; } = "/";

    /// <summary>The host of the site the visit came from; null when it came from nowhere or from this site.</summary>
    public string? ReferrerHost { get; set; }

    /// <summary>The link's utm_source, medium and campaign: lower case, plain characters, never an id, a token or an address.</summary>
    public string? UtmSource { get; set; }
    public string? UtmMedium { get; set; }
    public string? UtmCampaign { get; set; }

    /// <summary>
    /// Where the visit came from: utm_source when the link had one, else the referrer's kind
    /// (google, bing, duckduckgo, linkedin, facebook, instagram, x, ai, email), its host for
    /// any other site, or "direct".
    /// </summary>
    public string Source { get; set; } = "direct";

    /// <summary>A hash of the shortened address (IPv4 /24, IPv6 /48) and the browser with the day's random salt: counts a visitor once a day, never follows them to the next.</summary>
    public string VisitorHash { get; set; } = string.Empty;

    /// <summary>A hash of the tab's random session id with the day's salt: groups one visit's events. The id itself is not kept.</summary>
    public string SessionKey { get; set; } = string.Empty;

    /// <summary>mobile, tablet or desktop, from the browser's user agent.</summary>
    public string Device { get; set; } = "desktop";

    /// <summary>The browser's family (Chrome, Safari, Firefox, Edge…).</summary>
    public string Browser { get; set; } = "Other";

    /// <summary>The operating system's family (Windows, macOS, iOS, Android…).</summary>
    public string Os { get; set; } = "Other";

    /// <summary>The browser's language, its primary tag only ("de", "fr").</summary>
    public string? Language { get; set; }

    /// <summary>Whether someone was signed in to the app, never who: only a sign-up event is kept signed in.</summary>
    public bool SignedIn { get; set; }
}

/// <summary>What a <see cref="SiteEvent"/> records.</summary>
public static class SiteEventTypes
{
    public const string PageView = "page_view";
    public const string Click = "click";
    public const string Event = "event";

    public static bool IsType(string? value) => value is PageView or Click or Event;
}

/// <summary>
/// Requests from robots (crawlers, link previews, headless browsers, monitors), counted per day
/// and robot instead of being recorded as visits.
/// </summary>
public class SiteBotHit
{
    public DateOnly Day { get; set; }

    /// <summary>Which robot, as far as its user agent says ("googlebot", "headless", "other").</summary>
    public string Bot { get; set; } = string.Empty;

    /// <summary>Events it sent that day.</summary>
    public int Hits { get; set; }
}

/// <summary>
/// One Zurich day's salt for the site analytics' hashes: 32 random bytes, made by whichever
/// server needs it first (the day is the key, so every server uses the same), kept protected,
/// and deleted once the day is over and the next one too. Nothing it was used on can be
/// recomputed after that: there is no secret it could be made again from.
/// </summary>
public class SiteAnalyticsSalt
{
    /// <summary>The day it salts (Europe/Zurich).</summary>
    public DateOnly Day { get; set; }

    /// <summary>The random salt, protected with the server's data-protection keys.</summary>
    public string ProtectedSalt { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// The daily site analytics email for one day, taken by inserting this row (the day is its key),
/// so two servers, or one restarted, never send the same day twice.
/// </summary>
public class SiteAnalyticsDigest
{
    /// <summary>The day it reports on (Europe/Zurich): yesterday, when it was sent.</summary>
    public DateOnly Day { get; set; }

    public DateTime SentAtUtc { get; set; }

    /// <summary>How many platform admins it was queued for.</summary>
    public int Recipients { get; set; }
}
