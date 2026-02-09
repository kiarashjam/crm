namespace ACI.Domain.Entities;

/// <summary>
/// Contact (person) owned by a user; optionally linked to a company.
/// </summary>
public class Contact : Common.BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public Guid? CompanyId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    // Lead conversion traceability
    public Guid? ConvertedFromLeadId { get; set; }
    public DateTime? ConvertedAtUtc { get; set; }

    // Soft delete / archive
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public Guid? ArchivedByUserId { get; set; }

    // HP-7: Contact description / notes
    public string? Description { get; set; }

    // Contact preferences / compliance
    public bool DoNotContact { get; set; }
    public string? PreferredContactMethod { get; set; }

    public User User { get; set; } = null!;
    public User? UpdatedByUser { get; set; }
    public User? ArchivedByUser { get; set; }
    public Organization? Organization { get; set; }
    public Company? Company { get; set; }
    public Lead? ConvertedFromLead { get; set; }
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}
