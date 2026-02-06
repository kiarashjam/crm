using System.ComponentModel.DataAnnotations;
using ACI.Domain.Enums;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to update a member's role in an organization.
/// </summary>
public record UpdateMemberRoleRequest
{
    /// <summary>
    /// The new role for the member.
    /// </summary>
    [Required(ErrorMessage = "Role is required")]
    [EnumDataType(typeof(OrgMemberRole), ErrorMessage = "Invalid role value")]
    public required OrgMemberRole Role { get; init; }
}
