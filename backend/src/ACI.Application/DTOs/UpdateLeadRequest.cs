namespace ACI.Application.DTOs;

public record UpdateLeadRequest(string? Name, string? Email, string? Phone, Guid? CompanyId, string? Source, string? Status);
