using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

/// <summary>
/// A deal's line items. Anyone who can edit the deal can edit its lines; they are
/// reached only through a deal in the caller's workspace.
/// </summary>
public sealed class DealLineItemService : IDealLineItemService
{
    private const decimal MaxQuantity = 1_000_000m;
    private const decimal MaxUnitPrice = 1_000_000_000_000m;

    private readonly IDealLineItemRepository _repository;
    private readonly IWorkspaceRecords _records;

    public DealLineItemService(IDealLineItemRepository repository, IWorkspaceRecords records)
    {
        _repository = repository;
        _records = records;
    }

    public async Task<Result<IReadOnlyList<DealLineItemDto>>> ListAsync(Guid userId, Guid? organizationId, Guid dealId, CancellationToken ct = default)
    {
        if (!await _records.ExistsAsync(RecordTypes.Deal, dealId, userId, organizationId, ct))
            return Result.Failure<IReadOnlyList<DealLineItemDto>>(DomainErrors.DealLineItem.DealNotFound);
        var items = await _repository.ListAsync(dealId, ct);
        return Result.Success<IReadOnlyList<DealLineItemDto>>(items.Select(ToDto).ToList());
    }

    public async Task<Result<DealLineItemDto>> AddAsync(
        Guid userId, Guid? organizationId, Guid dealId, CreateDealLineItemRequest request, CancellationToken ct = default)
    {
        var error = Check(request.Name, request.Quantity, request.UnitPrice);
        if (error != null) return Result.Failure<DealLineItemDto>(error);
        if (!await _records.ExistsAsync(RecordTypes.Deal, dealId, userId, organizationId, ct))
            return Result.Failure<DealLineItemDto>(DomainErrors.DealLineItem.DealNotFound);

        var item = new DealLineItem
        {
            Id = Guid.NewGuid(),
            DealId = dealId,
            ProductId = CleanProductId(request.ProductId),
            Name = Clip(request.Name.Trim(), 200),
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            CreatedAtUtc = DateTime.UtcNow,
        };
        await _repository.AddAsync(item, ct);
        return Result.Success(ToDto(item));
    }

    public async Task<Result<DealLineItemDto>> UpdateAsync(
        Guid userId, Guid? organizationId, Guid id, UpdateDealLineItemRequest request, CancellationToken ct = default)
    {
        var item = await _repository.FindAsync(id, userId, organizationId, ct);
        if (item == null) return Result.Failure<DealLineItemDto>(DomainErrors.DealLineItem.NotFound);

        var error = Check(request.Name ?? item.Name, request.Quantity ?? item.Quantity, request.UnitPrice ?? item.UnitPrice);
        if (error != null) return Result.Failure<DealLineItemDto>(error);

        if (request.Name != null) item.Name = Clip(request.Name.Trim(), 200);
        if (request.Quantity is { } quantity) item.Quantity = quantity;
        if (request.UnitPrice is { } price) item.UnitPrice = price;
        if (request.ProductId != null) item.ProductId = CleanProductId(request.ProductId);
        await _repository.SaveAsync(item, ct);
        return Result.Success(ToDto(item));
    }

    public async Task<Result> DeleteAsync(Guid userId, Guid? organizationId, Guid id, CancellationToken ct = default)
    {
        var item = await _repository.FindAsync(id, userId, organizationId, ct);
        if (item == null) return Result.Failure(DomainErrors.DealLineItem.NotFound);
        await _repository.DeleteAsync(item, ct);
        return Result.Success();
    }

    private static Error? Check(string? name, decimal quantity, decimal unitPrice)
    {
        if (string.IsNullOrWhiteSpace(name)) return DomainErrors.DealLineItem.NameRequired;
        if (quantity <= 0) return DomainErrors.DealLineItem.QuantityInvalid;
        if (unitPrice < 0) return DomainErrors.DealLineItem.PriceInvalid;
        if (quantity > MaxQuantity || unitPrice > MaxUnitPrice) return DomainErrors.DealLineItem.TooLarge;
        return null;
    }

    private static string? CleanProductId(string? productId) =>
        string.IsNullOrWhiteSpace(productId) ? null : Clip(productId.Trim(), 64);

    private static DealLineItemDto ToDto(DealLineItem i) => new(i.Id, i.DealId, i.ProductId, i.Name, i.Quantity, i.UnitPrice);

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];
}
