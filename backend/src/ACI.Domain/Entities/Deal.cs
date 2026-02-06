namespace ACI.Domain.Entities;

/// <summary>
/// Deal (opportunity) owned by a user; optionally linked to a company.
/// </summary>
public class Deal : Common.BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Currency { get; set; }
    public string? Stage { get; set; }
    public Guid? PipelineId { get; set; }
    public Guid? DealStageId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? ContactId { get; set; }
    public Guid? AssigneeId { get; set; }
    public DateTime? ExpectedCloseDateUtc { get; set; }
    /// <summary>Null = open, true = won, false = lost.</summary>
    public bool? IsWon { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public User User { get; set; } = null!;
    public User? UpdatedByUser { get; set; }
    public User? Assignee { get; set; }
    public Organization? Organization { get; set; }
    public Pipeline? Pipeline { get; set; }
    public DealStage? DealStage { get; set; }
    public Company? Company { get; set; }
    public Contact? Contact { get; set; }
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public ICollection<TaskItem> TaskItems { get; set; } = new List<TaskItem>();
}
