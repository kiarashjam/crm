using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Persistence;

/// <summary>
/// What the club keeps for a CRM company or contact that is its client (a house account on the
/// ledger, a corporate membership, private events), when the company or contact is deleted or
/// merged into another.
/// </summary>
/// <remarks>
/// A client's account is keyed by the company's or contact's id, and every charge, payment and
/// subscription on it names that id. Deleting the record would leave entries on the books for
/// someone nobody can find, so a company or contact with money on its account, a corporate
/// membership or a private event still to come is not deleted; a merge moves all of it to the
/// record kept, as a member merge moves a member's.
/// </remarks>
internal static class ClubClientRecords
{
    /// <summary>What the club keeps the company for, in words for the refusal; null when nothing.</summary>
    public static async Task<string?> WhyCompanyIsKeptAsync(AppDbContext db, Guid companyId, Guid? organizationId, CancellationToken ct)
    {
        if (await db.ClubCorporateMemberships.AnyAsync(m => m.CompanyId == companyId && m.OrganizationId == organizationId, ct))
            return "a corporate membership at the club";
        if (await HasMoneyAsync(db, companyId, organizationId, ct)) return "charges or payments on its house account at the club";
        if (await ComingEvents(db, organizationId).AnyAsync(e => e.CompanyId == companyId, ct)) return "a private event at the club still to come";
        return null;
    }

    /// <summary>What the club keeps the contact for, in words for the refusal; null when nothing.</summary>
    public static async Task<string?> WhyContactIsKeptAsync(AppDbContext db, Guid contactId, Guid? organizationId, CancellationToken ct)
    {
        if (await HasMoneyAsync(db, contactId, organizationId, ct)) return "charges or payments on their house account at the club";
        if (await ComingEvents(db, organizationId).AnyAsync(e => e.ContactId == contactId, ct)) return "a private event at the club still to come";
        return null;
    }

    /// <summary>
    /// A company deleted (nothing kept it): an account opened for it with nothing on it goes, and
    /// its past private events stop naming it.
    /// </summary>
    public static async Task ForgetDeletedCompanyAsync(AppDbContext db, Guid companyId, Guid? organizationId, CancellationToken ct)
    {
        db.ClubHouseAccounts.RemoveRange(await db.ClubHouseAccounts.Where(a => a.MemberId == companyId && a.OrganizationId == organizationId).ToListAsync(ct));
        foreach (var e in await db.ClubPrivateEvents.Where(e => e.CompanyId == companyId && e.OrganizationId == organizationId).ToListAsync(ct))
        {
            e.CompanyId = null;
            e.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// A contact deleted (nothing kept them): an empty account of theirs goes, their past private
    /// events stop naming them, and no corporate membership sends its statements to them.
    /// </summary>
    public static async Task ForgetDeletedContactAsync(AppDbContext db, Guid contactId, Guid? organizationId, CancellationToken ct)
    {
        db.ClubHouseAccounts.RemoveRange(await db.ClubHouseAccounts
            .Where(a => a.MemberId == contactId && a.ContactId == contactId && a.OrganizationId == organizationId).ToListAsync(ct));
        foreach (var e in await db.ClubPrivateEvents.Where(e => e.ContactId == contactId && e.OrganizationId == organizationId).ToListAsync(ct))
        {
            e.ContactId = null;
            e.UpdatedAtUtc = DateTime.UtcNow;
        }
        foreach (var m in await db.ClubCorporateMemberships.Where(m => m.BillingContactId == contactId && m.OrganizationId == organizationId).ToListAsync(ct))
        {
            m.BillingContactId = null;
            m.UpdatedAtUtc = DateTime.UtcNow;
        }
    }

    /// <summary>The company merged into another: its corporate memberships, private events and account are the kept company's.</summary>
    public static async Task MoveCompanyAsync(AppDbContext db, Guid keepId, Guid dropId, Guid? organizationId, string keepName, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        foreach (var m in await db.ClubCorporateMemberships.Where(m => m.CompanyId == dropId && m.OrganizationId == organizationId).ToListAsync(ct))
        {
            (m.CompanyId, m.CompanyName, m.UpdatedAtUtc) = (keepId, Clip(keepName, 200), now);
        }
        foreach (var e in await db.ClubPrivateEvents.Where(e => e.CompanyId == dropId && e.OrganizationId == organizationId).ToListAsync(ct))
        {
            (e.CompanyId, e.UpdatedAtUtc) = (keepId, now);
        }
        await MoveAccountAsync(db, keepId, dropId, organizationId, keepName, ClubAccountHolders.Company, ct);
    }

    /// <summary>The contact merged into another: their private events, billing for corporate memberships and account are the kept contact's.</summary>
    public static async Task MoveContactAsync(AppDbContext db, Guid keepId, Guid dropId, Guid? organizationId, string keepName, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        foreach (var e in await db.ClubPrivateEvents.Where(e => e.ContactId == dropId && e.OrganizationId == organizationId).ToListAsync(ct))
        {
            (e.ContactId, e.UpdatedAtUtc) = (keepId, now);
        }
        foreach (var m in await db.ClubCorporateMemberships.Where(m => m.BillingContactId == dropId && m.OrganizationId == organizationId).ToListAsync(ct))
        {
            (m.BillingContactId, m.UpdatedAtUtc) = (keepId, now);
        }
        await MoveAccountAsync(db, keepId, dropId, organizationId, keepName, ClubAccountHolders.Contact, ct);
    }

    /// <summary>
    /// The entries on the dropped record's account name the kept record, under its name; no
    /// amount or date changes. The kept record's account takes them, or the dropped record's
    /// account becomes the kept record's when it has none.
    /// </summary>
    private static async Task MoveAccountAsync(AppDbContext db, Guid keepId, Guid dropId, Guid? organizationId, string keepName, string kind, CancellationToken ct)
    {
        var name = Clip(keepName, 200);
        foreach (var row in await db.ClubCharges.Where(c => c.MemberId == dropId && c.OrganizationId == organizationId).ToListAsync(ct))
            (row.MemberId, row.MemberName) = (keepId, name);
        foreach (var row in await db.ClubPayments.Where(p => p.MemberId == dropId && p.OrganizationId == organizationId).ToListAsync(ct))
            (row.MemberId, row.MemberName) = (keepId, name);
        foreach (var row in await db.ClubSubscriptions.Where(s => s.MemberId == dropId && s.OrganizationId == organizationId).ToListAsync(ct))
            (row.MemberId, row.MemberName) = (keepId, name);
        foreach (var row in await db.ClubPaymentReferences.Where(r => r.MemberId == dropId && r.OrganizationId == organizationId).ToListAsync(ct))
            row.MemberId = keepId;
        foreach (var e in await db.ClubPrivateEvents.Where(e => e.BillToAccountId == dropId && e.OrganizationId == organizationId).ToListAsync(ct))
            e.BillToAccountId = keepId;

        var dropAccount = await db.ClubHouseAccounts.FirstOrDefaultAsync(a => a.MemberId == dropId && a.OrganizationId == organizationId, ct);
        if (dropAccount == null) return;
        var keepAccount = await db.ClubHouseAccounts.FirstOrDefaultAsync(a => a.MemberId == keepId && a.OrganizationId == organizationId, ct);
        if (keepAccount != null)
        {
            db.ClubHouseAccounts.Remove(dropAccount);
            if (dropAccount.LastEntryAtUtc > (keepAccount.LastEntryAtUtc ?? DateTime.MinValue)) keepAccount.LastEntryAtUtc = dropAccount.LastEntryAtUtc;
            return;
        }
        dropAccount.MemberId = keepId;
        if (kind == ClubAccountHolders.Company) dropAccount.CompanyId = keepId;
        else dropAccount.ContactId = keepId;
    }

    private static IQueryable<ClubPrivateEvent> ComingEvents(AppDbContext db, Guid? organizationId) =>
        db.ClubPrivateEvents.Where(e => e.OrganizationId == organizationId
            && (e.Status == PrivateEventStatuses.Enquiry || e.Status == PrivateEventStatuses.Confirmed));

    private static async Task<bool> HasMoneyAsync(AppDbContext db, Guid accountId, Guid? organizationId, CancellationToken ct) =>
        await db.ClubCharges.AnyAsync(c => c.MemberId == accountId && c.OrganizationId == organizationId, ct)
        || await db.ClubPayments.AnyAsync(p => p.MemberId == accountId && p.OrganizationId == organizationId, ct)
        || await db.ClubSubscriptions.AnyAsync(s => s.MemberId == accountId && s.OrganizationId == organizationId, ct);

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];
}
