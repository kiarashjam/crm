using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

/// <summary>What an <see cref="AuthChallenge"/> is for.</summary>
public static class AuthChallengePurposes
{
    /// <summary>A round trip to Google or Microsoft, keyed by its state.</summary>
    public const string Sso = "sso";

    /// <summary>The one-time code a finished round trip hands back to the browser that started it.</summary>
    public const string SsoHandoff = "sso-handoff";

    public const string PasskeyRegistration = "passkey-registration";
    public const string PasskeySignIn = "passkey-sign-in";
    public const string PasskeySecondFactor = "passkey-second-factor";
}

/// <summary>Sign-in steps in progress (see <see cref="AuthChallenge"/>): each used once, within its lifetime.</summary>
public interface IAuthChallenges
{
    /// <summary>Keeps a step that <paramref name="secret"/> finishes; only the secret's hash is stored.</summary>
    Task CreateAsync(string purpose, string secret, Guid? userId, string payload, TimeSpan lifetime, CancellationToken ct = default);

    /// <summary>
    /// The step <paramref name="secret"/> names, removed as it is read so it cannot be used
    /// twice, even by two requests at once; null when there is none, or it has expired.
    /// </summary>
    Task<AuthChallenge?> TakeAsync(string purpose, string secret, CancellationToken ct = default);
}

/// <summary>
/// Single sign-on with Google and Microsoft (OpenID Connect, authorization code with PKCE).
/// </summary>
/// <remarks>
/// The browser goes to the provider and back to the API, which checks the provider's answer
/// and hands the browser a one-time code in the address; the app trades that code, with a
/// secret only the browser that started holds, for the same session a password gives.
/// </remarks>
public interface ISingleSignOn
{
    /// <summary>The providers this server is set up for.</summary>
    IReadOnlyList<SsoProviderDto> Providers { get; }

    /// <summary>
    /// Where to send the browser to sign in with <paramref name="provider"/>.
    /// <paramref name="clientChallenge"/> is the S256 hash of the secret the app keeps for
    /// <see cref="CompleteAsync"/>, so only the browser that started can finish.
    /// </summary>
    Task<Result<string>> StartAsync(string provider, string clientChallenge, CancellationToken ct = default);

    /// <summary>
    /// The provider sending the browser back: checks its answer, finds or makes the account,
    /// and says where to send the browser next (the app, with a one-time code or an error).
    /// </summary>
    Task<string> ReturnFromProviderAsync(string provider, string? code, string? state, string? error, CancellationToken ct = default);

    /// <summary>The app's sign-on page with <paramref name="reason"/> as the error; null when the app's address is not set.</summary>
    string? ReturnToAppWithError(string reason);

    /// <summary>Trades the one-time code for a session, or for the two-factor step when 2FA is on.</summary>
    Task<Result<LoginResponse>> CompleteAsync(SsoCompleteRequest request, CancellationToken ct = default);

    /// <summary>The Google and Microsoft accounts <paramref name="userId"/> signs in with.</summary>
    Task<IReadOnlyList<SsoLinkDto>> LinksAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Stops one signing them in, unless it is their only way in.</summary>
    Task<Result> UnlinkAsync(Guid userId, Guid linkId, CancellationToken ct = default);
}

/// <summary>Passkeys (WebAuthn): adding, listing and removing them, and signing in with one.</summary>
public interface IPasskeys
{
    bool IsConfigured { get; }

    Task<IReadOnlyList<PasskeyDto>> ListAsync(Guid userId, CancellationToken ct = default);

    /// <param name="sessionIssuedAtUtc">When the caller's session began: an account without a password needs a recent one.</param>
    Task<Result<PasskeyCeremonyDto>> RegistrationOptionsAsync(Guid userId, PasskeyRegistrationOptionsRequest request, DateTime? sessionIssuedAtUtc, CancellationToken ct = default);

    Task<Result<PasskeyDto>> RegisterAsync(Guid userId, PasskeyRegistrationRequest request, CancellationToken ct = default);

    Task<Result> RemoveAsync(Guid userId, Guid passkeyId, CancellationToken ct = default);

    /// <summary>Options for signing in with any passkey the browser has for this site.</summary>
    Task<Result<PasskeyCeremonyDto>> SignInOptionsAsync(CancellationToken ct = default);

    Task<Result<LoginResponse>> SignInAsync(PasskeyAssertionRequest request, CancellationToken ct = default);

    /// <summary>Options for using one of the person's passkeys in place of the authenticator code.</summary>
    Task<Result<PasskeyCeremonyDto>> SecondFactorOptionsAsync(PasskeySecondFactorOptionsRequest request, CancellationToken ct = default);

    Task<Result<LoginResponse>> SecondFactorAsync(PasskeySecondFactorRequest request, CancellationToken ct = default);
}

/// <summary>A workspace's single sign-on requirement (Organization.SsoRequired): the owner's setting, and who it holds.</summary>
public interface IOrganizationSso
{
    Task<Result<OrganizationSsoDto>> GetAsync(Guid organizationId, Guid userId, CancellationToken ct = default);

    Task<Result<OrganizationSsoDto>> UpdateAsync(Guid organizationId, Guid userId, UpdateOrganizationSsoRequest request, CancellationToken ct = default);

    /// <summary>
    /// Whether a session that began with <paramref name="signInMethod"/> may work in the
    /// workspace as <paramref name="userId"/>. Always yes for single sign-on, for the owner,
    /// for a workspace without the requirement, and for a member it does not apply to.
    /// </summary>
    Task<bool> AdmitsAsync(Guid organizationId, Guid userId, string signInMethod, CancellationToken ct = default);

    /// <summary>The workspace's name when it refuses such a session (to say so); null when it admits it.</summary>
    Task<string?> RefusalAsync(Guid organizationId, Guid userId, string signInMethod, CancellationToken ct = default);

    /// <summary>The workspaces among <paramref name="organizationIds"/> that would refuse such a session.</summary>
    Task<IReadOnlySet<Guid>> RefusingAsync(IReadOnlyCollection<Guid> organizationIds, Guid userId, string signInMethod, CancellationToken ct = default);
}
