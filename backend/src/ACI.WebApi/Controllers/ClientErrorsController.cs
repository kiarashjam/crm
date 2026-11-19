using System.Text.Json;
using System.Text.RegularExpressions;
using ACI.WebApi.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Crashes in the browser, reported by the app itself (src/app/lib/clientErrors.ts): what its
/// error boundary catches, and the window's error and unhandledrejection events. Each is written
/// to the log as one Warning, "monitoring.browser.error", which Application Insights keeps
/// however much else it samples.
/// </summary>
/// <remarks>
/// <para>
/// Anonymous, so a crash on a public page (signing a contract, paying, the member portal) is
/// counted too. So it is rate limited by address, held to 16 KB with every field clipped, and
/// the page's address is stripped of its query string and of a token in its path, as the
/// request log is; an address inside the message or the stack is treated the same way.
/// </para>
/// <para>
/// The body is read here rather than bound: navigator.sendBeacon posts text/plain, which a
/// browser sends to another origin without asking first, and which [FromBody] would refuse.
/// </para>
/// </remarks>
[ApiController]
[AllowAnonymous]
[Route("api/client-errors")]
[EnableRateLimiting(RateLimitPolicy)]
public sealed class ClientErrorsController : ControllerBase
{
    public const string RateLimitPolicy = "client-errors";

    /// <summary>Reports one address may send in a minute.</summary>
    public const int PerMinute = 30;

    /// <summary>The largest body taken.</summary>
    public const int MaxBodyBytes = 16 * 1024;

    public const int MaxMessage = 1000;
    public const int MaxStack = 4000;
    public const int MaxUrl = 2000;
    public const int MaxUserAgent = 512;
    public const int MaxRelease = 100;

    /// <summary>Stands in for a secret, as in the request log.</summary>
    public const string Placeholder = SecretPathRedactionMiddleware.Placeholder;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The app's own pages whose one variable segment is a secret: a contract's signing link, an
    /// unsubscribe link and a pay link (src/app/App.tsx). The API's are SecretPathRedactionMiddleware's.
    /// </summary>
    private static readonly Regex PageSecret = new(
        @"^(?<keep>/(sign|unsubscribe|pay)/)(?<secret>[^/]+)(?<rest>.*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AddressInText = new(@"https?://[^\s""'<>()\[\]{}]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>A JSON web token: a session, whoever holds it.</summary>
    private static readonly Regex Jwt = new(@"\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}", RegexOptions.Compiled);

    private readonly ILogger<ClientErrorsController> _logger;

    public ClientErrorsController(ILogger<ClientErrorsController> logger) => _logger = logger;

    /// <summary>
    /// <c>{ message, stack?, url?, componentStack?, userAgent?, release? }</c>, as JSON in any
    /// content type. 204 once logged; 400 without a message; 413 over 16 KB; 429 past
    /// <see cref="PerMinute"/> a minute from one address.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(MaxBodyBytes)]
    public async Task<IActionResult> Report(CancellationToken ct)
    {
        if (Request.ContentLength > MaxBodyBytes) return TooLarge();

        byte[]? body;
        try
        {
            body = await ReadAtMostAsync(Request.Body, MaxBodyBytes, ct);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return TooLarge();
        }
        if (body == null) return TooLarge();

        ClientErrorReport? report;
        try
        {
            report = JsonSerializer.Deserialize<ClientErrorReport>(body, Json);
        }
        catch (JsonException)
        {
            return BadRequest();
        }
        if (string.IsNullOrWhiteSpace(report?.Message)) return BadRequest();

        var properties = new Dictionary<string, object>();
        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value)) properties[name] = value;
        }
        Add("Stack", Clean(report.Stack, MaxStack));
        Add("ComponentStack", Clip(report.ComponentStack, MaxStack));
        Add("UserAgent", Clip(report.UserAgent ?? Request.Headers.UserAgent.ToString(), MaxUserAgent));
        Add("Release", Clip(report.Release, MaxRelease));

        using (_logger.BeginScope(properties))
        {
            _logger.LogWarning("monitoring.browser.error {Message} at {Url}", Clean(report.Message, MaxMessage), RedactUrl(report.Url));
        }
        return NoContent();
    }

    /// <summary>
    /// A page's address as it may be logged: no query string or fragment, no user name or
    /// password, and a secret path segment (the app's or the API's) replaced. Null for none.
    /// </summary>
    public static string? RedactUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var address = Bound(url.Trim(), MaxUrl);
        var cut = address.IndexOfAny(['?', '#']);
        if (cut >= 0) address = address[..cut];

        if (Uri.TryCreate(address, UriKind.Absolute, out var absolute) && !string.IsNullOrEmpty(absolute.Authority))
            address = absolute.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped) + RedactPath(absolute.AbsolutePath);
        else if (address.StartsWith('/'))
            address = RedactPath(address);
        return Clip(address, MaxUrl);
    }

    private static string RedactPath(string path)
    {
        var page = PageSecret.Match(path);
        if (page.Success) return page.Groups["keep"].Value + Placeholder + page.Groups["rest"].Value;
        return SecretPathRedactionMiddleware.RedactPath(path) ?? path;
    }

    /// <summary>Clipped, with every address in it redacted (<see cref="RedactUrl"/>) and any JSON web token replaced.</summary>
    public static string? Clean(string? text, int max)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var addresses = AddressInText.Replace(Bound(text, max), match => RedactUrl(match.Value) ?? "");
        return Clip(Jwt.Replace(addresses, Placeholder), max);
    }

    /// <summary>At most <paramref name="max"/> characters and an ellipsis.</summary>
    private static string? Clip(string? text, int max) =>
        string.IsNullOrEmpty(text) ? null : text.Length <= max ? text : text[..max] + "…";

    /// <summary>Cut short before it is worked on, so a long field costs no more than a short one.</summary>
    private static string Bound(string text, int max) => text.Length <= max + 64 ? text : text[..(max + 64)];

    private static async Task<byte[]?> ReadAtMostAsync(Stream body, int max, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        int read;
        while ((read = await body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > max) return null;
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    private ObjectResult TooLarge() => Problem(
        statusCode: StatusCodes.Status413PayloadTooLarge,
        title: "Too large",
        detail: $"A report is at most {MaxBodyBytes / 1024} KB.");

    /// <summary>What the browser sends. Every field is clipped before it is logged.</summary>
    public sealed record ClientErrorReport(
        string? Message,
        string? Stack,
        string? Url,
        string? ComponentStack,
        string? UserAgent,
        string? Release);
}
