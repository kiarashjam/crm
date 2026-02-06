using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface ICopyHistoryRepository
{
    Task<IReadOnlyList<CopyHistoryItem>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<CopyHistoryItem>> GetByUserIdAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<CopyHistoryItem?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CopyHistoryItem> AddAsync(CopyHistoryItem item, CancellationToken ct = default);
    Task<CopyHistoryItem> UpdateAsync(CopyHistoryItem item, CancellationToken ct = default);
    Task<int> CountByUserIdAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<int> CountSentThisWeekAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
}
