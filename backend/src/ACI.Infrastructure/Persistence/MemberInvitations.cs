using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Persistence;

/// <summary>
/// What a member's invitations to the club's events leave behind when the member is merged or
/// deleted (see ClubRepository).
/// </summary>
internal static class MemberInvitations
{
    /// <summary>
    /// A merge says the two records were one person: the duplicate's invitations are the kept
    /// member's, with their reply links still working, unless the kept member was invited to the
    /// same event too (one invitation per member and event), when the kept member's stands.
    /// </summary>
    public static async Task MoveToKeptMemberAsync(AppDbContext db, Guid keepId, Guid dropId, Guid? organizationId, string name, CancellationToken ct)
    {
        var keptEvents = (await db.ClubEventInvitations.Where(i => i.MemberId == keepId && i.OrganizationId == organizationId)
            .Select(i => i.EventId).ToListAsync(ct)).ToHashSet();
        foreach (var invitation in await db.ClubEventInvitations.Where(i => i.MemberId == dropId && i.OrganizationId == organizationId).ToListAsync(ct))
        {
            if (!keptEvents.Add(invitation.EventId))
            {
                db.ClubEventInvitations.Remove(invitation);
                continue;
            }
            invitation.MemberId = keepId;
            invitation.MemberName = name.Length <= 200 ? name : name[..200];
            invitation.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// A member deleted is invited to nothing: their invitations go, and their reply links with
    /// them, which would otherwise answer for nobody. Their places on event lists stay, as the
    /// lists keep them by name.
    /// </summary>
    public static async Task ForgetDeletedMemberAsync(AppDbContext db, Guid memberId, Guid? organizationId, CancellationToken ct) =>
        db.ClubEventInvitations.RemoveRange(await db.ClubEventInvitations.Where(i => i.MemberId == memberId && i.OrganizationId == organizationId).ToListAsync(ct));
}
