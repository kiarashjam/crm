using ACI.Domain.Entities;
using ACI.Infrastructure.Services.Payments;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Persistence;

/// <summary>
/// What a member's money from outside leaves behind when the member is merged or deleted: a
/// saved card, online payments still with Stripe, the QR references printed for them, and bank
/// transfers recorded as theirs.
/// </summary>
internal static class MemberPayments
{
    /// <summary>
    /// A merge says the two records were one person: their online payments, references and bank
    /// transfers are the kept member's, so a payment Stripe confirms or a transfer quoting an old
    /// statement's reference still lands on an account. One saved card is kept: the kept
    /// member's if they have one, else the duplicate's.
    /// </summary>
    public static async Task MoveToKeptMemberAsync(AppDbContext db, Guid keepId, Guid dropId, Guid? organizationId, string keptName, CancellationToken ct)
    {
        foreach (var row in await db.ClubOnlinePayments.Where(p => p.MemberId == dropId && p.OrganizationId == organizationId).ToListAsync(ct))
            (row.MemberId, row.MemberName) = (keepId, keptName);
        var keptStatements = await db.ClubPaymentReferences
            .Where(r => r.MemberId == keepId && r.OrganizationId == organizationId && r.Purpose == ClubQrBillService.StatementPurpose)
            .Select(r => new { r.PeriodStartUtc, r.PeriodEndUtc })
            .ToListAsync(ct);
        foreach (var row in await db.ClubPaymentReferences.Where(r => r.MemberId == dropId && r.OrganizationId == organizationId).ToListAsync(ct))
        {
            row.MemberId = keepId;
            // A statement has one reference per member and period (a unique index), and the
            // month's run prints both records' statements: where the kept member has their own,
            // the next print carries it. The duplicate's was printed too, so it still pays the
            // kept member's account when a transfer quotes it; it only stops being the period's.
            if (row.Purpose == ClubQrBillService.StatementPurpose
                && keptStatements.Any(k => k.PeriodStartUtc == row.PeriodStartUtc && k.PeriodEndUtc == row.PeriodEndUtc))
                row.Purpose = ClubQrBillService.MergedStatementPurpose;
        }
        foreach (var row in await db.ClubBankTransactions.Where(t => t.MemberId == dropId && t.OrganizationId == organizationId).ToListAsync(ct))
            (row.MemberId, row.MemberName) = (keepId, keptName);
        // A contract's deposit put on the duplicate's account is the kept member's, as its payment is.
        foreach (var row in await db.ContractDeposits.Where(d => d.MemberId == dropId && d.OrganizationId == organizationId).ToListAsync(ct))
            row.MemberId = keepId;

        var kept = await db.ClubPaymentProfiles.Where(p => p.MemberId == keepId && p.OrganizationId == organizationId).ToListAsync(ct);
        foreach (var dropped in await db.ClubPaymentProfiles.Where(p => p.MemberId == dropId && p.OrganizationId == organizationId).ToListAsync(ct))
        {
            var mine = kept.FirstOrDefault(p => p.Provider == dropped.Provider);
            if (mine == null) dropped.MemberId = keepId;
            else if (mine.PaymentMethodId == null && dropped.PaymentMethodId != null)
            {
                // The kept record has no card and the duplicate does: the card, and what was agreed for it, carry over.
                mine.CustomerId = dropped.CustomerId;
                mine.PaymentMethodId = dropped.PaymentMethodId;
                mine.PaymentMethodType = dropped.PaymentMethodType;
                mine.Brand = dropped.Brand;
                mine.Last4 = dropped.Last4;
                mine.ExpMonth = dropped.ExpMonth;
                mine.ExpYear = dropped.ExpYear;
                mine.SavedAtUtc = dropped.SavedAtUtc;
                mine.AutoPayDues = dropped.AutoPayDues;
                mine.AutoPayAgreedAtUtc = dropped.AutoPayAgreedAtUtc;
                mine.AutoPayAgreedBy = dropped.AutoPayAgreedBy;
                db.ClubPaymentProfiles.Remove(dropped);
            }
            else db.ClubPaymentProfiles.Remove(dropped);
        }
    }

    /// <summary>
    /// A member deleted is charged nothing more: their saved card is forgotten here. The rest
    /// (online payments, references, transfers) stays as the record of what happened.
    /// </summary>
    public static async Task ForgetDeletedMemberAsync(AppDbContext db, Guid memberId, Guid? organizationId, CancellationToken ct)
    {
        foreach (var profile in await db.ClubPaymentProfiles.Where(p => p.MemberId == memberId && p.OrganizationId == organizationId).ToListAsync(ct))
            db.ClubPaymentProfiles.Remove(profile);
    }
}
