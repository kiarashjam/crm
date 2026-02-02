namespace ACI.Application.DTOs;

public record UpdateTaskRequest(string? Title, string? Description, DateTime? DueDateUtc, bool? Completed, Guid? LeadId, Guid? DealId);
