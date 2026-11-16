using System.Net;

namespace ACI.Application.Interfaces;

/// <summary>
/// This API's public origin ("https://api.example.com"), for addresses that leave it and
/// must be reached from outside: an email image's, the callback a portal reports to.
/// </summary>
public interface IPublicAddress
{
    /// <summary>The origin, without a trailing slash.</summary>
    string ApiBaseUrl { get; }
}

/// <summary>Whether an address can be reached from outside this machine.</summary>
public static class PublicAddresses
{
    /// <summary>An absolute http(s) address whose host is not this machine (localhost, 127.x, ::1).</summary>
    public static bool IsPublic(string? url) =>
        Uri.TryCreate((url ?? "").Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && !IsLoopback(uri.Host);

    /// <summary>A host name that means this machine.</summary>
    public static bool IsLoopback(string? host)
    {
        var h = (host ?? "").Trim().Trim('[', ']').ToLowerInvariant();
        if (h.Length == 0 || h == "localhost" || h.EndsWith(".localhost", StringComparison.Ordinal)) return true;
        return IPAddress.TryParse(h, out var ip) && IPAddress.IsLoopback(ip);
    }
}
