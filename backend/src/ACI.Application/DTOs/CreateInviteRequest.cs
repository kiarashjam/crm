using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to create an organization invite.
/// </summary>
public record CreateInviteRequest
{
    /// <summary>
    /// Email address to invite.
    /// </summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters")]
    public required string Email { get; init; }

    /// <summary>
    /// The preset their permissions start from once they accept (MemberPermissions.Presets),
    /// which sets their role too. Left out, they join as a Member, as invitations always did.
    /// </summary>
    [StringLength(40)]
    public string? Preset { get; init; }
}
