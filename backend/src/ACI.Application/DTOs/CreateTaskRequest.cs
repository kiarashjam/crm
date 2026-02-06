using System.ComponentModel.DataAnnotations;

namespace ACI.Application.DTOs;

/// <summary>
/// Request to create a new task.
/// </summary>
public record CreateTaskRequest
{
    /// <summary>
    /// Task title.
    /// </summary>
    [Required(ErrorMessage = "Title is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters")]
    public required string Title { get; init; }

    /// <summary>
    /// Task description (optional).
    /// </summary>
    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
    public string? Description { get; init; }

    /// <summary>
    /// Due date (optional).
    /// </summary>
    public DateTime? DueDateUtc { get; init; }

    /// <summary>
    /// Reminder date (optional).
    /// </summary>
    public DateTime? ReminderDateUtc { get; init; }

    /// <summary>
    /// Task status. Valid values: todo, in_progress, completed, cancelled. Defaults to "todo".
    /// </summary>
    [StringLength(20, ErrorMessage = "Status cannot exceed 20 characters")]
    [RegularExpression(@"^(todo|in_progress|completed|cancelled)$", 
        ErrorMessage = "Status must be one of: todo, in_progress, completed, cancelled")]
    public string? Status { get; init; }

    /// <summary>
    /// Task priority. Valid values: none, low, medium, high. Defaults to "none".
    /// </summary>
    [StringLength(10, ErrorMessage = "Priority cannot exceed 10 characters")]
    [RegularExpression(@"^(none|low|medium|high)$", 
        ErrorMessage = "Priority must be one of: none, low, medium, high")]
    public string? Priority { get; init; }

    /// <summary>
    /// Related lead ID (optional).
    /// </summary>
    public Guid? LeadId { get; init; }

    /// <summary>
    /// Related deal ID (optional).
    /// </summary>
    public Guid? DealId { get; init; }

    /// <summary>
    /// Related contact ID (optional).
    /// </summary>
    public Guid? ContactId { get; init; }

    /// <summary>
    /// Assigned team member ID (optional).
    /// </summary>
    public Guid? AssigneeId { get; init; }

    /// <summary>
    /// Additional notes (optional).
    /// </summary>
    [StringLength(2000, ErrorMessage = "Notes cannot exceed 2000 characters")]
    public string? Notes { get; init; }
}
