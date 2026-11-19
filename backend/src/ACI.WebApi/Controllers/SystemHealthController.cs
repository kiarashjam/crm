using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Filters;
using ACI.WebApi.Monitoring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// System health: what has gone wrong in the workspace, in one read, for the people who manage
/// its settings (see <see cref="ISystemHealth"/>).
/// </summary>
/// <remarks>
/// Its failures are the workspace's own; the background jobs and their one-line errors are the
/// server's, shown to every workspace. Trying something again is done with the endpoints that
/// already do it: a handoff's resend, an automation run's retry.
/// </remarks>
[ApiController]
[Route("api/system-health")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
public class SystemHealthController : ControllerBase
{
    private readonly ISystemHealth _health;
    private readonly ISiteStatus _sites;
    private readonly ICurrentUserService _currentUser;

    public SystemHealthController(ISystemHealth health, ISiteStatus sites, ICurrentUserService currentUser)
    {
        _health = health;
        _sites = sites;
        _currentUser = currentUser;
    }

    /// <summary>The workspace's health now: the verdict, each area's count and first rows, the jobs and the portal's last ping.</summary>
    [HttpGet]
    [Requires(MemberPermissions.ManageSettings, What = "see the workspace's system health")]
    public async Task<ActionResult<SystemHealthDto>> Get(CancellationToken ct)
    {
        if (_currentUser.UserId == null || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        return Ok(await _health.SnapshotAsync(org, DateTime.UtcNow, ct));
    }

    /// <summary>
    /// The live status of the sites the workspace depends on: CadenceE's own checks, and its
    /// member portal's /health when one is set up (Settings › Welcome &amp; portal).
    /// </summary>
    [HttpGet("sites")]
    [Requires(MemberPermissions.ManageSettings, What = "see the workspace's system health")]
    public async Task<ActionResult<SitesStatusDto>> Sites(CancellationToken ct)
    {
        if (_currentUser.UserId == null || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        Response.Headers.CacheControl = "no-store";
        return Ok(await _sites.ReadAsync(org, ct));
    }
}
