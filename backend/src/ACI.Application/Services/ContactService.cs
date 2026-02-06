using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// Service for managing contacts in the CRM system.
/// </summary>
public class ContactService : IContactService
{
    private readonly IContactRepository _repository;
    private readonly IActivityRepository _activityRepository;
    private readonly ILogger<ContactService> _logger;

    public ContactService(
        IContactRepository repository, 
        IActivityRepository activityRepository,
        ILogger<ContactService> logger)
    {
        _repository = repository;
        _activityRepository = activityRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PagedResult<ContactDto>> GetContactsPagedAsync(
        Guid userId, 
        Guid? organizationId, 
        int page = 1, 
        int pageSize = 20, 
        string? search = null,
        bool includeArchived = false, 
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting paged contacts for user {UserId}, organization {OrganizationId}, page {Page}, pageSize {PageSize}, search '{Search}'",
            userId, organizationId, page, pageSize, search);

        var skip = (page - 1) * pageSize;
        var (items, totalCount) = await _repository.GetPagedAsync(userId, organizationId, skip, pageSize, search, includeArchived, ct);
        
        var lastByContact = await _activityRepository.GetLastActivityByContactIdsAsync(
            userId, organizationId, items.Select(c => c.Id), ct);
        
        var dtos = items.Select(c => Map(c, lastByContact.TryGetValue(c.Id, out var last) ? last : null)).ToList();
        
        _logger.LogInformation(
            "Retrieved {Count} of {Total} contacts for user {UserId} (page {Page})",
            items.Count, totalCount, userId, page);

        return PagedResult<ContactDto>.Create(dtos, totalCount, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContactDto>> GetContactsAsync(
        Guid userId, 
        Guid? organizationId, 
        bool includeArchived = false, 
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting contacts for user {UserId}, organization {OrganizationId}, includeArchived: {IncludeArchived}",
            userId, organizationId, includeArchived);

        var list = await _repository.GetByUserIdAsync(userId, organizationId, includeArchived, ct);
        var lastByContact = await _activityRepository.GetLastActivityByContactIdsAsync(
            userId, organizationId, list.Select(c => c.Id), ct);
        
        _logger.LogInformation(
            "Retrieved {Count} contacts for user {UserId}",
            list.Count, userId);

        return list.Select(c => Map(c, lastByContact.TryGetValue(c.Id, out var last) ? last : null)).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ContactDto>> SearchAsync(
        Guid userId, 
        Guid? organizationId, 
        string query, 
        bool includeArchived = false, 
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Searching contacts for user {UserId} with query '{Query}'",
            userId, query);

        var list = await _repository.SearchAsync(userId, organizationId, query.Trim(), includeArchived, ct);
        var lastByContact = await _activityRepository.GetLastActivityByContactIdsAsync(
            userId, organizationId, list.Select(c => c.Id), ct);

        _logger.LogInformation(
            "Search returned {Count} contacts for query '{Query}'",
            list.Count, query);

        return list.Select(c => Map(c, lastByContact.TryGetValue(c.Id, out var last) ? last : null)).ToList();
    }

    /// <inheritdoc />
    public async Task<Result<ContactDto>> GetByIdAsync(
        Guid id, 
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting contact {ContactId} for user {UserId}",
            id, userId);

        var entity = await _repository.GetByIdAsync(id, userId, organizationId, ct);
        
        if (entity == null)
        {
            _logger.LogWarning(
                "Contact {ContactId} not found for user {UserId}",
                id, userId);
            return DomainErrors.Contact.NotFound;
        }

        var lastByContact = await _activityRepository.GetLastActivityByContactIdsAsync(
            userId, organizationId, new[] { id }, ct);
        
        return Map(entity, lastByContact.TryGetValue(id, out var last) ? last : null);
    }

    /// <inheritdoc />
    public async Task<Result<ContactDto>> CreateAsync(
        Guid userId, 
        Guid? organizationId, 
        CreateContactRequest request, 
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Creating contact with email {Email} for user {UserId}, organization {OrganizationId}",
            request.Email, userId, organizationId);

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            _logger.LogWarning("Contact creation failed: Name is required");
            return DomainErrors.Contact.NameRequired;
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            _logger.LogWarning("Contact creation failed: Email is required");
            return DomainErrors.Contact.EmailRequired;
        }

        // Validate email format
        if (!ValidationHelper.IsValidEmail(request.Email))
        {
            _logger.LogWarning("Contact creation failed: Invalid email format {Email}", request.Email);
            return DomainErrors.Contact.EmailInvalid;
        }

        // Check for duplicate email
        var emailExists = await _repository.EmailExistsAsync(request.Email.Trim(), organizationId, null, ct);
        if (emailExists)
        {
            _logger.LogWarning(
                "Contact creation failed: Duplicate email {Email} in organization {OrganizationId}",
                request.Email, organizationId);
            return DomainErrors.Contact.DuplicateEmailWithValue(request.Email.Trim());
        }

        var entity = new Contact
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone?.Trim(),
            JobTitle = request.JobTitle?.Trim(),
            CompanyId = request.CompanyId,
            CreatedAtUtc = DateTime.UtcNow,
        };

        entity = await _repository.AddAsync(entity, ct);

        _logger.LogInformation(
            "Contact {ContactId} created successfully for user {UserId}",
            entity.Id, userId);

        return Map(entity, null);
    }

    /// <inheritdoc />
    public async Task<Result<ContactDto>> UpdateAsync(
        Guid id, 
        Guid userId, 
        Guid? organizationId, 
        UpdateContactRequest request, 
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Updating contact {ContactId} for user {UserId}",
            id, userId);

        var existing = await _repository.GetByIdAsync(id, userId, organizationId, ct);
        
        if (existing == null)
        {
            _logger.LogWarning(
                "Contact update failed: Contact {ContactId} not found for user {UserId}",
                id, userId);
            return DomainErrors.Contact.NotFound;
        }

        // Validate email format if provided
        if (request.Email != null && !ValidationHelper.IsValidEmail(request.Email))
        {
            _logger.LogWarning("Contact update failed: Invalid email format {Email}", request.Email);
            return DomainErrors.Contact.EmailInvalid;
        }

        // Check for duplicate email if email is being changed
        if (request.Email != null && !string.Equals(existing.Email, request.Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var emailExists = await _repository.EmailExistsAsync(request.Email.Trim(), organizationId, id, ct);
            if (emailExists)
            {
                _logger.LogWarning(
                    "Contact update failed: Duplicate email {Email}",
                    request.Email);
                return DomainErrors.Contact.DuplicateEmailWithValue(request.Email.Trim());
            }
        }

        // Apply partial updates
        if (request.Name != null) existing.Name = request.Name.Trim();
        if (request.Email != null) existing.Email = request.Email.Trim();
        if (request.Phone != null) existing.Phone = request.Phone.Trim();
        if (request.CompanyId.HasValue) existing.CompanyId = request.CompanyId;
        if (request.JobTitle != null) existing.JobTitle = request.JobTitle.Trim();
        if (request.DoNotContact.HasValue) existing.DoNotContact = request.DoNotContact.Value;
        if (request.PreferredContactMethod != null) existing.PreferredContactMethod = request.PreferredContactMethod.Trim();

        existing.UpdatedAtUtc = DateTime.UtcNow;
        existing.UpdatedByUserId = userId;

        var updated = await _repository.UpdateAsync(existing, userId, organizationId, ct);
        
        if (updated == null)
        {
            _logger.LogError(
                "Contact update failed unexpectedly for contact {ContactId}",
                id);
            return DomainErrors.General.ServerError;
        }

        _logger.LogInformation(
            "Contact {ContactId} updated successfully",
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
            "Deleting contact {ContactId} for user {UserId}",
            id, userId);

        var deleted = await _repository.DeleteAsync(id, userId, organizationId, ct);
        
        if (!deleted)
        {
            _logger.LogWarning(
                "Contact deletion failed: Contact {ContactId} not found",
                id);
            return DomainErrors.Contact.NotFound;
        }

        _logger.LogInformation(
            "Contact {ContactId} deleted successfully",
            id);

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ArchiveAsync(
        Guid id, 
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Archiving contact {ContactId} for user {UserId}",
            id, userId);

        var archived = await _repository.ArchiveAsync(id, userId, organizationId, ct);
        
        if (!archived)
        {
            _logger.LogWarning(
                "Contact archival failed: Contact {ContactId} not found",
                id);
            return DomainErrors.Contact.NotFound;
        }

        _logger.LogInformation(
            "Contact {ContactId} archived successfully",
            id);

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> UnarchiveAsync(
        Guid id, 
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Unarchiving contact {ContactId} for user {UserId}",
            id, userId);

        var unarchived = await _repository.UnarchiveAsync(id, userId, organizationId, ct);
        
        if (!unarchived)
        {
            _logger.LogWarning(
                "Contact unarchival failed: Contact {ContactId} not found",
                id);
            return DomainErrors.Contact.NotFound;
        }

        _logger.LogInformation(
            "Contact {ContactId} unarchived successfully",
            id);

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<bool> EmailExistsAsync(
        string email, 
        Guid? organizationId, 
        Guid? excludeContactId = null, 
        CancellationToken ct = default)
    {
        return await _repository.EmailExistsAsync(email, organizationId, excludeContactId, ct);
    }

    private static ContactDto Map(Contact e, DateTime? lastActivityAtUtc = null) =>
        new ContactDto(
            e.Id,
            e.Name,
            e.Email,
            e.Phone,
            e.JobTitle,
            e.CompanyId,
            lastActivityAtUtc,
            e.ConvertedFromLeadId,
            e.ConvertedAtUtc,
            e.IsArchived,
            e.DoNotContact,
            e.PreferredContactMethod);
}
