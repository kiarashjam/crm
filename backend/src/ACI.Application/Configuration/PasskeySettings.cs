namespace ACI.Application.Configuration;

/// <summary>
/// Passkeys (WebAuthn). A passkey belongs to one site, its relying party: the app's domain.
/// </summary>
/// <remarks>
/// Left empty, both come from Email:FrontendBaseUrl (its host is the relying party, its
/// origin the only one allowed), which is right when the app is served from one address.
/// Changing the relying party later makes every passkey already made stop working, so set it
/// once: to the app's host (app.example.ch), or to its registrable domain (example.ch) to let
/// passkeys work on every subdomain.
/// </remarks>
public sealed class PasskeySettings
{
    public const string SectionName = "Passkeys";

    /// <summary>The relying party id: the app's host, or a registrable domain it is under. Empty: Email:FrontendBaseUrl's host.</summary>
    public string? RelyingPartyId { get; set; }

    /// <summary>The name passkey prompts show for the site.</summary>
    public string RelyingPartyName { get; set; } = "Cadence";

    /// <summary>The app's origins passkeys may be used from, comma-separated (https://app.example.ch). Empty: Email:FrontendBaseUrl.</summary>
    public string? Origins { get; set; }
}
