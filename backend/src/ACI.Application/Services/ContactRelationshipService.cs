using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <inheritdoc />
/// <remarks>
/// <para>
/// Two kinds of tie, one place each. People who share a household (a couple, and the children,
/// parents or siblings who live with them) are a household: IClubHouseholds holds them, with
/// the club's primary and roll-up billing, and a tie between two of them is read from their
/// roles. Kin who live apart, friends and colleagues are relationships between two contacts,
/// held here, one row per pair. A spouse or partner recorded on a contact's page makes or joins
/// a household; any other family tie between two people of one household changes their roles.
/// Family used to be recorded twice, as a contact relationship and as a household role, and the
/// two disagreed.
/// </para>
/// <para>
/// In a personal workspace (no organization) there are no households, and every tie is a row.
/// </para>
/// </remarks>
public class ContactRelationshipService : IContactRelationshipService
{
    private readonly IContactRelationshipRepository _repository;
    private readonly IContactRepository _contacts;
    private readonly ILeadRepository _leads;
    private readonly ILogger<ContactRelationshipService> _logger;
    private readonly IClubHouseholds? _households;

    public ContactRelationshipService(
        IContactRelationshipRepository repository,
        IContactRepository contacts,
        ILeadRepository leads,
        ILogger<ContactRelationshipService> logger,
        IClubHouseholds? households = null)
    {
        _repository = repository;
        _contacts = contacts;
        _leads = leads;
        _logger = logger;
        _households = households;
    }

    /// <inheritdoc />
    public async Task<Result<ContactRelationshipListDto>> ListAsync(Guid contactId, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var contact = await _contacts.GetByIdAsync(contactId, userId, organizationId, ct);
        if (contact == null) return DomainErrors.Contact.NotFound;
        return await ListOfAsync(contactId, organizationId, ct);
    }

    /// <inheritdoc />
    public async Task<Result<ContactRelationshipDto>> AddAsync(
        Guid contactId, Guid userId, Guid? organizationId, SetContactRelationshipRequest request, CancellationToken ct = default)
    {
        var contact = await _contacts.GetByIdAsync(contactId, userId, organizationId, ct);
        if (contact == null) return DomainErrors.Contact.NotFound;

        if (await HouseholdTieAsync(contact, null, userId, organizationId, request, ct) is { } householdTie)
            return householdTie;

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
        if (existing == null)
        {
            // A tie read from a household: its id is the household link that holds it.
            if (organizationId is not { } org || _households == null) return DomainErrors.ContactRelationship.NotFound;
            var household = await _households.ForContactAsync(org, contactId, ct);
            if (household == null || household.People.All(p => p.LinkId != relationshipId)) return DomainErrors.ContactRelationship.NotFound;
            var type = ContactRelationshipTypes.Normalize(request.Type);
            if (type == null) return DomainErrors.ContactRelationship.InvalidType;
            var related = await _contacts.GetByIdAsync(request.RelatedContactId, userId, organizationId, ct);
            if (related == null) return DomainErrors.ContactRelationship.RelatedContactNotInWorkspace;
            var tie = await _households.RelateContactsAsync(org, userId, contact.Id, related.Id, type, ct);
            if (tie.IsFailure) return tie.Error;
            return await TieDtoAsync(contact.Id, org, tie.Value, related.Id, ct);
        }

        // A row that becomes a household tie (a spouse, or kin in the same household) is replaced by it.
        if (await HouseholdTieAsync(contact, existing, userId, organizationId, request, ct) is { } householdTie)
            return householdTie;

        var checkedRequest = await CheckAsync(contact, existing, userId, organizationId, request, ct);
        if (checkedRequest.IsFailure) return checkedRequest.Error;
        var (relatedContact, newType) = checkedRequest.Value;

        // Saved the way round it was asked, so the type reads from this contact.
        existing.ContactId = contact.Id;
        existing.RelatedContactId = relatedContact.Id;
        existing.Type = newType;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.SaveAsync(ct);
        _logger.LogInformation("Relationship {RelationshipId} changed to {Type} with {RelatedContactId}", existing.Id, newType, relatedContact.Id);
        return Map(existing, contact.Id, relatedContact);
    }

    /// <inheritdoc />
    public async Task<Result> RemoveAsync(Guid contactId, Guid relationshipId, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var contact = await _contacts.GetByIdAsync(contactId, userId, organizationId, ct);
        if (contact == null) return DomainErrors.Contact.NotFound;
        var existing = (await _repository.ForContactAsync(contactId, ct)).FirstOrDefault(r => r.Id == relationshipId);
        if (existing == null)
        {
            // A household tie ends by one of the two leaving the household (never its primary).
            if (organizationId is not { } org || _households == null) return DomainErrors.ContactRelationship.NotFound;
            var removed = await _households.UnrelateAsync(org, contactId, relationshipId, ct);
            if (removed.IsFailure) return removed.Error;
            _logger.LogInformation("Household tie {LinkId} ended from contact {ContactId}", relationshipId, contactId);
            return Result.Success();
        }
        await _repository.RemoveAsync(existing, ct);
        _logger.LogInformation("Relationship {RelationshipId} removed", relationshipId);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result<LeadContact>> LeadContactAsync(Guid leadId, Guid userId, Guid? organizationId, bool ensure, CancellationToken ct = default)
    {
        var lead = await _leads.GetByIdAsync(leadId, userId, organizationId, ct);
        if (lead == null) return Result.Failure<LeadContact>(DomainErrors.Lead.NotFound);
        // Every lead with a name and an email has a person contact; a write makes sure this
        // one does. Opening the lead page used to do it too, so a Viewer's visit created
        // contacts, and a contact someone had deleted came back as a new, empty one.
        if (ensure)
            await _contacts.EnsurePersonContactAsync(lead, ct);
        var contact = await _contacts.GetPersonContactAsync(lead, userId, organizationId, ct);
        return Result.Success(new LeadContact(contact?.Id, contact == null && lead.PersonContactRemovedAtUtc != null));
    }

    private async Task<ContactRelationshipListDto> ListOfAsync(Guid contactId, Guid? organizationId, CancellationToken ct)
    {
        var items = SeenFrom(await _repository.ForContactAsync(contactId, ct), contactId);
        var household = organizationId is { } org && _households != null ? await _households.ForContactAsync(org, contactId, ct) : null;
        return new ContactRelationshipListDto(contactId, WithHousehold(items, household, contactId));
    }

    /// <summary>
    /// Records the request as a household tie when it is one: a spouse or partner, or any family
    /// tie between two people of one household. Null when it is a relationship row instead. A row
    /// it replaces (<paramref name="existing"/>, or one between the two) is removed: the tie is
    /// the household's now, recorded once.
    /// </summary>
    private async Task<Result<ContactRelationshipDto>?> HouseholdTieAsync(
        Contact contact, ContactRelationship? existing, Guid userId, Guid? organizationId, SetContactRelationshipRequest request, CancellationToken ct)
    {
        if (organizationId is not { } org || _households == null) return null;
        var type = ContactRelationshipTypes.Normalize(request.Type);
        if (type == null) return DomainErrors.ContactRelationship.InvalidType;
        if (request.RelatedContactId == Guid.Empty) return DomainErrors.ContactRelationship.RelatedContactRequired;
        if (request.RelatedContactId == contact.Id) return DomainErrors.ContactRelationship.IsSelf;
        var related = await _contacts.GetByIdAsync(request.RelatedContactId, userId, organizationId, ct);
        if (related == null) return DomainErrors.ContactRelationship.RelatedContactNotInWorkspace;

        var mine = await _households.ForContactAsync(org, contact.Id, ct);
        var sameHousehold = mine != null && mine.People.Any(p => p.ContactId == related.Id);
        if (!ContactRelationshipTypes.IsCouple(type) && !sameHousehold) return null;
        var sameOther = existing != null && (existing.ContactId == related.Id || existing.RelatedContactId == related.Id);
        if (related.IsArchived && !sameOther && !sameHousehold) return DomainErrors.ContactRelationship.RelatedContactArchived;

        // A spouse or partner still recorded as a row from before couples moved into households
        // counts too: nobody gets a second one.
        if (ContactRelationshipTypes.IsCouple(type) && !sameHousehold)
        {
            if (await _repository.HasCoupleAsync(contact.Id, existing?.Id, ct)) return DomainErrors.ContactRelationship.CoupleDuplicate(contact.Name);
            if (await _repository.HasCoupleAsync(related.Id, existing?.Id, ct)) return DomainErrors.ContactRelationship.CoupleDuplicate(related.Name);
        }

        var tie = await _households.RelateContactsAsync(org, userId, contact.Id, related.Id, type, ct);
        if (tie.IsFailure) return tie.Error;
        var replaced = existing ?? await _repository.BetweenAsync(contact.Id, related.Id, ct);
        if (replaced != null) await _repository.RemoveAsync(replaced, ct);
        _logger.LogInformation("Contact {ContactId} and {RelatedContactId} tied in a household as {Type}", contact.Id, related.Id, type);
        return await TieDtoAsync(contact.Id, org, tie.Value, related.Id, ct);
    }

    private async Task<Result<ContactRelationshipDto>> TieDtoAsync(Guid contactId, Guid organizationId, Guid linkId, Guid relatedContactId, CancellationToken ct)
    {
        var list = await ListOfAsync(contactId, organizationId, ct);
        var item = list.Items.FirstOrDefault(i => i.Id == linkId) ?? list.Items.FirstOrDefault(i => i.RelatedContactId == relatedContactId);
        return item == null ? DomainErrors.ContactRelationship.NotFound : item;
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

    /// <summary>
    /// The relationships of <paramref name="viewerId"/> among <paramref name="rows"/> (both
    /// contacts loaded), as seen from that contact: the couple first, then by name.
    /// </summary>
    internal static IReadOnlyList<ContactRelationshipDto> SeenFrom(IEnumerable<ContactRelationship> rows, Guid viewerId) =>
        rows
            .Where(r => r.ContactId == viewerId || r.RelatedContactId == viewerId)
            .Select(r => Map(r, viewerId, r.ContactId == viewerId ? r.RelatedContact : r.Contact))
            .OrderBy(r => ContactRelationshipTypes.IsCouple(r.Type) ? 0 : 1)
            .ThenBy(r => r.RelatedContactName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// <paramref name="rows"/> with <paramref name="contactId"/>'s household folded in: the
    /// people they share a household with, as the household says they are related, in place of
    /// any row between them (the household is the record of people who live together). Only
    /// people with a contact are listed; the household page shows everyone.
    /// </summary>
    internal static IReadOnlyList<ContactRelationshipDto> WithHousehold(IReadOnlyList<ContactRelationshipDto> rows, HouseholdDto? household, Guid contactId)
    {
        var me = household?.People.FirstOrDefault(p => p.ContactId == contactId);
        if (household == null || me == null) return rows;
        var housemates = household.People.Where(p => p.LinkId != me.LinkId && p.ContactId != null && p.ContactId != contactId).ToList();
        var shared = housemates.Select(p => p.ContactId!.Value).ToHashSet();
        return rows.Where(r => !shared.Contains(r.RelatedContactId))
            .Concat(housemates.Select(p => new ContactRelationshipDto(
                TieId(me, p),
                TieType(me, p),
                p.ContactId!.Value,
                p.Name,
                p.Email,
                p.IsArchived,
                p.LeadId,
                p.AddedAtUtc,
                household.Id,
                household.Name)))
            .OrderBy(r => ContactRelationshipTypes.IsCouple(r.Type) ? 0 : 1)
            .ThenBy(r => r.RelatedContactName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>The household link that holds the tie between two people: the one of them who is not the primary.</summary>
    private static Guid TieId(HouseholdPersonDto me, HouseholdPersonDto other) => me.IsPrimary ? other.LinkId : other.IsPrimary ? me.LinkId : other.LinkId;

    /// <summary>What <paramref name="other"/> is to <paramref name="me"/>, from their roles in the household (each a relation to its primary).</summary>
    internal static string TieType(HouseholdPersonDto me, HouseholdPersonDto other)
    {
        if (me.IsPrimary) return TypeOf(other.Role);
        if (other.IsPrimary) return ContactRelationshipTypes.Inverse(TypeOf(me.Role));
        var mine = TypeOf(me.Role);
        var theirs = TypeOf(other.Role);
        return (mine, theirs) switch
        {
            // The primary's partner, and the primary's child: their child too.
            (ContactRelationshipTypes.Spouse or ContactRelationshipTypes.Partner, ContactRelationshipTypes.Child) => ContactRelationshipTypes.Child,
            (ContactRelationshipTypes.Child, ContactRelationshipTypes.Spouse or ContactRelationshipTypes.Partner) => ContactRelationshipTypes.Parent,
            (ContactRelationshipTypes.Child, ContactRelationshipTypes.Child) => ContactRelationshipTypes.Sibling,
            (ContactRelationshipTypes.Sibling, ContactRelationshipTypes.Sibling) => ContactRelationshipTypes.Sibling,
            _ => ContactRelationshipTypes.Family,
        };
    }

    private static string TypeOf(string role) => role switch
    {
        HouseholdRoles.Spouse => ContactRelationshipTypes.Spouse,
        HouseholdRoles.Partner => ContactRelationshipTypes.Partner,
        HouseholdRoles.Child or HouseholdRoles.Dependent => ContactRelationshipTypes.Child,
        HouseholdRoles.Parent => ContactRelationshipTypes.Parent,
        HouseholdRoles.Sibling => ContactRelationshipTypes.Sibling,
        _ => ContactRelationshipTypes.Family,
    };

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
