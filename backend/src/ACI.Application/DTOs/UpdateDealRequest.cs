namespace ACI.Application.DTOs;

public record UpdateDealRequest(string? Name, string? Value, string? Stage, Guid? CompanyId, Guid? ContactId, DateTime? ExpectedCloseDateUtc, bool? IsWon);
