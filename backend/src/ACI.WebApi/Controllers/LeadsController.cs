using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LeadsController : ControllerBase
{
    private readonly ILeadService _leadService;
    private readonly ICurrentUserService _currentUser;

    public LeadsController(ILeadService leadService, ICurrentUserService currentUser)
    {
        _leadService = leadService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LeadDto>>> GetLeads(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var list = await _leadService.GetLeadsAsync(userId.Value, ct);
        return Ok(list);
    }

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<LeadDto>>> Search([FromQuery] string q, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var list = await _leadService.SearchAsync(userId.Value, q ?? "", ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LeadDto>> GetById(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var lead = await _leadService.GetByIdAsync(id, userId.Value, ct);
        if (lead == null) return NotFound();
        return Ok(lead);
    }

    [HttpPost]
    public async Task<ActionResult<LeadDto>> Create([FromBody] CreateLeadRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var lead = await _leadService.CreateAsync(userId.Value, request, ct);
        return lead == null ? BadRequest() : Ok(lead);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<LeadDto>> Update(Guid id, [FromBody] UpdateLeadRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var lead = await _leadService.UpdateAsync(id, userId.Value, request, ct);
        if (lead == null) return NotFound();
        return Ok(lead);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var deleted = await _leadService.DeleteAsync(id, userId.Value, ct);
        if (!deleted) return NotFound();
        return NoContent();
    }

    [HttpPost("{id:guid}/convert")]
    public async Task<ActionResult<ConvertLeadResult>> Convert(Guid id, [FromBody] ConvertLeadRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var result = await _leadService.ConvertAsync(id, userId.Value, request, ct);
        if (result == null) return NotFound();
        return Ok(result);
    }
}
