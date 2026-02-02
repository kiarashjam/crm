namespace ACI.Application.DTOs;

public record CreateDealRequest(string Name, string Value, string? Stage, Guid? CompanyId, Guid? ContactId, DateTime? ExpectedCloseDateUtc);
