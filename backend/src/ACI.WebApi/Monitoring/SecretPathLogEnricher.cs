using ACI.WebApi.Middleware;
using Serilog.Core;
using Serilog.Events;

namespace ACI.WebApi.Monitoring;

/// <summary>
/// Keeps a secret path segment (a contract's signing token, an unsubscribe token…) out of every
/// log event's properties, by the request log's own allowlist (<see cref="SecretPathRedactionMiddleware"/>).
/// </summary>
/// <remarks>
/// <para>
/// SecretPathRedactionMiddleware rewrites the path before the request log's completion event
/// reads it. Other events written during the request still carried the raw path: ASP.NET Core
/// opens a logging scope for each request with its path as it arrived (RequestPath), Serilog
/// adds a scope's values to every event inside it, and GlobalExceptionHandler logs the path an
/// exception was thrown on (Path). The console template prints only the message, which had the
/// latter; Application Insights keeps every property, which would have had both.
/// </para>
/// <para>
/// Sinks render the message from the properties as they stand after enrichment, so the message
/// is redacted too.
/// </para>
/// </remarks>
public sealed class SecretPathLogEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        List<LogEventProperty>? redacted = null;
        foreach (var (name, value) in logEvent.Properties)
        {
            if (value is ScalarValue { Value: string path } && path.StartsWith('/')
                && SecretPathRedactionMiddleware.RedactPath(path.Split('?', 2)[0]) is { } safe)
            {
                (redacted ??= new()).Add(new LogEventProperty(name, new ScalarValue(safe)));
            }
        }
        if (redacted == null) return;
        foreach (var property in redacted) logEvent.AddOrUpdateProperty(property);
    }
}
