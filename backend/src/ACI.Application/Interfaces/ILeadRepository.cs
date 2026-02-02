using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface ILeadRepository
{
    Task<IReadOnlyList<Lead>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Lead>> SearchAsync(Guid userId, string query, CancellationToken ct = default);
    Task<Lead?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<Lead> AddAsync(Lead lead, CancellationToken ct = default);
    Task<Lead?> UpdateAsync(Lead lead, Guid userId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken ct = default);
}
