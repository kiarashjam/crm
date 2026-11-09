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
    private readonly ILeadAutomation _automation;
    private readonly ISequenceRepository _sequences;
    private readonly ICollaborationNotifier _notifier;
    private readonly ILogger<ActivityService> _logger;

    private static readonly HashSet<string> ReplyTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "call", "meeting", "email", "video", "demo",
    };

    // HP-1: The 9 types a person logs (matches frontend config), plus "system": the
    // updates the lead page records on its timeline. "system" is not a reply, so it
    // never counts as a first response or stops a sequence.
    private static readonly string[] ValidActivityTypes = { "call", "meeting", "email", "note", "task", "follow_up", "deadline", "video", "demo", "system" };

    public ActivityService(
        IActivityRepository repository,
        ILeadRepository leadRepository,
        IContactRepository contactRepository,
        IDealRepository dealRepository,
        ILeadAutomation automation,
        ISequenceRepository sequences,
        ICollaborationNotifier notifier,
        ILogger<ActivityService> logger)
    {
        _repository = repository;
        _leadRepository = leadRepository;
        _contactRepository = contactRepository;
        _dealRepository = dealRepository;
        _automation = automation;
        _sequences = sequences;
        _notifier = notifier;
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
    public async Task<IReadOnlyList<ActivityDto>> GetByLeadIdsAsync(
        IReadOnlyCollection<Guid> leadIds,
        Guid userId,
        Guid? organizationId,
        CancellationToken ct = default)
    {
        if (leadIds.Count == 0) return Array.Empty<ActivityDto>();
        var list = await _repository.GetByLeadIdsAsync(leadIds, userId, organizationId, ct);
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
        
        var now = DateTime.UtcNow;
        var occurredAt = request.OccurredAtUtc is DateTime at ? ActivityFacts.Utc(at) : now;
        var outcome = ActivityFacts.Word(request.Outcome);
        var direction = ActivityFacts.Word(request.Direction);
        var facts = ActivityFacts.Check(activityType, occurredAt, outcome, request.DurationMinutes, direction, now);
        if (facts.IsFailure) return facts.Error;

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
                // Do not contact is not checked here. An activity is a record of what
                // happened, and nothing is sent from here: the sends themselves
                // (sequences, automations, the composer) refuse a do-not-contact person.
                // Refusing the record as well meant staff could not write down the
                // note "asked to be removed", or log the call when that person rang in.
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
                OccurredAtUtc = occurredAt,
                Outcome = outcome,
                DurationMinutes = request.DurationMinutes,
                Direction = direction,
                CreatedAtUtc = now,
            };
            
            entity = await _repository.AddAsync(entity, ct);
            // An email they sent us, or a call of theirs nobody answered, is not the team
            // answering them: the first-response clock keeps running.
            if (entity.LeadId.HasValue && !request.IsAutomated && ActivityFacts.IsOurResponse(entity.Type, entity.Direction, entity.Outcome))
                await _automation.MarkFirstResponseAsync(entity.LeadId.Value, userId, organizationId, entity.Type, entity.OccurredAtUtc, ct);
            if (!request.IsAutomated && ReplyTypes.Contains(entity.Type))
                await StopSequencesAfterReplyAsync(organizationId, entity, ct);

            _logger.LogInformation("Successfully created activity {ActivityId} of type '{Type}'", entity.Id, entity.Type);
            // Re-fetch so the DTO can include the related entity and creator-user names.
            var fetched = await _repository.GetByIdAsync(entity.Id, userId, organizationId, ct);
            // "@Anna, can you call them?" in a note tells Anna. A sequence's own lines mention nobody.
            if (!request.IsAutomated)
                await _notifier.ActivityMentionsAsync(fetched ?? entity, RecordName(fetched), null, userId, ct);
            return Map(fetched ?? entity);
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

    /// <inheritdoc />
    public async Task<Result<ActivityDto>> UpdateAsync(
        Guid id,
        Guid userId,
        Guid? organizationId,
        UpdateActivityRequest request,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Updating activity {ActivityId} for user {UserId}", id, userId);

        var existing = await _repository.GetByIdAsync(id, userId, organizationId, ct);
        if (existing == null)
        {
            _logger.LogWarning("Activity {ActivityId} not found for update", id);
            return DomainErrors.Activity.NotFound;
        }

        // What it said before the edit: only someone the edit newly mentions is told.
        var textBefore = Mentions.TextOf(existing.Subject, existing.Body);
        var recordName = RecordName(existing);

        try
        {
            var activityType = (request.Type ?? existing.Type).ToLowerInvariant();
            if (request.Type != null && !ValidActivityTypes.Contains(activityType))
                return DomainErrors.Activity.InvalidType;

            var occurredAt = request.OccurredAtUtc is DateTime at ? ActivityFacts.Utc(at) : existing.OccurredAtUtc;
            var outcome = request.ClearOutcome == true ? null : ActivityFacts.Word(request.Outcome) ?? existing.Outcome;
            var duration = request.ClearDuration == true ? null : request.DurationMinutes ?? existing.DurationMinutes;
            var direction = request.ClearDirection == true ? null : ActivityFacts.Word(request.Direction) ?? existing.Direction;
            // A call changed into a note keeps no outcome, duration or direction: the note
            // has none of them. Only what this edit did not send is dropped this way; a
            // value sent with the new kind that it cannot carry is refused below.
            if (!string.Equals(activityType, existing.Type, StringComparison.OrdinalIgnoreCase))
            {
                if (ActivityFacts.Word(request.Outcome) == null && outcome != null && !ActivityFacts.OutcomesFor(activityType).Contains(outcome)) outcome = null;
                if (request.DurationMinutes == null && !ActivityFacts.HasDuration(activityType)) duration = null;
                if (ActivityFacts.Word(request.Direction) == null && !ActivityFacts.HasDirection(activityType)) direction = null;
            }
            // The time is checked when the edit changes it; one already saved stands.
            var facts = request.OccurredAtUtc != null
                ? ActivityFacts.Check(activityType, occurredAt, outcome, duration, direction, DateTime.UtcNow)
                : ActivityFacts.CheckFacts(activityType, outcome, duration, direction);
            if (facts.IsFailure) return facts.Error;

            existing.Type = activityType;
            existing.OccurredAtUtc = occurredAt;
            existing.Outcome = outcome;
            existing.DurationMinutes = duration;
            existing.Direction = direction;
            if (request.Subject != null) existing.Subject = request.Subject;
            if (request.Body != null) existing.Body = request.Body;
            if (request.Participants != null) existing.Participants = request.Participants;
            existing.UpdatedAtUtc = DateTime.UtcNow;
            existing.UpdatedByUserId = userId;

            existing = await _repository.UpdateAsync(existing, userId, organizationId, ct);
            if (existing == null)
            {
                _logger.LogWarning("Activity {ActivityId} update returned null", id);
                return DomainErrors.General.ServerError;
            }

            await _notifier.ActivityMentionsAsync(existing, recordName, textBefore, userId, ct);

            _logger.LogInformation("Successfully updated activity {ActivityId}", id);
            return Map(existing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating activity {ActivityId}", id);
            return DomainErrors.General.ServerError;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> GetOrgMemberActivityCountsAsync(
        Guid userId,
        Guid? organizationId,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting activity counts per org member for organization {OrganizationId}", organizationId);
        return await _repository.GetActivityCountsByOrgAsync(userId, organizationId, ct);
    }

    /// <summary>
    /// A person logged a call, meeting, or email. Scheduled outreach to that person stops.
    /// A sequence's own send is marked automated, so it does not stop itself.
    /// </summary>
    private async Task StopSequencesAfterReplyAsync(Guid? organizationId, Activity entity, CancellationToken ct)
    {
        try
        {
            if (entity.LeadId is Guid leadId)
                await _sequences.StopOpenForTargetAsync(organizationId, "lead", leadId, ct);
            if (entity.ContactId is Guid contactId)
                await _sequences.StopOpenForTargetAsync(organizationId, "contact", contactId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Activity {ActivityId} was saved, but its sequence could not be stopped", entity.Id);
        }
    }

    /// <summary>The lead, deal or contact the activity is on, by name, when it was read with them.</summary>
    private static string? RecordName(Activity? activity) =>
        activity?.Lead?.Name ?? activity?.Deal?.Name ?? activity?.Contact?.Name;

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
            e.CreatedAtUtc,
            e.UpdatedAtUtc,
            e.Contact?.Name,
            e.Deal?.Name,
            e.Lead?.Name,
            e.UserId,
            e.User?.Name,
            e.User?.Email,
            e.OccurredAtUtc,
            e.Outcome,
            e.DurationMinutes,
            e.Direction
        );
}
