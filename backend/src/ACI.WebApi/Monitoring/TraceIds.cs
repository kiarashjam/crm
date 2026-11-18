using System.Diagnostics;

namespace ACI.WebApi.Monitoring;

/// <summary>
/// The trace id an error response gives: the request's W3C trace id, which Application Insights
/// shows as its Operation ID, so one copied from an error can be searched for there.
/// </summary>
/// <remarks>
/// HttpContext.TraceIdentifier (the connection and request number, "0HN…:00000002") is in no
/// telemetry; MVC's default, Activity.Id, is the whole traceparent ("00-…-…-00"). The request's
/// own trace id stands in only when there is no W3C activity to take one from.
/// </remarks>
public static class TraceIds
{
    public static string Of(HttpContext context) =>
        Activity.Current is { IdFormat: ActivityIdFormat.W3C } activity && activity.TraceId != default
            ? activity.TraceId.ToHexString()
            : context.TraceIdentifier;
}
