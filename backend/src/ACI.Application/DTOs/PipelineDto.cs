namespace ACI.Application.DTOs;

public record PipelineDto(Guid Id, Guid OrganizationId, string Name, int DisplayOrder);
