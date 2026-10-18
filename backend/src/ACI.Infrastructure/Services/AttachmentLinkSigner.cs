using System.Globalization;
using System.Security.Cryptography;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace ACI.Infrastructure.Services;

/// <summary>
/// Download tokens: the attachment id and an expiry, encrypted and authenticated by
/// Data Protection, so neither can be read or altered. Two hours is long enough for
/// a page left open, short enough that a link copied out of the app soon stops working.
/// </summary>
public sealed class AttachmentLinkSigner : IAttachmentLinks
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(2);
    private readonly IDataProtector _protector;
    private readonly TimeProvider _clock;

    public AttachmentLinkSigner(IDataProtectionProvider provider, TimeProvider? clock = null)
    {
        _protector = provider.CreateProtector("ACI.Attachments.Download.v1");
        _clock = clock ?? TimeProvider.System;
    }

    public string CreateToken(Guid attachmentId)
    {
        var expires = _clock.GetUtcNow().Add(Lifetime).ToUnixTimeSeconds();
        return _protector.Protect($"{attachmentId:N}|{expires.ToString(CultureInfo.InvariantCulture)}");
    }

    public bool IsValid(Guid attachmentId, string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        try
        {
            var parts = _protector.Unprotect(token).Split('|');
            return parts.Length == 2
                && parts[0] == attachmentId.ToString("N")
                && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var expires)
                && _clock.GetUtcNow().ToUnixTimeSeconds() < expires;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }
}
