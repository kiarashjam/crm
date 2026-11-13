using System.Security.Cryptography;
using System.Text;

namespace ACI.Application.Common;

/// <summary>
/// How Cadence signs what it posts, and how a receiver proves a report back is theirs.
/// </summary>
/// <remarks>
/// <para>
/// Every signed post carries <c>X-Cadence-Timestamp</c> (Unix seconds) and
/// <c>X-Cadence-Signature-256: sha256=&lt;hex&gt;</c>, the HMAC-SHA256 with the shared
/// secret of <c>"{timestamp}.{body}"</c>. Signing the time with the body lets a receiver
/// refuse an old post replayed at it; the older <c>X-Cadence-Signature</c>, over the body
/// alone, is still sent so receivers built for it keep working.
/// </para>
/// <para>
/// A handoff's callback token is derived from the same secret, so Cadence stores no
/// token and a receiver needs nothing beyond the handoff to report on it. Changing the
/// secret retires the tokens of handoffs made before.
/// </para>
/// </remarks>
public static class WebhookSigning
{
    public const string TimestampHeader = "X-Cadence-Timestamp";
    public const string SignatureHeader = "X-Cadence-Signature-256";

    /// <summary>The <c>sha256=…</c> value of <see cref="SignatureHeader"/>.</summary>
    public static string Sign(string secret, long timestamp, string body) =>
        "sha256=" + Hex(secret, $"{timestamp}.{body}");

    /// <summary>The token a receiver presents, as a bearer token, to report on one handoff.</summary>
    public static string CallbackToken(string secret, Guid onboardingId) =>
        Hex(secret, "cadence-onboarding-callback:" + onboardingId.ToString("N"));

    /// <summary>Whether <paramref name="presented"/> is the handoff's token, compared in constant time.</summary>
    public static bool IsCallbackToken(string secret, Guid onboardingId, string? presented)
    {
        if (string.IsNullOrEmpty(presented)) return false;
        var expected = Encoding.UTF8.GetBytes(CallbackToken(secret, onboardingId));
        var given = Encoding.UTF8.GetBytes(presented.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expected, given);
    }

    /// <summary>
    /// Whether <paramref name="presented"/> is the <c>sha256=…</c> signature of
    /// <paramref name="body"/> at <paramref name="timestamp"/> with the secret, compared in
    /// constant time: what the club's own portal signs a request back to Cadence with.
    /// </summary>
    public static bool IsSignature(string secret, long timestamp, string body, string? presented)
    {
        if (string.IsNullOrEmpty(presented)) return false;
        var expected = Encoding.UTF8.GetBytes(Sign(secret, timestamp, body));
        var given = Encoding.UTF8.GetBytes(presented.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expected, given);
    }

    /// <summary>A new secret: 32 random bytes, as 64 hex characters.</summary>
    public static string NewSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    private static string Hex(string secret, string text) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
