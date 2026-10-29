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
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteMemberAsync(Guid id, Guid? organizationId, CancellationToken ct = default)
    {
        var row = await MemberAsync(id, organizationId, ct);
        if (row == null) return false;
        _db.ClubMembers.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
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
            foreach (var entry in await _db.LoyaltyLedger.Where(e => e.MemberId == dropId && e.OrganizationId == organizationId).ToListAsync(ct))
            {
                entry.MemberId = keepId;
                entry.MemberName = name;
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
            // Both in the same household is one person in it once.
            var links = await _db.ClubDocuments
                .Where(d => d.OrganizationId == organizationId && d.Kind == "household-members")
                .ToListAsync(ct);
            var seen = new HashSet<string>();
            foreach (var link in links.OrderBy(l => documents.Contains(l) ? 1 : 0))
            {
                var obj = JsonNode.Parse(link.Payload) as JsonObject;
                var key = $"{obj?["householdId"]}|{obj?["memberId"]}".ToLowerInvariant();
                if (!seen.Add(key)) _db.ClubDocuments.Remove(link);
            }

            keep.Phone = RecordMerge.Fill(keep.Phone, drop.Phone);
            keep.Notes = string.IsNullOrWhiteSpace(drop.Notes) || keep.Notes?.Contains(drop.Notes.Trim()) == true
                ? keep.Notes
                : string.IsNullOrWhiteSpace(keep.Notes) ? drop.Notes : $"{keep.Notes.TrimEnd()}\n\n{drop.Notes.Trim()}";
            keep.JoinedAtUtc = keep.JoinedAtUtc <= drop.JoinedAtUtc ? keep.JoinedAtUtc : drop.JoinedAtUtc;
            keep.RenewsAtUtc = keep.RenewsAtUtc >= drop.RenewsAtUtc ? keep.RenewsAtUtc : drop.RenewsAtUtc;
            // What the duplicate owed moved with its charges, and its opening or corrected
            // part with it, so the next recompute keeps both.
            keep.HouseAccountBalance += drop.HouseAccountBalance;
            keep.ManualBalance += drop.ManualBalance;

            _db.ClubMembers.Remove(drop);
            await _db.SaveChangesAsync(ct);
            return true;
        }, ct);

    public async Task<IReadOnlyList<ClubVisit>> VisitsAsync(Guid? organizationId, CancellationToken ct = default) =>
        await _db.ClubVisits.AsNoTracking().Where(v => v.OrganizationId == organizationId)
            .OrderByDescending(v => v.VisitedAtUtc).ToListAsync(ct);

    public async Task AddVisitAsync(ClubVisit visit, CancellationToken ct = default)
    {
        _db.ClubVisits.Add(visit);
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
