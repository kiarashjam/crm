using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ACI.WebApi.Monitoring;

/// <summary>
/// What /health answers: every check's status, what it says and its figures, as JSON; 200 when
/// Healthy or Degraded, 503 when Unhealthy, so a probe that reads only the status code sees a
/// server that cannot do its work, and one that reads the body sees why.
/// </summary>
/// <remarks>
/// /health is anonymous. A check's exception is never written, and neither is the description
/// of a check that threw: the health check service puts the exception's message there, and a
/// SQL error's message names the server, the database and the login. Those are in the log
/// (the health check service logs every check that fails).
/// </remarks>
public static class HealthResponse
{
    /// <summary>Said in place of the description of a check that threw.</summary>
    public const string FailedWithoutDetails = "The check failed. The details are in the server log.";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static HealthCheckOptions Options() => new()
    {
        ResponseWriter = WriteAsync,
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status200OK,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
        },
    };

    /// <summary>
    /// <c>{ status, totalDurationMs, checks: [{ name, status, description, durationMs, data }] }</c>.
    /// </summary>
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var body = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Exception != null ? FailedWithoutDetails : entry.Value.Description,
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1),
                data = entry.Value.Exception != null ? EmptyData : entry.Value.Data,
            }),
        };
        return JsonSerializer.SerializeAsync(context.Response.Body, body, Json, context.RequestAborted);
    }

    private static readonly IReadOnlyDictionary<string, object> EmptyData = new Dictionary<string, object>();
}
