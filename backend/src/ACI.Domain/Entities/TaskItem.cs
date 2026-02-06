using ACI.Domain.Enums;

namespace ACI.Domain.Entities;

/// <summary>
/// Task linked to a lead, deal, or contact; has due date, priority, and status tracking.
/// </summary>
public class TaskItem : Common.BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDateUtc { get; set; }
    public DateTime? ReminderDateUtc { get; set; }
    
    /// <summary>
    /// Task status: Todo, InProgress, Completed, Cancelled.
    /// </summary>
    public Enums.TaskStatus Status { get; set; } = Enums.TaskStatus.Todo;
    
    /// <summary>
    /// Task priority: None, Low, Medium, High.
    /// </summary>
    public TaskPriority Priority { get; set; } = TaskPriority.None;
    
    /// <summary>
    /// Legacy field for backward compatibility. Use Status instead.
    /// </summary>
    public bool Completed { get; set; }
    
    public Guid? AssigneeId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? DealId { get; set; }
    public Guid? ContactId { get; set; }
    
    /// <summary>
    /// Optional notes or comments on the task.
    /// </summary>
    public string? Notes { get; set; }
    
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public User? UpdatedByUser { get; set; }
    public User? Assignee { get; set; }
    public Organization? Organization { get; set; }
    public Lead? Lead { get; set; }
    public Deal? Deal { get; set; }
    public Contact? Contact { get; set; }
}
