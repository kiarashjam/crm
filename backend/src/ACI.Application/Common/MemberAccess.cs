using ACI.Application.Interfaces;
using ACI.Domain.Common;

namespace ACI.Application.Common;

/// <summary>What a member may do in an organization (see <see cref="MemberPermissions"/>), read fresh from their membership.</summary>
public static class MemberAccess
{
    /// <summary>What <paramref name="userId"/> may do in the organization; null when they are not a member.</summary>
    public static async Task<IReadOnlySet<string>?> PermissionsAsync(
        this IOrganizationRepository organizations, Guid userId, Guid organizationId, CancellationToken ct = default)
    {
        var member = await organizations.GetMemberAsync(organizationId, userId, ct);
        return member == null ? null : MemberPermissions.Effective(member.Role, member.PermissionPreset, member.Permissions);
    }

    /// <summary>Whether <paramref name="userId"/> is a member who holds <paramref name="permission"/>.</summary>
    public static async Task<bool> HasPermissionAsync(
        this IOrganizationRepository organizations, Guid userId, Guid organizationId, string permission, CancellationToken ct = default) =>
        (await organizations.PermissionsAsync(userId, organizationId, ct))?.Contains(permission) == true;

    /// <summary>
    /// Whether <paramref name="userId"/> is a member who holds every one of
    /// <paramref name="permissions"/>: what someone who manages the team may hand out, or
    /// may take from a member, is no more than they can do themselves.
    /// </summary>
    public static async Task<bool> HoldsAllAsync(
        this IOrganizationRepository organizations, Guid userId, Guid organizationId, IEnumerable<string> permissions, CancellationToken ct = default)
    {
        var mine = await organizations.PermissionsAsync(userId, organizationId, ct);
        return mine != null && permissions.All(mine.Contains);
    }

    /// <summary>What a member may do, read from their membership.</summary>
    public static IReadOnlySet<string> PermissionsOf(Domain.Entities.OrganizationMember member) =>
        MemberPermissions.Effective(member.Role, member.PermissionPreset, member.Permissions);
}

/// <summary>
/// What the signed-in caller may do in the request's workspace, read once per request.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// What <paramref name="userId"/> may do: in a personal workspace (no organization)
    /// everything, since it is all their own; in an organization what their membership
    /// allows; empty when they are not a member.
    /// </summary>
    Task<IReadOnlySet<string>> PermissionsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
}

/// <inheritdoc cref="IPermissionService"/>
public sealed class PermissionService : IPermissionService
{
    private static readonly IReadOnlySet<string> Everything = MemberPermissions.All.Select(p => p.Key).ToHashSet(StringComparer.Ordinal);
    private static readonly IReadOnlySet<string> Nothing = new HashSet<string>();

    private readonly IOrganizationRepository _organizations;
    private readonly Dictionary<(Guid, Guid), IReadOnlySet<string>> _read = new();

    public PermissionService(IOrganizationRepository organizations) => _organizations = organizations;

    public async Task<IReadOnlySet<string>> PermissionsAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        if (organizationId is not Guid org) return Everything;
        if (_read.TryGetValue((userId, org), out var known)) return known;
        var held = await _organizations.PermissionsAsync(userId, org, ct) ?? Nothing;
        _read[(userId, org)] = held;
        return held;
    }
}
