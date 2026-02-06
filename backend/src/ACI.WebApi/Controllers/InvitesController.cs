using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Manages organization invitations.
/// </summary>
/// <remarks>
/// Invites allow organization members to invite others to join.
/// Invited users can accept invites using the token or invite ID.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class InvitesController : ControllerBase
{
    private readonly IInviteService _inviteService;
    private readonly ICurrentUserService _currentUser;

    public InvitesController(IInviteService inviteService, ICurrentUserService currentUser)
    {
        _inviteService = inviteService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Lists pending invites for the current user.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of pending invites.</returns>
    /// <response code="200">Returns the list of pending invites.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(IReadOnlyList<InviteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<InviteDto>>> ListMyPending(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var result = await _inviteService.ListMyPendingInvitesAsync(userId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Accepts an invite using its token.
    /// </summary>
    /// <param name="request">Request containing the invite token.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The accepted invite details.</returns>
    /// <response code="200">Invite accepted successfully.</response>
    /// <response code="400">Invalid or expired token, or email mismatch.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost("accept")]
    [ProducesResponseType(typeof(InviteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<InviteDto>> Accept([FromBody] AcceptInviteRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var result = await _inviteService.AcceptInviteAsync(request.Token, userId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Accepts an invite by its ID.
    /// </summary>
    /// <param name="inviteId">The invite ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The accepted invite details.</returns>
    /// <response code="200">Invite accepted successfully.</response>
    /// <response code="400">Invalid or expired invite, or email mismatch.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost("{inviteId:guid}/accept")]
    [ProducesResponseType(typeof(InviteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<InviteDto>> AcceptById(Guid inviteId, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var result = await _inviteService.AcceptInviteByIdAsync(inviteId, userId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Lists pending invites for an organization.
    /// </summary>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of pending invites for the organization.</returns>
    /// <response code="200">Returns the list of pending invites.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not the organization owner.</response>
    [HttpGet("organization/{organizationId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<InviteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<InviteDto>>> ListPending(Guid organizationId, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var result = await _inviteService.ListPendingInvitesAsync(organizationId, userId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Creates a new invite to an organization.
    /// </summary>
    /// <remarks>
    /// Users can only invite to organizations they are members of.
    /// </remarks>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="request">Invite creation request with email and optional role.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created invite.</returns>
    /// <response code="200">Invite created successfully.</response>
    /// <response code="400">Not allowed or invalid (e.g., already a member).</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not the organization owner.</response>
    [HttpPost("{organizationId:guid}")]
    [ProducesResponseType(typeof(InviteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<InviteDto>> Create(Guid organizationId, [FromBody] CreateInviteRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var result = await _inviteService.CreateInviteAsync(organizationId, userId.Value, request, ct);
        return result.ToActionResult();
    }
}
