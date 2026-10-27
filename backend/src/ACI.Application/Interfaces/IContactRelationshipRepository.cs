using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

public interface IContactRelationshipRepository
{
    /// <summary>Every relationship either side of which is this contact, with both contacts loaded.</summary>
    Task<IReadOnlyList<ContactRelationship>> ForContactAsync(Guid contactId, CancellationToken ct = default);

    /// <summary>The relationship between two contacts, whichever way round it was recorded.</summary>
    Task<ContactRelationship?> BetweenAsync(Guid contactId, Guid otherContactId, CancellationToken ct = default);

    /// <summary>Whether the contact has a spouse or partner, other than in <paramref name="exceptId"/>.</summary>
    Task<bool> HasCoupleAsync(Guid contactId, Guid? exceptId, CancellationToken ct = default);

    Task AddAsync(ContactRelationship relationship, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
    Task RemoveAsync(ContactRelationship relationship, CancellationToken ct = default);
}
