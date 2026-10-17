using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/lead-ops")]
public class LeadOpsController : ControllerBase
{
    private readonly ILeadOpsService _ops;
    private readonly ICurrentUserService _currentUser;

    public LeadOpsController(ILeadOpsService ops, ICurrentUserService currentUser)
    {
        _ops = ops;
        _currentUser = currentUser;
    }

    [HttpGet("scoring")]
    public async Task<ActionResult<IReadOnlyList<ScoringRuleDto>>> Scoring(CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return Ok(await _ops.GetScoringRulesAsync(_currentUser.CurrentOrganizationId, ct));
    }

    [HttpPut("scoring")]
    public async Task<ActionResult<IReadOnlyList<ScoringRuleDto>>> SaveScoring([FromBody] SaveScoringRulesRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return Ok(await _ops.SaveScoringRulesAsync(_currentUser.CurrentOrganizationId, request.Rules, ct));
    }

    [HttpGet("assignment")]
    public async Task<ActionResult<IReadOnlyList<AssignmentRuleDto>>> Assignment(CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return Ok(await _ops.GetAssignmentRulesAsync(_currentUser.CurrentOrganizationId, ct));
    }

    [HttpPost("assignment")]
    public async Task<ActionResult<AssignmentRuleDto>> SaveAssignment([FromBody] AssignmentRuleDto rule, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return Ok(await _ops.SaveAssignmentRuleAsync(_currentUser.CurrentOrganizationId, rule, ct));
    }

    [HttpDelete("assignment/{id:guid}")]
    public async Task<IActionResult> DeleteAssignment(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        await _ops.DeleteAssignmentRuleAsync(id, _currentUser.CurrentOrganizationId, ct);
        return NoContent();
    }

    [HttpGet("territories")]
    public async Task<ActionResult<IReadOnlyList<TerritoryDto>>> Territories(CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return Ok(await _ops.GetTerritoriesAsync(_currentUser.CurrentOrganizationId, ct));
    }

    [HttpPost("territories")]
    public async Task<ActionResult<TerritoryDto>> SaveTerritory([FromBody] TerritoryDto territory, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return Ok(await _ops.SaveTerritoryAsync(_currentUser.CurrentOrganizationId, territory, ct));
    }

    [HttpDelete("territories/{id:guid}")]
    public async Task<IActionResult> DeleteTerritory(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        await _ops.DeleteTerritoryAsync(id, _currentUser.CurrentOrganizationId, ct);
        return NoContent();
    }

    [HttpGet("queues")]
    public async Task<ActionResult<IReadOnlyList<QueueDto>>> Queues(CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return Ok(await _ops.GetQueuesAsync(_currentUser.CurrentOrganizationId, ct));
    }

    [HttpPost("queues")]
    public async Task<ActionResult<QueueDto>> SaveQueue([FromBody] QueueDto queue, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return Ok(await _ops.SaveQueueAsync(_currentUser.CurrentOrganizationId, queue, ct));
    }

    [HttpDelete("queues/{id:guid}")]
    public async Task<IActionResult> DeleteQueue(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        await _ops.DeleteQueueAsync(id, _currentUser.CurrentOrganizationId, ct);
        return NoContent();
    }

    [HttpGet("sla")]
    public async Task<ActionResult<SlaSettingsDto>> Sla(CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return Ok(await _ops.GetSlaAsync(_currentUser.CurrentOrganizationId, ct));
    }

    [HttpPut("sla")]
    public async Task<ActionResult<SlaSettingsDto>> SaveSla([FromBody] SlaSettingsDto body, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return Ok(await _ops.SetSlaAsync(_currentUser.CurrentOrganizationId, body.Hours, ct));
    }

    [HttpGet("forms")]
    public async Task<ActionResult<IReadOnlyList<CaptureFormDto>>> Forms(CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return Ok(await _ops.GetFormsAsync(_currentUser.CurrentOrganizationId, ct));
    }

    [HttpPost("forms")]
    public async Task<ActionResult<CaptureFormDto>> CreateForm([FromBody] CaptureFormDto body, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        return Ok(await _ops.CreateFormAsync(userId.Value, _currentUser.CurrentOrganizationId, body.Name, ct));
    }

    [HttpGet("conversion")]
    public async Task<ActionResult<ConversionReportDto>> Conversion(CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return Ok(await _ops.ConversionReportAsync(_currentUser.CurrentOrganizationId, ct));
    }

    [HttpPost("bulk")]
    public async Task<ActionResult<BulkLeadUpdateResult>> Bulk([FromBody] BulkLeadUpdateRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        return Ok(await _ops.BulkUpdateAsync(userId.Value, _currentUser.CurrentOrganizationId, request, ct));
    }

    [HttpGet("history/{id:guid}")]
    public async Task<ActionResult<IReadOnlyList<LeadStatusEventDto>>> History(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        return Ok(await _ops.HistoryAsync(id, userId.Value, _currentUser.CurrentOrganizationId, ct));
    }
}
