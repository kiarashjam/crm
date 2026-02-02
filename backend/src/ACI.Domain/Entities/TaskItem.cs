namespace ACI.Domain.Entities;

/// <summary>
/// Task linked to a lead or deal; has due date and completion.
/// </summary>
public class TaskItem : Common.BaseEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public bool Completed { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? DealId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public Lead? Lead { get; set; }
    public Deal? Deal { get; set; }
}
