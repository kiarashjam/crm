namespace ACI.Application.DTOs;

/// <summary>
/// Task data transfer object with full details.
/// </summary>
public record TaskDto(
    Guid Id,
    string Title,
    string? Description,
    DateTime? DueDateUtc,
    DateTime? ReminderDateUtc,
    string Status,           // "todo", "in_progress", "completed", "cancelled"
    string Priority,         // "none", "low", "medium", "high"
    bool Completed,          // Legacy field for backward compatibility
    Guid? LeadId,
    Guid? DealId,
    Guid? ContactId,
    Guid? AssigneeId,
    string? AssigneeName,    // Assignee's display name
    string? LeadName,        // Linked lead's name
    string? DealName,        // Linked deal's name
    string? ContactName,     // Linked contact's name
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    DateTime? CompletedAtUtc
);
