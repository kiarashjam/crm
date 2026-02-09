using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class TaskRepository : ITaskRepository
{
    private readonly AppDbContext _db;

    public TaskRepository(AppDbContext db) => _db = db;

    private static IQueryable<TaskItem> FilterByUserAndOrg(IQueryable<TaskItem> q, Guid userId, Guid? organizationId) =>
        q.Where(t => t.UserId == userId && (organizationId == null ? t.OrganizationId == null : t.OrganizationId == organizationId));

    private static IQueryable<TaskItem> FilterByOrgMember(IQueryable<TaskItem> q, Guid? organizationId) =>
        q.Where(t => organizationId == null ? t.OrganizationId == null : t.OrganizationId == organizationId);

    private static IQueryable<TaskItem> IncludeRelations(IQueryable<TaskItem> q) =>
        q.Include(t => t.Assignee)
         .Include(t => t.Lead)
         .Include(t => t.Deal)
         .Include(t => t.Contact);

    private static IQueryable<TaskItem> OrderTasks(IQueryable<TaskItem> q) =>
        q.OrderBy(t => t.DueDateUtc ?? DateTime.MaxValue)
         .ThenByDescending(t => t.Priority);

    private static IQueryable<TaskItem> ApplySearch(IQueryable<TaskItem> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return query;
        var q = search.Trim().ToLowerInvariant();
        return query.Where(t => 
            t.Title.ToLower().Contains(q) || 
            (t.Description != null && t.Description.ToLower().Contains(q)) ||
            (t.Notes != null && t.Notes.ToLower().Contains(q)));
    }

    private static IQueryable<TaskItem> ApplyStatusFilter(IQueryable<TaskItem> query, string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return query;
        return status.ToLowerInvariant() switch
        {
            "todo" => query.Where(t => t.Status == Domain.Enums.TaskStatus.Todo),
            "in_progress" => query.Where(t => t.Status == Domain.Enums.TaskStatus.InProgress),
            "completed" => query.Where(t => t.Status == Domain.Enums.TaskStatus.Completed),
            "cancelled" => query.Where(t => t.Status == Domain.Enums.TaskStatus.Cancelled),
            _ => query
        };
    }

    private static IQueryable<TaskItem> ApplyPriorityFilter(IQueryable<TaskItem> query, string? priority)
    {
        if (string.IsNullOrWhiteSpace(priority)) return query;
        return priority.ToLowerInvariant() switch
        {
            "none" => query.Where(t => t.Priority == TaskPriority.None),
            "low" => query.Where(t => t.Priority == TaskPriority.Low),
            "medium" => query.Where(t => t.Priority == TaskPriority.Medium),
            "high" => query.Where(t => t.Priority == TaskPriority.High),
            _ => query
        };
    }

    private static IQueryable<TaskItem> ApplyOverdueFilter(IQueryable<TaskItem> query, bool? overdueOnly)
    {
        if (overdueOnly != true) return query;
        var now = DateTime.UtcNow;
        return query.Where(t => 
            t.Status != Domain.Enums.TaskStatus.Completed && 
            t.Status != Domain.Enums.TaskStatus.Cancelled &&
            t.DueDateUtc != null && t.DueDateUtc < now);
    }

    public async Task<(IReadOnlyList<TaskItem> Items, int TotalCount)> GetPagedAsync(
        Guid userId,
        Guid? organizationId,
        int skip,
        int take,
        string? search = null,
        string? status = null,
        string? priority = null,
        bool? overdueOnly = null,
        CancellationToken ct = default)
    {
        var query = FilterByUserAndOrg(_db.TaskItems, userId, organizationId);
        query = ApplySearch(query, search);
        query = ApplyStatusFilter(query, status);
        query = ApplyPriorityFilter(query, priority);
        query = ApplyOverdueFilter(query, overdueOnly);

        var totalCount = await query.CountAsync(ct);
        var items = await OrderTasks(IncludeRelations(query))
            .Skip(skip)
            .Take(take)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<TaskItem>> GetByUserIdAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await OrderTasks(IncludeRelations(FilterByUserAndOrg(_db.TaskItems, userId, organizationId)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TaskItem>> GetByUserIdAsync(Guid userId, Guid? organizationId, bool? overdueOnly, CancellationToken ct = default)
    {
        var query = IncludeRelations(FilterByUserAndOrg(_db.TaskItems, userId, organizationId));
            
        if (overdueOnly == true)
        {
            var now = DateTime.UtcNow;
            query = query.Where(t => t.Status != Domain.Enums.TaskStatus.Completed && 
                                     t.Status != Domain.Enums.TaskStatus.Cancelled &&
                                     t.DueDateUtc != null && t.DueDateUtc < now);
        }
        return await OrderTasks(query).ToListAsync(ct);
    }

    // SECURITY FIX: When in an org, allow team-wide visibility (see tasks assigned to any org member).
    // When personal (no org), restrict to the caller's own tasks to prevent data leakage.
    public async Task<IReadOnlyList<TaskItem>> GetByAssigneeIdAsync(Guid assigneeId, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var query = organizationId != null
            ? FilterByOrgMember(_db.TaskItems, organizationId)
            : FilterByUserAndOrg(_db.TaskItems, userId, null);
        return await OrderTasks(IncludeRelations(query.Where(t => t.AssigneeId == assigneeId))).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TaskItem>> GetByStatusAsync(Guid userId, Guid? organizationId, Domain.Enums.TaskStatus status, CancellationToken ct = default) =>
        await OrderTasks(IncludeRelations(FilterByUserAndOrg(_db.TaskItems, userId, organizationId)
            .Where(t => t.Status == status)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TaskItem>> GetByLeadIdAsync(Guid leadId, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await OrderTasks(IncludeRelations(FilterByUserAndOrg(_db.TaskItems, userId, organizationId)
            .Where(t => t.LeadId == leadId)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TaskItem>> GetByDealIdAsync(Guid dealId, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await OrderTasks(IncludeRelations(FilterByUserAndOrg(_db.TaskItems, userId, organizationId)
            .Where(t => t.DealId == dealId)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TaskItem>> GetByContactIdAsync(Guid contactId, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await OrderTasks(IncludeRelations(FilterByUserAndOrg(_db.TaskItems, userId, organizationId)
            .Where(t => t.ContactId == contactId)))
            .ToListAsync(ct);

    public async Task<TaskItem?> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await FilterByUserAndOrg(_db.TaskItems, userId, organizationId)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<TaskItem?> GetByIdWithRelationsAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await IncludeRelations(FilterByUserAndOrg(_db.TaskItems, userId, organizationId))
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<TaskItem> AddAsync(TaskItem taskItem, CancellationToken ct = default)
    {
        _db.TaskItems.Add(taskItem);
        await _db.SaveChangesAsync(ct);
        
        // Reload with relations
        return await _db.TaskItems
            .Include(t => t.Assignee)
            .Include(t => t.Lead)
            .Include(t => t.Deal)
            .Include(t => t.Contact)
            .FirstAsync(t => t.Id == taskItem.Id, ct);
    }

    public async Task<TaskItem?> UpdateAsync(TaskItem taskItem, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var existing = await FilterByUserAndOrg(_db.TaskItems, userId, organizationId)
            .FirstOrDefaultAsync(t => t.Id == taskItem.Id, ct);
        if (existing == null) return null;
        
        existing.Title = taskItem.Title;
        existing.Description = taskItem.Description;
        existing.DueDateUtc = taskItem.DueDateUtc;
        existing.ReminderDateUtc = taskItem.ReminderDateUtc;
        existing.Status = taskItem.Status;
        existing.Priority = taskItem.Priority;
        existing.Completed = taskItem.Completed;
        existing.LeadId = taskItem.LeadId;
        existing.DealId = taskItem.DealId;
        existing.ContactId = taskItem.ContactId;
        existing.AssigneeId = taskItem.AssigneeId;
        existing.Notes = taskItem.Notes;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        existing.UpdatedByUserId = userId;
        existing.CompletedAtUtc = taskItem.CompletedAtUtc;
        
        await _db.SaveChangesAsync(ct);
        
        // Reload with relations
        return await _db.TaskItems
            .Include(t => t.Assignee)
            .Include(t => t.Lead)
            .Include(t => t.Deal)
            .Include(t => t.Contact)
            .FirstAsync(t => t.Id == existing.Id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var existing = await FilterByUserAndOrg(_db.TaskItems, userId, organizationId)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
        if (existing == null) return false;
        _db.TaskItems.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> GetCountByStatusAsync(Guid userId, Guid? organizationId, Domain.Enums.TaskStatus status, CancellationToken ct = default) =>
        await FilterByUserAndOrg(_db.TaskItems, userId, organizationId)
            .CountAsync(t => t.Status == status, ct);

    public async Task<int> GetCountByLeadIdAsync(Guid leadId, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await FilterByUserAndOrg(_db.TaskItems, userId, organizationId)
            .CountAsync(t => t.LeadId == leadId, ct);

    public async Task<int> GetCountByDealIdAsync(Guid dealId, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await FilterByUserAndOrg(_db.TaskItems, userId, organizationId)
            .CountAsync(t => t.DealId == dealId, ct);
}
