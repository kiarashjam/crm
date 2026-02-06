using ACI.Domain.Entities;
using ACI.Domain.Enums;

namespace ACI.Application.Interfaces;

public interface IOrganizationRepository
{
    Task<IReadOnlyList<Organization>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> IsMemberAsync(Guid userId, Guid organizationId, CancellationToken ct = default);
    Task<OrgMemberRole?> GetMemberRoleAsync(Guid userId, Guid organizationId, CancellationToken ct = default);
    Task<Organization> CreateAsync(Organization organization, CancellationToken ct = default);
    Task AddMemberAsync(Guid organizationId, Guid userId, OrgMemberRole role, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationMember>> GetMembersAsync(Guid organizationId, CancellationToken ct = default);
    Task<OrganizationMember?> GetMemberAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
    Task UpdateMemberRoleAsync(Guid organizationId, Guid userId, OrgMemberRole role, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
    /// <summary>Set OrganizationId for all user data where OrganizationId is null (legacy backfill).</summary>
    Task BackfillUserDataToOrganizationAsync(Guid userId, Guid organizationId, CancellationToken ct = default);
    Task<Organization?> GetByApiKeyAsync(string apiKey, CancellationToken ct = default);
    Task UpdateAsync(Organization organization, CancellationToken ct = default);
}
