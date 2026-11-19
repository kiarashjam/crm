using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using ACI.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ACI.WebApi.Monitoring;

/// <summary>One check of a site, as its /health names it.</summary>
public sealed record SiteCheckDto(string Name, string Status, string? Description);

/// <summary>
/// A site's live status: CadenceE itself, or the workspace's member portal (Pavillon 46's website).
/// </summary>
/// <param name="Key">"cadence" or "portal".</param>
/// <param name="Status">"Healthy", "Degraded", "Unhealthy", or "Unreachable" when it did not answer.</param>
/// <param name="Url">Its /health address, for a person to open.</param>
/// <param name="ResponseMs">How long it took to answer; none when it did not.</param>
/// <param name="Error">Why it could not be read, in one line; none when it was.</param>
public sealed record SiteStatusDto(
    string Key, string Name, string Url, string Status, int? ResponseMs, DateTime CheckedAtUtc,
    IReadOnlyList<SiteCheckDto> Checks, string? Error = null);

public sealed record SitesStatusDto(DateTime CheckedAtUtc, IReadOnlyList<SiteStatusDto> Sites);

/// <summary>
/// The live status of the sites a workspace depends on, for Settings › System health: CadenceE's
/// own checks (run here, as /health runs them) and its member portal's /health, read from the
/// portal's own address. A portal's answer is kept for 30 seconds, so a page left open, or several
/// people looking, does not knock on its door more than that.
/// </summary>
public interface ISiteStatus
{
    Task<SitesStatusDto> ReadAsync(Guid organizationId, CancellationToken ct = default);
}

public sealed class SiteStatus : ISiteStatus
{
    /// <summary>The HttpClient that reads a portal's /health: public addresses only, as a webhook's.</summary>
    public const string ClientName = "site-status";

    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PortalTimeout = TimeSpan.FromSeconds(8);

    private readonly HealthCheckService _health;
    private readonly IOnboardingRepository _onboarding;
    private readonly IPublicAddress _address;
    private readonly IHttpClientFactory _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SiteStatus> _logger;

    public SiteStatus(HealthCheckService health, IOnboardingRepository onboarding, IPublicAddress address,
        IHttpClientFactory http, IMemoryCache cache, ILogger<SiteStatus> logger)
    {
        _health = health;
        _onboarding = onboarding;
        _address = address;
        _http = http;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SitesStatusDto> ReadAsync(Guid organizationId, CancellationToken ct = default)
    {
        var sites = new List<SiteStatusDto> { await CadenceAsync(ct) };
        var settings = await _onboarding.GetSettingsAsync(organizationId, ct);
        if (PortalHealthUrl(settings?.WebhookUrl) is { } portal)
        {
            var read = await _cache.GetOrCreateAsync($"site-status:{portal}", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = CacheFor;
                return PortalAsync(portal);
            });
            if (read != null) sites.Add(read);
        }
        return new SitesStatusDto(DateTime.UtcNow, sites);
    }

    /// <summary>CadenceE's own checks, run here, exactly as /health runs them.</summary>
    private async Task<SiteStatusDto> CadenceAsync(CancellationToken ct)
    {
        var watch = Stopwatch.StartNew();
        var report = await _health.CheckHealthAsync(ct);
        var checks = report.Entries
            .Select(e => new SiteCheckDto(e.Key, e.Value.Status.ToString(), e.Value.Exception != null
                ? "The check failed. The details are in the server log."
                : e.Value.Description))
            .ToList();
        return new SiteStatusDto("cadence", "CadenceE", $"{_address.ApiBaseUrl.TrimEnd('/')}/health",
            report.Status.ToString(), (int)watch.ElapsedMilliseconds, DateTime.UtcNow, checks);
    }

    /// <summary>The portal's /health, from the address its handoffs go to: the same site, its root.</summary>
    internal static string? PortalHealthUrl(string? webhookUrl) =>
        Uri.TryCreate(webhookUrl?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            ? $"{uri.GetLeftPart(UriPartial.Authority)}/health"
            : null;

    private async Task<SiteStatusDto> PortalAsync(string url)
    {
        var name = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "Member portal";
        var watch = Stopwatch.StartNew();
        try
        {
            using var timeout = new CancellationTokenSource(PortalTimeout);
            using var response = await _http.CreateClient(ClientName).GetAsync(url, timeout.Token);
            var ms = (int)watch.ElapsedMilliseconds;
            var body = await ReadAsync(response, timeout.Token);
            // A /health of the agreed shape names its own status (503 still carries it); any other
            // answer says only whether the site answered well.
            var status = body?.Status ?? (response.IsSuccessStatusCode ? "Healthy" : "Unhealthy");
            var checks = body?.Checks?.Where(c => !string.IsNullOrWhiteSpace(c.Name))
                .Select(c => new SiteCheckDto(Short(c.Name!, 60)!, Short(c.Status, 20) ?? "Unknown", Short(c.Description, 300)))
                .ToList() ?? [];
            return new SiteStatusDto("portal", name, url, Short(status, 20)!, ms, DateTime.UtcNow, checks,
                body == null && !response.IsSuccessStatusCode ? $"It answered {(int)response.StatusCode}." : null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            _logger.LogInformation("The member portal's /health at {Url} did not answer: {Error}", url, ex.GetType().Name);
            return new SiteStatusDto("portal", name, url, "Unreachable", null, DateTime.UtcNow, [],
                ex is HttpRequestException ? "It could not be reached." : $"It did not answer within {PortalTimeout.TotalSeconds:0} seconds.");
        }
    }

    private sealed record HealthBody(string? Status, List<HealthBodyCheck>? Checks);
    private sealed record HealthBodyCheck(string? Name, string? Status, string? Description);

    private static async Task<HealthBody?> ReadAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.Content.Headers.ContentType?.MediaType?.Contains("json", StringComparison.OrdinalIgnoreCase) != true) return null;
        try
        {
            return await response.Content.ReadFromJsonAsync<HealthBody>(new JsonSerializerOptions(JsonSerializerDefaults.Web), ct);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Short(string? text, int max) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Trim().Length <= max ? text.Trim() : text.Trim()[..(max - 1)] + "…";
}
