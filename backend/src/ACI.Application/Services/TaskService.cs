using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// Service for managing tasks in the CRM system.
/// </summary>
public class TaskService : ITaskService
{
    private readonly ITaskRepository _repository;
    private readonly ILogger<TaskService> _logger;

    public TaskService(ITaskRepository repository, ILogger<TaskService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PagedResult<TaskDto>> GetPagedAsync(
        Guid userId,
        Guid? organizationId,
        int page,
        int pageSize,
        string? search = null,
        TaskFilterParams? filters = null,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting paged tasks for user {UserId} in organization {OrganizationId}, page {Page}, pageSize {PageSize}, search '{Search}'", 
            userId, organizationId, page, pageSize, search);

        var skip = (page - 1) * pageSize;
        var (items, totalCount) = await _repository.GetPagedAsync(
            userId, 
            organizationId, 
            skip, 
            pageSize, 
            search, 
            filters?.Status, 
            filters?.Priority, 
            filters?.OverdueOnly, 
            ct);

        _logger.LogDebug("Retrieved {Count} of {Total} tasks for user {UserId}", items.Count, totalCount, userId);

        return PagedResult<TaskDto>.Create(
            items.Select(Map).ToList(),
            totalCount,
            page,
            pageSize);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TaskDto>> GetTasksAsync(
        Guid userId, 
        Guid? organizationId, 
        TaskFilterParams filters, 
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting tasks for user {UserId} with filters", userId);
        
        IReadOnlyList<TaskItem> list;
        
        // Filter by lead if specified
        if (!string.IsNullOrEmpty(filters.LeadId) && Guid.TryParse(filters.LeadId, out var leadGuid))
        {
            list = await _repository.GetByLeadIdAsync(leadGuid, userId, organizationId, ct);
        }
        // Filter by deal if specified
        else if (!string.IsNullOrEmpty(filters.DealId) && Guid.TryParse(filters.DealId, out var dealGuid))
        {
            list = await _repository.GetByDealIdAsync(dealGuid, userId, organizationId, ct);
        }
        // Filter by contact if specified
        else if (!string.IsNullOrEmpty(filters.ContactId) && Guid.TryParse(filters.ContactId, out var contactGuid))
        {
            list = await _repository.GetByContactIdAsync(contactGuid, userId, organizationId, ct);
        }
        // Filter by assignee if specified
        else if (!string.IsNullOrEmpty(filters.AssigneeId) && Guid.TryParse(filters.AssigneeId, out var assigneeGuid))
        {
            list = await _repository.GetByAssigneeIdAsync(assigneeGuid, organizationId, ct);
        }
        // Filter by status if specified
        else if (!string.IsNullOrEmpty(filters.Status) && TryParseStatus(filters.Status, out var taskStatus))
        {
            list = await _repository.GetByStatusAsync(userId, organizationId, taskStatus, ct);
        }
        // Filter overdue or get all
        else
        {
            list = filters.OverdueOnly == true
                ? await _repository.GetByUserIdAsync(userId, organizationId, true, ct)
                : await _repository.GetByUserIdAsync(userId, organizationId, ct);
        }
        
        // Apply priority filter if specified
        if (!string.IsNullOrEmpty(filters.Priority) && TryParsePriority(filters.Priority, out var priority))
        {
            list = list.Where(t => t.Priority == priority).ToList();
        }
        
        _logger.LogDebug("Retrieved {Count} tasks for user {UserId}", list.Count, userId);
        return list.Select(Map).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TaskDto>> GetTasksByLeadIdAsync(
        Guid leadId, 
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting tasks for lead {LeadId}", leadId);
        var list = await _repository.GetByLeadIdAsync(leadId, userId, organizationId, ct);
        return list.Select(Map).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TaskDto>> GetTasksByDealIdAsync(
        Guid dealId, 
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting tasks for deal {DealId}", dealId);
        var list = await _repository.GetByDealIdAsync(dealId, userId, organizationId, ct);
        return list.Select(Map).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TaskDto>> GetTasksByContactIdAsync(
        Guid contactId, 
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting tasks for contact {ContactId}", contactId);
        var list = await _repository.GetByContactIdAsync(contactId, userId, organizationId, ct);
        return list.Select(Map).ToList();
    }

    /// <inheritdoc />
    public async Task<Result<TaskDto>> GetByIdAsync(
        Guid id, 
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting task {TaskId} for user {UserId}", id, userId);
        
        var entity = await _repository.GetByIdWithRelationsAsync(id, userId, organizationId, ct);
        
        if (entity == null)
        {
            _logger.LogWarning("Task {TaskId} not found for user {UserId}", id, userId);
            return DomainErrors.Task.NotFound;
        }
        
        return Map(entity);
    }

    /// <inheritdoc />
    public async Task<Result<TaskDto>> CreateAsync(
        Guid userId, 
        Guid? organizationId, 
        CreateTaskRequest request, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Creating task for user {UserId} with title '{Title}'", userId, request.Title);
        
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            _logger.LogWarning("Task creation failed - title is required");
            return DomainErrors.Task.TitleRequired;
        }

        try
        {
            var status = TryParseStatus(request.Status, out var s) ? s : Domain.Enums.TaskStatus.Todo;
            var priority = TryParsePriority(request.Priority, out var p) ? p : TaskPriority.None;
            
            var entity = new TaskItem
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrganizationId = organizationId,
                Title = request.Title,
                Description = request.Description,
                DueDateUtc = request.DueDateUtc,
                ReminderDateUtc = request.ReminderDateUtc,
                Status = status,
                Priority = priority,
                Completed = status == Domain.Enums.TaskStatus.Completed,
                LeadId = request.LeadId,
                DealId = request.DealId,
                ContactId = request.ContactId,
                AssigneeId = request.AssigneeId,
                Notes = request.Notes,
                CreatedAtUtc = DateTime.UtcNow,
            };
            
            entity = await _repository.AddAsync(entity, ct);
            
            _logger.LogInformation("Successfully created task {TaskId} with title '{Title}'", entity.Id, entity.Title);
            return Map(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating task for user {UserId}", userId);
            return DomainErrors.General.ServerError;
        }
    }

    /// <inheritdoc />
    public async Task<Result<TaskDto>> UpdateAsync(
        Guid id, 
        Guid userId, 
        Guid? organizationId, 
        UpdateTaskRequest request, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Updating task {TaskId} for user {UserId}", id, userId);
        
        var existing = await _repository.GetByIdWithRelationsAsync(id, userId, organizationId, ct);
        
        if (existing == null)
        {
            _logger.LogWarning("Task {TaskId} not found for update", id);
            return DomainErrors.Task.NotFound;
        }
        
        try
        {
            // Update title if provided (reject empty/whitespace titles)
            if (request.Title != null)
            {
                if (string.IsNullOrWhiteSpace(request.Title))
                    return DomainErrors.General.ValidationError;
                existing.Title = request.Title.Trim();
            }
            
            // Update description if provided
            if (request.Description != null) existing.Description = request.Description;
            
            // Update due date
            if (request.ClearDueDate == true)
                existing.DueDateUtc = null;
            else if (request.DueDateUtc != null)
                existing.DueDateUtc = request.DueDateUtc;
            
            // Update reminder date
            if (request.ClearReminderDate == true)
                existing.ReminderDateUtc = null;
            else if (request.ReminderDateUtc != null)
                existing.ReminderDateUtc = request.ReminderDateUtc;
            
            // Update status
            if (request.Status != null && TryParseStatus(request.Status, out var status))
            {
                var wasCompleted = existing.Status == Domain.Enums.TaskStatus.Completed;
                existing.Status = status;
                existing.Completed = status == Domain.Enums.TaskStatus.Completed;
                
                // Set completed timestamp
                if (status == Domain.Enums.TaskStatus.Completed && !wasCompleted)
                    existing.CompletedAtUtc = DateTime.UtcNow;
                else if (status != Domain.Enums.TaskStatus.Completed && wasCompleted)
                    existing.CompletedAtUtc = null;
            }
            
            // Legacy completed field support
            if (request.Completed != null)
            {
                existing.Completed = request.Completed.Value;
                if (request.Completed.Value)
                {
                    existing.Status = Domain.Enums.TaskStatus.Completed;
                    existing.CompletedAtUtc ??= DateTime.UtcNow;
                }
                else
                {
                    existing.Status = Domain.Enums.TaskStatus.Todo;
                    existing.CompletedAtUtc = null;
                }
            }
            
            // Update priority
            if (request.Priority != null && TryParsePriority(request.Priority, out var priority))
            {
                existing.Priority = priority;
            }
            
            // Update lead link
            if (request.ClearLead == true)
                existing.LeadId = null;
            else if (request.LeadId != null)
                existing.LeadId = request.LeadId;
            
            // Update deal link
            if (request.ClearDeal == true)
                existing.DealId = null;
            else if (request.DealId != null)
                existing.DealId = request.DealId;
            
            // Update contact link
            if (request.ClearContact == true)
                existing.ContactId = null;
            else if (request.ContactId != null)
                existing.ContactId = request.ContactId;
            
            // Update assignee
            if (request.ClearAssignee == true)
                existing.AssigneeId = null;
            else if (request.AssigneeId != null)
                existing.AssigneeId = request.AssigneeId;
            
            // Update notes
            if (request.Notes != null) existing.Notes = request.Notes;
            
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.UpdatedByUserId = userId;
            
            existing = await _repository.UpdateAsync(existing, userId, organizationId, ct);
            
            if (existing == null)
            {
                _logger.LogWarning("Task {TaskId} update returned null", id);
                return DomainErrors.Task.NotFound;
            }
            
            _logger.LogInformation("Successfully updated task {TaskId}", id);
            return Map(existing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating task {TaskId}", id);
            return DomainErrors.General.ServerError;
        }
    }

    /// <inheritdoc />
    public async Task<Result<TaskDto>> UpdateStatusAsync(
        Guid id, 
        Guid userId, 
        Guid? organizationId, 
        string status, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Updating status of task {TaskId} to '{Status}'", id, status);
        
        if (!TryParseStatus(status, out var taskStatus))
        {
            _logger.LogWarning("Invalid task status: {Status}", status);
            return DomainErrors.Task.InvalidStatus;
        }
            
        var existing = await _repository.GetByIdWithRelationsAsync(id, userId, organizationId, ct);
        
        if (existing == null)
        {
            _logger.LogWarning("Task {TaskId} not found for status update", id);
            return DomainErrors.Task.NotFound;
        }
        
        try
        {
            var wasCompleted = existing.Status == Domain.Enums.TaskStatus.Completed;
            existing.Status = taskStatus;
            existing.Completed = taskStatus == Domain.Enums.TaskStatus.Completed;
            
            // Set completed timestamp
            if (taskStatus == Domain.Enums.TaskStatus.Completed && !wasCompleted)
                existing.CompletedAtUtc = DateTime.UtcNow;
            else if (taskStatus != Domain.Enums.TaskStatus.Completed && wasCompleted)
                existing.CompletedAtUtc = null;
            
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.UpdatedByUserId = userId;
            
            existing = await _repository.UpdateAsync(existing, userId, organizationId, ct);
            
            if (existing == null)
            {
                _logger.LogWarning("Task {TaskId} status update returned null", id);
                return DomainErrors.Task.NotFound;
            }
            
            _logger.LogInformation("Successfully updated task {TaskId} status to '{Status}'", id, status);
            return Map(existing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating task {TaskId} status", id);
            return DomainErrors.General.ServerError;
        }
    }

    /// <inheritdoc />
    public async Task<Result<TaskDto>> AssignTaskAsync(
        Guid id, 
        Guid userId, 
        Guid? organizationId, 
        Guid? assigneeId, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Assigning task {TaskId} to user {AssigneeId}", id, assigneeId);
        
        var existing = await _repository.GetByIdWithRelationsAsync(id, userId, organizationId, ct);
        
        if (existing == null)
        {
            _logger.LogWarning("Task {TaskId} not found for assignment", id);
            return DomainErrors.Task.NotFound;
        }
        
        try
        {
            existing.AssigneeId = assigneeId;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.UpdatedByUserId = userId;
            
            existing = await _repository.UpdateAsync(existing, userId, organizationId, ct);
            
            if (existing == null)
            {
                _logger.LogWarning("Task {TaskId} assignment returned null", id);
                return DomainErrors.Task.NotFound;
            }
            
            _logger.LogInformation("Successfully assigned task {TaskId} to user {AssigneeId}", id, assigneeId);
            return Map(existing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning task {TaskId}", id);
            return DomainErrors.General.ServerError;
        }
    }

    /// <inheritdoc />
    public async Task<Result<TaskDto>> LinkToLeadAsync(
        Guid id, 
        Guid userId, 
        Guid? organizationId, 
        Guid? leadId, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Linking task {TaskId} to lead {LeadId}", id, leadId);
        
        var existing = await _repository.GetByIdWithRelationsAsync(id, userId, organizationId, ct);
        
        if (existing == null)
        {
            _logger.LogWarning("Task {TaskId} not found for lead linking", id);
            return DomainErrors.Task.NotFound;
        }
        
        try
        {
            existing.LeadId = leadId;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.UpdatedByUserId = userId;
            
            existing = await _repository.UpdateAsync(existing, userId, organizationId, ct);
            
            if (existing == null)
            {
                _logger.LogWarning("Task {TaskId} lead linking returned null", id);
                return DomainErrors.Task.NotFound;
            }
            
            _logger.LogInformation("Successfully linked task {TaskId} to lead {LeadId}", id, leadId);
            return Map(existing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking task {TaskId} to lead", id);
            return DomainErrors.General.ServerError;
        }
    }

    /// <inheritdoc />
    public async Task<Result<TaskDto>> LinkToDealAsync(
        Guid id, 
        Guid userId, 
        Guid? organizationId, 
        Guid? dealId, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Linking task {TaskId} to deal {DealId}", id, dealId);
        
        var existing = await _repository.GetByIdWithRelationsAsync(id, userId, organizationId, ct);
        
        if (existing == null)
        {
            _logger.LogWarning("Task {TaskId} not found for deal linking", id);
            return DomainErrors.Task.NotFound;
        }
        
        try
        {
            existing.DealId = dealId;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.UpdatedByUserId = userId;
            
            existing = await _repository.UpdateAsync(existing, userId, organizationId, ct);
            
            if (existing == null)
            {
                _logger.LogWarning("Task {TaskId} deal linking returned null", id);
                return DomainErrors.Task.NotFound;
            }
            
            _logger.LogInformation("Successfully linked task {TaskId} to deal {DealId}", id, dealId);
            return Map(existing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error linking task {TaskId} to deal", id);
            return DomainErrors.General.ServerError;
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(
        Guid id, 
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting task {TaskId} for user {UserId}", id, userId);
        
        try
        {
            var deleted = await _repository.DeleteAsync(id, userId, organizationId, ct);
            
            if (!deleted)
            {
                _logger.LogWarning("Task {TaskId} not found for deletion", id);
                return DomainErrors.Task.NotFound;
            }
            
            _logger.LogInformation("Successfully deleted task {TaskId}", id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting task {TaskId}", id);
            return DomainErrors.General.ServerError;
        }
    }

    /// <inheritdoc />
    public async Task<TaskStatsDto> GetStatsAsync(
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting task stats for user {UserId}", userId);
        
        var tasks = await _repository.GetByUserIdAsync(userId, organizationId, ct);
        var now = DateTime.UtcNow;
        var today = now.Date;
        
        return new TaskStatsDto(
            Total: tasks.Count,
            Todo: tasks.Count(t => t.Status == Domain.Enums.TaskStatus.Todo),
            InProgress: tasks.Count(t => t.Status == Domain.Enums.TaskStatus.InProgress),
            Completed: tasks.Count(t => t.Status == Domain.Enums.TaskStatus.Completed),
            Cancelled: tasks.Count(t => t.Status == Domain.Enums.TaskStatus.Cancelled),
            Overdue: tasks.Count(t => t.Status != Domain.Enums.TaskStatus.Completed && 
                                      t.Status != Domain.Enums.TaskStatus.Cancelled &&
                                      t.DueDateUtc != null && t.DueDateUtc < now),
            DueToday: tasks.Count(t => t.Status != Domain.Enums.TaskStatus.Completed && 
                                       t.Status != Domain.Enums.TaskStatus.Cancelled &&
                                       t.DueDateUtc != null && 
                                       t.DueDateUtc.Value.Date == today),
            HighPriority: tasks.Count(t => t.Status != Domain.Enums.TaskStatus.Completed && 
                                          t.Status != Domain.Enums.TaskStatus.Cancelled &&
                                          t.Priority == TaskPriority.High)
        );
    }

    private static TaskDto Map(TaskItem e) =>
        new TaskDto(
            Id: e.Id,
            Title: e.Title,
            Description: e.Description,
            DueDateUtc: e.DueDateUtc,
            ReminderDateUtc: e.ReminderDateUtc,
            Status: StatusToString(e.Status),
            Priority: PriorityToString(e.Priority),
            Completed: e.Completed,
            LeadId: e.LeadId,
            DealId: e.DealId,
            ContactId: e.ContactId,
            AssigneeId: e.AssigneeId,
            AssigneeName: e.Assignee?.Name,
            LeadName: e.Lead?.Name,
            DealName: e.Deal?.Name,
            ContactName: e.Contact?.Name,
            Notes: e.Notes,
            CreatedAtUtc: e.CreatedAtUtc,
            UpdatedAtUtc: e.UpdatedAtUtc,
            CompletedAtUtc: e.CompletedAtUtc
        );

    private static string StatusToString(Domain.Enums.TaskStatus status) => status switch
    {
        Domain.Enums.TaskStatus.Todo => "todo",
        Domain.Enums.TaskStatus.InProgress => "in_progress",
        Domain.Enums.TaskStatus.Completed => "completed",
        Domain.Enums.TaskStatus.Cancelled => "cancelled",
        _ => "todo"
    };

    private static bool TryParseStatus(string? status, out Domain.Enums.TaskStatus result)
    {
        result = status?.ToLowerInvariant() switch
        {
            "todo" => Domain.Enums.TaskStatus.Todo,
            "in_progress" or "inprogress" => Domain.Enums.TaskStatus.InProgress,
            "completed" or "done" => Domain.Enums.TaskStatus.Completed,
            "cancelled" or "canceled" => Domain.Enums.TaskStatus.Cancelled,
            _ => Domain.Enums.TaskStatus.Todo
        };
        return !string.IsNullOrEmpty(status);
    }

    private static string PriorityToString(TaskPriority priority) => priority switch
    {
        TaskPriority.High => "high",
        TaskPriority.Medium => "medium",
        TaskPriority.Low => "low",
        _ => "none"
    };

    private static bool TryParsePriority(string? priority, out TaskPriority result)
    {
        result = priority?.ToLowerInvariant() switch
        {
            "high" => TaskPriority.High,
            "medium" => TaskPriority.Medium,
            "low" => TaskPriority.Low,
            "none" => TaskPriority.None,
            _ => TaskPriority.None
        };
        return !string.IsNullOrEmpty(priority);
    }

    /// <inheritdoc />
    public async Task<Result<BulkTaskResult>> BulkUpdateAsync(
        Guid userId,
        Guid? organizationId,
        BulkTaskRequest request,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Bulk {Action} for {Count} tasks by user {UserId}", 
            request.Action, request.TaskIds.Length, userId);

        if (request.TaskIds.Length == 0)
            return new BulkTaskResult(0, 0, 0);

        var validActions = new[] { "status", "priority", "assignee", "delete" };
        if (!validActions.Contains(request.Action.ToLowerInvariant()))
            return DomainErrors.General.ValidationError;

        int succeeded = 0;
        int failed = 0;

        foreach (var taskId in request.TaskIds)
        {
            try
            {
                Result result = request.Action.ToLowerInvariant() switch
                {
                    "status" when request.Status != null => 
                        await UpdateStatusAsync(taskId, userId, organizationId, request.Status, ct),
                    "priority" when request.Priority != null =>
                        await UpdatePriorityAsync(taskId, userId, organizationId, request.Priority, ct),
                    "assignee" =>
                        await AssignTaskAsync(taskId, userId, organizationId, request.AssigneeId, ct),
                    "delete" =>
                        await DeleteAsync(taskId, userId, organizationId, ct),
                    _ => DomainErrors.General.ValidationError
                };

                if (result.IsSuccess) succeeded++;
                else failed++;
            }
            catch
            {
                failed++;
            }
        }

        _logger.LogInformation("Bulk {Action} completed: {Succeeded}/{Total} succeeded", 
            request.Action, succeeded, request.TaskIds.Length);

        return new BulkTaskResult(request.TaskIds.Length, succeeded, failed);
    }

    private async Task<Result<TaskDto>> UpdatePriorityAsync(
        Guid id, Guid userId, Guid? organizationId, string priority, CancellationToken ct)
    {
        var existing = await _repository.GetByIdWithRelationsAsync(id, userId, organizationId, ct);
        if (existing == null) return DomainErrors.Task.NotFound;

        if (TryParsePriority(priority, out var parsed))
        {
            existing.Priority = parsed;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.UpdatedByUserId = userId;
            var updated = await _repository.UpdateAsync(existing, userId, organizationId, ct);
            return updated != null ? Map(updated) : DomainErrors.General.ServerError;
        }

        return DomainErrors.General.ValidationError;
    }
}
