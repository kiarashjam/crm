namespace ACI.Application.DTOs;

public record LeadStatusDto(Guid Id, Guid OrganizationId, string Name, int DisplayOrder);
