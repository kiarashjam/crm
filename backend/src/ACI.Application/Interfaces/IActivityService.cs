using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IActivityService
{
    Task<IReadOnlyList<ActivityDto>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ActivityDto>> GetByContactIdAsync(Guid contactId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ActivityDto>> GetByDealIdAsync(Guid dealId, Guid userId, CancellationToken ct = default);
    Task<ActivityDto?> CreateAsync(Guid userId, CreateActivityRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
}
