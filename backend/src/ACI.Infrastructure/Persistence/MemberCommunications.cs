using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Persistence;

/// <summary>What a member merge does to the duplicate's consent and messages.</summary>
internal static class MemberCommunications
{
    /// <summary>
    /// Moves the dropped member's consent answers to the member kept, inside the merge's
    /// transaction. Saves once on the way, so a database never sees two standing answers
    /// for one channel and purpose (the unique index would refuse it).
    /// </summary>
    /// <remarks>
    /// Left behind, the duplicate's answers pointed at a member who no longer exists: a
    /// person who unsubscribed under one of their two records was emailed news again once
    /// the records were merged. Both records are one person, so the latest answer either of
    /// them holds for a channel and purpose is the one that stands; every answer stays in
    /// the history.
    /// </remarks>
    public static async Task MoveToKeptMemberAsync(AppDbContext db, Guid keepId, Guid dropId, Guid? organizationId, CancellationToken ct)
    {
        if (organizationId is not Guid org) return;
        var rows = await db.ConsentRecords
            .Where(c => c.OrganizationId == org && c.SubjectType == ConsentSubjects.Member && (c.SubjectId == keepId || c.SubjectId == dropId))
            .ToListAsync(ct);
        if (rows.Count == 0) return;

        foreach (var answers in rows.Where(r => r.IsCurrent).GroupBy(r => (r.Channel, r.Purpose)))
        {
            var standing = answers.OrderByDescending(r => r.RecordedAtUtc).First();
            foreach (var row in answers) row.IsCurrent = ReferenceEquals(row, standing);
        }
        await db.SaveChangesAsync(ct);

        foreach (var row in rows.Where(r => r.SubjectId == dropId)) row.SubjectId = keepId;
    }
}
