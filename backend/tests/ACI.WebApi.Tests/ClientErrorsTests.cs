using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using ACI.WebApi.Controllers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ACI.WebApi.Tests;

/// <summary>
/// Crashes in the browser reach the server's log (POST /api/client-errors): one Warning each,
/// anonymous, rate limited, size limited, and with the tokens a page's address can carry taken out.
/// </summary>
public class ClientErrorsTests
{
    private const string Token = "SUPERSECRETTOKEN123abcXYZ";

    public sealed record Logged(LogLevel Level, string? Template, IReadOnlyDictionary<string, object?> Values, IReadOnlyDictionary<string, object?> Scope);

    /// <summary>The controller's logger, kept: what the log would have been given.</summary>
    public sealed class CapturingLogger : ILogger<ClientErrorsController>
    {
        private readonly AsyncLocal<IReadOnlyDictionary<string, object?>?> _scope = new();
        public ConcurrentQueue<Logged> Entries { get; } = new();

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            var previous = _scope.Value;
            _scope.Value = state is IEnumerable<KeyValuePair<string, object>> pairs
                ? pairs.ToDictionary(p => p.Key, p => (object?)p.Value)
                : null;
            return new Restore(() => _scope.Value = previous);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.ToDictionary(p => p.Key, p => p.Value)
                : new Dictionary<string, object?>();
            Entries.Enqueue(new Logged(logLevel, values.GetValueOrDefault("{OriginalFormat}") as string, values,
                _scope.Value ?? new Dictionary<string, object?>()));
        }

        private sealed class Restore(Action restore) : IDisposable
        {
            public void Dispose() => restore();
        }
    }

    private sealed class App : CustomWebApplicationFactory
    {
        public readonly CapturingLogger Log = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services => services.AddSingleton<ILogger<ClientErrorsController>>(Log));
        }
    }

    private static StringContent Report(object body, string contentType = "text/plain") =>
        new(JsonSerializer.Serialize(body), Encoding.UTF8, contentType);

    [Fact]
    public async Task AReportIsLoggedOnceAsAWarningAndAnswered204()
    {
        using var app = new App();
        // No session, no workspace: a crash on a public page.
        var client = app.CreateClient();

        var res = await client.PostAsync("/api/client-errors", Report(new
        {
            message = "TypeError: Cannot read properties of undefined (reading 'name')",
            stack = "TypeError: Cannot read properties of undefined\n    at Sign (https://app.example.com/assets/index-abc123.js:12:345)",
            url = $"https://app.example.com/sign/{Token}?utm_source=mail#top",
            componentStack = "\n    at Sign\n    at App",
            userAgent = "Mozilla/5.0 (Test)",
            release = "2026.11.18-abc123",
        }));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var logged = app.Log.Entries.Should().ContainSingle().Subject;
        logged.Level.Should().Be(LogLevel.Warning);
        logged.Template.Should().Be("monitoring.browser.error {Message} at {Url}");
        logged.Values["Message"].Should().Be("TypeError: Cannot read properties of undefined (reading 'name')");
        logged.Values["Url"].Should().Be("https://app.example.com/sign/[redacted]");
        logged.Scope["Stack"].Should().Be("TypeError: Cannot read properties of undefined\n    at Sign (https://app.example.com/assets/index-abc123.js:12:345)");
        logged.Scope["ComponentStack"].Should().Be("\n    at Sign\n    at App");
        logged.Scope["UserAgent"].Should().Be("Mozilla/5.0 (Test)");
        logged.Scope["Release"].Should().Be("2026.11.18-abc123");
        JsonSerializer.Serialize(app.Log.Entries).Should().NotContain(Token).And.NotContain("utm_source");
    }

    [Fact]
    public async Task ItTakesTheReportAsJsonTooAndNeedsOnlyAMessage()
    {
        using var app = new App();

        var res = await app.CreateClient().PostAsync("/api/client-errors", Report(new { message = "Boom" }, "application/json"));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var logged = app.Log.Entries.Should().ContainSingle().Subject;
        logged.Values["Message"].Should().Be("Boom");
        logged.Values["Url"].Should().BeNull();
        logged.Scope.Keys.Should().NotContain(new[] { "Stack", "ComponentStack", "Release" }, "fields not sent are not logged as empty");
    }

    [Theory]
    // The app's pages whose segment is the whole permission: signing a contract, unsubscribing, paying.
    [InlineData("https://app.example.com/sign/" + Token, "https://app.example.com/sign/[redacted]")]
    [InlineData("https://app.example.com/unsubscribe/" + Token, "https://app.example.com/unsubscribe/[redacted]")]
    [InlineData("https://app.example.com/pay/" + Token + "/done", "https://app.example.com/pay/[redacted]/done")]
    // The API's, by the request log's own list.
    [InlineData("https://api.example.com/api/public/contracts/" + Token + "/pdf", "https://api.example.com/api/public/contracts/[redacted]/pdf")]
    // A query string or fragment holds reset and verification tokens; a user name and password are gone too.
    [InlineData("https://app.example.com/reset-password?token=" + Token, "https://app.example.com/reset-password")]
    [InlineData("https://user:" + Token + "@app.example.com/leads#" + Token, "https://app.example.com/leads")]
    [InlineData("/sign/" + Token + "?x=1", "/sign/[redacted]")]
    // Everything else as it is.
    [InlineData("https://app.example.com/leads/3f9a21c0-0000-0000-0000-000000000000", "https://app.example.com/leads/3f9a21c0-0000-0000-0000-000000000000")]
    [InlineData("https://app.example.com:8443/capture/form-key", "https://app.example.com:8443/capture/form-key")]
    public void APagesAddressLosesWhatCouldBeASecret(string url, string logged)
    {
        ClientErrorsController.RedactUrl(url).Should().Be(logged);
    }

    [Fact]
    public void AnAddressInTheMessageOrStackIsRedactedTooAsIsASessionToken()
    {
        var jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.dozjgNryP4J3jVmNHl0w5N_XgL0n3I9PlFUP0THsR8U";
        var cleaned = ClientErrorsController.Clean(
            $"Request to https://api.example.com/api/public/contracts/{Token}/sign?a=b failed with Bearer {jwt}", 1000);

        cleaned.Should().Be("Request to https://api.example.com/api/public/contracts/[redacted]/sign failed with Bearer [redacted]");
    }

    [Fact]
    public async Task EveryFieldIsClipped()
    {
        using var app = new App();

        var res = await app.CreateClient().PostAsync("/api/client-errors", Report(new
        {
            message = new string('m', 5000),
            url = "https://app.example.com/" + new string('u', 3000),
            userAgent = new string('a', 2000),
        }));

        res.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var logged = app.Log.Entries.Should().ContainSingle().Subject;
        ((string)logged.Values["Message"]!).Length.Should().BeLessThanOrEqualTo(ClientErrorsController.MaxMessage + 1);
        ((string)logged.Values["Url"]!).Length.Should().BeLessThanOrEqualTo(ClientErrorsController.MaxUrl + 1);
        ((string)logged.Scope["UserAgent"]!).Length.Should().BeLessThanOrEqualTo(ClientErrorsController.MaxUserAgent + 1);
    }

    [Fact]
    public async Task ABodyOver16KbIsRefusedAndNothingIsLogged()
    {
        using var app = new App();
        var client = app.CreateClient();

        var declared = await client.PostAsync("/api/client-errors", Report(new { message = new string('x', ClientErrorsController.MaxBodyBytes) }));
        declared.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);

        // Sent without a length, so only reading it finds it too long.
        var streamed = new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { message = new string('x', 20_000) }))));
        streamed.Headers.ContentLength = null;
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/client-errors") { Content = streamed };
        request.Headers.TransferEncodingChunked = true;
        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);

        app.Log.Entries.Should().BeEmpty();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"message\":\"   \"}")]
    [InlineData("not json")]
    [InlineData("{\"message\":42}")]
    public async Task AReportWithoutAMessageIsRefused(string body)
    {
        using var app = new App();

        var res = await app.CreateClient().PostAsync("/api/client-errors", new StringContent(body, Encoding.UTF8, "text/plain"));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        app.Log.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task OneAddressMaySendThirtyAMinute()
    {
        using var app = new App();
        var client = app.CreateClient();

        for (var i = 0; i < ClientErrorsController.PerMinute; i++)
            (await client.PostAsync("/api/client-errors", Report(new { message = $"Error {i}" }))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var refused = await client.PostAsync("/api/client-errors", Report(new { message = "One too many" }));

        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        app.Log.Entries.Should().HaveCount(ClientErrorsController.PerMinute);
    }
}
