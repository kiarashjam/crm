using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface ITaskService
{
    Task<IReadOnlyList<TaskDto>> GetTasksAsync(Guid userId, bool? overdueOnly, CancellationToken ct = default);
    Task<TaskDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<TaskDto?> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken ct = default);
    Task<TaskDto?> UpdateAsync(Guid id, Guid userId, UpdateTaskRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
}
