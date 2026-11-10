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

    /// <summary>
    /// The preset this member's permissions start from (see Common.MemberPermissions.Presets),
    /// or null for a member from before permission sets, who keeps what their role allowed.
    /// </summary>
    public string? PermissionPreset { get; set; }

    /// <summary>
    /// The member's own permissions when the owner changed them from the preset's, stored
    /// as ",key,key,"; null when they are the preset's.
    /// </summary>
    public string? Permissions { get; set; }

    public Organization Organization { get; set; } = null!;
    public User User { get; set; } = null!;
}
