using System.Collections.Concurrent;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ACI.Infrastructure.Services.SignIn;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ACI.WebApi.Tests.Helpers;

/// <summary>
/// A passkey authenticator in software: an ES256 key pair that answers WebAuthn options the way
/// a platform authenticator does ("none" attestation), producing the JSON the app's browser code
/// posts (binary fields base64url).
/// </summary>
/// <remarks>
/// It builds the bytes the specification lays down (authenticator data, the attestation object in
/// CTAP2 canonical CBOR, client data, a DER ECDSA signature), so the server's library verifies them
/// exactly as it would a phone's or a security key's.
/// </remarks>
public sealed class SoftwareAuthenticator
{
    private const byte UserPresent = 0x01, UserVerified = 0x04, BackupEligible = 0x08, BackedUp = 0x10, AttestedData = 0x40;

    public SoftwareAuthenticator(string rpId, string origin, ECDsa? key = null, byte[]? credentialId = null)
    {
        RpId = rpId;
        Origin = origin;
        Key = key ?? ECDsa.Create(ECCurve.NamedCurves.nistP256);
        CredentialId = credentialId ?? RandomNumberGenerator.GetBytes(32);
    }

    public string RpId { get; }
    public string Origin { get; }
    public ECDsa Key { get; }
    public byte[] CredentialId { get; }

    /// <summary>The user handle the relying party gave at registration (its user.id).</summary>
    public byte[] UserHandle { get; private set; } = [];

    public uint SignCount { get; set; }

    /// <summary>Whether it reports itself as a synced passkey (backup eligible and backed up).</summary>
    public bool Synced { get; set; }

    public string CredentialIdText => B64(CredentialId);

    /// <summary>Answers navigator.credentials.create with the server's options.</summary>
    public JsonObject Register(JsonElement options, string? origin = null, string? rpId = null)
    {
        var challenge = options.GetProperty("challenge").GetString()!;
        UserHandle = FromB64(options.GetProperty("user").GetProperty("id").GetString()!);

        var authData = AuthenticatorData(rpId ?? RpId, (byte)(UserPresent | UserVerified | AttestedData | (Synced ? BackupEligible | BackedUp : 0)), SignCount, attested: true);
        var writer = new CborWriter(CborConformanceMode.Ctap2Canonical);
        writer.WriteStartMap(3);
        writer.WriteTextString("fmt");
        writer.WriteTextString("none");
        writer.WriteTextString("attStmt");
        writer.WriteStartMap(0);
        writer.WriteEndMap();
        writer.WriteTextString("authData");
        writer.WriteByteString(authData);
        writer.WriteEndMap();

        return new JsonObject
        {
            ["id"] = CredentialIdText,
            ["rawId"] = CredentialIdText,
            ["type"] = "public-key",
            ["response"] = new JsonObject
            {
                ["attestationObject"] = B64(writer.Encode()),
                ["clientDataJSON"] = B64(ClientData("webauthn.create", challenge, origin ?? Origin)),
            },
        };
    }

    /// <summary>Answers navigator.credentials.get with the server's options; the counter rises by one unless given.</summary>
    public JsonObject Assert(JsonElement options, uint? signCount = null, bool userVerified = true, string? origin = null, ECDsa? signWith = null)
    {
        var challenge = options.GetProperty("challenge").GetString()!;
        SignCount = signCount ?? SignCount + 1;
        var flags = (byte)(UserPresent | (userVerified ? UserVerified : 0) | (Synced ? BackupEligible | BackedUp : 0));
        var authData = AuthenticatorData(RpId, flags, SignCount, attested: false);
        var clientData = ClientData("webauthn.get", challenge, origin ?? Origin);
        var signature = (signWith ?? Key).SignData([.. authData, .. SHA256.HashData(clientData)], HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);

        return new JsonObject
        {
            ["id"] = CredentialIdText,
            ["rawId"] = CredentialIdText,
            ["type"] = "public-key",
            ["response"] = new JsonObject
            {
                ["authenticatorData"] = B64(authData),
                ["clientDataJSON"] = B64(clientData),
                ["signature"] = B64(signature),
                ["userHandle"] = B64(UserHandle),
            },
        };
    }

    private byte[] AuthenticatorData(string rpId, byte flags, uint signCount, bool attested)
    {
        var data = new List<byte>(SHA256.HashData(Encoding.UTF8.GetBytes(rpId))) { flags };
        data.AddRange([(byte)(signCount >> 24), (byte)(signCount >> 16), (byte)(signCount >> 8), (byte)signCount]);
        if (!attested) return data.ToArray();

        data.AddRange(new byte[16]); // AAGUID: none given
        data.AddRange([(byte)(CredentialId.Length >> 8), (byte)CredentialId.Length]);
        data.AddRange(CredentialId);
        var point = Key.ExportParameters(false).Q;
        var cose = new CborWriter(CborConformanceMode.Ctap2Canonical);
        cose.WriteStartMap(5);
        cose.WriteInt32(1); cose.WriteInt32(2);   // kty: EC2
        cose.WriteInt32(3); cose.WriteInt32(-7);  // alg: ES256
        cose.WriteInt32(-1); cose.WriteInt32(1);  // crv: P-256
        cose.WriteInt32(-2); cose.WriteByteString(point.X!);
        cose.WriteInt32(-3); cose.WriteByteString(point.Y!);
        cose.WriteEndMap();
        data.AddRange(cose.Encode());
        return data.ToArray();
    }

    private static byte[] ClientData(string type, string challenge, string origin) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Dictionary<string, object> { ["type"] = type, ["challenge"] = challenge, ["origin"] = origin, ["crossOrigin"] = false }));

    public static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] FromB64(string text)
    {
        var s = text.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s + new string('=', (4 - s.Length % 4) % 4));
    }
}

/// <summary>Challenges a test chose, handed out in order before random ones: to replay recorded answers.</summary>
public sealed class QueuedChallenges : PasskeyChallengeSource
{
    public ConcurrentQueue<byte[]> Queue { get; } = new();

    public override byte[] Next() => Queue.TryDequeue(out var challenge) ? challenge : base.Next();
}

/// <summary>The clock moved on by <see cref="Ahead"/>: a session signed in now looks that old.</summary>
public sealed class ShiftedClock : TimeProvider
{
    public TimeSpan Ahead { get; set; }

    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + Ahead;
}

/// <summary>The app with passkeys on for https://app.cadence.test, and challenges a test can choose.</summary>
public sealed class PasskeyApp : CustomWebApplicationFactory
{
    public const string RpId = "app.cadence.test";
    public const string Origin = "https://app.cadence.test";

    public QueuedChallenges Challenges { get; } = new();

    public ShiftedClock Clock { get; } = new();

    public SoftwareAuthenticator NewAuthenticator() => new(RpId, Origin);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Passkeys:RelyingPartyId"] = RpId,
            ["Passkeys:Origins"] = Origin,
            ["Email:FrontendBaseUrl"] = Origin,
        }));
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<PasskeyChallengeSource>(Challenges);
            services.AddSingleton<TimeProvider>(Clock);
        });
    }
}
