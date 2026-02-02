using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class TaskRepository : ITaskRepository
{
    private readonly AppDbContext _db;

    public TaskRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<TaskItem>> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await _db.TaskItems.Where(t => t.UserId == userId).OrderBy(t => t.DueDateUtc ?? DateTime.MaxValue).ToListAsync(ct);

    public async Task<IReadOnlyList<TaskItem>> GetByUserIdAsync(Guid userId, bool? overdueOnly, CancellationToken ct = default)
    {
        var query = _db.TaskItems.Where(t => t.UserId == userId);
        if (overdueOnly == true)
        {
            var now = DateTime.UtcNow;
            query = query.Where(t => !t.Completed && t.DueDateUtc != null && t.DueDateUtc < now);
        }
        return await query.OrderBy(t => t.DueDateUtc ?? DateTime.MaxValue).ToListAsync(ct);
    }

    public async Task<TaskItem?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default) =>
        await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);

    public async Task<TaskItem> AddAsync(TaskItem taskItem, CancellationToken ct = default)
    {
        _db.TaskItems.Add(taskItem);
        await _db.SaveChangesAsync(ct);
        return taskItem;
    }

    public async Task<TaskItem?> UpdateAsync(TaskItem taskItem, Guid userId, CancellationToken ct = default)
    {
        var existing = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == taskItem.Id && t.UserId == userId, ct);
        if (existing == null) return null;
        existing.Title = taskItem.Title;
        existing.Description = taskItem.Description;
        existing.DueDateUtc = taskItem.DueDateUtc;
        existing.Completed = taskItem.Completed;
        existing.LeadId = taskItem.LeadId;
        existing.DealId = taskItem.DealId;
        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var existing = await _db.TaskItems.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct);
        if (existing == null) return false;
        _db.TaskItems.Remove(existing);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
