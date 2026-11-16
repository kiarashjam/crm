using ACI.Application.Interfaces;
using ACI.Domain.Common;
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

    public Task AddMemberAsync(Guid organizationId, Guid userId, OrgMemberRole role, CancellationToken ct = default) =>
        AddMemberAsync(organizationId, userId, role, null, ct);

    public async Task AddMemberAsync(Guid organizationId, Guid userId, OrgMemberRole role, string? permissionPreset, CancellationToken ct = default)
    {
        _db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = role,
            PermissionPreset = permissionPreset,
            JoinedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateMemberPermissionsAsync(Guid organizationId, Guid userId, OrgMemberRole role, string? permissionPreset, string? permissions, CancellationToken ct = default)
    {
        var member = await _db.OrganizationMembers.FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, ct);
        if (member == null) return;
        member.Role = role;
        member.PermissionPreset = permissionPreset;
        member.Permissions = permissions;
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
            // A role set this way means what it meant before presets: the preset and any
            // permissions of the member's own give way to it.
            member.Role = role;
            member.PermissionPreset = null;
            member.Permissions = null;
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

    public async Task<MemberHandOver> RemoveMemberAsync(Guid organizationId, Guid userId, Guid handOverToUserId, Guid actingUserId, CancellationToken ct = default)
    {
        // The organization's records, whichever workspace the request came from.
        using var acting = _db.ActFor(organizationId, actingUserId);
        var handedOver = MemberHandOver.Nothing;
        await RecordMerge.InTransactionAsync(_db, async () =>
        {
            var member = await _db.OrganizationMembers.FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, ct);
            if (member == null) return false;
            var now = DateTime.UtcNow;

            // What they work: assigned to them, or theirs with nobody assigned, as every
            // reminder and notice picks its person. Work that is over keeps whoever did
            // it, so reports still credit them: a converted, signed or lost lead, a won or
            // lost deal, a finished task.
            var leads = (await _db.Leads
                    .Where(l => l.OrganizationId == organizationId && !l.IsConverted
                        && (l.AssignedToUserId == userId || (l.AssignedToUserId == null && l.UserId == userId)))
                    .ToListAsync(ct))
                .Where(l => !LeadStatusVocabulary.IsClosed(l.Status))
                .ToList();
            foreach (var lead in leads)
            {
                lead.AssignedToUserId = handOverToUserId;
                lead.UpdatedAtUtc = now;
                lead.UpdatedByUserId = actingUserId;
            }
            var deals = await _db.Deals
                .Where(d => d.OrganizationId == organizationId && d.IsWon == null
                    && (d.AssigneeId == userId || (d.AssigneeId == null && d.UserId == userId)))
                .ToListAsync(ct);
            foreach (var deal in deals)
            {
                deal.AssigneeId = handOverToUserId;
                deal.UpdatedAtUtc = now;
                deal.UpdatedByUserId = actingUserId;
            }
            var tasks = await _db.TaskItems
                .Where(t => t.OrganizationId == organizationId && !t.Completed
                    && t.Status != Domain.Enums.TaskStatus.Completed && t.Status != Domain.Enums.TaskStatus.Cancelled
                    && (t.AssigneeId == userId || (t.AssigneeId == null && t.UserId == userId)))
                .ToListAsync(ct);
            foreach (var task in tasks)
            {
                task.AssigneeId = handOverToUserId;
                task.UpdatedAtUtc = now;
                task.UpdatedByUserId = actingUserId;
            }

            _db.OrganizationMembers.Remove(member);
            await _db.SaveChangesAsync(ct);
            handedOver = new MemberHandOver(
                leads.Select(l => (l.Id, l.Name)).ToList(),
                deals.Select(d => (d.Id, d.Name)).ToList(),
                tasks);
            return true;
        }, ct);
        return handedOver;
    }

    public async Task BackfillUserDataToOrganizationAsync(Guid userId, Guid organizationId, CancellationToken ct = default)
    {
        // The rows that move are the person's own personal workspace's, whichever
        // workspace the request creating the organization came from.
        using var acting = _db.ActFor(null, userId);
        await _db.Companies.Where(c => c.UserId == userId && c.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(c => c.OrganizationId, organizationId), ct);
        await _db.Contacts.Where(c => c.UserId == userId && c.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(c => c.OrganizationId, organizationId), ct);
        await _db.ContactRelationships.Where(r => r.UserId == userId && r.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(r => r.OrganizationId, organizationId), ct);
        await _db.Deals.Where(d => d.UserId == userId && d.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(d => d.OrganizationId, organizationId), ct);
        await _db.Leads.Where(l => l.UserId == userId && l.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(l => l.OrganizationId, organizationId), ct);
        await _db.TaskItems.Where(t => t.UserId == userId && t.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.OrganizationId, organizationId), ct);
        await _db.Activities.Where(a => a.UserId == userId && a.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(a => a.OrganizationId, organizationId), ct);
        await _db.CopyHistoryItems.Where(h => h.UserId == userId && h.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(h => h.OrganizationId, organizationId), ct);
        // What hangs off those records moves with them, or it would be left behind in
        // the personal workspace, out of sight. Line items follow their deal on their own.
        await _db.AuditEvents.Where(e => e.OwnerUserId == userId && e.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(e => e.OrganizationId, organizationId), ct);
        await _db.Attachments.Where(a => a.OwnerUserId == userId && a.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(a => a.OrganizationId, organizationId), ct);
        await _db.CustomFieldDefinitions.Where(d => d.OwnerUserId == userId && d.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(d => d.OrganizationId, organizationId), ct);
        await _db.CustomFieldValues.Where(v => v.OwnerUserId == userId && v.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(v => v.OrganizationId, organizationId), ct);
        await _db.CustomFieldEntries.Where(v => v.OwnerUserId == userId && v.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(v => v.OrganizationId, organizationId), ct);
        await _db.Tags.Where(t => t.OwnerUserId == userId && t.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.OrganizationId, organizationId), ct);
        await _db.RecordTags.Where(t => t.OwnerUserId == userId && t.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.OrganizationId, organizationId), ct);
        await _db.SavedViews.Where(v => v.OwnerUserId == userId && v.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(v => v.OrganizationId, organizationId), ct);
        await _db.SavedViewDefaults.Where(d => d.UserId == userId && d.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(d => d.OrganizationId, organizationId), ct);
        // The price list and saved forecasts are kept per person outside an organization.
        // Members and rules can't be made there any more, but the ones made before that
        // rule went in are their creator's, and arrive with them.
        await _db.Products.Where(p => p.OwnerUserId == userId && p.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(p => p.OrganizationId, organizationId), ct);
        await _db.ForecastSnapshots.Where(f => f.SavedByUserId == userId && f.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(f => f.OrganizationId, organizationId), ct);
        await _db.ClubMembers.Where(m => m.OwnerUserId == userId && m.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(m => m.OrganizationId, organizationId), ct);
        await _db.AutomationRules.Where(r => r.OwnerUserId == userId && r.OrganizationId == null).ExecuteUpdateAsync(s => s.SetProperty(r => r.OrganizationId, organizationId), ct);
    }

    public async Task<Organization?> GetByWebhookApiKeyHashAsync(string keyHash, CancellationToken ct = default) =>
        await _db.Organizations.FirstOrDefaultAsync(o => o.WebhookApiKeyHash == keyHash, ct);

    public async Task<Organization?> GetByMcpApiKeyHashAsync(string keyHash, CancellationToken ct = default) =>
        await _db.Organizations.FirstOrDefaultAsync(o => o.McpApiKeyHash == keyHash, ct);

    public async Task UpdateAsync(Organization organization, CancellationToken ct = default)
    {
        _db.Organizations.Update(organization);
        await _db.SaveChangesAsync(ct);
    }
}
