using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// A person's data rights: look someone up across the workspace, download everything held
/// about them, and erase them.
/// </summary>
/// <remarks>
/// Everything here takes privacy.manage, held by the owner and managers: an export hands over
/// everything about someone at once, and an erasure cannot be undone. Lookups and previews
/// are POSTs because they carry an email address, which does not belong in a URL (and so in
/// server and proxy logs); they change nothing.
/// </remarks>
[ApiController]
[Route("api/privacy")]
[Authorize]
[WorkspaceRequired]
[Requires(MemberPermissions.ManagePrivacy, What = "handle privacy requests")]
[Produces("application/json")]
public class PrivacyController : ControllerBase
{
    private readonly IPersonData _people;
    private readonly ICurrentUserService _currentUser;

    public PrivacyController(IPersonData people, ICurrentUserService currentUser)
    {
        _people = people;
        _currentUser = currentUser;
    }

    /// <summary>Records that look like the person: by an email address, or from one of their records.</summary>
    /// <response code="200">The records found, each saying why, and the addresses involved.</response>
    /// <response code="404">The record started from is not in this workspace.</response>
    [HttpPost("find")]
    [ProducesResponseType(typeof(PersonFoundDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> Find([FromBody] PersonLookup lookup, CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        var result = await _people.FindAsync(org, lookup, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>Everything held about the chosen records, as a zip of JSON files with a readable summary.</summary>
    /// <response code="200">The zip. The download is written to the audit trail.</response>
    [HttpPost("export")]
    [Produces("application/zip")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<ActionResult> Export([FromBody] PersonSelection selection, CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org || _currentUser.UserId is not Guid user) return Unauthorized();
        var result = await _people.ExportAsync(org, user, selection, ct);
        if (!result.IsSuccess) return result.Error.ToProblemResult();
        Response.Headers.CacheControl = "no-store";
        return File(result.Value.Content, "application/zip", result.Value.FileName);
    }

    /// <summary>What erasing the chosen records would delete, anonymise, cancel and keep. Changes nothing.</summary>
    [HttpPost("erasure/preview")]
    [ProducesResponseType(typeof(ErasurePlanDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> Preview([FromBody] PersonSelection selection, CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        var result = await _people.PlanErasureAsync(org, selection, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>Erases the chosen records. The body must say <c>"confirm": "ERASE"</c>.</summary>
    /// <response code="200">What was done, and the pseudonym the books use for the person now.</response>
    /// <response code="400">Not confirmed, or a reference that names the person.</response>
    /// <response code="409">One of the records changed meanwhile; nothing was erased.</response>
    [HttpPost("erasure")]
    [ProducesResponseType(typeof(ErasureResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> Erase([FromBody] EraseRequest request, CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org || _currentUser.UserId is not Guid user) return Unauthorized();
        var result = await _people.EraseAsync(org, user, request, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>The log of erasures: numbers, dates, who confirmed them and counts. It names nobody.</summary>
    [HttpGet("erasures")]
    [ProducesResponseType(typeof(IReadOnlyList<PersonErasureDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PersonErasureDto>>> Erasures(CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        return Ok(await _people.ErasuresAsync(org, ct));
    }
}
