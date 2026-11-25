using ACI.Application.Configuration;
using ACI.Application.Services;
using ACI.WebApi.Filters;
using ACI.WebApi.SiteAnalytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ACI.WebApi.Controllers;

/// <summary>
/// The site analytics of cadencee.ai: first-party, cookieless, and for the platform's admins.
/// The tracker in the app (src/app/lib/siteAnalytics.ts) posts what visitors do; platform admins
/// read the figures back (see <see cref="ISiteAnalytics"/>).
/// </summary>
/// <remarks>
/// <para>
/// The ingest is anonymous, so it is rate limited by address (<see cref="PerMinute"/> a minute),
/// held to <see cref="MaxBodyBytes"/>, takes at most <see cref="MaxEvents"/> events a post, and
/// checks and clips every field (SiteTraffic): a path is its route's template with no query
/// string and no token, a referrer its host, a signed-in visitor only "signed in". Robots are
/// counted and dropped. Whatever is posted, it answers 204: the tracker never waits for it,
/// and a visitor's page never fails because of it.
/// </para>
/// <para>
/// The body is read here rather than bound: navigator.sendBeacon posts text/plain, which a
/// browser sends to another origin without asking first, and which [FromBody] would refuse.
/// </para>
/// </remarks>
[ApiController]
[Route("api/site-analytics")]
[Produces("application/json")]
public sealed class SiteAnalyticsController : ControllerBase
{
    public const string RateLimitPolicy = "site-analytics";

    /// <summary>Posts one address may make in a minute: a tab posts every five seconds at most, so this is room for several people behind one address.</summary>
    public const int PerMinute = 120;

    /// <summary>The largest body taken.</summary>
    public const int MaxBodyBytes = 32 * 1024;

    /// <summary>Events taken from one post; the rest are left.</summary>
    public const int MaxEvents = 25;

    private readonly ISiteAnalytics _analytics;
    private readonly SiteAnalyticsSettings _settings;
    private readonly ILogger<SiteAnalyticsController> _logger;

    public SiteAnalyticsController(ISiteAnalytics analytics, IOptions<SiteAnalyticsSettings> settings, ILogger<SiteAnalyticsController> logger)
    {
        _analytics = analytics;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// <c>{ events: [{ type, name?, path, title?, referrer?, utm?: { source, medium, campaign, term, content },
    /// sessionId, at?, screen?: { w, h }, lang?, signedIn? }] }</c>, as JSON in any content type.
    /// 204 whatever it holds; 413 over 32 KB; 429 past <see cref="PerMinute"/> a minute from one address.
    /// </summary>
    [HttpPost("events")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicy)]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> Events(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        if (Request.ContentLength > MaxBodyBytes) return TooLarge();

        byte[]? body;
        try
        {
            body = await ClientErrorsController.ReadAtMostAsync(Request.Body, MaxBodyBytes, ct);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return TooLarge();
        }
        if (body == null) return TooLarge();
        if (!_settings.Enabled) return NoContent();

        try
        {
            var events = SiteTraffic.Read(body, SiteTraffic.OwnHost(Request), MaxEvents);
            if (events.Count > 0) await _analytics.IngestAsync(SiteTraffic.Visit(HttpContext), events, DateTime.UtcNow, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Counting a visit never fails the visitor's page: what went wrong is logged, and the answer is the same.
            _logger.LogWarning(ex, "Site analytics events could not be recorded");
        }
        return NoContent();
    }

    /// <summary>
    /// The figures from <paramref name="from"/> to <paramref name="to"/> (yyyy-MM-dd, Europe/Zurich,
    /// both included; the last 30 days to today by default), with the same figures for the period
    /// as long just before. "visitors" counts each visitor once a day: over several days it is the
    /// days' distinct visitors summed, since nobody is followed from one day to the next. See
    /// <see cref="SiteSummaryDto"/> for the rest.
    /// </summary>
    [HttpGet("summary")]
    [Authorize]
    [PlatformAdmin]
    public async Task<ActionResult<SiteSummaryDto>> Summary([FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
    {
        var parsedTo = SiteAnalyticsRules.ParseDay(to);
        var parsedFrom = SiteAnalyticsRules.ParseDay(from);
        if ((!string.IsNullOrWhiteSpace(to) && parsedTo == null) || (!string.IsNullOrWhiteSpace(from) && parsedFrom == null))
            return Refused("A day is written yyyy-MM-dd.");
        var end = parsedTo ?? SiteAnalyticsRules.DayOf(DateTime.UtcNow);
        var start = parsedFrom ?? end.AddDays(-(SiteAnalyticsRules.DefaultRangeDays - 1));
        if (start > end) return Refused("The period starts after it ends.");
        if (end.DayNumber - start.DayNumber + 1 > SiteAnalyticsRules.MaxRangeDays)
            return Refused($"A period is at most {SiteAnalyticsRules.MaxRangeDays} days.");

        Response.Headers.CacheControl = "no-store";
        return Ok(await _analytics.SummaryAsync(start, end, ct));
    }

    /// <summary>Who is on the site in the last five minutes, where, and the last thirty events.</summary>
    [HttpGet("live")]
    [Authorize]
    [PlatformAdmin]
    public async Task<ActionResult<SiteLiveDto>> Live(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(await _analytics.LiveAsync(DateTime.UtcNow, ct));
    }

    /// <summary>
    /// The latest sessions of <paramref name="day"/> (yyyy-MM-dd, Zurich; today by default), newest
    /// first, at most <paramref name="limit"/> (50 by default, 200 at most), each with its steps.
    /// </summary>
    [HttpGet("sessions")]
    [Authorize]
    [PlatformAdmin]
    public async Task<ActionResult<IReadOnlyList<SiteSessionDto>>> Sessions([FromQuery] string? day, [FromQuery] int? limit, CancellationToken ct)
    {
        var parsed = SiteAnalyticsRules.ParseDay(day);
        if (!string.IsNullOrWhiteSpace(day) && parsed == null) return Refused("A day is written yyyy-MM-dd.");
        Response.Headers.CacheControl = "no-store";
        return Ok(await _analytics.SessionsAsync(
            parsed ?? SiteAnalyticsRules.DayOf(DateTime.UtcNow),
            Math.Clamp(limit ?? SiteAnalyticsRules.DefaultSessions, 1, SiteAnalyticsRules.MaxSessions),
            ct));
    }

    private ObjectResult Refused(string detail) => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Not a period", detail: detail);

    private ObjectResult TooLarge() => Problem(
        statusCode: StatusCodes.Status413PayloadTooLarge,
        title: "Too large",
        detail: $"A post is at most {MaxBodyBytes / 1024} KB.");
}
