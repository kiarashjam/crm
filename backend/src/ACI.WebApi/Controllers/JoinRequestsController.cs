using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Manages organization join requests.
/// </summary>
/// <remarks>
/// Users can request to join an organization. Members who manage the team (owners and managers by default)
/// can then accept or reject these requests. This provides an alternative to
/// the invite flow where users initiate the request.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class JoinRequestsController : ControllerBase
{
    private readonly IJoinRequestService _joinRequestService;
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    /// Initializes a new instance of the JoinRequestsController.
    /// </summary>
    public JoinRequestsController(IJoinRequestService joinRequestService, ICurrentUserService currentUser)
    {
        _joinRequestService = joinRequestService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Creates a join request for an organization.
    /// </summary>
    /// <remarks>
    /// The current user will request to join the specified organization.
    /// Returns an error if the user is already a member or has a pending request.
    /// </remarks>
    /// <param name="organizationId">The organization to request to join.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created join request.</returns>
    /// <response code="200">Request created successfully.</response>
    /// <response code="400">Already a member or request exists.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="409">A pending request already exists.</response>
    [HttpPost("{organizationId:guid}")]
    [ProducesResponseType(typeof(JoinRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JoinRequestDto>> Create(Guid organizationId, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var result = await _joinRequestService.CreateJoinRequestAsync(organizationId, userId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Lists pending join requests for an organization.
    /// </summary>
    /// <remarks>
    /// Only members who manage the team (owners and managers by default) can view pending requests.
    /// </remarks>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of pending join requests.</returns>
    /// <response code="200">Returns the list of pending requests.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not the organization owner.</response>
    [HttpGet("organization/{organizationId:guid}")]
    [ActsForRouteOrganization("organizationId")]
    [ProducesResponseType(typeof(IReadOnlyList<JoinRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<JoinRequestDto>>> ListPending(Guid organizationId, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var result = await _joinRequestService.ListPendingJoinRequestsAsync(organizationId, userId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Accepts a join request.
    /// </summary>
    /// <remarks>
    /// The requesting user will be added as a member of the organization.
    /// Only members who manage the team (owners and managers by default) can accept requests.
    /// </remarks>
    /// <param name="joinRequestId">The join request ID.</param>
    /// <param name="joinRequests">Finds the request's workspace, which may require single sign-on of the caller.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated join request.</returns>
    /// <response code="200">Request accepted successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not the organization owner.</response>
    /// <response code="404">Request not found.</response>
    /// <response code="409">Request already processed.</response>
    [HttpPost("{joinRequestId:guid}/accept")]
    [ProducesResponseType(typeof(JoinRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JoinRequestDto>> Accept(Guid joinRequestId, [FromServices] IJoinRequestRepository joinRequests, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        if (await SsoGate.RefusalAsync(HttpContext, (await joinRequests.GetByIdAsync(joinRequestId, ct))?.OrganizationId, ct) is { } refusal)
            return (ActionResult)refusal;
        var result = await _joinRequestService.AcceptJoinRequestAsync(joinRequestId, userId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Rejects a join request.
    /// </summary>
    /// <remarks>
    /// The requesting user will not be added to the organization.
    /// Only members who manage the team (owners and managers by default) can reject requests.
    /// </remarks>
    /// <param name="joinRequestId">The join request ID.</param>
    /// <param name="joinRequests">Finds the request's workspace, which may require single sign-on of the caller.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated join request.</returns>
    /// <response code="200">Request rejected successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not the organization owner.</response>
    /// <response code="404">Request not found.</response>
    /// <response code="409">Request already processed.</response>
    [HttpPost("{joinRequestId:guid}/reject")]
    [ProducesResponseType(typeof(JoinRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JoinRequestDto>> Reject(Guid joinRequestId, [FromServices] IJoinRequestRepository joinRequests, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        if (await SsoGate.RefusalAsync(HttpContext, (await joinRequests.GetByIdAsync(joinRequestId, ct))?.OrganizationId, ct) is { } refusal)
            return (ActionResult)refusal;
        var result = await _joinRequestService.RejectJoinRequestAsync(joinRequestId, userId.Value, ct);
        return result.ToActionResult();
    }
}
