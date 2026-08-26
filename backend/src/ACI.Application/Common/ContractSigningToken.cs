using System.Security.Cryptography;

namespace ACI.Application.Common;

/// <summary>
/// The one-time token in a contract signing link.
/// </summary>
/// <remarks>
/// <para>
/// Modelled on <see cref="PasswordResetCrypto"/>, deliberately, and NOT on the
/// invite token. Invites use <c>Guid.NewGuid()</c> stored in plaintext; a guid is
/// not a CSPRNG output and a plaintext column means a database leak hands over the
/// ability to accept invitations. For a link that can sign a contract, neither is
/// acceptable: the raw token is generated from <see cref="RandomNumberGenerator"/>,
/// emailed, and never stored — only its SHA-256 hash is.
/// </para>
/// <para>
/// Comparison happens by hashing the incoming token and looking that up, so no
/// secret is ever compared byte by byte in application code.
/// </para>
/// </remarks>
public static class ContractSigningToken
{
    /// <summary>Bytes of entropy. 32 is the same as the password-reset token.</summary>
    private const int TokenBytes = 32;

    /// <summary>
    /// How long a signing link stays valid.
    /// </summary>
    /// <remarks>
    /// Far longer than a password reset's hour, because a contract genuinely does
    /// sit in an inbox over a weekend or a holiday, and a link that dies before
    /// the counterparty gets to it turns into a support conversation. Still
    /// bounded: an indefinite signing link is a standing invitation to sign a
    /// document whose terms may have moved on.
    /// </remarks>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

    /// <summary>
    /// How long a link stays usable after the contract is finished.
    /// </summary>
    /// <remarks>
    /// Declined and countersigned are terminal: the state machine permits nothing
    /// from them, including <c>void</c>, which is the only thing that kills a link.
    /// So a link to a contract that had been declined stayed live for the rest of its
    /// thirty days, exposing the full text of a dead agreement at an anonymous URL
    /// that could not be revoked by any means the product offered.
    ///
    /// Cutting it to zero would be tidier and worse: somebody clicking the bookmark
    /// they signed from would get "this link is not valid" instead of "you have
    /// already signed this", which reads like a fault. A week is long enough to cover
    /// that, short enough to bound the exposure, and the executed PDF they were
    /// emailed is the copy they should be keeping anyway.
    ///
    /// While the contract is still live — sent, or signed and awaiting our
    /// countersignature — a leaked link is revoked by voiding the contract, which the
    /// state machine does permit from both.
    /// </remarks>
    public static readonly TimeSpan GraceAfterCompletion = TimeSpan.FromDays(7);

    /// <summary>A fresh URL-safe token. Return it to the caller once; never store it.</summary>
    public static string CreateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenBytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    /// <summary>SHA-256 of the raw token, lowercase hex. What goes in the database.</summary>
    public static string HashRawToken(string rawToken)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(rawToken);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    /// <summary>
    /// SHA-256 of a contract body, lowercase hex — the tamper-evidence stamp taken
    /// at send time and never recomputed afterwards.
    /// </summary>
    public static string HashBody(string body)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(body ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
