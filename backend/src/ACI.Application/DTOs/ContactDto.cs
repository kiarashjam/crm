namespace ACI.Application.DTOs;

public record ContactDto(Guid Id, string Name, string Email, Guid? CompanyId);
