namespace ACI.Application.DTOs;

public record LeadDto(Guid Id, string Name, string Email, string? Phone, Guid? CompanyId, string? Source, string Status);
