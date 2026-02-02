namespace ACI.Application.DTOs;

public record ActivityDto(Guid Id, string Type, string? Subject, string? Body, Guid? ContactId, Guid? DealId, DateTime CreatedAtUtc);
