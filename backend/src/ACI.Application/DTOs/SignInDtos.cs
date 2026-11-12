using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace ACI.Application.DTOs;

/// <summary>A single sign-on provider the server is set up for.</summary>
/// <param name="Id">"google" or "microsoft", as in /api/auth/sso/{id}/start.</param>
/// <param name="Name">What the button says: Google, Microsoft.</param>
public record SsoProviderDto(string Id, string Name);

/// <summary>The ways to sign in this server offers besides the password, for the sign-in page.</summary>
public record SignInOptionsDto(IReadOnlyList<SsoProviderDto> Sso, bool Passkeys);

/// <summary>
/// The browser finishing a single sign-on: the one-time code the server handed back in the
/// address, and the secret only the browser that started the sign-on holds (its PKCE verifier).
/// </summary>
public record SsoCompleteRequest
{
    [Required]
    [StringLength(128, MinimumLength = 20)]
    public required string Code { get; init; }

    [Required]
    [StringLength(128, MinimumLength = 43)]
    public required string Verifier { get; init; }
}

/// <summary>A Google or Microsoft account the person signs in with.</summary>
public record SsoLinkDto(Guid Id, string Provider, string Email, DateTime CreatedAtUtc, DateTime? LastUsedAtUtc);

/// <summary>A passkey, as Settings lists it.</summary>
/// <param name="Synced">Whether it syncs between the person's devices (a password manager's passkey).</param>
public record PasskeyDto(Guid Id, string Name, DateTime CreatedAtUtc, DateTime? LastUsedAtUtc, bool Synced);

/// <summary>
/// What the browser hands navigator.credentials, and the id of the step to name when it answers.
/// </summary>
/// <param name="CeremonyId">Names this step when the browser sends the passkey's answer back. Used once.</param>
/// <param name="Options">WebAuthn options as JSON (PublicKeyCredentialCreationOptions or ...RequestOptions), binary fields base64url.</param>
public record PasskeyCeremonyDto(string CeremonyId, JsonElement Options);

/// <summary>Starts adding a passkey: the account's password when it has one (see PasskeyService).</summary>
public record PasskeyRegistrationOptionsRequest
{
    [StringLength(200)]
    public string? Password { get; init; }
}

/// <summary>The browser's answer to navigator.credentials.create, with a name for the new passkey.</summary>
public record PasskeyRegistrationRequest
{
    [Required]
    [StringLength(128)]
    public required string CeremonyId { get; init; }

    [StringLength(60)]
    public string? Name { get; init; }

    /// <summary>The PublicKeyCredential as JSON, binary fields base64url.</summary>
    public required JsonElement Credential { get; init; }
}

/// <summary>The browser's answer to navigator.credentials.get.</summary>
public record PasskeyAssertionRequest
{
    [Required]
    [StringLength(128)]
    public required string CeremonyId { get; init; }

    /// <summary>The PublicKeyCredential as JSON, binary fields base64url.</summary>
    public required JsonElement Credential { get; init; }
}

/// <summary>A passkey in place of the authenticator code, after the password or single sign-on.</summary>
public record PasskeySecondFactorOptionsRequest
{
    [Required]
    [StringLength(4096)]
    public required string TwoFactorToken { get; init; }
}

/// <summary>The browser's answer, with the step token it was asked for.</summary>
public record PasskeySecondFactorRequest
{
    [Required]
    [StringLength(4096)]
    public required string TwoFactorToken { get; init; }

    [Required]
    [StringLength(128)]
    public required string CeremonyId { get; init; }

    public required JsonElement Credential { get; init; }
}

/// <summary>
/// A workspace's single sign-on requirement, for its Settings card.
/// </summary>
/// <param name="Required">Whether members it applies to must sign in with Google or Microsoft to work here.</param>
/// <param name="Domains">The workspace's email domains: the requirement applies to members at them; empty, to every member but the owner.</param>
/// <param name="Providers">The providers this server offers; with none, the requirement cannot be turned on.</param>
/// <param name="CanChange">Whether the caller may change it (the owner alone).</param>
/// <param name="Members">Each member, whether the requirement applies to them and whether they have signed in with single sign-on before.</param>
public record OrganizationSsoDto(
    bool Required,
    IReadOnlyList<string> Domains,
    IReadOnlyList<SsoProviderDto> Providers,
    bool CanChange,
    IReadOnlyList<OrganizationSsoMemberDto> Members);

/// <param name="Applies">Whether the requirement holds them (never the owner).</param>
/// <param name="HasSignedInWithSso">Whether a Google or Microsoft account is linked to them already.</param>
public record OrganizationSsoMemberDto(Guid UserId, string Name, string Email, bool IsOwner, bool Applies, bool HasSignedInWithSso);

/// <summary>Sets the requirement; a field left out keeps its value.</summary>
public record UpdateOrganizationSsoRequest
{
    public bool? Required { get; init; }

    /// <summary>The workspace's email domains ("pavillon46.ch"); an empty list clears them.</summary>
    public IReadOnlyList<string>? Domains { get; init; }
}
