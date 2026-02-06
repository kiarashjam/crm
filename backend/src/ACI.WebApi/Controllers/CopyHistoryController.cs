using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Manages copy generation history and statistics.
/// </summary>
/// <remarks>
/// Tracks all generated and sent copy for analytics and audit purposes.
/// Statistics include copy counts by type, success rates, and usage trends.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class CopyHistoryController : ControllerBase
{
    private readonly ICopyHistoryService _copyHistoryService;
    private readonly ICurrentUserService _currentUser;

    public CopyHistoryController(ICopyHistoryService copyHistoryService, ICurrentUserService currentUser)
    {
        _copyHistoryService = copyHistoryService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Gets the copy generation history for the current user.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of copy history items.</returns>
    /// <response code="200">Returns the copy history.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CopyHistoryItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<CopyHistoryItemDto>>> GetHistory(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var list = await _copyHistoryService.GetHistoryAsync(userId.Value, _currentUser.CurrentOrganizationId, ct);
        return Ok(list);
    }

    /// <summary>
    /// Gets copy generation statistics for the current user.
    /// </summary>
    /// <remarks>
    /// Returns aggregated statistics including:
    /// - Total copies generated
    /// - Copies by type (email, note, message)
    /// - Recent activity trends
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Copy generation statistics.</returns>
    /// <response code="200">Returns the statistics.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(CopyHistoryStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CopyHistoryStatsDto>> GetStats(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var stats = await _copyHistoryService.GetStatsAsync(userId.Value, _currentUser.CurrentOrganizationId, ct);
        return Ok(stats);
    }
}
