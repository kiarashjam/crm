using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

public class DealStageService : IDealStageService
{
    private readonly IDealStageRepository _repository;
    private readonly IPipelineRepository _pipelineRepository;
    private readonly ILogger<DealStageService> _logger;

    public DealStageService(IDealStageRepository repository, IPipelineRepository pipelineRepository, ILogger<DealStageService> logger)
    {
        _repository = repository;
        _pipelineRepository = pipelineRepository;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<DealStageDto>>> GetByPipelineIdAsync(Guid pipelineId, Guid organizationId, CancellationToken ct = default)
    {
        _logger.LogInformation("Getting deal stages for pipeline {PipelineId} in organization {OrganizationId}", pipelineId, organizationId);

        var pipeline = await _pipelineRepository.GetByIdAsync(pipelineId, organizationId, ct);
        if (pipeline == null)
        {
            _logger.LogWarning("Pipeline {PipelineId} not found for organization {OrganizationId}", pipelineId, organizationId);
            return Result.Failure<IReadOnlyList<DealStageDto>>(DomainErrors.Pipeline.NotFound);
        }

        var list = await _repository.GetByPipelineIdAsync(pipelineId, ct);
        var dtos = list.Select(Map).ToList();

        _logger.LogInformation("Found {Count} deal stages for pipeline {PipelineId}", dtos.Count, pipelineId);
        return Result.Success<IReadOnlyList<DealStageDto>>(dtos);
    }

    public async Task<Result<DealStageDto>> GetByIdAsync(Guid id, Guid organizationId, CancellationToken ct = default)
    {
        _logger.LogInformation("Getting deal stage {DealStageId} for organization {OrganizationId}", id, organizationId);

        var entity = await _repository.GetByIdAsync(id, organizationId, ct);
        if (entity == null)
        {
            _logger.LogWarning("Deal stage {DealStageId} not found for organization {OrganizationId}", id, organizationId);
            return Result.Failure<DealStageDto>(DomainErrors.DealStage.NotFound);
        }

        return Result.Success(Map(entity));
    }

    public async Task<Result<DealStageDto>> CreateAsync(Guid pipelineId, Guid organizationId, string name, int displayOrder, bool isWon, bool isLost, int? probability = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating deal stage for pipeline {PipelineId} with name {Name}", pipelineId, name);

        var pipeline = await _pipelineRepository.GetByIdAsync(pipelineId, organizationId, ct);
        if (pipeline == null)
        {
            _logger.LogWarning("Pipeline {PipelineId} not found for organization {OrganizationId}", pipelineId, organizationId);
            return Result.Failure<DealStageDto>(DomainErrors.Pipeline.NotFound);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("Deal stage name is required");
            return Result.Failure<DealStageDto>(DomainErrors.DealStage.NameRequired);
        }
        if (probability is < 0 or > 100) return Result.Failure<DealStageDto>(DomainErrors.DealStage.InvalidProbability);

        var entity = new DealStage
        {
            Id = Guid.NewGuid(),
            PipelineId = pipelineId,
            Name = name.Trim(),
            DisplayOrder = displayOrder,
            IsWon = isWon,
            IsLost = isLost,
            Probability = probability,
        };
        entity = await _repository.AddAsync(entity, ct);

        _logger.LogInformation("Deal stage {DealStageId} created for pipeline {PipelineId}", entity.Id, pipelineId);
        return Result.Success(Map(entity));
    }

    public async Task<Result<DealStageDto>> UpdateAsync(Guid id, Guid organizationId, string? name, int? displayOrder, bool? isWon, bool? isLost, int? probability = null, bool clearProbability = false, CancellationToken ct = default)
    {
        _logger.LogInformation("Updating deal stage {DealStageId} for organization {OrganizationId}", id, organizationId);

        var existing = await _repository.GetByIdAsync(id, organizationId, ct);
        if (existing == null)
        {
            _logger.LogWarning("Deal stage {DealStageId} not found for organization {OrganizationId}", id, organizationId);
            return Result.Failure<DealStageDto>(DomainErrors.DealStage.NotFound);
        }

        if (name != null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.LogWarning("Deal stage name cannot be empty");
                return Result.Failure<DealStageDto>(DomainErrors.DealStage.NameRequired);
            }
            existing.Name = name.Trim();
        }

        if (probability is < 0 or > 100) return Result.Failure<DealStageDto>(DomainErrors.DealStage.InvalidProbability);

        var outcomeBefore = DealStageMoves.OutcomeOf(existing);
        if (displayOrder.HasValue) existing.DisplayOrder = displayOrder.Value;
        if (isWon.HasValue) existing.IsWon = isWon.Value;
        if (isLost.HasValue) existing.IsLost = isLost.Value;
        var outcomeAfter = DealStageMoves.OutcomeOf(existing);
        if (outcomeAfter != outcomeBefore)
        {
            // The deals that had the outcome the stage gave take the one it gives now, saved
            // with it, as moving each of them into such a stage would. The board counts an
            // open deal in a won stage as won and the reports read the deal's own outcome,
            // so a stage made the won stage in Settings left every deal in it won on the
            // board and open in the reports. A deal closed the other way on its own page
            // keeps its outcome. No automation runs: a stage relabelled is not a deal won.
            var now = DateTime.UtcNow;
            foreach (var deal in await _repository.GetDealsWithOutcomeAsync(existing.Id, outcomeBefore, ct))
            {
                deal.IsWon = outcomeAfter;
                deal.ClosedAtUtc = outcomeAfter == null ? null : now;
            }
        }
        // Every open deal in the stage without a probability of its own is re-weighted at
        // once: those deals read the stage's, rather than having been given a copy of it.
        if (clearProbability) existing.Probability = null;
        else if (probability.HasValue) existing.Probability = probability.Value;

        existing = await _repository.UpdateAsync(existing, organizationId, ct);
        if (existing == null)
        {
            _logger.LogWarning("Failed to update deal stage {DealStageId}", id);
            return Result.Failure<DealStageDto>(DomainErrors.DealStage.NotFound);
        }

        _logger.LogInformation("Deal stage {DealStageId} updated for organization {OrganizationId}", id, organizationId);
        return Result.Success(Map(existing));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting deal stage {DealStageId} for organization {OrganizationId}", id, organizationId);

        var existing = await _repository.GetByIdAsync(id, organizationId, ct);
        if (existing == null)
        {
            _logger.LogWarning("Deal stage {DealStageId} not found for organization {OrganizationId}", id, organizationId);
            return Result.Failure(DomainErrors.DealStage.NotFound);
        }

        if (await _repository.HasAnyDealsAsync(id, ct))
        {
            _logger.LogWarning("Deal stage {DealStageId} cannot be deleted: deals reference it", id);
            return Result.Failure(DomainErrors.DealStage.HasDeals);
        }

        var deleted = await _repository.DeleteAsync(id, organizationId, ct);
        if (!deleted)
        {
            _logger.LogWarning("Failed to delete deal stage {DealStageId}", id);
            return Result.Failure(DomainErrors.DealStage.NotFound);
        }

        _logger.LogInformation("Deal stage {DealStageId} deleted for organization {OrganizationId}", id, organizationId);
        return Result.Success();
    }

    private static DealStageDto Map(DealStage e) => new DealStageDto(e.Id, e.PipelineId, e.Name, e.DisplayOrder, e.IsWon, e.IsLost, e.Probability);
}
