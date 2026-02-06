using ACI.Domain.Enums;

namespace ACI.Domain.Entities;

/// <summary>
/// Request from a user to join an organization; owner can accept or reject.
/// </summary>
public class JoinRequest : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public JoinRequestStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>Set when owner accepts or rejects.</summary>
    public Guid? RespondedByUserId { get; set; }
    public DateTime? RespondedAtUtc { get; set; }

    public Organization Organization { get; set; } = null!;
    public User User { get; set; } = null!;
}
