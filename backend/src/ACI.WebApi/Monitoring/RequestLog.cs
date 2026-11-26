using Serilog;
using Serilog.AspNetCore;
using Serilog.Events;

namespace ACI.WebApi.Monitoring;

/// <summary>
/// The request log (Serilog's UseSerilogRequestLogging): one event per request, with who called
/// it, for which workspace, and from which address; but not the beacons every visitor's browser
/// sends by itself.
/// </summary>
/// <remarks>
/// The site analytics' posts (/api/site-analytics) and the browsers' crash reports
/// (/api/client-errors) come from every page a visitor opens. Logged, each would put the
/// visitor's IP address beside the moment they read a page: what the analytics take care never
/// to keep. So their event is at Verbose, below anything the log writes (Information and up),
/// unless the request failed (an exception, a 5xx); and for them the address is never added.
/// </remarks>
public static class RequestLog
{
    public const string MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

    /// <summary>The paths of the anonymous beacons, and everything under them.</summary>
    public static readonly IReadOnlyList<PathString> QuietPaths = [new("/api/site-analytics"), new("/api/client-errors")];

    /// <summary>Sets the request log's message, level and properties.</summary>
    public static void Configure(RequestLoggingOptions options)
    {
        options.MessageTemplate = MessageTemplate;
        options.GetLevel = Level;
        options.EnrichDiagnosticContext = Enrich;
    }

    /// <summary>Whether a request is one of the beacons (<see cref="QuietPaths"/>), whatever the case of its path.</summary>
    public static bool IsQuiet(PathString path) =>
        QuietPaths.Any(quiet => path.StartsWithSegments(quiet, StringComparison.OrdinalIgnoreCase));

    /// <summary>Error for an exception or a 5xx; Verbose (not written) for a beacon; Information for the rest.</summary>
    public static LogEventLevel Level(HttpContext context, double elapsedMs, Exception? exception)
    {
        if (exception != null || context.Response.StatusCode > 499) return LogEventLevel.Error;
        return IsQuiet(context.Request.Path) ? LogEventLevel.Verbose : LogEventLevel.Information;
    }

    /// <summary>Who called, for which workspace, and, except for a beacon, from which address.</summary>
    public static void Enrich(IDiagnosticContext diagnosticContext, HttpContext httpContext)
    {
        diagnosticContext.Set("UserId", httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous");
        diagnosticContext.Set("OrganizationId", httpContext.Request.Headers["X-Organization-Id"].FirstOrDefault() ?? "none");
        if (!IsQuiet(httpContext.Request.Path))
            diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }
}
