using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Records entered more than once: duplicate contacts and companies, found on the server,
/// and the team's "not the same" decisions for every kind of record.
/// </summary>
/// <remarks>
/// A decision is the workspace's, not one person's or one browser's: once someone says two
/// records are not the same, nobody in the workspace is offered them again, until someone
/// asks to see them again.
/// </remarks>
[ApiController]
[Route("api/duplicates")]
[Authorize]
[Produces("application/json")]
public class DuplicatesController : ControllerBase
{
    private readonly IDuplicateService _duplicates;
    private readonly ICurrentUserService _currentUser;

    public DuplicatesController(IDuplicateService duplicates, ICurrentUserService currentUser)
    {
        _duplicates = duplicates;
        _currentUser = currentUser;
    }

    /// <summary>
    /// The workspace's duplicate contacts: the same email, the same phone however it is
    /// written, or the same name when neither phone nor company disagrees.
    /// </summary>
    /// <response code="200">The groups still to deal with, and those hidden by "not the same".</response>
    [HttpGet("contacts")]
    [ProducesResponseType(typeof(DuplicateScanDto<ContactDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<DuplicateScanDto<ContactDto>>> Contacts(CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return Ok(await _duplicates.ScanContactsAsync(userId, _currentUser.CurrentOrganizationId, ct));
    }

    /// <summary>The workspace's duplicate companies: the same web domain, or the same name when the domains do not disagree.</summary>
    /// <response code="200">The groups still to deal with, and those hidden by "not the same".</response>
    [HttpGet("companies")]
    [ProducesResponseType(typeof(DuplicateScanDto<CompanyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<DuplicateScanDto<CompanyDto>>> Companies(CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return Ok(await _duplicates.ScanCompaniesAsync(userId, _currentUser.CurrentOrganizationId, ct));
    }

    /// <summary>The pairs of this kind the team said are not the same: contacts, companies, leads, deals, members or households.</summary>
    /// <response code="200">The pairs.</response>
    /// <response code="400">Unknown kind.</response>
    [HttpGet("{kind}/not-same")]
    [ProducesResponseType(typeof(IReadOnlyList<DuplicatePairDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<DuplicatePairDto>>> NotSame(string kind, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var result = await _duplicates.NotSameAsync(kind, userId, _currentUser.CurrentOrganizationId, ct);
        return result.ToActionResult();
    }

    /// <summary>These records are not the same: every pair among them is kept apart, for the whole workspace.</summary>
    /// <response code="204">Recorded.</response>
    /// <response code="400">Unknown kind, fewer than two records or more than fifty, or records not in this workspace.</response>
    [HttpPost("{kind}/not-same")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> MarkNotSame(string kind, [FromBody] DuplicateDecisionRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var result = await _duplicates.MarkNotSameAsync(kind, request?.RecordIds ?? [], userId, _currentUser.CurrentOrganizationId, ct);
        return result.ToNoContentResult();
    }

    /// <summary>Offers these records as possible duplicates again: the decisions among them are forgotten.</summary>
    /// <response code="204">Forgotten.</response>
    /// <response code="400">Unknown kind, or fewer than two records or more than fifty.</response>
    [HttpPost("{kind}/not-same/clear")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ClearNotSame(string kind, [FromBody] DuplicateDecisionRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var result = await _duplicates.ClearNotSameAsync(kind, request?.RecordIds ?? [], userId, _currentUser.CurrentOrganizationId, ct);
        return result.ToNoContentResult();
    }
}
