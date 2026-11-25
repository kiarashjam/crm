using System.Globalization;
using ACI.Application.Common;

namespace ACI.Application.Services;

/// <summary>
/// First-party, cookieless visitor analytics of cadencee.ai: what the tracker (src/app/lib/siteAnalytics.ts)
/// posts, and what platform admins read back.
/// </summary>
/// <remarks>
/// <para>
/// Every figure counts by calendar day in Europe/Zurich. A visitor is a hash of that day's salt,
/// the address and the browser, so "visitors" over several days is the distinct visitors of
/// each day, summed: the same person on two days is two visitors, and nobody can be followed
/// from one day to the next.
/// </para>
/// <para>
/// Reads aggregate in SQL (counts per day, per page, per session), and only the per-session
/// rows of a period (at most <see cref="SiteAnalyticsRules.MaxSessionsRead"/>, newest first) are
/// folded in memory into the session figures: plenty for a young site, and bounded however
/// much traffic comes.
/// </para>
/// </remarks>
public interface ISiteAnalytics
{
    /// <summary>
    /// Records what one request posted: its events, each already checked and clipped, or, for a
    /// robot, one more count of its hits. One insert for the lot.
    /// </summary>
    Task IngestAsync(SiteVisit visit, IReadOnlyList<SiteEventInput> events, DateTime nowUtc, CancellationToken ct = default);

    /// <summary>The figures for <paramref name="from"/> to <paramref name="to"/> (Zurich days, inclusive), and the same for the period as long before it.</summary>
    Task<SiteSummaryDto> SummaryAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>Who is on the site now (the last five minutes) and the last thirty events.</summary>
    Task<SiteLiveDto> LiveAsync(DateTime nowUtc, CancellationToken ct = default);

    /// <summary>The day's latest sessions, newest first, each with its steps.</summary>
    Task<IReadOnlyList<SiteSessionDto>> SessionsAsync(DateOnly day, int limit, CancellationToken ct = default);

    /// <summary>Deletes what is older than <see cref="SiteAnalyticsRules.RetentionDays"/> days. Returns the events deleted.</summary>
    Task<int> PurgeAsync(DateTime nowUtc, CancellationToken ct = default);
}

/// <summary>
/// The daily site analytics email: from 07:30 Europe/Zurich, yesterday's figures to every
/// platform admin, once a day.
/// </summary>
public interface ISiteAnalyticsDigest
{
    /// <summary>Sends yesterday's email if it is due and has not gone. Returns how many admins it was queued for.</summary>
    Task<int> SendDueAsync(DateTime nowUtc, CancellationToken ct = default);
}

/// <summary>
/// The platform's admins (Platform:AdminEmails): the people who run cadencee.ai itself, above
/// every workspace.
/// </summary>
public interface IPlatformAdmins
{
    /// <summary>The configured addresses, lower case, each once; empty when none is set.</summary>
    IReadOnlyList<string> Emails { get; }

    /// <summary>Whether this address is one of them, whatever its case.</summary>
    bool IsAdminEmail(string? email);

    /// <summary>Whether this account is a platform admin: its address is one of them, and it has confirmed that address.</summary>
    Task<bool> IsPlatformAdminAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>The request the events came in: what is known of the browser, never kept as such.</summary>
/// <param name="Address">The caller's IP address: hashed with the day's salt, never stored.</param>
/// <param name="UserAgent">Hashed with the address, and read for the families below; never stored.</param>
/// <param name="Bot">The robot it is, when the user agent says so: its events are counted, not recorded.</param>
/// <param name="Language">The primary tag of Accept-Language, for an event that sends none.</param>
public sealed record SiteVisit(
    string Address,
    string UserAgent,
    string? Bot,
    string Device,
    string Browser,
    string Os,
    string? Language);

/// <summary>One event as the tracker sent it, checked and clipped (SiteTraffic in the API).</summary>
public sealed record SiteEventInput
{
    public required string Type { get; init; }
    public string? Name { get; init; }
    public required string Path { get; init; }
    public string? Title { get; init; }
    public string? ReferrerHost { get; init; }
    public string? UtmSource { get; init; }
    public string? UtmMedium { get; init; }
    public string? UtmCampaign { get; init; }
    public string? UtmTerm { get; init; }
    public string? UtmContent { get; init; }
    public required string Source { get; init; }

    /// <summary>The tab's random session id: hashed with the day's salt, never stored.</summary>
    public required string SessionId { get; init; }

    /// <summary>When the browser says it happened; taken only within a few minutes of the server's clock.</summary>
    public DateTime? AtUtc { get; init; }

    public string? ScreenBucket { get; init; }
    public string? Language { get; init; }
    public bool SignedIn { get; init; }
}

/// <summary>The limits and calendar the endpoints, the email and the clean-up share.</summary>
public static class SiteAnalyticsRules
{
    /// <summary>The calendar every day is counted on.</summary>
    public const string TimeZone = "Europe/Zurich";

    /// <summary>How long events are kept, in days after their day.</summary>
    public const int RetentionDays = 400;

    /// <summary>The longest period one summary covers.</summary>
    public const int MaxRangeDays = 366;

    /// <summary>The period a summary covers when none is asked for.</summary>
    public const int DefaultRangeDays = 30;

    /// <summary>Rows in each top list.</summary>
    public const int TopListSize = 20;

    /// <summary>The sessions of one period folded in memory, newest first; more are left out of the session figures.</summary>
    public const int MaxSessionsRead = 100_000;

    /// <summary>"Now", for the live view.</summary>
    public static readonly TimeSpan LiveWindow = TimeSpan.FromMinutes(5);

    /// <summary>The latest events the live view lists.</summary>
    public const int RecentEvents = 30;

    /// <summary>Sessions the journeys view lists by default, and at most.</summary>
    public const int DefaultSessions = 50;
    public const int MaxSessions = 200;

    /// <summary>Steps of one session the journeys view lists; its counts cover them all.</summary>
    public const int MaxSteps = 100;

    /// <summary>The event the app reports when an account has been made.</summary>
    public const string SignupCompleted = "signup_completed";

    /// <summary>A click on a link to another site; its path is that site's host.</summary>
    public const string Outbound = "outbound";

    /// <summary>The source of a visit with no referrer and no utm_source.</summary>
    public const string Direct = "direct";

    /// <summary>Where the dashboard lives in the app.</summary>
    public const string DashboardPath = "/admin/analytics";

    public static TimeZoneInfo Zone => RegionalSettings.ZoneOf(TimeZone);

    /// <summary>The Zurich day <paramref name="utc"/> falls on.</summary>
    public static DateOnly DayOf(DateTime utc) => RegionalSettings.TodayIn(Zone, utc);

    /// <summary>yyyy-MM-dd.</summary>
    public static string Format(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A yyyy-MM-dd day, or null.</summary>
    public static DateOnly? ParseDay(string? value) =>
        DateOnly.TryParseExact((value ?? "").Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) ? day : null;
}

// ── What the endpoints answer ────────────────────────────────────────────────

/// <param name="Visitors">Distinct visitors of each day, summed over the days (a visitor is counted once a day).</param>
/// <param name="BounceRate">The share of sessions, 0 to 1, with one page view and no click or event.</param>
/// <param name="AvgSessionSeconds">The average of each session's last event less its first; a one-page session counts as 0.</param>
/// <param name="Signups">signup_completed events.</param>
/// <param name="BotHits">Events robots sent, counted and dropped.</param>
public sealed record SiteTotalsDto(
    int Visitors,
    int Sessions,
    int PageViews,
    double BounceRate,
    double PagesPerSession,
    double AvgSessionSeconds,
    int Signups,
    int BotHits);

/// <param name="Date">yyyy-MM-dd, Zurich.</param>
public sealed record SiteDailyDto(string Date, int Visitors, int Sessions, int PageViews, int Signups);

public sealed record SitePageDto(string Path, int Views, int Visitors);

/// <summary>A page sessions began (or ended) on. <paramref name="Views"/> is the same as <paramref name="Sessions"/>: the sessions that entered (or left) there.</summary>
public sealed record SiteEntryPageDto(string Path, int Sessions, int Visitors, int Views);

public sealed record SiteSourceDto(string Source, int Sessions, int Visitors);

public sealed record SiteReferrerDto(string Host, int Sessions);

public sealed record SiteCampaignDto(string Campaign, string? Source, string? Medium, int Sessions);

public sealed record SiteNamedCountDto(string Name, int Sessions);

public sealed record SiteEventCountDto(string Name, int Count);

/// <param name="From">yyyy-MM-dd, Zurich, inclusive.</param>
/// <param name="To">yyyy-MM-dd, Zurich, inclusive.</param>
/// <param name="Previous">The same figures for the period of the same length just before.</param>
/// <param name="Daily">Every day of the period, oldest first, zeros included.</param>
public sealed record SiteSummaryDto(
    string From,
    string To,
    SiteTotalsDto Totals,
    SiteTotalsDto Previous,
    IReadOnlyList<SiteDailyDto> Daily,
    IReadOnlyList<SitePageDto> TopPages,
    IReadOnlyList<SiteEntryPageDto> EntryPages,
    IReadOnlyList<SiteEntryPageDto> ExitPages,
    IReadOnlyList<SiteSourceDto> Sources,
    IReadOnlyList<SiteReferrerDto> Referrers,
    IReadOnlyList<SiteCampaignDto> Campaigns,
    IReadOnlyList<SiteNamedCountDto> Devices,
    IReadOnlyList<SiteNamedCountDto> Browsers,
    IReadOnlyList<SiteNamedCountDto> Os,
    IReadOnlyList<SiteNamedCountDto> Languages,
    IReadOnlyList<SiteEventCountDto> Events);

public sealed record SiteLivePageDto(string Path, int Visitors);

public sealed record SiteLiveEventDto(DateTime At, string Type, string Path, string? Name, string Source, string Device);

/// <param name="VisitorsNow">Distinct visitors with an event in the last five minutes.</param>
/// <param name="Pages">Where each of those sessions is: the page of its latest event.</param>
/// <param name="Recent">The last thirty events, newest first.</param>
public sealed record SiteLiveDto(int VisitorsNow, int SessionsNow, IReadOnlyList<SiteLivePageDto> Pages, IReadOnlyList<SiteLiveEventDto> Recent);

public sealed record SiteStepDto(DateTime At, string Type, string Path, string? Name);

/// <param name="Id">Short and opaque: a prefix of the session's hash.</param>
/// <param name="Converted">Whether an account was made in it (signup_completed).</param>
public sealed record SiteSessionDto(
    string Id,
    DateTime StartedAt,
    DateTime EndedAt,
    int DurationSeconds,
    string Source,
    string? ReferrerHost,
    string? Campaign,
    string Device,
    string Browser,
    string? Language,
    bool SignedIn,
    int PageViews,
    IReadOnlyList<SiteStepDto> Steps,
    bool Converted);
