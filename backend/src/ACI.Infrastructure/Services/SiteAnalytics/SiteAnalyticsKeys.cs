using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services.SiteAnalytics;

/// <summary>
/// The hashes that stand in for a visitor and a session in the site analytics, made with a
/// random salt of the day.
/// </summary>
/// <remarks>
/// <para>
/// Each Zurich day has its own salt: <see cref="SaltBytes"/> bytes from RandomNumberGenerator,
/// made by the first server that needs it and kept, protected with the data-protection keys, in
/// that day's <see cref="SiteAnalyticsSalt"/> row. The day is the row's key, so two servers
/// making one at once cannot both keep theirs: the second reads the first's, and every server
/// hashes the day with the same salt. A salt is made from nothing: once the daily clean-up has
/// deleted it (every salt older than yesterday's, SiteAnalyticsService.PurgeAsync), nobody, the
/// operator included, can make it again, so no hash of that day can be recomputed from an
/// address, and the addresses cannot be tried one by one.
/// </para>
/// <para>
/// A visitor is HMAC-SHA256 of the shortened address (<see cref="ShortenedAddress"/>: IPv4 /24,
/// IPv6 /48) and the user agent with the day's salt; a session HMAC of the tab's random id with
/// the same salt; both cut to 128 bits. Neither the address, nor the agent, nor the id is kept,
/// and with a new salt every day the same browser is a different visitor tomorrow.
/// </para>
/// <para>
/// A singleton: the salts read are kept in memory, today's and yesterday's only.
/// </para>
/// </remarks>
public sealed class SiteAnalyticsKeys
{
    /// <summary>The length of a day's salt.</summary>
    public const int SaltBytes = 32;

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<SiteAnalyticsKeys> _logger;
    private readonly SemaphoreSlim _loading = new(1, 1);
    private readonly ConcurrentDictionary<DateOnly, byte[]> _salts = new();

    public SiteAnalyticsKeys(IServiceScopeFactory scopes, ILogger<SiteAnalyticsKeys> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    /// <summary>The hash that counts one browser at one network (the shortened address) as one visitor on <paramref name="day"/>.</summary>
    public async Task<string> VisitorAsync(DateOnly day, string address, string userAgent, CancellationToken ct = default) =>
        Hash(await SaltAsync(day, ct), $"v|{ShortenedAddress(address)}|{userAgent}");

    /// <summary>The hash that groups one tab's events on <paramref name="day"/>, with the same salt.</summary>
    public async Task<string> SessionAsync(DateOnly day, string sessionId, CancellationToken ct = default) =>
        Hash(await SaltAsync(day, ct), $"s|{sessionId}");

    /// <summary>
    /// The address with its last part cut off: an IPv4 address's /24 ("198.51.100.0"), an IPv6
    /// address's /48 ("2001:db8:1::"), an IPv4 address written as IPv6 as the IPv4 one. What is
    /// not an address ("unknown") is left as it is.
    /// </summary>
    public static string ShortenedAddress(string address)
    {
        if (!IPAddress.TryParse(address, out var ip)) return address;
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        var bytes = ip.GetAddressBytes();
        var kept = ip.AddressFamily == AddressFamily.InterNetwork ? 3 : 6;
        for (var i = kept; i < bytes.Length; i++) bytes[i] = 0;
        return new IPAddress(bytes).ToString();
    }

    /// <summary>The day's salt: its row's, or a new random one saved as its row by the first server to need it.</summary>
    public async Task<byte[]> SaltAsync(DateOnly day, CancellationToken ct = default)
    {
        if (_salts.TryGetValue(day, out var cached)) return cached;
        await _loading.WaitAsync(ct);
        try
        {
            if (_salts.TryGetValue(day, out cached)) return cached;
            var salt = await StoredSaltAsync(day, ct);
            // Only today's and yesterday's are asked for: older ones leave memory as a new day's comes in.
            Forget(day.AddDays(-1));
            _salts[day] = salt;
            return salt;
        }
        finally
        {
            _loading.Release();
        }
    }

    /// <summary>Drops from memory the salts of the days before <paramref name="before"/> (the clean-up deletes their rows).</summary>
    public void Forget(DateOnly before)
    {
        foreach (var day in _salts.Keys.Where(d => d < before).ToList()) _salts.TryRemove(day, out _);
    }

    private static string Hash(byte[] salt, string value) =>
        Convert.ToHexString(HMACSHA256.HashData(salt, Encoding.UTF8.GetBytes(value)), 0, 16).ToLowerInvariant();

    private async Task<byte[]> StoredSaltAsync(DateOnly day, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var protector = scope.ServiceProvider.GetRequiredService<ISecretProtector>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var row = await db.SiteAnalyticsSalts.AsNoTracking().FirstOrDefaultAsync(s => s.Day == day, ct);
            if (row != null)
            {
                if (Unprotect(protector, row.ProtectedSalt) is { } stored) return stored;
                // The data-protection keys it was protected with are gone (a new host, say): a new
                // salt replaces it, and the day's visitors already counted are counted again once.
                _logger.LogWarning("The site analytics salt of {Day} could not be read; a new one replaces it", day);
                await DeleteAsync(db, row, ct);
                continue;
            }

            var salt = RandomNumberGenerator.GetBytes(SaltBytes);
            var created = new SiteAnalyticsSalt
            {
                Day = day,
                ProtectedSalt = protector.Protect(Convert.ToBase64String(salt)),
                CreatedAtUtc = DateTime.UtcNow,
            };
            db.SiteAnalyticsSalts.Add(created);
            try
            {
                await db.SaveChangesAsync(ct);
                return salt;
            }
            catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or ArgumentException)
            {
                // Another server saved the day's salt first (SQL Server: a duplicate key; in
                // memory: an argument error): read theirs.
                db.Entry(created).State = EntityState.Detached;
                _logger.LogInformation(ex, "Another server saved the site analytics salt of {Day} first; reading it", day);
            }
        }
        throw new InvalidOperationException($"The site analytics salt of {day:yyyy-MM-dd} could be neither read nor saved.");
    }

    private static byte[]? Unprotect(ISecretProtector protector, string protectedSalt)
    {
        try
        {
            var salt = Convert.FromBase64String(protector.Unprotect(protectedSalt));
            return salt.Length == SaltBytes ? salt : null;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }

    /// <summary>Deletes the unreadable row, and only it: a salt another server has put in its place meanwhile stays.</summary>
    private static async Task DeleteAsync(AppDbContext db, SiteAnalyticsSalt row, CancellationToken ct)
    {
        if (db.Database.IsRelational())
        {
            await db.SiteAnalyticsSalts.Where(s => s.Day == row.Day && s.ProtectedSalt == row.ProtectedSalt).ExecuteDeleteAsync(ct);
            return;
        }
        // The in-memory test host has no DELETE statements; one process, so no race.
        db.SiteAnalyticsSalts.Remove(row);
        await db.SaveChangesAsync(ct);
        db.Entry(row).State = EntityState.Detached;
    }
}
