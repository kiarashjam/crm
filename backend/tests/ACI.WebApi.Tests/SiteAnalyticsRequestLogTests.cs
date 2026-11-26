using System.Collections.Concurrent;
using System.Net;
using System.Text;
using ACI.WebApi.Monitoring;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace ACI.WebApi.Tests;

/// <summary>
/// The request log keeps no trace of the beacons every visitor's page sends: the site analytics'
/// posts and the crash reports are not logged (Verbose, below what the log writes), and never
/// with the caller's address. Every other request still is, with its address.
/// </summary>
/// <remarks>
/// In the process-wide log collection: the end-to-end test swaps Serilog's static logger, which
/// the request log writes to, and a host starting beside it would swap it back.
/// </remarks>
[Collection(ProcessWideLogCollection.Name)]
public class SiteAnalyticsRequestLogTests
{
    /// <summary>What the request log was handed: a diagnostic context that keeps what is set on it.</summary>
    private sealed class Diagnostics : IDiagnosticContext
    {
        public Dictionary<string, object?> Values { get; } = new();
        public void Set(string propertyName, object? value, bool destructureObjects = false) => Values[propertyName] = value;
        public void SetException(Exception exception) { }
    }

    /// <summary>A Serilog sink that keeps the events.</summary>
    private sealed class Kept : ILogEventSink
    {
        public ConcurrentQueue<LogEvent> Events { get; } = new();
        public void Emit(LogEvent logEvent) => Events.Enqueue(logEvent);
    }

    private static HttpContext Request(string path, int status = 204)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.StatusCode = status;
        context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.23");
        return context;
    }

    [Theory]
    [InlineData("/api/site-analytics/events")]
    [InlineData("/api/site-analytics/events/")]
    [InlineData("/API/Site-Analytics/Events")]
    [InlineData("/api/site-analytics/summary")]
    [InlineData("/api/client-errors")]
    public void A_beacon_is_logged_at_Verbose_and_without_the_callers_address(string path)
    {
        RequestLog.Level(Request(path), 1.0, null).Should().Be(LogEventLevel.Verbose);
        ((int)LogEventLevel.Verbose).Should().BeLessThan((int)LogEventLevel.Information, "the log writes Information and up");

        var diagnostics = new Diagnostics();
        RequestLog.Enrich(diagnostics, Request(path));
        diagnostics.Values.Keys.Should().BeEquivalentTo("UserId", "OrganizationId");
        diagnostics.Values.Values.Should().NotContain("198.51.100.23");
    }

    [Theory]
    [InlineData("/api/leads", 200, LogEventLevel.Information)]
    [InlineData("/api/site-analytics-other", 200, LogEventLevel.Information)]
    [InlineData("/api/leads", 500, LogEventLevel.Error)]
    [InlineData("/api/site-analytics/events", 500, LogEventLevel.Error)]
    public void Every_other_request_is_logged_as_before(string path, int status, LogEventLevel level)
    {
        RequestLog.Level(Request(path, status), 1.0, null).Should().Be(level);
        RequestLog.Level(Request(path, 200), 1.0, new InvalidOperationException()).Should().Be(LogEventLevel.Error);

        var diagnostics = new Diagnostics();
        RequestLog.Enrich(diagnostics, Request(path));
        diagnostics.Values.ContainsKey("ClientIP").Should().Be(!RequestLog.IsQuiet(path));
    }

    [Fact]
    public async Task The_request_log_written_for_real_requests_has_no_beacon_and_no_beacons_address()
    {
        await using var app = new AnalyticsApp();
        var browser = app.Browser(address: "198.51.100.23");
        var kept = new Kept();
        var previous = Log.Logger;
        // The levels Program.cs writes at: Information and up, the framework's Warning and up.
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Sink(kept)
            .CreateLogger();
        try
        {
            (await browser.PostAsync(SiteAnalyticsWorld.Endpoint, SiteAnalyticsWorld.Events(new { type = "page_view", path = "/pricing", sessionId = "a1b2c3d4e5f6" })))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await browser.PostAsync("/api/client-errors", new StringContent("{\"message\":\"TypeError: boom\"}", Encoding.UTF8, "text/plain")))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await browser.GetAsync("/ping")).StatusCode.Should().Be(HttpStatusCode.OK);

            // The request log writes a request's line once it has answered: wait for the ping's.
            List<LogEvent> requests = [];
            for (var i = 0; i < 100 && !requests.Any(e => Path(e) == "/ping"); i++)
            {
                await Task.Delay(20);
                requests = kept.Events.Where(e => e.MessageTemplate.Text == RequestLog.MessageTemplate).ToList();
            }

            requests.Should().Contain(e => Path(e) == "/ping", "every other request is still logged")
                .Which.Properties.Should().ContainKey("ClientIP");
            requests.Select(Path).Should().NotContain(p => p!.StartsWith("/api/site-analytics") || p.StartsWith("/api/client-errors"));
            // Whatever else is written during a beacon (here only the test server's own warning
            // that it cannot limit a body's size) carries no address.
            kept.Events.Where(e => Path(e)?.StartsWith("/api/site-analytics") == true)
                .Should().OnlyContain(e => !e.Properties.ContainsKey("ClientIP") && e.Level >= LogEventLevel.Warning);
        }
        finally
        {
            Log.Logger = previous;
        }
    }

    private static string? Path(LogEvent e) =>
        e.Properties.TryGetValue("RequestPath", out var value) && value is ScalarValue { Value: string path } ? path : null;
}
