using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> ListAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<Product?> GetAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task AddAsync(Product product, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
}
