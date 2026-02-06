using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

public class PipelineService : IPipelineService
{
    private readonly IPipelineRepository _repository;
    private readonly ILogger<PipelineService> _logger;

    public PipelineService(IPipelineRepository repository, ILogger<PipelineService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<PipelineDto>>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
    {
        _logger.LogInformation("Getting pipelines for organization {OrganizationId}", organizationId);

        var list = await _repository.GetByOrganizationIdAsync(organizationId, ct);
        var dtos = list.Select(Map).ToList();

        _logger.LogInformation("Found {Count} pipelines for organization {OrganizationId}", dtos.Count, organizationId);
        return Result.Success<IReadOnlyList<PipelineDto>>(dtos);
    }

    public async Task<Result<PipelineDto>> GetByIdAsync(Guid id, Guid organizationId, CancellationToken ct = default)
    {
        _logger.LogInformation("Getting pipeline {PipelineId} for organization {OrganizationId}", id, organizationId);

        var entity = await _repository.GetByIdAsync(id, organizationId, ct);
        if (entity == null)
        {
            _logger.LogWarning("Pipeline {PipelineId} not found for organization {OrganizationId}", id, organizationId);
            return Result.Failure<PipelineDto>(DomainErrors.Pipeline.NotFound);
        }

        return Result.Success(Map(entity));
    }

    public async Task<Result<PipelineDto>> CreateAsync(Guid organizationId, string name, int displayOrder, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating pipeline for organization {OrganizationId} with name {Name}", organizationId, name);

        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("Pipeline name is required");
            return Result.Failure<PipelineDto>(DomainErrors.Pipeline.NameRequired);
        }

        var entity = new Pipeline
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name.Trim(),
            DisplayOrder = displayOrder,
        };
        entity = await _repository.AddAsync(entity, ct);

        _logger.LogInformation("Pipeline {PipelineId} created for organization {OrganizationId}", entity.Id, organizationId);
        return Result.Success(Map(entity));
    }

    public async Task<Result<PipelineDto>> UpdateAsync(Guid id, Guid organizationId, string? name, int? displayOrder, CancellationToken ct = default)
    {
        _logger.LogInformation("Updating pipeline {PipelineId} for organization {OrganizationId}", id, organizationId);

        var existing = await _repository.GetByIdAsync(id, organizationId, ct);
        if (existing == null)
        {
            _logger.LogWarning("Pipeline {PipelineId} not found for organization {OrganizationId}", id, organizationId);
            return Result.Failure<PipelineDto>(DomainErrors.Pipeline.NotFound);
        }

        if (name != null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.LogWarning("Pipeline name cannot be empty");
                return Result.Failure<PipelineDto>(DomainErrors.Pipeline.NameRequired);
            }
            existing.Name = name.Trim();
        }

        if (displayOrder.HasValue)
        {
            existing.DisplayOrder = displayOrder.Value;
        }

        existing = await _repository.UpdateAsync(existing, ct);
        if (existing == null)
        {
            _logger.LogWarning("Failed to update pipeline {PipelineId}", id);
            return Result.Failure<PipelineDto>(DomainErrors.Pipeline.NotFound);
        }

        _logger.LogInformation("Pipeline {PipelineId} updated for organization {OrganizationId}", id, organizationId);
        return Result.Success(Map(existing));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting pipeline {PipelineId} for organization {OrganizationId}", id, organizationId);

        var existing = await _repository.GetByIdAsync(id, organizationId, ct);
        if (existing == null)
        {
            _logger.LogWarning("Pipeline {PipelineId} not found for organization {OrganizationId}", id, organizationId);
            return Result.Failure(DomainErrors.Pipeline.NotFound);
        }

        var deleted = await _repository.DeleteAsync(id, organizationId, ct);
        if (!deleted)
        {
            _logger.LogWarning("Failed to delete pipeline {PipelineId}", id);
            return Result.Failure(DomainErrors.Pipeline.NotFound);
        }

        _logger.LogInformation("Pipeline {PipelineId} deleted for organization {OrganizationId}", id, organizationId);
        return Result.Success();
    }

    private static PipelineDto Map(Pipeline e) => new PipelineDto(e.Id, e.OrganizationId, e.Name, e.DisplayOrder);
}
