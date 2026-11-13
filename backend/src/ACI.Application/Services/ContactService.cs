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
    private readonly IWorkspaceRecords _records;
    private readonly ILogger<ContactService> _logger;

    public ContactService(
        IContactRepository repository, 
        IActivityRepository activityRepository,
        IWorkspaceRecords records,
        ILogger<ContactService> logger)
    {
        _repository = repository;
        _activityRepository = activityRepository;
        _records = records;
        _logger = logger;
    }

    /// <summary>The most rows one export writes: plenty for a workspace, and a bound on one request.</summary>
    public const int MaxExportRows = 50_000;

    /// <inheritdoc />
    public Task<PagedResult<ContactDto>> GetContactsPagedAsync(
        Guid userId, 
        Guid? organizationId, 
        int page = 1, 
        int pageSize = 20, 
        string? search = null,
        bool includeArchived = false,
        Guid? companyId = null, 
        CancellationToken ct = default) =>
        GetContactsPagedAsync(userId, organizationId, page, pageSize, new ContactQueryOptions
        {
            Search = search,
            Status = includeArchived ? ContactQueryOptions.All : ContactQueryOptions.Active,
            CompanyId = companyId,
        }, ct);

    /// <inheritdoc />
    public async Task<PagedResult<ContactDto>> GetContactsPagedAsync(
        Guid userId,
        Guid? organizationId,
        int page,
        int pageSize,
        ContactQueryOptions options,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting paged contacts for user {UserId}, organization {OrganizationId}, page {Page}, pageSize {PageSize}, search '{Search}', status {Status}",
            userId, organizationId, page, pageSize, options.Search, options.NormalizedStatus);

        var skip = (Math.Max(page, 1) - 1) * pageSize;
        var (items, totalCount) = await _repository.GetPagedAsync(userId, organizationId, skip, pageSize, options, ct);
        
        var lastByContact = await _activityRepository.GetLastActivityByContactIdsAsync(
            userId, organizationId, items.Select(c => c.Id), ct);
        
        var dtos = items.Select(c => Map(c, lastByContact.TryGetValue(c.Id, out var last) ? last : null)).ToList();
        
        _logger.LogInformation(
            "Retrieved {Count} of {Total} contacts for user {UserId} (page {Page})",
            items.Count, totalCount, userId, page);

        return PagedResult<ContactDto>.Create(dtos, totalCount, Math.Max(page, 1), pageSize);
    }

    /// <inheritdoc />
    public Task<ContactStatsDto> GetStatsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        _repository.GetStatsAsync(userId, organizationId, ct);

    /// <inheritdoc />
    public async Task<ExportFile> ExportAsync(Guid userId, Guid? organizationId, ContactQueryOptions options, char separator = CsvExport.Comma, CancellationToken ct = default)
    {
        // The rows the list would show, all of them: the page used to export the twenty
        // contacts on screen.
        var (rows, total) = await _repository.GetExportRowsAsync(userId, organizationId, options, MaxExportRows, ct);
        var csv = new CsvExport(separator, "Name", "Email", "Phone", "Job title", "Company", "Do not contact", "Archived", "Last activity (UTC)", "Added (UTC)");
        foreach (var r in rows)
        {
            csv.Row(r.Name, r.Email, r.Phone, r.JobTitle, r.CompanyName, CsvExport.YesNo(r.DoNotContact), CsvExport.YesNo(r.IsArchived),
                CsvExport.Utc(r.LastActivityAtUtc), CsvExport.Utc(r.CreatedAtUtc));
        }
        _logger.LogInformation("Exported {Count} of {Total} contacts for user {UserId}", rows.Count, total, userId);
        return new ExportFile($"contacts-{DateTime.UtcNow:yyyy-MM-dd}.csv", csv.ToBytes(), rows.Count, total);
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
        CancellationToken ct = default,
        int? take = null)
    {
        _logger.LogDebug(
            "Searching contacts for user {UserId} with query '{Query}'",
            userId, query);

        var trimmed = query.Trim();
        var list = take is > 0
            ? await _repository.SearchAsync(userId, organizationId, trimmed, includeArchived, ct, take)
            : await _repository.SearchAsync(userId, organizationId, trimmed, includeArchived, ct);
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

        var links = await _records.CheckLinksAsync(userId, organizationId, new RecordLinks { CompanyId = request.CompanyId }, ct);
        if (links.IsFailure) return links.Error;

        // Check for duplicate email
        var emailExists = await _repository.EmailExistsAsync(request.Email.Trim(), userId, organizationId, null, ct);
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
            Description = request.Description?.Trim(),
            DoNotContact = request.DoNotContact,
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

        // Made from an older read: saving would write the form's older values over what
        // someone saved since. See UpdateContactRequest.Version.
        if (request.Version is Guid readVersion && readVersion != existing.Version)
            return DomainErrors.Contact.Changed;

        // Validate email format if provided
        if (request.Email != null && !ValidationHelper.IsValidEmail(request.Email))
        {
            _logger.LogWarning("Contact update failed: Invalid email format {Email}", request.Email);
            return DomainErrors.Contact.EmailInvalid;
        }

        if (request.CompanyId is Guid companyId && companyId != existing.CompanyId)
        {
            var links = await _records.CheckLinksAsync(userId, organizationId, new RecordLinks { CompanyId = companyId }, ct);
            if (links.IsFailure) return links.Error;
        }

        // Check for duplicate email if email is being changed
        if (request.Email != null && !string.Equals(existing.Email, request.Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var emailExists = await _repository.EmailExistsAsync(request.Email.Trim(), userId, organizationId, id, ct);
            if (emailExists)
            {
                _logger.LogWarning(
                    "Contact update failed: Duplicate email {Email}",
                    request.Email);
                return DomainErrors.Contact.DuplicateEmailWithValue(request.Email.Trim());
            }
        }

        // Apply partial updates. A field sent as null is taken off; one left out is kept.
        // Only "left out" used to be possible, so a contact who left Acme stayed linked
        // to it, and a wrong phone number could be changed but never removed.
        if (request.Name != null) existing.Name = request.Name.Trim();
        if (request.Email != null) existing.Email = request.Email.Trim();
        if (request.Phone != null) existing.Phone = request.Phone.Trim();
        else if (request.Clears(nameof(request.Phone))) existing.Phone = null;
        if (request.CompanyId.HasValue) existing.CompanyId = request.CompanyId;
        else if (request.Clears(nameof(request.CompanyId))) existing.CompanyId = null;
        if (request.JobTitle != null) existing.JobTitle = request.JobTitle.Trim();
        else if (request.Clears(nameof(request.JobTitle))) existing.JobTitle = null;
        if (request.DoNotContact.HasValue) existing.DoNotContact = request.DoNotContact.Value;
        if (request.PreferredContactMethod != null) existing.PreferredContactMethod = request.PreferredContactMethod.Trim();
        else if (request.Clears(nameof(request.PreferredContactMethod))) existing.PreferredContactMethod = null;
        if (request.Description != null) existing.Description = request.Description.Trim();
        else if (request.Clears(nameof(request.Description))) existing.Description = null;

        existing.UpdatedAtUtc = DateTime.UtcNow;
        existing.UpdatedByUserId = userId;

        Contact? updated;
        try
        {
            updated = await _repository.UpdateAsync(existing, userId, organizationId, ct);
        }
        catch (ConcurrencyConflictException)
        {
            // Saved by someone else between this request reading the contact and writing it.
            return DomainErrors.Contact.Changed;
        }
        
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

        if (await _repository.KeptByClubAsync(id, organizationId, ct) is { } kept) return DomainErrors.Contact.KeptByClub(kept);
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

        // A contact's email is unique among the workspace's active contacts. Someone may
        // have been added again under the same address since this one was archived, and
        // restoring it then broke that rule: the database refused, and the person saw
        // "failed" with no reason. Archived contacts can now be listed and restored, so
        // this is said plainly instead.
        var contact = await _repository.GetByIdAsync(id, userId, organizationId, ct);
        if (contact is { IsArchived: true }
            && await _repository.EmailExistsAsync(contact.Email, userId, organizationId, id, ct))
            return DomainErrors.Contact.UnarchiveDuplicateEmail(contact.Email.Trim());

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
        Guid userId,
        Guid? organizationId,
        Guid? excludeContactId = null,
        CancellationToken ct = default)
    {
        return await _repository.EmailExistsAsync(email, userId, organizationId, excludeContactId, ct);
    }

    public async Task<IReadOnlyList<ContactLookalikeDto>> FindLookalikesAsync(
        Guid id, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var contact = await _repository.GetByIdAsync(id, userId, organizationId, ct);
        if (contact == null || contact.IsArchived) return [];

        var others = await _repository.FindLookalikesAsync(contact, ct);
        return others.Select(other => new ContactLookalikeDto(
            other.Id,
            other.Name,
            other.Email,
            other.Phone,
            Compare(contact, other))).ToList();
    }

    public async Task<Result> MergeAsync(
        Guid keepId, Guid dropId, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var merged = await _repository.MergeIntoAsync(keepId, dropId, userId, organizationId, ct);
        if (!merged) return DomainErrors.Contact.NotFound;
        _logger.LogInformation("Merged contact {DropId} into {KeepId}", dropId, keepId);
        return Result.Success();
    }

    // "Same" as the duplicate rules read it (MatchKeys): one address in any case, one
    // number however it is written, one name with or without accents and extra spaces.
    private static IReadOnlyList<ContactFieldComparisonDto> Compare(Contact yours, Contact theirs) =>
    [
        Field("Name", yours.Name, theirs.Name, MatchKeys.Name(yours.Name) is { } name && name == MatchKeys.Name(theirs.Name)),
        Field("Email", yours.Email, theirs.Email, MatchKeys.Email(yours.Email) is { } email && email == MatchKeys.Email(theirs.Email)),
        Field("Phone", yours.Phone, theirs.Phone, MatchKeys.Phone(yours.Phone) is { } phone && phone == MatchKeys.Phone(theirs.Phone)),
    ];

    private static ContactFieldComparisonDto Field(string name, string? yours, string? theirs, bool same) =>
        new(name, string.IsNullOrWhiteSpace(yours) ? null : yours.Trim(), string.IsNullOrWhiteSpace(theirs) ? null : theirs.Trim(), same);

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
            e.PreferredContactMethod,
            e.Company?.Name,
            e.CreatedAtUtc,
            e.UpdatedAtUtc,
            e.Description,
            e.Version);
}
