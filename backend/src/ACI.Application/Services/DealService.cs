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
    private readonly IAutomationEngine? _automations;
    private readonly IOutboundWebhookPublisher? _webhooks;
    private readonly IOrganizationRepository? _organizations;
    private readonly ILogger<DealService> _logger;

    public DealService(
        IDealRepository repository, 
        IActivityRepository activityRepository,
        ILogger<DealService> logger,
        IAutomationEngine? automations = null,
        IOutboundWebhookPublisher? webhooks = null,
        IOrganizationRepository? organizations = null)
    {
        _repository = repository;
        _activityRepository = activityRepository;
        _logger = logger;
        _automations = automations;
        _webhooks = webhooks;
        _organizations = organizations;
    }

    /// <inheritdoc />
    public async Task<PagedResult<DealDto>> GetDealsPagedAsync(
        Guid userId, 
        Guid? organizationId, 
        int page = 1, 
        int pageSize = 20, 
        string? search = null,
        Guid? companyId = null,
        Guid? contactId = null,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting paged deals for user {UserId}, organization {OrganizationId}, page {Page}, pageSize {PageSize}, search '{Search}', companyId {CompanyId}, contactId {ContactId}",
            userId, organizationId, page, pageSize, search, companyId, contactId);

        var skip = (page - 1) * pageSize;
        var (items, totalCount) = await _repository.GetPagedAsync(userId, organizationId, skip, pageSize, search, companyId, contactId, ct);
        
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
        CancellationToken ct = default,
        int? take = null)
    {
        _logger.LogDebug(
            "Searching deals for user {UserId} with query '{Query}'",
            userId, query);

        var trimmed = query.Trim();
        var list = take is > 0
            ? await _repository.SearchAsync(userId, organizationId, trimmed, ct, take)
            : await _repository.SearchAsync(userId, organizationId, trimmed, ct);
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
            Description = request.Description?.Trim(),
            Probability = request.Probability,
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

        // Track stage change before applying update (HP-11)
        var oldStageId = existing.DealStageId;
        var oldStageName = existing.DealStage?.Name ?? existing.Stage;
        var previousWon = existing.IsWon;

        if (request.IsPrivate is bool makePrivate && makePrivate != existing.IsPrivate
            && !await MayChangePrivacyAsync(existing, userId, organizationId, ct))
            return DomainErrors.Deal.PrivacyNotOwner;

        // Apply partial updates
        if (request.Name != null) existing.Name = request.Name.Trim();
        if (request.Value != null) existing.Value = request.Value.Trim();
        if (request.Currency != null) existing.Currency = request.Currency.Trim();
        if (request.Stage != null) existing.Stage = request.Stage.Trim();
        if (request.PipelineId != null) existing.PipelineId = request.PipelineId;
        if (request.DealStageId != null) existing.DealStageId = request.DealStageId;
        if (request.CompanyId.HasValue) existing.CompanyId = request.CompanyId;
        if (request.AssigneeId.HasValue) existing.AssigneeId = request.AssigneeId;
        if (request.ExpectedCloseDateUtc != null) existing.ExpectedCloseDateUtc = request.ExpectedCloseDateUtc;
        if (request.Description != null) existing.Description = request.Description.Trim();
        if (request.Probability.HasValue) existing.Probability = request.Probability;
        if (request.ContactId.HasValue) existing.ContactId = request.ContactId;

        // Handle close with reason (HP-8)
        if (request.IsWon != null)
        {
            existing.IsWon = request.IsWon;
            existing.ClosedAtUtc = DateTime.UtcNow;
        }
        if (request.ClosedReason != null) existing.ClosedReason = request.ClosedReason.Trim();
        if (request.ClosedReasonCategory != null) existing.ClosedReasonCategory = request.ClosedReasonCategory.Trim();
        if (request.IsPrivate.HasValue) existing.IsPrivate = request.IsPrivate.Value;
        if (request.TeamId.HasValue) existing.TeamId = request.TeamId;

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

        // HP-11: Record stage change if the stage was actually changed
        if (request.DealStageId != null && oldStageId != existing.DealStageId)
        {
            var stageChange = new DealStageChange
            {
                Id = Guid.NewGuid(),
                DealId = id,
                FromDealStageId = oldStageId,
                FromStageName = oldStageName,
                ToDealStageId = existing.DealStageId,
                ToStageName = existing.DealStage?.Name ?? existing.Stage,
                ChangedByUserId = userId,
                ChangedAtUtc = DateTime.UtcNow,
            };
            await _repository.AddStageChangeAsync(stageChange, ct);
            _logger.LogInformation(
                "Stage change recorded for deal {DealId}: {From} → {To}",
                id, oldStageName, stageChange.ToStageName);
            if (_automations != null)
            {
                await _automations.DispatchAsync("deal.stage_changed", new AutomationEvent(
                    organizationId, userId, $"{id}:{existing.DealStageId}", existing.Name, DealId: id, AssigneeId: existing.AssigneeId), ct);
            }
            if (_webhooks != null)
                await _webhooks.PublishAsync(organizationId, "deal.stage_changed", new { id, existing.Name, stageId = existing.DealStageId }, ct);
        }

        if (_automations != null && request.IsWon == true && previousWon != true)
            await _automations.DispatchAsync("deal.won", new AutomationEvent(organizationId, userId, $"{id}:won", existing.Name, DealId: id, AssigneeId: existing.AssigneeId), ct);
        if (_webhooks != null && request.IsWon == true && previousWon != true)
            await _webhooks.PublishAsync(organizationId, "deal.won", new { id, existing.Name }, ct);
        if (_automations != null && request.IsWon == false && previousWon != false)
            await _automations.DispatchAsync("deal.lost", new AutomationEvent(organizationId, userId, $"{id}:lost", existing.Name, DealId: id, AssigneeId: existing.AssigneeId), ct);

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

    /// <inheritdoc />
    public async Task<Result> MergeAsync(Guid keepId, Guid dropId, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        if (keepId == dropId) return DomainErrors.Deal.NotFound;
        var keep = await _repository.GetByIdAsync(keepId, userId, organizationId, ct);
        var drop = await _repository.GetByIdAsync(dropId, userId, organizationId, ct);
        if (keep == null || drop == null) return DomainErrors.Deal.NotFound;
        // A private duplicate makes the kept deal private, which hides it from the team:
        // the same decision as making it private by hand, so the same people may make it.
        if (drop.IsPrivate && !keep.IsPrivate && !await MayChangePrivacyAsync(keep, userId, organizationId, ct))
            return DomainErrors.Deal.MergeWouldHideNotOwner;
        var merged = await _repository.MergeIntoAsync(keepId, dropId, userId, organizationId, ct);
        if (!merged) return DomainErrors.Deal.NotFound;
        _logger.LogInformation("Merged deal {DropId} into {KeepId}", dropId, keepId);
        return Result.Success();
    }

    /// <summary>
    /// Making a deal private hides it from the rest of the team, so it is for the deal's
    /// creator, its owner, or a manager to decide, not for anyone who can open it.
    /// </summary>
    private async Task<bool> MayChangePrivacyAsync(Deal deal, Guid userId, Guid? organizationId, CancellationToken ct)
    {
        if (organizationId is not Guid org) return true;
        if (deal.UserId == userId || deal.AssigneeId == userId) return true;
        if (_organizations == null) return false;
        var role = await _organizations.GetMemberRoleAsync(userId, org, ct);
        return role is Domain.Enums.OrgMemberRole.Owner or Domain.Enums.OrgMemberRole.Manager;
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
            lastActivityAtUtc,
            // New fields — HP-1
            e.Description,
            e.Probability,
            // Enriched names — HP-4
            e.Assignee?.Name,
            e.Company?.Name,
            e.Contact?.Name,
            e.Pipeline?.Name,
            e.DealStage?.Name,
            e.CreatedAtUtc,
            e.UpdatedAtUtc,
            // Close reason — HP-8
            e.ClosedReason,
            e.ClosedReasonCategory,
            e.ClosedAtUtc,
            e.IsPrivate,
            e.TeamId);
}
