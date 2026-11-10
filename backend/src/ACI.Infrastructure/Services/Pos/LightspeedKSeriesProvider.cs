using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services.Pos;

/// <summary>
/// Lightspeed Restaurant K-Series, through its public API: OAuth 2 (authorization code, then
/// refresh tokens) to read the club's account, the Financial API's business locations, and its
/// sales (closed tickets) for a location and a period, page by page.
/// </summary>
/// <remarks>
/// <para>
/// Configuration (the owner's, once per server): Pos:Lightspeed:ClientId and ClientSecret, the
/// API client Lightspeed gives a partner; Pos:Lightspeed:ApiBase, https://api.lsk.lightspeed.app/
/// unless set (the trial environment is https://api.trial.lsk.lightspeed.app/); and
/// Pos:Lightspeed:Scope, financial-api unless set. Without the client id and secret nothing here
/// is called and the POS page says Lightspeed is not set up.
/// </para>
/// <para>
/// The answers are read by the names the Financial API documents (a sale's receiptId, timeClosed,
/// salesLines with totalNetAmountWithTax and taxRatePercentage, payments with their code and
/// netAmountWithTax, staff, table and consumer), and by the names its older version used where
/// they differ, so a field renamed between versions does not lose a ticket. Amounts may come as
/// numbers or as text; both are read, to the cent. Nothing here was tried against a live
/// account: the HTTP tests answer for Lightspeed with the documented shapes.
/// </para>
/// </remarks>
public sealed class LightspeedKSeriesProvider : IPosProvider
{
    public const string HttpClientName = "lightspeed-k";

    private readonly IHttpClientFactory _http;
    private readonly ILogger<LightspeedKSeriesProvider> _logger;
    private readonly Uri _apiBase;
    private readonly string? _clientId;
    private readonly string? _clientSecret;
    private readonly string _scope;

    public LightspeedKSeriesProvider(IHttpClientFactory http, IConfiguration configuration, ILogger<LightspeedKSeriesProvider> logger)
    {
        _http = http;
        _logger = logger;
        var configured = configuration["Pos:Lightspeed:ApiBase"];
        _apiBase = new Uri(string.IsNullOrWhiteSpace(configured) ? "https://api.lsk.lightspeed.app/" : configured.TrimEnd('/') + "/");
        _clientId = Empty(configuration["Pos:Lightspeed:ClientId"]);
        _clientSecret = Empty(configuration["Pos:Lightspeed:ClientSecret"]);
        _scope = Empty(configuration["Pos:Lightspeed:Scope"]) ?? "financial-api";
    }

    public string Name => PosProviders.LightspeedK;

    public bool IsConfigured => _clientId != null && _clientSecret != null;

    public string AuthorizeUrl(string state, string redirectUri) =>
        new Uri(_apiBase, "oauth/authorize").ToString()
        + $"?response_type=code&client_id={Uri.EscapeDataString(_clientId ?? "")}&scope={Uri.EscapeDataString(_scope)}"
        + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}&state={Uri.EscapeDataString(state)}";

    public Task<ProviderResult<PosTokens>> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct = default) =>
        TokenAsync([new("grant_type", "authorization_code"), new("code", code), new("redirect_uri", redirectUri)], ct);

    public Task<ProviderResult<PosTokens>> RefreshAsync(string refreshToken, CancellationToken ct = default) =>
        TokenAsync([new("grant_type", "refresh_token"), new("refresh_token", refreshToken)], ct);

    public async Task<ProviderResult<IReadOnlyList<PosLocationDto>>> LocationsAsync(string accessToken, CancellationToken ct = default)
    {
        var answer = await GetAsync(accessToken, "f/data/businesses", ct);
        if (answer.Failure is { } failure) return ProviderResult<IReadOnlyList<PosLocationDto>>.Fail(failure);
        var locations = new List<PosLocationDto>();
        var root = answer.Json!.Value;
        var businesses = root.ValueKind == JsonValueKind.Array ? root
            : Property(root, "businesses", "_embedded") is { ValueKind: JsonValueKind.Array } list ? list : root;
        foreach (var business in Items(businesses))
        {
            var currency = Text(business, "currencyCode", "currency");
            foreach (var location in Items(Property(business, "businessLocations", "locations") ?? default))
            {
                var id = Text(location, "blId", "blID", "businessLocationId", "id");
                if (id == null) continue;
                locations.Add(new PosLocationDto(id, Text(location, "blName", "name", "businessLocationName") ?? id, Text(location, "currencyCode", "currency") ?? currency));
            }
        }
        return ProviderResult<IReadOnlyList<PosLocationDto>>.Ok(locations);
    }

    public async Task<ProviderResult<PosTicketPage>> ClosedTicketsAsync(string accessToken, string locationId, DateTime fromUtc, DateTime toUtc, string? pageToken, CancellationToken ct = default)
    {
        var path = $"f/v2/business-location/{Uri.EscapeDataString(locationId)}/sales"
            + $"?from={Uri.EscapeDataString(Iso(fromUtc))}&to={Uri.EscapeDataString(Iso(toUtc))}&pageSize=100"
            + "&include=staff,table,consumer,payments"
            + (pageToken != null ? $"&nextPageToken={Uri.EscapeDataString(pageToken)}" : "");
        var answer = await GetAsync(accessToken, path, ct);
        if (answer.Failure is { } failure) return ProviderResult<PosTicketPage>.Fail(failure);
        var root = answer.Json!.Value;
        var tickets = new List<PosTicketData>();
        foreach (var sale in Items(Property(root, "sales", "data") ?? default))
            if (ReadSale(sale, locationId) is { } ticket) tickets.Add(ticket);
        var next = Text(root, "nextPageToken");
        return ProviderResult<PosTicketPage>.Ok(new PosTicketPage(tickets, string.IsNullOrWhiteSpace(next) ? null : next));
    }

    // ── reading a sale ───────────────────────────────────────────────────────

    /// <summary>A closed sale as a ticket; null when it carries no id or no close time, which nothing can be keyed or dated by.</summary>
    internal static PosTicketData? ReadSale(JsonElement sale, string locationId)
    {
        var receipt = Text(sale, "receiptId", "receiptNumber", "accountReference", "id");
        var closed = Date(sale, "timeClosed", "timeOfCloseAndPaid", "closeDate", "closedAt");
        if (receipt == null || closed == null) return null;
        var status = Text(sale, "status")?.ToUpperInvariant();
        var cancelled = Flag(sale, "cancelled", "canceled") || status is "CANCELLED" or "CANCELED" or "VOID" or "VOIDED";

        var lines = new List<PosTicketLine>();
        foreach (var line in Items(Property(sale, "salesLines", "lines") ?? default))
        {
            var total = Money(line, "totalNetAmountWithTax", "totalAmountWithTax", "amountWithTax", "total");
            if (total == null) continue;
            lines.Add(new PosTicketLine(
                Text(line, "nameOverride", "name", "itemName") ?? "Item",
                Text(line, "sku", "itemSku"),
                Number(line, "quantity", "qty") ?? 1m,
                total.Value,
                VatRate(line),
                Money(line, "discountAmount", "discount") ?? 0m,
                Text(line, "discountName", "discountCode", "discountReason")));
        }

        var payments = new List<PosTicketPayment>();
        foreach (var payment in Items(Property(sale, "payments") ?? default))
        {
            var method = Property(payment, "paymentMethod");
            payments.Add(new PosTicketPayment(
                Text(payment, "code", "paymentMethodCode") ?? (method is { } m ? Text(m, "code") : null),
                Text(payment, "description", "name", "paymentMethodName") ?? (method is { } n ? Text(n, "name", "description") : null),
                Money(payment, "netAmountWithTax", "amountWithTax", "amount", "totalAmount") ?? 0m,
                Money(payment, "tip", "tipAmount", "gratuity") ?? 0m));
        }

        var consumer = Property(sale, "consumer", "customer");
        var codes = new List<string>();
        void Code(string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !codes.Contains(value.Trim())) codes.Add(value.Trim());
        }
        if (consumer is { } c)
            foreach (var field in new[] { "customerNumber", "cardNumber", "loyaltyCardNumber", "membershipNumber", "memberNumber", "externalReference", "externalId", "reference", "code" })
                Code(Text(c, field));
        // The name staff gave the bill, which is where a member's number is typed when the POS has no customer for them.
        Code(Text(sale, "accountName", "accountProfileName", "name", "customerReference"));

        var staff = Property(sale, "staff");
        var table = Property(sale, "table");
        var firstName = consumer is { } fc ? Text(fc, "firstName") : null;
        var lastName = consumer is { } lc ? Text(lc, "lastName") : null;
        var customerName = string.Join(' ', new[] { firstName, lastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return new PosTicketData(
            $"{locationId}:{receipt}",
            receipt,
            locationId,
            closed.Value,
            Text(sale, "currency", "currencyCode"),
            cancelled,
            lines,
            payments,
            Text(sale, "staffName", "ownerName") ?? (staff is { } s ? Text(s, "name", "fullName") ?? Join(Text(s, "firstName"), Text(s, "lastName")) : null),
            Text(sale, "tableName", "tableNumber") ?? (table is { } t ? Text(t, "name", "number") : null),
            consumer is { } ci ? Text(ci, "id", "consumerId", "customerId") : null,
            customerName.Length > 0 ? customerName : consumer is { } cn ? Text(cn, "name", "companyName") : null,
            consumer is { } ce ? Text(ce, "email", "emailAddress") : null,
            codes);
    }

    /// <summary>
    /// The line's VAT rate in percent: the one it states, or worked out from its tax and net
    /// amounts and set to the Swiss rate it is within a twentieth of a percent of.
    /// </summary>
    private static decimal? VatRate(JsonElement line)
    {
        if (Number(line, "taxRatePercentage", "taxRate", "vatRate") is { } stated)
            return Math.Round(stated is > 0 and < 1 ? stated * 100 : stated, 2);
        var tax = Money(line, "taxAmount", "vatAmount");
        var net = Money(line, "totalNetAmountWithoutTax", "amountWithoutTax");
        if (tax is not { } t || net is not { } n || n <= 0) return tax == 0 ? 0 : null;
        var rate = t / n * 100;
        foreach (var known in new[] { 8.1m, 2.6m, 3.8m, 7.7m, 2.5m, 3.7m, 0m })
            if (Math.Abs(rate - known) <= 0.05m) return known;
        return Math.Round(rate, 2);
    }

    // ── HTTP ─────────────────────────────────────────────────────────────────

    private async Task<ProviderResult<PosTokens>> TokenAsync(List<KeyValuePair<string, string>> form, CancellationToken ct)
    {
        if (!IsConfigured) return ProviderResult<PosTokens>.Fail(new ProviderFailure("Lightspeed is not set up on this server."));
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_apiBase, "oauth/token")) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}")));
        var answer = await SendAsync(request, ct);
        if (answer.Failure is { } failure) return ProviderResult<PosTokens>.Fail(failure);
        var json = answer.Json!.Value;
        var access = Text(json, "access_token");
        if (access == null) return ProviderResult<PosTokens>.Fail(new ProviderFailure("Lightspeed answered without an access token."));
        var seconds = Number(json, "expires_in") is { } s and > 0 ? (double)s : 3600;
        return ProviderResult<PosTokens>.Ok(new PosTokens(access, Text(json, "refresh_token"), DateTime.UtcNow.AddSeconds(seconds)));
    }

    private async Task<(JsonElement? Json, ProviderFailure? Failure)> GetAsync(string accessToken, string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_apiBase, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await SendAsync(request, ct);
    }

    private async Task<(JsonElement? Json, ProviderFailure? Failure)> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        HttpResponseMessage response;
        try
        {
            response = await _http.CreateClient(HttpClientName).SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Lightspeed could not be reached ({Path})", request.RequestUri?.AbsolutePath);
            return (null, new ProviderFailure("Lightspeed could not be reached. The next import tries again.", Unreachable: true));
        }
        using (response)
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            JsonElement? json = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(text)) json = JsonDocument.Parse(text).RootElement.Clone();
            }
            catch (JsonException)
            {
                json = null;
            }
            if (response.IsSuccessStatusCode && json != null) return (json, null);
            var status = (int)response.StatusCode;
            var message = json is { } j ? Text(j, "error_description", "message", "detail", "error", "title") : null;
            var unreachable = response.StatusCode is HttpStatusCode.TooManyRequests || status >= 500;
            var words = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => "Lightspeed no longer accepts Cadence's access to the account. Connect it again.",
                HttpStatusCode.Forbidden => "Lightspeed does not let Cadence read this: the account or location does not allow it.",
                HttpStatusCode.NotFound => "Lightspeed does not know that location.",
                _ when unreachable => "Lightspeed is busy or unavailable. The next import tries again.",
                _ => "Lightspeed refused the request.",
            };
            if (message != null) words += $" ({Truncate(message, 200)})";
            return (null, new ProviderFailure(words, Text(json ?? default, "error"), unreachable, status));
        }
    }

    // ── reading JSON ─────────────────────────────────────────────────────────

    private static IEnumerable<JsonElement> Items(JsonElement element) =>
        element.ValueKind == JsonValueKind.Array ? element.EnumerateArray() : [];

    private static JsonElement? Property(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        foreach (var name in names)
            foreach (var property in element.EnumerateObject())
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase) && property.Value.ValueKind != JsonValueKind.Null)
                    return property.Value;
        return null;
    }

    private static string? Text(JsonElement element, params string[] names)
    {
        if (Property(element, names) is not { } value) return null;
        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static decimal? Number(JsonElement element, params string[] names)
    {
        if (Property(element, names) is not { } value) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String
            && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        return null;
    }

    private static decimal? Money(JsonElement element, params string[] names) =>
        Number(element, names) is { } value ? Math.Round(value, 2, MidpointRounding.AwayFromZero) : null;

    private static bool Flag(JsonElement element, params string[] names) =>
        Property(element, names) is { } value && (value.ValueKind == JsonValueKind.True
            || (value.ValueKind == JsonValueKind.String && string.Equals(value.GetString(), "true", StringComparison.OrdinalIgnoreCase)));

    private static DateTime? Date(JsonElement element, params string[] names) =>
        Text(element, names) is { } text
        && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date)
            ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
            : null;

    private static string? Join(string? first, string? last)
    {
        var joined = string.Join(' ', new[] { first, last }.Where(s => !string.IsNullOrWhiteSpace(s)));
        return joined.Length > 0 ? joined : null;
    }

    private static string Iso(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
