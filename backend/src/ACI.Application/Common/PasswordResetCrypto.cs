using System.Security.Cryptography;
using System.Text;

namespace ACI.Application.Common;

/// <summary>
/// URL-safe reset tokens and SHA-256 hashes for persistence.
/// </summary>
public static class PasswordResetCrypto
{
    public static string CreateRawToken()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string HashRawToken(string rawToken)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(rawToken)));
    }
}
