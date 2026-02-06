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
    Task<Result> UpdateMemberRoleAsync(Guid organizationId, Guid requestingUserId, Guid memberUserId, OrgMemberRole newRole, CancellationToken ct = default);
    
    /// <summary>
    /// Removes a member from the organization (owner only).
    /// </summary>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="requestingUserId">The user making the request.</param>
    /// <param name="memberUserId">The member to remove.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or failure result.</returns>
    Task<Result> RemoveMemberAsync(Guid organizationId, Guid requestingUserId, Guid memberUserId, CancellationToken ct = default);
    
    /// <summary>
    /// Gets webhook configuration for an organization.
    /// </summary>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="userId">The requesting user's ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Webhook info if user is a member.</returns>
    Task<Result<WebhookInfoDto>> GetWebhookInfoAsync(Guid organizationId, Guid userId, CancellationToken ct = default);
    
    /// <summary>
    /// Generates a new webhook API key (owner/manager only).
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
}
