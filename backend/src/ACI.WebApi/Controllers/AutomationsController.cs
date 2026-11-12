using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.WebApi.Extensions;
using ACI.Domain.Common;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Workspace rules: when something happens, check the record, then do the stored steps.
/// </summary>
/// <remarks>
/// A rule can reassign records and send mail for the whole workspace, so changing one
/// takes managing the workspace's settings (the owner and managers by default), as lead routing does. Everyone can see them.
/// What the rules did (their runs and the log of each) is for the same people: a run names
/// the record it was about and the emails it queued.
/// </remarks>
[ApiController]
[Route("api/automations")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
public class AutomationsController : ControllerBase
{
    private const int DefaultRuns = 50;

    private readonly IAutomationService _automations;
    private readonly ICurrentUserService _currentUser;

    public AutomationsController(IAutomationService automations, ICurrentUserService currentUser)
    {
        _automations = automations;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AutomationRuleDto>>> List(CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return Ok(await _automations.ListAsync(_currentUser.CurrentOrganizationId, ct));
    }

    /// <summary>The triggers, what their conditions can test, the dates their waits can wait for and the steps they can take.</summary>
    [HttpGet("catalog")]
    public ActionResult<AutomationCatalogDto> Catalog() => Ok(_automations.Catalog());

    [HttpPost]
    [Requires(MemberPermissions.ManageSettings, What = "change automations")]
    public async Task<ActionResult<AutomationRuleDto>> Create([FromBody] AutomationWriteRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var result = await _automations.CreateAsync(userId, _currentUser.CurrentOrganizationId, request, ct);
        if (result.IsSuccess) Response.SetETag(result.Value.Version);
        return result.ToActionResult();
    }

    /// <summary>Changes a rule. Send the version it was read at (the body's or If-Match): an edit of an older one answers 409.</summary>
    [HttpPut("{id:guid}")]
    [Requires(MemberPermissions.ManageSettings, What = "change automations")]
    public async Task<ActionResult<AutomationRuleDto>> Update(Guid id, [FromBody] AutomationWriteRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        var (version, unreadable) = Request.ExpectedVersion(request.Version);
        if (unreadable != null) return unreadable.ToProblemResult();
        request.Version = version;
        var result = await _automations.UpdateAsync(id, _currentUser.CurrentOrganizationId, request, ct);
        if (result.IsSuccess) Response.SetETag(result.Value.Version);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    [Requires(MemberPermissions.ManageSettings, What = "change automations")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return (await _automations.DeleteAsync(id, _currentUser.CurrentOrganizationId, ct)).ToNoContentResult();
    }

    /// <summary>
    /// The latest runs, newest first: of one rule, and/or in one state (active for the ones
    /// still going, failed, completed, cancelled).
    /// </summary>
    [HttpGet("runs")]
    [Requires(MemberPermissions.ManageSettings, What = "see what automations did")]
    public async Task<ActionResult<IReadOnlyList<AutomationRunDto>>> Runs([FromQuery] Guid? ruleId, [FromQuery] string? status, [FromQuery] int? take, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return Ok(await _automations.RunsAsync(ruleId, status?.Trim().ToLowerInvariant(), take ?? DefaultRuns, ct));
    }

    /// <summary>A run with the log of its steps, and where each email it queued stands in the outbox.</summary>
    [HttpGet("runs/{runId:guid}")]
    [Requires(MemberPermissions.ManageSettings, What = "see what automations did")]
    public async Task<ActionResult<AutomationRunDetailDto>> Run(Guid runId, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return (await _automations.RunAsync(runId, ct)).ToActionResult();
    }

    /// <summary>Tries a failed run's failed step again, now.</summary>
    [HttpPost("runs/{runId:guid}/retry")]
    [Requires(MemberPermissions.ManageSettings, What = "change automations")]
    public async Task<ActionResult<AutomationRunDto>> Retry(Guid runId, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return (await _automations.RetryAsync(runId, ct)).ToActionResult();
    }

    /// <summary>Stops a run that has not finished: what it was still to do is not done.</summary>
    [HttpPost("runs/{runId:guid}/cancel")]
    [Requires(MemberPermissions.ManageSettings, What = "change automations")]
    public async Task<ActionResult<AutomationRunDto>> Cancel(Guid runId, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return (await _automations.CancelAsync(runId, ct)).ToActionResult();
    }
}
