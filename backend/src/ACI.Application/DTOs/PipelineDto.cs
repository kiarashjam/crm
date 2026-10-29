namespace ACI.Application.DTOs;

/// <summary>A pipeline with its stages in board order. The board draws its columns from these.</summary>
public record PipelineDto(Guid Id, Guid OrganizationId, string Name, int DisplayOrder, IReadOnlyList<DealStageDto> DealStages);
