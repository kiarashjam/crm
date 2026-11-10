using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using ACI.Application.Services;
using ACI.Infrastructure.Services.Pos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace ACI.WebApi.Tests.Helpers;

/// <summary>
/// Lightspeed Restaurant K-Series, played by the test: its OAuth token endpoint, the Financial
/// API's businesses and its closed sales for a location, page by page, in the shapes its API
/// documentation gives. Every request is kept, and sales can be held back to make a race.
/// </summary>
/// <remarks>
/// No Lightspeed account is involved, so these tests prove what Cadence sends and how it reads
/// the documented answers; a live account is the owner's to try.
/// </remarks>
public sealed class FakeLightspeed : HttpMessageHandler
{
    public const string ClientId = "cadence-test-client";
    public const string ClientSecret = "cadence-test-secret";

    public sealed record Request(string Method, string Path, string Query, IReadOnlyDictionary<string, string> Form, string? Authorization);

    public ConcurrentQueue<Request> Requests { get; } = new();

    /// <summary>The sales the location has closed, newest last, as the Financial API answers them.</summary>
    public List<object> Sales { get; } = [];

    /// <summary>How many sales a page holds before it gives a nextPageToken.</summary>
    public int PageSize { get; set; } = 100;

    /// <summary>When set, a request for sales waits for it: a test holds an import part way.</summary>
    public TaskCompletionSource? HoldSales { get; set; }

    /// <summary>Set when a request for sales has arrived (and is held, when <see cref="HoldSales"/> is set).</summary>
    public TaskCompletionSource SalesAsked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>The refresh token Lightspeed gives with each new access token (it rotates them).</summary>
    public int TokensIssued;

    /// <summary>Answer the token endpoint with 400, as Lightspeed does for a code or refresh token it does not accept.</summary>
    public bool RefuseTokens { get; set; }

    public IReadOnlyList<Request> Sent(string method, string pathStart) =>
        Requests.Where(r => r.Method == method && r.Path.StartsWith(pathStart, StringComparison.Ordinal)).ToList();

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
        var path = message.RequestUri!.AbsolutePath;
        var request = new Request(message.Method.Method, path, message.RequestUri.Query, form, message.Headers.Authorization?.ToString());
        Requests.Enqueue(request);

        if (request.Method == "POST" && path == "/oauth/token")
        {
            var basic = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}"));
            if (RefuseTokens || request.Authorization != basic)
                return Answer(HttpStatusCode.BadRequest, new { error = "invalid_grant", error_description = "The grant is not valid." });
            var n = Interlocked.Increment(ref TokensIssued);
            return Answer(HttpStatusCode.OK, new
            {
                access_token = $"access-{n}", refresh_token = $"refresh-{n}", token_type = "bearer", expires_in = 3600, scope = "financial-api",
            });
        }

        if (request.Authorization?.StartsWith("Bearer access-", StringComparison.Ordinal) != true)
            return Answer(HttpStatusCode.Unauthorized, new { message = "Unauthorized" });

        if (request.Method == "GET" && path == "/f/data/businesses")
            return Answer(HttpStatusCode.OK, new[]
            {
                new
                {
                    businessName = "Pavillon 46", businessId = 4610, currencyCode = "CHF",
                    businessLocations = new[] { new { blName = "Pavillon 46 Restaurant", blId = 46100001L }, new { blName = "Pavillon 46 Bar", blId = 46100002L } },
                },
            });

        if (request.Method == "GET" && path.StartsWith("/f/v2/business-location/", StringComparison.Ordinal) && path.EndsWith("/sales", StringComparison.Ordinal))
        {
            SalesAsked.TrySetResult();
            if (HoldSales is { } hold) await hold.Task.WaitAsync(ct);
            var query = System.Web.HttpUtility.ParseQueryString(message.RequestUri.Query);
            var start = int.TryParse(query["nextPageToken"], out var token) ? token : 0;
            List<object> sales;
            lock (Sales) sales = Sales.Skip(start).Take(PageSize).ToList();
            var next = start + PageSize < Sales.Count ? (start + PageSize).ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
            return Answer(HttpStatusCode.OK, new { sales, nextPageToken = next });
        }

        return Answer(HttpStatusCode.NotFound, new { message = $"No such route: {request.Method} {path}" });
    }

    private static HttpResponseMessage Answer(HttpStatusCode status, object body) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
    };

    /// <summary>A closed sale as the Financial API gives it: lines with their tax, payments with their method.</summary>
    public static object Sale(string receiptId, DateTime closedAtUtc, IEnumerable<object> lines, IEnumerable<object> payments,
        object? consumer = null, string? accountName = null, string staff = "Mariah", string table = "12", bool cancelled = false) => new
    {
        receiptId,
        accountReference = $"A{receiptId}",
        timeClosed = closedAtUtc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture),
        cancelled,
        currency = "CHF",
        tableNumber = table,
        staffName = staff,
        accountName,
        consumer,
        salesLines = lines,
        payments,
    };

    /// <summary>A line, VAT included, with its tax as the POS worked it out.</summary>
    public static object Line(string name, decimal quantity, decimal totalWithTax, decimal vatRate, decimal discount = 0, string? discountName = null) => new
    {
        name,
        sku = name.ToUpperInvariant().Replace(' ', '-'),
        quantity = quantity.ToString(System.Globalization.CultureInfo.InvariantCulture),
        totalNetAmountWithTax = totalWithTax.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
        taxRatePercentage = vatRate,
        taxAmount = Math.Round(totalWithTax * vatRate / (100 + vatRate), 2).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
        discountAmount = discount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
        discountName,
    };

    public static object Payment(string code, string description, decimal amount, decimal tip = 0) => new
    {
        code,
        description,
        netAmountWithTax = amount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
        tip = tip.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
    };

    /// <summary>Puts the fake in the app's place: the Lightspeed client set up with the test's client id and secret.</summary>
    public void Install(IServiceCollection services)
    {
        services.AddHttpClient(LightspeedKSeriesProvider.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => this);
        services.RemoveAll<IPosProvider>();
        services.AddSingleton<IPosProvider>(sp => new LightspeedKSeriesProvider(
            sp.GetRequiredService<IHttpClientFactory>(),
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Pos:Lightspeed:ClientId"] = ClientId,
                ["Pos:Lightspeed:ClientSecret"] = ClientSecret,
                ["Pos:Lightspeed:ApiBase"] = "https://lightspeed.test/",
            }).Build(),
            NullLogger<LightspeedKSeriesProvider>.Instance));
    }
}

/// <summary>The app with Lightspeed played by <see cref="Lightspeed"/>.</summary>
public class PosApp : CustomWebApplicationFactory
{
    public FakeLightspeed Lightspeed { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(Lightspeed.Install);
    }
}
