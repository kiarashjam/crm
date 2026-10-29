namespace ACI.Domain.Entities;

/// <summary>
/// Invitation to join an organization, sent by owner by email.
/// </summary>
public class Invite : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }
    public string Email { get; set; } = string.Empty;
    /// <summary>
    /// SHA-256 hex hash of the one-time token in the emailed accept link (stored in the
    /// Token column). The raw token is never stored.
    /// </summary>
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>Set when the invite is accepted (user who accepted).</summary>
    public Guid? AcceptedByUserId { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }

    public Organization Organization { get; set; } = null!;
}
