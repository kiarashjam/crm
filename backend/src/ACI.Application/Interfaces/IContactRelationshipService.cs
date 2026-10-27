using ACI.Application.Common;
using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

/// <summary>
/// How contacts are related to each other: spouses and partners (for the couples
/// package), parents and children, siblings, and so on.
/// </summary>
public interface IContactRelationshipService
{
    Task<Result<ContactRelationshipListDto>> ListAsync(Guid contactId, Guid userId, Guid? organizationId, CancellationToken ct = default);

    Task<Result<ContactRelationshipDto>> AddAsync(Guid contactId, Guid userId, Guid? organizationId, SetContactRelationshipRequest request, CancellationToken ct = default);

    Task<Result<ContactRelationshipDto>> UpdateAsync(Guid contactId, Guid relationshipId, Guid userId, Guid? organizationId, SetContactRelationshipRequest request, CancellationToken ct = default);

    Task<Result> RemoveAsync(Guid contactId, Guid relationshipId, Guid userId, Guid? organizationId, CancellationToken ct = default);

    /// <summary>
    /// The contact a lead's relationships hang off: the person contact every lead with a
    /// name and an email has. Null when the lead has none.
    /// </summary>
    Task<Result<Guid?>> LeadContactIdAsync(Guid leadId, Guid userId, Guid? organizationId, CancellationToken ct = default);
}
