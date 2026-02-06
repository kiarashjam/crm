using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

public sealed class JoinRequestService : IJoinRequestService
{
    private readonly IJoinRequestRepository _joinRequestRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ILogger<JoinRequestService> _logger;

    public JoinRequestService(
        IJoinRequestRepository joinRequestRepository,
        IOrganizationRepository organizationRepository,
        ILogger<JoinRequestService> logger)
    {
        _joinRequestRepository = joinRequestRepository;
        _organizationRepository = organizationRepository;
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

        if (org.OwnerUserId != userId)
        {
            _logger.LogWarning("User {UserId} is not owner of organization {OrganizationId}", userId, organizationId);
            return Result.Failure<IReadOnlyList<JoinRequestDto>>(DomainErrors.Organization.NotOwner);
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

        if (org.OwnerUserId != userId)
        {
            _logger.LogWarning("User {UserId} is not owner of organization {OrganizationId}", userId, jr.OrganizationId);
            return Result.Failure<JoinRequestDto>(DomainErrors.Organization.NotOwner);
        }

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

        if (org.OwnerUserId != userId)
        {
            _logger.LogWarning("User {UserId} is not owner of organization {OrganizationId}", userId, jr.OrganizationId);
            return Result.Failure<JoinRequestDto>(DomainErrors.Organization.NotOwner);
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
