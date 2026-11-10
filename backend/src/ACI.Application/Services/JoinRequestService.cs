using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

public sealed class JoinRequestService : IJoinRequestService
{
    private readonly IJoinRequestRepository _joinRequestRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<JoinRequestService> _logger;

    public JoinRequestService(
        IJoinRequestRepository joinRequestRepository,
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        ILogger<JoinRequestService> logger)
    {
        _joinRequestRepository = joinRequestRepository;
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<Result<JoinRequestDto>> CreateJoinRequestAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        _logger.LogInformation("User {UserId} creating join request for organization {OrganizationId}", userId, organizationId);

        var org = await _organizationRepository.GetByIdAsync(organizationId, ct);
        if (org == null)
        {
            _logger.LogWarning("Organization {OrganizationId} not found", organizationId);
            return Result.Failure<JoinRequestDto>(DomainErrors.Organization.NotFound);
        }

        if (await _organizationRepository.IsMemberAsync(userId, organizationId, ct))
        {
            _logger.LogWarning("User {UserId} is already a member of organization {OrganizationId}", userId, organizationId);
            return Result.Failure<JoinRequestDto>(DomainErrors.Organization.AlreadyMember);
        }

        // The owner approves a request by the name and email it shows, so the email
        // must really be the requester's.
        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user?.EmailVerifiedAtUtc == null)
        {
            _logger.LogWarning("User {UserId} asked to join organization {OrganizationId} before confirming their email", userId, organizationId);
            return Result.Failure<JoinRequestDto>(DomainErrors.Auth.EmailNotVerified);
        }

        var existing = await _joinRequestRepository.ListPendingByOrganizationIdAsync(organizationId, ct);
        if (existing.Any(j => j.UserId == userId))
        {
            _logger.LogWarning("User {UserId} already has a pending join request for organization {OrganizationId}", userId, organizationId);
            return Result.Failure<JoinRequestDto>(DomainErrors.JoinRequest.AlreadyPending);
        }

        var joinRequest = new JoinRequest
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            Status = JoinRequestStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow,
        };
        await _joinRequestRepository.AddAsync(joinRequest, ct);

        var withUser = await _joinRequestRepository.GetByIdAsync(joinRequest.Id, ct);
        if (withUser == null)
        {
            _logger.LogError("Failed to retrieve created join request {JoinRequestId}", joinRequest.Id);
            return Result.Failure<JoinRequestDto>(DomainErrors.JoinRequest.NotFound);
        }

        _logger.LogInformation("Join request {JoinRequestId} created by user {UserId} for organization {OrganizationId}", joinRequest.Id, userId, organizationId);
        return Result.Success(ToDto(withUser));
    }

    public async Task<Result<IReadOnlyList<JoinRequestDto>>> ListPendingJoinRequestsAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        _logger.LogInformation("Listing pending join requests for organization {OrganizationId} by user {UserId}", organizationId, userId);

        var org = await _organizationRepository.GetByIdAsync(organizationId, ct);
        if (org == null)
        {
            _logger.LogWarning("Organization {OrganizationId} not found", organizationId);
            return Result.Failure<IReadOnlyList<JoinRequestDto>>(DomainErrors.Organization.NotFound);
        }

        if (!await _organizationRepository.HasPermissionAsync(userId, organizationId, MemberPermissions.ManageMembers, ct))
        {
            _logger.LogWarning("User {UserId} may not manage the members of organization {OrganizationId}", userId, organizationId);
            return Result.Failure<IReadOnlyList<JoinRequestDto>>(DomainErrors.Permission.Missing(MemberPermissions.ManageMembers));
        }

        var list = await _joinRequestRepository.ListPendingByOrganizationIdAsync(organizationId, ct);
        var dtos = list.Select(ToDto).ToList();

        _logger.LogInformation("Found {Count} pending join requests for organization {OrganizationId}", dtos.Count, organizationId);
        return Result.Success<IReadOnlyList<JoinRequestDto>>(dtos);
    }

    public async Task<Result<JoinRequestDto>> AcceptJoinRequestAsync(Guid joinRequestId, Guid userId, CancellationToken ct = default)
    {
        _logger.LogInformation("User {UserId} accepting join request {JoinRequestId}", userId, joinRequestId);

        var jr = await _joinRequestRepository.GetByIdAsync(joinRequestId, ct);
        if (jr == null)
        {
            _logger.LogWarning("Join request {JoinRequestId} not found", joinRequestId);
            return Result.Failure<JoinRequestDto>(DomainErrors.JoinRequest.NotFound);
        }

        if (jr.Status != JoinRequestStatus.Pending)
        {
            _logger.LogWarning("Join request {JoinRequestId} is not pending (status: {Status})", joinRequestId, jr.Status);
            return Result.Failure<JoinRequestDto>(DomainErrors.JoinRequest.AlreadyProcessed);
        }

        var org = await _organizationRepository.GetByIdAsync(jr.OrganizationId, ct);
        if (org == null)
        {
            _logger.LogWarning("Organization {OrganizationId} not found for join request {JoinRequestId}", jr.OrganizationId, joinRequestId);
            return Result.Failure<JoinRequestDto>(DomainErrors.Organization.NotFound);
        }

        if (!await _organizationRepository.HasPermissionAsync(userId, jr.OrganizationId, MemberPermissions.ManageMembers, ct))
        {
            _logger.LogWarning("User {UserId} may not manage the members of organization {OrganizationId}", userId, jr.OrganizationId);
            return Result.Failure<JoinRequestDto>(DomainErrors.Permission.Missing(MemberPermissions.ManageMembers));
        }
        // They join as a Member, with what Members could always do.
        if (!await _organizationRepository.HoldsAllAsync(userId, jr.OrganizationId, MemberPermissions.LegacyMember, ct))
            return Result.Failure<JoinRequestDto>(DomainErrors.Permission.MoreThanYourOwn);

        await _organizationRepository.AddMemberAsync(jr.OrganizationId, jr.UserId, OrgMemberRole.Member, ct);
        jr.Status = JoinRequestStatus.Accepted;
        jr.RespondedByUserId = userId;
        jr.RespondedAtUtc = DateTime.UtcNow;
        await _joinRequestRepository.UpdateAsync(jr, ct);

        _logger.LogInformation("Join request {JoinRequestId} accepted, user {RequestUserId} added to organization {OrganizationId}", joinRequestId, jr.UserId, jr.OrganizationId);
        return Result.Success(ToDto(jr));
    }

    public async Task<Result<JoinRequestDto>> RejectJoinRequestAsync(Guid joinRequestId, Guid userId, CancellationToken ct = default)
    {
        _logger.LogInformation("User {UserId} rejecting join request {JoinRequestId}", userId, joinRequestId);

        var jr = await _joinRequestRepository.GetByIdAsync(joinRequestId, ct);
        if (jr == null)
        {
            _logger.LogWarning("Join request {JoinRequestId} not found", joinRequestId);
            return Result.Failure<JoinRequestDto>(DomainErrors.JoinRequest.NotFound);
        }

        if (jr.Status != JoinRequestStatus.Pending)
        {
            _logger.LogWarning("Join request {JoinRequestId} is not pending (status: {Status})", joinRequestId, jr.Status);
            return Result.Failure<JoinRequestDto>(DomainErrors.JoinRequest.AlreadyProcessed);
        }

        var org = await _organizationRepository.GetByIdAsync(jr.OrganizationId, ct);
        if (org == null)
        {
            _logger.LogWarning("Organization {OrganizationId} not found for join request {JoinRequestId}", jr.OrganizationId, joinRequestId);
            return Result.Failure<JoinRequestDto>(DomainErrors.Organization.NotFound);
        }

        if (!await _organizationRepository.HasPermissionAsync(userId, jr.OrganizationId, MemberPermissions.ManageMembers, ct))
        {
            _logger.LogWarning("User {UserId} may not manage the members of organization {OrganizationId}", userId, jr.OrganizationId);
            return Result.Failure<JoinRequestDto>(DomainErrors.Permission.Missing(MemberPermissions.ManageMembers));
        }

        jr.Status = JoinRequestStatus.Rejected;
        jr.RespondedByUserId = userId;
        jr.RespondedAtUtc = DateTime.UtcNow;
        await _joinRequestRepository.UpdateAsync(jr, ct);

        _logger.LogInformation("Join request {JoinRequestId} rejected by user {UserId}", joinRequestId, userId);
        return Result.Success(ToDto(jr));
    }

    private static JoinRequestDto ToDto(JoinRequest j) =>
        new(j.Id, j.OrganizationId, j.Organization.Name, j.UserId, j.User.Name, j.User.Email, j.Status.ToString(), j.CreatedAtUtc);
}
