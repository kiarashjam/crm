using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Provides sales reporting and analytics data.
/// </summary>
/// <remarks>
/// Includes dashboard statistics, pipeline metrics, and performance reports.
/// All data is scoped to the current user's organization.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ReportingController : ControllerBase
{
    private readonly IReportingService _reportingService;
    private readonly ICurrentUserService _currentUser;

    public ReportingController(IReportingService reportingService, ICurrentUserService currentUser)
    {
        _reportingService = reportingService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Gets dashboard statistics summary.
    /// </summary>
    /// <remarks>
    /// Returns key metrics including:
    /// - Total leads, contacts, and deals
    /// - Pipeline value
    /// - Recent activity counts
    /// - Conversion rates
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dashboard statistics.</returns>
    /// <response code="200">Returns the dashboard statistics.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DashboardStatsDto>> GetDashboardStats(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var stats = await _reportingService.GetDashboardStatsAsync(userId.Value, _currentUser.CurrentOrganizationId, ct);
        return Ok(stats);
    }

    /// <summary>
    /// Gets pipeline value broken down by stage.
    /// </summary>
    /// <remarks>
    /// Shows the total value of deals at each stage in the pipeline.
    /// Useful for visualizing the sales funnel.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Pipeline value by stage.</returns>
    /// <response code="200">Returns pipeline stage values.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("pipeline-by-stage")]
    [ProducesResponseType(typeof(IReadOnlyList<PipelineStageValueDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<PipelineStageValueDto>>> GetPipelineByStage(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var list = await _reportingService.GetPipelineValueByStageAsync(userId.Value, _currentUser.CurrentOrganizationId, ct);
        return Ok(list);
    }

    /// <summary>
    /// Gets pipeline value broken down by assignee.
    /// </summary>
    /// <remarks>
    /// Shows the total value of deals assigned to each team member.
    /// Useful for tracking individual performance.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Pipeline value by assignee.</returns>
    /// <response code="200">Returns pipeline values by assignee.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("pipeline-by-assignee")]
    [ProducesResponseType(typeof(IReadOnlyList<PipelineValueByAssigneeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<PipelineValueByAssigneeDto>>> GetPipelineByAssignee(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var list = await _reportingService.GetPipelineValueByAssigneeAsync(userId.Value, _currentUser.CurrentOrganizationId, ct);
        return Ok(list);
    }
}
