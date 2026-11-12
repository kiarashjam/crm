using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ACI.Application.Interfaces;
using ACI.Infrastructure.Services.SignIn;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ACI.WebApi.Tests.Helpers;

/// <summary>
/// Google and Microsoft, played by the test: their discovery documents, signing keys and
/// token endpoints, at their real addresses. The id token the token endpoint hands out is
/// whatever the test signs (<see cref="IdToken"/>).
/// </summary>
/// <remarks>
/// No provider account is involved, so these tests prove what Cadence sends and what it
/// believes, as the providers document their endpoints.
/// </remarks>
public sealed class FakeOidcProvider : HttpMessageHandler
{
    public const string GoogleClientId = "cadence-test.apps.googleusercontent.com";
    public const string GoogleClientSecret = "google-test-secret";
    public const string GoogleIssuer = "https://accounts.google.com";
    public const string GoogleAuthorize = "https://accounts.google.com/o/oauth2/v2/auth";
    public const string GoogleToken = "https://oauth2.googleapis.com/token";

    public const string MicrosoftClientId = "6e3c8e1b-1f4e-4b9e-9a64-3f2f8a3b9c11";
    public const string MicrosoftClientSecret = "microsoft-test-secret";
    public const string MicrosoftAuthorize = "https://login.microsoftonline.com/organizations/oauth2/v2.0/authorize";
    public const string MicrosoftToken = "https://login.microsoftonline.com/organizations/oauth2/v2.0/token";
    public const string ClubTenant = "4f6c2b7e-0d1a-4c8e-9b3f-5a6d7e8f9012";

    /// <summary>The key the providers publish and sign with.</summary>
    public RsaSecurityKey Key { get; } = NewKey("published-key");

    /// <summary>A key nobody published: a forger's.</summary>
    public RsaSecurityKey Stranger { get; } = NewKey("forged-key");

    /// <summary>Every request to a token endpoint: where, and the form fields.</summary>
    public ConcurrentQueue<(string Url, IReadOnlyDictionary<string, string> Form)> TokenRequests { get; } = new();

    /// <summary>What a token endpoint answers with as the id token, given the request's form; null answers 400.</summary>
    public Func<IReadOnlyDictionary<string, string>, string?> IdToken { get; set; } = _ => null;

    public string Sign(IDictionary<string, object> claims, RsaSecurityKey? key = null) =>
        new JsonWebTokenHandler().CreateToken(
            JsonSerializer.Serialize(claims),
            new SigningCredentials(key ?? Key, SecurityAlgorithms.RsaSha256));

    /// <summary>The claims Google puts in an id token for this app, now; change what a test needs.</summary>
    public static Dictionary<string, object> GoogleClaims(string sub, string email, string nonce, bool verified = true) => new()
    {
        ["iss"] = GoogleIssuer,
        ["aud"] = GoogleClientId,
        ["azp"] = GoogleClientId,
        ["sub"] = sub,
        ["email"] = email,
        ["email_verified"] = verified,
        ["name"] = "Anna Keller",
        ["nonce"] = nonce,
        ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
        ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
    };

    /// <summary>The claims Entra ID puts in a v2 id token for a work account in <paramref name="tenant"/>.</summary>
    public static Dictionary<string, object> MicrosoftClaims(string sub, string email, string nonce, string tenant = ClubTenant, bool domainVerified = true)
    {
        var claims = new Dictionary<string, object>
        {
            ["iss"] = $"https://login.microsoftonline.com/{tenant}/v2.0",
            ["aud"] = MicrosoftClientId,
            ["sub"] = sub,
            ["tid"] = tenant,
            ["oid"] = Guid.NewGuid().ToString(),
            ["email"] = email,
            ["preferred_username"] = email,
            ["name"] = "Marc Dubois",
            ["nonce"] = nonce,
            ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(),
        };
        if (domainVerified) claims["xms_edov"] = true;
        return claims;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var url = request.RequestUri!.GetLeftPart(UriPartial.Path);
        switch (request.Method.Method, url)
        {
            case ("GET", "https://accounts.google.com/.well-known/openid-configuration"):
                return Json(new
                {
                    issuer = GoogleIssuer,
                    authorization_endpoint = GoogleAuthorize,
                    token_endpoint = GoogleToken,
                    jwks_uri = "https://www.googleapis.com/oauth2/v3/certs",
                });
            case ("GET", "https://login.microsoftonline.com/organizations/v2.0/.well-known/openid-configuration"):
                return Json(new
                {
                    issuer = "https://login.microsoftonline.com/{tenantid}/v2.0",
                    authorization_endpoint = MicrosoftAuthorize,
                    token_endpoint = MicrosoftToken,
                    jwks_uri = "https://login.microsoftonline.com/organizations/discovery/v2.0/keys",
                });
            case ("GET", "https://www.googleapis.com/oauth2/v3/certs"):
            case ("GET", "https://login.microsoftonline.com/organizations/discovery/v2.0/keys"):
                return Json(new { keys = new[] { Jwk(Key) } });
            case ("POST", GoogleToken):
            case ("POST", MicrosoftToken):
                var form = (await request.Content!.ReadAsStringAsync(ct)).Split('&', StringSplitOptions.RemoveEmptyEntries)
                    .Select(pair => pair.Split('=', 2))
                    .ToDictionary(kv => WebUtility.UrlDecode(kv[0]), kv => kv.Length > 1 ? WebUtility.UrlDecode(kv[1]) : "");
                TokenRequests.Enqueue((url, form));
                return IdToken(form) is { } idToken
                    ? Json(new { access_token = "an-access-token", token_type = "Bearer", expires_in = 3599, id_token = idToken })
                    : Json(new { error = "invalid_grant", error_description = "Bad code" }, HttpStatusCode.BadRequest);
            default:
                return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private static object Jwk(RsaSecurityKey key)
    {
        var parameters = key.Rsa.ExportParameters(false);
        return new { kty = "RSA", use = "sig", alg = "RS256", kid = key.KeyId, n = Base64UrlEncoder.Encode(parameters.Modulus), e = Base64UrlEncoder.Encode(parameters.Exponent) };
    }

    private static HttpResponseMessage Json(object body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    private static RsaSecurityKey NewKey(string id) => new(RSA.Create(2048)) { KeyId = id };
}

/// <summary>
/// The app with Google and Microsoft sign-in set up against <see cref="FakeOidcProvider"/>,
/// and a mail server that keeps the password reset links it sends.
/// </summary>
public sealed class SsoApp : CustomWebApplicationFactory
{
    public const string AppBase = "https://app.cadence.test";
    public const string ApiBase = "https://api.cadence.test";

    private readonly List<(string To, string Url)> _resetLinks = new();

    public FakeOidcProvider Provider { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Sso:Google:ClientId"] = FakeOidcProvider.GoogleClientId,
            ["Sso:Google:ClientSecret"] = FakeOidcProvider.GoogleClientSecret,
            ["Sso:Microsoft:ClientId"] = FakeOidcProvider.MicrosoftClientId,
            ["Sso:Microsoft:ClientSecret"] = FakeOidcProvider.MicrosoftClientSecret,
            ["Sso:Microsoft:Tenant"] = "organizations",
            ["Sso:ApiBaseUrl"] = ApiBase,
            ["Email:FrontendBaseUrl"] = AppBase,
        }));
        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient(OidcMetadataSource.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => Provider);
            var sender = new Mock<IEmailSender>();
            sender.Setup(s => s.SendPasswordResetEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback((string to, string _, string url, CancellationToken _) => { lock (_resetLinks) _resetLinks.Add((to, url)); })
                .ReturnsAsync(true);
            sender.Setup(s => s.SendEmailVerificationEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            sender.Setup(s => s.SendOrganizationInviteEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            services.AddSingleton(sender.Object);
        });
    }

    /// <summary>The token in the last password reset link sent to <paramref name="to"/>.</summary>
    public string ResetToken(string to)
    {
        string url;
        lock (_resetLinks) url = _resetLinks.Last(l => string.Equals(l.To, to, StringComparison.OrdinalIgnoreCase)).Url;
        return Uri.UnescapeDataString(new Uri(url).Query.TrimStart('?').Split('&').Single(p => p.StartsWith("token=", StringComparison.Ordinal))["token=".Length..]);
    }
}
