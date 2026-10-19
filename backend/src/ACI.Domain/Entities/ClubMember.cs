namespace ACI.Domain.Entities;

/// <summary>A club member. This is not a CRM contact.</summary>
public class ClubMember : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid OwnerUserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Tier { get; set; } = "Bronze";
    public string Status { get; set; } = "Pending";
    public DateTime JoinedAtUtc { get; set; }
    public DateTime RenewsAtUtc { get; set; }
    public decimal DuesAmount { get; set; }
    public string DuesFrequency { get; set; } = "Monthly";
    public decimal HouseAccountBalance { get; set; }
    public string? Notes { get; set; }
}

public class ClubVisit : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string MemberTier { get; set; } = string.Empty;
    public string Venue { get; set; } = string.Empty;
    public DateTime VisitedAtUtc { get; set; }
    public int GuestCount { get; set; }
    public int PointsAwarded { get; set; }
    public string? Notes { get; set; }
}

public class LoyaltyLedgerEntry : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string Kind { get; set; } = "Earned";
    public string Reason { get; set; } = "Visit";
    public int Points { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
