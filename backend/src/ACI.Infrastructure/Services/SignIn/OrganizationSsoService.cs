using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Common;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services.SignIn;

/// <summary>
/// A workspace's single sign-on requirement: the owner's setting, and the check every request
/// into the workspace passes (see <see cref="IOrganizationSso"/>).
/// </summary>
/// <remarks>
/// <para>
/// The requirement is the workspace's, not the account's. Accounts are shared between
/// workspaces, and anyone who manages a team can add an existing account to theirs, so a
/// rule that refused the password at sign-in would let one workspace's owner shut a person
/// out of every other workspace they belong to, and of their own account. Instead the
/// workspace refuses, request by request, sessions that did not begin with single sign-on
/// (the session token says how it began, SignInMethods); the same session goes on working
/// elsewhere.
/// </para>
/// <para>
/// The check runs on every request into a workspace, so the policy and each person's address
/// are cached for half a minute. This instance forgets a policy the moment it changes; another
/// instance of a scaled-out API follows within that half minute, as with security stamps.
/// </para>
/// </remarks>
public sealed class OrganizationSsoService : IOrganizationSso
{
    public static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(30);

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ISingleSignOn _sso;
    private readonly ILogger<OrganizationSsoService> _logger;

    public OrganizationSsoService(AppDbContext db, IMemoryCache cache, ISingleSignOn sso, ILogger<OrganizationSsoService> logger)
    {
        _db = db;
        _cache = cache;
        _sso = sso;
        _logger = logger;
    }

    private sealed record Policy(string Name, Guid OwnerUserId, bool Required, string? Domains);

    public static string PolicyKey(Guid organizationId) => "sso-policy:" + organizationId;
    private static string EmailKey(Guid userId) => "sso-email:" + userId;

    public async Task<Result<OrganizationSsoDto>> GetAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        var access = await AccessAsync(organizationId, userId, ct);
        if (access.Error is { } error) return error;
        return await ViewAsync(organizationId, access.IsOwner, ct);
    }

    public async Task<Result<OrganizationSsoDto>> UpdateAsync(Guid organizationId, Guid userId, UpdateOrganizationSsoRequest request, CancellationToken ct = default)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct);
        if (org == null) return DomainErrors.Organization.NotFound;
        if (!await _db.OrganizationMembers.AnyAsync(m => m.OrganizationId == organizationId && m.UserId == userId, ct))
            return DomainErrors.Organization.NotMember;
        // Who may work here at all is the owner's to decide, like who owns the workspace.
        if (org.OwnerUserId != userId) return DomainErrors.Organization.NotOwner;

        var domains = org.SsoDomains;
        if (request.Domains != null)
        {
            var normalized = SsoRequirement.Normalize(request.Domains);
            if (normalized.IsFailure) return normalized.Error;
            domains = normalized.Value;
        }
        var required = request.Required ?? org.SsoRequired;
        // With no provider on this server, nobody the requirement holds could open the workspace.
        if (required && !org.SsoRequired && _sso.Providers.Count == 0) return DomainErrors.Sso.NoProvider;

        var changed = org.SsoRequired != required || org.SsoDomains != domains;
        org.SsoRequired = required;
        org.SsoDomains = domains;
        if (changed)
        {
            await _db.SaveChangesAsync(ct);
            _cache.Remove(PolicyKey(organizationId));
            _logger.LogInformation(
                "User {UserId} set the single sign-on requirement of organization {OrganizationId}: required {Required}, domains {Domains}",
                userId, organizationId, required, domains ?? "(every member)");
        }
        return await ViewAsync(organizationId, isOwner: true, ct);
    }

    public async Task<bool> AdmitsAsync(Guid organizationId, Guid userId, string signInMethod, CancellationToken ct = default) =>
        await RefusalAsync(organizationId, userId, signInMethod, ct) == null;

    /// <summary>The workspace's name when it refuses the session; null when it admits it.</summary>
    public async Task<string?> RefusalAsync(Guid organizationId, Guid userId, string signInMethod, CancellationToken ct = default)
    {
        // The usual case costs nothing: a single sign-on session meets every requirement.
        if (signInMethod == SignInMethods.Sso) return null;
        var policy = await PolicyAsync(organizationId, ct);
        if (policy == null || !policy.Required || policy.OwnerUserId == userId) return null;
        var holds = SsoRequirement.Holds(policy.Required, policy.Domains, isOwner: false, await EmailAsync(userId, ct) ?? "");
        return holds ? policy.Name : null;
    }

    public async Task<IReadOnlySet<Guid>> RefusingAsync(IReadOnlyCollection<Guid> organizationIds, Guid userId, string signInMethod, CancellationToken ct = default)
    {
        var refusing = new HashSet<Guid>();
        if (signInMethod == SignInMethods.Sso) return refusing;
        foreach (var id in organizationIds)
            if (await RefusalAsync(id, userId, signInMethod, ct) != null) refusing.Add(id);
        return refusing;
    }

    private async Task<Policy?> PolicyAsync(Guid organizationId, CancellationToken ct)
    {
        if (_cache.TryGetValue(PolicyKey(organizationId), out Policy? cached)) return cached;
        // Organizations are not filtered to a workspace (they are how a person finds theirs).
        var policy = await _db.Organizations.AsNoTracking()
            .Where(o => o.Id == organizationId)
            .Select(o => new Policy(o.Name, o.OwnerUserId, o.SsoRequired, o.SsoDomains))
            .FirstOrDefaultAsync(ct);
        _cache.Set(PolicyKey(organizationId), policy, CacheFor);
        return policy;
    }

    private async Task<string?> EmailAsync(Guid userId, CancellationToken ct)
    {
        if (_cache.TryGetValue(EmailKey(userId), out string? cached)) return cached;
        var email = await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync(ct);
        _cache.Set(EmailKey(userId), email, CacheFor);
        return email;
    }

    /// <summary>Who may see the setting: the owner, and those who manage the workspace's settings or team.</summary>
    private async Task<(bool IsOwner, Error? Error)> AccessAsync(Guid organizationId, Guid userId, CancellationToken ct)
    {
        var org = await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => new { o.OwnerUserId }).FirstOrDefaultAsync(ct);
        if (org == null) return (false, DomainErrors.Organization.NotFound);
        var member = await _db.OrganizationMembers.AsNoTracking().FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId, ct);
        if (member == null) return (false, DomainErrors.Organization.NotMember);
        if (org.OwnerUserId == userId) return (true, null);
        var held = MemberPermissions.Effective(member.Role, member.PermissionPreset, member.Permissions);
        if (!held.Contains(MemberPermissions.ManageSettings) && !held.Contains(MemberPermissions.ManageMembers))
            return (false, DomainErrors.Permission.Missing(MemberPermissions.ManageSettings, "see how the team signs in"));
        return (false, null);
    }

    private async Task<OrganizationSsoDto> ViewAsync(Guid organizationId, bool isOwner, CancellationToken ct)
    {
        var org = await _db.Organizations.AsNoTracking().FirstAsync(o => o.Id == organizationId, ct);
        var members = await _db.OrganizationMembers.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId)
            .Select(m => new { m.UserId, m.User.Name, m.User.Email })
            .ToListAsync(ct);
        var ids = members.Select(m => m.UserId).ToList();
        var linked = (await _db.UserExternalLogins.AsNoTracking().Where(l => ids.Contains(l.UserId)).Select(l => l.UserId).Distinct().ToListAsync(ct)).ToHashSet();

        return new OrganizationSsoDto(
            org.SsoRequired,
            SsoRequirement.Parse(org.SsoDomains),
            _sso.Providers,
            CanChange: isOwner,
            members
                .OrderBy(m => m.Name)
                .Select(m =>
                {
                    var owner = m.UserId == org.OwnerUserId;
                    return new OrganizationSsoMemberDto(m.UserId, m.Name, m.Email, owner,
                        Applies: SsoRequirement.Holds(true, org.SsoDomains, owner, m.Email),
                        HasSignedInWithSso: linked.Contains(m.UserId));
                })
                .ToList());
    }
}
