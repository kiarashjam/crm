using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using ACI.Application.Common.Payments;
using ACI.Infrastructure.Services.Payments;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ACI.WebApi.Tests.Helpers;

/// <summary>
/// Stripe's API, played by the test: every request the app sends is kept (method, path,
/// form fields, headers), and answered by the handler registered for it, or 404.
/// </summary>
/// <remarks>
/// No Stripe account is involved, so these tests prove what Cadence sends and how it reads
/// the answers, as Stripe documents them; the requests' shape is what a live account would get.
/// </remarks>
public sealed class FakeStripe : HttpMessageHandler
{
    public sealed record Request(string Method, string Path, string Query, IReadOnlyDictionary<string, string> Form, string? IdempotencyKey, string? Authorization, string? Version);

    private readonly ConcurrentDictionary<string, Func<Request, (HttpStatusCode Status, object Body)>> _routes = new();

    public ConcurrentQueue<Request> Requests { get; } = new();

    /// <summary>Answer <paramref name="method"/> <paramref name="path"/> (no query) with what <paramref name="answer"/> returns.</summary>
    public FakeStripe On(string method, string path, Func<Request, (HttpStatusCode, object)> answer)
    {
        _routes[$"{method} {path}"] = r => answer(r);
        return this;
    }

    public FakeStripe On(string method, string path, object body) => On(method, path, _ => (HttpStatusCode.OK, body));

    /// <summary>Stripe accepts any key that starts sk_test_good; the key check lists customers.</summary>
    public FakeStripe AcceptingKeys()
    {
        On("GET", "/v1/customers", r => r.Authorization?.StartsWith("Bearer sk_test_good", StringComparison.Ordinal) == true
            ? (HttpStatusCode.OK, new { @object = "list", data = Array.Empty<object>() })
            : (HttpStatusCode.Unauthorized, new { error = new { type = "invalid_request_error", message = "Invalid API Key provided" } }));
        return this;
    }

    public IReadOnlyList<Request> Sent(string method, string path) =>
        Requests.Where(r => r.Method == method && r.Path == path).ToList();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken ct)
    {
        var form = new Dictionary<string, string>(StringComparer.Ordinal);
        if (message.Content != null)
        {
            var text = await message.Content.ReadAsStringAsync(ct);
            foreach (var pair in text.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = pair.Split('=', 2);
                form[WebUtility.UrlDecode(kv[0])] = kv.Length > 1 ? WebUtility.UrlDecode(kv[1]) : "";
            }
        }
        var request = new Request(
            message.Method.Method,
            message.RequestUri!.AbsolutePath,
            message.RequestUri.Query,
            form,
            message.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.First() : null,
            message.Headers.Authorization?.ToString(),
            message.Headers.TryGetValues("Stripe-Version", out var versions) ? versions.First() : null);
        Requests.Enqueue(request);

        var (status, body) = _routes.TryGetValue($"{request.Method} {request.Path}", out var answer)
            ? answer(request)
            : (HttpStatusCode.NotFound, new { error = new { type = "invalid_request_error", message = $"No such route: {request.Method} {request.Path}" } });
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };
    }

    /// <summary>A webhook post as Stripe signs it, for a test to send to the app.</summary>
    public static HttpRequestMessage Webhook(string url, string secret, object stripeEvent, DateTime? signedAtUtc = null)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(stripeEvent));
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        request.Headers.Add("Stripe-Signature", StripeSignature.Header(secret, body, signedAtUtc ?? DateTime.UtcNow));
        return request;
    }
}

/// <summary>The app with Stripe played by <see cref="Stripe"/>.</summary>
public class PaymentsApp : CustomWebApplicationFactory
{
    public FakeStripe Stripe { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
            services.AddHttpClient(StripePaymentProvider.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Stripe));
    }
}
