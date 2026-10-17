using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

[ApiController]
[Authorize]
[Route("api/sequences")]
public class SequencesController : ControllerBase
{
    private readonly ISequenceService _sequences;
    private readonly ICurrentUserService _currentUser;

    public SequencesController(ISequenceService sequences, ICurrentUserService currentUser)
    {
        _sequences = sequences;
        _currentUser = currentUser;
    }

    [HttpGet("enrollments")]
    public async Task<ActionResult<IReadOnlyList<EnrollmentDto>>> Enrollments([FromQuery] Guid? sequenceId, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return Ok(await _sequences.EnrollmentsAsync(_currentUser.CurrentOrganizationId, sequenceId, ct));
    }

    [HttpPost("enroll")]
    public async Task<ActionResult<EnrollmentDto>> Enroll([FromBody] EnrollRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var created = await _sequences.EnrollAsync(userId.Value, _currentUser.CurrentOrganizationId, request, ct);
        return created == null ? NotFound() : Ok(created);
    }

    [HttpPatch("enrollments/{id:guid}")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] EnrollmentStatusRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return await _sequences.SetStatusAsync(id, request.Status, ct) ? NoContent() : NotFound();
    }

    [HttpDelete("enrollments/{id:guid}")]
    public async Task<IActionResult> Unenroll(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return await _sequences.UnenrollAsync(id, ct) ? NoContent() : NotFound();
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SequenceDto>>> List(CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return Ok(await _sequences.ListAsync(_currentUser.CurrentOrganizationId, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SequenceDto>> Get(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        var row = await _sequences.GetAsync(id, _currentUser.CurrentOrganizationId, ct);
        return row == null ? NotFound() : Ok(row);
    }

    [HttpPost]
    public async Task<ActionResult<SequenceDto>> Create([FromBody] SequenceWriteRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest();
        return Ok(await _sequences.CreateAsync(userId.Value, _currentUser.CurrentOrganizationId, request, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SequenceDto>> Update(Guid id, [FromBody] SequenceWriteRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        var row = await _sequences.UpdateAsync(id, _currentUser.CurrentOrganizationId, request, ct);
        return row == null ? NotFound() : Ok(row);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return await _sequences.DeleteAsync(id, _currentUser.CurrentOrganizationId, ct) ? NoContent() : NotFound();
    }
}
