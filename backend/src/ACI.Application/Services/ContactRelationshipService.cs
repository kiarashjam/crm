using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <inheritdoc />
public class ContactRelationshipService : IContactRelationshipService
{
    private readonly IContactRelationshipRepository _repository;
    private readonly IContactRepository _contacts;
    private readonly ILeadRepository _leads;
    private readonly ILogger<ContactRelationshipService> _logger;

    public ContactRelationshipService(
        IContactRelationshipRepository repository,
        IContactRepository contacts,
        ILeadRepository leads,
        ILogger<ContactRelationshipService> logger)
    {
        _repository = repository;
        _contacts = contacts;
        _leads = leads;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<ContactRelationshipListDto>> ListAsync(Guid contactId, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var contact = await _contacts.GetByIdAsync(contactId, userId, organizationId, ct);
        if (contact == null) return DomainErrors.Contact.NotFound;
        return await ListOfAsync(contactId, ct);
    }

    /// <inheritdoc />
    public async Task<Result<ContactRelationshipDto>> AddAsync(
        Guid contactId, Guid userId, Guid? organizationId, SetContactRelationshipRequest request, CancellationToken ct = default)
    {
        var contact = await _contacts.GetByIdAsync(contactId, userId, organizationId, ct);
        if (contact == null) return DomainErrors.Contact.NotFound;
        var checkedRequest = await CheckAsync(contact, null, userId, organizationId, request, ct);
        if (checkedRequest.IsFailure) return checkedRequest.Error;
        var (related, type) = checkedRequest.Value;

        var relationship = new ContactRelationship
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrganizationId = organizationId,
            ContactId = contact.Id,
            RelatedContactId = related.Id,
            Type = type,
            CreatedAtUtc = DateTime.UtcNow,
        };
        await _repository.AddAsync(relationship, ct);
        _logger.LogInformation("Contact {ContactId} related to {RelatedContactId} as {Type}", contact.Id, related.Id, type);
        return Map(relationship, contact.Id, related);
    }

    /// <inheritdoc />
    public async Task<Result<ContactRelationshipDto>> UpdateAsync(
        Guid contactId, Guid relationshipId, Guid userId, Guid? organizationId, SetContactRelationshipRequest request, CancellationToken ct = default)
    {
        var contact = await _contacts.GetByIdAsync(contactId, userId, organizationId, ct);
        if (contact == null) return DomainErrors.Contact.NotFound;
        var existing = (await _repository.ForContactAsync(contactId, ct)).FirstOrDefault(r => r.Id == relationshipId);
        if (existing == null) return DomainErrors.ContactRelationship.NotFound;
        var checkedRequest = await CheckAsync(contact, existing, userId, organizationId, request, ct);
        if (checkedRequest.IsFailure) return checkedRequest.Error;
        var (related, type) = checkedRequest.Value;

        // Saved the way round it was asked, so the type reads from this contact.
        existing.ContactId = contact.Id;
        existing.RelatedContactId = related.Id;
        existing.Type = type;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.SaveAsync(ct);
        _logger.LogInformation("Relationship {RelationshipId} changed to {Type} with {RelatedContactId}", existing.Id, type, related.Id);
        return Map(existing, contact.Id, related);
    }

    /// <inheritdoc />
    public async Task<Result> RemoveAsync(Guid contactId, Guid relationshipId, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var contact = await _contacts.GetByIdAsync(contactId, userId, organizationId, ct);
        if (contact == null) return DomainErrors.Contact.NotFound;
        var existing = (await _repository.ForContactAsync(contactId, ct)).FirstOrDefault(r => r.Id == relationshipId);
        if (existing == null) return DomainErrors.ContactRelationship.NotFound;
        await _repository.RemoveAsync(existing, ct);
        _logger.LogInformation("Relationship {RelationshipId} removed", relationshipId);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<Guid?>> LeadContactIdAsync(Guid leadId, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var lead = await _leads.GetByIdAsync(leadId, userId, organizationId, ct);
        if (lead == null) return Result.Failure<Guid?>(DomainErrors.Lead.NotFound);
        // Every lead with a name and an email has a person contact; make sure this one does.
        await _contacts.EnsurePersonContactAsync(lead, ct);
        if (lead.ConvertedToContactId is Guid id && await _contacts.GetByIdAsync(id, userId, organizationId, ct) != null)
            return Result.Success<Guid?>(id);
        return Result.Success<Guid?>(null);
    }

    private async Task<ContactRelationshipListDto> ListOfAsync(Guid contactId, CancellationToken ct)
    {
        var items = (await _repository.ForContactAsync(contactId, ct))
            .Select(r => Map(r, contactId, r.ContactId == contactId ? r.RelatedContact : r.Contact))
            // The couple first, then by name.
            .OrderBy(r => ContactRelationshipTypes.IsCouple(r.Type) ? 0 : 1)
            .ThenBy(r => r.RelatedContactName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new ContactRelationshipListDto(contactId, items);
    }

    /// <summary>
    /// The other contact has to be in this workspace, active, and not this contact; the
    /// two may be related only once; and each of them has at most one spouse or partner.
    /// </summary>
    private async Task<Result<(Contact Related, string Type)>> CheckAsync(
        Contact contact, ContactRelationship? existing, Guid userId, Guid? organizationId,
        SetContactRelationshipRequest request, CancellationToken ct)
    {
        var type = ContactRelationshipTypes.Normalize(request.Type);
        if (type == null) return DomainErrors.ContactRelationship.InvalidType;
        if (request.RelatedContactId == Guid.Empty) return DomainErrors.ContactRelationship.RelatedContactRequired;
        if (request.RelatedContactId == contact.Id) return DomainErrors.ContactRelationship.IsSelf;

        var related = await _contacts.GetByIdAsync(request.RelatedContactId, userId, organizationId, ct);
        if (related == null) return DomainErrors.ContactRelationship.RelatedContactNotInWorkspace;
        var sameOther = existing != null
            && (existing.ContactId == related.Id || existing.RelatedContactId == related.Id);
        // An archived contact already on this relationship may stay; a new one may not.
        if (related.IsArchived && !sameOther) return DomainErrors.ContactRelationship.RelatedContactArchived;

        var between = await _repository.BetweenAsync(contact.Id, related.Id, ct);
        if (between != null && between.Id != existing?.Id)
            return DomainErrors.ContactRelationship.Duplicate(related.Name);

        if (ContactRelationshipTypes.IsCouple(type))
        {
            if (await _repository.HasCoupleAsync(contact.Id, existing?.Id, ct))
                return DomainErrors.ContactRelationship.CoupleDuplicate(contact.Name);
            if (await _repository.HasCoupleAsync(related.Id, existing?.Id, ct))
                return DomainErrors.ContactRelationship.CoupleDuplicate(related.Name);
        }

        return (related, type);
    }

    /// <summary>The relationship as seen from <paramref name="viewerId"/>, whose relative is <paramref name="other"/>.</summary>
    private static ContactRelationshipDto Map(ContactRelationship r, Guid viewerId, Contact other) =>
        new(
            r.Id,
            r.ContactId == viewerId ? r.Type : ContactRelationshipTypes.Inverse(r.Type),
            other.Id,
            other.Name,
            string.IsNullOrWhiteSpace(other.Email) ? null : other.Email,
            other.IsArchived,
            other.ConvertedFromLeadId,
            r.CreatedAtUtc);
}
