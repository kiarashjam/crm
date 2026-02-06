using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace ACI.Application.Services;

/// <summary>
/// Service for managing organizations and their members.
/// </summary>
public sealed class OrganizationService : IOrganizationService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ILeadStatusRepository _leadStatusRepository;
    private readonly ILeadSourceRepository _leadSourceRepository;
    private readonly ILogger<OrganizationService> _logger;

    public OrganizationService(
        IOrganizationRepository organizationRepository, 
        ILeadStatusRepository leadStatusRepository, 
        ILeadSourceRepository leadSourceRepository,
        ILogger<OrganizationService> logger)
    {
        _organizationRepository = organizationRepository;
        _leadStatusRepository = leadStatusRepository;
        _leadSourceRepository = leadSourceRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<OrganizationDto>> ListMyOrganizationsAsync(Guid userId, CancellationToken ct = default)
    {
        _logger.LogDebug("Listing organizations for user {UserId}", userId);
        
        var orgs = await _organizationRepository.GetByUserIdAsync(userId, ct);
        
        _logger.LogDebug("Found {Count} organizations for user {UserId}", orgs.Count, userId);
        return orgs.Select(o => new OrganizationDto(o.Id, o.Name, o.OwnerUserId, o.OwnerUserId == userId)).ToList();
    }

    public async Task<Result<OrganizationDto>> GetOrganizationAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        _logger.LogDebug("Getting organization {OrganizationId} for user {UserId}", organizationId, userId);
        
        var isMember = await _organizationRepository.IsMemberAsync(userId, organizationId, ct);
        if (!isMember)
        {
            _logger.LogWarning("User {UserId} is not a member of organization {OrganizationId}", userId, organizationId);
            return DomainErrors.Organization.NotMember;
        }
        
        var org = await _organizationRepository.GetByIdAsync(organizationId, ct);
        if (org == null)
        {
            _logger.LogWarning("Organization {OrganizationId} not found", organizationId);
            return DomainErrors.Organization.NotFound;
        }
        
        _logger.LogDebug("Successfully retrieved organization {OrganizationId}", organizationId);
        return new OrganizationDto(org.Id, org.Name, org.OwnerUserId, org.OwnerUserId == userId);
    }

    public async Task<Result<OrganizationDto>> CreateOrganizationAsync(Guid userId, CreateOrganizationRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating organization for user {UserId}", userId);
        
        try
        {
            var org = new Organization
            {
                Id = Guid.NewGuid(),
                Name = string.IsNullOrWhiteSpace(request.Name) ? "My Organization" : request.Name.Trim(),
                OwnerUserId = userId,
                CreatedAtUtc = DateTime.UtcNow,
            };
            
            await _organizationRepository.CreateAsync(org, ct);
            await _organizationRepository.AddMemberAsync(org.Id, userId, OrgMemberRole.Owner, ct);
            await _organizationRepository.BackfillUserDataToOrganizationAsync(userId, org.Id, ct);
            
            // Seed default Lead Statuses
            var defaultStatuses = new[] { 
                "New", "Open", "Attempted Contact", "Contacted", "Connected", 
                "In Progress", "Qualified", "Unqualified", "Open Deal", "Lost" 
            };
            for (int i = 0; i < defaultStatuses.Length; i++)
            {
                var status = new LeadStatus
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = org.Id,
                    Name = defaultStatuses[i],
                    DisplayOrder = i,
                };
                await _leadStatusRepository.AddAsync(status, ct);
            }
            
            // Seed default Lead Sources
            var defaultSources = new[] { 
                "Website", "Referral", "Social Media", "Paid Search", "Email Campaign",
                "Cold Call", "Events", "Partner", "LinkedIn", "Manual" 
            };
            for (int i = 0; i < defaultSources.Length; i++)
            {
                var source = new LeadSource
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = org.Id,
                    Name = defaultSources[i],
                    DisplayOrder = i,
                };
                await _leadSourceRepository.AddAsync(source, ct);
            }
            
            _logger.LogInformation("Successfully created organization {OrganizationId} for user {UserId}", org.Id, userId);
            return new OrganizationDto(org.Id, org.Name, org.OwnerUserId, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create organization for user {UserId}", userId);
            return DomainErrors.General.ServerError;
        }
    }

    public async Task<Result<IReadOnlyList<OrgMemberDto>>> GetMembersAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        _logger.LogDebug("Getting members for organization {OrganizationId}", organizationId);
        
        var isMember = await _organizationRepository.IsMemberAsync(userId, organizationId, ct);
        if (!isMember)
        {
            _logger.LogWarning("User {UserId} is not a member of organization {OrganizationId}", userId, organizationId);
            return DomainErrors.Organization.NotMember;
        }
        
        var members = await _organizationRepository.GetMembersAsync(organizationId, ct);
        var result = members
            .Where(m => m.User != null)
            .Select(m => new OrgMemberDto(m.UserId, m.User!.Name, m.User.Email, m.Role))
            .ToList();
        
        _logger.LogDebug("Found {Count} members for organization {OrganizationId}", result.Count, organizationId);
        return result;
    }

    public async Task<Result> UpdateMemberRoleAsync(Guid organizationId, Guid requestingUserId, Guid memberUserId, OrgMemberRole newRole, CancellationToken ct = default)
    {
        _logger.LogInformation("Updating role for member {MemberUserId} in organization {OrganizationId} by user {RequestingUserId}", 
            memberUserId, organizationId, requestingUserId);
        
        var requesterRole = await _organizationRepository.GetMemberRoleAsync(requestingUserId, organizationId, ct);
        if (requesterRole != OrgMemberRole.Owner)
        {
            _logger.LogWarning("User {RequestingUserId} is not the owner of organization {OrganizationId}", requestingUserId, organizationId);
            return DomainErrors.Organization.NotOwner;
        }
        
        if (newRole == OrgMemberRole.Owner)
        {
            _logger.LogWarning("Cannot change member role to Owner via UpdateMemberRole - use transfer ownership");
            return Result.Failure(new Error("Organization.CannotAssignOwner", "Use transfer ownership to change the owner"));
        }
        
        var member = await _organizationRepository.GetMemberAsync(organizationId, memberUserId, ct);
        if (member == null)
        {
            _logger.LogWarning("Member {MemberUserId} not found in organization {OrganizationId}", memberUserId, organizationId);
            return DomainErrors.Organization.NotMember;
        }
        
        if (member.Role == OrgMemberRole.Owner)
        {
            _logger.LogWarning("Cannot change owner's role in organization {OrganizationId}", organizationId);
            return Result.Failure(new Error("Organization.CannotChangeOwnerRole", "Cannot change the owner's role"));
        }
        
        await _organizationRepository.UpdateMemberRoleAsync(organizationId, memberUserId, newRole, ct);
        
        _logger.LogInformation("Successfully updated role for member {MemberUserId} to {NewRole} in organization {OrganizationId}", 
            memberUserId, newRole, organizationId);
        return Result.Success();
    }

    public async Task<Result> RemoveMemberAsync(Guid organizationId, Guid requestingUserId, Guid memberUserId, CancellationToken ct = default)
    {
        _logger.LogInformation("Removing member {MemberUserId} from organization {OrganizationId} by user {RequestingUserId}", 
            memberUserId, organizationId, requestingUserId);
        
        var requesterRole = await _organizationRepository.GetMemberRoleAsync(requestingUserId, organizationId, ct);
        if (requesterRole != OrgMemberRole.Owner)
        {
            _logger.LogWarning("User {RequestingUserId} is not the owner of organization {OrganizationId}", requestingUserId, organizationId);
            return DomainErrors.Organization.NotOwner;
        }
        
        var member = await _organizationRepository.GetMemberAsync(organizationId, memberUserId, ct);
        if (member == null)
        {
            _logger.LogWarning("Member {MemberUserId} not found in organization {OrganizationId}", memberUserId, organizationId);
            return DomainErrors.Organization.NotMember;
        }
        
        if (member.Role == OrgMemberRole.Owner)
        {
            _logger.LogWarning("Cannot remove owner from organization {OrganizationId}", organizationId);
            return DomainErrors.Organization.CannotRemoveOwner;
        }
        
        await _organizationRepository.RemoveMemberAsync(organizationId, memberUserId, ct);
        
        _logger.LogInformation("Successfully removed member {MemberUserId} from organization {OrganizationId}", memberUserId, organizationId);
        return Result.Success();
    }

    public async Task<Result<WebhookInfoDto>> GetWebhookInfoAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        _logger.LogDebug("Getting webhook info for organization {OrganizationId}", organizationId);
        
        var isMember = await _organizationRepository.IsMemberAsync(userId, organizationId, ct);
        if (!isMember)
        {
            _logger.LogWarning("User {UserId} is not a member of organization {OrganizationId}", userId, organizationId);
            return DomainErrors.Organization.NotMember;
        }
        
        var org = await _organizationRepository.GetByIdAsync(organizationId, ct);
        if (org == null)
        {
            _logger.LogWarning("Organization {OrganizationId} not found", organizationId);
            return DomainErrors.Organization.NotFound;
        }

        var webhookUrl = "/api/webhook/leads";

        _logger.LogDebug("Successfully retrieved webhook info for organization {OrganizationId}", organizationId);
        return new WebhookInfoDto(
            WebhookUrl: webhookUrl,
            ApiKey: org.WebhookApiKey,
            ApiKeyCreatedAt: org.WebhookApiKeyCreatedAtUtc,
            HasApiKey: !string.IsNullOrEmpty(org.WebhookApiKey)
        );
    }

    public async Task<Result<string>> GenerateWebhookApiKeyAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating webhook API key for organization {OrganizationId} by user {UserId}", organizationId, userId);
        
        var requesterRole = await _organizationRepository.GetMemberRoleAsync(userId, organizationId, ct);
        if (requesterRole != OrgMemberRole.Owner && requesterRole != OrgMemberRole.Manager)
        {
            _logger.LogWarning("User {UserId} is not owner or manager of organization {OrganizationId}", userId, organizationId);
            return DomainErrors.Organization.NotOwnerOrManager;
        }

        var org = await _organizationRepository.GetByIdAsync(organizationId, ct);
        if (org == null)
        {
            _logger.LogWarning("Organization {OrganizationId} not found", organizationId);
            return DomainErrors.Organization.NotFound;
        }

        // Generate secure random API key: "aci_" prefix + base64 encoded random bytes
        var randomBytes = new byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        var apiKey = "aci_" + Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');

        org.WebhookApiKey = apiKey;
        org.WebhookApiKeyCreatedAtUtc = DateTime.UtcNow;

        await _organizationRepository.UpdateAsync(org, ct);
        
        _logger.LogInformation("Successfully generated webhook API key for organization {OrganizationId}", organizationId);
        return apiKey;
    }

    public async Task<Organization?> GetByApiKeyAsync(string apiKey, CancellationToken ct = default)
    {
        return await _organizationRepository.GetByApiKeyAsync(apiKey, ct);
    }
}
