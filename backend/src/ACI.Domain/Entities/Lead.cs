namespace ACI.Domain.Entities;

/// <summary>
/// Lead (prospect) owned by a user; optional company link. Status: New, Contacted, Qualified, Lost.
/// </summary>
public class Lead : Common.BaseEntity
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public Guid? CompanyId { get; set; }
    /// <summary>Source: website, referral, ads, events, manual.</summary>
    public string? Source { get; set; }
    /// <summary>Status: New, Contacted, Qualified, Lost.</summary>
    public string Status { get; set; } = "New";
    public DateTime CreatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public Company? Company { get; set; }
    public ICollection<TaskItem> TaskItems { get; set; } = new List<TaskItem>();
}
