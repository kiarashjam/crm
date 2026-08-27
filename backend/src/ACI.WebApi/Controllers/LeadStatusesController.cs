using System.ComponentModel.DataAnnotations;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Enums;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Manages custom lead statuses for an organization.
/// </summary>
/// <remarks>
/// Lead statuses define the lifecycle stages of a lead (e.g., New, Contacted, Qualified).
/// Only organization owners and managers can create, update, or delete statuses.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class LeadStatusesController : ControllerBase
{
    private readonly ILeadStatusService _leadStatusService;
    private readonly ICurrentUserService _currentUser;
    private readonly IOrganizationRepository _organizationRepository;

    /// <summary>
    /// Initializes a new instance of the LeadStatusesController.
    /// </summary>
    public LeadStatusesController(ILeadStatusService leadStatusService, ICurrentUserService currentUser, IOrganizationRepository organizationRepository)
    {
        _leadStatusService = leadStatusService;
        _currentUser = currentUser;
        _organizationRepository = organizationRepository;
    }

    private async Task<ActionResult?> RequireOrgOwnerOrManager(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var orgId = _currentUser.CurrentOrganizationId;
        if (orgId == null) return Problem(detail: "X-Organization-Id required", statusCode: StatusCodes.Status400BadRequest);
        var role = await _organizationRepository.GetMemberRoleAsync(userId.Value, orgId.Value, ct);
        if (role != OrgMemberRole.Owner && role != OrgMemberRole.Manager)
            return Forbid();
        return null;
    }

    /// <summary>
    /// Gets all lead statuses for the organization.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of lead statuses.</returns>
    /// <response code="200">Returns the list of statuses.</response>
    /// <response code="400">X-Organization-Id header is required.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<LeadStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<LeadStatusDto>>> GetLeadStatuses(CancellationToken ct)
    {
        var orgId = _currentUser.CurrentOrganizationId;
        if (orgId == null) return Problem(detail: "X-Organization-Id required", statusCode: StatusCodes.Status400BadRequest);
        // ToActionResult, NOT Ok(): the service returns Result<T>, and Ok() serialises
        // the WRAPPER — {"isSuccess":true,"value":[...]} — where the client expects
        // the array. This was the whole reason a pipeline step never moved a lead's
        // status: `Array.isArray(list)` is false for an object, so the frontend read
        // every response as an empty status list, held the status write back, and
        // said nothing. The other three org-config controllers had it right; only
        // this one did not, which is why only the status was affected.
        var result = await _leadStatusService.GetByOrganizationIdAsync(orgId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Gets a specific lead status by ID.
    /// </summary>
    /// <param name="id">The status ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The lead status if found.</returns>
    /// <response code="200">Returns the status.</response>
    /// <response code="400">X-Organization-Id header is required.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Status not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LeadStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeadStatusDto>> GetById(Guid id, CancellationToken ct)
    {
        var orgId = _currentUser.CurrentOrganizationId;
        if (orgId == null) return Problem(detail: "X-Organization-Id required", statusCode: StatusCodes.Status400BadRequest);
        // The `status == null` check this replaces could never fire — Result<T> is a
        // class and is never null — so a genuine not-found returned 200 carrying a
        // failure envelope. ToActionResult maps the error to a real status code.
        var result = await _leadStatusService.GetByIdAsync(id, orgId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Creates a new lead status.
    /// </summary>
    /// <remarks>
    /// Only organization owners and managers can create statuses.
    /// </remarks>
    /// <param name="request">Status creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created status.</returns>
    /// <response code="200">Status created successfully.</response>
    /// <response code="400">Invalid request or X-Organization-Id required.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not an owner or manager.</response>
    [HttpPost]
    [ProducesResponseType(typeof(LeadStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LeadStatusDto>> Create([FromBody] CreateLeadStatusRequest request, CancellationToken ct)
    {
        var fail = await RequireOrgOwnerOrManager(ct);
        if (fail != null) return fail;
        var orgId = _currentUser.CurrentOrganizationId!.Value;
        var result = await _leadStatusService.CreateAsync(orgId, request.Name, request.DisplayOrder, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Updates an existing lead status.
    /// </summary>
    /// <remarks>
    /// Only organization owners and managers can update statuses.
    /// </remarks>
    /// <param name="id">The status ID to update.</param>
    /// <param name="request">Status update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated status.</returns>
    /// <response code="200">Status updated successfully.</response>
    /// <response code="400">X-Organization-Id header is required.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not an owner or manager.</response>
    /// <response code="404">Status not found.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(LeadStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeadStatusDto>> Update(Guid id, [FromBody] UpdateLeadStatusRequest request, CancellationToken ct)
    {
        var fail = await RequireOrgOwnerOrManager(ct);
        if (fail != null) return fail;
        var orgId = _currentUser.CurrentOrganizationId!.Value;
        var result = await _leadStatusService.UpdateAsync(id, orgId, request.Name, request.DisplayOrder, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Deletes a lead status.
    /// </summary>
    /// <remarks>
    /// Only organization owners and managers can delete statuses.
    /// Statuses with existing leads may need to be reassigned first.
    /// </remarks>
    /// <param name="id">The status ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Status deleted successfully.</response>
    /// <response code="400">X-Organization-Id header is required.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not an owner or manager.</response>
    /// <response code="404">Status not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var fail = await RequireOrgOwnerOrManager(ct);
        if (fail != null) return fail;
        var orgId = _currentUser.CurrentOrganizationId!.Value;
        var result = await _leadStatusService.DeleteAsync(id, orgId, ct);
        return result.ToNoContentResult();
    }
}

/// <summary>
/// Request to create a new lead status.
/// </summary>
public record CreateLeadStatusRequest
{
    /// <summary>
    /// Status name.
    /// </summary>
    [Required(ErrorMessage = "Name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
    public required string Name { get; init; }

    /// <summary>
    /// Display order.
    /// </summary>
    public int DisplayOrder { get; init; } = 0;
}

/// <summary>
/// Request to update a lead status.
/// </summary>
public record UpdateLeadStatusRequest
{
    /// <summary>
    /// New status name (optional).
    /// </summary>
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
    public string? Name { get; init; }

    /// <summary>
    /// New display order (optional).
    /// </summary>
    public int? DisplayOrder { get; init; }
}
