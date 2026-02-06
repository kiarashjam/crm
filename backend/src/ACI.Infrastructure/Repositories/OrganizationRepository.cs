using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class OrganizationRepository : IOrganizationRepository
{
    private readonly AppDbContext _db;

    public OrganizationRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Organization>> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await _db.OrganizationMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.Organization)
            .OrderBy(o => o.Name)
            .ToListAsync(ct);

    public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);

    public async Task<bool> IsMemberAsync(Guid userId, Guid organizationId, CancellationToken ct = default) =>
        await _db.OrganizationMembers.AnyAsync(m => m.OrganizationId == organizationId && m.UserId == userId, ct);

    public async Task<OrgMemberRole?> GetMemberRoleAsync(Guid userId, Guid organizationId, CancellationToken ct = default)
    {
        var member = await _db.OrganizationMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, ct);
        return member?.Role;
    }

    public async Task<Organization> CreateAsync(Organization organization, CancellationToken ct = default)
    {
        _db.Organizations.Add(organization);
        await _db.SaveChangesAsync(ct);
        return organization;
    }

    public async Task AddMemberAsync(Guid organizationId, Guid userId, OrgMemberRole role, CancellationToken ct = default)
    {
        _db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = role,
            JoinedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<OrganizationMember>> GetMembersAsync(Guid organizationId, CancellationToken ct = default) =>
        await _db.OrganizationMembers
            .Where(m => m.OrganizationId == organizationId)
            .Include(m => m.User)
            .OrderBy(m => m.User!.Name)
            .ToListAsync(ct);

    public async Task<OrganizationMember?> GetMemberAsync(Guid organizationId, Guid userId, CancellationToken ct = default) =>
        await _db.OrganizationMembers
            .Include(m => m.User)
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, ct);

    public async Task UpdateMemberRoleAsync(Guid organizationId, Guid userId, OrgMemberRole role, CancellationToken ct = default)
    {
        var member = await _db.OrganizationMembers.FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, ct);
        if (member != null)
        {
            member.Role = role;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task RemoveMemberAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        var member = await _db.OrganizationMembers.FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, ct);
        if (member != null)
        {
            _db.OrganizationMembers.Remove(member);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task BackfillUserDataToOrganizationAsync(Guid userId, Guid organizationId, CancellationToken ct = default)
    {
        await _db.Companies.Where(c => c.UserId == userId && c.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(c => c.OrganizationId, organizationId), ct);
        await _db.Contacts.Where(c => c.UserId == userId && c.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(c => c.OrganizationId, organizationId), ct);
        await _db.Deals.Where(d => d.UserId == userId && d.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(d => d.OrganizationId, organizationId), ct);
        await _db.Leads.Where(l => l.UserId == userId && l.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(l => l.OrganizationId, organizationId), ct);
        await _db.TaskItems.Where(t => t.UserId == userId && t.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.OrganizationId, organizationId), ct);
        await _db.Activities.Where(a => a.UserId == userId && a.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(a => a.OrganizationId, organizationId), ct);
        await _db.CopyHistoryItems.Where(h => h.UserId == userId && h.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(h => h.OrganizationId, organizationId), ct);
    }

    public async Task<Organization?> GetByApiKeyAsync(string apiKey, CancellationToken ct = default) =>
        await _db.Organizations.FirstOrDefaultAsync(o => o.WebhookApiKey == apiKey, ct);

    public async Task UpdateAsync(Organization organization, CancellationToken ct = default)
    {
        _db.Organizations.Update(organization);
        await _db.SaveChangesAsync(ct);
    }
}
