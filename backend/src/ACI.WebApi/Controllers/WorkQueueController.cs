using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// The signed-in rep's work: their open tasks, leads and deals, or with scope=team the
/// whole workspace's. The assistant's "do this next" queue is worked out from it.
/// </summary>
[ApiController]
[Route("api/work-queue")]
[Authorize]
[Produces("application/json")]
public class WorkQueueController : ControllerBase
{
    private readonly IWorkQueueService _queue;
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    /// Initializes a new instance of the WorkQueueController.
    /// </summary>
    public WorkQueueController(IWorkQueueService queue, ICurrentUserService currentUser)
    {
        _queue = queue;
        _currentUser = currentUser;
    }

    /// <summary>
    /// The open work that is the caller's, or the team's.
    /// </summary>
    /// <param name="scope">"mine" (the default): given to the caller, or made by them and given to nobody. "team": everyone's.</param>
    /// <param name="limit">The most tasks, leads and deals to return of each, up to 1000. Defaults to 500.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The open tasks, leads and deals.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(WorkQueueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<WorkQueueDto>> Get(
        [FromQuery] string? scope = null,
        [FromQuery] int limit = WorkQueueService.DefaultPerList,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        return Ok(await _queue.GetAsync(userId.Value, _currentUser.CurrentOrganizationId, scope, limit, ct));
    }
}
