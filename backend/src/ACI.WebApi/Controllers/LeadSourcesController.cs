using System.ComponentModel.DataAnnotations;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Enums;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Manages custom lead sources for an organization.
/// </summary>
/// <remarks>
/// Lead sources track where leads originate from (e.g., Website, Referral, Trade Show).
/// Only organization owners and managers can create, update, or delete sources.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class LeadSourcesController : ControllerBase
{
    private readonly ILeadSourceService _leadSourceService;
    private readonly ICurrentUserService _currentUser;
    private readonly IOrganizationRepository _organizationRepository;

    /// <summary>
    /// Initializes a new instance of the LeadSourcesController.
    /// </summary>
    public LeadSourcesController(ILeadSourceService leadSourceService, ICurrentUserService currentUser, IOrganizationRepository organizationRepository)
    {
        _leadSourceService = leadSourceService;
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
    /// Gets all lead sources for the organization.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of lead sources.</returns>
    /// <response code="200">Returns the list of sources.</response>
    /// <response code="400">X-Organization-Id header is required.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<LeadSourceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<LeadSourceDto>>> GetLeadSources(CancellationToken ct)
    {
        var orgId = _currentUser.CurrentOrganizationId;
        if (orgId == null) return Problem(detail: "X-Organization-Id required", statusCode: StatusCodes.Status400BadRequest);
        var result = await _leadSourceService.GetByOrganizationIdAsync(orgId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Gets a specific lead source by ID.
    /// </summary>
    /// <param name="id">The source ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The lead source if found.</returns>
    /// <response code="200">Returns the source.</response>
    /// <response code="400">X-Organization-Id header is required.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Source not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LeadSourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeadSourceDto>> GetById(Guid id, CancellationToken ct)
    {
        var orgId = _currentUser.CurrentOrganizationId;
        if (orgId == null) return Problem(detail: "X-Organization-Id required", statusCode: StatusCodes.Status400BadRequest);
        var result = await _leadSourceService.GetByIdAsync(id, orgId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Creates a new lead source.
    /// </summary>
    /// <remarks>
    /// Only organization owners and managers can create sources.
    /// </remarks>
    /// <param name="request">Source creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created source.</returns>
    /// <response code="200">Source created successfully.</response>
    /// <response code="400">Invalid request or X-Organization-Id required.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not an owner or manager.</response>
    [HttpPost]
    [ProducesResponseType(typeof(LeadSourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LeadSourceDto>> Create([FromBody] CreateLeadSourceRequest request, CancellationToken ct)
    {
        var fail = await RequireOrgOwnerOrManager(ct);
        if (fail != null) return fail;
        var orgId = _currentUser.CurrentOrganizationId!.Value;
        var result = await _leadSourceService.CreateAsync(orgId, request.Name, request.DisplayOrder, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Updates an existing lead source.
    /// </summary>
    /// <remarks>
    /// Only organization owners and managers can update sources.
    /// </remarks>
    /// <param name="id">The source ID to update.</param>
    /// <param name="request">Source update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated source.</returns>
    /// <response code="200">Source updated successfully.</response>
    /// <response code="400">X-Organization-Id header is required.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not an owner or manager.</response>
    /// <response code="404">Source not found.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(LeadSourceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeadSourceDto>> Update(Guid id, [FromBody] UpdateLeadSourceRequest request, CancellationToken ct)
    {
        var fail = await RequireOrgOwnerOrManager(ct);
        if (fail != null) return fail;
        var orgId = _currentUser.CurrentOrganizationId!.Value;
        var result = await _leadSourceService.UpdateAsync(id, orgId, request.Name, request.DisplayOrder, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Deletes a lead source.
    /// </summary>
    /// <remarks>
    /// Only organization owners and managers can delete sources.
    /// Sources with existing leads may need to be reassigned first.
    /// </remarks>
    /// <param name="id">The source ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Source deleted successfully.</response>
    /// <response code="400">X-Organization-Id header is required.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not an owner or manager.</response>
    /// <response code="404">Source not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var fail = await RequireOrgOwnerOrManager(ct);
        if (fail != null) return fail;
        var orgId = _currentUser.CurrentOrganizationId!.Value;
        var result = await _leadSourceService.DeleteAsync(id, orgId, ct);
        return result.ToNoContentResult();
    }
}

/// <summary>
/// Request to create a new lead source.
/// </summary>
public record CreateLeadSourceRequest
{
    /// <summary>
    /// Source name.
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
/// Request to update a lead source.
/// </summary>
public record UpdateLeadSourceRequest
{
    /// <summary>
    /// New source name (optional).
    /// </summary>
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
    public string? Name { get; init; }

    /// <summary>
    /// New display order (optional).
    /// </summary>
    public int? DisplayOrder { get; init; }
}
