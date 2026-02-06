namespace ACI.Domain.Entities;

/// <summary>
/// Lead (prospect) owned by a user; optional company link. Status: New, Contacted, Qualified, Lost.
/// </summary>
public class Lead : Common.BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public Guid? CompanyId { get; set; }
    /// <summary>Source: website, referral, ads, events, manual (legacy string).</summary>
    public string? Source { get; set; }
    /// <summary>Status: New, Contacted, Qualified, Lost (legacy string).</summary>
    public string Status { get; set; } = "New";
    public Guid? LeadSourceId { get; set; }
    public Guid? LeadStatusId { get; set; }
    public int? LeadScore { get; set; }
    public DateTime? LastContactedAt { get; set; }
    public string? Description { get; set; }
    public string? LifecycleStage { get; set; }
    public bool IsConverted { get; set; }
    public DateTime? ConvertedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    // Conversion traceability - what entities were created from this lead
    public Guid? ConvertedToContactId { get; set; }
    public Guid? ConvertedToDealId { get; set; }
    public Guid? ConvertedToCompanyId { get; set; }

    public User User { get; set; } = null!;
    public User? UpdatedByUser { get; set; }
    public Organization? Organization { get; set; }
    public Company? Company { get; set; }
    public LeadSource? LeadSource { get; set; }
    public LeadStatus? LeadStatus { get; set; }
    public Contact? ConvertedToContact { get; set; }
    public Deal? ConvertedToDeal { get; set; }
    public ICollection<TaskItem> TaskItems { get; set; } = new List<TaskItem>();
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
}
