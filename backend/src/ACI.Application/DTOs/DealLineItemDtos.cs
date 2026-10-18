namespace ACI.Application.DTOs;

/// <summary>
/// A deal line item. Property names match the client's DealLineItem
/// (src/app/api/products.ts) so no client-side mapping is needed.
/// </summary>
public record DealLineItemDto(Guid Id, Guid DealId, string? ProductId, string Name, decimal Quantity, decimal UnitPrice);

public record CreateDealLineItemRequest(string? ProductId, string Name, decimal Quantity, decimal UnitPrice);

/// <summary>A partial update: only the properties sent are changed.</summary>
public record UpdateDealLineItemRequest(string? ProductId, string? Name, decimal? Quantity, decimal? UnitPrice);
