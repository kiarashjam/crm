using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace ACI.Infrastructure.Services.Pos;

/// <summary>
/// The OAuth state that goes to Lightspeed and comes back with the browser: which workspace is
/// connecting and who started it, encrypted and authenticated by Data Protection, with an end
/// date inside. Connecting is finished only by that person, signed in, in that workspace
/// (IClubPos.CompleteConnectAsync compares the two).
/// </summary>
/// <remarks>
/// Without it, a code from anyone's Lightspeed account could be connected to any workspace,
/// whose members' accounts its tickets would then be posted to.
/// </remarks>
public sealed class PosConnectState
{
    /// <summary>The first byte of a packed state, so a later layout can be told apart.</summary>
    private const byte Layout = 1;

    private const int Bytes = 1 + 16 + 16 + 8 + 8;

    /// <summary>How long someone has to finish connecting on Lightspeed's page.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private readonly IDataProtector _protector;
    private readonly TimeProvider _clock;

    public PosConnectState(IDataProtectionProvider provider, TimeProvider? clock = null)
    {
        _protector = provider.CreateProtector("ACI.Pos.Connect.v1");
        _clock = clock ?? TimeProvider.System;
    }

    public string Create(Guid organizationId, Guid userId)
    {
        var bytes = new byte[Bytes];
        bytes[0] = Layout;
        organizationId.TryWriteBytes(bytes.AsSpan(1, 16));
        userId.TryWriteBytes(bytes.AsSpan(17, 16));
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(33, 8), _clock.GetUtcNow().Add(Lifetime).ToUnixTimeSeconds());
        RandomNumberGenerator.Fill(bytes.AsSpan(41, 8));
        return Convert.ToBase64String(_protector.Protect(bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public bool TryRead(string? state, out Guid organizationId, out Guid userId)
    {
        organizationId = Guid.Empty;
        userId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(state) || state.Length > 1024) return false;
        try
        {
            var base64 = state.Trim().Replace('-', '+').Replace('_', '/');
            var bytes = _protector.Unprotect(Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=')));
            if (bytes.Length != Bytes || bytes[0] != Layout) return false;
            var expires = DateTimeOffset.FromUnixTimeSeconds(BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(33, 8)));
            if (expires <= _clock.GetUtcNow()) return false;
            organizationId = new Guid(bytes.AsSpan(1, 16));
            userId = new Guid(bytes.AsSpan(17, 16));
            return true;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
