using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ACI.Application.Common.Payments;

/// <summary>
/// Stripe's webhook signature: the Stripe-Signature header carries a time (t) and one or more
/// HMAC-SHA256 signatures (v1) of "{t}.{body}" made with the endpoint's signing secret. A post
/// whose signature does not match, or whose time is more than five minutes off, is not from
/// Stripe, or is an old one played again.
/// </summary>
public static class StripeSignature
{
    public static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(5);

    /// <summary>Whether the post was signed with <paramref name="secret"/> within the tolerance; why not, when it was not.</summary>
    public static bool Verify(string secret, byte[] payload, string? header, DateTime utcNow, out string? problem)
    {
        problem = null;
        if (string.IsNullOrWhiteSpace(secret))
        {
            problem = "No webhook signing secret is set.";
            return false;
        }
        long? timestamp = null;
        var signatures = new List<string>();
        foreach (var part in (header ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2) continue;
            if (pair[0] == "t" && long.TryParse(pair[1], NumberStyles.None, CultureInfo.InvariantCulture, out var t)) timestamp = t;
            else if (pair[0] == "v1") signatures.Add(pair[1]);
        }
        if (timestamp is not { } at || signatures.Count == 0)
        {
            problem = "The post carries no Stripe signature.";
            return false;
        }
        var signedAt = DateTimeOffset.FromUnixTimeSeconds(at).UtcDateTime;
        if ((utcNow - signedAt).Duration() > Tolerance)
        {
            problem = "The signature's time is more than five minutes off.";
            return false;
        }
        var expected = Encoding.ASCII.GetBytes(Compute(secret, payload, at));
        foreach (var signature in signatures)
            if (CryptographicOperations.FixedTimeEquals(expected, Encoding.ASCII.GetBytes(signature.ToLowerInvariant())))
                return true;
        problem = "The signature does not match the webhook signing secret.";
        return false;
    }

    /// <summary>The header Stripe would send for this body at this time: for tests, which play Stripe.</summary>
    public static string Header(string secret, byte[] payload, DateTime signedAtUtc)
    {
        var at = new DateTimeOffset(DateTime.SpecifyKind(signedAtUtc, DateTimeKind.Utc)).ToUnixTimeSeconds();
        return $"t={at},v1={Compute(secret, payload, at)}";
    }

    private static string Compute(string secret, byte[] payload, long timestamp)
    {
        var prefix = Encoding.UTF8.GetBytes(timestamp.ToString(CultureInfo.InvariantCulture) + ".");
        var signed = new byte[prefix.Length + payload.Length];
        prefix.CopyTo(signed, 0);
        payload.CopyTo(signed, prefix.Length);
        return Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signed)).ToLowerInvariant();
    }
}
