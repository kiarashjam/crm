using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>A charge past its due date and not paid in full: what is still owed on it, after credit the member holds.</summary>
internal sealed record OverdueCharge(Guid Id, Guid MemberId, string Description, decimal Owed, string Currency, DateTime DueAtUtc, Guid? SubscriptionId);

/// <summary>
/// What members owe past its due date, worked out one way for everything that asks: the
/// billing's reminders, past-due dues and lapses, a member's at-risk score, and whether a
/// member is in good standing to propose or second someone.
/// </summary>
/// <remarks>
/// Taken out of the billing run so the at-risk score and the sponsor rule read overdue exactly
/// as the billing chases it. Two copies of the rule would drift: a member the billing no longer
/// chased (their credit covers the charge) would still count as owing money on the score.
/// </remarks>
internal static class ClubOverdue
{
    /// <summary>The workspace's charges past their due date with something still to pay, oldest first; with <paramref name="memberIds"/>, only theirs.</summary>
    public static async Task<List<OverdueCharge>> ListAsync(AppDbContext db, Guid organizationId, DateTime now,
        IReadOnlyCollection<Guid>? memberIds, CancellationToken ct)
    {
        var members = memberIds?.ToList();
        var candidates = await db.ClubCharges.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && (members == null || members.Contains(c.MemberId))
                && c.ReversesChargeId == null && c.DueAtUtc != null && c.DueAtUtc < now && c.Amount > 0)
            .Select(c => new { c.Id, c.MemberId, c.Description, c.Amount, c.Currency, Due = c.DueAtUtc!.Value, c.SubscriptionId })
            .ToListAsync(ct);
        if (candidates.Count == 0) return [];
        var ids = candidates.Select(c => c.Id).ToList();
        var voided = (await db.ClubCharges.AsNoTracking()
                .Where(c => c.OrganizationId == organizationId && c.ReversesChargeId != null && ids.Contains(c.ReversesChargeId.Value))
                .Select(c => c.ReversesChargeId!.Value).ToListAsync(ct))
            .ToHashSet();
        var paid = await db.ClubPaymentAllocations.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId && ids.Contains(a.ChargeId))
            .GroupBy(a => a.ChargeId).Select(g => new { ChargeId = g.Key, Sum = g.Sum(a => a.Amount) })
            .ToDictionaryAsync(g => g.ChargeId, g => g.Sum, ct);
        var owing = candidates.Where(c => !voided.Contains(c.Id))
            .Select(c => new OverdueCharge(c.Id, c.MemberId, c.Description, c.Amount - paid.GetValueOrDefault(c.Id), c.Currency, c.Due, c.SubscriptionId))
            .Where(c => c.Owed > 0)
            .OrderBy(c => c.DueAtUtc)
            .ToList();
        return await LessCreditHeldAsync(db, organizationId, owing, ct);
    }

    /// <summary>
    /// The overdue charges less what credit their members already hold, the credit set
    /// against the charges that fell due first.
    /// </summary>
    /// <remarks>
    /// Credit on an account (an opening balance in the member's favour, a correction by hand)
    /// pays no charge until someone applies it to one, so the ledger shows the charge as
    /// owing; but the member owes the club nothing for the part the credit covers. Chased as
    /// it stands, a member in credit was sent payment reminders and lapsed. The credit is
    /// worked out as the ledger works it out when it is applied (each payment or credit, less
    /// what was taken back from it and what it has paid already).
    /// </remarks>
    private static async Task<List<OverdueCharge>> LessCreditHeldAsync(AppDbContext db, Guid organizationId, List<OverdueCharge> overdue, CancellationToken ct)
    {
        if (overdue.Count == 0) return overdue;
        var memberIds = overdue.Select(c => c.MemberId).Distinct().ToList();
        var payments = await db.ClubPayments.AsNoTracking()
            .Where(p => p.OrganizationId == organizationId && memberIds.Contains(p.MemberId))
            .Select(p => new { p.Id, p.MemberId, p.Amount, p.ReversesPaymentId })
            .ToListAsync(ct);
        var originals = payments.Where(p => p.ReversesPaymentId == null).Select(p => p.Id).ToList();
        var allocated = await db.ClubPaymentAllocations.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId && originals.Contains(a.PaymentId))
            .GroupBy(a => a.PaymentId).Select(g => new { PaymentId = g.Key, Sum = g.Sum(a => a.Amount) })
            .ToDictionaryAsync(g => g.PaymentId, g => g.Sum, ct);
        var reversed = payments.Where(p => p.ReversesPaymentId != null)
            .GroupBy(p => p.ReversesPaymentId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));
        var credit = payments.Where(p => p.ReversesPaymentId == null)
            .GroupBy(p => p.MemberId)
            .ToDictionary(g => g.Key, g => g.Sum(p => Math.Max(0, p.Amount + reversed.GetValueOrDefault(p.Id) - allocated.GetValueOrDefault(p.Id))));

        var left = new List<OverdueCharge>(overdue.Count);
        foreach (var charge in overdue)
        {
            var held = credit.GetValueOrDefault(charge.MemberId);
            if (held <= 0)
            {
                left.Add(charge);
                continue;
            }
            var covered = Math.Min(held, charge.Owed);
            credit[charge.MemberId] = held - covered;
            if (charge.Owed > covered) left.Add(charge with { Owed = charge.Owed - covered });
        }
        return left;
    }
}
