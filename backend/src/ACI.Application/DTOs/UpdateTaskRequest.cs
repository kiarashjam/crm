using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to update an existing task.
/// </summary>
public record UpdateTaskRequest
{
    /// <summary>
    /// Task title.
    /// </summary>
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters")]
    public string? Title { get; init; }

    /// <summary>
    /// Task description.
    /// </summary>
    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
    public string? Description { get; init; }

    /// <summary>
    /// Due date.
    /// </summary>
    public DateTime? DueDateUtc { get; init; }

    /// <summary>
    /// Reminder date.
    /// </summary>
    public DateTime? ReminderDateUtc { get; init; }

    /// <summary>
    /// Task status. Valid values: todo, in_progress, completed, cancelled.
    /// </summary>
    [StringLength(20, ErrorMessage = "Status cannot exceed 20 characters")]
    [RegularExpression(@"^(todo|in_progress|completed|cancelled)$", 
        ErrorMessage = "Status must be one of: todo, in_progress, completed, cancelled")]
    public string? Status { get; init; }

    /// <summary>
    /// Task priority. Valid values: none, low, medium, high.
    /// </summary>
    [StringLength(10, ErrorMessage = "Priority cannot exceed 10 characters")]
    [RegularExpression(@"^(none|low|medium|high)$", 
        ErrorMessage = "Priority must be one of: none, low, medium, high")]
    public string? Priority { get; init; }

    /// <summary>
    /// Legacy field - sets status to completed if true.
    /// </summary>
    public bool? Completed { get; init; }

    /// <summary>
    /// Related lead ID.
    /// </summary>
    public Guid? LeadId { get; init; }

    /// <summary>
    /// Related deal ID.
    /// </summary>
    public Guid? DealId { get; init; }

    /// <summary>
    /// Related contact ID.
    /// </summary>
    public Guid? ContactId { get; init; }

    /// <summary>
    /// Assigned team member ID.
    /// </summary>
    public Guid? AssigneeId { get; init; }

    /// <summary>
    /// Additional notes.
    /// </summary>
    [StringLength(2000, ErrorMessage = "Notes cannot exceed 2000 characters")]
    public string? Notes { get; init; }

    /// <summary>
    /// Set to true to clear the due date.
    /// </summary>
    public bool? ClearDueDate { get; init; }

    /// <summary>
    /// Set to true to clear the reminder date.
    /// </summary>
    public bool? ClearReminderDate { get; init; }

    /// <summary>
    /// Set to true to unassign the task.
    /// </summary>
    public bool? ClearAssignee { get; init; }

    /// <summary>
    /// Set to true to unlink the lead.
    /// </summary>
    public bool? ClearLead { get; init; }

    /// <summary>
    /// Set to true to unlink the deal.
    /// </summary>
    public bool? ClearDeal { get; init; }

    /// <summary>
    /// Set to true to unlink the contact.
    /// </summary>
    public bool? ClearContact { get; init; }
}
