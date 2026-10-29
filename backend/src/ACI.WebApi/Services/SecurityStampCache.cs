using ACI.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace ACI.WebApi.Services;

/// <summary>
/// Each user's current security stamp, kept for a short while so checking it does not
/// cost a database read on every request.
/// </summary>
/// <remarks>
/// In memory and per instance, like <see cref="ACI.Application.Services.AuthAttemptLimiter"/>.
/// This instance forgets a stamp the moment it changes; another instance of a scaled-out
/// API would accept an old token for up to <see cref="Lifetime"/> longer.
/// </remarks>
public sealed class SecurityStampCache : ISecurityStampCache
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    private readonly IMemoryCache _cache;

    public SecurityStampCache(IMemoryCache cache) => _cache = cache;

    private static string Key(Guid userId) => "sstamp:" + userId;

    /// <summary>The user's stamp ("" before the first change), or null when the user no longer exists.</summary>
    public async Task<string?> CurrentAsync(Guid userId, IUserRepository users, CancellationToken ct)
    {
        if (_cache.TryGetValue(Key(userId), out string? cached))
            return cached;

        var stamp = await users.GetSecurityStampAsync(userId, ct);
        _cache.Set(Key(userId), stamp, Lifetime);
        return stamp;
    }

    public void Forget(Guid userId) => _cache.Remove(Key(userId));
}
