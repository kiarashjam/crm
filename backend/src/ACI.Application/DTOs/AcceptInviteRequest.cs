using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to accept an organization invite.
/// </summary>
public record AcceptInviteRequest
{
    /// <summary>
    /// Invite token.
    /// </summary>
    [Required(ErrorMessage = "Token is required")]
    [StringLength(500, ErrorMessage = "Token cannot exceed 500 characters")]
    public required string Token { get; init; }
}
