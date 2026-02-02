namespace ACI.Application.DTOs;

public record CreateActivityRequest(string Type, string? Subject, string? Body, Guid? ContactId, Guid? DealId);
