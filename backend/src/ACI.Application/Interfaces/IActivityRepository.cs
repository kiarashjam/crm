using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface IActivityRepository
{
    Task<(IReadOnlyList<Activity> Items, int TotalCount)> GetPagedAsync(
        Guid userId,
        Guid? organizationId,
        int skip,
        int take,
        string? search = null,
        string? activityType = null,
        CancellationToken ct = default);
    Task<IReadOnlyList<Activity>> GetByUserIdAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<Activity>> GetByContactIdAsync(Guid contactId, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<Activity>> GetByDealIdAsync(Guid dealId, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<Activity>> GetByLeadIdAsync(Guid leadId, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<Activity?> GetByIdAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<Activity> AddAsync(Activity activity, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, DateTime>> GetLastActivityByContactIdsAsync(Guid userId, Guid? organizationId, IEnumerable<Guid> contactIds, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, DateTime>> GetLastActivityByDealIdsAsync(Guid userId, Guid? organizationId, IEnumerable<Guid> dealIds, CancellationToken ct = default);
}
