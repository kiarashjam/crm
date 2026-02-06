namespace ACI.Application.DTOs;

public record CompanyDto(Guid Id, string Name, string? Domain, string? Industry, string? Size);
