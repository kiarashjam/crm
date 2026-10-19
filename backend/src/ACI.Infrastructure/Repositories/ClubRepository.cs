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
