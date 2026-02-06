using System.ComponentModel.DataAnnotations;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Enums;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Manages sales pipelines for deal tracking.
/// </summary>
/// <remarks>
/// Pipelines are organization-scoped and contain deal stages.
/// Only owners and managers can create, update, or delete pipelines.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class PipelinesController : ControllerBase
{
    private readonly IPipelineService _pipelineService;
    private readonly ICurrentUserService _currentUser;
    private readonly IOrganizationRepository _organizationRepository;

    public PipelinesController(IPipelineService pipelineService, ICurrentUserService currentUser, IOrganizationRepository organizationRepository)
    {
        _pipelineService = pipelineService;
        _currentUser = currentUser;
        _organizationRepository = organizationRepository;
    }

    private async Task<ActionResult?> RequireOrgOwnerOrManager(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var orgId = _currentUser.CurrentOrganizationId;
        if (orgId == null) return BadRequest("X-Organization-Id required");
        var role = await _organizationRepository.GetMemberRoleAsync(userId.Value, orgId.Value, ct);
        if (role != OrgMemberRole.Owner && role != OrgMemberRole.Manager)
            return Forbid();
        return null;
    }

    /// <summary>
    /// Gets all pipelines for the current organization.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of pipelines.</returns>
    /// <response code="200">Returns the list of pipelines.</response>
    /// <response code="400">X-Organization-Id header is required.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PipelineDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<PipelineDto>>> GetPipelines(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var orgId = _currentUser.CurrentOrganizationId;
        if (orgId == null) return Problem(detail: "X-Organization-Id required", statusCode: StatusCodes.Status400BadRequest);
        var result = await _pipelineService.GetByOrganizationIdAsync(orgId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Gets a specific pipeline by ID.
    /// </summary>
    /// <param name="id">The pipeline ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The pipeline details.</returns>
    /// <response code="200">Returns the pipeline.</response>
    /// <response code="400">X-Organization-Id header is required.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Pipeline not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PipelineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PipelineDto>> GetById(Guid id, CancellationToken ct)
    {
        var orgId = _currentUser.CurrentOrganizationId;
        if (orgId == null) return Problem(detail: "X-Organization-Id required", statusCode: StatusCodes.Status400BadRequest);
        var result = await _pipelineService.GetByIdAsync(id, orgId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Creates a new pipeline.
    /// </summary>
    /// <remarks>
    /// Only organization owners and managers can create pipelines.
    /// </remarks>
    /// <param name="request">Pipeline creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created pipeline.</returns>
    /// <response code="200">Pipeline created successfully.</response>
    /// <response code="400">Invalid request data or X-Organization-Id required.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not an owner or manager.</response>
    [HttpPost]
    [ProducesResponseType(typeof(PipelineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PipelineDto>> Create([FromBody] CreatePipelineRequest request, CancellationToken ct)
    {
        var fail = await RequireOrgOwnerOrManager(ct);
        if (fail != null) return fail;
        var orgId = _currentUser.CurrentOrganizationId!.Value;
        var result = await _pipelineService.CreateAsync(orgId, request.Name, request.DisplayOrder, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Updates an existing pipeline.
    /// </summary>
    /// <remarks>
    /// Only organization owners and managers can update pipelines.
    /// </remarks>
    /// <param name="id">The pipeline ID.</param>
    /// <param name="request">Pipeline update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated pipeline.</returns>
    /// <response code="200">Pipeline updated successfully.</response>
    /// <response code="400">X-Organization-Id header is required.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not an owner or manager.</response>
    /// <response code="404">Pipeline not found.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PipelineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PipelineDto>> Update(Guid id, [FromBody] UpdatePipelineRequest request, CancellationToken ct)
    {
        var fail = await RequireOrgOwnerOrManager(ct);
        if (fail != null) return fail;
        var orgId = _currentUser.CurrentOrganizationId!.Value;
        var result = await _pipelineService.UpdateAsync(id, orgId, request.Name, request.DisplayOrder, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Deletes a pipeline.
    /// </summary>
    /// <remarks>
    /// Only organization owners and managers can delete pipelines.
    /// Pipelines with existing deals cannot be deleted.
    /// </remarks>
    /// <param name="id">The pipeline ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Pipeline deleted successfully.</response>
    /// <response code="400">X-Organization-Id header is required or pipeline has deals.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not an owner or manager.</response>
    /// <response code="404">Pipeline not found.</response>
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
        var result = await _pipelineService.DeleteAsync(id, orgId, ct);
        return result.ToNoContentResult();
    }
}

/// <summary>
/// Request to create a new pipeline.
/// </summary>
public record CreatePipelineRequest
{
    /// <summary>
    /// Pipeline name.
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
/// Request to update a pipeline.
/// </summary>
public record UpdatePipelineRequest
{
    /// <summary>
    /// New pipeline name (optional).
    /// </summary>
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
    public string? Name { get; init; }

    /// <summary>
    /// New display order (optional).
    /// </summary>
    public int? DisplayOrder { get; init; }
}
