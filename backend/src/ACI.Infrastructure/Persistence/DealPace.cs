using ACI.Application.Common;
using ACI.Domain.Entities;

namespace ACI.Infrastructure.Persistence;

/// <summary>When a deal last moved and when it came into its stage, read from its stage history.</summary>
public sealed class DealPaceRow
{
    public Guid DealId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastActivityAtUtc { get; set; }
    public DateTime? LastMoveAtUtc { get; set; }
    /// <summary>Null when no line says when it came into the stage it is in: a deal from before the history was kept.</summary>
    public DateTime? StageEnteredAtUtc { get; set; }

    /// <summary>The last time anything happened to the deal: made, worked on, or moved.</summary>
    public DateTime IdleSinceUtc => StaleDeals.IdleSince(CreatedAtUtc, LastActivityAtUtc, LastMoveAtUtc);
}

/// <summary>
/// The pace of deals, worked out by the database: how long each has been in its stage,
/// and whether anyone has done anything with it lately.
/// </summary>
/// <remarks>
/// <para>
/// A deal is worked on when an activity is logged on it (a call, a note, an email, the
/// app's own timeline lines among them, as the board's "last activity" counts them) or
/// when it moves stage. One with neither since a moment, and created before it, has sat
/// idle since then: <see cref="IdleSince"/> finds those in SQL, so a stage's stale count
/// covers every deal in it, not only the ones on screen.
/// </para>
/// <para>
/// The time in stage is read from the history line that brought the deal into the stage
/// it is in now. A deal from before the history was kept has none, and is reported as
/// not known rather than dated from its creation, which a move by name before then
/// would have made wrong.
/// </para>
/// </remarks>
public static class DealPace
{
    /// <summary>Activities that count for a deal: the workspace's, or in a personal workspace the person's own.</summary>
    private static IQueryable<Activity> ActivitiesIn(AppDbContext db, Guid userId, Guid? organizationId) =>
        organizationId == null
            ? db.Activities.Where(a => a.OrganizationId == null && a.UserId == userId)
            : db.Activities.Where(a => a.OrganizationId == organizationId);

    /// <summary>Each deal's pace, one row per deal.</summary>
    public static IQueryable<DealPaceRow> PaceOf(this IQueryable<Deal> deals, AppDbContext db, Guid userId, Guid? organizationId)
    {
        var activities = ActivitiesIn(db, userId, organizationId);
        return deals.Select(d => new DealPaceRow
        {
            DealId = d.Id,
            CreatedAtUtc = d.CreatedAtUtc,
            LastActivityAtUtc = activities.Where(a => a.DealId == d.Id).Max(a => (DateTime?)a.CreatedAtUtc),
            LastMoveAtUtc = db.DealStageChanges.Where(c => c.DealId == d.Id).Max(c => (DateTime?)c.ChangedAtUtc),
            // Into the stage it is in: by id, or for a deal known only by its stage's name, by that name.
            StageEnteredAtUtc = db.DealStageChanges
                .Where(c => c.DealId == d.Id
                    && ((d.DealStageId != null && c.ToDealStageId == d.DealStageId)
                        || (d.DealStageId == null && c.ToDealStageId == null && c.ToStageName == d.Stage)))
                .Max(c => (DateTime?)c.ChangedAtUtc),
        });
    }

    /// <summary>
    /// The deals nothing has happened to since <paramref name="cutoff"/>: created before
    /// it, with no activity logged and no move made since. Whether they are open is for
    /// the caller to say.
    /// </summary>
    public static IQueryable<Deal> IdleSince(this IQueryable<Deal> deals, AppDbContext db, DateTime cutoff, Guid userId, Guid? organizationId)
    {
        var activities = ActivitiesIn(db, userId, organizationId);
        return deals.Where(d => d.CreatedAtUtc < cutoff
            && !activities.Any(a => a.DealId == d.Id && a.CreatedAtUtc >= cutoff)
            && !db.DealStageChanges.Any(c => c.DealId == d.Id && c.ChangedAtUtc >= cutoff));
    }
}
