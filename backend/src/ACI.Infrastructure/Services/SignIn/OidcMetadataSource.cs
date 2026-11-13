using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace ACI.Infrastructure.Services.SignIn;

/// <summary>A provider's endpoints and the keys its id tokens are signed with, from its discovery document.</summary>
public sealed record OidcMetadata(
    string Issuer,
    string AuthorizationEndpoint,
    string TokenEndpoint,
    IReadOnlyList<SecurityKey> SigningKeys,
    DateTime FetchedAtUtc);

/// <summary>
/// Reads each provider's discovery document and signing keys (JWKS), and keeps them for a day.
/// </summary>
/// <remarks>
/// Providers rotate their keys. A token signed with a key not in the cached set has the set
/// read again, at most every five minutes, so a rotation is picked up at once and a flood of
/// forged tokens cannot make the server hammer the provider.
/// </remarks>
public sealed class OidcMetadataSource
{
    public const string HttpClientName = "sso";

    private static readonly TimeSpan KeepFor = TimeSpan.FromHours(24);
    private static readonly TimeSpan RefreshNoSoonerThan = TimeSpan.FromMinutes(5);

    private readonly IHttpClientFactory _http;
    private readonly ILogger<OidcMetadataSource> _logger;
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<string, OidcMetadata> _cache = new(StringComparer.Ordinal);

    public OidcMetadataSource(IHttpClientFactory http, ILogger<OidcMetadataSource> logger, TimeProvider? clock = null)
    {
        _http = http;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    /// <param name="refresh">Read it again (a token named a key the cached set lacks), unless it was read very recently.</param>
    public async Task<OidcMetadata> GetAsync(OidcProvider provider, bool refresh, CancellationToken ct)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        if (_cache.TryGetValue(provider.DiscoveryUrl, out var cached))
        {
            var age = now - cached.FetchedAtUtc;
            if (age < KeepFor && (!refresh || age < RefreshNoSoonerThan)) return cached;
        }

        var fresh = await FetchAsync(provider, now, ct);
        _cache[provider.DiscoveryUrl] = fresh;
        return fresh;
    }

    private async Task<OidcMetadata> FetchAsync(OidcProvider provider, DateTime now, CancellationToken ct)
    {
        var client = _http.CreateClient(HttpClientName);
        using var discovery = JsonDocument.Parse(await client.GetStringAsync(provider.DiscoveryUrl, ct));
        var root = discovery.RootElement;
        string Required(string name) =>
            root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
                ? text
                : throw new InvalidOperationException($"The discovery document of {provider.Name} has no {name}.");

        var issuer = Required("issuer");
        var authorization = Required("authorization_endpoint");
        var token = Required("token_endpoint");
        var jwksUri = Required("jwks_uri");
        foreach (var endpoint in new[] { authorization, token, jwksUri })
        {
            // The code, the client secret and the keys travel over these; never in the clear.
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new InvalidOperationException($"{provider.Name} published an endpoint that is not https: {endpoint}");
        }

        var keys = new JsonWebKeySet(await client.GetStringAsync(jwksUri, ct)).GetSigningKeys();
        if (keys.Count == 0) throw new InvalidOperationException($"{provider.Name} published no signing keys.");

        _logger.LogInformation("Read {Provider}'s sign-in metadata: {KeyCount} signing keys", provider.Name, keys.Count);
        return new OidcMetadata(issuer, authorization, token, keys.ToList(), now);
    }
}
