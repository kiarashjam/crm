using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

public class LeadSourceService : ILeadSourceService
{
    private readonly ILeadSourceRepository _repository;
    private readonly ILogger<LeadSourceService> _logger;

    public LeadSourceService(ILeadSourceRepository repository, ILogger<LeadSourceService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<LeadSourceDto>>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
    {
        _logger.LogInformation("Getting lead sources for organization {OrganizationId}", organizationId);

        var list = await _repository.GetByOrganizationIdAsync(organizationId, ct);
        var dtos = list.Select(Map).ToList();

        _logger.LogInformation("Found {Count} lead sources for organization {OrganizationId}", dtos.Count, organizationId);
        return Result.Success<IReadOnlyList<LeadSourceDto>>(dtos);
    }

    public async Task<Result<LeadSourceDto>> GetByIdAsync(Guid id, Guid organizationId, CancellationToken ct = default)
    {
        _logger.LogInformation("Getting lead source {LeadSourceId} for organization {OrganizationId}", id, organizationId);

        var entity = await _repository.GetByIdAsync(id, organizationId, ct);
        if (entity == null)
        {
            _logger.LogWarning("Lead source {LeadSourceId} not found for organization {OrganizationId}", id, organizationId);
            return Result.Failure<LeadSourceDto>(DomainErrors.LeadSource.NotFound);
        }

        return Result.Success(Map(entity));
    }

    public async Task<Result<LeadSourceDto>> CreateAsync(Guid organizationId, string name, int displayOrder, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating lead source for organization {OrganizationId} with name {Name}", organizationId, name);

        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("Lead source name is required");
            return Result.Failure<LeadSourceDto>(DomainErrors.LeadSource.NameRequired);
        }

        var entity = new LeadSource
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name.Trim(),
            DisplayOrder = displayOrder,
        };
        entity = await _repository.AddAsync(entity, ct);

        _logger.LogInformation("Lead source {LeadSourceId} created for organization {OrganizationId}", entity.Id, organizationId);
        return Result.Success(Map(entity));
    }

    public async Task<Result<LeadSourceDto>> UpdateAsync(Guid id, Guid organizationId, string? name, int? displayOrder, CancellationToken ct = default)
    {
        _logger.LogInformation("Updating lead source {LeadSourceId} for organization {OrganizationId}", id, organizationId);

        var existing = await _repository.GetByIdAsync(id, organizationId, ct);
        if (existing == null)
        {
            _logger.LogWarning("Lead source {LeadSourceId} not found for organization {OrganizationId}", id, organizationId);
            return Result.Failure<LeadSourceDto>(DomainErrors.LeadSource.NotFound);
        }

        if (name != null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.LogWarning("Lead source name cannot be empty");
                return Result.Failure<LeadSourceDto>(DomainErrors.LeadSource.NameRequired);
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
            _logger.LogWarning("Failed to update lead source {LeadSourceId}", id);
            return Result.Failure<LeadSourceDto>(DomainErrors.LeadSource.NotFound);
        }

        _logger.LogInformation("Lead source {LeadSourceId} updated for organization {OrganizationId}", id, organizationId);
        return Result.Success(Map(existing));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting lead source {LeadSourceId} for organization {OrganizationId}", id, organizationId);

        var existing = await _repository.GetByIdAsync(id, organizationId, ct);
        if (existing == null)
        {
            _logger.LogWarning("Lead source {LeadSourceId} not found for organization {OrganizationId}", id, organizationId);
            return Result.Failure(DomainErrors.LeadSource.NotFound);
        }

        var deleted = await _repository.DeleteAsync(id, organizationId, ct);
        if (!deleted)
        {
            _logger.LogWarning("Failed to delete lead source {LeadSourceId}", id);
            return Result.Failure(DomainErrors.LeadSource.NotFound);
        }

        _logger.LogInformation("Lead source {LeadSourceId} deleted for organization {OrganizationId}", id, organizationId);
        return Result.Success();
    }

    private static LeadSourceDto Map(LeadSource e) => new LeadSourceDto(e.Id, e.OrganizationId, e.Name, e.DisplayOrder);
}
