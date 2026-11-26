using System.Threading.RateLimiting;
using ACI.Application.Configuration;
using ACI.Application.Services;
using ACI.WebApi.Filters;
using ACI.WebApi.SiteAnalytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;

namespace ACI.WebApi.Controllers;

/// <summary>
/// The site analytics of cadencee.ai: first-party, cookieless, and for the platform's admins.
/// The tracker in the app (src/app/lib/siteAnalytics.ts) posts what visitors do; platform admins
/// read the figures back (see <see cref="ISiteAnalytics"/>).
/// </summary>
/// <remarks>
/// <para>
/// The ingest is anonymous, so it is rate limited by address (<see cref="PerMinute"/> a minute)
/// and for everyone together (<see cref="EveryonePerMinute"/>), held to <see cref="MaxBodyBytes"/>,
/// takes at most <see cref="MaxEvents"/> events a post, stores at most SiteAnalytics:DailyEventCap
/// events a day per server, and checks and clips every field (SiteTraffic): a path is its
/// route's template with no query string and no token, a referrer its host; a customer's page
/// is never kept, and signed in only a sign-up event is, as "signed in". Robots are counted and
/// dropped. A browser that sends Do Not Track or Global Privacy Control is not read at all.
/// Whatever is posted, it answers 204: the tracker never waits for it, and a visitor's page
/// never fails because of it.
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
    /// <summary>Posts one address may make in a minute: a tab posts every five seconds at most, so this is room for two tabs and their beacons as they leave.</summary>
    public const int PerMinute = 30;

    /// <summary>Posts everyone together may make in a minute: many addresses at once still fill the table by a bounded amount.</summary>
    public const int EveryonePerMinute = 600;

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
    /// <c>{ events: [{ type, name?, path, referrer?, utm?: { source, medium, campaign }, sessionId,
    /// at?, lang?, signedIn? }] }</c>, as JSON in any content type. 204 whatever it holds, and
    /// without reading it when the browser sends <c>Sec-GPC: 1</c> or <c>DNT: 1</c>; 413 over
    /// 32 KB; 429 past <see cref="PerMinute"/> a minute from one address, or
    /// <see cref="EveryonePerMinute"/> from everyone (<see cref="Limiter"/>).
    /// </summary>
    [HttpPost("events")]
    [AllowAnonymous]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> Events(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        // The browser asks not to be tracked: nothing is read, nothing kept.
        if (AsksNotToBeTracked(Request)) return NoContent();
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
    /// <see cref="SiteSummaryDto"/> for the rest. The audience is whose events: "visitors" (the
    /// default: the website's, signed out, and every custom event such as a sign-up), "signed-in"
    /// (the app's users) or "everyone"; 400 for anything else.
    /// </summary>
    [HttpGet("summary")]
    [Authorize]
    [PlatformAdmin]
    public async Task<ActionResult<SiteSummaryDto>> Summary([FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? audience, CancellationToken ct)
    {
        if (!SiteAudiences.TryParse(audience, out var who)) return NotAnAudience();
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
        return Ok(await _analytics.SummaryAsync(start, end, who, ct));
    }

    /// <summary>Who of the audience (as for the summary) is on the site in the last five minutes, where, and their last thirty events.</summary>
    [HttpGet("live")]
    [Authorize]
    [PlatformAdmin]
    public async Task<ActionResult<SiteLiveDto>> Live([FromQuery] string? audience, CancellationToken ct)
    {
        if (!SiteAudiences.TryParse(audience, out var who)) return NotAnAudience();
        Response.Headers.CacheControl = "no-store";
        return Ok(await _analytics.LiveAsync(DateTime.UtcNow, who, ct));
    }

    /// <summary>
    /// The latest sessions of <paramref name="day"/> (yyyy-MM-dd, Zurich; today by default) with
    /// events of the audience (as for the summary), newest first, at most
    /// <paramref name="limit"/> (50 by default, 200 at most), each with its steps of the audience.
    /// </summary>
    [HttpGet("sessions")]
    [Authorize]
    [PlatformAdmin]
    public async Task<ActionResult<IReadOnlyList<SiteSessionDto>>> Sessions([FromQuery] string? day, [FromQuery] int? limit, [FromQuery] string? audience, CancellationToken ct)
    {
        if (!SiteAudiences.TryParse(audience, out var who)) return NotAnAudience();
        var parsed = SiteAnalyticsRules.ParseDay(day);
        if (!string.IsNullOrWhiteSpace(day) && parsed == null) return Refused("A day is written yyyy-MM-dd.");
        Response.Headers.CacheControl = "no-store";
        return Ok(await _analytics.SessionsAsync(
            parsed ?? SiteAnalyticsRules.DayOf(DateTime.UtcNow),
            Math.Clamp(limit ?? SiteAnalyticsRules.DefaultSessions, 1, SiteAnalyticsRules.MaxSessions),
            who,
            ct));
    }

    /// <summary>Whether the request carries Global Privacy Control (<c>Sec-GPC: 1</c>) or Do Not Track (<c>DNT: 1</c>).</summary>
    public static bool AsksNotToBeTracked(HttpRequest request) =>
        request.Headers["Sec-GPC"].ToString().Trim() == "1" || request.Headers["DNT"].ToString().Trim() == "1";

    /// <summary>
    /// The tracker's rate limits, chained: first the caller's address (<paramref name="perAddress"/>
    /// a minute, SiteTraffic.ClientAddress), then everyone's together (<paramref name="everyone"/> a
    /// minute), so an address over its own limit takes nothing from everyone's. It is the app's
    /// global limiter (Program.cs), as an endpoint's policy is one limiter and cannot chain; every
    /// request but the tracker's post goes through it unlimited.
    /// </summary>
    public static PartitionedRateLimiter<HttpContext> Limiter(int perAddress = PerMinute, int everyone = EveryonePerMinute) =>
        PartitionedRateLimiter.CreateChained(
            PartitionedRateLimiter.Create<HttpContext, string>(context => IsIngest(context)
                ? RateLimitPartition.GetFixedWindowLimiter("site-analytics:" + SiteTraffic.ClientAddress(context), _ => PerMinuteWindow(perAddress))
                : RateLimitPartition.GetNoLimiter(string.Empty)),
            PartitionedRateLimiter.Create<HttpContext, string>(context => IsIngest(context)
                ? RateLimitPartition.GetFixedWindowLimiter("site-analytics", _ => PerMinuteWindow(everyone))
                : RateLimitPartition.GetNoLimiter(string.Empty)));

    /// <summary>Whether the request is routed to the tracker's post (<see cref="Events"/>).</summary>
    public static bool IsIngest(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>() is { } action
        && action.ControllerTypeInfo.AsType() == typeof(SiteAnalyticsController)
        && action.ActionName == nameof(Events);

    private static FixedWindowRateLimiterOptions PerMinuteWindow(int permits) => new()
    {
        PermitLimit = permits,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
    };

    private ObjectResult Refused(string detail) => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Not a period", detail: detail);

    private ObjectResult NotAnAudience() => Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Not an audience",
        detail: $"The audience is \"{SiteAudiences.Visitors}\" (the default), \"{SiteAudiences.SignedIn}\" or \"{SiteAudiences.Everyone}\".");

    private ObjectResult TooLarge() => Problem(
        statusCode: StatusCodes.Status413PayloadTooLarge,
        title: "Too large",
        detail: $"A post is at most {MaxBodyBytes / 1024} KB.");
}
