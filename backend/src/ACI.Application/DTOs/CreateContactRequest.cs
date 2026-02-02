namespace ACI.Application.DTOs;

public record CreateContactRequest(string Name, string Email, string? Phone, Guid? CompanyId);
