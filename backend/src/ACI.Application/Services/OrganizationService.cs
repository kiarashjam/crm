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
    private readonly ICollaborationNotifier? _collaboration;

    public OrganizationService(
        IOrganizationRepository organizationRepository,
        ILeadStatusRepository leadStatusRepository,
        ILeadSourceRepository leadSourceRepository,
        IUserRepository userRepository,
        ILogger<OrganizationService> logger,
        ICollaborationNotifier? collaboration = null)
    {
        _organizationRepository = organizationRepository;
        _leadStatusRepository = leadStatusRepository;
        _leadSourceRepository = leadSourceRepository;
        _userRepository = userRepository;
        _logger = logger;
        _collaboration = collaboration;
    }

    public async Task<IReadOnlyList<OrganizationDto>> ListMyOrganizationsAsync(Guid userId, CancellationToken ct = default)
    {
        _logger.LogDebug("Listing organizations for user {UserId}", userId);

        var orgs = await _organizationRepository.GetByUserIdAsync(userId, ct);
        var result = new List<OrganizationDto>(orgs.Count);
        foreach (var o in orgs)
        {
            var member = await _organizationRepository.GetMemberAsync(o.Id, userId, ct);
            if (member == null) continue;
            result.Add(ToDto(o, userId, member));
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

        var member = await _organizationRepository.GetMemberAsync(organizationId, userId, ct);
        if (member == null) return DomainErrors.Organization.NotMember;
        _logger.LogDebug("Successfully retrieved organization {OrganizationId}", organizationId);
        return ToDto(org, userId, member);
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
            return ToDto(org, userId, new OrganizationMember { OrganizationId = org.Id, UserId = userId, Role = OrgMemberRole.Owner });
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
            .Select(m => MemberDto(m, m.User!.Name, m.User.Email))
            .ToList();
        
        _logger.LogDebug("Found {Count} members for organization {OrganizationId}", result.Count, organizationId);
        return result;
    }

    public async Task<Result<OrgMemberDto>> AddMemberByEmailAsync(
        Guid organizationId, Guid requestingUserId, string email, OrgMemberRole role, CancellationToken ct = default, string? preset = null)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        _logger.LogInformation(
            "User {RequestingUserId} adding {Email} to organization {OrganizationId} as {Role}",
            requestingUserId, normalized, organizationId, role);

        if (!await _organizationRepository.HasPermissionAsync(requestingUserId, organizationId, MemberPermissions.ManageMembers, ct))
        {
            _logger.LogWarning("User {RequestingUserId} may not manage the members of organization {OrganizationId}", requestingUserId, organizationId);
            return Result.Failure<OrgMemberDto>(DomainErrors.Permission.Missing(MemberPermissions.ManageMembers));
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

        // Anyone can register an address before its owner does; adding that account
        // would hand the workspace to whoever got there first.
        if (user.EmailVerifiedAtUtc == null)
        {
            _logger.LogWarning("Cannot add {Email}: the account has not confirmed the address", normalized);
            return Result.Failure<OrgMemberDto>(EmailNotConfirmed);
        }

        if (await _organizationRepository.IsMemberAsync(user.Id, organizationId, ct))
        {
            _logger.LogWarning("User {UserId} is already a member of organization {OrganizationId}", user.Id, organizationId);
            return Result.Failure<OrgMemberDto>(DomainErrors.Organization.AlreadyMember);
        }

        // A preset chosen sets the role with it; nobody hands out more than they can do.
        var added = new OrganizationMember { OrganizationId = organizationId, UserId = user.Id, Role = role };
        if (preset != null)
        {
            var chosen = MemberPermissions.FindPreset(preset);
            if (chosen == null || chosen == MemberPermissions.Owner) return Result.Failure<OrgMemberDto>(UnknownPreset);
            added.PermissionPreset = chosen.Key;
            added.Role = MemberPermissions.RoleFor(chosen, chosen.Permissions.ToHashSet());
        }
        if (!await _organizationRepository.HoldsAllAsync(requestingUserId, organizationId, MemberAccess.PermissionsOf(added), ct))
            return Result.Failure<OrgMemberDto>(DomainErrors.Permission.MoreThanYourOwn);
        await _organizationRepository.AddMemberAsync(organizationId, user.Id, added.Role, added.PermissionPreset, ct);

        _logger.LogInformation(
            "Added user {UserId} to organization {OrganizationId} as {Role} ({Preset})",
            user.Id, organizationId, added.Role, added.PermissionPreset ?? "role");
        return Result.Success(MemberDto(added, user.Name, user.Email));
    }

    private static readonly Error UnknownPreset = new(
        "Organization.UnknownPreset", "That is not one of the presets. Pick Manager, Sales rep, Membership director, Finance, Front desk or Read-only accountant.");

    private static readonly Error UnknownPermission = new(
        "Organization.UnknownPermission", "One of those is not a permission this version knows.");

    private static readonly Error CustomiseNotOwner = new(
        "Organization.CustomiseNotOwner", "Only the owner can change single permissions. Pick one of the presets instead.");

    public PermissionCatalogueDto GetPermissionCatalogue() => new(
        MemberPermissions.All.Select(p => new PermissionDto(p.Key, p.Label, p.Description)).ToList(),
        MemberPermissions.Presets
            .Select(p => new PermissionPresetDto(p.Key, p.Label, p.Description, p.Permissions, p != MemberPermissions.Owner))
            .ToList());

    public async Task<Result<OrgMemberDto>> SetMemberPermissionsAsync(
        Guid organizationId, Guid requestingUserId, Guid memberUserId, SetMemberPermissionsRequest request, CancellationToken ct = default)
    {
        var requester = await _organizationRepository.GetMemberAsync(organizationId, requestingUserId, ct);
        if (requester == null) return DomainErrors.Organization.NotMember;
        var mine = Permissions(requester);
        if (!mine.Contains(MemberPermissions.ManageMembers)) return DomainErrors.Permission.Missing(MemberPermissions.ManageMembers);

        var member = await _organizationRepository.GetMemberAsync(organizationId, memberUserId, ct);
        if (member == null) return DomainErrors.Organization.NotMember;
        // The owner can do everything, always: there is nobody above them to undo a mistake.
        if (member.Role == OrgMemberRole.Owner)
            return Result.Failure<OrgMemberDto>(new Error("Organization.CannotChangeOwnerRole", "The owner can always do everything."));

        var preset = MemberPermissions.FindPreset(request.Preset);
        if (preset == null || preset == MemberPermissions.Owner) return UnknownPreset;
        IReadOnlySet<string> wanted = preset.Permissions.ToHashSet(StringComparer.Ordinal);
        if (request.Permissions != null)
        {
            // Editable per member by owners: a manager picks presets, the owner fine-tunes.
            if (requester.Role != OrgMemberRole.Owner) return CustomiseNotOwner;
            if (request.Permissions.Any(p => !MemberPermissions.IsPermission(p))) return UnknownPermission;
            wanted = request.Permissions.ToHashSet(StringComparer.Ordinal);
        }
        if (requester.Role != OrgMemberRole.Owner && (!wanted.IsSubsetOf(mine) || !Permissions(member).IsSubsetOf(mine)))
            return DomainErrors.Permission.MoreThanYourOwn;

        var stored = wanted.SetEquals(preset.Permissions) ? null : MemberPermissions.Format(wanted);
        var role = MemberPermissions.RoleFor(preset, wanted);
        await _organizationRepository.UpdateMemberPermissionsAsync(organizationId, memberUserId, role, preset.Key, stored, ct);
        _logger.LogInformation("User {RequestingUserId} set member {MemberUserId} of organization {OrganizationId} to {Preset}{Custom}",
            requestingUserId, memberUserId, organizationId, preset.Key, stored == null ? "" : " (customised)");

        member.Role = role;
        member.PermissionPreset = preset.Key;
        member.Permissions = stored;
        return MemberDto(member, member.User?.Name ?? "", member.User?.Email ?? "");
    }

    private static readonly Error NoAccountForEmail = new(
        "Organization.NoAccountForEmail",
        "No account exists with that email address yet. Ask them to sign up first, then add them — or send an invitation instead.");

    private static readonly Error EmailNotConfirmed = new(
        "Organization.MemberEmailNotConfirmed",
        "That account has not confirmed its email address yet. Ask them to open the confirmation email CadenceE sent them, then add them — or send an invitation instead.");

    public async Task<Result> UpdateMemberRoleAsync(Guid organizationId, Guid requestingUserId, Guid memberUserId, OrgMemberRole newRole, CancellationToken ct = default)
    {
        _logger.LogInformation("Updating role for member {MemberUserId} in organization {OrganizationId} by user {RequestingUserId}", 
            memberUserId, organizationId, requestingUserId);
        
        if (!await _organizationRepository.HasPermissionAsync(requestingUserId, organizationId, MemberPermissions.ManageMembers, ct))
        {
            _logger.LogWarning("User {RequestingUserId} may not manage the members of organization {OrganizationId}", requestingUserId, organizationId);
            return DomainErrors.Permission.Missing(MemberPermissions.ManageMembers);
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

        // A role set here means what it meant before presets (UpdateMemberRoleAsync clears
        // the member's preset), and nobody gives or takes more than they hold.
        if (!await _organizationRepository.HoldsAllAsync(requestingUserId, organizationId,
                MemberAccess.PermissionsOf(member).Concat(MemberPermissions.Effective(newRole, null, null)), ct))
            return Result.Failure(DomainErrors.Permission.MoreThanYourOwn);

        await _organizationRepository.UpdateMemberRoleAsync(organizationId, memberUserId, newRole, ct);
        
        _logger.LogInformation("Successfully updated role for member {MemberUserId} to {NewRole} in organization {OrganizationId}", 
            memberUserId, newRole, organizationId);
        return Result.Success();
    }

    public async Task<Result<MemberRemovedDto>> RemoveMemberAsync(Guid organizationId, Guid requestingUserId, Guid memberUserId, Guid? handOverToUserId = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Removing member {MemberUserId} from organization {OrganizationId} by user {RequestingUserId}", 
            memberUserId, organizationId, requestingUserId);
        
        if (!await _organizationRepository.HasPermissionAsync(requestingUserId, organizationId, MemberPermissions.ManageMembers, ct))
        {
            _logger.LogWarning("User {RequestingUserId} may not manage the members of organization {OrganizationId}", requestingUserId, organizationId);
            return DomainErrors.Permission.Missing(MemberPermissions.ManageMembers);
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
        if (!await _organizationRepository.HoldsAllAsync(requestingUserId, organizationId, MemberAccess.PermissionsOf(member), ct))
            return DomainErrors.Permission.MoreThanYourOwn;

        // Their open work goes to someone who can carry it on: the member chosen, or the
        // owner. Left with them, their leads and deals showed an owner nobody could act
        // as, and their tasks kept emailing reminders, with customers' details, to
        // someone who no longer works here.
        var org = await _organizationRepository.GetByIdAsync(organizationId, ct);
        if (org == null) return DomainErrors.Organization.NotFound;
        var heir = handOverToUserId ?? org.OwnerUserId;
        if (heir == memberUserId) return DomainErrors.Organization.HandOverToLeaver;
        var heirMember = await _organizationRepository.GetMemberAsync(organizationId, heir, ct);
        if (heirMember == null) return DomainErrors.Organization.HandOverOutsider;
        if (!Permissions(heirMember).Contains(MemberPermissions.EditRecords)) return DomainErrors.Organization.HandOverReadOnly;

        var handedOver = await _organizationRepository.RemoveMemberAsync(organizationId, memberUserId, heir, requestingUserId, ct);

        // One notice for each kind of record that moved, rather than one per record.
        if (_collaboration != null)
        {
            await _collaboration.RecordsAssignedAsync(RecordTypes.Lead, heir, organizationId, handedOver.Leads, requestingUserId, ct);
            await _collaboration.RecordsAssignedAsync(RecordTypes.Deal, heir, organizationId, handedOver.Deals, requestingUserId, ct);
            await _collaboration.TasksAssignedAsync(heir, handedOver.Tasks, requestingUserId, ct);
        }

        _logger.LogInformation(
            "Removed member {MemberUserId} from organization {OrganizationId}; {Leads} leads, {Deals} deals and {Tasks} tasks handed to {HeirUserId}",
            memberUserId, organizationId, handedOver.Leads.Count, handedOver.Deals.Count, handedOver.Tasks.Count, heir);
        return new MemberRemovedDto(heir, heirMember.User?.Name ?? "", handedOver.Leads.Count, handedOver.Deals.Count, handedOver.Tasks.Count);
    }

    public async Task<Result<WebhookInfoDto>> GetWebhookInfoAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        _logger.LogDebug("Getting webhook info for organization {OrganizationId}", organizationId);
        
        var role = await _organizationRepository.GetMemberRoleAsync(userId, organizationId, ct);
        if (role == null)
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
            // Only the key's hash is kept, so there is no key to send back: it is shown
            // once, to whoever generates it.
            ApiKeyCreatedAt: org.WebhookApiKeyCreatedAtUtc,
            HasApiKey: !string.IsNullOrEmpty(org.WebhookApiKeyHash),
            UsesDefaultWebhookPassword: string.IsNullOrEmpty(org.WebhookPassword),
            PasswordWebhookUrl: string.Empty,
            OutboundWebhookUrl: org.OutboundWebhookUrl,
            HasOutboundWebhookSecret: !string.IsNullOrEmpty(org.OutboundWebhookSecret),
            HasMcpApiKey: !string.IsNullOrEmpty(org.McpApiKeyHash),
            McpApiKeyCreatedAt: org.McpApiKeyCreatedAtUtc
        );
    }

    public async Task<Organization?> GetByIdUnauthenticatedAsync(Guid organizationId, CancellationToken ct = default)
    {
        return await _organizationRepository.GetByIdAsync(organizationId, ct);
    }

    public async Task<Result> UpdateWebhookPasswordAsync(Guid organizationId, Guid userId, string? newPassword, CancellationToken ct = default)
    {
        if (!await _organizationRepository.HasPermissionAsync(userId, organizationId, MemberPermissions.ManageSettings, ct))
        {
            _logger.LogWarning("User {UserId} may not change the settings of organization {OrganizationId}", userId, organizationId);
            return DomainErrors.Permission.Missing(MemberPermissions.ManageSettings);
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

    public async Task<Result> UpdateOutboundWebhookAsync(Guid organizationId, Guid userId, string? url, string? secret, CancellationToken ct = default)
    {
        if (!await _organizationRepository.HasPermissionAsync(userId, organizationId, MemberPermissions.ManageSettings, ct))
            return DomainErrors.Permission.Missing(MemberPermissions.ManageSettings);
        var org = await _organizationRepository.GetByIdAsync(organizationId, ct);
        if (org == null) return DomainErrors.Organization.NotFound;
        var trimmed = (url ?? "").Trim();
        if (trimmed.Length == 0)
        {
            org.OutboundWebhookUrl = null;
            org.OutboundWebhookSecret = null;
        }
        else if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return new Error("Organization.WebhookUrl", "The outbound address must be an https URL.");
        }
        else if (trimmed.Length > 500 || (secret?.Trim().Length ?? 0) > 200)
        {
            return new Error("Organization.WebhookUrl", "The address can be at most 500 characters, and the secret at most 200.");
        }
        else if (!PublicAddress.LooksPublic(uri))
        {
            return new Error("Organization.WebhookUrl", "The outbound address must be on the public internet.");
        }
        else
        {
            org.OutboundWebhookUrl = trimmed;
            if (secret != null) org.OutboundWebhookSecret = secret.Trim();
        }
        await _organizationRepository.UpdateAsync(org, ct);
        return Result.Success();
    }

    public async Task<Result<OrganizationDto>> UpdateSettingsAsync(Guid organizationId, Guid userId, UpdateOrganizationSettingsRequest request, CancellationToken ct = default)
    {
        // The currency prices every new deal and labels every club amount, and the zone
        // moves where each report's months begin, for the whole team. So it is for whoever
        // manages the settings (the owner and managers by default), like the pipelines.
        var member = await _organizationRepository.GetMemberAsync(organizationId, userId, ct);
        if (member == null) return DomainErrors.Organization.NotMember;
        if (!Permissions(member).Contains(MemberPermissions.ManageSettings))
            return DomainErrors.Permission.Missing(MemberPermissions.ManageSettings);
        var org = await _organizationRepository.GetByIdAsync(organizationId, ct);
        if (org == null) return DomainErrors.Organization.NotFound;

        string? currency = null, locale = null, zone = null;
        if (request.Currency != null && !RegionalSettings.TryCurrency(request.Currency, out currency))
            return DomainErrors.Organization.InvalidCurrency;
        if (request.Locale != null && !RegionalSettings.TryLocale(request.Locale, out locale))
            return DomainErrors.Organization.InvalidLocale;
        if (request.TimeZone != null && !RegionalSettings.TryTimeZone(request.TimeZone, out zone))
            return DomainErrors.Organization.InvalidTimeZone;
        if (request.StaleDealDays is int staleDays && !StaleDeals.IsValidSetting(staleDays))
            return DomainErrors.Organization.InvalidStaleDealDays;

        // Deals keep the currency they were priced in. Only new ones take the new one.
        if (currency != null) org.Currency = currency;
        if (locale != null) org.Locale = locale;
        if (zone != null) org.TimeZone = zone;
        if (request.StaleDealDays is int days) org.StaleDealDays = days;
        await _organizationRepository.UpdateAsync(org, ct);
        _logger.LogInformation("Regional settings of organization {OrganizationId} set to {Currency}, {Locale}, {TimeZone}, stale after {StaleDealDays} days, by {UserId}",
            organizationId, org.Currency, org.Locale, org.TimeZone, org.StaleDealDays, userId);
        return ToDto(org, userId, member);
    }

    public async Task<Result<string>> GenerateWebhookApiKeyAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        _logger.LogInformation("Generating webhook API key for organization {OrganizationId} by user {UserId}", organizationId, userId);
        
        if (!await _organizationRepository.HasPermissionAsync(userId, organizationId, MemberPermissions.ManageSettings, ct))
        {
            _logger.LogWarning("User {UserId} may not change the settings of organization {OrganizationId}", userId, organizationId);
            return DomainErrors.Permission.Missing(MemberPermissions.ManageSettings);
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

        org.WebhookApiKeyHash = HashApiKey(apiKey);
        org.WebhookApiKeyCreatedAtUtc = DateTime.UtcNow;

        await _organizationRepository.UpdateAsync(org, ct);
        
        _logger.LogInformation("Successfully generated webhook API key for organization {OrganizationId}", organizationId);
        return apiKey;
    }

    public async Task<Organization?> GetByApiKeyAsync(string apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return null;
        return await _organizationRepository.GetByWebhookApiKeyHashAsync(HashApiKey(apiKey.Trim()), ct);
    }

    public async Task<Result<string>> GenerateMcpApiKeyAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        if (!await _organizationRepository.HasPermissionAsync(userId, organizationId, MemberPermissions.ManageSettings, ct))
            return DomainErrors.Permission.Missing(MemberPermissions.ManageSettings);

        var org = await _organizationRepository.GetByIdAsync(organizationId, ct);
        if (org == null) return DomainErrors.Organization.NotFound;

        var randomBytes = new byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        var apiKey = "aci_mcp_" + Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');

        org.McpApiKeyHash = HashApiKey(apiKey);
        org.McpApiKeyCreatedAtUtc = DateTime.UtcNow;
        await _organizationRepository.UpdateAsync(org, ct);

        _logger.LogInformation("Generated MCP API key for organization {OrganizationId} by user {UserId}", organizationId, userId);
        return apiKey;
    }

    public async Task<Organization?> GetByMcpApiKeyAsync(string apiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey)) return null;
        return await _organizationRepository.GetByMcpApiKeyHashAsync(HashApiKey(apiKey.Trim()), ct);
    }

    /// <summary>
    /// Lower-case hex SHA-256 of the key's UTF-8 bytes: what is stored and looked up for
    /// webhook and MCP keys, never the key itself. LegacyWebhookKeys computes the same in SQL.
    /// </summary>
    public static string HashApiKey(string apiKey) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(apiKey))).ToLowerInvariant();

    private static OrganizationDto ToDto(Organization org, Guid userId, OrganizationMember member) =>
        new(org.Id, org.Name, org.OwnerUserId, org.OwnerUserId == userId, (int)member.Role, org.Currency, org.Locale, org.TimeZone, org.StaleDealDays,
            MemberPermissions.PresetOf(member.Role, member.PermissionPreset).Key, Ordered(Permissions(member)),
            SalesEnabled: !org.SalesModuleOff, ClubEnabled: !org.ClubModuleOff, SsoRequired: org.SsoRequired);

    private static IReadOnlySet<string> Permissions(OrganizationMember member) =>
        MemberPermissions.Effective(member.Role, member.PermissionPreset, member.Permissions);

    /// <summary>A set of permissions in the order the catalogue lists them.</summary>
    private static IReadOnlyList<string> Ordered(IReadOnlySet<string> permissions) =>
        MemberPermissions.All.Select(p => p.Key).Where(permissions.Contains).ToList();

    private static OrgMemberDto MemberDto(OrganizationMember member, string name, string email) => new(
        member.UserId, name, email, member.Role,
        MemberPermissions.PresetOf(member.Role, member.PermissionPreset).Key,
        Ordered(Permissions(member)),
        MemberPermissions.IsCustomised(member.Role, member.PermissionPreset, member.Permissions));
}
