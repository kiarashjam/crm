using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// The audit trail: who changed what, and when. The server records lines itself
/// as records change; this controller reads them and accepts the occasional line
/// the client records. Scoped to the current workspace, or to the caller's own
/// records in a personal workspace.
/// </summary>
[ApiController]
[Route("api/audit")]
[Authorize]
[Produces("application/json")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    /// Initializes a new instance of the AuditController.
    /// </summary>
    public AuditController(IAuditService auditService, ICurrentUserService currentUser)
    {
        _auditService = auditService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Retrieves audit lines, newest first.
    /// </summary>
    /// <param name="entityType">Only lines about this kind of record (lead, contact, company, deal, task, …).</param>
    /// <param name="entityId">Only lines about this record.</param>
    /// <param name="limit">Maximum lines to return. Defaults to 100, max 500.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the audit lines.</response>
    /// <response code="400">The entity type is not one the trail records.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AuditEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<AuditEventDto>>> GetAuditLog(
        [FromQuery] string? entityType,
        [FromQuery] Guid? entityId,
        [FromQuery] int? limit,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        var result = await _auditService.GetAsync(
            userId.Value, _currentUser.CurrentOrganizationId, entityType, entityId, limit, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Records a line from the client. The actor and time are the server's; a
    /// Viewer is refused by the read-only middleware before this runs.
    /// </summary>
    /// <param name="request">What happened.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the recorded line.</response>
    /// <response code="400">The entity type or action is unknown, or the summary is empty.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost]
    [ProducesResponseType(typeof(AuditEventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuditEventDto>> Record(
        [FromBody] RecordAuditEventRequest request,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        var result = await _auditService.RecordAsync(
            userId.Value, _currentUser.UserName, _currentUser.CurrentOrganizationId, request, ct);
        return result.ToActionResult();
    }
}
