using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Repositories;

public sealed class ProductRepository : IProductRepository
{
    private readonly AppDbContext _db;

    public ProductRepository(AppDbContext db) => _db = db;

    private static IQueryable<Product> Scope(IQueryable<Product> query, Guid userId, Guid? organizationId) =>
        organizationId == null
            ? query.Where(p => p.OwnerUserId == userId && p.OrganizationId == null)
            : query.Where(p => p.OrganizationId == organizationId);

    public async Task<IReadOnlyList<Product>> ListAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await Scope(_db.Products.AsNoTracking(), userId, organizationId)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

    public Task<Product?> GetAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        Scope(_db.Products, userId, organizationId).FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddAsync(Product product, CancellationToken ct = default)
    {
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var row = await GetAsync(id, userId, organizationId, ct);
        if (row == null) return false;
        _db.Products.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
