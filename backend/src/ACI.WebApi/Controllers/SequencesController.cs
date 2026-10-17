using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Outreach sequences and the people enrolled in them.
/// </summary>
/// <remarks>
/// Sequences belong to a workspace, so every endpoint needs one. A missing
/// X-Organization-Id used to become a null organisation, and "OrganizationId IS
/// NULL" was one bucket shared by every user without a workspace — each could list
/// the others' enrollments, names and email addresses included. Enrollment actions
/// are also scoped to the workspace now; they used to act on any enrollment id.
/// </remarks>
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

    /// <summary>Signed in, with a workspace selected — or the reason not.</summary>
    private ActionResult? NoWorkspace(out Guid organizationId)
    {
        organizationId = _currentUser.CurrentOrganizationId ?? Guid.Empty;
        if (_currentUser.UserId == null) return Unauthorized();
        if (_currentUser.CurrentOrganizationId == null)
            return Problem(detail: "X-Organization-Id required", statusCode: StatusCodes.Status400BadRequest);
        return null;
    }

    [HttpGet("enrollments")]
    public async Task<ActionResult<IReadOnlyList<EnrollmentDto>>> Enrollments([FromQuery] Guid? sequenceId, CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        return Ok(await _sequences.EnrollmentsAsync(org, sequenceId, ct));
    }

    [HttpPost("enroll")]
    public async Task<ActionResult<EnrollmentDto>> Enroll([FromBody] EnrollRequest request, CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        var result = await _sequences.EnrollAsync(_currentUser.UserId!.Value, org, request, ct);
        return result.ToActionResult();
    }

    [HttpPatch("enrollments/{id:guid}")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] EnrollmentStatusRequest request, CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        var result = await _sequences.SetStatusAsync(id, org, request.Status, ct);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpDelete("enrollments/{id:guid}")]
    public async Task<IActionResult> Unenroll(Guid id, CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        return await _sequences.UnenrollAsync(id, org, ct) ? NoContent() : NotFound();
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SequenceDto>>> List(CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        return Ok(await _sequences.ListAsync(org, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SequenceDto>> Get(Guid id, CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        var row = await _sequences.GetAsync(id, org, ct);
        return row == null ? NotFound() : Ok(row);
    }

    [HttpPost]
    public async Task<ActionResult<SequenceDto>> Create([FromBody] SequenceWriteRequest request, CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        if (string.IsNullOrWhiteSpace(request.Name))
            return Problem(detail: "A sequence needs a name.", statusCode: StatusCodes.Status400BadRequest);
        return Ok(await _sequences.CreateAsync(_currentUser.UserId!.Value, org, request, ct));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SequenceDto>> Update(Guid id, [FromBody] SequenceWriteRequest request, CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        var row = await _sequences.UpdateAsync(id, org, request, ct);
        return row == null ? NotFound() : Ok(row);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (NoWorkspace(out var org) is { } fail) return fail;
        return await _sequences.DeleteAsync(id, org, ct) ? NoContent() : NotFound();
    }
}
