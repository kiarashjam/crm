using ACI.Domain.Entities;
using ACI.Domain.Enums;

namespace ACI.Application.Interfaces;

/// <summary>The open work a member who left handed over: the leads' and deals' ids and names, and the tasks.</summary>
public sealed record MemberHandOver(
    IReadOnlyList<(Guid Id, string Name)> Leads,
    IReadOnlyList<(Guid Id, string Name)> Deals,
    IReadOnlyList<TaskItem> Tasks)
{
    public static readonly MemberHandOver Nothing = new([], [], []);
}

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

    /// <summary>
    /// Removes a member and, in the same transaction, hands their open work in the
    /// organization to <paramref name="handOverToUserId"/>: the open leads and deals they
    /// work and the open tasks that are theirs. Returns what was handed over; nothing when
    /// they were not a member.
    /// </summary>
    Task<MemberHandOver> RemoveMemberAsync(Guid organizationId, Guid userId, Guid handOverToUserId, Guid actingUserId, CancellationToken ct = default);
    /// <summary>Set OrganizationId for all user data where OrganizationId is null (legacy backfill).</summary>
    Task BackfillUserDataToOrganizationAsync(Guid userId, Guid organizationId, CancellationToken ct = default);
    Task<Organization?> GetByWebhookApiKeyHashAsync(string keyHash, CancellationToken ct = default);
    Task<Organization?> GetByMcpApiKeyHashAsync(string keyHash, CancellationToken ct = default);
    Task UpdateAsync(Organization organization, CancellationToken ct = default);
}
