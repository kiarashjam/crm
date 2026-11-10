using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace ACI.Infrastructure.Services.Payments;

/// <summary>What a pay link is for: whose account, what it pays, and until when it works.</summary>
public sealed record PayLinkClaim(Guid OrganizationId, Guid MemberId, string Purpose, IReadOnlyList<Guid> ChargeIds, bool AutoPay, DateTime ExpiresAtUtc);

/// <summary>
/// Pay links: the workspace, the member and what to pay, encrypted and authenticated by Data
/// Protection (the key ring that keeps two-factor secrets), with an end date inside. Nobody
/// can make one for another account, change what it pays, or use one past its date.
/// </summary>
/// <remarks>
/// The link opens Cadence's own page, not Stripe's: Stripe's pages last a day at most, and a
/// link in a statement email has to work for weeks. The amount is worked out when the member
/// presses Pay, so a link sent before a payment never asks for money already paid.
/// </remarks>
public sealed class PayLinkSigner
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IDataProtector _protector;
    private readonly TimeProvider _clock;

    public PayLinkSigner(IDataProtectionProvider provider, TimeProvider? clock = null)
    {
        _protector = provider.CreateProtector("ACI.Payments.PayLink.v1");
        _clock = clock ?? TimeProvider.System;
    }

    public string Create(PayLinkClaim claim) => _protector.Protect(JsonSerializer.Serialize(claim, Json));

    public bool TryRead(string? token, out PayLinkClaim? claim)
    {
        claim = null;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2000) return false;
        try
        {
            var read = JsonSerializer.Deserialize<PayLinkClaim>(_protector.Unprotect(token.Trim()), Json);
            if (read == null || read.ExpiresAtUtc <= _clock.GetUtcNow().UtcDateTime) return false;
            claim = read;
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException)
        {
            return false;
        }
    }
}
