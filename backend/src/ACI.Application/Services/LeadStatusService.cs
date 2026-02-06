using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

public class LeadStatusService : ILeadStatusService
{
    private readonly ILeadStatusRepository _repository;
    private readonly ILogger<LeadStatusService> _logger;

    public LeadStatusService(ILeadStatusRepository repository, ILogger<LeadStatusService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<LeadStatusDto>>> GetByOrganizationIdAsync(Guid organizationId, CancellationToken ct = default)
    {
        _logger.LogInformation("Getting lead statuses for organization {OrganizationId}", organizationId);

        var list = await _repository.GetByOrganizationIdAsync(organizationId, ct);
        var dtos = list.Select(Map).ToList();

        _logger.LogInformation("Found {Count} lead statuses for organization {OrganizationId}", dtos.Count, organizationId);
        return Result.Success<IReadOnlyList<LeadStatusDto>>(dtos);
    }

    public async Task<Result<LeadStatusDto>> GetByIdAsync(Guid id, Guid organizationId, CancellationToken ct = default)
    {
        _logger.LogInformation("Getting lead status {LeadStatusId} for organization {OrganizationId}", id, organizationId);

        var entity = await _repository.GetByIdAsync(id, organizationId, ct);
        if (entity == null)
        {
            _logger.LogWarning("Lead status {LeadStatusId} not found for organization {OrganizationId}", id, organizationId);
            return Result.Failure<LeadStatusDto>(DomainErrors.LeadStatus.NotFound);
        }

        return Result.Success(Map(entity));
    }

    public async Task<Result<LeadStatusDto>> CreateAsync(Guid organizationId, string name, int displayOrder, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating lead status for organization {OrganizationId} with name {Name}", organizationId, name);

        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogWarning("Lead status name is required");
            return Result.Failure<LeadStatusDto>(DomainErrors.LeadStatus.NameRequired);
        }

        var entity = new LeadStatus
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Name = name.Trim(),
            DisplayOrder = displayOrder,
        };
        entity = await _repository.AddAsync(entity, ct);

        _logger.LogInformation("Lead status {LeadStatusId} created for organization {OrganizationId}", entity.Id, organizationId);
        return Result.Success(Map(entity));
    }

    public async Task<Result<LeadStatusDto>> UpdateAsync(Guid id, Guid organizationId, string? name, int? displayOrder, CancellationToken ct = default)
    {
        _logger.LogInformation("Updating lead status {LeadStatusId} for organization {OrganizationId}", id, organizationId);

        var existing = await _repository.GetByIdAsync(id, organizationId, ct);
        if (existing == null)
        {
            _logger.LogWarning("Lead status {LeadStatusId} not found for organization {OrganizationId}", id, organizationId);
            return Result.Failure<LeadStatusDto>(DomainErrors.LeadStatus.NotFound);
        }

        if (name != null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.LogWarning("Lead status name cannot be empty");
                return Result.Failure<LeadStatusDto>(DomainErrors.LeadStatus.NameRequired);
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
            _logger.LogWarning("Failed to update lead status {LeadStatusId}", id);
            return Result.Failure<LeadStatusDto>(DomainErrors.LeadStatus.NotFound);
        }

        _logger.LogInformation("Lead status {LeadStatusId} updated for organization {OrganizationId}", id, organizationId);
        return Result.Success(Map(existing));
    }

    public async Task<Result> DeleteAsync(Guid id, Guid organizationId, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting lead status {LeadStatusId} for organization {OrganizationId}", id, organizationId);

        var existing = await _repository.GetByIdAsync(id, organizationId, ct);
        if (existing == null)
        {
            _logger.LogWarning("Lead status {LeadStatusId} not found for organization {OrganizationId}", id, organizationId);
            return Result.Failure(DomainErrors.LeadStatus.NotFound);
        }

        var deleted = await _repository.DeleteAsync(id, organizationId, ct);
        if (!deleted)
        {
            _logger.LogWarning("Failed to delete lead status {LeadStatusId}", id);
            return Result.Failure(DomainErrors.LeadStatus.NotFound);
        }

        _logger.LogInformation("Lead status {LeadStatusId} deleted for organization {OrganizationId}", id, organizationId);
        return Result.Success();
    }

    private static LeadStatusDto Map(LeadStatus e) => new LeadStatusDto(e.Id, e.OrganizationId, e.Name, e.DisplayOrder);
}
