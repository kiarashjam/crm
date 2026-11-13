using System.Security.Cryptography;
using System.Text;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace ACI.Infrastructure.Services;

/// <summary>
/// Unsubscribe tokens: the workspace and the address, encrypted and authenticated
/// by Data Protection, the same key ring that keeps two-factor secrets. Nobody can
/// make one for an address they were not sent, and the address does not show in
/// the link. No expiry, on purpose.
/// </summary>
/// <remarks>
/// <para>
/// The token travels as one segment of the link's path (/unsubscribe/{token} and the API's
/// /api/public/unsubscribe/{token}/one-click). IIS on a Windows App Service refuses a path
/// segment longer than 260 characters (UrlSegmentMaxLength) with a 400, before the app sees
/// it. The ids used to be written as text ("{org:N}|{member:N}|{email}"), so a member's token
/// passed 260 characters from an address of about 30: those people could not unsubscribe.
/// The ids are now packed as bytes behind a layout byte, which keeps a member's token within
/// 260 characters for an address of up to 62 characters, and an outreach token for one of up
/// to 78 (Data Protection adds 84 bytes of its own around the encrypted payload).
/// </para>
/// <para>
/// Tokens already sent in emails, in the text form, are still read: their plaintext starts
/// with a hex digit, never with the layout byte.
/// </para>
/// </remarks>
public sealed class UnsubscribeLinkSigner : IUnsubscribeLinks
{
    /// <summary>The first byte of a packed token's plaintext, so a later layout can be told apart.</summary>
    private const byte Layout = 1;

    /// <summary>The longest token read: anything far longer is not one of ours.</summary>
    private const int MaxTokenLength = 1024;

    private readonly IDataProtector _protector;

    // A purpose of its own: an outreach link, which stops every email to the address, can
    // never be read as a member's, which stops only marketing, nor the other way round.
    private readonly IDataProtector _members;

    public UnsubscribeLinkSigner(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("ACI.Email.Unsubscribe.v1");
        _members = provider.CreateProtector("ACI.Email.Unsubscribe.Member.v1");
    }

    public string CreateToken(Guid organizationId, string email) => Pack(_protector, organizationId, null, email);

    public bool TryRead(string token, out Guid organizationId, out string email)
    {
        organizationId = Guid.Empty;
        email = "";
        if (Open(_protector, token) is not { } plain) return false;
        if (plain.Length > 17 && plain[0] == Layout)
        {
            organizationId = new Guid(plain.AsSpan(1, 16));
            email = Encoding.UTF8.GetString(plain, 17, plain.Length - 17);
            return true;
        }
        // A link sent before the packed form: "{org:N}|{email}".
        var parts = Encoding.UTF8.GetString(plain).Split('|', 2);
        if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out organizationId) || parts[1].Length == 0) return false;
        email = parts[1];
        return true;
    }

    public string CreateMemberToken(Guid organizationId, Guid memberId, string email) => Pack(_members, organizationId, memberId, email);

    public bool TryReadMember(string token, out Guid organizationId, out Guid memberId, out string email)
    {
        organizationId = Guid.Empty;
        memberId = Guid.Empty;
        email = "";
        if (Open(_members, token) is not { } plain) return false;
        if (plain.Length > 33 && plain[0] == Layout)
        {
            organizationId = new Guid(plain.AsSpan(1, 16));
            memberId = new Guid(plain.AsSpan(17, 16));
            email = Encoding.UTF8.GetString(plain, 33, plain.Length - 33);
            return true;
        }
        // A link sent before the packed form: "{org:N}|{member:N}|{email}".
        var parts = Encoding.UTF8.GetString(plain).Split('|', 3);
        if (parts.Length != 3 || !Guid.TryParseExact(parts[0], "N", out organizationId)
            || !Guid.TryParseExact(parts[1], "N", out memberId)) return false;
        email = parts[2];
        return true;
    }

    /// <summary>The layout byte, the workspace, the member when there is one, and the address in UTF-8.</summary>
    private static string Pack(IDataProtector protector, Guid organizationId, Guid? memberId, string email)
    {
        var address = Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant());
        var header = memberId == null ? 17 : 33;
        var bytes = new byte[header + address.Length];
        bytes[0] = Layout;
        organizationId.TryWriteBytes(bytes.AsSpan(1, 16));
        if (memberId is Guid member) member.TryWriteBytes(bytes.AsSpan(17, 16));
        address.CopyTo(bytes, header);
        return ToBase64Url(protector.Protect(bytes));
    }

    /// <summary>The token's plaintext, or null when it is not one of this protector's.</summary>
    private static byte[]? Open(IDataProtector protector, string? token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength) return null;
        try
        {
            return protector.Unprotect(FromBase64Url(token.Trim()));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }

    /// <summary>Base64 as a URL carries it: '-' and '_' for '+' and '/', no padding (as Data Protection writes its text tokens).</summary>
    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string text)
    {
        var base64 = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
    }
}
