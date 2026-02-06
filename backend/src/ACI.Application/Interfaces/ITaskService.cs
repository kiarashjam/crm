using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

/// <summary>
/// Service for managing tasks in the CRM system.
/// </summary>
public interface ITaskService
{
    /// <summary>
    /// Gets paginated tasks for a user with optional filters and search.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="page">The page number (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="search">Optional search term.</param>
    /// <param name="filters">Optional filter parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of tasks.</returns>
    Task<PagedResult<TaskDto>> GetPagedAsync(
        Guid userId,
        Guid? organizationId,
        int page,
        int pageSize,
        string? search = null,
        TaskFilterParams? filters = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets tasks for a user with optional filters.
    /// </summary>
    Task<IReadOnlyList<TaskDto>> GetTasksAsync(Guid userId, Guid? organizationId, TaskFilterParams filters, CancellationToken ct = default);

    /// <summary>
    /// Gets tasks linked to a specific lead.
    /// </summary>
    Task<IReadOnlyList<TaskDto>> GetTasksByLeadIdAsync(Guid leadId, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Gets tasks linked to a specific deal.
    /// </summary>
    Task<IReadOnlyList<TaskDto>> GetTasksByDealIdAsync(Guid dealId, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Gets tasks linked to a specific contact.
    /// </summary>
    Task<IReadOnlyList<TaskDto>> GetTasksByContactIdAsync(Guid contactId, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Gets a task by its ID.
    /// </summary>
    Task<Result<TaskDto>> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Creates a new task.
    /// </summary>
    Task<Result<TaskDto>> CreateAsync(Guid userId, Guid? organizationId, CreateTaskRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates an existing task.
    /// </summary>
    Task<Result<TaskDto>> UpdateAsync(Guid id, Guid userId, Guid? organizationId, UpdateTaskRequest request, CancellationToken ct = default);

    /// <summary>
    /// Updates the status of a task.
    /// </summary>
    Task<Result<TaskDto>> UpdateStatusAsync(Guid id, Guid userId, Guid? organizationId, string status, CancellationToken ct = default);

    /// <summary>
    /// Assigns a task to a user.
    /// </summary>
    Task<Result<TaskDto>> AssignTaskAsync(Guid id, Guid userId, Guid? organizationId, Guid? assigneeId, CancellationToken ct = default);

    /// <summary>
    /// Links a task to a lead.
    /// </summary>
    Task<Result<TaskDto>> LinkToLeadAsync(Guid id, Guid userId, Guid? organizationId, Guid? leadId, CancellationToken ct = default);

    /// <summary>
    /// Links a task to a deal.
    /// </summary>
    Task<Result<TaskDto>> LinkToDealAsync(Guid id, Guid userId, Guid? organizationId, Guid? dealId, CancellationToken ct = default);

    /// <summary>
    /// Deletes a task.
    /// </summary>
    Task<Result> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// Gets task statistics for the dashboard.
    /// </summary>
    Task<TaskStatsDto> GetStatsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
}

/// <summary>
/// Filter parameters for task queries.
/// </summary>
public record TaskFilterParams(
    bool? OverdueOnly = null,
    string? Status = null,
    string? AssigneeId = null,
    string? LeadId = null,
    string? DealId = null,
    string? ContactId = null,
    string? Priority = null
);

/// <summary>
/// Task statistics for the dashboard.
/// </summary>
public record TaskStatsDto(
    int Total,
    int Todo,
    int InProgress,
    int Completed,
    int Cancelled,
    int Overdue,
    int DueToday,
    int HighPriority
);
