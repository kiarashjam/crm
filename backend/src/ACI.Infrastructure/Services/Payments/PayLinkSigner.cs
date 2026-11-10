using System.Buffers.Binary;
using System.Security.Cryptography;
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
/// <para>
/// The link opens Cadence's own page, not Stripe's: Stripe's pages last a day at most, and a
/// link in a statement email has to work for weeks. The amount is worked out when the member
/// presses Pay, so a link sent before a payment never asks for money already paid.
/// </para>
/// <para>
/// The claim is packed as bytes (ids as their 16 bytes, the date as seconds) rather than as
/// JSON, which made a link for what is owed nearly 400 characters and one for 50 charges about
/// 3,000: past what mail programs make clickable. Packed, they are about 180 and 1,250.
/// </para>
/// </remarks>
public sealed class PayLinkSigner
{
    /// <summary>The first byte of a packed claim, so a later layout can be told apart.</summary>
    private const byte Layout = 1;

    /// <summary>Version, workspace, member, purpose, flags and the end date, before the charges.</summary>
    private const int HeaderBytes = 1 + 16 + 16 + 1 + 1 + 8;

    /// <summary>The purposes a link is for, by the number a packed claim keeps.</summary>
    private static readonly string[] Purposes = [ClubPaymentsService.Account, ClubPaymentsService.Charges, ClubPaymentsService.Setup];

    /// <summary>
    /// The longest token read: a link for the most charges staff may tick (50) is about 1,250
    /// characters, and anything far longer is not one of ours.
    /// </summary>
    public const int MaxTokenLength = 2048;

    private readonly IDataProtector _protector;
    private readonly TimeProvider _clock;

    public PayLinkSigner(IDataProtectionProvider provider, TimeProvider? clock = null)
    {
        _protector = provider.CreateProtector("ACI.Payments.PayLink.v2");
        _clock = clock ?? TimeProvider.System;
    }

    public string Create(PayLinkClaim claim)
    {
        var purpose = Array.IndexOf(Purposes, claim.Purpose);
        if (purpose < 0) throw new ArgumentException($"A pay link is for {string.Join(", ", Purposes)}.", nameof(claim));
        var bytes = new byte[HeaderBytes + 16 * claim.ChargeIds.Count];
        bytes[0] = Layout;
        claim.OrganizationId.TryWriteBytes(bytes.AsSpan(1, 16));
        claim.MemberId.TryWriteBytes(bytes.AsSpan(17, 16));
        bytes[33] = (byte)purpose;
        bytes[34] = (byte)(claim.AutoPay ? 1 : 0);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(35, 8), new DateTimeOffset(DateTime.SpecifyKind(claim.ExpiresAtUtc, DateTimeKind.Utc)).ToUnixTimeSeconds());
        for (var i = 0; i < claim.ChargeIds.Count; i++) claim.ChargeIds[i].TryWriteBytes(bytes.AsSpan(HeaderBytes + 16 * i, 16));
        return ToBase64Url(_protector.Protect(bytes));
    }

    public bool TryRead(string? token, out PayLinkClaim? claim)
    {
        claim = null;
        if (string.IsNullOrWhiteSpace(token) || token.Length > MaxTokenLength) return false;
        try
        {
            var bytes = _protector.Unprotect(FromBase64Url(token.Trim()));
            if (bytes.Length < HeaderBytes || bytes[0] != Layout || (bytes.Length - HeaderBytes) % 16 != 0 || bytes[33] >= Purposes.Length) return false;
            var expires = DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(35, 8))).UtcDateTime;
            if (expires <= _clock.GetUtcNow().UtcDateTime) return false;
            var charges = new List<Guid>();
            for (var offset = HeaderBytes; offset < bytes.Length; offset += 16) charges.Add(new Guid(bytes.AsSpan(offset, 16)));
            claim = new PayLinkClaim(new Guid(bytes.AsSpan(1, 16)), new Guid(bytes.AsSpan(17, 16)), Purposes[bytes[33]], charges, bytes[34] == 1, expires);
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    /// <summary>Base64 as a URL carries it: '-' and '_' for '+' and '/', no padding.</summary>
    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string text)
    {
        var base64 = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
    }
}
