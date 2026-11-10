namespace ACI.Application.DTOs;

/// <summary>A pipeline with its stages in board order. The board draws its columns from these.</summary>
/// <param name="StaleDealDays">
/// Days without activity after which an open deal in it is stale: null when the pipeline
/// follows the workspace's setting, 0 when its deals are never flagged.
/// </param>
public record PipelineDto(Guid Id, Guid OrganizationId, string Name, int DisplayOrder, IReadOnlyList<DealStageDto> DealStages, int? StaleDealDays = null);
