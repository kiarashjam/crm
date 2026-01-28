using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

public interface IDealService
{
    Task<IReadOnlyList<DealDto>> GetDealsAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<DealDto>> SearchAsync(Guid userId, string query, CancellationToken ct = default);
    Task<DealDto?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
}
