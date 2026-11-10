using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ACI.Application.Common.Payments;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services.Payments;

/// <summary>
/// Stripe, through its HTTP API: form-encoded requests with the club's secret key, an
/// Idempotency-Key on every request that creates something (so a retry never charges twice),
/// and a pinned API version so the answers keep their shape.
/// </summary>
/// <remarks>
/// <para>
/// No Stripe package: the handful of calls the club needs are plain requests, and keeping them
/// here keeps what is sent to Stripe readable and testable (the HTTP tests answer for Stripe
/// with a fake handler).
/// </para>
/// <para>
/// Payments:Stripe:ApiBase says where Stripe is (https://api.stripe.com/ unless set), which a
/// test or a local stripe-mock can change.
/// </para>
/// </remarks>
public sealed class StripePaymentProvider : IPaymentProvider
{
    public const string HttpClientName = "stripe";

    /// <summary>The API version the requests and answers are written for.</summary>
    public const string ApiVersion = "2024-06-20";

    /// <summary>Currencies Stripe counts in whole units (no cents).</summary>
    private static readonly HashSet<string> ZeroDecimal = new(StringComparer.OrdinalIgnoreCase)
    {
        "BIF", "CLP", "DJF", "GNF", "JPY", "KMF", "KRW", "MGA", "PYG", "RWF", "UGX", "VND", "VUV", "XAF", "XOF", "XPF",
    };

    private readonly IHttpClientFactory _http;
    private readonly ILogger<StripePaymentProvider> _logger;
    private readonly Uri _apiBase;

    public StripePaymentProvider(IHttpClientFactory http, IConfiguration configuration, ILogger<StripePaymentProvider> logger)
    {
        _http = http;
        _logger = logger;
        var configured = configuration["Payments:Stripe:ApiBase"];
        _apiBase = new Uri(string.IsNullOrWhiteSpace(configured) ? "https://api.stripe.com/" : configured.TrimEnd('/') + "/");
    }

    public string Name => PaymentProviders.Stripe;

    public async Task<ProviderResult<bool>> CheckKeyAsync(string secretKey, CancellationToken ct = default)
    {
        // Listing one customer reads and changes nothing, and a restricted key made for Cadence
        // needs that permission anyway.
        var answer = await SendAsync(secretKey, HttpMethod.Get, "v1/customers?limit=1", null, null, ct);
        return answer.Failure is { } failure ? ProviderResult<bool>.Fail(failure) : ProviderResult<bool>.Ok(true);
    }

    public async Task<ProviderResult<string>> CreateCustomerAsync(string secretKey, ProviderCustomerRequest request, CancellationToken ct = default)
    {
        var form = new List<KeyValuePair<string, string>> { new("name", request.Name) };
        if (!string.IsNullOrWhiteSpace(request.Email)) form.Add(new("email", request.Email));
        AddMetadata(form, "metadata", request.Metadata);
        var answer = await SendAsync(secretKey, HttpMethod.Post, "v1/customers", form, request.IdempotencyKey, ct);
        if (answer.Failure is { } failure) return ProviderResult<string>.Fail(failure);
        return ProviderResult<string>.Ok(Text(answer.Json!.Value, "id") ?? "");
    }

    public async Task<ProviderResult<ProviderCheckout>> CreateCheckoutAsync(string secretKey, ProviderCheckoutRequest request, CancellationToken ct = default)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("mode", request.Mode),
            new("success_url", request.SuccessUrl),
            new("cancel_url", request.CancelUrl),
            new("client_reference_id", request.ClientReferenceId),
            new("expires_at", new DateTimeOffset(request.ExpiresAtUtc).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
            new("locale", "auto"),
        };
        for (var i = 0; i < request.MethodTypes.Count; i++) form.Add(new($"payment_method_types[{i}]", request.MethodTypes[i]));
        if (request.CustomerId != null) form.Add(new("customer", request.CustomerId));
        else if (!string.IsNullOrWhiteSpace(request.CustomerEmail)) form.Add(new("customer_email", request.CustomerEmail));
        AddMetadata(form, "metadata", request.Metadata);
        if (request.Mode == "payment")
        {
            form.Add(new("line_items[0][quantity]", "1"));
            form.Add(new("line_items[0][price_data][currency]", request.Currency.ToLowerInvariant()));
            form.Add(new("line_items[0][price_data][unit_amount]", Minor(request.Amount, request.Currency)));
            form.Add(new("line_items[0][price_data][product_data][name]", Clip(request.Description, 250)));
            form.Add(new("payment_intent_data[description]", Clip(request.Description, 250)));
            AddMetadata(form, "payment_intent_data[metadata]", request.Metadata);
        }
        else
        {
            // Saving a card: a currency is needed for a SEPA mandate, and harmless otherwise.
            form.Add(new("currency", request.Currency.ToLowerInvariant()));
            form.Add(new("setup_intent_data[description]", Clip(request.Description, 250)));
            AddMetadata(form, "setup_intent_data[metadata]", request.Metadata);
        }
        if (!string.IsNullOrWhiteSpace(request.ConsentText)) form.Add(new("custom_text[submit][message]", Clip(request.ConsentText, 1200)));

        var answer = await SendAsync(secretKey, HttpMethod.Post, "v1/checkout/sessions", form, request.IdempotencyKey, ct);
        if (answer.Failure is { } failure) return ProviderResult<ProviderCheckout>.Fail(failure);
        return ProviderResult<ProviderCheckout>.Ok(ReadCheckout(answer.Json!.Value));
    }

    public async Task<ProviderResult<ProviderPaymentIntent>> ChargeSavedMethodAsync(string secretKey, ProviderChargeRequest request, CancellationToken ct = default)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("amount", Minor(request.Amount, request.Currency)),
            new("currency", request.Currency.ToLowerInvariant()),
            new("customer", request.CustomerId),
            new("payment_method", request.PaymentMethodId),
            new("payment_method_types[0]", request.MethodType),
            new("off_session", "true"),
            new("confirm", "true"),
            new("description", Clip(request.Description, 250)),
        };
        AddMetadata(form, "metadata", request.Metadata);
        var answer = await SendAsync(secretKey, HttpMethod.Post, "v1/payment_intents", form, request.IdempotencyKey, ct);
        // A declined card is an error answer that carries the payment it failed: that payment,
        // failed, is the answer, not a failure to reach Stripe.
        if (answer.Failure is { } failure)
        {
            if (answer.Json is { } error && error.TryGetProperty("error", out var body) && body.TryGetProperty("payment_intent", out var intent)
                && intent.ValueKind == JsonValueKind.Object)
                return ProviderResult<ProviderPaymentIntent>.Ok(ReadPaymentIntent(intent) with
                {
                    FailureCode = Text(body, "decline_code") ?? Text(body, "code"),
                    FailureMessage = Text(body, "message"),
                });
            return ProviderResult<ProviderPaymentIntent>.Fail(failure);
        }
        return ProviderResult<ProviderPaymentIntent>.Ok(ReadPaymentIntent(answer.Json!.Value));
    }

    public async Task<ProviderResult<ProviderSetup>> GetSetupAsync(string secretKey, string setupId, CancellationToken ct = default)
    {
        var answer = await SendAsync(secretKey, HttpMethod.Get, $"v1/setup_intents/{Uri.EscapeDataString(setupId)}?expand[]=payment_method", null, null, ct);
        if (answer.Failure is { } failure) return ProviderResult<ProviderSetup>.Fail(failure);
        return ProviderResult<ProviderSetup>.Ok(ReadSetup(answer.Json!.Value));
    }

    public async Task<ProviderResult<ProviderRefund>> RefundAsync(string secretKey, ProviderRefundRequest request, CancellationToken ct = default)
    {
        var form = new List<KeyValuePair<string, string>>
        {
            new("payment_intent", request.PaymentIntentId),
            new("amount", Minor(request.Amount, request.Currency)),
            new("reason", "requested_by_customer"),
        };
        AddMetadata(form, "metadata", request.Metadata);
        var answer = await SendAsync(secretKey, HttpMethod.Post, "v1/refunds", form, request.IdempotencyKey, ct);
        if (answer.Failure is { } failure) return ProviderResult<ProviderRefund>.Fail(failure);
        return ProviderResult<ProviderRefund>.Ok(ReadRefund(answer.Json!.Value));
    }

    public async Task<ProviderResult<bool>> DetachPaymentMethodAsync(string secretKey, string paymentMethodId, CancellationToken ct = default)
    {
        var answer = await SendAsync(secretKey, HttpMethod.Post, $"v1/payment_methods/{Uri.EscapeDataString(paymentMethodId)}/detach", [], null, ct);
        return answer.Failure is { } failure ? ProviderResult<bool>.Fail(failure) : ProviderResult<bool>.Ok(true);
    }

    public ProviderEvent? ReadEvent(string webhookSecret, byte[] body, string? signatureHeader, DateTime utcNow, out string? problem)
    {
        if (!StripeSignature.Verify(webhookSecret, body, signatureHeader, utcNow, out problem)) return null;
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            var id = Text(root, "id");
            var type = Text(root, "type");
            if (id == null || type == null || !root.TryGetProperty("data", out var data) || !data.TryGetProperty("object", out var obj))
            {
                problem = "The post is not a Stripe event.";
                return null;
            }
            var objectType = Text(obj, "object");
            return new ProviderEvent(id, type, Text(obj, "id"),
                PaymentIntent: objectType == "payment_intent" ? ReadPaymentIntent(obj) : null,
                Setup: objectType == "setup_intent" ? ReadSetup(obj) : null,
                Checkout: objectType == "checkout.session" ? ReadCheckout(obj) : null,
                Charge: objectType == "charge" ? ReadCharge(obj) : null);
        }
        catch (JsonException)
        {
            problem = "The post is not JSON.";
            return null;
        }
    }

    // ── sending ──────────────────────────────────────────────────────────────

    private sealed record Answer(JsonElement? Json, ProviderFailure? Failure);

    private async Task<Answer> SendAsync(string secretKey, HttpMethod method, string path, List<KeyValuePair<string, string>>? form,
        string? idempotencyKey, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, new Uri(_apiBase, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);
        request.Headers.Add("Stripe-Version", ApiVersion);
        if (idempotencyKey != null) request.Headers.Add("Idempotency-Key", idempotencyKey);
        if (form != null) request.Content = new FormUrlEncodedContent(form);
        try
        {
            using var response = await _http.CreateClient(HttpClientName).SendAsync(request, ct);
            var text = await response.Content.ReadAsStringAsync(ct);
            JsonElement? json = null;
            try
            {
                using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text);
                json = document.RootElement.Clone();
            }
            catch (JsonException)
            {
                // Not JSON: a proxy's error page, say. The status says enough.
            }
            if (response.IsSuccessStatusCode) return new Answer(json ?? default(JsonElement), null);

            var error = json is { } j && j.TryGetProperty("error", out var e) ? e : default;
            var message = error.ValueKind == JsonValueKind.Object ? Text(error, "message") : null;
            var code = error.ValueKind == JsonValueKind.Object ? Text(error, "decline_code") ?? Text(error, "code") ?? Text(error, "type") : null;
            var status = (int)response.StatusCode;
            var failure = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => new ProviderFailure("Stripe did not accept the key.", code ?? "authentication", StatusCode: status),
                HttpStatusCode.Forbidden => new ProviderFailure($"The Stripe key may not do this: {message ?? "permission denied"}.", code, StatusCode: status),
                >= HttpStatusCode.InternalServerError => new ProviderFailure("Stripe could not be reached just now.", code, Unreachable: true, StatusCode: status),
                (HttpStatusCode)429 => new ProviderFailure("Stripe is busy; try again in a minute.", code, Unreachable: true, StatusCode: status),
                _ => new ProviderFailure(message ?? $"Stripe refused the request ({status}).", code, StatusCode: status),
            };
            if (failure.Unreachable) _logger.LogWarning("Stripe answered {Status} to {Method} {Path}", status, method, PathOnly(path));
            return new Answer(json, failure);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Stripe could not be reached for {Method} {Path}", method, PathOnly(path));
            return new Answer(null, new ProviderFailure("Stripe could not be reached just now.", Unreachable: true));
        }
    }

    private static string PathOnly(string path) => path.Split('?')[0];

    private static void AddMetadata(List<KeyValuePair<string, string>> form, string prefix, IReadOnlyDictionary<string, string> metadata)
    {
        foreach (var (key, value) in metadata) form.Add(new($"{prefix}[{key}]", Clip(value, 500)));
    }

    /// <summary>The amount in Stripe's minor unit: cents for CHF and EUR, whole units for the zero-decimal currencies.</summary>
    public static string Minor(decimal amount, string currency) =>
        (ZeroDecimal.Contains(currency) ? Math.Round(amount, 0, MidpointRounding.AwayFromZero) : Math.Round(amount * 100, 0, MidpointRounding.AwayFromZero))
        .ToString("0", CultureInfo.InvariantCulture);

    private static decimal Major(long minor, string currency) => ZeroDecimal.Contains(currency) ? minor : minor / 100m;

    // ── reading answers ──────────────────────────────────────────────────────

    private static ProviderCheckout ReadCheckout(JsonElement obj) => new(
        Text(obj, "id") ?? "",
        Text(obj, "url"),
        Text(obj, "mode") ?? "payment",
        Text(obj, "status"),
        Text(obj, "payment_status"),
        IdOf(obj, "payment_intent"),
        IdOf(obj, "setup_intent"),
        IdOf(obj, "customer"),
        obj.TryGetProperty("expires_at", out var expires) && expires.ValueKind == JsonValueKind.Number
            ? DateTimeOffset.FromUnixTimeSeconds(expires.GetInt64()).UtcDateTime : null,
        Metadata(obj));

    private static ProviderPaymentIntent ReadPaymentIntent(JsonElement obj)
    {
        var currency = (Text(obj, "currency") ?? "").ToUpperInvariant();
        var error = obj.TryGetProperty("last_payment_error", out var e) && e.ValueKind == JsonValueKind.Object ? e : default;
        string? methodType = null;
        if (obj.TryGetProperty("payment_method_types", out var types) && types.ValueKind == JsonValueKind.Array && types.GetArrayLength() == 1)
            methodType = types[0].GetString();
        if (obj.TryGetProperty("payment_method", out var method) && method.ValueKind == JsonValueKind.Object) methodType = Text(method, "type") ?? methodType;
        if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("payment_method", out var failedMethod) && failedMethod.ValueKind == JsonValueKind.Object)
            methodType ??= Text(failedMethod, "type");
        return new ProviderPaymentIntent(
            Text(obj, "id") ?? "",
            Text(obj, "status") ?? "",
            Major(Long(obj, "amount"), currency),
            Major(Long(obj, "amount_received"), currency),
            currency,
            IdOf(obj, "latest_charge"),
            methodType,
            error.ValueKind == JsonValueKind.Object ? Text(error, "decline_code") ?? Text(error, "code") : null,
            error.ValueKind == JsonValueKind.Object ? Text(error, "message") : null,
            IdOf(obj, "customer"),
            IdOf(obj, "payment_method"),
            Metadata(obj));
    }

    private static ProviderSetup ReadSetup(JsonElement obj)
    {
        ProviderPaymentMethod? method = null;
        if (obj.TryGetProperty("payment_method", out var pm) && pm.ValueKind == JsonValueKind.Object)
        {
            var type = Text(pm, "type") ?? "card";
            var details = pm.TryGetProperty(type, out var d) && d.ValueKind == JsonValueKind.Object ? d : default;
            method = new ProviderPaymentMethod(
                Text(pm, "id") ?? "",
                type,
                details.ValueKind == JsonValueKind.Object ? Text(details, "brand") ?? Text(details, "bank_code") : null,
                details.ValueKind == JsonValueKind.Object ? Text(details, "last4") : null,
                details.ValueKind == JsonValueKind.Object && details.TryGetProperty("exp_month", out var m) && m.ValueKind == JsonValueKind.Number ? m.GetInt32() : null,
                details.ValueKind == JsonValueKind.Object && details.TryGetProperty("exp_year", out var y) && y.ValueKind == JsonValueKind.Number ? y.GetInt32() : null);
        }
        else if (IdOf(obj, "payment_method") is { } pmId)
        {
            method = new ProviderPaymentMethod(pmId, "card", null, null, null, null);
        }
        return new ProviderSetup(Text(obj, "id") ?? "", Text(obj, "status") ?? "", IdOf(obj, "customer"), method, Metadata(obj));
    }

    private static ProviderRefund ReadRefund(JsonElement obj)
    {
        var currency = (Text(obj, "currency") ?? "").ToUpperInvariant();
        return new ProviderRefund(Text(obj, "id") ?? "", Text(obj, "status") ?? "", Major(Long(obj, "amount"), currency), currency, IdOf(obj, "payment_intent"));
    }

    private static ProviderCharge ReadCharge(JsonElement obj)
    {
        var currency = (Text(obj, "currency") ?? "").ToUpperInvariant();
        var refunds = new List<ProviderRefund>();
        if (obj.TryGetProperty("refunds", out var list) && list.ValueKind == JsonValueKind.Object
            && list.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            foreach (var refund in data.EnumerateArray()) refunds.Add(ReadRefund(refund));
        return new ProviderCharge(Text(obj, "id") ?? "", IdOf(obj, "payment_intent"), currency, refunds);
    }

    private static IReadOnlyDictionary<string, string> Metadata(JsonElement obj)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (obj.TryGetProperty("metadata", out var metadata) && metadata.ValueKind == JsonValueKind.Object)
            foreach (var property in metadata.EnumerateObject())
                if (property.Value.ValueKind == JsonValueKind.String) result[property.Name] = property.Value.GetString() ?? "";
        return result;
    }

    private static string? Text(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>An id that may come as a string or, expanded, as an object with an id.</summary>
    private static string? IdOf(JsonElement obj, string name)
    {
        if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Object => Text(value, "id"),
            _ => null,
        };
    }

    private static long Long(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var n) ? n : 0;

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];
}
