namespace ACI.Application.DTOs;

public record TaskDto(Guid Id, string Title, string? Description, DateTime? DueDateUtc, bool Completed, Guid? LeadId, Guid? DealId);
