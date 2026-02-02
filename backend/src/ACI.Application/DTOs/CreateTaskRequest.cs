namespace ACI.Application.DTOs;

public record CreateTaskRequest(string Title, string? Description, DateTime? DueDateUtc, Guid? LeadId, Guid? DealId);
