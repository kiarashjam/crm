using System.Globalization;
using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Infrastructure.Services;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
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

    /// <summary>
    /// Every figure on the Reports page, for a period, whose records, a pipeline and a currency.
    /// </summary>
    /// <param name="from">Start of the period, an instant (the client sends its own midnight). Default: the 1st of this month, UTC.</param>
    /// <param name="to">End of the period, exclusive. Default: now.</param>
    /// <param name="scope">me, team or org (default).</param>
    /// <param name="teamId">With scope=team: the team. Default: the caller's own.</param>
    /// <param name="pipelineId">Deals in this pipeline only.</param>
    /// <param name="currency">Deals in this currency only.</param>
    /// <param name="tz">IANA time zone the charts count days in, e.g. Europe/Zurich. Default: UTC.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// Money is listed per currency and never added across currencies. Private deals,
    /// and activities logged on them, are left out for whoever may not see them.
    /// </remarks>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ReportSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> GetSummary(
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] string? scope,
        [FromQuery] Guid? teamId, [FromQuery] Guid? pipelineId, [FromQuery] string? currency, [FromQuery] string? tz,
        CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        if (!ReportingService.TryParseScope(scope, out var parsedScope)) return DomainErrors.Reporting.InvalidScope.ToProblemResult();
        var now = DateTime.UtcNow;
        var result = await _reportingService.GetSummaryAsync(userId, _currentUser.CurrentOrganizationId, new ReportFilter
        {
            FromUtc = from?.UtcDateTime ?? new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = to?.UtcDateTime ?? now,
            Scope = parsedScope,
            TeamId = teamId,
            PipelineId = pipelineId,
            Currency = currency,
            TimeZone = tz,
        }, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>Open deals by the month they are expected to close in, beside what was won each month.</summary>
    /// <param name="from">First month, as yyyy-MM. Default: this month (UTC).</param>
    /// <param name="months">How many months, 1 to 24. Default 6.</param>
    /// <param name="scope">me, team or org (default).</param>
    /// <param name="teamId">With scope=team: the team. Default: the caller's own.</param>
    /// <param name="pipelineId">Deals in this pipeline only.</param>
    /// <param name="currency">Deals in this currency only.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("forecast")]
    [ProducesResponseType(typeof(ForecastDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> GetForecast(
        [FromQuery] string? from, [FromQuery] int? months, [FromQuery] string? scope,
        [FromQuery] Guid? teamId, [FromQuery] Guid? pipelineId, [FromQuery] string? currency, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        if (!ReportingService.TryParseScope(scope, out var parsedScope)) return DomainErrors.Reporting.InvalidScope.ToProblemResult();
        var now = DateTime.UtcNow;
        var first = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!DateTime.TryParseExact(from.Trim(), "yyyy-MM", CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
                return DomainErrors.Reporting.InvalidMonth.ToProblemResult();
            first = new DateTime(parsed.Year, parsed.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }
        var result = await _reportingService.GetForecastAsync(userId, _currentUser.CurrentOrganizationId, new ForecastFilter
        {
            FirstMonthUtc = first,
            Months = months ?? 6,
            Scope = parsedScope,
            TeamId = teamId,
            PipelineId = pipelineId,
            Currency = currency,
        }, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>
    /// Saves a month's forecast (default this month, UTC) so later edits to deals do not
    /// rewrite it. Earlier saves for the month are kept.
    /// </summary>
    /// <remarks>
    /// A manager's forecast counts private deals, so saved forecasts are for managers to
    /// save and to read; a salesperson would otherwise see a private deal's amount and
    /// owner in the totals. The month is the saver's own, so the browser sends it: at
    /// half past midnight on the 1st in Zurich, UTC is still on the 31st.
    /// </remarks>
    [HttpPost("forecast-snapshot")]
    [ManagersOnly("save the forecast")]
    [ProducesResponseType(typeof(ForecastSnapshotSetDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> SaveForecast([FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var result = await _reportingService.SaveForecastSnapshotAsync(userId, _currentUser.CurrentOrganizationId, year, month, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>A month's saved forecasts: every save, one save's lines (the latest unless another is named), and what was won.</summary>
    [HttpGet("forecast-snapshots")]
    [ManagersOnly("see saved forecasts")]
    [ProducesResponseType(typeof(ForecastSnapshotSetDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> ListForecastSaves(
        [FromQuery] int? year, [FromQuery] int? month, [FromQuery] Guid? snapshotId, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var now = DateTime.UtcNow;
        var result = await _reportingService.ListForecastSnapshotsAsync(
            userId, _currentUser.CurrentOrganizationId, year ?? now.Year, month ?? now.Month, snapshotId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>The lines of a month's latest saved forecast, as this endpoint always returned them.</summary>
    [HttpGet("forecast-snapshot")]
    [ManagersOnly("see saved forecasts")]
    public async Task<ActionResult<object>> ListForecast([FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var now = DateTime.UtcNow;
        var result = await _reportingService.ListForecastSnapshotsAsync(
            userId, _currentUser.CurrentOrganizationId, year ?? now.Year, month ?? now.Month, null, ct);
        return result.IsSuccess ? Ok(result.Value.Rows) : result.Error.ToProblemResult();
    }
}
