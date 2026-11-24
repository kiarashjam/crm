using System.Security.Cryptography;
using System.Text;

namespace ACI.Application.Common;

/// <summary>
/// The token in a member's reply link to an event invitation: random, sent once in the email,
/// and kept only as its SHA-256, as a contract's signing link is (<see cref="ContractSigningToken"/>).
/// </summary>
/// <remarks>
/// The link answers for one member about one event until the event starts, so a database copy
/// must not hand over the power to answer for anyone: only the hash is stored, and the token a
/// request brings is hashed and looked up, never compared in code.
/// </remarks>
public static class InvitationReplyToken
{
    /// <summary>Bytes of entropy, as a password reset's.</summary>
    private const int TokenBytes = 32;

    /// <summary>The length of a token this makes (unpadded base64url of 32 bytes).</summary>
    public const int Length = 43;

    /// <summary>A fresh URL-safe token. Put it in the email; never store it.</summary>
    public static string Create() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenBytes)).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    /// <summary>SHA-256 of the token, lowercase hex: what the database keeps and looks up.</summary>
    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()))).ToLowerInvariant();

    /// <summary>Whether text is shaped like a token this makes, before any database work.</summary>
    public static bool LooksLikeOne(string? token) =>
        token is { Length: Length } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
