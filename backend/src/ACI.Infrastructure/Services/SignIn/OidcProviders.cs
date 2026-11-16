using System.Text.Json;
using ACI.Application.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ACI.Infrastructure.Services.SignIn;

/// <summary>
/// An OpenID Connect provider people sign in with, and what its id tokens must say to be
/// believed: who issued them, and whether the address in them was verified.
/// </summary>
public abstract class OidcProvider
{
    protected OidcProvider(string clientId, string clientSecret)
    {
        ClientId = clientId;
        ClientSecret = clientSecret;
    }

    /// <summary>"google" or "microsoft", as in the callback path.</summary>
    public abstract string Id { get; }

    public abstract string Name { get; }

    /// <summary>Where the provider publishes its endpoints and signing keys.</summary>
    public abstract string DiscoveryUrl { get; }

    public string ClientId { get; }

    public string ClientSecret { get; }

    /// <summary>
    /// Whether <paramref name="issuer"/>, the token's "iss", is one this provider issues for
    /// the accounts the server accepts. <paramref name="metadataIssuer"/> is the issuer the
    /// discovery document gives.
    /// </summary>
    public abstract bool AcceptsIssuer(string issuer, string metadataIssuer, JsonWebToken token);

    /// <summary>
    /// The token's address when the provider vouches the account owns it; null when it does not
    /// say so. An address the provider has not verified matches nobody: anyone can put any
    /// address on an account at some providers.
    /// </summary>
    public abstract string? VerifiedEmail(JsonWebToken token);

    protected static bool IsTrue(JsonWebToken token, string claim)
    {
        if (!token.TryGetPayloadValue<object>(claim, out var value) || value == null) return false;
        return value switch
        {
            bool b => b,
            string s => s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1",
            JsonElement { ValueKind: JsonValueKind.True } => true,
            JsonElement { ValueKind: JsonValueKind.String } e => e.GetString() is { } t && (t.Equals("true", StringComparison.OrdinalIgnoreCase) || t == "1"),
            _ => false,
        };
    }

    protected static string? Text(JsonWebToken token, string claim) =>
        token.TryGetPayloadValue<string>(claim, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}

/// <summary>Sign in with Google: any Google account whose address Google has verified.</summary>
public sealed class GoogleProvider : OidcProvider
{
    public GoogleProvider(string clientId, string clientSecret) : base(clientId, clientSecret) { }

    public override string Id => SsoProviderIds.Google;
    public override string Name => "Google";
    public override string DiscoveryUrl => "https://accounts.google.com/.well-known/openid-configuration";

    // Google documents both forms of its issuer.
    public override bool AcceptsIssuer(string issuer, string metadataIssuer, JsonWebToken token) =>
        issuer is "https://accounts.google.com" or "accounts.google.com";

    public override string? VerifiedEmail(JsonWebToken token) =>
        IsTrue(token, "email_verified") ? Text(token, "email") : null;
}

/// <summary>
/// Sign in with Microsoft Entra ID (work and school accounts, and personal Microsoft accounts
/// when the tenant setting allows them).
/// </summary>
/// <remarks>
/// Entra's "email" claim is whatever the person's directory says, which a tenant's admin can
/// set to any address (the 2023 "nOAuth" problem). It is believed only when the token also
/// carries xms_edov ("email domain owner verified"), an optional claim the app registration
/// must ask for; without it nobody is matched by address.
/// </remarks>
public sealed class MicrosoftProvider : OidcProvider
{
    /// <summary>The tenant every personal Microsoft account signs in through.</summary>
    public const string ConsumerTenant = "9188040d-6c67-4c5b-b112-36a304b66dad";

    private readonly string _tenant;

    public MicrosoftProvider(string clientId, string clientSecret, string tenant) : base(clientId, clientSecret)
    {
        _tenant = string.IsNullOrWhiteSpace(tenant) ? "organizations" : tenant.Trim();
    }

    public override string Id => SsoProviderIds.Microsoft;
    public override string Name => "Microsoft";
    public override string DiscoveryUrl => $"https://login.microsoftonline.com/{Uri.EscapeDataString(_tenant)}/v2.0/.well-known/openid-configuration";

    public override bool AcceptsIssuer(string issuer, string metadataIssuer, JsonWebToken token)
    {
        // The shared endpoints (organizations, common, consumers) publish their issuer with a
        // {tenantid} placeholder; each token names the person's own tenant in it and in "tid".
        var tid = Text(token, "tid");
        if (tid == null || !Guid.TryParse(tid, out _)) return false;
        var expected = metadataIssuer.Replace("{tenantid}", tid, StringComparison.OrdinalIgnoreCase);
        if (!string.Equals(issuer, expected, StringComparison.Ordinal)) return false;

        var personal = string.Equals(tid, ConsumerTenant, StringComparison.OrdinalIgnoreCase);
        return _tenant.ToLowerInvariant() switch
        {
            "organizations" => !personal,
            "consumers" => personal,
            "common" => true,
            // One tenant: its discovery document names it, so the issuer check above holds it to that tenant.
            _ => true,
        };
    }

    public override string? VerifiedEmail(JsonWebToken token) =>
        IsTrue(token, "xms_edov") ? Text(token, "email") : null;
}

public static class SsoProviderIds
{
    public const string Google = "google";
    public const string Microsoft = "microsoft";
}

/// <summary>The providers the configuration sets up; one without a client id and secret is not offered.</summary>
public sealed class OidcProviderCatalog
{
    private readonly IReadOnlyList<OidcProvider> _providers;

    public OidcProviderCatalog(IOptions<SingleSignOnSettings> settings)
    {
        var s = settings.Value;
        var providers = new List<OidcProvider>();
        if (s.Google.IsSet) providers.Add(new GoogleProvider(s.Google.ClientId!.Trim(), s.Google.ClientSecret!.Trim()));
        if (s.Microsoft.IsSet) providers.Add(new MicrosoftProvider(s.Microsoft.ClientId!.Trim(), s.Microsoft.ClientSecret!.Trim(), s.Microsoft.Tenant));
        _providers = providers;
    }

    public IReadOnlyList<OidcProvider> All => _providers;

    public OidcProvider? Find(string? id) =>
        _providers.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
}
