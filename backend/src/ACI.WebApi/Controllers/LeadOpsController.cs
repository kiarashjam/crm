using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Enums;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Lead scoring, routing, SLA and capture forms — the workspace's lead settings.
/// </summary>
/// <remarks>
/// <para>
/// Every endpoint here is workspace-wide, so every one needs a workspace. These
/// used to pass a missing X-Organization-Id straight through as null, and every
/// query then filtered "OrganizationId IS NULL" — one bucket shared by every user
/// without a workspace. Any of them could read the others' capture-form tokens and
/// re-route their leads to themselves.
/// </para>
/// <para>
/// Writes need an Owner or Manager, as every other workspace setting does
/// (statuses, sources, pipelines, stages). Without that a salesperson could post a
/// round-robin rule listing only themselves and receive every new lead.
/// </para>
/// </remarks>
[ApiController]
[Authorize]
[Route("api/lead-ops")]
public class LeadOpsController : ControllerBase
{
    private readonly ILeadOpsService _ops;
    private readonly ICurrentUserService _currentUser;
    private readonly IOrganizationRepository _organizations;

    public LeadOpsController(ILeadOpsService ops, ICurrentUserService currentUser, IOrganizationRepository organizations)
    {
        _ops = ops;
        _currentUser = currentUser;
        _organizations = organizations;
    }

    /// <summary>Signed in, with a workspace selected — or the reason not.</summary>
    private ActionResult? NoWorkspace(out Guid organizationId)
    {
        organizationId = _currentUser.CurrentOrganizationId ?? Guid.Empty;
        if (_currentUser.UserId == null) return Unauthorized();
        if (_currentUser.CurrentOrganizationId == null)
            return Problem(detail: "X-Organization-Id required", statusCode: StatusCodes.Status400BadRequest);
        return null;
    }

    /// <summary>An Owner or Manager of this workspace — or 403.</summary>
    private async Task<ActionResult?> NotManagerAsync(Guid organizationId, CancellationToken ct)
    {
        var role = await _organizations.GetMemberRoleAsync(_currentUser.UserId!.Value, organizationId, ct);
        return role is OrgMemberRole.Owner or OrgMemberRole.Manager ? null : Forbid();
    }

    [HttpGet("scoring")]
    public async Task<ActionResult<IReadOnlyList<ScoringRuleDto>>> Scoring(CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        return Ok(await _ops.GetScoringRulesAsync(org, ct));
    }

    [HttpPut("scoring")]
    public async Task<ActionResult<IReadOnlyList<ScoringRuleDto>>> SaveScoring([FromBody] SaveScoringRulesRequest request, CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        if (await NotManagerAsync(org, ct) is { } denied) return denied;
        return Ok(await _ops.SaveScoringRulesAsync(org, request.Rules, ct));
    }

    [HttpGet("assignment")]
    public async Task<ActionResult<IReadOnlyList<AssignmentRuleDto>>> Assignment(CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        return Ok(await _ops.GetAssignmentRulesAsync(org, ct));
    }

    [HttpPost("assignment")]
    public async Task<ActionResult<AssignmentRuleDto>> SaveAssignment([FromBody] AssignmentRuleDto rule, CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        if (await NotManagerAsync(org, ct) is { } denied) return denied;
        return Ok(await _ops.SaveAssignmentRuleAsync(org, rule, ct));
    }

    [HttpDelete("assignment/{id:guid}")]
    public async Task<IActionResult> DeleteAssignment(Guid id, CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        if (await NotManagerAsync(org, ct) is { } denied) return denied;
        await _ops.DeleteAssignmentRuleAsync(id, org, ct);
        return NoContent();
    }

    [HttpGet("territories")]
    public async Task<ActionResult<IReadOnlyList<TerritoryDto>>> Territories(CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        return Ok(await _ops.GetTerritoriesAsync(org, ct));
    }

    [HttpPost("territories")]
    public async Task<ActionResult<TerritoryDto>> SaveTerritory([FromBody] TerritoryDto territory, CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        if (await NotManagerAsync(org, ct) is { } denied) return denied;
        return Ok(await _ops.SaveTerritoryAsync(org, territory, ct));
    }

    [HttpDelete("territories/{id:guid}")]
    public async Task<IActionResult> DeleteTerritory(Guid id, CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        if (await NotManagerAsync(org, ct) is { } denied) return denied;
        await _ops.DeleteTerritoryAsync(id, org, ct);
        return NoContent();
    }

    [HttpGet("queues")]
    public async Task<ActionResult<IReadOnlyList<QueueDto>>> Queues(CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        return Ok(await _ops.GetQueuesAsync(org, ct));
    }

    [HttpPost("queues")]
    public async Task<ActionResult<QueueDto>> SaveQueue([FromBody] QueueDto queue, CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        if (await NotManagerAsync(org, ct) is { } denied) return denied;
        return Ok(await _ops.SaveQueueAsync(org, queue, ct));
    }

    [HttpDelete("queues/{id:guid}")]
    public async Task<IActionResult> DeleteQueue(Guid id, CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        if (await NotManagerAsync(org, ct) is { } denied) return denied;
        await _ops.DeleteQueueAsync(id, org, ct);
        return NoContent();
    }

    [HttpGet("sla")]
    public async Task<ActionResult<SlaSettingsDto>> Sla(CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        return Ok(await _ops.GetSlaAsync(org, ct));
    }

    [HttpPut("sla")]
    public async Task<ActionResult<SlaSettingsDto>> SaveSla([FromBody] SlaSettingsDto body, CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        if (await NotManagerAsync(org, ct) is { } denied) return denied;
        return Ok(await _ops.SetSlaAsync(org, body.Hours, ct));
    }

    [HttpGet("forms")]
    public async Task<ActionResult<IReadOnlyList<CaptureFormDto>>> Forms(CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        return Ok(await _ops.GetFormsAsync(org, ct));
    }

    [HttpPost("forms")]
    public async Task<ActionResult<CaptureFormDto>> CreateForm([FromBody] CaptureFormDto body, CancellationToken ct)
    {
        // A capture form is a public door into the workspace's lead list.
        if (NoWorkspace(out var org) is { } fail) return fail;
        if (await NotManagerAsync(org, ct) is { } denied) return denied;
        return Ok(await _ops.CreateFormAsync(_currentUser.UserId!.Value, org, body.Name, ct));
    }

    [HttpGet("conversion")]
    public async Task<ActionResult<ConversionReportDto>> Conversion(CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        return Ok(await _ops.ConversionReportAsync(org, ct));
    }

    // Bulk update and history act on individual leads, and go through the lead
    // repository's own scoping (organisation, or the user's own leads without one).

    [HttpPost("bulk")]
    public async Task<ActionResult<BulkLeadUpdateResult>> Bulk([FromBody] BulkLeadUpdateRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var result = await _ops.BulkUpdateAsync(userId.Value, _currentUser.CurrentOrganizationId, request, ct);
        return result.ToActionResult();
    }

    [HttpGet("history/{id:guid}")]
    public async Task<ActionResult<IReadOnlyList<LeadStatusEventDto>>> History(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        return Ok(await _ops.HistoryAsync(id, userId.Value, _currentUser.CurrentOrganizationId, ct));
    }
}
