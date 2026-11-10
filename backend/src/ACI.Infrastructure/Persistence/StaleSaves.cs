using ACI.Application.Common;
using ACI.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Persistence;

/// <summary>
/// Saving a record that someone else saved after this request read it.
/// </summary>
internal static class StaleSaves
{
    /// <summary>
    /// Saves, or throws <see cref="ConcurrencyConflictException"/> when a versioned record
    /// changed after this request read it.
    /// </summary>
    /// <remarks>
    /// The refused save was one unit, so everything it would have written is let go of:
    /// left tracked, a later save in the same request would write half of it, such as the
    /// line recording a stage move that never happened.
    /// </remarks>
    public static async Task SaveRefusingStaleAsync(this AppDbContext db, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            var pending = db.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .ToList();
            foreach (var entry in pending) entry.State = EntityState.Detached;
            throw new ConcurrencyConflictException();
        }
    }

    /// <summary>
    /// Makes the save of <paramref name="tracked"/> refused unless the row still holds the
    /// version <paramref name="readCopy"/> was read with.
    /// </summary>
    /// <remarks>
    /// For a repository that loads a fresh tracked row and copies the caller's fields onto
    /// it: without this, the check is against the fresh read, so whatever someone saved
    /// between the caller's read and this one was quietly written over with the caller's
    /// older values.
    /// </remarks>
    public static void ExpectVersionOf<T>(this AppDbContext db, T tracked, T readCopy) where T : class, IVersioned
    {
        if (ReferenceEquals(tracked, readCopy) || readCopy.Version == Guid.Empty) return;
        db.Entry(tracked).Property(nameof(IVersioned.Version)).OriginalValue = readCopy.Version;
    }
}
