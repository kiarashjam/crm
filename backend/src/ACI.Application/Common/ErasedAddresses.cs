using System.Security.Cryptography;
using System.Text;

namespace ACI.Application.Common;

/// <summary>
/// How the suppression list remembers the address of a person who was erased: by a
/// fingerprint, never the address itself.
/// </summary>
/// <remarks>
/// <para>
/// An erased person must not be emailed again, nor quietly brought back by the next import
/// of an old list, so their address has to be recognised when it comes back. Keeping the
/// address in clear would keep the very thing they asked to have erased. A SHA-256 of the
/// address as the outbox compares it (trimmed, lower case) recognises it without holding it:
/// the suppression row reads "erased:" and 64 hex digits.
/// </para>
/// <para>
/// Not salted per workspace, on purpose: a workspace copied with the organization cloner
/// keeps its suppression rows, and must go on recognising the addresses in them. The
/// fingerprint still names nobody by itself; it only answers "is this the address?" for an
/// address someone already has.
/// </para>
/// </remarks>
public static class ErasedAddresses
{
    public const string Prefix = "erased:";

    /// <summary>The address as every sender compares it.</summary>
    public static string Normalize(string? email) => (email ?? "").Trim().ToLowerInvariant();

    /// <summary>What the suppression list holds for an erased <paramref name="email"/>; empty for no address.</summary>
    public static string Key(string? email)
    {
        var address = Normalize(email);
        if (address.Length == 0) return "";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(address));
        return Prefix + Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Whether a suppression row holds a fingerprint rather than an address.</summary>
    public static bool IsKey(string? value) => value != null && value.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// The values a suppression lookup asks for: each address as itself, and as the
    /// fingerprint an erasure left for it.
    /// </summary>
    public static IReadOnlyList<string> LookupValues(IEnumerable<string?> addresses)
    {
        var values = new List<string>();
        foreach (var address in addresses.Select(Normalize).Where(a => a.Length > 0).Distinct())
        {
            values.Add(address);
            values.Add(Key(address));
        }
        return values;
    }
}
