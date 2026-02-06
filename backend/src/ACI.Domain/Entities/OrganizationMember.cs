using ACI.Domain.Enums;

namespace ACI.Domain.Entities;

/// <summary>
/// Membership of a user in an organization.
/// </summary>
public class OrganizationMember
{
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public OrgMemberRole Role { get; set; }
    public DateTime JoinedAtUtc { get; set; }

    public Organization Organization { get; set; } = null!;
    public User User { get; set; } = null!;
}
