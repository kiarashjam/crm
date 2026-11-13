using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class ClubRepository : IClubRepository
{
    private readonly AppDbContext _db;
    public ClubRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ClubMember>> MembersAsync(Guid? organizationId, CancellationToken ct = default) =>
        await _db.ClubMembers.AsNoTracking().Where(m => m.OrganizationId == organizationId).ToListAsync(ct);

    public Task<ClubMember?> MemberAsync(Guid id, Guid? organizationId, CancellationToken ct = default) =>
        _db.ClubMembers.FirstOrDefaultAsync(m => m.Id == id && m.OrganizationId == organizationId, ct);

    public async Task<DateTime?> ErasedAtAsync(Guid? organizationId, string email, CancellationToken ct = default)
    {
        var key = ACI.Application.Common.ErasedAddresses.Key(email);
        if (organizationId is not Guid org || key.Length == 0) return null;
        return await _db.EmailSuppressions.AsNoTracking()
            .Where(s => s.OrganizationId == org && s.Email == key && s.Reason == EmailSuppressionReasons.Erased)
            .Select(s => (DateTime?)s.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
    }

    public async Task SaveMemberAsync(ClubMember member, CancellationToken ct = default)
    {
        var existing = await _db.ClubMembers.FirstOrDefaultAsync(m => m.Id == member.Id, ct);
        if (existing == null) _db.ClubMembers.Add(member);
        else
        {
            existing.FirstName = member.FirstName;
            existing.LastName = member.LastName;
            existing.Email = member.Email;
            existing.Phone = member.Phone;
            existing.Tier = member.Tier;
            existing.Status = member.Status;
            existing.JoinedAtUtc = member.JoinedAtUtc;
            existing.RenewsAtUtc = member.RenewsAtUtc;
            existing.DuesAmount = member.DuesAmount;
            existing.DuesFrequency = member.DuesFrequency;
            existing.HouseAccountBalance = member.HouseAccountBalance;
            existing.ManualBalance = member.ManualBalance;
            existing.Notes = member.Notes;
            existing.ContactId = member.ContactId;
            existing.LeadId = member.LeadId;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteMemberAsync(Guid id, Guid? organizationId, CancellationToken ct = default)
    {
        var row = await MemberAsync(id, organizationId, ct);
        if (row == null) return false;
        // Their places on event lists, the guests they bring and their bookings stay, under the
        // names they were made in, but stop pointing at a member who is gone. Left pointing, a
        // seat that came free for them (or a no-show fee) was posted to an account nobody has;
        // the ledger refused it, and with it the whole change: someone else's cancellation, say.
        foreach (var registration in await _db.ClubEventRegistrations.Where(r => r.MemberId == id && r.OrganizationId == organizationId).ToListAsync(ct))
            registration.MemberId = null;
        foreach (var registration in await _db.ClubEventRegistrations.Where(r => r.HostMemberId == id && r.OrganizationId == organizationId).ToListAsync(ct))
            registration.HostMemberId = null;
        foreach (var booking in await _db.ClubReservations.Where(r => r.MemberId == id && r.OrganizationId == organizationId).ToListAsync(ct))
            booking.MemberId = null;
        // They leave their household (ClubService refuses deleting a primary with others in it).
        // A household nobody is left in goes with them.
        var link = await _db.ClubHouseholdMembers.FirstOrDefaultAsync(l => l.MemberId == id && l.OrganizationId == organizationId, ct);
        if (link != null)
        {
            _db.ClubHouseholdMembers.Remove(link);
            if (!await _db.ClubHouseholdMembers.AnyAsync(l => l.HouseholdId == link.HouseholdId && l.Id != link.Id, ct)
                && await _db.ClubHouseholds.FirstOrDefaultAsync(h => h.Id == link.HouseholdId && h.OrganizationId == organizationId, ct) is { } empty)
                _db.ClubHouseholds.Remove(empty);
        }
        // Nothing more is charged to a card the member saved.
        await MemberPayments.ForgetDeletedMemberAsync(_db, id, organizationId, ct);
        // And no code names them at the POS any more.
        await MemberPos.ForgetDeletedMemberAsync(_db, id, organizationId, ct);
        // Their score, renewals and concerns go, and nobody is named as referred or sponsored by them.
        await MemberRetention.ForgetDeletedMemberAsync(_db, id, organizationId, ct);
        // Their corporate seats go (one they hold is free for the company); guest lists keep them by name.
        await MemberCorporate.ForgetDeletedMemberAsync(_db, id, organizationId, ct);
        _db.ClubMembers.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<(string Member, string Household)?> PrimaryOfHouseholdAsync(Guid memberId, Guid? organizationId, CancellationToken ct = default)
    {
        var link = await _db.ClubHouseholdMembers.AsNoTracking()
            .FirstOrDefaultAsync(l => l.MemberId == memberId && l.OrganizationId == organizationId && l.IsPrimary, ct);
        if (link == null || !await _db.ClubHouseholdMembers.AnyAsync(l => l.HouseholdId == link.HouseholdId && l.Id != link.Id, ct)) return null;
        var member = await _db.ClubMembers.AsNoTracking().Where(m => m.Id == memberId).Select(m => m.FirstName + " " + m.LastName).FirstOrDefaultAsync(ct) ?? "This member";
        var household = await _db.ClubHouseholds.AsNoTracking().Where(h => h.Id == link.HouseholdId).Select(h => h.Name).FirstOrDefaultAsync(ct) ?? "their";
        return (member.Trim(), household);
    }

    public Task<bool> MergeMembersAsync(Guid keepId, Guid dropId, Guid? organizationId, CancellationToken ct = default) =>
        RecordMerge.InTransactionAsync(_db, async () =>
        {
            if (keepId == dropId) return false;
            var keep = await MemberAsync(keepId, organizationId, ct);
            var drop = await MemberAsync(dropId, organizationId, ct);
            if (keep == null || drop == null) return false;
            var name = $"{keep.FirstName} {keep.LastName}".Trim();

            foreach (var visit in await _db.ClubVisits.Where(v => v.MemberId == dropId && v.OrganizationId == organizationId).ToListAsync(ct))
            {
                visit.MemberId = keepId;
                visit.MemberName = name;
            }
            // The duplicate's custom field values fill the kept member's empty fields, and its tags
            // join theirs, before deleting it would take them away (RecordDependents).
            await RecordMerge.MoveFieldsAndTagsAsync(_db, keepId, dropId, ct);
            foreach (var entry in await _db.LoyaltyLedger.Where(e => e.MemberId == dropId && e.OrganizationId == organizationId).ToListAsync(ct))
            {
                entry.MemberId = keepId;
                entry.MemberName = name;
            }

            // The money ledger names the member on each row, with the name they had when it
            // was posted. A merge says the two records were one person all along, so the
            // rows move to the member kept, under their name; no amount or date changes,
            // which is the one way a posted entry is ever touched.
            foreach (var row in await _db.ClubCharges.Where(c => c.MemberId == dropId && c.OrganizationId == organizationId).ToListAsync(ct))
                (row.MemberId, row.MemberName) = (keepId, name);
            foreach (var row in await _db.ClubPayments.Where(p => p.MemberId == dropId && p.OrganizationId == organizationId).ToListAsync(ct))
                (row.MemberId, row.MemberName) = (keepId, name);
            foreach (var row in await _db.ClubSubscriptions.Where(s => s.MemberId == dropId && s.OrganizationId == organizationId).ToListAsync(ct))
                (row.MemberId, row.MemberName) = (keepId, name);
            foreach (var row in await _db.ClubTabs.Where(t => t.MemberId == dropId && t.OrganizationId == organizationId).ToListAsync(ct))
                (row.MemberId, row.MemberName) = (keepId, name);
            foreach (var row in await _db.ClubDonations.Where(d => d.MemberId == dropId && d.OrganizationId == organizationId).ToListAsync(ct))
                (row.MemberId, row.MemberName) = (keepId, name);
            foreach (var row in await _db.ClubGiftCards.Where(g => g.PurchaserMemberId == dropId && g.OrganizationId == organizationId).ToListAsync(ct))
                row.PurchaserMemberId = keepId;
            foreach (var row in await _db.ClubGiftCardTransactions.Where(t => t.MemberId == dropId && t.OrganizationId == organizationId).ToListAsync(ct))
                row.MemberId = keepId;
            // What the duplicate ran up on a household primary's account is the kept member's doing.
            foreach (var row in await _db.ClubCharges.Where(c => c.IncurredByMemberId == dropId && c.OrganizationId == organizationId).ToListAsync(ct))
                (row.IncurredByMemberId, row.IncurredByName) = (keepId, name);
            // The applications that admitted the duplicate admitted the kept member.
            foreach (var row in await _db.ClubApplications.Where(a => a.MemberId == dropId && a.OrganizationId == organizationId).ToListAsync(ct))
                row.MemberId = keepId;
            // Reminders follow the charges they are about, which moved above. A statement sent for a
            // month the kept member already had one for stays under the duplicate's id, as the
            // record of what was sent; the others move, so the next monthly run does not send the
            // kept member a second statement for a month already done.
            foreach (var row in await _db.ClubPaymentReminders.Where(r => r.MemberId == dropId && r.OrganizationId == organizationId).ToListAsync(ct))
                row.MemberId = keepId;
            var keptMonths = (await _db.ClubStatementDeliveries.Where(d => d.MemberId == keepId && d.OrganizationId == organizationId)
                .Select(d => d.PeriodStartUtc).ToListAsync(ct)).ToHashSet();
            foreach (var row in await _db.ClubStatementDeliveries.Where(d => d.MemberId == dropId && d.OrganizationId == organizationId).ToListAsync(ct))
                if (keptMonths.Add(row.PeriodStartUtc)) row.MemberId = keepId;
            // One account per member: the duplicate's entries are the kept member's now.
            var dropAccount = await _db.ClubHouseAccounts.FirstOrDefaultAsync(a => a.MemberId == dropId && a.OrganizationId == organizationId, ct);
            if (dropAccount != null) _db.ClubHouseAccounts.Remove(dropAccount);

            // Their places on event lists (their own tickets, and the guests they bring) and
            // their bookings are the kept member's, under the kept member's name and address.
            // A booking used to find its member by email text, so merging two records left
            // the duplicate's bookings with nobody.
            foreach (var row in await _db.ClubEventRegistrations.Where(r => r.MemberId == dropId && r.OrganizationId == organizationId).ToListAsync(ct))
            {
                (row.MemberId, row.Name) = (keepId, name);
                if (!string.IsNullOrWhiteSpace(keep.Email)) row.Email = keep.Email;
            }
            foreach (var row in await _db.ClubEventRegistrations.Where(r => r.HostMemberId == dropId && r.OrganizationId == organizationId).ToListAsync(ct))
                (row.HostMemberId, row.HostName) = (keepId, name);
            foreach (var row in await _db.ClubReservations.Where(r => r.MemberId == dropId && r.OrganizationId == organizationId).ToListAsync(ct))
            {
                (row.MemberId, row.MemberName) = (keepId, name);
                if (!string.IsNullOrWhiteSpace(keep.Email)) row.MemberEmail = keep.Email;
            }

            // Charges, payments, reservations, gift cards, donations, subscriptions and
            // household links carry the member's id inside their own document. An id is
            // unique, so wherever it appears it is this member.
            var dropText = dropId.ToString();
            var dropUpper = dropText.ToUpperInvariant();
            var keepText = keepId.ToString();
            var documents = await _db.ClubDocuments
                .Where(d => d.OrganizationId == organizationId && (d.Payload.Contains(dropText) || d.Payload.Contains(dropUpper)))
                .ToListAsync(ct);
            foreach (var document in documents)
            {
                document.Payload = Regex.Replace(document.Payload, Regex.Escape(dropText), keepText, RegexOptions.IgnoreCase);
                document.UpdatedAtUtc = DateTime.UtcNow;
            }
            // A person is in one household. The duplicate's place moves to the kept member when
            // they have none; when both have one, the kept member's stands and the duplicate's goes.
            await MergeHouseholdLinksAsync(keepId, dropId, organizationId, ct);

            // Their online payments, QR references, bank transfers and saved card, so a payment
            // confirmed later still lands on an account.
            await MemberPayments.MoveToKeptMemberAsync(_db, keepId, dropId, organizationId, name, ct);

            // Their codes at the POS, their sales and the POS tickets posted to them.
            await MemberPos.MoveToKeptMemberAsync(_db, keepId, dropId, organizationId, name, ct);

            // Consent answers and messages are the person's, so they move too.
            await MemberCommunications.MoveToKeptMemberAsync(_db, keepId, dropId, organizationId, ct);
            await MemberCommunications.MoveMessagesToKeptMemberAsync(_db, keepId, dropId, organizationId, name, ct);

            // Their concerns and renewals, and the members they referred or sponsored, are the kept member's.
            await MemberRetention.MoveToKeptMemberAsync(_db, keepId, dropId, organizationId, ct);
            // Their seat on a company's corporate membership (one a member), and the guest lists they are on.
            await MemberCorporate.MoveToKeptMemberAsync(_db, keepId, dropId, organizationId, name, ct);
            if (keep.ReferredByMemberId == null && drop.ReferredByMemberId is { } referrer && referrer != keepId) keep.ReferredByMemberId = referrer;

            keep.Phone = RecordMerge.Fill(keep.Phone, drop.Phone);
            keep.ContactId ??= drop.ContactId;
            keep.LeadId ??= drop.LeadId;
            keep.Notes = string.IsNullOrWhiteSpace(drop.Notes) || keep.Notes?.Contains(drop.Notes.Trim()) == true
                ? keep.Notes
                : string.IsNullOrWhiteSpace(keep.Notes) ? drop.Notes : $"{keep.Notes.TrimEnd()}\n\n{drop.Notes.Trim()}";
            keep.JoinedAtUtc = keep.JoinedAtUtc <= drop.JoinedAtUtc ? keep.JoinedAtUtc : drop.JoinedAtUtc;
            keep.RenewsAtUtc = keep.RenewsAtUtc >= drop.RenewsAtUtc ? keep.RenewsAtUtc : drop.RenewsAtUtc;
            // What the duplicate owed moved with its entries; the figure the lists show follows.
            keep.HouseAccountBalance += drop.HouseAccountBalance;

            _db.ClubMembers.Remove(drop);
            await _db.SaveChangesAsync(ct);
            return true;
        }, ct);

    /// <summary>
    /// The household links of two members being merged. Saved in steps inside the merge's
    /// transaction, because a household has one primary and the index that keeps it so would see
    /// two if a new primary were set in the same statement as the old one leaves.
    /// </summary>
    private async Task MergeHouseholdLinksAsync(Guid keepId, Guid dropId, Guid? organizationId, CancellationToken ct)
    {
        var drop = await _db.ClubHouseholdMembers.FirstOrDefaultAsync(l => l.MemberId == dropId && l.OrganizationId == organizationId, ct);
        if (drop == null) return;
        var keep = await _db.ClubHouseholdMembers.FirstOrDefaultAsync(l => l.MemberId == keepId && l.OrganizationId == organizationId, ct);
        if (keep == null)
        {
            drop.MemberId = keepId;
            drop.UpdatedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return;
        }
        var householdId = drop.HouseholdId;
        var wasPrimary = drop.IsPrimary;
        _db.ClubHouseholdMembers.Remove(drop);
        await _db.SaveChangesAsync(ct);
        if (!wasPrimary) return;
        // The duplicate headed a household: someone still in it takes over, the kept member when
        // it is theirs, else whoever joined it first.
        var next = householdId == keep.HouseholdId
            ? keep
            : await _db.ClubHouseholdMembers.Where(l => l.HouseholdId == householdId && l.OrganizationId == organizationId)
                .OrderBy(l => l.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (next == null)
        {
            if (await _db.ClubHouseholds.FirstOrDefaultAsync(h => h.Id == householdId && h.OrganizationId == organizationId, ct) is { } empty)
                _db.ClubHouseholds.Remove(empty);
        }
        else
        {
            next.IsPrimary = true;
            next.Role = Domain.Entities.HouseholdRoles.Primary;
            next.RollUpBilling = false;
            next.UpdatedAtUtc = DateTime.UtcNow;
            // A contact who is not a member heads it now: there is no account to bill, so the
            // ledger bills each member on their own, and their roll-up must not say otherwise.
            if (next.MemberId == null)
                foreach (var other in await _db.ClubHouseholdMembers
                             .Where(l => l.HouseholdId == householdId && l.OrganizationId == organizationId && l.Id != next.Id && l.RollUpBilling).ToListAsync(ct))
                {
                    other.RollUpBilling = false;
                    other.UpdatedAtUtc = DateTime.UtcNow;
                }
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ClubVisit>> VisitsAsync(Guid? organizationId, CancellationToken ct = default) =>
        await _db.ClubVisits.AsNoTracking().Where(v => v.OrganizationId == organizationId)
            .OrderByDescending(v => v.VisitedAtUtc).ToListAsync(ct);

    public async Task AddVisitAsync(ClubVisit visit, LoyaltyLedgerEntry? earned = null, CancellationToken ct = default)
    {
        _db.ClubVisits.Add(visit);
        if (earned != null) _db.LoyaltyLedger.Add(earned);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteVisitAsync(Guid id, Guid? organizationId, CancellationToken ct = default)
    {
        var row = await _db.ClubVisits.FirstOrDefaultAsync(v => v.Id == id && v.OrganizationId == organizationId, ct);
        if (row == null) return false;
        _db.ClubVisits.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<LoyaltyLedgerEntry>> LedgerAsync(Guid? organizationId, CancellationToken ct = default) =>
        await _db.LoyaltyLedger.AsNoTracking().Where(e => e.OrganizationId == organizationId)
            .OrderByDescending(e => e.CreatedAtUtc).ToListAsync(ct);

    public async Task AddLedgerAsync(LoyaltyLedgerEntry entry, CancellationToken ct = default)
    {
        _db.LoyaltyLedger.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteLedgerAsync(Guid id, Guid? organizationId, CancellationToken ct = default)
    {
        var row = await _db.LoyaltyLedger.FirstOrDefaultAsync(e => e.Id == id && e.OrganizationId == organizationId, ct);
        if (row == null) return false;
        _db.LoyaltyLedger.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
