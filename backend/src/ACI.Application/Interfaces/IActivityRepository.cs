using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface IActivityRepository
{
    Task<IReadOnlyList<Activity>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Activity>> GetByContactIdAsync(Guid contactId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Activity>> GetByDealIdAsync(Guid dealId, Guid userId, CancellationToken ct = default);
    Task<Activity?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<Activity> AddAsync(Activity activity, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, DateTime>> GetLastActivityByContactIdsAsync(Guid userId, IEnumerable<Guid> contactIds, CancellationToken ct = default);
    Task<IReadOnlyDictionary<Guid, DateTime>> GetLastActivityByDealIdsAsync(Guid userId, IEnumerable<Guid> dealIds, CancellationToken ct = default);
}
