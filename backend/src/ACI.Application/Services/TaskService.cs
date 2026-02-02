using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

public class TaskService : ITaskService
{
    private readonly ITaskRepository _repository;

    public TaskService(ITaskRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<TaskDto>> GetTasksAsync(Guid userId, bool? overdueOnly, CancellationToken ct = default)
    {
        var list = overdueOnly == true
            ? await _repository.GetByUserIdAsync(userId, true, ct)
            : await _repository.GetByUserIdAsync(userId, ct);
        return list.Select(Map).ToList();
    }

    public async Task<TaskDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repository.GetByIdAsync(id, userId, ct);
        return entity == null ? null : Map(entity);
    }

    public async Task<TaskDto?> CreateAsync(Guid userId, CreateTaskRequest request, CancellationToken ct = default)
    {
        var entity = new TaskItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = request.Title,
            Description = request.Description,
            DueDateUtc = request.DueDateUtc,
            LeadId = request.LeadId,
            DealId = request.DealId,
            CreatedAtUtc = DateTime.UtcNow,
        };
        entity = await _repository.AddAsync(entity, ct);
        return Map(entity);
    }

    public async Task<TaskDto?> UpdateAsync(Guid id, Guid userId, UpdateTaskRequest request, CancellationToken ct = default)
    {
        var existing = await _repository.GetByIdAsync(id, userId, ct);
        if (existing == null) return null;
        if (request.Title != null) existing.Title = request.Title;
        if (request.Description != null) existing.Description = request.Description;
        if (request.DueDateUtc != null) existing.DueDateUtc = request.DueDateUtc;
        if (request.Completed != null) existing.Completed = request.Completed.Value;
        if (request.LeadId != null) existing.LeadId = request.LeadId;
        if (request.DealId != null) existing.DealId = request.DealId;
        existing = await _repository.UpdateAsync(existing, userId, ct);
        return existing == null ? null : Map(existing);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default)
    {
        return await _repository.DeleteAsync(id, userId, ct);
    }

    private static TaskDto Map(TaskItem e) =>
        new TaskDto(e.Id, e.Title, e.Description, e.DueDateUtc, e.Completed, e.LeadId, e.DealId);
}
