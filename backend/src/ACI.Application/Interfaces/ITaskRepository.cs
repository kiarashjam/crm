using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface ITaskRepository
{
    Task<IReadOnlyList<TaskItem>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetByUserIdAsync(Guid userId, bool? overdueOnly, CancellationToken ct = default);
    Task<TaskItem?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<TaskItem> AddAsync(TaskItem taskItem, CancellationToken ct = default);
    Task<TaskItem?> UpdateAsync(TaskItem taskItem, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
}
