namespace ACI.Application.DTOs;

public record CreateLeadRequest(string Name, string Email, string? Phone, Guid? CompanyId, string? Source, string Status);
