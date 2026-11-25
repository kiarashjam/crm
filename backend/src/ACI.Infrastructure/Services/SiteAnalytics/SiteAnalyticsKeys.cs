using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Infrastructure.Services.SiteAnalytics;

/// <summary>
/// The hashes that stand in for a visitor and a session in the site analytics, made with a salt
/// that changes every day.
/// </summary>
/// <remarks>
/// <para>
/// The day's salt is HMAC-SHA256 of the Zurich day with the secret: SiteAnalytics:Secret when
/// the configuration sets it, otherwise a random secret generated once and kept in the database
/// (SiteAnalyticsSecrets, protected with the data-protection keys). A visitor is HMAC of the
/// address and the user agent with the day's salt, a session HMAC of the tab's random id with
/// it; both cut to 128 bits. Neither the address, nor the agent, nor the id is kept, and with a
/// new salt every day the same browser is a different visitor tomorrow.
/// </para>
/// <para>
/// A singleton: the secret is read once, the salts of the last few days are kept.
/// </para>
/// </remarks>
public sealed class SiteAnalyticsKeys
{
    private const int SecretRowId = 1;

    private readonly IServiceScopeFactory _scopes;
    private readonly SiteAnalyticsSettings _settings;
    private readonly ILogger<SiteAnalyticsKeys> _logger;
    private readonly SemaphoreSlim _loading = new(1, 1);
    private readonly ConcurrentDictionary<DateOnly, byte[]> _salts = new();
    private byte[]? _secret;

    public SiteAnalyticsKeys(IServiceScopeFactory scopes, IOptions<SiteAnalyticsSettings> settings, ILogger<SiteAnalyticsKeys> logger)
    {
        _scopes = scopes;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>The hash that counts one browser at one address as one visitor on <paramref name="day"/>.</summary>
    public async Task<string> VisitorAsync(DateOnly day, string address, string userAgent, CancellationToken ct = default) =>
        Hash(await SaltAsync(day, ct), $"v|{address}|{userAgent}");

    /// <summary>The hash that groups one tab's events on <paramref name="day"/>.</summary>
    public async Task<string> SessionAsync(DateOnly day, string sessionId, CancellationToken ct = default) =>
        Hash(await SaltAsync(day, ct), $"s|{sessionId}");

    /// <summary>The day's salt: HMAC-SHA256 of the day with the secret.</summary>
    public async Task<byte[]> SaltAsync(DateOnly day, CancellationToken ct = default)
    {
        if (_salts.TryGetValue(day, out var cached)) return cached;
        var secret = await SecretAsync(ct);
        var salt = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes("site-analytics:" + SiteAnalyticsRules.Format(day)));
        // Only the last few days are ever asked for; the rest are dropped, so the cache stays small.
        if (_salts.Count > 8)
            foreach (var old in _salts.Keys.Where(d => d < day.AddDays(-3)).ToList()) _salts.TryRemove(old, out _);
        return _salts.GetOrAdd(day, salt);
    }

    private static string Hash(byte[] salt, string value) =>
        Convert.ToHexString(HMACSHA256.HashData(salt, Encoding.UTF8.GetBytes(value)), 0, 16).ToLowerInvariant();

    private async Task<byte[]> SecretAsync(CancellationToken ct)
    {
        if (_secret != null) return _secret;
        await _loading.WaitAsync(ct);
        try
        {
            if (_secret != null) return _secret;
            var configured = _settings.Secret?.Trim();
            _secret = !string.IsNullOrEmpty(configured)
                ? Encoding.UTF8.GetBytes(configured)
                : await StoredSecretAsync(ct);
            return _secret;
        }
        finally
        {
            _loading.Release();
        }
    }

    /// <summary>The secret kept in the database, generated and saved the first time.</summary>
    private async Task<byte[]> StoredSecretAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var row = await db.SiteAnalyticsSecrets.AsTracking().FirstOrDefaultAsync(s => s.Id == SecretRowId, ct);
            if (row != null)
            {
                try
                {
                    return Convert.FromBase64String(protector.Unprotect(row.ProtectedSecret));
                }
                catch (Exception ex) when (ex is CryptographicException or FormatException)
                {
                    // The data-protection keys it was protected with are gone (a new host, say).
                    // A new secret only changes today's hashes: visitors already counted today
                    // are counted again once.
                    _logger.LogWarning(ex, "The site analytics secret could not be read; a new one replaces it");
                    var fresh = RandomNumberGenerator.GetBytes(32);
                    row.ProtectedSecret = protector.Protect(Convert.ToBase64String(fresh));
                    row.CreatedAtUtc = DateTime.UtcNow;
                    await db.SaveChangesAsync(ct);
                    return fresh;
                }
            }

            var secret = RandomNumberGenerator.GetBytes(32);
            var created = new SiteAnalyticsSecret
            {
                Id = SecretRowId,
                ProtectedSecret = protector.Protect(Convert.ToBase64String(secret)),
                CreatedAtUtc = DateTime.UtcNow,
            };
            db.SiteAnalyticsSecrets.Add(created);
            try
            {
                await db.SaveChangesAsync(ct);
                return secret;
            }
            catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or ArgumentException)
            {
                // Another server saved one first: read theirs.
                db.Entry(created).State = EntityState.Detached;
                _logger.LogInformation(ex, "Another server saved the site analytics secret first; reading it");
            }
        }
        throw new InvalidOperationException("The site analytics secret could be neither read nor saved.");
    }
}
