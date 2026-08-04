using ACI.Application.Common;
using ACI.Application.Configuration;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Application.Services;

public sealed class InviteService : IInviteService
{
    private readonly IInviteRepository _inviteRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEmailSender _emailSender;
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<InviteService> _logger;

    public InviteService(
        IInviteRepository inviteRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IEmailSender emailSender,
        IOptions<EmailSettings> emailSettings,
        ILogger<InviteService> logger)
    {
        _inviteRepository = inviteRepository;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _emailSender = emailSender;
        _emailSettings = emailSettings.Value;
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

        var requesterRole = await _organizationRepository.GetMemberRoleAsync(userId, organizationId, ct);
        if (requesterRole != OrgMemberRole.Owner && requesterRole != OrgMemberRole.Manager)
        {
            _logger.LogWarning("User {UserId} is not owner or manager of organization {OrganizationId}", userId, organizationId);
            return Result.Failure<InviteDto>(DomainErrors.Organization.NotOwnerOrManager);
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

        // Tell the invitee. Delivery is best-effort: the invitation already exists and
        // is visible in the app, so a mail problem must not fail the request. The
        // sender logs its own failures.
        var inviter = await _userRepository.GetByIdAsync(userId, ct);
        var acceptUrl = BuildAcceptUrl();
        var sent = await _emailSender.SendOrganizationInviteEmailAsync(
            email, org.Name, inviter?.Name ?? string.Empty, acceptUrl, ct);
        if (!sent)
        {
            _logger.LogWarning(
                "Invite {InviteId} was created but the notification email to {Email} could not be sent",
                invite.Id, email);
        }

        _logger.LogInformation("Invite {InviteId} created for email {Email} to organization {OrganizationId}", invite.Id, email, organizationId);
        return Result.Success(new InviteDto(invite.Id, org.Id, org.Name, invite.Email, invite.ExpiresAtUtc, invite.CreatedAtUtc));
    }

    public async Task<Result<InviteDto>> ResendInviteAsync(Guid inviteId, Guid userId, CancellationToken ct = default)
    {
        _logger.LogInformation("User {UserId} resending invite {InviteId}", userId, inviteId);

        var invite = await _inviteRepository.GetByIdAsync(inviteId, ct);
        if (invite == null)
        {
            _logger.LogWarning("Resend failed: invite {InviteId} not found", inviteId);
            return Result.Failure<InviteDto>(DomainErrors.Invite.NotFound);
        }

        if (invite.AcceptedByUserId != null)
        {
            _logger.LogWarning("Resend failed: invite {InviteId} has already been accepted", inviteId);
            return Result.Failure<InviteDto>(DomainErrors.Invite.AlreadyAccepted);
        }

        var org = await _organizationRepository.GetByIdAsync(invite.OrganizationId, ct);
        if (org == null)
        {
            _logger.LogWarning("Resend failed: organization {OrganizationId} not found", invite.OrganizationId);
            return Result.Failure<InviteDto>(DomainErrors.Organization.NotFound);
        }

        var requesterRole = await _organizationRepository.GetMemberRoleAsync(userId, invite.OrganizationId, ct);
        if (requesterRole != OrgMemberRole.Owner && requesterRole != OrgMemberRole.Manager)
        {
            _logger.LogWarning("User {UserId} is not owner or manager of organization {OrganizationId}", userId, invite.OrganizationId);
            return Result.Failure<InviteDto>(DomainErrors.Organization.NotOwnerOrManager);
        }

        // Give the recipient a full window again — an invitation worth re-sending is
        // usually one that sat unseen, and an already-expired link would be useless.
        invite.ExpiresAtUtc = DateTime.UtcNow.AddDays(7);
        await _inviteRepository.UpdateAsync(invite, ct);

        var inviter = await _userRepository.GetByIdAsync(userId, ct);
        var sent = await _emailSender.SendOrganizationInviteEmailAsync(
            invite.Email, org.Name, inviter?.Name ?? string.Empty, BuildAcceptUrl(), ct);

        if (!sent)
        {
            // Surfaced to the caller on purpose: "resend" that silently does nothing is
            // indistinguishable from a mail server that is not configured at all.
            _logger.LogError("Resend failed: could not send invitation email to {Email}", invite.Email);
            return Result.Failure<InviteDto>(EmailNotSent);
        }

        _logger.LogInformation("Invite {InviteId} re-sent to {Email}", inviteId, invite.Email);
        return Result.Success(new InviteDto(invite.Id, org.Id, org.Name, invite.Email, invite.ExpiresAtUtc, invite.CreatedAtUtc));
    }

    private static readonly Error EmailNotSent = new(
        "Invite.EmailNotSent",
        "The invitation could not be emailed. Check the server's email settings (SendGrid API key and a verified sender address).");

    public async Task<Result<IReadOnlyList<InviteDto>>> ListPendingInvitesAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        _logger.LogInformation("Listing pending invites for organization {OrganizationId} by user {UserId}", organizationId, userId);

        var org = await _organizationRepository.GetByIdAsync(organizationId, ct);
        if (org == null)
        {
            _logger.LogWarning("Organization {OrganizationId} not found", organizationId);
            return Result.Failure<IReadOnlyList<InviteDto>>(DomainErrors.Organization.NotFound);
        }

        var requesterRole = await _organizationRepository.GetMemberRoleAsync(userId, organizationId, ct);
        if (requesterRole != OrgMemberRole.Owner && requesterRole != OrgMemberRole.Manager)
        {
            _logger.LogWarning("User {UserId} is not owner or manager of organization {OrganizationId}", userId, organizationId);
            return Result.Failure<IReadOnlyList<InviteDto>>(DomainErrors.Organization.NotOwnerOrManager);
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

        // Atomically claim the invite so two concurrent accepts don't both add the member.
        var claimed = await _inviteRepository.TryClaimAsync(invite.Id, userId, ct);
        if (!claimed)
        {
            // Loser of a race, OR this user already claimed it but a previous AddMemberAsync failed
            // (leaving claimed-but-not-a-member). Recover the latter case so the user isn't stuck.
            var current = await _inviteRepository.GetByIdAsync(invite.Id, ct);
            if (current?.AcceptedByUserId != userId)
            {
                _logger.LogWarning("Invite {InviteId} was claimed by another concurrent request", invite.Id);
                return Result.Failure<InviteDto>(DomainErrors.Invite.AlreadyAccepted);
            }
            _logger.LogInformation("Invite {InviteId} already claimed by user {UserId}; completing add-member step", invite.Id, userId);
        }

        await _organizationRepository.AddMemberAsync(invite.OrganizationId, userId, OrgMemberRole.Member, ct);

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

        // Atomically claim the invite so two concurrent accepts don't both add the member.
        var claimed = await _inviteRepository.TryClaimAsync(invite.Id, userId, ct);
        if (!claimed)
        {
            // Loser of a race, OR this user already claimed it but a previous AddMemberAsync failed
            // (leaving claimed-but-not-a-member). Recover the latter case so the user isn't stuck.
            var current = await _inviteRepository.GetByIdAsync(invite.Id, ct);
            if (current?.AcceptedByUserId != userId)
            {
                _logger.LogWarning("Invite {InviteId} was claimed by another concurrent request", invite.Id);
                return Result.Failure<InviteDto>(DomainErrors.Invite.AlreadyAccepted);
            }
            _logger.LogInformation("Invite {InviteId} already claimed by user {UserId}; completing add-member step", invite.Id, userId);
        }

        await _organizationRepository.AddMemberAsync(invite.OrganizationId, userId, OrgMemberRole.Member, ct);

        _logger.LogInformation("User {UserId} accepted invite {InviteId} to organization {OrganizationId}", userId, inviteId, invite.OrganizationId);
        return Result.Success(new InviteDto(invite.Id, invite.OrganizationId, invite.Organization.Name, invite.Email, invite.ExpiresAtUtc, invite.CreatedAtUtc));
    }

    /// <summary>
    /// Where the invitee should go to accept. Invitations are accepted in the app on the
    /// organizations page (there is no token link), so the email points there and asks
    /// them to sign in with the invited address.
    /// </summary>
    private string BuildAcceptUrl()
    {
        var baseUrl = (_emailSettings.FrontendBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        return baseUrl.Length == 0 ? "/organizations" : $"{baseUrl}/organizations";
    }
}
