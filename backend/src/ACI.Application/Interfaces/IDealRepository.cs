using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface IDealRepository
{
    Task<IReadOnlyList<Deal>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Deal>> SearchAsync(Guid userId, string query, CancellationToken ct = default);
    Task<Deal?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<Deal> AddAsync(Deal deal, CancellationToken ct = default);
}
