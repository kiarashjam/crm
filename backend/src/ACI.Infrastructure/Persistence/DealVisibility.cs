using ACI.Domain.Common;
using ACI.Domain.Entities;
using ACI.Domain.Enums;

namespace ACI.Infrastructure.Persistence;

/// <summary>
/// Which deals someone can see: in an organization, its deals less the private ones
/// that are neither theirs nor theirs to manage; with none, their own.
/// </summary>
/// <remarks>
/// Every query that reads deals goes through this, not only the deal list. Private
/// deals were hidden from the list and the detail page while reports, company totals,
/// line items, attachments and custom fields still read them.
/// </remarks>
public static class DealVisibility
{
    public static IQueryable<Deal> VisibleTo(this IQueryable<Deal> deals, AppDbContext db, Guid userId, Guid? organizationId) =>
        organizationId == null
            ? deals.Where(d => d.UserId == userId && d.OrganizationId == null)
            : deals.Where(d => d.OrganizationId == organizationId
                && (!d.IsPrivate
                    || d.UserId == userId
                    || d.AssigneeId == userId
                    || db.OrganizationMembers.Where(m => m.OrganizationId == organizationId && m.UserId == userId).Any(SeesAllDeals)));

    // Who may see every deal, in SQL: MemberPermissions.Effective holding SeeAllDeals.
    // The owner always; a member with a list of their own when it holds it; one with a
    // preset when the preset does; and a member from before presets when they were a manager.
    private static readonly string[] PresetsSeeingAll = MemberPermissions.PresetsWith(MemberPermissions.SeeAllDeals);
    private static readonly string SeeAllStored = MemberPermissions.Stored(MemberPermissions.SeeAllDeals);

    /// <summary>Whether a membership sees every deal of its organization, private ones too.</summary>
    public static readonly System.Linq.Expressions.Expression<Func<OrganizationMember, bool>> SeesAllDeals = m =>
        m.Role == OrgMemberRole.Owner
        || (m.Permissions != null
            ? m.Permissions.Contains(SeeAllStored)
            : m.PermissionPreset != null
                ? PresetsSeeingAll.Contains(m.PermissionPreset)
                : m.Role == OrgMemberRole.Manager);
}
