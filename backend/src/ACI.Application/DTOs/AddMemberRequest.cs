using System.ComponentModel.DataAnnotations;
using ACI.Domain.Enums;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to add an existing registered user to an organization directly, without
/// an invitation.
/// </summary>
public record AddMemberRequest
{
    /// <summary>
    /// Email address of the person to add. They must already have an account.
    /// </summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters")]
    public required string Email { get; init; }

    /// <summary>
    /// Role to grant. Owner cannot be granted here — ownership is transferred separately.
    /// </summary>
    [Required(ErrorMessage = "Role is required")]
    [EnumDataType(typeof(OrgMemberRole), ErrorMessage = "Invalid role value")]
    public required OrgMemberRole Role { get; init; }

    /// <summary>
    /// The preset their permissions start from (MemberPermissions.Presets). It sets the
    /// role too. Left out, they get what <see cref="Role"/> allowed before presets existed.
    /// </summary>
    [StringLength(40)]
    public string? Preset { get; init; }
}
