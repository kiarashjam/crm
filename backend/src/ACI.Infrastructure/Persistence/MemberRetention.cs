using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Persistence;

/// <summary>
/// What a member's retention and referral records leave behind when the member is merged or
/// deleted: their at-risk score, their renewals on the board, the complaints or concerns about
/// them, the referral bonuses that name them, and the links that name them as the member who
/// referred, proposed or seconded someone.
/// </summary>
internal static class MemberRetention
{
    /// <summary>
    /// A merge says the two records were one person. The kept member's score stands (the next
    /// run works it out from the merged records) and the duplicate's goes; the duplicate's
    /// renewals move unless the kept member has one for the same end (of the same subscription);
    /// their concerns move. The members and applications the duplicate referred, proposed or
    /// seconded name the kept member (one that would name the kept member as their own referrer
    /// names nobody), and a bonus row moves unless the kept member already has one of the same
    /// kind, when it stays under the duplicate's id as the record of what was given.
    /// </summary>
    public static async Task MoveToKeptMemberAsync(AppDbContext db, Guid keepId, Guid dropId, Guid? organizationId, CancellationToken ct)
    {
        db.ClubMemberRisks.RemoveRange(await db.ClubMemberRisks.Where(r => r.MemberId == dropId && r.OrganizationId == organizationId).ToListAsync(ct));

        var keptEnds = (await db.ClubRenewals.Where(r => r.MemberId == keepId && r.OrganizationId == organizationId)
                .Select(r => new { r.Source, r.SubscriptionId, r.EndsAtUtc }).ToListAsync(ct))
            .Select(r => (r.Source, r.SubscriptionId, r.EndsAtUtc)).ToHashSet();
        foreach (var renewal in await db.ClubRenewals.Where(r => r.MemberId == dropId && r.OrganizationId == organizationId).ToListAsync(ct))
        {
            if (keptEnds.Add((renewal.Source, renewal.SubscriptionId, renewal.EndsAtUtc))) renewal.MemberId = keepId;
            else db.ClubRenewals.Remove(renewal);
        }
        foreach (var concern in await db.ClubMemberConcerns.Where(c => c.MemberId == dropId && c.OrganizationId == organizationId).ToListAsync(ct))
            concern.MemberId = keepId;

        foreach (var member in await db.ClubMembers.Where(m => m.ReferredByMemberId == dropId && m.OrganizationId == organizationId).ToListAsync(ct))
            member.ReferredByMemberId = member.Id == keepId ? null : keepId;
        foreach (var application in await db.ClubApplications
                     .Where(a => a.OrganizationId == organizationId
                         && (a.ReferredByMemberId == dropId || a.ProposerMemberId == dropId || a.SeconderMemberId == dropId))
                     .ToListAsync(ct))
        {
            if (application.ReferredByMemberId == dropId) application.ReferredByMemberId = application.MemberId == keepId ? null : keepId;
            if (application.ProposerMemberId == dropId) application.ProposerMemberId = keepId;
            if (application.SeconderMemberId == dropId) application.SeconderMemberId = keepId;
        }

        foreach (var bonus in await db.ClubReferralBonuses.Where(b => b.ReferrerMemberId == dropId && b.OrganizationId == organizationId).ToListAsync(ct))
            bonus.ReferrerMemberId = keepId;
        // One bonus per member brought in (the unique index): the kept member's stands.
        if (!await db.ClubReferralBonuses.AnyAsync(b => b.ReferredMemberId == keepId && b.OrganizationId == organizationId, ct))
            foreach (var bonus in await db.ClubReferralBonuses.Where(b => b.ReferredMemberId == dropId && b.OrganizationId == organizationId).ToListAsync(ct))
                bonus.ReferredMemberId = keepId;
    }

    /// <summary>
    /// A member deleted has no score, renewals or concerns any more, and names nobody as their
    /// referrer, proposer or seconder: the members and applications that named them name nobody.
    /// Referral bonuses stay, as the record of points or money given, as ledger entries do.
    /// </summary>
    public static async Task ForgetDeletedMemberAsync(AppDbContext db, Guid memberId, Guid? organizationId, CancellationToken ct)
    {
        db.ClubMemberRisks.RemoveRange(await db.ClubMemberRisks.Where(r => r.MemberId == memberId && r.OrganizationId == organizationId).ToListAsync(ct));
        db.ClubRenewals.RemoveRange(await db.ClubRenewals.Where(r => r.MemberId == memberId && r.OrganizationId == organizationId).ToListAsync(ct));
        db.ClubMemberConcerns.RemoveRange(await db.ClubMemberConcerns.Where(c => c.MemberId == memberId && c.OrganizationId == organizationId).ToListAsync(ct));
        foreach (var member in await db.ClubMembers.Where(m => m.ReferredByMemberId == memberId && m.OrganizationId == organizationId).ToListAsync(ct))
            member.ReferredByMemberId = null;
        foreach (var application in await db.ClubApplications
                     .Where(a => a.OrganizationId == organizationId
                         && (a.ReferredByMemberId == memberId || a.ProposerMemberId == memberId || a.SeconderMemberId == memberId))
                     .ToListAsync(ct))
        {
            if (application.ReferredByMemberId == memberId) application.ReferredByMemberId = null;
            if (application.ProposerMemberId == memberId) application.ProposerMemberId = null;
            if (application.SeconderMemberId == memberId) application.SeconderMemberId = null;
        }
    }
}
