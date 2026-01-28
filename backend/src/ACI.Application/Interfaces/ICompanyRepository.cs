using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface ICompanyRepository
{
    Task<IReadOnlyList<Company>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Company?> GetByIdAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<Company> AddAsync(Company company, CancellationToken ct = default);
}
