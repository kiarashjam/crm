using ACI.Domain.Entities;
using ACI.Domain.Enums;

namespace ACI.Application.Interfaces;

public interface ITaskRepository
{
    Task<(IReadOnlyList<TaskItem> Items, int TotalCount)> GetPagedAsync(
        Guid userId,
        Guid? organizationId,
        int skip,
        int take,
        string? search = null,
        string? status = null,
        string? priority = null,
        bool? overdueOnly = null,
        bool mineOnly = false,
        CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetByUserIdAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    /// <summary>The tasks that are this person's work (see WorkScope), whatever their status.</summary>
    Task<IReadOnlyList<TaskItem>> GetMineAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    /// <summary>Open tasks, the person's own or the whole team's, soonest due first: at most <paramref name="take"/>.</summary>
    Task<IReadOnlyList<TaskItem>> GetOpenAsync(Guid userId, Guid? organizationId, bool mineOnly, int take, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetByUserIdAsync(Guid userId, Guid? organizationId, bool? overdueOnly, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetByAssigneeIdAsync(Guid assigneeId, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetByStatusAsync(Guid userId, Guid? organizationId, ACI.Domain.Enums.TaskStatus status, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetByLeadIdAsync(Guid leadId, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetByDealIdAsync(Guid dealId, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<TaskItem>> GetByContactIdAsync(Guid contactId, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<TaskItem?> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<TaskItem?> GetByIdWithRelationsAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<TaskItem> AddAsync(TaskItem taskItem, CancellationToken ct = default);
    Task<TaskItem?> UpdateAsync(TaskItem taskItem, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<int> GetCountByStatusAsync(Guid userId, Guid? organizationId, ACI.Domain.Enums.TaskStatus status, CancellationToken ct = default);
    Task<int> GetCountByLeadIdAsync(Guid leadId, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<int> GetCountByDealIdAsync(Guid dealId, Guid userId, Guid? organizationId, CancellationToken ct = default);
}
