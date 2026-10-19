using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

public interface IProductService
{
    Task<IReadOnlyList<ProductDto>> ListAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<Result<ProductDto>> CreateAsync(Guid userId, Guid? organizationId, SaveProductRequest request, CancellationToken ct = default);
    Task<Result> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default);
}

public sealed class ProductService : IProductService
{
    private readonly IProductRepository _products;

    public ProductService(IProductRepository products) => _products = products;

    public async Task<IReadOnlyList<ProductDto>> ListAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var rows = await _products.ListAsync(userId, organizationId, ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<Result<ProductDto>> CreateAsync(Guid userId, Guid? organizationId, SaveProductRequest request, CancellationToken ct = default)
    {
        var name = (request.Name ?? "").Trim();
        if (name.Length == 0)
            return new Error("Product.NameRequired", "A product needs a name.");
        if (name.Length > 200)
            return new Error("Product.NameTooLong", "A product name can be at most 200 characters.");
        if (request.UnitPrice < 0)
            return new Error("Product.PriceInvalid", "A price cannot be negative.");

        var row = new Product
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            OwnerUserId = userId,
            Name = name,
            Sku = string.IsNullOrWhiteSpace(request.Sku) ? null : request.Sku.Trim(),
            UnitPrice = request.UnitPrice,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? null : request.Currency.Trim().ToUpperInvariant(),
            CreatedAtUtc = DateTime.UtcNow,
        };
        await _products.AddAsync(row, ct);
        return ToDto(row);
    }

    public async Task<Result> DeleteAsync(Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var deleted = await _products.DeleteAsync(id, userId, organizationId, ct);
        return deleted ? Result.Success() : new Error("Product.NotFound", "That product was not found.");
    }

    private static ProductDto ToDto(Product p) => new(p.Id, p.Name, p.Sku, p.UnitPrice, p.Currency);
}
