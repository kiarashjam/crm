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
                    || db.OrganizationMembers.Any(m => m.OrganizationId == organizationId
                        && m.UserId == userId
                        && (m.Role == OrgMemberRole.Owner || m.Role == OrgMemberRole.Manager))));
}
