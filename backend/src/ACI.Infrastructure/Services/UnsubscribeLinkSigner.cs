using System.Security.Cryptography;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace ACI.Infrastructure.Services;

/// <summary>
/// Unsubscribe tokens: the workspace and the address, encrypted and authenticated
/// by Data Protection, the same key ring that keeps two-factor secrets. Nobody can
/// make one for an address they were not sent, and the address does not show in
/// the link. No expiry, on purpose.
/// </summary>
public sealed class UnsubscribeLinkSigner : IUnsubscribeLinks
{
    private readonly IDataProtector _protector;

    public UnsubscribeLinkSigner(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector("ACI.Email.Unsubscribe.v1");

    public string CreateToken(Guid organizationId, string email) =>
        _protector.Protect($"{organizationId:N}|{email.Trim().ToLowerInvariant()}");

    public bool TryRead(string token, out Guid organizationId, out string email)
    {
        organizationId = Guid.Empty;
        email = "";
        if (string.IsNullOrWhiteSpace(token)) return false;
        try
        {
            var parts = _protector.Unprotect(token.Trim()).Split('|', 2);
            if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out organizationId) || parts[1].Length == 0) return false;
            email = parts[1];
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return false;
        }
    }
}
