using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using ACI.WebApi.Monitoring;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog.Events;
using Serilog.Parsing;

namespace ACI.WebApi.Tests;

/// <summary>
/// Application Insights: on only with a connection string; then every item is "cadence-api", the
/// contract signing token is in no request it records, and exceptions and monitoring.* events
/// are never sampled out.
/// </summary>
public class ApplicationInsightsTests
{
    private const string Token = "SUPERSECRETTOKEN123abcXYZ";

    [Fact]
    public void WithoutAConnectionStringNothingIsRegistered()
    {
        using var factory = new CustomWebApplicationFactory();

        factory.Services.GetService<TelemetryConfiguration>().Should().BeNull("tests and local runs must not need Application Insights");
        factory.Services.GetService<ITelemetryChannel>().Should().BeNull();
    }

    [Fact]
    public void TheConnectionStringComesFromEitherSetting()
    {
        static IConfiguration Config(params (string Key, string Value)[] values) =>
            new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(v => v.Key, v => (string?)v.Value)).Build();

        ApplicationInsights.ConnectionString(Config()).Should().BeNull();
        ApplicationInsights.ConnectionString(Config(("ApplicationInsights:ConnectionString", "  "))).Should().BeNull();
        ApplicationInsights.ConnectionString(Config(("APPLICATIONINSIGHTS_CONNECTION_STRING", "InstrumentationKey=a"))).Should().Be("InstrumentationKey=a");
        ApplicationInsights.ConnectionString(Config(("ApplicationInsights:ConnectionString", "InstrumentationKey=b"))).Should().Be("InstrumentationKey=b");
    }

    [Fact]
    public void ARequestsUrlNameAndOperationLoseTheTokenAndTheQueryString()
    {
        var request = new RequestTelemetry
        {
            Url = new Uri($"https://api.example.com/api/public/contracts/{Token}/sign?search=Anna%20Keller"),
            Name = $"POST /api/public/contracts/{Token}/sign",
        };
        request.Context.Operation.Name = $"POST /api/public/contracts/{Token}/sign";

        new SecretPathTelemetryInitializer().Initialize(request);

        request.Url.ToString().Should().Be("https://api.example.com/api/public/contracts/[redacted]/sign");
        request.Name.Should().Be("POST /api/public/contracts/[redacted]/sign");
        request.Context.Operation.Name.Should().Be("POST /api/public/contracts/[redacted]/sign");
    }

    [Fact]
    public void ARequestWithNothingSecretKeepsItsPathAndName()
    {
        var request = new RequestTelemetry { Url = new Uri("https://api.example.com/api/leads?page=2"), Name = "GET Leads/GetLeads" };
        var trace = new TraceTelemetry("hello");
        trace.Context.Operation.Name = "GET Leads/GetLeads";

        new SecretPathTelemetryInitializer().Initialize(request);
        new SecretPathTelemetryInitializer().Initialize(trace);

        request.Url.ToString().Should().Be("https://api.example.com/api/leads");
        request.Name.Should().Be("GET Leads/GetLeads");
        trace.Context.Operation.Name.Should().Be("GET Leads/GetLeads");
    }

    [Fact]
    public void EveryItemIsCadenceApi()
    {
        var trace = new TraceTelemetry("hello");
        trace.Context.Cloud.RoleName = "aci-crm-prod";

        new RoleNameInitializer().Initialize(trace);

        trace.Context.Cloud.RoleName.Should().Be("cadence-api");
    }

    [Fact]
    public void ALogEventsPathsLoseTheTokenAndSoDoesItsMessage()
    {
        // ASP.NET Core's request scope gives every event in a request its RequestPath, as it
        // arrived; GlobalExceptionHandler logs the Path an exception was thrown on.
        var logEvent = new LogEvent(DateTimeOffset.UtcNow, LogEventLevel.Error, null,
            new MessageTemplateParser().Parse("Unhandled exception occurred. Path: {Path}"),
            new[]
            {
                new LogEventProperty("Path", new ScalarValue($"/api/public/contracts/{Token}/sign")),
                new LogEventProperty("RequestPath", new ScalarValue($"/api/public/unsubscribe/{Token}")),
                new LogEventProperty("Other", new ScalarValue("/api/leads/3f9a21c0")),
            });

        new SecretPathLogEnricher().Enrich(logEvent, null!);

        logEvent.RenderMessage().Should().Be("Unhandled exception occurred. Path: \"/api/public/contracts/[redacted]/sign\"");
        logEvent.Properties["RequestPath"].ToString().Should().Be("\"/api/public/unsubscribe/[redacted]\"");
        logEvent.Properties["Other"].ToString().Should().Be("\"/api/leads/3f9a21c0\"");
    }

    private sealed class Kept : ITelemetryProcessor
    {
        public readonly List<ITelemetry> Items = new();
        public void Process(ITelemetry item) => Items.Add(item);
    }

    /// <summary>A sampler that keeps nothing: whatever still arrives was never sampled.</summary>
    private sealed class DropsEverything : ITelemetryProcessor
    {
        public void Process(ITelemetry item) { }
    }

    [Fact]
    public void ExceptionsAndMonitoringEventsAreNeverSampledOut()
    {
        var kept = new Kept();
        var sampling = new MonitoringAwareSampling(kept, _ => new DropsEverything());
        var exception = new ExceptionTelemetry(new InvalidOperationException("boom"));
        var browserError = new TraceTelemetry("monitoring.browser.error \"TypeError\" at \"https://app.example.com/leads\"", SeverityLevel.Warning);
        var byTemplate = new TraceTelemetry("Something rendered differently");
        byTemplate.Properties["MessageTemplate"] = "monitoring.alert.sent {Rule}";
        var monitoringEvent = new EventTelemetry("monitoring.job.silent");
        var ordinary = new TraceTelemetry("HTTP GET /api/leads responded 200");
        var request = new RequestTelemetry { Name = "GET /api/leads" };

        foreach (var item in new ITelemetry[] { exception, browserError, byTemplate, monitoringEvent, ordinary, request })
            sampling.Process(item);

        kept.Items.Should().Equal(exception, browserError, byTemplate, monitoringEvent);
    }

    [Fact]
    public void WithTheSdksOwnSamplerAtAlmostNothingTheyStillAllArrive()
    {
        var kept = new Kept();
        var sampling = new MonitoringAwareSampling(kept,
            after => new Microsoft.ApplicationInsights.WindowsServer.TelemetryChannel.SamplingTelemetryProcessor(after) { SamplingPercentage = 0.001 });

        for (var i = 0; i < 200; i++)
        {
            foreach (var item in new ITelemetry[]
            {
                new ExceptionTelemetry(new InvalidOperationException($"boom {i}")),
                new TraceTelemetry($"monitoring.browser.error \"Error {i}\" at \"https://app.example.com/\"", SeverityLevel.Warning),
                new TraceTelemetry($"HTTP GET /api/leads/{i} responded 200"),
            })
            {
                item.Context.Operation.Id = ActivityTraceId.CreateRandom().ToHexString();
                sampling.Process(item);
            }
        }

        kept.Items.OfType<ExceptionTelemetry>().Should().HaveCount(200);
        kept.Items.OfType<TraceTelemetry>().Count(t => t.Message.StartsWith("monitoring.")).Should().Be(200);
        kept.Items.OfType<TraceTelemetry>().Count(t => !t.Message.StartsWith("monitoring.")).Should().BeLessThan(10, "the sampler does sample");
    }
}

/// <summary>
/// A host with Application Insights on, end to end. With the process-wide tests: request
/// tracking listens to every host's diagnostics in the process, and Serilog's static logger is
/// replaced by every test host that starts, so hosts starting beside it took its log events and
/// its requests went untracked.
/// </summary>
[Collection(ProcessWideLogCollection.Name)]
public class ApplicationInsightsHostTests
{
    private const string Token = "SUPERSECRETTOKEN123abcXYZ";

    /// <summary>What the SDK would have sent, kept instead.</summary>
    private sealed class CapturingChannel : ITelemetryChannel
    {
        public readonly ConcurrentQueue<ITelemetry> Sent = new();
        public bool? DeveloperMode { get; set; }
        public string EndpointAddress { get; set; } = "";
        public void Send(ITelemetry item) => Sent.Enqueue(item);
        public void Flush() { }
        public void Dispose() { }
    }

    /// <summary>A host with Application Insights configured, whose telemetry goes to a list.</summary>
    private sealed class App : CustomWebApplicationFactory
    {
        public readonly CapturingChannel Channel = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            // Read by Program.cs before the host is built, as App Service's setting would be;
            // endpoints that go nowhere, in case anything tried to send.
            builder.UseSetting("ApplicationInsights:ConnectionString",
                "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=http://127.0.0.1:9/;LiveEndpoint=http://127.0.0.1:9/");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITelemetryChannel>();
                services.AddSingleton<ITelemetryChannel>(Channel);
                // Nothing in a test host reaches out on its own.
                services.Configure<ApplicationInsightsServiceOptions>(options =>
                {
                    options.EnableQuickPulseMetricStream = false;
                    options.EnablePerformanceCounterCollectionModule = false;
                    options.EnableAzureInstanceMetadataTelemetryModule = false;
                    options.EnableAppServicesHeartbeatTelemetryModule = false;
                    options.EnableHeartbeat = false;
                });
            });
        }

        public async Task<List<T>> WaitForAsync<T>(Func<T, bool> wanted) where T : ITelemetry
        {
            // A request is tracked once the server has finished with it, which can be a moment
            // after the client has its answer.
            for (var i = 0; i < 200; i++)
            {
                var found = Channel.Sent.OfType<T>().Where(wanted).ToList();
                if (found.Count > 0) return found;
                await Task.Delay(50);
            }
            return new List<T>();
        }
    }

    [Fact]
    public async Task WithAConnectionStringRequestsAndLogsGoThereWithoutTheToken()
    {
        using var app = new App();
        var telemetry = app.Services.GetRequiredService<TelemetryConfiguration>();
        // The only sampler is the adaptive one inside MonitoringAwareSampling, which exceptions and
        // monitoring.* events go around; the standard metrics are counted before anything is sampled.
        telemetry.DefaultTelemetrySink.TelemetryProcessors.Select(p => p.GetType().Name).Should().Equal(
            "AutocollectedMetricsExtractor", nameof(MonitoringAwareSampling), "TransmissionProcessor");
        var client = app.CreateClient();

        await client.GetAsync($"/api/public/contracts/{Token}?ref=mail");
        var report = await client.PostAsync("/api/client-errors",
            new StringContent("{\"message\":\"TypeError: boom\",\"url\":\"https://app.example.com/sign/" + Token + "\"}", Encoding.UTF8, "text/plain"));
        report.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var requests = await app.WaitForAsync<RequestTelemetry>(r => r.Url?.AbsolutePath.StartsWith("/api/public/contracts/") == true);
        requests.Should().NotBeEmpty("the request should have been tracked (sent: {0})",
            string.Join(" | ", app.Channel.Sent.Select(i => i.GetType().Name)));
        var request = requests[0];
        request.Url.ToString().Should().EndWith("/api/public/contracts/[redacted]");
        request.Context.Cloud.RoleName.Should().Be("cadence-api");

        var browserErrors = await app.WaitForAsync<TraceTelemetry>(t => t.Message.StartsWith("monitoring.browser.error"));
        browserErrors.Should().NotBeEmpty("Serilog sends its log events through the SDK's TelemetryConfiguration");
        browserErrors[0].SeverityLevel.Should().Be(SeverityLevel.Warning);
        browserErrors[0].Context.Cloud.RoleName.Should().Be("cadence-api");

        foreach (var item in app.Channel.Sent.ToList())
        {
            var said = item switch
            {
                RequestTelemetry r => $"{r.Url} {r.Name} {r.Context.Operation.Name}",
                TraceTelemetry t => $"{t.Message} {string.Join(' ', t.Properties.Values)} {t.Context.Operation.Name}",
                ExceptionTelemetry e => $"{e.Message} {string.Join(' ', e.Properties.Values)} {e.Context.Operation.Name}",
                _ => item.Context.Operation.Name ?? "",
            };
            said.Should().NotContain(Token).And.NotContain("ref=mail");
        }
    }
}
