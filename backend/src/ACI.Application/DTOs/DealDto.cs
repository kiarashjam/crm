namespace ACI.Application.DTOs;

public record DealDto(Guid Id, string Name, string Value, string? Stage, Guid? CompanyId);
