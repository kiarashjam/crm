namespace ACI.Application.DTOs;

public record LeadSourceDto(Guid Id, Guid OrganizationId, string Name, int DisplayOrder);
