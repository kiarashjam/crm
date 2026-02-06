using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// Service for managing deals in the CRM system.
/// </summary>
public class DealService : IDealService
{
    private readonly IDealRepository _repository;
    private readonly IActivityRepository _activityRepository;
    private readonly ILogger<DealService> _logger;

    public DealService(
        IDealRepository repository, 
        IActivityRepository activityRepository,
        ILogger<DealService> logger)
    {
        _repository = repository;
        _activityRepository = activityRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PagedResult<DealDto>> GetDealsPagedAsync(
        Guid userId, 
        Guid? organizationId, 
        int page = 1, 
        int pageSize = 20, 
        string? search = null,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting paged deals for user {UserId}, organization {OrganizationId}, page {Page}, pageSize {PageSize}, search '{Search}'",
            userId, organizationId, page, pageSize, search);

        var skip = (page - 1) * pageSize;
        var (items, totalCount) = await _repository.GetPagedAsync(userId, organizationId, skip, pageSize, search, ct);
        
        var lastByDeal = await _activityRepository.GetLastActivityByDealIdsAsync(
            userId, organizationId, items.Select(d => d.Id), ct);
        
        var dtos = items.Select(d => Map(d, lastByDeal.TryGetValue(d.Id, out var last) ? last : null)).ToList();
        
        _logger.LogInformation(
            "Retrieved {Count} of {Total} deals for user {UserId} (page {Page})",
            items.Count, totalCount, userId, page);

        return PagedResult<DealDto>.Create(dtos, totalCount, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DealDto>> GetDealsAsync(
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting deals for user {UserId}, organization {OrganizationId}",
            userId, organizationId);

        var list = await _repository.GetByUserIdAsync(userId, organizationId, ct);
        var lastByDeal = await _activityRepository.GetLastActivityByDealIdsAsync(
            userId, organizationId, list.Select(d => d.Id), ct);
        
        _logger.LogInformation(
            "Retrieved {Count} deals for user {UserId}",
            list.Count, userId);

        return list.Select(d => Map(d, lastByDeal.TryGetValue(d.Id, out var last) ? last : null)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DealDto>> SearchAsync(
        Guid userId, 
        Guid? organizationId, 
        string query, 
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Searching deals for user {UserId} with query '{Query}'",
            userId, query);

        var list = await _repository.SearchAsync(userId, organizationId, query.Trim(), ct);
        var lastByDeal = await _activityRepository.GetLastActivityByDealIdsAsync(
            userId, organizationId, list.Select(d => d.Id), ct);

        _logger.LogInformation(
            "Search returned {Count} deals for query '{Query}'",
            list.Count, query);

        return list.Select(d => Map(d, lastByDeal.TryGetValue(d.Id, out var last) ? last : null)).ToList();
    }

    /// <inheritdoc />
    public async Task<Result<DealDto>> GetByIdAsync(
        Guid id, 
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting deal {DealId} for user {UserId}",
            id, userId);

        var entity = await _repository.GetByIdAsync(id, userId, organizationId, ct);
        
        if (entity == null)
        {
            _logger.LogWarning(
                "Deal {DealId} not found for user {UserId}",
                id, userId);
            return DomainErrors.Deal.NotFound;
        }

        var lastByDeal = await _activityRepository.GetLastActivityByDealIdsAsync(
            userId, organizationId, new[] { id }, ct);
        
        return Map(entity, lastByDeal.TryGetValue(id, out var last) ? last : null);
    }

    /// <inheritdoc />
    public async Task<Result<DealDto>> CreateAsync(
        Guid userId, 
        Guid? organizationId, 
        CreateDealRequest request, 
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Creating deal '{Name}' for user {UserId}",
            request.Name, userId);

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            _logger.LogWarning("Deal creation failed: Name is required");
            return DomainErrors.Deal.NameRequired;
        }

        if (string.IsNullOrWhiteSpace(request.Value))
        {
            _logger.LogWarning("Deal creation failed: Value is required");
            return DomainErrors.Deal.ValueRequired;
        }

        var entity = new Deal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            Name = request.Name.Trim(),
            Value = request.Value.Trim(),
            Currency = request.Currency?.Trim(),
            Stage = request.Stage?.Trim(),
            PipelineId = request.PipelineId,
            DealStageId = request.DealStageId,
            CompanyId = request.CompanyId,
            ContactId = request.ContactId,
            AssigneeId = request.AssigneeId,
            ExpectedCloseDateUtc = request.ExpectedCloseDateUtc,
            CreatedAtUtc = DateTime.UtcNow,
        };

        entity = await _repository.AddAsync(entity, ct);

        _logger.LogInformation(
            "Deal {DealId} created successfully for user {UserId}",
            entity.Id, userId);

        return Map(entity, null);
    }

    /// <inheritdoc />
    public async Task<Result<DealDto>> UpdateAsync(
        Guid id, 
        Guid userId, 
        Guid? organizationId, 
        UpdateDealRequest request, 
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Updating deal {DealId} for user {UserId}",
            id, userId);

        var existing = await _repository.GetByIdAsync(id, userId, organizationId, ct);
        
        if (existing == null)
        {
            _logger.LogWarning(
                "Deal update failed: Deal {DealId} not found",
                id);
            return DomainErrors.Deal.NotFound;
        }

        // Apply partial updates
        if (request.Name != null) existing.Name = request.Name.Trim();
        if (request.Value != null) existing.Value = request.Value.Trim();
        if (request.Currency != null) existing.Currency = request.Currency.Trim();
        if (request.Stage != null) existing.Stage = request.Stage.Trim();
        if (request.PipelineId != null) existing.PipelineId = request.PipelineId;
        if (request.DealStageId != null) existing.DealStageId = request.DealStageId;
        if (request.CompanyId != null) existing.CompanyId = request.CompanyId;
        if (request.ContactId.HasValue) existing.ContactId = request.ContactId;
        if (request.AssigneeId.HasValue) existing.AssigneeId = request.AssigneeId;
        if (request.ExpectedCloseDateUtc != null) existing.ExpectedCloseDateUtc = request.ExpectedCloseDateUtc;
        if (request.IsWon != null) existing.IsWon = request.IsWon;

        existing.UpdatedAtUtc = DateTime.UtcNow;
        existing.UpdatedByUserId = userId;

        var updated = await _repository.UpdateAsync(existing, userId, organizationId, ct);
        
        if (updated == null)
        {
            _logger.LogError(
                "Deal update failed unexpectedly for deal {DealId}",
                id);
            return DomainErrors.General.ServerError;
        }

        _logger.LogInformation(
            "Deal {DealId} updated successfully",
            id);

        return Map(updated, null);
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(
        Guid id, 
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Deleting deal {DealId} for user {UserId}",
            id, userId);

        var deleted = await _repository.DeleteAsync(id, userId, organizationId, ct);
        
        if (!deleted)
        {
            _logger.LogWarning(
                "Deal deletion failed: Deal {DealId} not found",
                id);
            return DomainErrors.Deal.NotFound;
        }

        _logger.LogInformation(
            "Deal {DealId} deleted successfully",
            id);

        return Result.Success();
    }

    private static DealDto Map(Deal e, DateTime? lastActivityAtUtc) =>
        new DealDto(
            e.Id, 
            e.Name, 
            e.Value, 
            e.Currency, 
            e.Stage, 
            e.PipelineId, 
            e.DealStageId, 
            e.CompanyId, 
            e.ContactId, 
            e.AssigneeId, 
            e.ExpectedCloseDateUtc, 
            e.IsWon, 
            lastActivityAtUtc);
}
