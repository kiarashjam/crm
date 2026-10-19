using System.Net;
using System.Net.Sockets;

namespace ACI.Application.Common;

/// <summary>
/// Whether an address is on the public internet. A workspace's outbound webhook is
/// posted by this server, so it must not reach the server itself or the network it
/// runs in: loopback, private ranges, link-local (where cloud metadata lives), carrier
/// NAT, multicast and their IPv6 equivalents are all refused.
/// </summary>
public static class PublicAddress
{
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address)) return false;
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 0 || b[0] == 10 || b[0] == 127
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
                || (b[0] == 169 && b[1] == 254)
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || b[0] >= 224);
        }
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !(address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.IPv6None)
                || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal
                || address.IsIPv6UniqueLocal || address.IsIPv6Multicast);
        }
        return false;
    }

    /// <summary>
    /// A first check of a URL someone typed, so a plainly internal address is refused
    /// when it is saved. The sending side checks again after DNS, which is the check
    /// that counts: a public-looking name can still resolve to a private address.
    /// </summary>
    public static bool LooksPublic(Uri uri)
    {
        var host = uri.IdnHost;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            return false;
        return !IPAddress.TryParse(host.Trim('[', ']'), out var ip) || IsPublic(ip);
    }
}
