using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

public sealed class InviteService : IInviteService
{
    private readonly IInviteRepository _inviteRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<InviteService> _logger;

    public InviteService(
        IInviteRepository inviteRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        ILogger<InviteService> logger)
    {
        _inviteRepository = inviteRepository;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<Result<InviteDto>> CreateInviteAsync(Guid organizationId, Guid userId, CreateInviteRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation("Creating invite for organization {OrganizationId} by user {UserId}", organizationId, userId);

        var org = await _organizationRepository.GetByIdAsync(organizationId, ct);
        if (org == null)
        {
            _logger.LogWarning("Organization {OrganizationId} not found when creating invite", organizationId);
            return Result.Failure<InviteDto>(DomainErrors.Organization.NotFound);
        }

        if (org.OwnerUserId != userId)
        {
            _logger.LogWarning("User {UserId} is not owner of organization {OrganizationId}", userId, organizationId);
            return Result.Failure<InviteDto>(DomainErrors.Organization.NotOwner);
        }

        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email))
        {
            _logger.LogWarning("Empty email provided for invite");
            return Result.Failure<InviteDto>(DomainErrors.Contact.EmailRequired);
        }

        var existingUser = await _userRepository.GetByEmailAsync(email, ct);
        if (existingUser != null && await _organizationRepository.IsMemberAsync(existingUser.Id, organizationId, ct))
        {
            _logger.LogWarning("User with email {Email} is already a member of organization {OrganizationId}", email, organizationId);
            return Result.Failure<InviteDto>(DomainErrors.Organization.AlreadyMember);
        }

        var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var invite = new Invite
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Email = email,
            Token = token,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7),
            CreatedAtUtc = DateTime.UtcNow,
        };
        await _inviteRepository.AddAsync(invite, ct);

        _logger.LogInformation("Invite {InviteId} created for email {Email} to organization {OrganizationId}", invite.Id, email, organizationId);
        return Result.Success(new InviteDto(invite.Id, org.Id, org.Name, invite.Email, invite.ExpiresAtUtc, invite.CreatedAtUtc));
    }

    public async Task<Result<IReadOnlyList<InviteDto>>> ListPendingInvitesAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        _logger.LogInformation("Listing pending invites for organization {OrganizationId} by user {UserId}", organizationId, userId);

        var org = await _organizationRepository.GetByIdAsync(organizationId, ct);
        if (org == null)
        {
            _logger.LogWarning("Organization {OrganizationId} not found", organizationId);
            return Result.Failure<IReadOnlyList<InviteDto>>(DomainErrors.Organization.NotFound);
        }

        if (org.OwnerUserId != userId)
        {
            _logger.LogWarning("User {UserId} is not owner of organization {OrganizationId}", userId, organizationId);
            return Result.Failure<IReadOnlyList<InviteDto>>(DomainErrors.Organization.NotOwner);
        }

        var list = await _inviteRepository.ListPendingByOrganizationIdAsync(organizationId, ct);
        var dtos = list.Select(i => new InviteDto(i.Id, i.OrganizationId, org.Name, i.Email, i.ExpiresAtUtc, i.CreatedAtUtc)).ToList();

        _logger.LogInformation("Found {Count} pending invites for organization {OrganizationId}", dtos.Count, organizationId);
        return Result.Success<IReadOnlyList<InviteDto>>(dtos);
    }

    public async Task<Result<InviteDto>> AcceptInviteAsync(string token, Guid userId, CancellationToken ct = default)
    {
        _logger.LogInformation("User {UserId} attempting to accept invite with token", userId);

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Empty invite token provided");
            return Result.Failure<InviteDto>(DomainErrors.Invite.InvalidToken);
        }

        var invite = await _inviteRepository.GetByTokenAsync(token.Trim(), ct);
        if (invite == null)
        {
            _logger.LogWarning("Invite with provided token not found");
            return Result.Failure<InviteDto>(DomainErrors.Invite.NotFound);
        }

        if (invite.AcceptedByUserId != null)
        {
            _logger.LogWarning("Invite {InviteId} has already been accepted", invite.Id);
            return Result.Failure<InviteDto>(DomainErrors.Invite.AlreadyAccepted);
        }

        if (invite.ExpiresAtUtc < DateTime.UtcNow)
        {
            _logger.LogWarning("Invite {InviteId} has expired", invite.Id);
            return Result.Failure<InviteDto>(DomainErrors.Invite.Expired);
        }

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null || !string.Equals(user.Email.Trim(), invite.Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("User {UserId} email does not match invite email", userId);
            return Result.Failure<InviteDto>(DomainErrors.Invite.EmailMismatch);
        }

        if (await _organizationRepository.IsMemberAsync(userId, invite.OrganizationId, ct))
        {
            _logger.LogWarning("User {UserId} is already a member of organization {OrganizationId}", userId, invite.OrganizationId);
            return Result.Failure<InviteDto>(DomainErrors.Organization.AlreadyMember);
        }

        await _organizationRepository.AddMemberAsync(invite.OrganizationId, userId, OrgMemberRole.Member, ct);
        invite.AcceptedByUserId = userId;
        invite.AcceptedAtUtc = DateTime.UtcNow;
        await _inviteRepository.UpdateAsync(invite, ct);

        _logger.LogInformation("User {UserId} accepted invite {InviteId} to organization {OrganizationId}", userId, invite.Id, invite.OrganizationId);
        return Result.Success(new InviteDto(invite.Id, invite.OrganizationId, invite.Organization.Name, invite.Email, invite.ExpiresAtUtc, invite.CreatedAtUtc));
    }

    public async Task<Result<IReadOnlyList<InviteDto>>> ListMyPendingInvitesAsync(Guid userId, CancellationToken ct = default)
    {
        _logger.LogInformation("Listing pending invites for user {UserId}", userId);

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found", userId);
            return Result.Failure<IReadOnlyList<InviteDto>>(DomainErrors.Auth.EmailNotFound);
        }

        var email = user.Email?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(email))
        {
            return Result.Success<IReadOnlyList<InviteDto>>(Array.Empty<InviteDto>());
        }

        var list = await _inviteRepository.ListPendingByEmailAsync(email, ct);
        var dtos = list.Select(i => new InviteDto(i.Id, i.OrganizationId, i.Organization.Name, i.Email, i.ExpiresAtUtc, i.CreatedAtUtc)).ToList();

        _logger.LogInformation("Found {Count} pending invites for user {UserId}", dtos.Count, userId);
        return Result.Success<IReadOnlyList<InviteDto>>(dtos);
    }

    public async Task<Result<InviteDto>> AcceptInviteByIdAsync(Guid inviteId, Guid userId, CancellationToken ct = default)
    {
        _logger.LogInformation("User {UserId} attempting to accept invite {InviteId}", userId, inviteId);

        var invite = await _inviteRepository.GetByIdAsync(inviteId, ct);
        if (invite == null)
        {
            _logger.LogWarning("Invite {InviteId} not found", inviteId);
            return Result.Failure<InviteDto>(DomainErrors.Invite.NotFound);
        }

        if (invite.AcceptedByUserId != null)
        {
            _logger.LogWarning("Invite {InviteId} has already been accepted", inviteId);
            return Result.Failure<InviteDto>(DomainErrors.Invite.AlreadyAccepted);
        }

        if (invite.ExpiresAtUtc < DateTime.UtcNow)
        {
            _logger.LogWarning("Invite {InviteId} has expired", inviteId);
            return Result.Failure<InviteDto>(DomainErrors.Invite.Expired);
        }

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user == null || !string.Equals(user.Email?.Trim(), invite.Email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("User {UserId} email does not match invite {InviteId} email", userId, inviteId);
            return Result.Failure<InviteDto>(DomainErrors.Invite.EmailMismatch);
        }

        if (await _organizationRepository.IsMemberAsync(userId, invite.OrganizationId, ct))
        {
            _logger.LogWarning("User {UserId} is already a member of organization {OrganizationId}", userId, invite.OrganizationId);
            return Result.Failure<InviteDto>(DomainErrors.Organization.AlreadyMember);
        }

        await _organizationRepository.AddMemberAsync(invite.OrganizationId, userId, OrgMemberRole.Member, ct);
        invite.AcceptedByUserId = userId;
        invite.AcceptedAtUtc = DateTime.UtcNow;
        await _inviteRepository.UpdateAsync(invite, ct);

        _logger.LogInformation("User {UserId} accepted invite {InviteId} to organization {OrganizationId}", userId, inviteId, invite.OrganizationId);
        return Result.Success(new InviteDto(invite.Id, invite.OrganizationId, invite.Organization.Name, invite.Email, invite.ExpiresAtUtc, invite.CreatedAtUtc));
    }
}
