using System.Security.Cryptography;
using System.Text;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services.SignIn;

/// <summary>Sign-in steps in progress, in the database (see <see cref="AuthChallenge"/>).</summary>
public sealed class AuthChallengeStore : IAuthChallenges
{
    /// <summary>Steps nobody finished are swept this many at a time, as new ones are started.</summary>
    private const int SweepBatch = 50;

    private readonly AppDbContext _db;
    private readonly TimeProvider _clock;

    public AuthChallengeStore(AppDbContext db, TimeProvider? clock = null)
    {
        _db = db;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task CreateAsync(string purpose, string secret, Guid? userId, string payload, TimeSpan lifetime, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow().UtcDateTime;

        // Abandoned steps (a tab closed at Google's page, a passkey prompt cancelled) would
        // otherwise stay for ever. Sign-ins are rare enough that sweeping a few as each new
        // step starts keeps the table small without a job of its own.
        var stale = await _db.AuthChallenges
            .Where(c => c.ExpiresAtUtc < now)
            .OrderBy(c => c.ExpiresAtUtc)
            .Take(SweepBatch)
            .ToListAsync(ct);
        _db.AuthChallenges.RemoveRange(stale);

        _db.AuthChallenges.Add(new AuthChallenge
        {
            Id = Guid.NewGuid(),
            Purpose = purpose,
            KeyHash = Hash(secret),
            UserId = userId,
            Payload = payload,
            CreatedAtUtc = now,
            ExpiresAtUtc = now + lifetime,
        });
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another request swept the same stale rows first; they are gone either way.
            foreach (var entry in _db.ChangeTracker.Entries<AuthChallenge>().Where(e => e.State == EntityState.Deleted).ToList())
                entry.State = EntityState.Detached;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<AuthChallenge?> TakeAsync(string purpose, string secret, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(secret)) return null;
        var hash = Hash(secret);

        // Under load SQL Server can end a race for one step in a deadlock (error 1205)
        // instead of letting the loser delete nothing: one request's read looks the row up
        // through the (Purpose, KeyHash) index and then the row itself, while another's
        // delete holds the row and wants that index entry. The request chosen as the victim
        // was rolled back and simply tries again; by then the step is gone, or its delete
        // waits for the winner and removes nothing. Either way it is refused like any other
        // loser, rather than answering 500. No caller holds a transaction around this, so
        // trying again is safe.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await TakeOnceAsync(purpose, hash, ct);
            }
            catch (Exception ex) when (attempt < 3 && IsDeadlockVictim(ex))
            {
            }
        }
    }

    private async Task<AuthChallenge?> TakeOnceAsync(string purpose, string hash, CancellationToken ct)
    {
        var step = await _db.AuthChallenges.FirstOrDefaultAsync(c => c.Purpose == purpose && c.KeyHash == hash, ct);
        if (step == null) return null;

        // Deleting is what claims it: of two requests racing with the same secret, the
        // second deletes nothing and is refused, so a step is finished once.
        _db.AuthChallenges.Remove(step);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.Entry(step).State = EntityState.Detached;
            return null;
        }
        catch (DbUpdateException)
        {
            // Leave no pending delete behind for a second try, or for the caller's next save.
            _db.Entry(step).State = EntityState.Detached;
            throw;
        }

        return step.ExpiresAtUtc < _clock.GetUtcNow().UtcDateTime ? null : step;
    }

    /// <summary>SQL Server chose this request as a deadlock victim, whether reading or saving.</summary>
    private static bool IsDeadlockVictim(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
            if (e is SqlException { Number: 1205 }) return true;
        return false;
    }

    /// <summary>A random secret for a step, base64url: 256 bits, too many to guess.</summary>
    public static string NewSecret() => Base64Url(RandomNumberGenerator.GetBytes(32));

    public static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Hash(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();
}
