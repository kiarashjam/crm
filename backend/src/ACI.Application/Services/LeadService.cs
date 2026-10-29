using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// Service for managing leads in the CRM system.
/// </summary>
public class LeadService : ILeadService
{
    private readonly ILeadRepository _repository;
    private readonly ICompanyRepository _companyRepository;
    private readonly IContactRepository _contactRepository;
    private readonly IDealRepository _dealRepository;
    private readonly ILeadAutomation _automation;
    private readonly IWorkspaceRecords _records;
    private readonly IAutomationEngine? _automations;
    private readonly IOutboundWebhookPublisher? _webhooks;
    private readonly IContactRelationshipRepository? _relationships;
    private readonly ILogger<LeadService> _logger;

    public LeadService(
        ILeadRepository repository, 
        ICompanyRepository companyRepository, 
        IContactRepository contactRepository, 
        IDealRepository dealRepository,
        ILeadAutomation automation,
        IWorkspaceRecords records,
        ILogger<LeadService> logger,
        IAutomationEngine? automations = null,
        IOutboundWebhookPublisher? webhooks = null,
        IContactRelationshipRepository? relationships = null)
    {
        _repository = repository;
        _companyRepository = companyRepository;
        _contactRepository = contactRepository;
        _dealRepository = dealRepository;
        _automation = automation;
        _records = records;
        _logger = logger;
        _automations = automations;
        _webhooks = webhooks;
        _relationships = relationships;
    }

    /// <inheritdoc />
    public async Task<PagedResult<LeadDto>> GetLeadsPagedAsync(
        Guid userId,
        Guid? organizationId,
        int page = 1,
        int pageSize = 20,
        LeadQueryOptions? options = null,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting paged leads for user {UserId}, organization {OrganizationId}, page {Page}, pageSize {PageSize}",
            userId, organizationId, page, pageSize);

        await _contactRepository.EnsureMissingPersonContactsAsync(userId, organizationId, ct);

        var skip = (page - 1) * pageSize;
        var (items, totalCount) = await _repository.GetPagedAsync(userId, organizationId, skip, pageSize, options, ct);

        var dtos = await MapWithRelationshipsAsync(items, userId, organizationId, ct);

        _logger.LogInformation(
            "Retrieved {Count} of {Total} leads for user {UserId} (page {Page})",
            items.Count, totalCount, userId, page);

        return PagedResult<LeadDto>.Create(dtos, totalCount, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<LeadStatsDto> GetStatsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        await _repository.GetStatsAsync(userId, organizationId, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<LeadDto>> GetLeadsAsync(
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting leads for user {UserId}, organization {OrganizationId}",
            userId, organizationId);

        await _contactRepository.EnsureMissingPersonContactsAsync(userId, organizationId, ct);

        var list = await _repository.GetByUserIdAsync(userId, organizationId, ct);
        
        _logger.LogInformation(
            "Retrieved {Count} leads for user {UserId}",
            list.Count, userId);

        return await MapWithRelationshipsAsync(list, userId, organizationId, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LeadDto>> SearchAsync(
        Guid userId, 
        Guid? organizationId, 
        string query, 
        CancellationToken ct = default,
        int? take = null)
    {
        _logger.LogDebug(
            "Searching leads for user {UserId} with query '{Query}'",
            userId, query);

        var trimmed = query.Trim();
        var list = take is > 0
            ? await _repository.SearchAsync(userId, organizationId, trimmed, ct, take)
            : await _repository.SearchAsync(userId, organizationId, trimmed, ct);

        _logger.LogInformation(
            "Search returned {Count} leads for query '{Query}'",
            list.Count, query);

        return await MapWithRelationshipsAsync(list, userId, organizationId, ct);
    }

    /// <inheritdoc />
    public async Task<Result<LeadDto>> GetByIdAsync(
        Guid id, 
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting lead {LeadId} for user {UserId}",
            id, userId);

        var entity = await _repository.GetByIdAsync(id, userId, organizationId, ct);
        
        if (entity == null)
        {
            _logger.LogWarning(
                "Lead {LeadId} not found for user {UserId}",
                id, userId);
            return DomainErrors.Lead.NotFound;
        }

        return Map(entity);
    }

    /// <inheritdoc />
    public async Task<Result<LeadDto>> CreateAsync(
        Guid userId, 
        Guid? organizationId, 
        CreateLeadRequest request, 
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Creating lead with email {Email} for user {UserId}",
            request.Email, userId);

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            _logger.LogWarning("Lead creation failed: Name is required");
            return DomainErrors.Lead.NameRequired;
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            _logger.LogWarning("Lead creation failed: Email is required");
            return DomainErrors.Lead.EmailRequired;
        }

        // Validate email format
        if (!ValidationHelper.IsValidEmail(request.Email))
        {
            _logger.LogWarning("Lead creation failed: Invalid email format {Email}", request.Email);
            return DomainErrors.Lead.EmailInvalid;
        }

        var links = await _records.CheckLinksAsync(userId, organizationId, new RecordLinks { CompanyId = request.CompanyId }, ct);
        if (links.IsFailure) return links.Error;

        if (!request.AllowDuplicate)
        {
            var duplicate = await _automation.FindOpenDuplicateAsync(userId, organizationId, request.Email, null, ct);
            if (duplicate != null)
            {
                return new Error(
                    "Lead.Duplicate",
                    $"An open lead with this email already exists ({duplicate.Name}).");
            }
        }

        if (request.ReferredByContactId is Guid referrerId)
        {
            var referrer = await CheckReferrerAsync(referrerId, null, userId, organizationId, ct);
            if (referrer.IsFailure) return referrer.Error;
        }

        var entity = new Lead
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone?.Trim(),
            CompanyId = request.CompanyId,
            Source = request.Source?.Trim(),
            Status = request.Status?.Trim() ?? "New",
            LeadSourceId = request.LeadSourceId,
            LeadStatusId = request.LeadStatusId,
            LeadScore = request.LeadScore,
            ScoreIsManual = request.LeadScore.HasValue,
            LastContactedAt = request.LastContactedAt,
            Description = request.Description?.Trim(),
            LifecycleStage = request.LifecycleStage?.Trim(),
            JobTitle = request.JobTitle?.Trim(),
            Country = request.Country?.Trim(),
            Industry = request.Industry?.Trim(),
            CompanySize = request.CompanySize?.Trim(),
            ReferredByContactId = request.ReferredByContactId,
            CreatedAtUtc = DateTime.UtcNow,
        };

        await _automation.PrepareNewAsync(entity, ct);
        // An imported row is someone the workspace already knows, not an inquiry
        // waiting for an answer, so no first-response clock starts for it.
        if (request.IsImport)
            entity.FirstResponseDueAtUtc = null;
        // Already worked when it arrives: its status is past "New", or a contact date
        // came with it. That is a first response, as it is when an edit does the same
        // (see UpdateAsync); without it, a lead created as "Contacted" was reported
        // late hours after somebody had spoken to them.
        if (!string.Equals(entity.Status.Trim(), "New", StringComparison.OrdinalIgnoreCase) || entity.LastContactedAt.HasValue)
        {
            entity.FirstResponseAtUtc = entity.CreatedAtUtc;
            entity.SlaBreached = false;
        }
        entity = await _repository.AddAsync(entity, ct);
        await _automation.RecordStatusAsync(entity, null, entity.Status, userId, ct);
        await _contactRepository.EnsurePersonContactAsync(entity, ct);

        _logger.LogInformation(
            "Lead {LeadId} created successfully for user {UserId}",
            entity.Id, userId);

        // An import is not a new lead arriving: its rows do not set off the
        // workspace's welcome emails and sequences unless the person importing asked
        // for that, and they are not announced to integrations one webhook per row.
        if (_automations != null && (!request.IsImport || request.RunAutomations))
        {
            await _automations.DispatchAsync("lead.created", new AutomationEvent(
                organizationId, userId, entity.Id.ToString(), entity.Name, LeadId: entity.Id, Email: entity.Email, AssigneeId: entity.AssignedToUserId), ct);
        }
        if (_webhooks != null && !request.IsImport)
            await _webhooks.PublishAsync(organizationId, "lead.created", new { entity.Id, entity.Name, entity.Email }, ct);

        return Map(entity);
    }

    /// <inheritdoc />
    public async Task<Result<LeadDto>> UpdateAsync(
        Guid id, 
        Guid userId, 
        Guid? organizationId, 
        UpdateLeadRequest request, 
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Updating lead {LeadId} for user {UserId}",
            id, userId);

        var existing = await _repository.GetByIdAsync(id, userId, organizationId, ct);
        
        if (existing == null)
        {
            _logger.LogWarning(
                "Lead update failed: Lead {LeadId} not found",
                id);
            return DomainErrors.Lead.NotFound;
        }

        if (existing.IsConverted)
        {
            _logger.LogWarning(
                "Lead update failed: Lead {LeadId} is already converted",
                id);
            return DomainErrors.Lead.AlreadyConverted;
        }

        // Validate email format if provided
        if (request.Email != null && !ValidationHelper.IsValidEmail(request.Email))
        {
            _logger.LogWarning("Lead update failed: Invalid email format {Email}", request.Email);
            return DomainErrors.Lead.EmailInvalid;
        }

        if (request.CompanyId is Guid companyId && companyId != existing.CompanyId)
        {
            var links = await _records.CheckLinksAsync(userId, organizationId, new RecordLinks { CompanyId = companyId }, ct);
            if (links.IsFailure) return links.Error;
        }

        if (request.Email != null && !request.Email.Trim().Equals(existing.Email, StringComparison.OrdinalIgnoreCase))
        {
            var duplicate = await _automation.FindOpenDuplicateAsync(userId, organizationId, request.Email, existing.Id, ct);
            if (duplicate != null)
                return new Error("Lead.Duplicate", $"An open lead with this email already exists ({duplicate.Name}).");
        }

        var previousStatus = existing.Status;

        // Apply partial updates
        if (request.Name != null) existing.Name = request.Name.Trim();
        if (request.Email != null) existing.Email = request.Email.Trim();
        if (request.Phone != null) existing.Phone = request.Phone.Trim();
        if (request.CompanyId != null) existing.CompanyId = request.CompanyId;
        if (request.Source != null) existing.Source = request.Source.Trim();
        if (request.Status != null) existing.Status = request.Status.Trim();
        if (request.LeadSourceId.HasValue) existing.LeadSourceId = request.LeadSourceId;
        if (request.LeadStatusId.HasValue) existing.LeadStatusId = request.LeadStatusId;
        // A score only becomes manual when someone actually changes it. Both edit
        // dialogs pre-fill the current automatic score and send it back with every
        // save, so fixing a typo in a phone number used to freeze that lead's score
        // for good — no later rescore, no email-open points, nothing to undo it.
        if (request.LeadScore.HasValue && request.LeadScore != existing.LeadScore)
        {
            existing.LeadScore = request.LeadScore;
            existing.ScoreIsManual = true;
        }
        if (request.LastContactedAt.HasValue) existing.LastContactedAt = request.LastContactedAt;
        if (request.Description != null) existing.Description = request.Description.Trim();
        if (request.LifecycleStage != null) existing.LifecycleStage = request.LifecycleStage.Trim();
        if (request.PipelineState != null) existing.PipelineState = request.PipelineState;
        if (request.JobTitle != null) existing.JobTitle = request.JobTitle.Trim();
        if (request.Country != null) existing.Country = request.Country.Trim();
        if (request.Industry != null) existing.Industry = request.Industry.Trim();
        if (request.CompanySize != null) existing.CompanySize = request.CompanySize.Trim();

        // Someone has worked this lead: its status left "New" (the pipeline moves it
        // when outreach is logged), or a contact date was recorded. Either one is a
        // first response. Only call/meeting/email activities used to count, so a
        // lead moved along through the pipeline still raised "First response is
        // late" hours after it was called.
        var worked = !string.Equals(existing.Status?.Trim(), "New", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(previousStatus?.Trim(), existing.Status?.Trim(), StringComparison.OrdinalIgnoreCase);
        if (existing.FirstResponseAtUtc == null && (worked || request.LastContactedAt.HasValue))
        {
            existing.FirstResponseAtUtc = DateTime.UtcNow;
            existing.SlaBreached = false;
        }

        if (!existing.ScoreIsManual)
            await _automation.RescoreAsync(existing, ct);

        existing.UpdatedAtUtc = DateTime.UtcNow;
        existing.UpdatedByUserId = userId;

        var updated = await _repository.UpdateAsync(existing, userId, organizationId, ct);
        
        if (updated == null)
        {
            _logger.LogError(
                "Lead update failed unexpectedly for lead {LeadId}",
                id);
            return DomainErrors.General.ServerError;
        }

        await _contactRepository.EnsurePersonContactAsync(updated, ct);
        if (!string.Equals(previousStatus, updated.Status, StringComparison.Ordinal))
        {
            await _automation.RecordStatusAsync(updated, previousStatus, updated.Status, userId, ct);
            if (_automations != null)
            {
                await _automations.DispatchAsync("lead.status_changed", new AutomationEvent(
                    organizationId, userId, $"{updated.Id}:{updated.Status}", updated.Name, LeadId: updated.Id, Email: updated.Email, AssigneeId: updated.AssignedToUserId), ct);
            }
        }

        _logger.LogInformation(
            "Lead {LeadId} updated successfully",
            id);

        return Map(updated);
    }

    /// <inheritdoc />
    public async Task<Result<LeadDto>> AssignAsync(
        Guid id,
        Guid userId,
        Guid? organizationId,
        Guid? assignedToUserId,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Assigning lead {LeadId} to user {AssignedToUserId} (requested by {UserId})",
            id, assignedToUserId, userId);

        // In a workspace, a member; in a personal one, only you. Any account could be
        // made the owner, and then got the lead's SLA alerts and new inquiries.
        var links = await _records.CheckLinksAsync(userId, organizationId, new RecordLinks { AssigneeId = assignedToUserId }, ct);
        if (links.IsFailure) return links.Error;

        var updated = await _repository.AssignAsync(id, userId, organizationId, assignedToUserId, ct);

        if (updated == null)
        {
            _logger.LogWarning("Lead assignment failed: Lead {LeadId} not found", id);
            return DomainErrors.Lead.NotFound;
        }

        return Map(updated);
    }

    /// <inheritdoc />
    public async Task<Result<LeadDto>> SetReferralAsync(
        Guid id,
        Guid userId,
        Guid? organizationId,
        Guid? contactId,
        CancellationToken ct = default)
    {
        var lead = await _repository.GetByIdAsync(id, userId, organizationId, ct);
        if (lead == null) return DomainErrors.Lead.NotFound;
        // Who referred a lead stays true after it converts, so this is allowed on a converted lead.
        if (contactId is Guid referrerId)
        {
            var referrer = await CheckReferrerAsync(referrerId, lead, userId, organizationId, ct);
            if (referrer.IsFailure) return referrer.Error;
        }

        var updated = await _repository.SetReferralAsync(id, userId, organizationId, contactId, ct);
        if (updated == null) return DomainErrors.Lead.NotFound;
        _logger.LogInformation("Lead {LeadId} referral set to {ContactId}", id, contactId);
        return Map(updated);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<LeadDto>>> GetReferredByContactAsync(
        Guid contactId,
        Guid userId,
        Guid? organizationId,
        CancellationToken ct = default)
    {
        var contact = await _contactRepository.GetByIdAsync(contactId, userId, organizationId, ct);
        if (contact == null) return Result.Failure<IReadOnlyList<LeadDto>>(DomainErrors.Contact.NotFound);
        var leads = await _repository.GetReferredByAsync(contactId, userId, organizationId, ct);
        return Result.Success<IReadOnlyList<LeadDto>>(leads.Select(Map).ToList());
    }

    /// <summary>
    /// A referrer has to be a contact this workspace can see, still active, and not the
    /// contact this same lead became: a lead cannot refer itself.
    /// </summary>
    private async Task<Result> CheckReferrerAsync(Guid contactId, Lead? lead, Guid userId, Guid? organizationId, CancellationToken ct)
    {
        var contact = await _contactRepository.GetByIdAsync(contactId, userId, organizationId, ct);
        if (contact == null) return DomainErrors.Lead.ReferrerNotFound;
        if (lead != null && (lead.ConvertedToContactId == contactId || contact.ConvertedFromLeadId == lead.Id))
            return DomainErrors.Lead.ReferrerIsSelf;
        // An archived contact already recorded as the referrer may stay; a new one may not.
        if (contact.IsArchived && lead?.ReferredByContactId != contactId) return DomainErrors.Lead.ReferrerArchived;
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(
        Guid id,
        Guid userId,
        Guid? organizationId,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Deleting lead {LeadId} for user {UserId}",
            id, userId);

        var deleted = await _repository.DeleteAsync(id, userId, organizationId, ct);
        
        if (!deleted)
        {
            _logger.LogWarning(
                "Lead deletion failed: Lead {LeadId} not found",
                id);
            return DomainErrors.Lead.NotFound;
        }

        _logger.LogInformation(
            "Lead {LeadId} deleted successfully",
            id);

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> MergeAsync(Guid keepId, Guid dropId, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        if (keepId == dropId) return DomainErrors.Lead.NotFound;
        var merged = await _repository.MergeIntoAsync(keepId, dropId, userId, organizationId, ct);
        if (!merged) return DomainErrors.Lead.NotFound;
        _logger.LogInformation("Merged lead {DropId} into {KeepId}", dropId, keepId);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<ConvertLeadResult>> ConvertAsync(
        Guid leadId, 
        Guid userId, 
        Guid? organizationId, 
        ConvertLeadRequest request, 
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Converting lead {LeadId} for user {UserId}. CreateContact: {CreateContact}, CreateDeal: {CreateDeal}",
            leadId, userId, request.CreateContact, request.CreateDeal);

        var lead = await _repository.GetByIdAsync(leadId, userId, organizationId, ct);
        
        if (lead == null)
        {
            _logger.LogWarning(
                "Lead conversion failed: Lead {LeadId} not found",
                leadId);
            return DomainErrors.Lead.NotFound;
        }

        if (lead.IsConverted)
        {
            _logger.LogWarning(
                "Lead conversion failed: Lead {LeadId} is already converted",
                leadId);
            return DomainErrors.Lead.AlreadyConverted;
        }

        if (!request.CreateContact && !request.CreateDeal && request.ExistingDealId == null)
        {
            _logger.LogWarning(
                "Lead conversion failed: No conversion action specified for lead {LeadId}",
                leadId);
            return new Error("Lead.ConversionRequired", "At least one conversion action must be specified");
        }

        // Before anything is created, so a refused pipeline or stage leaves no half-converted lead.
        if (request.CreateDeal && !request.ExistingDealId.HasValue)
        {
            var links = await _records.CheckLinksAsync(userId, organizationId, new RecordLinks
            {
                PipelineId = request.PipelineId,
                DealStageId = request.DealStageId,
            }, ct);
            if (links.IsFailure) return links.Error;
        }

        Guid? companyId = lead.CompanyId;
        Guid? newCompanyId = null;

        // Handle company creation/linking
        if (request.CreateNewCompany)
        {
            var companyName = string.IsNullOrWhiteSpace(request.NewCompanyName) 
                ? (lead.Company?.Name ?? lead.Name) 
                : request.NewCompanyName.Trim();
                
            if (!string.IsNullOrWhiteSpace(companyName))
            {
                var company = new Company
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    OrganizationId = organizationId,
                    Name = companyName,
                    CreatedAtUtc = DateTime.UtcNow,
                };
                await _companyRepository.AddAsync(company, ct);
                companyId = company.Id;
                newCompanyId = company.Id;
                
                _logger.LogInformation(
                    "Created company {CompanyId} during lead conversion",
                    company.Id);
            }
        }
        else if (request.ExistingCompanyId.HasValue)
        {
            var existingCompany = await _companyRepository.GetByIdAsync(
                request.ExistingCompanyId.Value, userId, organizationId, ct);
                
            if (existingCompany == null)
            {
                _logger.LogWarning(
                    "Lead conversion failed: Company {CompanyId} not found",
                    request.ExistingCompanyId);
                return DomainErrors.Company.NotFound;
            }
            companyId = request.ExistingCompanyId;
        }

        Guid? contactId = request.ExistingContactId;
        
        // Validate existing contact if specified
        if (request.ExistingContactId.HasValue)
        {
            var existingContact = await _contactRepository.GetByIdAsync(
                request.ExistingContactId.Value, userId, organizationId, ct);
                
            if (existingContact == null)
            {
                _logger.LogWarning(
                    "Lead conversion failed: Contact {ContactId} not found",
                    request.ExistingContactId);
                return DomainErrors.Contact.NotFound;
            }
        }

        // Handle contact creation
        if (request.CreateContact && !request.ExistingContactId.HasValue)
        {
            var existingContactByEmail = await _contactRepository.GetByEmailAsync(
                lead.Email, userId, organizationId, ct);
                
            if (existingContactByEmail != null)
            {
                contactId = existingContactByEmail.Id;
                
                if (companyId.HasValue && existingContactByEmail.CompanyId != companyId)
                {
                    existingContactByEmail.CompanyId = companyId;
                    existingContactByEmail.UpdatedAtUtc = DateTime.UtcNow;
                    existingContactByEmail.UpdatedByUserId = userId;
                    await _contactRepository.UpdateAsync(existingContactByEmail, userId, organizationId, ct);
                }
                
                _logger.LogInformation(
                    "Reused existing contact {ContactId} for lead conversion",
                    contactId);
            }
            else
            {
                var contact = new Contact
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    OrganizationId = organizationId,
                    Name = lead.Name,
                    Email = lead.Email,
                    Phone = lead.Phone,
                    CompanyId = companyId,
                    CreatedAtUtc = DateTime.UtcNow,
                    ConvertedFromLeadId = lead.Id,
                    ConvertedAtUtc = DateTime.UtcNow,
                };
                await _contactRepository.AddAsync(contact, ct);
                contactId = contact.Id;
                
                _logger.LogInformation(
                    "Created contact {ContactId} during lead conversion",
                    contact.Id);
            }
        }

        Guid? dealId = request.ExistingDealId;
        
        // Handle deal creation
        if (request.CreateDeal && !request.ExistingDealId.HasValue)
        {
            var deal = new Deal
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrganizationId = organizationId,
                Name = string.IsNullOrWhiteSpace(request.DealName) ? lead.Name : request.DealName.Trim(),
                Value = string.IsNullOrWhiteSpace(request.DealValue) ? "0" : request.DealValue.Trim(),
                Stage = request.DealStage?.Trim(),
                PipelineId = request.PipelineId,
                DealStageId = request.DealStageId,
                CompanyId = companyId,
                ContactId = contactId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            await _dealRepository.AddAsync(deal, ct);
            dealId = deal.Id;
            
            _logger.LogInformation(
                "Created deal {DealId} during lead conversion",
                deal.Id);
        }
        else if (request.ExistingDealId.HasValue && contactId.HasValue)
        {
            var existingDeal = await _dealRepository.GetByIdAsync(
                request.ExistingDealId.Value, userId, organizationId, ct);
                
            if (existingDeal != null)
            {
                existingDeal.ContactId = contactId;
                existingDeal.UpdatedAtUtc = DateTime.UtcNow;
                existingDeal.UpdatedByUserId = userId;
                await _dealRepository.UpdateAsync(existingDeal, userId, organizationId, ct);
            }
        }

        // Mark lead as converted with full traceability
        lead.IsConverted = true;
        lead.ConvertedAtUtc = DateTime.UtcNow;
        lead.UpdatedAtUtc = DateTime.UtcNow;
        lead.UpdatedByUserId = userId;
        
        if (contactId.HasValue && request.CreateContact) lead.ConvertedToContactId = contactId;
        if (dealId.HasValue && request.CreateDeal) lead.ConvertedToDealId = dealId;
        if (newCompanyId.HasValue) lead.ConvertedToCompanyId = newCompanyId;
        
        await _repository.UpdateAsync(lead, userId, organizationId, ct);

        _logger.LogInformation(
            "Lead {LeadId} converted successfully. CompanyId: {CompanyId}, ContactId: {ContactId}, DealId: {DealId}",
            leadId, companyId, contactId, dealId);

        return new ConvertLeadResult(companyId, contactId, dealId);
    }

    /// <summary>
    /// Leads for a list, each with the relationships of its person contact (spouse or
    /// partner first), read in one query for the whole page.
    /// </summary>
    private async Task<List<LeadDto>> MapWithRelationshipsAsync(IReadOnlyList<Lead> leads, Guid userId, Guid? organizationId, CancellationToken ct)
    {
        var dtos = leads.Select(Map).ToList();
        if (_relationships == null) return dtos;
        var contactIds = leads.Where(l => l.ConvertedToContactId != null).Select(l => l.ConvertedToContactId!.Value).Distinct().ToList();
        if (contactIds.Count == 0) return dtos;
        var rows = await _relationships.ForContactsAsync(contactIds, userId, organizationId, ct);
        if (rows.Count == 0) return dtos;
        for (var i = 0; i < leads.Count; i++)
        {
            if (leads[i].ConvertedToContactId is not Guid contactId) continue;
            var seen = ContactRelationshipService.SeenFrom(rows, contactId);
            if (seen.Count > 0) dtos[i] = dtos[i] with { Relationships = seen };
        }
        return dtos;
    }

    private static LeadDto Map(Lead e) =>
        new LeadDto(
            e.Id,
            e.Name,
            e.Email,
            e.Phone,
            e.CompanyId,
            e.Company?.Name,
            e.Source,
            e.Status,
            e.LeadSourceId,
            e.LeadStatusId,
            e.LeadScore,
            e.LastContactedAt,
            e.Description,
            e.LifecycleStage,
            e.IsConverted,
            e.ConvertedAtUtc,
            e.CreatedAtUtc,
            e.AssignedToUserId,
            e.AssignedToUser?.Name,
            e.PipelineState,
            e.JobTitle,
            e.Country,
            e.Industry,
            e.CompanySize,
            e.FirstResponseAtUtc,
            e.FirstResponseDueAtUtc,
            e.SlaBreached,
            e.QueueId,
            null,
            e.TerritoryId,
            null,
            e.EmailOpenCount,
            e.EnrichedDomain,
            e.ScoreIsManual,
            e.ReferredByContactId,
            e.ReferredByContact?.Name);
}
