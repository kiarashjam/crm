namespace ACI.Application.Configuration;

/// <summary>
/// Single sign-on with Google and Microsoft Entra ID (OpenID Connect, authorization code
/// with PKCE). A provider is offered only once its client id and secret are set, so it is
/// all off until the owner registers the apps (backend/README.md, "Single sign-on").
/// </summary>
public sealed class SingleSignOnSettings
{
    public const string SectionName = "Sso";

    public OidcClientSettings Google { get; set; } = new();

    public MicrosoftSignInSettings Microsoft { get; set; } = new();

    /// <summary>
    /// This API's public address (https://api.example.ch), which the providers send people
    /// back to: {ApiBaseUrl}/api/auth/sso/{google|microsoft}/callback, exactly as registered
    /// with each provider. Empty: Email:ApiBaseUrl.
    /// </summary>
    public string? ApiBaseUrl { get; set; }
}

public class OidcClientSettings
{
    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    public bool IsSet => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}

public sealed class MicrosoftSignInSettings : OidcClientSettings
{
    /// <summary>
    /// Whose Microsoft accounts may sign in: "organizations" (any work or school account, the
    /// default), "common" (those and personal Microsoft accounts), "consumers" (personal only),
    /// or one tenant's id, which keeps it to that organisation's directory.
    /// </summary>
    public string Tenant { get; set; } = "organizations";
}
