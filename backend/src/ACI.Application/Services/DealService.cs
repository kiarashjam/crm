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
    private readonly IWorkspaceRecords _records;
    private readonly IAutomationEngine? _automations;
    private readonly IOutboundWebhookPublisher? _webhooks;
    private readonly IOrganizationRepository? _organizations;
    private readonly IDealStageRepository? _stages;
    private readonly ICollaborationNotifier? _notifier;
    private readonly ILogger<DealService> _logger;

    public DealService(
        IDealRepository repository, 
        IActivityRepository activityRepository,
        IWorkspaceRecords records,
        ILogger<DealService> logger,
        IAutomationEngine? automations = null,
        IOutboundWebhookPublisher? webhooks = null,
        IOrganizationRepository? organizations = null,
        IDealStageRepository? stages = null,
        ICollaborationNotifier? notifier = null)
    {
        _repository = repository;
        _activityRepository = activityRepository;
        _records = records;
        _logger = logger;
        _automations = automations;
        _webhooks = webhooks;
        _organizations = organizations;
        _stages = stages;
        _notifier = notifier;
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

        // The table, list and card layouts of the board show how each deal is moving too.
        var pace = await _repository.GetPaceAsync(userId, organizationId, list.Select(d => d.Id), ct);
        var staleDays = await WorkspaceStaleDaysAsync(organizationId, ct);
        var now = DateTime.UtcNow;
        return list.Select(d => Map(d, lastByDeal.TryGetValue(d.Id, out var last) ? last : null,
            pace.GetValueOrDefault(d.Id), staleDays, now)).ToList();
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<DealDto> Items, bool More)> GetOpenWorkAsync(
        Guid userId, Guid? organizationId, bool mineOnly, int take, CancellationToken ct = default)
    {
        var list = await _repository.GetOpenAsync(userId, organizationId, mineOnly, take + 1, ct);
        var deals = list.Take(take).ToList();
        // Read as the deal list reads them: last activity, time in stage and the stale flag.
        var lastByDeal = await _activityRepository.GetLastActivityByDealIdsAsync(userId, organizationId, deals.Select(d => d.Id), ct);
        var pace = await _repository.GetPaceAsync(userId, organizationId, deals.Select(d => d.Id), ct);
        var staleDays = await WorkspaceStaleDaysAsync(organizationId, ct);
        var now = DateTime.UtcNow;
        var items = deals.Select(d => Map(d, lastByDeal.TryGetValue(d.Id, out var last) ? last : null,
            pace.GetValueOrDefault(d.Id), staleDays, now)).ToList();
        return (items, list.Count > take);
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

        return await MapWithPaceAsync(entity, userId, organizationId, ct);
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

        var amount = DealMoney.AmountOf(request.Amount, request.Value);
        if (amount.IsFailure) return amount.Error;
        if (amount.Value is not decimal dealAmount)
        {
            _logger.LogWarning("Deal creation failed: Value is required");
            return DomainErrors.Deal.ValueRequired;
        }
        var currency = DealMoney.CurrencyOf(request.Currency, await _organizations.WorkspaceCurrencyAsync(organizationId, ct));
        if (currency.IsFailure) return currency.Error;

        var links = await _records.CheckLinksAsync(userId, organizationId, new RecordLinks
        {
            CompanyId = request.CompanyId,
            ContactId = request.ContactId,
            AssigneeId = request.AssigneeId,
            PipelineId = request.PipelineId,
            DealStageId = request.DealStageId,
        }, ct);
        if (links.IsFailure) return links.Error;

        var now = DateTime.UtcNow;
        var entity = new Deal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            Name = request.Name.Trim(),
            Currency = currency.Value,
            Stage = request.Stage?.Trim(),
            PipelineId = request.PipelineId,
            DealStageId = request.DealStageId,
            CompanyId = request.CompanyId,
            ContactId = request.ContactId,
            AssigneeId = request.AssigneeId,
            ExpectedCloseDateUtc = request.ExpectedCloseDateUtc,
            Description = request.Description?.Trim(),
            Probability = request.Probability,
            CreatedAtUtc = now,
        };
        DealMoney.SetAmount(entity, dealAmount);

        // The stage decides the deal's pipeline, its stage name and, for a won or lost
        // stage, its outcome, as a move into it does. Created in "Closed Won", a deal
        // sat in the won column while every report counted it as open. Its automations
        // are not run: recording a deal that is already won is not winning it.
        var stage = await StageForAsync(request.DealStageId, request.Stage, request.PipelineId, organizationId, ct);
        if (stage != null) DealStageMoves.Place(entity, stage, now);
        // Saved with the deal, so a deal never exists without the line saying when it
        // entered its first stage, which is where its time in that stage starts.
        if (DealStageMoves.Entered(entity, stage?.Name, null, null, userId, now) is { } entered)
            entity.StageChanges.Add(entered);

        entity = await _repository.AddAsync(entity, ct);
        // A deal made for someone else tells them.
        if (_notifier != null)
            await _notifier.RecordAssignedAsync(RecordTypes.Deal, entity.Id, entity.Name, organizationId, entity.AssigneeId, userId, null, ct);

        _logger.LogInformation(
            "Deal {DealId} created successfully for user {UserId}",
            entity.Id, userId);

        return await MapSavedAsync(entity, userId, organizationId, ct);
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
        var assigneeBefore = existing.AssigneeId;
        var oldStageName = existing.DealStage?.Name ?? existing.Stage;
        var previousWon = existing.IsWon;

        if (request.IsPrivate is bool makePrivate && makePrivate != existing.IsPrivate
            && !await MayChangePrivacyAsync(existing, userId, organizationId, ct))
            return DomainErrors.Deal.PrivacyNotOwner;

        // Only what this edit changes is checked, so a deal whose owner has since left
        // can still be edited. A pipeline and stage sent together have to agree, and a
        // new pipeline sent alone has to hold the stage the deal keeps: otherwise the
        // deal sat in a pipeline with none of its columns and dropped off the board.
        var stageOrPipelineChanges = (request.DealStageId is Guid st && st != existing.DealStageId)
            || (request.PipelineId is Guid p && p != existing.PipelineId);
        var links = await _records.CheckLinksAsync(userId, organizationId, new RecordLinks
        {
            CompanyId = request.CompanyId != existing.CompanyId ? request.CompanyId : null,
            ContactId = request.ContactId != existing.ContactId ? request.ContactId : null,
            AssigneeId = request.AssigneeId != existing.AssigneeId ? request.AssigneeId : null,
            PipelineId = stageOrPipelineChanges ? request.PipelineId : null,
            DealStageId = stageOrPipelineChanges ? request.DealStageId ?? existing.DealStageId : null,
        }, ct);
        if (links.IsFailure) return links.Error;

        var amount = DealMoney.AmountOf(request.Amount, request.Value);
        if (amount.IsFailure) return amount.Error;
        string? currency = null;
        if (!string.IsNullOrWhiteSpace(request.Currency))
        {
            if (!RegionalSettings.TryCurrency(request.Currency, out var code)) return DomainErrors.Deal.InvalidCurrency;
            currency = code;
        }

        var now = DateTime.UtcNow;

        // Apply partial updates
        if (request.Name != null) existing.Name = request.Name.Trim();
        if (amount.Value is decimal newAmount) DealMoney.SetAmount(existing, newAmount);
        if (currency != null) existing.Currency = currency;
        if (request.Stage != null) existing.Stage = request.Stage.Trim();
        if (request.PipelineId != null) existing.PipelineId = request.PipelineId;
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
            existing.ClosedAtUtc = now;
        }

        // The stage the deal moves to, if any.
        DealStage? target = null;
        if (request.DealStageId is Guid wanted && wanted != oldStageId)
        {
            existing.DealStageId = wanted;
            if (_stages != null && organizationId is Guid stageOrg)
            {
                target = await _stages.GetByIdAsync(wanted, stageOrg, ct);
                if (target == null) return DomainErrors.DealStage.NotFound;
            }
        }
        else if (request.DealStageId == null && request.Stage != null && !DealStageMoves.SameName(request.Stage, oldStageName))
        {
            // A move by name, from a client that knows stages only by name: to the stage
            // of that name in the deal's pipeline, when it has one, so the deal's outcome
            // and history follow as they do for a move by id.
            target = await StageForAsync(null, request.Stage, existing.PipelineId ?? existing.DealStage?.PipelineId, organizationId, ct);
        }
        else if (request.DealStageId == null && request.IsWon is bool closedAs)
        {
            // Won or lost from the deal page, which names no stage: the deal goes to its
            // pipeline's won or lost stage, so the board shows it where the reports count
            // it. It stayed in Negotiation, as an open deal on the board and a won one in
            // every report.
            target = await OutcomeStageAsync(existing, closedAs, organizationId, ct);
        }

        // A stage flagged won or lost closes the deal, and moving it back to an open
        // stage reopens it. The board moves deals by stage, and every report reads
        // IsWon, so without this a deal in a custom "Signed" stage counted as open.
        if (target != null)
        {
            existing.DealStageId = target.Id;
            existing.PipelineId = target.PipelineId;
            existing.Stage = target.Name;
            if (request.IsWon == null)
            {
                var outcome = DealStageMoves.OutcomeOf(target);
                if (outcome != existing.IsWon)
                {
                    existing.IsWon = outcome;
                    existing.ClosedAtUtc = outcome == null ? null : now;
                }
            }
        }

        // Every move is written, by id or by name, saved with the deal itself. Only moves
        // by id were, after the save, so a deal moved by name (every move in a workspace
        // without pipelines) or closed from its page left no history.
        var moved = existing.DealStageId != oldStageId
            || (existing.DealStageId == null && !DealStageMoves.SameName(existing.Stage, oldStageName));
        if (moved && DealStageMoves.Entered(existing, target?.Name, oldStageId, oldStageName, userId, now) is { } stageChange)
            existing.StageChanges.Add(stageChange);
        if (request.ClosedReason != null) existing.ClosedReason = request.ClosedReason.Trim();
        if (request.ClosedReasonCategory != null) existing.ClosedReasonCategory = request.ClosedReasonCategory.Trim();
        if (request.IsPrivate.HasValue) existing.IsPrivate = request.IsPrivate.Value;
        if (request.TeamId.HasValue) existing.TeamId = request.TeamId;

        existing.UpdatedAtUtc = now;
        existing.UpdatedByUserId = userId;

        var updated = await _repository.UpdateAsync(existing, userId, organizationId, ct);
        
        if (updated == null)
        {
            _logger.LogError(
                "Deal update failed unexpectedly for deal {DealId}",
                id);
            return DomainErrors.General.ServerError;
        }
        if (_notifier != null && existing.AssigneeId != assigneeBefore)
            await _notifier.RecordAssignedAsync(RecordTypes.Deal, existing.Id, existing.Name, organizationId, existing.AssigneeId, userId, null, ct);

        if (moved)
        {
            _logger.LogInformation(
                "Stage change recorded for deal {DealId}: {From} → {To}",
                id, oldStageName, existing.Stage);
            if (_automations != null)
            {
                await _automations.DispatchAsync("deal.stage_changed", new AutomationEvent(
                    organizationId, userId, $"{id}:{existing.DealStageId?.ToString() ?? existing.Stage}", existing.Name, DealId: id, AssigneeId: existing.AssigneeId), ct);
            }
            if (_webhooks != null)
                await _webhooks.QueueAsync(organizationId, "deal.stage_changed", new { id, existing.Name, stageId = existing.DealStageId, stage = existing.Stage }, ct);
        }

        if (_automations != null && existing.IsWon == true && previousWon != true)
            await _automations.DispatchAsync("deal.won", new AutomationEvent(organizationId, userId, $"{id}:won", existing.Name, DealId: id, AssigneeId: existing.AssigneeId), ct);
        if (_webhooks != null && existing.IsWon == true && previousWon != true)
            await _webhooks.QueueAsync(organizationId, "deal.won", new { id, existing.Name }, ct);
        if (_automations != null && existing.IsWon == false && previousWon != false)
            await _automations.DispatchAsync("deal.lost", new AutomationEvent(organizationId, userId, $"{id}:lost", existing.Name, DealId: id, AssigneeId: existing.AssigneeId), ct);

        _logger.LogInformation(
            "Deal {DealId} updated successfully",
            id);

        return await MapSavedAsync(updated, userId, organizationId, ct);
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

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<DealStageChangeDto>>> GetStageHistoryAsync(Guid dealId, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        // Through the same visibility as the deal: a private deal's moves are its own.
        var deal = await _repository.GetByIdAsync(dealId, userId, organizationId, ct);
        if (deal == null) return DomainErrors.Deal.NotFound;
        var changes = await _repository.GetStageChangesAsync(dealId, userId, organizationId, ct);

        // Each stage is left when the deal enters the next one; the newest is where it is.
        var chronological = changes.OrderBy(c => c.ChangedAtUtc).ThenBy(c => c.Id).ToList();
        var history = new List<DealStageChangeDto>(chronological.Count);
        for (var i = 0; i < chronological.Count; i++)
        {
            var c = chronological[i];
            history.Add(new DealStageChangeDto(
                c.Id,
                c.FromDealStageId,
                c.FromStageName,
                c.ToDealStageId,
                c.ToStageName,
                c.ChangedByUserId,
                c.ChangedByUser?.Name,
                c.ChangedAtUtc,
                i + 1 < chronological.Count ? chronological[i + 1].ChangedAtUtc : null,
                // The line written with the deal: from nowhere, when the deal was made.
                i == 0 && c.FromDealStageId == null && c.FromStageName == null
                    && (c.ChangedAtUtc - deal.CreatedAtUtc).Duration() < TimeSpan.FromMinutes(1)));
        }
        history.Reverse();
        return history;
    }

    /// <summary>
    /// The stage a write names: by id, or else by name within the pipeline given, so a
    /// client that knows stages only by name still puts the deal in one. Null when
    /// neither leads to one of this workspace's stages.
    /// </summary>
    private async Task<DealStage?> StageForAsync(Guid? stageId, string? stageName, Guid? pipelineId, Guid? organizationId, CancellationToken ct)
    {
        if (_stages == null || organizationId is not Guid org) return null;
        if (stageId is Guid id) return await _stages.GetByIdAsync(id, org, ct);
        if (pipelineId is not Guid pipeline || string.IsNullOrWhiteSpace(stageName)) return null;
        var named = (await _stages.GetByPipelineIdAsync(pipeline, ct))
            .FirstOrDefault(s => DealStageMoves.SameName(s.Name, stageName));
        // Read again through the workspace check: a pipeline's stages are listed by its id alone.
        return named == null ? null : await _stages.GetByIdAsync(named.Id, org, ct);
    }

    /// <summary>
    /// The first won (or lost) stage of the deal's pipeline, or null when the pipeline has
    /// none or the deal is already in one of them.
    /// </summary>
    private async Task<DealStage?> OutcomeStageAsync(Deal deal, bool won, Guid? organizationId, CancellationToken ct)
    {
        if (_stages == null || organizationId is not Guid org) return null;
        if ((deal.PipelineId ?? deal.DealStage?.PipelineId) is not Guid pipeline) return null;
        var outcomeStages = (await _stages.GetByPipelineIdAsync(pipeline, ct))
            .Where(s => won ? s.IsWon : s.IsLost)
            .ToList();
        if (outcomeStages.Count == 0 || outcomeStages.Any(s => s.Id == deal.DealStageId)) return null;
        return await _stages.GetByIdAsync(outcomeStages[0].Id, org, ct);
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
        // Whoever sees every deal decides about anyone's: the owner and managers by default.
        return await _organizations.HasPermissionAsync(userId, org, Domain.Common.MemberPermissions.SeeAllDeals, ct);
    }

    /// <summary>After how many quiet days the workspace's deals are stale, unless their pipeline says otherwise.</summary>
    private async Task<int> WorkspaceStaleDaysAsync(Guid? organizationId, CancellationToken ct)
    {
        if (organizationId is not Guid org || _organizations == null) return StaleDeals.DefaultDays;
        return (await _organizations.GetByIdAsync(org, ct))?.StaleDealDays ?? StaleDeals.DefaultDays;
    }

    /// <summary>A deal as reading it returns it: its last activity, when it came into its stage, and whether it has gone stale.</summary>
    private async Task<DealDto> MapWithPaceAsync(Deal deal, Guid userId, Guid? organizationId, CancellationToken ct)
    {
        var lastByDeal = await _activityRepository.GetLastActivityByDealIdsAsync(userId, organizationId, new[] { deal.Id }, ct);
        var pace = await _repository.GetPaceAsync(userId, organizationId, new[] { deal.Id }, ct);
        return Map(deal, lastByDeal.TryGetValue(deal.Id, out var last) ? last : null,
            pace.GetValueOrDefault(deal.Id), await WorkspaceStaleDaysAsync(organizationId, ct), DateTime.UtcNow);
    }

    /// <summary>
    /// A deal just created or saved, answered as reading it would be. Answered bare, a
    /// save said no history knew when the deal came into its stage right after writing
    /// that very line, and called a stale deal fresh: the deal page took the answer and
    /// showed "not recorded" and no stale flag until it was opened again.
    /// </summary>
    private async Task<DealDto> MapSavedAsync(Deal saved, Guid userId, Guid? organizationId, CancellationToken ct)
    {
        // Read again for what the save may have changed around it: the pipeline whose
        // stale days apply, and the owner, company and stage names it shows. Someone who
        // can no longer see it (an owner who handed a private deal on) gets it as saved.
        var deal = await _repository.GetByIdAsync(saved.Id, userId, organizationId, ct) ?? saved;
        return await MapWithPaceAsync(deal, userId, organizationId, ct);
    }

    /// <summary>A deal with how it is moving: when it came into its stage, when it was last worked on, and whether it has gone stale.</summary>
    private static DealDto Map(Deal e, DateTime? lastActivityAtUtc, DealPaceInfo? pace, int workspaceStaleDays, DateTime now)
    {
        var idleSince = StaleDeals.IdleSince(e.CreatedAtUtc, lastActivityAtUtc, pace?.LastMoveAtUtc);
        var staleDays = StaleDeals.DaysFor(e.Pipeline?.StaleDealDays, workspaceStaleDays);
        // Open: not marked won or lost, and not in a won or lost stage.
        var open = e.IsWon == null && e.DealStage is not { IsWon: true } and not { IsLost: true };
        return Map(e, lastActivityAtUtc) with
        {
            StageEnteredAtUtc = pace?.StageEnteredAtUtc,
            IdleSinceUtc = idleSince,
            IsStale = StaleDeals.IsStale(open, idleSince, staleDays, now),
            StaleAfterDays = staleDays,
        };
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
            e.TeamId,
            e.Amount);
}
