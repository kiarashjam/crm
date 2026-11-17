using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ACI.Application.Common;

/// <summary>What a lead's invitation to book a meeting says, as the website reads it.</summary>
/// <param name="Version">1.</param>
/// <param name="OrganizationId">The workspace the lead is in.</param>
/// <param name="LeadId">The lead; <see cref="Guid.Empty"/> in a test email's invitation, whose booking is recorded nowhere.</param>
/// <param name="Name">The lead's full name.</param>
/// <param name="Email">Where the lead was emailed.</param>
/// <param name="Code">The booking code shown in the email ("RDV-7K2Q").</param>
/// <param name="ApiBaseUrl">This CRM's public API address: the website posts the booking to its /api/public/meetings.</param>
/// <param name="ExpiresAt">Unix seconds after which the website refuses the invitation.</param>
public sealed record MeetingInvitationPayload(
    int Version, Guid OrganizationId, Guid LeadId, string Name, string Email, string Code, string ApiBaseUrl, long ExpiresAt);

/// <summary>
/// A lead's personal link to the workspace's booking page, and the short code that goes with it.
/// </summary>
/// <remarks>
/// <para>
/// The website and the CRM share the workspace's onboarding secret (the one that signs its
/// handoffs). The invitation is <c>base64url(JSON) + "." + base64url(HMAC-SHA256(secret, first part))</c>,
/// base64url as in RFC 4648 §5 without padding, the HMAC keyed with the secret's UTF-8 bytes
/// over the ASCII bytes of the first part. The JSON is
/// <c>{"v":1,"o":org,"l":lead,"n":name,"e":email,"c":code,"u":api,"x":expiry}</c>, ids written
/// as lower-case hyphenated GUIDs and the expiry in Unix seconds. The website checks the
/// signature and the expiry, greets the person by name and posts the booking back, signed.
/// </para>
/// <para>
/// The code is for people, not machines: a short reference the person can read out on the
/// phone. It is "RDV-" and four characters with nothing that looks like another (no 0, O, 1,
/// I or L), made from the SHA-256 of the lead's id, so the same lead always has the same one
/// and nothing has to be stored. Two leads can share one; the lead's id in the invitation is
/// what identifies them.
/// </para>
/// </remarks>
public static class MeetingInvitation
{
    public const string CodePrefix = "RDV-";

    /// <summary>The characters a code is made of: letters and digits that cannot be read as another.</summary>
    public const string CodeAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>How long an invitation can be used: the newsletter's reader has two months to book.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(60);

    /// <summary>
    /// The lead's booking code: "RDV-" and four characters, from the first four bytes of the
    /// SHA-256 of the lead's id (lower-case, hyphenated) read as a big-endian number, taken in
    /// base 31 over <see cref="CodeAlphabet"/>, lowest digit first.
    /// </summary>
    public static string Code(Guid leadId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(leadId.ToString("D")));
        var value = BinaryPrimitives.ReadUInt32BigEndian(hash);
        var chars = new char[4];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = CodeAlphabet[(int)(value % (uint)CodeAlphabet.Length)];
            value /= (uint)CodeAlphabet.Length;
        }
        return CodePrefix + new string(chars);
    }

    /// <summary>The signed invitation for <paramref name="payload"/>.</summary>
    public static string Token(string secret, MeetingInvitationPayload payload)
    {
        var first = Base64Url(Json(payload));
        return first + "." + Base64Url(Sign(secret, first));
    }

    /// <summary>The booking page with the invitation added to its query string.</summary>
    public static string BookingUrl(string bookingPage, string token)
    {
        var page = bookingPage.Trim();
        var fragment = page.IndexOf('#');
        var hash = fragment < 0 ? "" : page[fragment..];
        if (fragment >= 0) page = page[..fragment];
        var join = page.Contains('?') ? (page.EndsWith('?') || page.EndsWith('&') ? "" : "&") : "?";
        return page + join + "invite=" + token + hash;
    }

    /// <summary>
    /// The invitation's payload when <paramref name="secret"/> signed it, compared in constant
    /// time; null for anything else. Expiry is the reader's to check.
    /// </summary>
    public static MeetingInvitationPayload? Read(string secret, string? token)
    {
        var parts = (token ?? "").Split('.');
        if (parts.Length != 2 || parts[0].Length == 0) return null;
        byte[] given, body;
        try
        {
            given = FromBase64Url(parts[1]);
            body = FromBase64Url(parts[0]);
        }
        catch (FormatException)
        {
            return null;
        }
        if (!CryptographicOperations.FixedTimeEquals(Sign(secret, parts[0]), given)) return null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var e = doc.RootElement;
            return new MeetingInvitationPayload(
                e.GetProperty("v").GetInt32(), e.GetProperty("o").GetGuid(), e.GetProperty("l").GetGuid(),
                e.GetProperty("n").GetString() ?? "", e.GetProperty("e").GetString() ?? "", e.GetProperty("c").GetString() ?? "",
                e.GetProperty("u").GetString() ?? "", e.GetProperty("x").GetInt64());
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    /// <summary>The payload's JSON, its keys in the order the website documents.</summary>
    internal static byte[] Json(MeetingInvitationPayload p)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteNumber("v", p.Version);
            json.WriteString("o", p.OrganizationId.ToString("D"));
            json.WriteString("l", p.LeadId.ToString("D"));
            json.WriteString("n", p.Name);
            json.WriteString("e", p.Email);
            json.WriteString("c", p.Code);
            json.WriteString("u", p.ApiBaseUrl);
            json.WriteNumber("x", p.ExpiresAt);
            json.WriteEndObject();
        }
        return stream.ToArray();
    }

    private static byte[] Sign(string secret, string first) =>
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.ASCII.GetBytes(first));

    /// <summary>RFC 4648 §5: '-' and '_' for '+' and '/', and no padding.</summary>
    public static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] FromBase64Url(string text)
    {
        var s = text.Replace('-', '+').Replace('_', '/');
        s += (s.Length % 4) switch { 2 => "==", 3 => "=", 0 => "", _ => throw new FormatException("Not base64url.") };
        return Convert.FromBase64String(s);
    }
}
