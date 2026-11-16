using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Persistence;

/// <summary>
/// What a member's sales and POS tickets leave behind when the member is merged or deleted:
/// the codes that name them at the POS, the sales they bought and the tickets posted to them.
/// </summary>
internal static class MemberPos
{
    /// <summary>
    /// A merge says the two records were one person: their codes name the kept member, so a
    /// card scanned at the POS still finds an account, and their sales and tickets are the kept
    /// member's, as the ledger entries they posted are. A code both records carried is kept once.
    /// </summary>
    public static async Task MoveToKeptMemberAsync(AppDbContext db, Guid keepId, Guid dropId, Guid? organizationId, string keptName, CancellationToken ct)
    {
        var kept = (await db.ClubMemberCodes.Where(c => c.MemberId == keepId && c.OrganizationId == organizationId)
            .Select(c => new { c.Kind, c.Code }).ToListAsync(ct)).Select(c => (c.Kind, c.Code)).ToHashSet();
        foreach (var code in await db.ClubMemberCodes.Where(c => c.MemberId == dropId && c.OrganizationId == organizationId).ToListAsync(ct))
        {
            if (kept.Contains((code.Kind, code.Code))) db.ClubMemberCodes.Remove(code);
            else code.MemberId = keepId;
        }
        foreach (var sale in await db.ClubSales.Where(s => s.MemberId == dropId && s.OrganizationId == organizationId).ToListAsync(ct))
            (sale.MemberId, sale.MemberName) = (keepId, keptName);
        foreach (var sale in await db.ClubSales.Where(s => s.BilledMemberId == dropId && s.OrganizationId == organizationId).ToListAsync(ct))
            sale.BilledMemberId = keepId;
        foreach (var ticket in await db.ClubPosTickets.Where(t => t.MemberId == dropId && t.OrganizationId == organizationId).ToListAsync(ct))
            (ticket.MemberId, ticket.MemberName) = (keepId, keptName);
    }

    /// <summary>
    /// A member deleted is named by no code: a card of theirs scanned at the POS later finds
    /// nobody, and the ticket waits for staff instead of posting to an account nobody has. Their
    /// sales and tickets stay, as the record of what was sold, as their ledger entries do.
    /// </summary>
    public static async Task ForgetDeletedMemberAsync(AppDbContext db, Guid memberId, Guid? organizationId, CancellationToken ct)
    {
        foreach (var code in await db.ClubMemberCodes.Where(c => c.MemberId == memberId && c.OrganizationId == organizationId).ToListAsync(ct))
            db.ClubMemberCodes.Remove(code);
    }
}
