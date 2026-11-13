using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Persistence;

/// <summary>
/// What a member's seats on companies' corporate memberships and places on private events'
/// guest lists leave behind when the member is merged or deleted.
/// </summary>
internal static class MemberCorporate
{
    /// <summary>
    /// A merge says the two records were one person. The duplicate's seat moves to the kept
    /// member when the kept member holds none; when both hold one, the kept member's stands and
    /// the duplicate's ends, saying why (a member holds one seat). The seats the duplicate held
    /// before, and the guest lists it is on, name the kept member.
    /// </summary>
    public static async Task MoveToKeptMemberAsync(AppDbContext db, Guid keepId, Guid dropId, Guid? organizationId, string name, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var keepSeated = await db.ClubCorporateNominees.AnyAsync(n => n.MemberId == keepId && n.RemovedAtUtc == null && n.OrganizationId == organizationId, ct);
        var seats = await db.ClubCorporateNominees.Where(n => n.MemberId == dropId && n.OrganizationId == organizationId).ToListAsync(ct);
        foreach (var seat in seats)
        {
            if (seat.RemovedAtUtc == null && keepSeated)
            {
                seat.RemovedAtUtc = now;
                seat.RemovedReason = Clip($"Merged into {name}, who holds a seat already", 500);
            }
            (seat.MemberId, seat.MemberName) = (keepId, Clip(name, 200));
        }
        foreach (var seat in await db.ClubCorporateNominees.Where(n => n.ReplacedByMemberId == dropId && n.OrganizationId == organizationId).ToListAsync(ct))
            seat.ReplacedByMemberId = keepId;
        await TouchMembershipsAsync(db, seats.Select(s => s.CorporateMembershipId), now, ct);

        foreach (var guest in await db.ClubPrivateEventGuests.Where(g => g.MemberId == dropId && g.OrganizationId == organizationId).ToListAsync(ct))
            (guest.MemberId, guest.Name) = (keepId, Clip(name, 200));
    }

    /// <summary>
    /// A member deleted holds no seat any more: their seats go (one they hold is free for the
    /// company), and nobody is named as having taken a seat over from them. On guest lists they
    /// stay by the name they were listed under, no longer pointing at a member who is gone.
    /// </summary>
    public static async Task ForgetDeletedMemberAsync(AppDbContext db, Guid memberId, Guid? organizationId, CancellationToken ct)
    {
        var seats = await db.ClubCorporateNominees.Where(n => n.MemberId == memberId && n.OrganizationId == organizationId).ToListAsync(ct);
        db.ClubCorporateNominees.RemoveRange(seats);
        foreach (var seat in await db.ClubCorporateNominees.Where(n => n.ReplacedByMemberId == memberId && n.OrganizationId == organizationId).ToListAsync(ct))
            seat.ReplacedByMemberId = null;
        await TouchMembershipsAsync(db, seats.Where(s => s.RemovedAtUtc == null).Select(s => s.CorporateMembershipId), DateTime.UtcNow, ct);

        foreach (var guest in await db.ClubPrivateEventGuests.Where(g => g.MemberId == memberId && g.OrganizationId == organizationId).ToListAsync(ct))
            guest.MemberId = null;
    }

    /// <summary>
    /// Seat changes are made against the membership's row (its version), so a nominee added at
    /// the same moment is decided one after the other with this.
    /// </summary>
    private static async Task TouchMembershipsAsync(AppDbContext db, IEnumerable<Guid> membershipIds, DateTime now, CancellationToken ct)
    {
        var ids = membershipIds.Distinct().ToList();
        if (ids.Count == 0) return;
        foreach (var membership in await db.ClubCorporateMemberships.Where(m => ids.Contains(m.Id)).ToListAsync(ct))
        {
            membership.NomineesChangedAtUtc = now;
            membership.UpdatedAtUtc = now;
        }
    }

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];
}
