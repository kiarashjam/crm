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
    private readonly ILogger<LeadService> _logger;

    public LeadService(
        ILeadRepository repository, 
        ICompanyRepository companyRepository, 
        IContactRepository contactRepository, 
        IDealRepository dealRepository,
        ILogger<LeadService> logger)
    {
        _repository = repository;
        _companyRepository = companyRepository;
        _contactRepository = contactRepository;
        _dealRepository = dealRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PagedResult<LeadDto>> GetLeadsPagedAsync(
        Guid userId, 
        Guid? organizationId, 
        int page = 1, 
        int pageSize = 20, 
        string? search = null,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting paged leads for user {UserId}, organization {OrganizationId}, page {Page}, pageSize {PageSize}, search '{Search}'",
            userId, organizationId, page, pageSize, search);

        var skip = (page - 1) * pageSize;
        var (items, totalCount) = await _repository.GetPagedAsync(userId, organizationId, skip, pageSize, search, ct);
        
        var dtos = items.Select(Map).ToList();
        
        _logger.LogInformation(
            "Retrieved {Count} of {Total} leads for user {UserId} (page {Page})",
            items.Count, totalCount, userId, page);

        return PagedResult<LeadDto>.Create(dtos, totalCount, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LeadDto>> GetLeadsAsync(
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting leads for user {UserId}, organization {OrganizationId}",
            userId, organizationId);

        var list = await _repository.GetByUserIdAsync(userId, organizationId, ct);
        
        _logger.LogInformation(
            "Retrieved {Count} leads for user {UserId}",
            list.Count, userId);

        return list.Select(Map).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LeadDto>> SearchAsync(
        Guid userId, 
        Guid? organizationId, 
        string query, 
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Searching leads for user {UserId} with query '{Query}'",
            userId, query);

        var list = await _repository.SearchAsync(userId, organizationId, query.Trim(), ct);

        _logger.LogInformation(
            "Search returned {Count} leads for query '{Query}'",
            list.Count, query);

        return list.Select(Map).ToList();
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
            LastContactedAt = request.LastContactedAt,
            Description = request.Description?.Trim(),
            LifecycleStage = request.LifecycleStage?.Trim(),
            CreatedAtUtc = DateTime.UtcNow,
        };

        entity = await _repository.AddAsync(entity, ct);

        _logger.LogInformation(
            "Lead {LeadId} created successfully for user {UserId}",
            entity.Id, userId);

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

        // Apply partial updates
        if (request.Name != null) existing.Name = request.Name.Trim();
        if (request.Email != null) existing.Email = request.Email.Trim();
        if (request.Phone != null) existing.Phone = request.Phone.Trim();
        if (request.CompanyId != null) existing.CompanyId = request.CompanyId;
        if (request.Source != null) existing.Source = request.Source.Trim();
        if (request.Status != null) existing.Status = request.Status.Trim();
        if (request.LeadSourceId.HasValue) existing.LeadSourceId = request.LeadSourceId;
        if (request.LeadStatusId.HasValue) existing.LeadStatusId = request.LeadStatusId;
        if (request.LeadScore.HasValue) existing.LeadScore = request.LeadScore;
        if (request.LastContactedAt.HasValue) existing.LastContactedAt = request.LastContactedAt;
        if (request.Description != null) existing.Description = request.Description.Trim();
        if (request.LifecycleStage != null) existing.LifecycleStage = request.LifecycleStage.Trim();
        
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

        _logger.LogInformation(
            "Lead {LeadId} updated successfully",
            id);

        return Map(updated);
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

    private static LeadDto Map(Lead e) =>
        new LeadDto(
            e.Id, 
            e.Name, 
            e.Email, 
            e.Phone, 
            e.CompanyId, 
            e.Source, 
            e.Status, 
            e.LeadSourceId, 
            e.LeadStatusId, 
            e.LeadScore, 
            e.LastContactedAt, 
            e.Description, 
            e.LifecycleStage, 
            e.IsConverted, 
            e.ConvertedAtUtc);
}
