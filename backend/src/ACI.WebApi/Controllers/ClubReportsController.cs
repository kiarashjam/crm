using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// The club's figures, summed on the server: Club Reports (revenue, receivables, gift cards,
/// loyalty points, subscriptions, events) and Today at the Club.
/// </summary>
/// <remarks>
/// Reading them takes being a member of the workspace, as the money pages do; the
/// spreadsheet takes exporting lists, as every export does.
/// </remarks>
[ApiController]
[Route("api/club-reports")]
[Authorize]
[WorkspaceRequired]
[InModule(WorkspaceModules.Club)]
[Produces("application/json")]
public class ClubReportsController : ControllerBase
{
    private readonly IClubReports _reports;
    private readonly ICurrentUserService _currentUser;
    private readonly IWorkspaceCsv _csv;

    public ClubReportsController(IClubReports reports, ICurrentUserService currentUser, IWorkspaceCsv csv)
    {
        _reports = reports;
        _currentUser = currentUser;
        _csv = csv;
    }

    /// <summary>Every figure on Club Reports for a period; left out, the last twelve months by month.</summary>
    /// <param name="from">The first instant, e.g. the workspace's midnight on the first day.</param>
    /// <param name="to">The instant after the last.</param>
    /// <param name="grain">day, week or month; left out, the one that suits the period's length.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The figures, every amount per currency.</response>
    /// <response code="400">The period ends before it starts or is longer than three years, or the grain is not day, week or month.</response>
    [HttpGet]
    [ProducesResponseType(typeof(ClubReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<object>> Get([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] string? grain, CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        var result = await _reports.ReportAsync(org, new ClubReportQuery(from?.UtcDateTime, to?.UtcDateTime, grain), ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>The same figures as a spreadsheet (CSV), one figure per row.</summary>
    /// <response code="200">The CSV file.</response>
    [HttpGet("export")]
    [Requires(MemberPermissions.ExportRecords, What = "export the club's reports")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<ActionResult> Export([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] string? grain, CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        var result = await _reports.ReportAsync(org, new ClubReportQuery(from?.UtcDateTime, to?.UtcDateTime, grain), ct);
        if (!result.IsSuccess) return result.Error.ToProblemResult();
        var report = result.Value;
        var separator = await _csv.SeparatorAsync(org, ct);
        return File(ClubReportCsv.Write(report, DateTime.UtcNow, separator), "text/csv; charset=utf-8", ClubReportCsv.FileName(report));
    }

    /// <summary>What the club's day holds, on the workspace's calendar.</summary>
    /// <response code="200">Check-ins, bookings, open tabs, applications, what is past due, the week's events and anniversaries.</response>
    [HttpGet("today")]
    [ProducesResponseType(typeof(ClubTodayDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> Today(CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        var result = await _reports.TodayAsync(org, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }
}
