using ACI.WebApi.Middleware;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel;

namespace ACI.WebApi.Monitoring;

/// <summary>
/// Application Insights, when a connection string is configured: requests, dependencies and
/// Serilog's traces and exceptions (Program.cs) go to it as the cloud role "cadence-api".
/// Without one, nothing is registered and nothing changes: tests and local runs need none.
/// </summary>
public static class ApplicationInsights
{
    /// <summary>The cloud role every item carries, so the API is one node on the application map.</summary>
    public const string RoleName = "cadence-api";

    /// <summary>
    /// The connection string: APPLICATIONINSIGHTS_CONNECTION_STRING (what App Service sets), or
    /// ApplicationInsights:ConnectionString. Null when neither is set.
    /// </summary>
    public static string? ConnectionString(IConfiguration configuration) =>
        new[] { configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"], configuration["ApplicationInsights:ConnectionString"] }
            .Select(value => value?.Trim())
            .FirstOrDefault(value => !string.IsNullOrEmpty(value));

    /// <summary>Registers Application Insights when it is configured. Returns whether it was.</summary>
    public static bool AddIfConfigured(IServiceCollection services, IConfiguration configuration)
    {
        if (ConnectionString(configuration) is not { } connectionString) return false;

        services.AddApplicationInsightsTelemetry(options =>
        {
            options.ConnectionString = connectionString;
            // Sampled by MonitoringAwareSampling instead (below), which never drops an exception
            // or a monitoring.* event.
            options.EnableAdaptiveSampling = false;
            // An exception a request throws reaches GlobalExceptionHandler, which logs it, and
            // Serilog sends it: tracked here as well, each would arrive twice.
            options.RequestCollectionOptions.TrackExceptions = false;
            // The SDK never reads request bodies; nothing here asks it to.
        });
        services.AddSingleton<ITelemetryInitializer, RoleNameInitializer>();
        services.AddSingleton<ITelemetryInitializer, SecretPathTelemetryInitializer>();
        // After the SDK's own setup, which builds the chain with its metrics extractor first, so
        // the standard metrics still count every request before anything is sampled out.
        services.Configure<TelemetryConfiguration>(telemetry =>
        {
            var chain = telemetry.DefaultTelemetrySink.TelemetryProcessorChainBuilder;
            chain.Use(next => new MonitoringAwareSampling(next));
            chain.Build();
        });
        return true;
    }
}

/// <summary>Names the API "cadence-api" on every item, over the App Service site name the SDK would use.</summary>
public sealed class RoleNameInitializer : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry) => telemetry.Context.Cloud.RoleName = ApplicationInsights.RoleName;
}

/// <summary>
/// Keeps the secrets in a URL out of request telemetry, as <see cref="SecretPathRedactionMiddleware"/>
/// keeps them out of the request log.
/// </summary>
/// <remarks>
/// The SDK can take a request's URL as it arrives, before that middleware rewrites the path on
/// the way out, and its name and operation name can be "GET /path" when no route template
/// names it. So the same allowlist of secret path segments is applied here to all three, and
/// the query string is dropped, as the request log drops it (a search typed into a list is
/// someone's name).
/// </remarks>
public sealed class SecretPathTelemetryInitializer : ITelemetryInitializer
{
    public void Initialize(ITelemetry telemetry)
    {
        if (telemetry is RequestTelemetry request)
        {
            if (request.Url is { } url) request.Url = Redact(url);
            if (request.Name is { } name) request.Name = RedactName(name);
        }
        if (telemetry.Context.Operation.Name is { } operation)
            telemetry.Context.Operation.Name = RedactName(operation);
    }

    /// <summary>The URL without its query string or fragment, and with a secret path segment replaced.</summary>
    public static Uri Redact(Uri url)
    {
        if (!url.IsAbsoluteUri)
        {
            var relative = url.OriginalString.Split('?', '#')[0];
            return new Uri(SecretPathRedactionMiddleware.RedactPath(relative) ?? relative, UriKind.Relative);
        }
        var path = url.AbsolutePath;
        return new Uri($"{url.Scheme}://{url.Authority}{SecretPathRedactionMiddleware.RedactPath(path) ?? path}");
    }

    /// <summary>"GET /api/public/contracts/abc" as "GET /api/public/contracts/[redacted]"; any other name as it is.</summary>
    public static string RedactName(string name)
    {
        var space = name.IndexOf(' ');
        var path = space < 0 ? name : name[(space + 1)..];
        var withoutQuery = path.Split('?', '#')[0];
        var redacted = SecretPathRedactionMiddleware.RedactPath(withoutQuery) ?? withoutQuery;
        return redacted == path ? name : (space < 0 ? "" : name[..(space + 1)]) + redacted;
    }
}

/// <summary>
/// Adaptive sampling for everything but what must never be sampled out: exceptions, and the
/// monitoring.* log events (a browser's crash report, an alert's own trace), which are rare and
/// are what someone is looking for when they look.
/// </summary>
public sealed class MonitoringAwareSampling : ITelemetryProcessor, IDisposable
{
    /// <summary>The prefix of the log events that are always kept.</summary>
    public const string MonitoringPrefix = "monitoring.";

    /// <summary>The property the Serilog sink puts the message template under.</summary>
    private const string MessageTemplateProperty = "MessageTemplate";

    private readonly ITelemetryProcessor _next;
    private readonly ITelemetryProcessor _sampling;

    public MonitoringAwareSampling(ITelemetryProcessor next)
        : this(next, after => new AdaptiveSamplingTelemetryProcessor(after))
    {
    }

    /// <summary>With another sampler in place of adaptive sampling (tests).</summary>
    public MonitoringAwareSampling(ITelemetryProcessor next, Func<ITelemetryProcessor, ITelemetryProcessor> sampling)
    {
        _next = next;
        _sampling = sampling(next);
    }

    public void Process(ITelemetry item)
    {
        if (AlwaysKept(item)) _next.Process(item);
        else _sampling.Process(item);
    }

    /// <summary>An exception, or a monitoring.* log event.</summary>
    public static bool AlwaysKept(ITelemetry item) => item switch
    {
        ExceptionTelemetry => true,
        TraceTelemetry trace => IsMonitoring(trace.Message)
            || (trace.Properties.TryGetValue(MessageTemplateProperty, out var template) && IsMonitoring(template)),
        EventTelemetry custom => IsMonitoring(custom.Name),
        _ => false,
    };

    private static bool IsMonitoring(string? text) => text != null && text.StartsWith(MonitoringPrefix, StringComparison.Ordinal);

    public void Dispose() => (_sampling as IDisposable)?.Dispose();
}
