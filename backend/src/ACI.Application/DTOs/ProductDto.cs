namespace ACI.Application.DTOs;

public record ProductDto(Guid Id, string Name, string? Sku, decimal UnitPrice, string? Currency);

public record SaveProductRequest(string? Name, string? Sku, decimal UnitPrice, string? Currency);
