namespace ACI.Application.DTOs;

public record UpdateContactRequest(string? Name, string? Email, string? Phone, Guid? CompanyId);
