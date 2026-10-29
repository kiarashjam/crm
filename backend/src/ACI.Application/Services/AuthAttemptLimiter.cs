using System.Collections.Concurrent;

namespace ACI.Application.Services;

/// <summary>
/// Counts failed sign-in steps per account and stops them after a few.
/// </summary>
/// <remarks>
/// Keyed by the account (email or user id), not the caller's IP: behind Azure's front
/// end the connection address may be the proxy's, and X-Forwarded-For is whatever the
/// caller says it is. Per-account limits are what stop guessing one password or one
/// six-digit code, which is the attack that matters here.
///
/// In memory, so the counts reset when the app restarts and are per instance. That is
/// enough for one App Service instance; a scaled-out API needs a shared store.
/// </remarks>
public sealed class AuthAttemptLimiter
{
    /// <summary>Failed attempts allowed inside one window.</summary>
    public const int MaxFailures = 5;

    /// <summary>Password reset (or email confirmation) emails per address inside one window.</summary>
    public const int MaxResetEmails = 3;

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, Bucket> _buckets = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeProvider _clock;

    public AuthAttemptLimiter(TimeProvider? clock = null)
    {
        _clock = clock ?? TimeProvider.System;
    }

    public static string Login(string email) => "login:" + email.Trim();
    public static string TwoFactor(Guid userId) => "2fa:" + userId;
    public static string PasswordReset(string email) => "reset:" + email.Trim();
    public static string VerificationEmail(Guid userId) => "verify:" + userId;

    /// <summary>Whether this key has used up its attempts for the current window.</summary>
    public bool IsBlocked(string key, int limit = MaxFailures)
    {
        var now = _clock.GetUtcNow();
        return _buckets.TryGetValue(key, out var bucket) && bucket.Count(now) >= limit;
    }

    /// <summary>Count one attempt (a failure, or a reset email sent) against this key.</summary>
    public void Record(string key)
    {
        var now = _clock.GetUtcNow();
        _buckets.AddOrUpdate(key, _ => Bucket.Start(now), (_, b) => b.Add(now));
        if (_buckets.Count > 10_000) Sweep(now);
    }

    /// <summary>A success clears the count, so one typo does not linger.</summary>
    public void Reset(string key) => _buckets.TryRemove(key, out _);

    private void Sweep(DateTimeOffset now)
    {
        foreach (var (key, bucket) in _buckets)
        {
            if (bucket.Count(now) == 0) _buckets.TryRemove(key, out _);
        }
    }

    private sealed record Bucket(DateTimeOffset StartedAt, int Attempts)
    {
        public static Bucket Start(DateTimeOffset now) => new(now, 1);

        public int Count(DateTimeOffset now) => now - StartedAt < Window ? Attempts : 0;

        public Bucket Add(DateTimeOffset now) => Count(now) == 0 ? Start(now) : this with { Attempts = Attempts + 1 };
    }
}
