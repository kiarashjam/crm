using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Configuration;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Infrastructure.Services.SignIn;

/// <summary>Where passkey challenges come from: random, except in a test replaying recorded answers.</summary>
public class PasskeyChallengeSource
{
    public virtual byte[] Next() => RandomNumberGenerator.GetBytes(32);
}

/// <summary>
/// Passkeys (see <see cref="IPasskeys"/>). Each ceremony's options are kept server-side under a
/// one-time id (AuthChallenges) and the browser's answer is verified by Fido2NetLib against
/// them: the challenge, the origin, the relying party, the signature, and on sign-in that the
/// person was verified on the device.
/// </summary>
/// <remarks>
/// <para>
/// A passkey that verified its user (PIN, fingerprint, face) is two factors at once, so signing
/// in with one gives the session without the authenticator code; where 2FA is on, a passkey
/// can stand in for the code after the password or single sign-on.
/// </para>
/// <para>
/// The signature counter is checked here rather than by the library, whose check (3.x) lets a
/// counter fall back to 0 through: a counter that does not rise, when either value is not 0,
/// means the key may have been copied (WebAuthn §7.2 step 21), and the sign-in is refused.
/// Passkeys that sync between devices report 0 every time and pass. The new value is saved
/// with the old one as a concurrency token, so two sign-ins racing with one value cannot
/// both win.
/// </para>
/// <para>
/// Fido2NetLib 3.0.1 rather than 4.x: 4.x needs Microsoft.Extensions.* 9.0, which would mean
/// raising five package references the whole API shares; 3.0.1 targets .NET 6 and runs on 8
/// with every existing package as it is. Only ES256 and RS256 keys are asked for, which every
/// platform authenticator and security key offers; EdDSA would load a native library.
/// </para>
/// </remarks>
public sealed class PasskeyService : IPasskeys
{
    private const int MaxPerAccount = 20;
    private static readonly TimeSpan CeremonyLifetime = TimeSpan.FromMinutes(5);

    /// <summary>An account without a password proves it is the person's with a sign-in this recent.</summary>
    public static readonly TimeSpan RecentSignIn = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db;
    private readonly IAuthChallenges _challenges;
    private readonly SignInSessions _sessions;
    private readonly ITokenService _tokens;
    private readonly IPasswordHasher _passwords;
    private readonly AuthAttemptLimiter _attempts;
    private readonly PasskeyChallengeSource _challengeSource;
    private readonly TimeProvider _clock;
    private readonly ILogger<PasskeyService> _logger;
    private readonly Fido2Configuration? _config;

    public PasskeyService(
        AppDbContext db,
        IAuthChallenges challenges,
        SignInSessions sessions,
        ITokenService tokens,
        IPasswordHasher passwords,
        AuthAttemptLimiter attempts,
        PasskeyChallengeSource challengeSource,
        IOptions<PasskeySettings> settings,
        IOptions<EmailSettings> email,
        ILogger<PasskeyService> logger,
        TimeProvider? clock = null)
    {
        _db = db;
        _challenges = challenges;
        _sessions = sessions;
        _tokens = tokens;
        _passwords = passwords;
        _attempts = attempts;
        _challengeSource = challengeSource;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
        _config = Configure(settings.Value, email.Value.FrontendBaseUrl);
    }

    public bool IsConfigured => _config != null;

    public static string AttemptKey(Guid userId) => "passkey:" + userId;

    public async Task<IReadOnlyList<PasskeyDto>> ListAsync(Guid userId, CancellationToken ct = default) =>
        await _db.UserPasskeys.AsNoTracking()
            .Where(k => k.UserId == userId)
            .OrderBy(k => k.CreatedAtUtc)
            .Select(k => new PasskeyDto(k.Id, k.Name, k.CreatedAtUtc, k.LastUsedAtUtc, k.IsBackedUp))
            .ToListAsync(ct);

    public async Task<Result<PasskeyCeremonyDto>> RegistrationOptionsAsync(Guid userId, PasskeyRegistrationOptionsRequest request, DateTime? sessionIssuedAtUtc, CancellationToken ct = default)
    {
        if (_config == null) return DomainErrors.Passkeys.NotConfigured;
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return DomainErrors.Auth.EmailNotFound;

        // A passkey is a way in, and with it the authenticator code is not asked for: someone
        // holding a stolen session must not be able to add one of their own. So, as for turning
        // 2FA on, the password; an account without one (made by single sign-on) needs a session
        // that began moments ago instead.
        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            if (string.IsNullOrEmpty(request.Password)) return DomainErrors.Passkeys.PasswordRequired;
            var key = AuthAttemptLimiter.Login(user.Email);
            if (_attempts.IsBlocked(key)) return DomainErrors.Auth.AccountLocked;
            if (!_passwords.Verify(request.Password, user.PasswordHash))
            {
                _attempts.Record(key);
                return DomainErrors.Auth.WrongPassword;
            }
            _attempts.Reset(key);
        }
        else if (sessionIssuedAtUtc is not { } issued || _clock.GetUtcNow().UtcDateTime - issued > RecentSignIn)
        {
            return DomainErrors.Passkeys.RecentSignInRequired;
        }

        var existing = await _db.UserPasskeys.Where(k => k.UserId == userId).Select(k => k.CredentialId).ToListAsync(ct);
        if (existing.Count >= MaxPerAccount) return DomainErrors.Passkeys.TooMany;

        var options = new Fido2(_config).RequestNewCredential(
            new Fido2User { Id = user.Id.ToByteArray(), Name = user.Email, DisplayName = user.Name },
            existing.Select(id => new PublicKeyCredentialDescriptor(id)).ToList(),
            // A passkey proper: kept on the authenticator (so it can sign in without a name
            // typed first) and unlocked by the person's PIN or biometrics.
            new AuthenticatorSelection { RequireResidentKey = true, UserVerification = UserVerificationRequirement.Required },
            AttestationConveyancePreference.None,
            extensions: null);
        options.Challenge = _challengeSource.Next();
        options.PubKeyCredParams = [new PubKeyCredParam(COSE.Algorithm.ES256, PublicKeyCredentialType.PublicKey), new PubKeyCredParam(COSE.Algorithm.RS256, PublicKeyCredentialType.PublicKey)];

        return await CeremonyAsync(AuthChallengePurposes.PasskeyRegistration, userId, options.ToJson(), ct);
    }

    public async Task<Result<PasskeyDto>> RegisterAsync(Guid userId, PasskeyRegistrationRequest request, CancellationToken ct = default)
    {
        if (_config == null) return DomainErrors.Passkeys.NotConfigured;
        var step = await _challenges.TakeAsync(AuthChallengePurposes.PasskeyRegistration, request.CeremonyId, ct);
        if (step == null || step.UserId != userId) return DomainErrors.Passkeys.CeremonyExpired;

        var answer = Read<AuthenticatorAttestationRawResponse>(request.Credential);
        if (answer?.Response == null) return DomainErrors.Passkeys.RegistrationFailed;

        Fido2.CredentialMakeResult made;
        try
        {
            made = await new Fido2(_config).MakeNewCredentialAsync(
                answer,
                CredentialCreateOptions.FromJson(step.Payload),
                async (args, token) => !await _db.UserPasskeys.AnyAsync(k => k.CredentialId == args.CredentialId, token),
                requestTokenBindingId: null,
                ct);
        }
        catch (Fido2VerificationException ex)
        {
            _logger.LogWarning("A passkey for user {UserId} was refused: {Reason}", userId, ex.Message);
            return DomainErrors.Passkeys.RegistrationFailed;
        }
        if (made.Result is not { } credential || made.Status != "ok")
        {
            _logger.LogWarning("A passkey for user {UserId} was refused: {Reason}", userId, made.ErrorMessage);
            return DomainErrors.Passkeys.RegistrationFailed;
        }

        var flags = Flags(AuthenticatorDataOf(answer.Response.AttestationObject));
        var name = (request.Name ?? "").Trim();
        var passkey = new UserPasskey
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CredentialId = credential.CredentialId,
            PublicKey = credential.PublicKey,
            SignCount = credential.Counter,
            Aaguid = credential.Aaguid,
            Name = name.Length == 0 ? "Passkey" : name.Length > 60 ? name[..60] : name,
            IsBackupEligible = (flags & BackupEligible) != 0,
            IsBackedUp = (flags & BackedUp) != 0,
            CreatedAtUtc = _clock.GetUtcNow().UtcDateTime,
        };
        _db.UserPasskeys.Add(passkey);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // The same authenticator registered twice at once: the unique index keeps one.
            return DomainErrors.Passkeys.AlreadyAdded;
        }

        _logger.LogInformation("User {UserId} added a passkey {PasskeyId}", userId, passkey.Id);
        return new PasskeyDto(passkey.Id, passkey.Name, passkey.CreatedAtUtc, null, passkey.IsBackedUp);
    }

    public async Task<Result> RemoveAsync(Guid userId, Guid passkeyId, CancellationToken ct = default)
    {
        var passkey = await _db.UserPasskeys.FirstOrDefaultAsync(k => k.Id == passkeyId && k.UserId == userId, ct);
        if (passkey == null) return DomainErrors.Passkeys.NotFound;
        _db.UserPasskeys.Remove(passkey);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("User {UserId} removed passkey {PasskeyId}", userId, passkeyId);
        return Result.Success();
    }

    public async Task<Result<PasskeyCeremonyDto>> SignInOptionsAsync(CancellationToken ct = default)
    {
        if (_config == null) return DomainErrors.Passkeys.NotConfigured;
        // No list of credentials: the browser offers whichever passkeys it has for this site,
        // so nothing here says whether an address has an account.
        var options = new Fido2(_config).GetAssertionOptions([], UserVerificationRequirement.Required, extensions: null);
        options.Challenge = _challengeSource.Next();
        return await CeremonyAsync(AuthChallengePurposes.PasskeySignIn, null, options.ToJson(), ct);
    }

    public async Task<Result<LoginResponse>> SignInAsync(PasskeyAssertionRequest request, CancellationToken ct = default)
    {
        if (_config == null) return DomainErrors.Passkeys.NotConfigured;
        var step = await _challenges.TakeAsync(AuthChallengePurposes.PasskeySignIn, request.CeremonyId, ct);
        if (step == null) return DomainErrors.Passkeys.CeremonyExpired;

        var answer = Read<AuthenticatorAssertionRawResponse>(request.Credential);
        if (answer?.Response == null || answer.RawId == null) return DomainErrors.Passkeys.NotRecognised;
        var passkey = await _db.UserPasskeys.Include(k => k.User).FirstOrDefaultAsync(k => k.CredentialId == answer.RawId, ct);
        if (passkey == null) return DomainErrors.Passkeys.NotRecognised;

        // As many wrong tries as a password gets (AuthAttemptLimiter), per account.
        var attempts = AttemptKey(passkey.UserId);
        if (_attempts.IsBlocked(attempts)) return DomainErrors.Auth.AccountLocked;

        var checkedAnswer = await VerifyAsync(answer, step.Payload, passkey, ct);
        if (checkedAnswer.IsFailure)
        {
            _attempts.Record(attempts);
            return checkedAnswer.Error;
        }
        _attempts.Reset(attempts);
        return await _sessions.IssueAsync(passkey.User, SignInMethods.Passkey, secondFactorDone: true, ct);
    }

    public async Task<Result<PasskeyCeremonyDto>> SecondFactorOptionsAsync(PasskeySecondFactorOptionsRequest request, CancellationToken ct = default)
    {
        if (_config == null) return DomainErrors.Passkeys.NotConfigured;
        if (_tokens.ValidateTwoFactorTokenAndGetUserId(request.TwoFactorToken) is not { } userId) return DomainErrors.Auth.InvalidTwoFactorCode;

        var credentials = await _db.UserPasskeys.Where(k => k.UserId == userId).Select(k => k.CredentialId).ToListAsync(ct);
        if (credentials.Count == 0) return DomainErrors.Passkeys.NoneForAccount;

        // The person is known from the step token, so only their passkeys are offered; the
        // password or single sign-on was the first factor, so the device need not verify them.
        var options = new Fido2(_config).GetAssertionOptions(
            credentials.Select(id => new PublicKeyCredentialDescriptor(id)).ToList(),
            UserVerificationRequirement.Preferred,
            extensions: null);
        options.Challenge = _challengeSource.Next();
        return await CeremonyAsync(AuthChallengePurposes.PasskeySecondFactor, userId, options.ToJson(), ct);
    }

    public async Task<Result<LoginResponse>> SecondFactorAsync(PasskeySecondFactorRequest request, CancellationToken ct = default)
    {
        if (_config == null) return DomainErrors.Passkeys.NotConfigured;
        if (_tokens.ValidateTwoFactorTokenAndGetUserId(request.TwoFactorToken) is not { } userId) return DomainErrors.Auth.InvalidTwoFactorCode;

        // The same allowance as the authenticator code: one for the second step, whichever is used.
        var attempts = AuthAttemptLimiter.TwoFactor(userId);
        if (_attempts.IsBlocked(attempts)) return DomainErrors.Auth.AccountLocked;

        var step = await _challenges.TakeAsync(AuthChallengePurposes.PasskeySecondFactor, request.CeremonyId, ct);
        if (step == null || step.UserId != userId) return DomainErrors.Passkeys.CeremonyExpired;

        var answer = Read<AuthenticatorAssertionRawResponse>(request.Credential);
        var passkey = answer?.RawId == null
            ? null
            : await _db.UserPasskeys.Include(k => k.User).FirstOrDefaultAsync(k => k.UserId == userId && k.CredentialId == answer.RawId, ct);
        if (answer?.Response == null || passkey == null || !passkey.User.TwoFactorEnabled)
        {
            _attempts.Record(attempts);
            return DomainErrors.Passkeys.NotRecognised;
        }

        var checkedAnswer = await VerifyAsync(answer, step.Payload, passkey, ct);
        if (checkedAnswer.IsFailure)
        {
            _attempts.Record(attempts);
            return checkedAnswer.Error;
        }
        _attempts.Reset(attempts);
        var method = _tokens.TwoFactorSignInMethod(request.TwoFactorToken) ?? SignInMethods.Password;
        return await _sessions.IssueAsync(passkey.User, method, secondFactorDone: true, ct);
    }

    /// <summary>Checks a passkey's answer against the options it was asked with, then its counter, and records the use.</summary>
    private async Task<Result> VerifyAsync(AuthenticatorAssertionRawResponse answer, string optionsJson, UserPasskey passkey, CancellationToken ct)
    {
        AssertionVerificationResult verified;
        try
        {
            verified = await new Fido2(_config!).MakeAssertionAsync(
                answer,
                AssertionOptions.FromJson(optionsJson),
                passkey.PublicKey,
                // 0: the counter is checked below, completely (see the remarks on the class).
                storedSignatureCounter: 0,
                (args, _) => Task.FromResult(
                    args.UserHandle.AsSpan().SequenceEqual(passkey.UserId.ToByteArray())
                    && args.CredentialId.AsSpan().SequenceEqual(passkey.CredentialId)),
                requestTokenBindingId: null,
                ct);
        }
        catch (Fido2VerificationException ex)
        {
            _logger.LogWarning("Refused passkey {PasskeyId} of user {UserId}: {Reason}", passkey.Id, passkey.UserId, ex.Message);
            return DomainErrors.Passkeys.NotRecognised;
        }
        if (verified.Status != "ok")
        {
            _logger.LogWarning("Refused passkey {PasskeyId} of user {UserId}: {Reason}", passkey.Id, passkey.UserId, verified.ErrorMessage);
            return DomainErrors.Passkeys.NotRecognised;
        }

        long reported = verified.Counter;
        if ((passkey.SignCount > 0 || reported > 0) && reported <= passkey.SignCount)
        {
            _logger.LogWarning(
                "Refused passkey {PasskeyId} of user {UserId}: its counter went from {Stored} to {Reported}, as a copied key's would",
                passkey.Id, passkey.UserId, passkey.SignCount, reported);
            return DomainErrors.Passkeys.CounterWentBack;
        }

        passkey.SignCount = reported;
        passkey.LastUsedAtUtc = _clock.GetUtcNow().UtcDateTime;
        passkey.IsBackedUp = answer.Response.AuthenticatorData is { Length: > 32 } data && (data[32] & BackedUp) != 0;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another sign-in moved the counter from the same value first.
            _logger.LogWarning("Refused passkey {PasskeyId} of user {UserId}: another sign-in used the same counter", passkey.Id, passkey.UserId);
            return DomainErrors.Passkeys.CounterWentBack;
        }
        return Result.Success();
    }

    private async Task<Result<PasskeyCeremonyDto>> CeremonyAsync(string purpose, Guid? userId, string optionsJson, CancellationToken ct)
    {
        var ceremonyId = AuthChallengeStore.NewSecret();
        await _challenges.CreateAsync(purpose, ceremonyId, userId, optionsJson, CeremonyLifetime, ct);
        using var options = JsonDocument.Parse(optionsJson);
        return new PasskeyCeremonyDto(ceremonyId, options.RootElement.Clone());
    }

    private static T? Read<T>(JsonElement json) where T : class
    {
        try
        {
            return json.ValueKind == JsonValueKind.Object ? JsonSerializer.Deserialize<T>(json.GetRawText()) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    // Authenticator data flags (WebAuthn §6.1): backup eligible, backed up.
    private const byte BackupEligible = 0x08;
    private const byte BackedUp = 0x10;

    private static byte Flags(byte[]? authenticatorData) =>
        authenticatorData is { Length: > 32 } ? authenticatorData[32] : (byte)0;

    /// <summary>The authenticator data inside an attestation object (CBOR map, key "authData"), read once the library has verified it.</summary>
    private static byte[]? AuthenticatorDataOf(byte[]? attestationObject)
    {
        if (attestationObject == null) return null;
        try
        {
            var reader = new CborReader(attestationObject);
            var entries = reader.ReadStartMap();
            for (var i = 0; entries == null || i < entries; i++)
            {
                if (reader.PeekState() == CborReaderState.EndMap) break;
                var key = reader.ReadTextString();
                if (key == "authData") return reader.ReadByteString();
                reader.SkipValue();
            }
        }
        catch (Exception ex) when (ex is CborContentException or InvalidOperationException)
        {
            // Only the flags come from here, and the library has already accepted the object.
        }
        return null;
    }

    /// <summary>The relying party and origins, from Passkeys or else Email:FrontendBaseUrl; null when they do not fit together.</summary>
    private Fido2Configuration? Configure(PasskeySettings settings, string? frontendBaseUrl)
    {
        var origins = (string.IsNullOrWhiteSpace(settings.Origins) ? frontendBaseUrl ?? "" : settings.Origins)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(o => Uri.TryCreate(o, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
                ? uri.GetLeftPart(UriPartial.Authority)
                : null)
            .ToList();
        if (origins.Count == 0 || origins.Any(o => o == null))
        {
            if (!string.IsNullOrWhiteSpace(settings.Origins) || !string.IsNullOrWhiteSpace(settings.RelyingPartyId))
                _logger.LogError("Passkeys are off: Passkeys:Origins must be https origins (http only for localhost)");
            return null;
        }

        var rpId = string.IsNullOrWhiteSpace(settings.RelyingPartyId)
            ? new Uri(origins[0]!).Host
            : settings.RelyingPartyId.Trim().ToLowerInvariant();
        // WebAuthn: the relying party must be each origin's host or a domain it is under.
        if (origins.Any(o => new Uri(o!).Host is var host && host != rpId && !host.EndsWith("." + rpId, StringComparison.Ordinal)))
        {
            _logger.LogError("Passkeys are off: Passkeys:RelyingPartyId {RelyingPartyId} is not the host of every origin, nor a domain they are under", rpId);
            return null;
        }

        return new Fido2Configuration
        {
            ServerDomain = rpId,
            ServerName = string.IsNullOrWhiteSpace(settings.RelyingPartyName) ? "Cadence" : settings.RelyingPartyName.Trim(),
            Origins = origins.Select(o => o!).ToHashSet(StringComparer.Ordinal),
            TimestampDriftTolerance = 300_000,
        };
    }
}
