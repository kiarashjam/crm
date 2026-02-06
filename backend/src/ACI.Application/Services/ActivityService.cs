using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// Service for managing activities in the CRM system.
/// </summary>
public class ActivityService : IActivityService
{
    private readonly IActivityRepository _repository;
    private readonly ILeadRepository _leadRepository;
    private readonly IContactRepository _contactRepository;
    private readonly IDealRepository _dealRepository;
    private readonly ILogger<ActivityService> _logger;

    private static readonly string[] ValidActivityTypes = { "call", "meeting", "email", "note" };

    public ActivityService(
        IActivityRepository repository,
        ILeadRepository leadRepository,
        IContactRepository contactRepository,
        IDealRepository dealRepository,
        ILogger<ActivityService> logger)
    {
        _repository = repository;
        _leadRepository = leadRepository;
        _contactRepository = contactRepository;
        _dealRepository = dealRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PagedResult<ActivityDto>> GetPagedAsync(
        Guid userId,
        Guid? organizationId,
        int page,
        int pageSize,
        string? search = null,
        string? activityType = null,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting paged activities for user {UserId} in organization {OrganizationId}, page {Page}, pageSize {PageSize}, search '{Search}', type '{Type}'", 
            userId, organizationId, page, pageSize, search, activityType);

        var skip = (page - 1) * pageSize;
        var (items, totalCount) = await _repository.GetPagedAsync(userId, organizationId, skip, pageSize, search, activityType, ct);

        _logger.LogDebug("Retrieved {Count} of {Total} activities for user {UserId}", items.Count, totalCount, userId);

        return PagedResult<ActivityDto>.Create(
            items.Select(Map).ToList(),
            totalCount,
            page,
            pageSize);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActivityDto>> GetByUserIdAsync(
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting activities for user {UserId} in organization {OrganizationId}", userId, organizationId);
        
        var list = await _repository.GetByUserIdAsync(userId, organizationId, ct);
        
        _logger.LogDebug("Retrieved {Count} activities for user {UserId}", list.Count, userId);
        
        return list.Select(Map).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActivityDto>> GetByContactIdAsync(
        Guid contactId, 
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting activities for contact {ContactId}", contactId);
        
        var list = await _repository.GetByContactIdAsync(contactId, userId, organizationId, ct);
        
        return list.Select(Map).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActivityDto>> GetByDealIdAsync(
        Guid dealId, 
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting activities for deal {DealId}", dealId);
        
        var list = await _repository.GetByDealIdAsync(dealId, userId, organizationId, ct);
        
        return list.Select(Map).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ActivityDto>> GetByLeadIdAsync(
        Guid leadId, 
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting activities for lead {LeadId}", leadId);
        
        var list = await _repository.GetByLeadIdAsync(leadId, userId, organizationId, ct);
        
        return list.Select(Map).ToList();
    }

    /// <inheritdoc />
    public async Task<Result<ActivityDto>> CreateAsync(
        Guid userId, 
        Guid? organizationId, 
        CreateActivityRequest request, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Creating activity for user {UserId} of type '{Type}'", userId, request.Type);
        
        // Validate activity type
        var activityType = request.Type?.ToLowerInvariant() ?? "note";
        if (!ValidActivityTypes.Contains(activityType))
        {
            _logger.LogWarning("Invalid activity type: {Type}", request.Type);
            return DomainErrors.Activity.InvalidType;
        }
        
        // Validate that at least one related entity is specified
        if (!request.LeadId.HasValue && !request.ContactId.HasValue && !request.DealId.HasValue)
        {
            _logger.LogWarning("Activity creation failed - no related entity specified");
            return DomainErrors.Activity.NoRelatedEntity;
        }

        try
        {
            // Validate that referenced entities exist
            if (request.LeadId.HasValue)
            {
                var lead = await _leadRepository.GetByIdAsync(request.LeadId.Value, userId, organizationId, ct);
                if (lead == null)
                {
                    _logger.LogWarning("Lead {LeadId} not found for activity creation", request.LeadId);
                    return DomainErrors.Activity.RelatedEntityNotFound;
                }
            }

            if (request.ContactId.HasValue)
            {
                var contact = await _contactRepository.GetByIdAsync(request.ContactId.Value, userId, organizationId, ct);
                if (contact == null)
                {
                    _logger.LogWarning("Contact {ContactId} not found for activity creation", request.ContactId);
                    return DomainErrors.Activity.RelatedEntityNotFound;
                }
            }

            if (request.DealId.HasValue)
            {
                var deal = await _dealRepository.GetByIdAsync(request.DealId.Value, userId, organizationId, ct);
                if (deal == null)
                {
                    _logger.LogWarning("Deal {DealId} not found for activity creation", request.DealId);
                    return DomainErrors.Activity.RelatedEntityNotFound;
                }
            }

            var entity = new Activity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrganizationId = organizationId,
                Type = activityType,
                Subject = request.Subject,
                Body = request.Body,
                ContactId = request.ContactId,
                DealId = request.DealId,
                LeadId = request.LeadId,
                Participants = request.Participants,
                CreatedAtUtc = DateTime.UtcNow,
            };
            
            entity = await _repository.AddAsync(entity, ct);
            
            _logger.LogInformation("Successfully created activity {ActivityId} of type '{Type}'", entity.Id, entity.Type);
            return Map(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating activity for user {UserId}", userId);
            return DomainErrors.General.ServerError;
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(
        Guid id, 
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting activity {ActivityId} for user {UserId}", id, userId);
        
        try
        {
            var deleted = await _repository.DeleteAsync(id, userId, organizationId, ct);
            
            if (!deleted)
            {
                _logger.LogWarning("Activity {ActivityId} not found for deletion", id);
                return DomainErrors.Activity.NotFound;
            }
            
            _logger.LogInformation("Successfully deleted activity {ActivityId}", id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting activity {ActivityId}", id);
            return DomainErrors.General.ServerError;
        }
    }

    private static ActivityDto Map(Activity e) =>
        new ActivityDto(
            e.Id, 
            e.Type, 
            e.Subject, 
            e.Body, 
            e.ContactId, 
            e.DealId, 
            e.LeadId, 
            e.Participants, 
            e.CreatedAtUtc
        );
}
