using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Domain.Enums;

namespace ACI.Application.Interfaces;

/// <summary>
/// Service for managing organizations and their members.
/// </summary>
public interface IOrganizationService
{
    /// <summary>
    /// Lists all organizations the user is a member of.
    /// </summary>
    /// <param name="userId">The user's ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of organizations.</returns>
    Task<IReadOnlyList<OrganizationDto>> ListMyOrganizationsAsync(Guid userId, CancellationToken ct = default);
    
    /// <summary>
    /// Gets an organization by ID if the user is a member.
    /// </summary>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="userId">The user's ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The organization if found and user is a member.</returns>
    Task<Result<OrganizationDto>> GetOrganizationAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
    
    /// <summary>
    /// Creates a new organization with the user as owner.
    /// </summary>
    /// <param name="userId">The user's ID (becomes owner).</param>
    /// <param name="request">Organization creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created organization.</returns>
    Task<Result<OrganizationDto>> CreateOrganizationAsync(Guid userId, CreateOrganizationRequest request, CancellationToken ct = default);
    
    /// <summary>
    /// Gets all members of an organization.
    /// </summary>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="userId">The requesting user's ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of organization members.</returns>
    Task<Result<IReadOnlyList<OrgMemberDto>>> GetMembersAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
    
    /// <summary>
    /// Updates a member's role (owner only).
    /// </summary>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="requestingUserId">The user making the request.</param>
    /// <param name="memberUserId">The member to update.</param>
    /// <param name="newRole">The new role.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or failure result.</returns>
    /// <summary>
    /// Adds an existing registered user to the organization with the given role, without an
    /// invitation. Takes managing the team. Lets an administrator bring somebody in directly when
    /// waiting on an invitation email is not wanted — the person must already have an account,
    /// because a membership needs a user to point at.
    /// </summary>
    Task<Result<OrgMemberDto>> AddMemberByEmailAsync(Guid organizationId, Guid requestingUserId, string email, OrgMemberRole role, CancellationToken ct = default, string? preset = null);

    /// <summary>Every permission and preset, as the Team page offers them.</summary>
    PermissionCatalogueDto GetPermissionCatalogue();

    /// <summary>
    /// Sets what a member may do: a preset (anyone who manages the team, up to what they
    /// can do themselves), or their own list of permissions (the owner only).
    /// </summary>
    Task<Result<OrgMemberDto>> SetMemberPermissionsAsync(Guid organizationId, Guid requestingUserId, Guid memberUserId, SetMemberPermissionsRequest request, CancellationToken ct = default);

    Task<Result> UpdateMemberRoleAsync(Guid organizationId, Guid requestingUserId, Guid memberUserId, OrgMemberRole newRole, CancellationToken ct = default);
    
    /// <summary>
    /// Removes a member from the organization (takes managing the team), and hands their open
    /// leads, deals and tasks to another member: <paramref name="handOverToUserId"/>, or
    /// the organization's owner when none is given.
    /// </summary>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="requestingUserId">The user making the request.</param>
    /// <param name="memberUserId">The member to remove.</param>
    /// <param name="handOverToUserId">Who takes over their work: a member who can work records.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Who took over and how much, or why nothing was done.</returns>
    Task<Result<MemberRemovedDto>> RemoveMemberAsync(Guid organizationId, Guid requestingUserId, Guid memberUserId, Guid? handOverToUserId = null, CancellationToken ct = default);
    
    /// <summary>
    /// Gets an organization by id (no membership check; used for public webhook resolution).
    /// </summary>
    Task<Domain.Entities.Organization?> GetByIdUnauthenticatedAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Gets webhook configuration for an organization.
    /// </summary>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="userId">The requesting user's ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Webhook info if user is a member.</returns>
    Task<Result<WebhookInfoDto>> GetWebhookInfoAsync(Guid organizationId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Sets or clears the JSON webhook password (takes managing settings). Empty clears to the app default.
    /// </summary>
    Task<Result> UpdateWebhookPasswordAsync(Guid organizationId, Guid userId, string? newPassword, CancellationToken ct = default);
    Task<Result> UpdateOutboundWebhookAsync(Guid organizationId, Guid userId, string? url, string? secret, CancellationToken ct = default);

    /// <summary>
    /// Sets the workspace's currency, locale and time zone (takes managing settings). A field
    /// left null keeps its value; one that is not a code, a locale or a zone this host
    /// knows is refused and nothing is changed.
    /// </summary>
    Task<Result<OrganizationDto>> UpdateSettingsAsync(Guid organizationId, Guid userId, UpdateOrganizationSettingsRequest request, CancellationToken ct = default);

    /// <summary>
    /// Generates a new webhook API key (takes managing settings). Returned once; only its hash is kept.
    /// </summary>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="userId">The requesting user's ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new API key.</returns>
    Task<Result<string>> GenerateWebhookApiKeyAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
    
    /// <summary>
    /// Gets an organization by its webhook API key.
    /// </summary>
    /// <param name="apiKey">The API key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The organization if found.</returns>
    Task<Domain.Entities.Organization?> GetByApiKeyAsync(string apiKey, CancellationToken ct = default);

    /// <summary>A new MCP key for the workspace (takes managing settings). Returned once; only its hash is kept.</summary>
    Task<Result<string>> GenerateMcpApiKeyAsync(Guid organizationId, Guid userId, CancellationToken ct = default);

    /// <summary>The workspace an MCP key belongs to, if any.</summary>
    Task<Domain.Entities.Organization?> GetByMcpApiKeyAsync(string apiKey, CancellationToken ct = default);
}
