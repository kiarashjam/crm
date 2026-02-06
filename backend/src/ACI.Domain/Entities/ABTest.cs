using ACI.Domain.Enums;

namespace ACI.Domain.Entities;

/// <summary>
/// A/B Test for comparing copy variants.
/// </summary>
public class ABTest : Common.BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public CopyTypeId CopyTypeId { get; set; }
    public string Goal { get; set; } = string.Empty;
    public ABTestStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public Guid? WinningVariantId { get; set; }
    
    public User? User { get; set; }
    public Organization? Organization { get; set; }
    public ICollection<ABTestVariant> Variants { get; set; } = new List<ABTestVariant>();
}

/// <summary>
/// Individual variant in an A/B test.
/// </summary>
public class ABTestVariant : Common.BaseEntity
{
    public Guid TestId { get; set; }
    public string Name { get; set; } = string.Empty; // e.g., "Variant A", "Variant B"
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public int SendCount { get; set; }
    public int OpenCount { get; set; }
    public int ClickCount { get; set; }
    public int ReplyCount { get; set; }
    public double OpenRate => SendCount > 0 ? (double)OpenCount / SendCount * 100 : 0;
    public double ClickRate => SendCount > 0 ? (double)ClickCount / SendCount * 100 : 0;
    public double ReplyRate => SendCount > 0 ? (double)ReplyCount / SendCount * 100 : 0;
    
    public ABTest? Test { get; set; }
}

public enum ABTestStatus
{
    Draft,
    Running,
    Completed,
    Cancelled
}
