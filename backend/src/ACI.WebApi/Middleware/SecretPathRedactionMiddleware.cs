using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.Features;

namespace ACI.WebApi.Middleware;

/// <summary>
/// Keeps secrets that live in a URL path out of the request log.
/// </summary>
/// <remarks>
/// <para>
/// A contract signing link carries its token in the path, because it has to: it is
/// emailed to somebody with no account, and the token is the entire authorisation
/// to read and sign that contract. Serilog's request logging writes the whole
/// request path at Information level, which put that token — in plaintext, in full
/// — into the application log on every read, every signature and every decline.
/// </para>
/// <para>
/// That defeats the point of everything <c>ContractSigningToken</c> does. The token
/// is 32 CSPRNG bytes and stored only as a SHA-256 hash precisely so that a
/// database leak cannot hand anybody the ability to sign; a log file that holds the
/// raw value hands over the same thing, to a wider audience, with no expiry.
/// </para>
/// <para>
/// So the path is rewritten on the way OUT of the pipeline, after routing has
/// already used it and before Serilog reads it to write the completion event.
/// Register this immediately after <c>UseSerilogRequestLogging</c>, so it sits
/// inside that middleware and its cleanup runs first.
/// </para>
/// <para>
/// Redacting rather than dropping the route: "/api/public/contracts/[redacted]" is
/// still useful in a log — it says which endpoint was hit and how it answered —
/// while carrying nothing worth stealing.
/// </para>
/// </remarks>
public sealed class SecretPathRedactionMiddleware
{
    /// <summary>Stands in for the secret. Distinctive, so its presence is obvious.</summary>
    public const string Placeholder = "[redacted]";

    /// <summary>
    /// Paths whose one variable segment is a secret.
    /// </summary>
    /// <remarks>
    /// An allowlist of shapes rather than a search for anything token-shaped: a
    /// pattern that guessed would eventually redact a lead id and eventually miss a
    /// real secret. Every entry here is a route where a path segment IS a
    /// credential, and adding such a route means adding it here.
    /// </remarks>
    private static readonly Regex[] SecretSegments =
    {
        // /api/public/contracts/{token}[/sign|/decline|/pdf]
        new(@"^(?<keep>/api/public/contracts/)(?<secret>[^/?]+)(?<rest>.*)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase),
    };

    private readonly RequestDelegate _next;

    public SecretPathRedactionMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        finally
        {
            // In a finally block so an exception on the way through cannot leave the
            // secret in place for the error log — which is the log most likely to be
            // read by the most people.
            Redact(context);
        }
    }

    private static void Redact(HttpContext context)
    {
        var path = context.Request.Path.Value;
        if (string.IsNullOrEmpty(path)) return;

        var redacted = RedactPath(path);
        if (redacted is null) return;

        context.Request.Path = redacted;

        // Serilog prefers the raw target when it is available, so rewriting only
        // Request.Path would silently change nothing on a real server. The raw
        // target includes the query string, which must be preserved.
        var feature = context.Features.Get<IHttpRequestFeature>();
        if (feature?.RawTarget is { Length: > 0 } raw)
        {
            var split = raw.IndexOf('?');
            var rawPath = split < 0 ? raw : raw[..split];
            var query = split < 0 ? "" : raw[split..];
            feature.RawTarget = (RedactPath(rawPath) ?? rawPath) + query;
        }
    }

    /// <summary>The path with its secret segment replaced, or null if it holds none.</summary>
    public static string? RedactPath(string path)
    {
        foreach (var pattern in SecretSegments)
        {
            var match = pattern.Match(path);
            if (!match.Success) continue;

            var secret = match.Groups["secret"].Value;
            // An empty segment is not a secret, and neither is one we already did —
            // this runs in a finally block and must be safe to reach twice.
            if (secret.Length == 0 || secret == Placeholder) return null;

            return match.Groups["keep"].Value + Placeholder + match.Groups["rest"].Value;
        }
        return null;
    }
}
