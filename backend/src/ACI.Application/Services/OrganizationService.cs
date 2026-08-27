using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Common;
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
    private readonly IUserRepository _userRepository;
    private readonly ILogger<OrganizationService> _logger;

    public OrganizationService(
        IOrganizationRepository organizationRepository, 
        ILeadStatusRepository leadStatusRepository, 
        ILeadSourceRepository leadSourceRepository,
        IUserRepository userRepository,
        ILogger<OrganizationService> logger)
    {
        _organizationRepository = organizationRepository;
        _leadStatusRepository = leadStatusRepository;
        _leadSourceRepository = leadSourceRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<OrganizationDto>> ListMyOrganizationsAsync(Guid userId, CancellationToken ct = default)
    {
        _logger.LogDebug("Listing organizations for user {UserId}", userId);

        var orgs = await _organizationRepository.GetByUserIdAsync(userId, ct);
        var result = new List<OrganizationDto>(orgs.Count);
        foreach (var o in orgs)
        {
            var role = await _organizationRepository.GetMemberRoleAsync(userId, o.Id, ct) ?? OrgMemberRole.Member;
            result.Add(new OrganizationDto(o.Id, o.Name, o.OwnerUserId, o.OwnerUserId == userId, (int)role));
        }

        _logger.LogDebug("Found {Count} organizations for user {UserId}", result.Count, userId);
        return result;
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

        var role = await _organizationRepository.GetMemberRoleAsync(userId, organizationId, ct) ?? OrgMemberRole.Member;
        _logger.LogDebug("Successfully retrieved organization {OrganizationId}", organizationId);
        return new OrganizationDto(org.Id, org.Name, org.OwnerUserId, org.OwnerUserId == userId, (int)role);
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
            
            // Seed default Lead Statuses.
            // Read from LeadStatusVocabulary rather than restated here: the same
            // list has to be applied to organisations that already exist, and a
            // vocabulary written down twice drifts. That is exactly what happened
            // before — see the notes on LeadStatusVocabulary.
            var defaultStatuses = LeadStatusVocabulary.Default;
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
            return new OrganizationDto(org.Id, org.Name, org.OwnerUserId, true, (int)OrgMemberRole.Owner);
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

    public async Task<Result<OrgMemberDto>> AddMemberByEmailAsync(
        Guid organizationId, Guid requestingUserId, string email, OrgMemberRole role, CancellationToken ct = default)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        _logger.LogInformation(
            "User {RequestingUserId} adding {Email} to organization {OrganizationId} as {Role}",
            requestingUserId, normalized, organizationId, role);

        var requesterRole = await _organizationRepository.GetMemberRoleAsync(requestingUserId, organizationId, ct);
        if (requesterRole != OrgMemberRole.Owner && requesterRole != OrgMemberRole.Manager)
        {
            _logger.LogWarning("User {RequestingUserId} is not owner or manager of organization {OrganizationId}", requestingUserId, organizationId);
            return Result.Failure<OrgMemberDto>(DomainErrors.Organization.NotOwnerOrManager);
        }

        if (string.IsNullOrEmpty(normalized))
            return Result.Failure<OrgMemberDto>(DomainErrors.Contact.EmailRequired);

        // Ownership is transferred deliberately, never granted here.
        if (role == OrgMemberRole.Owner)
            return Result.Failure<OrgMemberDto>(new Error("Organization.CannotAssignOwner", "Use transfer ownership to change the owner"));

        var org = await _organizationRepository.GetByIdAsync(organizationId, ct);
        if (org == null)
            return Result.Failure<OrgMemberDto>(DomainErrors.Organization.NotFound);

        var user = await _userRepository.GetByEmailAsync(normalized, ct);
        if (user == null)
        {
            _logger.LogWarning("Cannot add {Email}: no account exists with that address", normalized);
            return Result.Failure<OrgMemberDto>(NoAccountForEmail);
        }

        if (await _organizationRepository.IsMemberAsync(user.Id, organizationId, ct))
        {
            _logger.LogWarning("User {UserId} is already a member of organization {OrganizationId}", user.Id, organizationId);
            return Result.Failure<OrgMemberDto>(DomainErrors.Organization.AlreadyMember);
        }

        await _organizationRepository.AddMemberAsync(organizationId, user.Id, role, ct);

        _logger.LogInformation(
            "Added user {UserId} to organization {OrganizationId} as {Role}",
            user.Id, organizationId, role);
        return Result.Success(new OrgMemberDto(user.Id, user.Name, user.Email, role));
    }

    private static readonly Error NoAccountForEmail = new(
        "Organization.NoAccountForEmail",
        "No account exists with that email address yet. Ask them to sign up first, then add them — or send an invitation instead.");

    public async Task<Result> UpdateMemberRoleAsync(Guid organizationId, Guid requestingUserId, Guid memberUserId, OrgMemberRole newRole, CancellationToken ct = default)
    {
        _logger.LogInformation("Updating role for member {MemberUserId} in organization {OrganizationId} by user {RequestingUserId}", 
            memberUserId, organizationId, requestingUserId);
        
        var requesterRole = await _organizationRepository.GetMemberRoleAsync(requestingUserId, organizationId, ct);
        if (requesterRole != OrgMemberRole.Owner && requesterRole != OrgMemberRole.Manager)
        {
            _logger.LogWarning("User {RequestingUserId} is not an owner or manager of organization {OrganizationId}", requestingUserId, organizationId);
            return DomainErrors.Organization.NotOwnerOrManager;
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
        if (requesterRole != OrgMemberRole.Owner && requesterRole != OrgMemberRole.Manager)
        {
            _logger.LogWarning("User {RequestingUserId} is not an owner or manager of organization {OrganizationId}", requestingUserId, organizationId);
            return DomainErrors.Organization.NotOwnerOrManager;
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
            HasApiKey: !string.IsNullOrEmpty(org.WebhookApiKey),
            UsesDefaultWebhookPassword: string.IsNullOrEmpty(org.WebhookPassword),
            PasswordWebhookUrl: string.Empty
        );
    }

    public async Task<Organization?> GetByIdUnauthenticatedAsync(Guid organizationId, CancellationToken ct = default)
    {
        return await _organizationRepository.GetByIdAsync(organizationId, ct);
    }

    public async Task<Result> UpdateWebhookPasswordAsync(Guid organizationId, Guid userId, string? newPassword, CancellationToken ct = default)
    {
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

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            org.WebhookPassword = null;
        }
        else
        {
            var trimmed = newPassword.Trim();
            if (trimmed.Length > 256)
                return DomainErrors.Organization.WebhookPasswordTooLong;

            org.WebhookPassword = trimmed;
        }

        await _organizationRepository.UpdateAsync(org, ct);
        _logger.LogInformation("Webhook password updated for organization {OrganizationId}", organizationId);
        return Result.Success();
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
