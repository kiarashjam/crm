using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Configuration;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ACI.Infrastructure.Services.SignIn;

/// <summary>
/// Single sign-on with Google and Microsoft: OpenID Connect's authorization code flow with
/// PKCE, done by hand against each provider's published endpoints (see <see cref="ISingleSignOn"/>).
/// </summary>
/// <remarks>
/// <para>
/// Hand-rolled rather than ASP.NET Core's OpenIdConnect handler: the handler signs people
/// in to a cookie, and this API has no cookie sessions, only the bearer token the app keeps.
/// Using it would mean adding a cookie scheme next to the bearer one just to carry the result
/// a few seconds, and its correlation cookies would have to cross from the API's domain to the
/// app's. The flow itself is short, and the parts that are easy to get wrong are left to
/// maintained code: the id token's signature, issuer, audience and lifetime are checked by
/// Microsoft.IdentityModel's JsonWebTokenHandler against the provider's published keys.
/// </para>
/// <para>
/// The round trip: StartAsync keeps a state, a nonce and a PKCE verifier under the state, and
/// sends the browser to the provider. The provider sends it back with a code; the state finds
/// the round trip (once), the code is redeemed with the verifier and the client secret, and
/// the id token must carry the nonce. The browser then gets a one-time code in the address
/// fragment, which the app trades, with the secret whose hash it sent at the start, for the
/// session: a code copied into another browser, or a sign-on finished in someone else's
/// browser, is worth nothing there.
/// </para>
/// </remarks>
public sealed class SingleSignOnService : ISingleSignOn
{
    private static readonly TimeSpan RoundTripLifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan HandoffLifetime = TimeSpan.FromMinutes(2);

    private readonly OidcProviderCatalog _catalog;
    private readonly OidcMetadataSource _metadata;
    private readonly IHttpClientFactory _http;
    private readonly IAuthChallenges _challenges;
    private readonly AppDbContext _db;
    private readonly SignInSessions _sessions;
    private readonly ISecurityStampCache? _stamps;
    private readonly ILogger<SingleSignOnService> _logger;
    private readonly string? _apiBase;
    private readonly string? _appBase;

    public SingleSignOnService(
        OidcProviderCatalog catalog,
        OidcMetadataSource metadata,
        IHttpClientFactory http,
        IAuthChallenges challenges,
        AppDbContext db,
        SignInSessions sessions,
        IOptions<SingleSignOnSettings> sso,
        IOptions<EmailSettings> email,
        ILogger<SingleSignOnService> logger,
        ISecurityStampCache? stamps = null)
    {
        _catalog = catalog;
        _metadata = metadata;
        _http = http;
        _challenges = challenges;
        _db = db;
        _sessions = sessions;
        _stamps = stamps;
        _logger = logger;
        _apiBase = AbsoluteBase(string.IsNullOrWhiteSpace(sso.Value.ApiBaseUrl) ? email.Value.ApiBaseUrl : sso.Value.ApiBaseUrl);
        _appBase = AbsoluteBase(email.Value.FrontendBaseUrl);
    }

    // Without both addresses the providers cannot send anyone back, nor this API on to the app.
    public IReadOnlyList<SsoProviderDto> Providers =>
        _apiBase == null || _appBase == null
            ? []
            : _catalog.All.Select(p => new SsoProviderDto(p.Id, p.Name)).ToList();

    private OidcProvider? Offered(string provider) =>
        _apiBase == null || _appBase == null ? null : _catalog.Find(provider);

    public async Task<Result<string>> StartAsync(string provider, string clientChallenge, CancellationToken ct = default)
    {
        if (Offered(provider) is not { } p) return DomainErrors.Sso.NotOffered;
        if (!IsS256Challenge(clientChallenge)) return DomainErrors.Sso.BadChallenge;

        OidcMetadata metadata;
        try
        {
            metadata = await _metadata.GetAsync(p, refresh: false, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException or TaskCanceledException)
        {
            _logger.LogError(ex, "Could not read {Provider}'s sign-in metadata", p.Name);
            return DomainErrors.Sso.ProviderUnavailable;
        }

        var state = AuthChallengeStore.NewSecret();
        var nonce = AuthChallengeStore.NewSecret();
        var verifier = AuthChallengeStore.NewSecret();
        await _challenges.CreateAsync(
            AuthChallengePurposes.Sso, state, userId: null,
            JsonSerializer.Serialize(new RoundTrip(p.Id, nonce, verifier, clientChallenge)),
            RoundTripLifetime, ct);

        var query = new Dictionary<string, string>
        {
            ["client_id"] = p.ClientId,
            ["response_type"] = "code",
            ["response_mode"] = "query",
            ["redirect_uri"] = RedirectUri(p),
            ["scope"] = "openid email profile",
            ["state"] = state,
            ["nonce"] = nonce,
            ["code_challenge"] = S256(verifier),
            ["code_challenge_method"] = "S256",
            // On a shared desk, the account the browser is signed in to may not be the person's.
            ["prompt"] = "select_account",
        };
        var separator = metadata.AuthorizationEndpoint.Contains('?') ? '&' : '?';
        return metadata.AuthorizationEndpoint + separator
            + string.Join('&', query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
    }

    public async Task<string> ReturnFromProviderAsync(string provider, string? code, string? state, string? error, CancellationToken ct = default)
    {
        if (Offered(provider) is not { } p) return BackToApp("error", "not_configured");

        // The state is the round trip's key, and taking it ends the round trip: a second
        // visit to the same address, or one crafted with another state, finds nothing.
        var step = string.IsNullOrEmpty(state) ? null : await _challenges.TakeAsync(AuthChallengePurposes.Sso, state, ct);
        var trip = step == null ? null : JsonSerializer.Deserialize<RoundTrip>(step.Payload);
        if (trip == null || !string.Equals(trip.Provider, p.Id, StringComparison.Ordinal))
            return BackToApp("error", "expired");

        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogInformation("{Provider} sign-in came back with {Error}", p.Name, Truncate(error, 60));
            return BackToApp("error", error == "access_denied" ? "cancelled" : "provider_error");
        }
        if (string.IsNullOrEmpty(code)) return BackToApp("error", "provider_error");

        JsonWebToken? token;
        try
        {
            var metadata = await _metadata.GetAsync(p, refresh: false, ct);
            var idToken = await RedeemAsync(p, metadata, code, trip.Verifier, ct);
            if (idToken == null) return BackToApp("error", "provider_error");
            // Checking it can read the provider's keys again (a key published since they were
            // kept), so a provider that does not answer then is the provider's failure too.
            token = await ValidateAsync(p, idToken, trip.Nonce, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or JsonException or TaskCanceledException)
        {
            _logger.LogError(ex, "Could not redeem or check the {Provider} sign-in code", p.Name);
            return BackToApp("error", "provider_error");
        }
        if (token == null) return BackToApp("error", "invalid_token");

        var (user, refusal) = await FindOrMakeAccountAsync(p, token, ct);
        if (user == null) return BackToApp("error", refusal!);

        var handoff = AuthChallengeStore.NewSecret();
        await _challenges.CreateAsync(
            AuthChallengePurposes.SsoHandoff, handoff, user.Id,
            JsonSerializer.Serialize(new Handoff(trip.ClientChallenge, p.Id)),
            HandoffLifetime, ct);
        return BackToApp("code", handoff);
    }

    public string? ReturnToAppWithError(string reason) => _appBase == null ? null : BackToApp("error", reason);

    public async Task<Result<LoginResponse>> CompleteAsync(SsoCompleteRequest request, CancellationToken ct = default)
    {
        var step = await _challenges.TakeAsync(AuthChallengePurposes.SsoHandoff, request.Code, ct);
        var handoff = step == null ? null : JsonSerializer.Deserialize<Handoff>(step.Payload);
        if (step?.UserId is not { } userId || handoff == null) return DomainErrors.Sso.HandoffExpired;

        // Only the browser that started holds the verifier whose hash came with the start.
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(S256(request.Verifier)), Encoding.ASCII.GetBytes(handoff.ClientChallenge)))
        {
            _logger.LogWarning("A single sign-on hand-back for user {UserId} came from a browser that did not start it", userId);
            return DomainErrors.Sso.HandoffNotYours;
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) return DomainErrors.Sso.HandoffExpired;
        return await _sessions.IssueAsync(user, SignInMethods.Sso, secondFactorDone: false, ct);
    }

    public async Task<IReadOnlyList<SsoLinkDto>> LinksAsync(Guid userId, CancellationToken ct = default) =>
        await _db.UserExternalLogins.AsNoTracking()
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.CreatedAtUtc)
            .Select(l => new SsoLinkDto(l.Id, l.Provider, l.Email, l.CreatedAtUtc, l.LastUsedAtUtc))
            .ToListAsync(ct);

    public async Task<Result> UnlinkAsync(Guid userId, Guid linkId, CancellationToken ct = default)
    {
        var link = await _db.UserExternalLogins.FirstOrDefaultAsync(l => l.Id == linkId && l.UserId == userId, ct);
        if (link == null) return DomainErrors.Sso.LinkNotFound;

        var user = await _db.Users.FirstAsync(u => u.Id == userId, ct);
        var otherWays = !string.IsNullOrEmpty(user.PasswordHash)
            || await _db.UserExternalLogins.AnyAsync(l => l.UserId == userId && l.Id != linkId, ct)
            || await _db.UserPasskeys.AnyAsync(k => k.UserId == userId, ct);
        if (!otherWays) return DomainErrors.Sso.LastWayIn;

        _db.UserExternalLogins.Remove(link);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("User {UserId} unlinked their {Provider} sign-in", userId, link.Provider);
        return Result.Success();
    }

    private async Task<string?> RedeemAsync(OidcProvider provider, OidcMetadata metadata, string code, string verifier, CancellationToken ct)
    {
        var client = _http.CreateClient(OidcMetadataSource.HttpClientName);
        using var response = await client.PostAsync(metadata.TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri(provider),
            ["client_id"] = provider.ClientId,
            ["client_secret"] = provider.ClientSecret,
            ["code_verifier"] = verifier,
        }), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        using var json = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        if (!response.IsSuccessStatusCode)
        {
            // The error code only: the description can echo the request.
            var reason = json.RootElement.TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;
            _logger.LogWarning("{Provider} refused the sign-in code: {Status} {Error}", provider.Name, (int)response.StatusCode, Truncate(reason ?? "", 60));
            return null;
        }
        return json.RootElement.TryGetProperty("id_token", out var idToken) && idToken.ValueKind == JsonValueKind.String ? idToken.GetString() : null;
    }

    /// <summary>The id token when the provider signed it for this app, recently, for this round trip; null otherwise.</summary>
    private async Task<JsonWebToken?> ValidateAsync(OidcProvider provider, string idToken, string nonce, CancellationToken ct)
    {
        var metadata = await _metadata.GetAsync(provider, refresh: false, ct);
        var result = await ValidateWith(metadata);
        if (!result.IsValid && result.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            // Signed with a key published since the cached set was read.
            metadata = await _metadata.GetAsync(provider, refresh: true, ct);
            result = await ValidateWith(metadata);
        }
        if (!result.IsValid || result.SecurityToken is not JsonWebToken token)
        {
            _logger.LogWarning("Refused a {Provider} id token: {Reason}", provider.Name, result.Exception?.GetType().Name ?? "invalid");
            return null;
        }

        if (!token.TryGetPayloadValue<string>("nonce", out var carried) || !string.Equals(carried, nonce, StringComparison.Ordinal))
        {
            _logger.LogWarning("Refused a {Provider} id token: it does not carry this sign-in's nonce", provider.Name);
            return null;
        }
        // OpenID Connect Core 3.1.3.7: a token for several audiences must say it was issued to this app.
        if (token.Audiences.Count() > 1 && !string.Equals(token.TryGetPayloadValue<string>("azp", out var azp) ? azp : null, provider.ClientId, StringComparison.Ordinal))
        {
            _logger.LogWarning("Refused a {Provider} id token: several audiences and not authorised for this app", provider.Name);
            return null;
        }
        if (string.IsNullOrEmpty(token.Subject))
        {
            _logger.LogWarning("Refused a {Provider} id token: it names no account", provider.Name);
            return null;
        }
        return token;

        Task<TokenValidationResult> ValidateWith(OidcMetadata m) => new JsonWebTokenHandler().ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidateIssuer = true,
            IssuerValidator = (issuer, securityToken, _) =>
                securityToken is JsonWebToken jwt && provider.AcceptsIssuer(issuer, m.Issuer, jwt)
                    ? issuer
                    : throw new SecurityTokenInvalidIssuerException($"Issuer {issuer} is not one {provider.Name} signs in with here."),
            ValidateAudience = true,
            ValidAudience = provider.ClientId,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = m.SigningKeys,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        });
    }

    /// <summary>
    /// The account the provider's account signs in as: the one linked to it before, else the
    /// one with the address the provider vouches for (linking it), else a new account, which,
    /// like any new sign-up, belongs to no workspace until someone invites it.
    /// </summary>
    private async Task<(User? User, string? Refusal)> FindOrMakeAccountAsync(OidcProvider provider, JsonWebToken token, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await FindOrMakeAccountOnceAsync(provider, token, ct);
            }
            catch (DbUpdateException ex) when (attempt == 0)
            {
                // Two sign-ins for one new person at once: the other made the account or the
                // link first (unique indexes). Forget this attempt and find what it made.
                _logger.LogInformation(ex, "Single sign-on for a new account raced another; looking again");
                foreach (var entry in _db.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged).ToList())
                    entry.State = EntityState.Detached;
            }
        }
    }

    private async Task<(User? User, string? Refusal)> FindOrMakeAccountOnceAsync(OidcProvider provider, JsonWebToken token, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var issuer = token.Issuer;
        var subject = token.Subject;
        var email = provider.VerifiedEmail(token)?.ToLowerInvariant();

        var link = await _db.UserExternalLogins.Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Provider == provider.Id && l.Issuer == issuer && l.Subject == subject, ct);
        if (link != null)
        {
            link.LastUsedAtUtc = now;
            if (email != null) link.Email = email;
            await _db.SaveChangesAsync(ct);
            return (link.User, null);
        }

        if (email == null)
        {
            _logger.LogWarning("{Provider} sign-in refused: the provider did not vouch for the account's address", provider.Name);
            return (null, "email_not_verified");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);
        var tookOver = false;
        if (user == null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Name = NameOf(token, email),
                Email = email,
                CreatedAtUtc = now,
                // The provider has checked the address, as the confirmation link would.
                EmailVerifiedAtUtc = now,
            };
            _db.Users.Add(user);
            _logger.LogInformation("{Provider} sign-in made a new account {UserId}", provider.Name, user.Id);
        }
        else if (user.EmailVerifiedAtUtc == null)
        {
            await TakeOverAsync(user, now, ct);
            tookOver = true;
        }

        _db.UserExternalLogins.Add(new UserExternalLogin
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Provider = provider.Id,
            Issuer = issuer,
            Subject = subject,
            Email = email,
            CreatedAtUtc = now,
            LastUsedAtUtc = now,
        });
        await _db.SaveChangesAsync(ct);
        if (tookOver) _stamps?.Forget(user.Id);
        return (user, null);
    }

    /// <summary>
    /// The address's owner, proven by the provider, takes over an account registered with it
    /// that never proved it owned the address.
    /// </summary>
    /// <remarks>
    /// Anyone can register any address. Whoever registered this one before its owner chose the
    /// password, and could have turned on 2FA or added passkeys of their own: linking the owner's
    /// Google or Microsoft account to it as it stood would let that person in beside them. So
    /// those go, and every session is ended; the owner signs in with the provider, and can set
    /// a password with Forgot password, whose link goes to the address. A password reset makes
    /// the same claim for the same reason (AuthService.ResetPasswordAsync).
    /// </remarks>
    private async Task TakeOverAsync(User user, DateTime now, CancellationToken ct)
    {
        user.PasswordHash = null;
        user.PasswordResetTokenHash = null;
        user.PasswordResetTokenExpiresAtUtc = null;
        user.TwoFactorEnabled = false;
        user.TwoFactorSecretProtected = null;
        user.TwoFactorEnabledAtUtc = null;
        user.EmailVerificationTokenHash = null;
        user.EmailVerificationTokenExpiresAtUtc = null;
        user.EmailVerifiedAtUtc = now;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        _db.UserPasskeys.RemoveRange(await _db.UserPasskeys.Where(k => k.UserId == user.Id).ToListAsync(ct));
        _logger.LogWarning("Single sign-on proved the address of unconfirmed account {UserId}; its password, 2FA and passkeys were removed", user.Id);
    }

    private static string NameOf(JsonWebToken token, string email)
    {
        var name = Claim(token, "name")
            ?? string.Join(' ', new[] { Claim(token, "given_name"), Claim(token, "family_name") }.Where(n => n != null)).Trim();
        if (string.IsNullOrWhiteSpace(name)) name = email.Split('@')[0];
        return name.Length > 256 ? name[..256] : name;

        static string? Claim(JsonWebToken t, string type) =>
            t.TryGetPayloadValue<string>(type, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
    }

    private string RedirectUri(OidcProvider provider) => $"{_apiBase}/api/auth/sso/{provider.Id}/callback";

    private string BackToApp(string key, string value) => $"{_appBase}/auth/sso#{key}={Uri.EscapeDataString(value)}";

    /// <summary>base64url(SHA-256(ASCII(verifier))), RFC 7636's S256.</summary>
    public static string S256(string verifier) => AuthChallengeStore.Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    // The hash of a 32-byte verifier: 43 base64url characters.
    private static bool IsS256Challenge(string? challenge) =>
        challenge is { Length: 43 } && challenge.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

    private static string? AbsoluteBase(string? configured)
    {
        var value = (configured ?? "").Trim().TrimEnd('/');
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
               && !string.IsNullOrEmpty(uri.Host)
            ? value
            : null;
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];

    private sealed record RoundTrip(string Provider, string Nonce, string Verifier, string ClientChallenge);

    private sealed record Handoff(string ClientChallenge, string Provider);
}
