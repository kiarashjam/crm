using System.ComponentModel.DataAnnotations;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Enums;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Manages deal stages within pipelines.
/// </summary>
/// <remarks>
/// Deal stages represent the steps in a sales process.
/// Only owners and managers can create, update, or delete stages.
/// Each stage can be marked as "won" or "lost" for deal outcomes.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class DealStagesController : ControllerBase
{
    private readonly IDealStageService _dealStageService;
    private readonly ICurrentUserService _currentUser;
    private readonly IOrganizationRepository _organizationRepository;

    public DealStagesController(IDealStageService dealStageService, ICurrentUserService currentUser, IOrganizationRepository organizationRepository)
    {
        _dealStageService = dealStageService;
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
    /// Gets all stages for a specific pipeline.
    /// </summary>
    /// <param name="pipelineId">The pipeline ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of deal stages for the pipeline.</returns>
    /// <response code="200">Returns the list of stages.</response>
    /// <response code="400">X-Organization-Id header is required.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Pipeline not found.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DealStageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DealStageDto>>> GetByPipeline([FromQuery] Guid pipelineId, CancellationToken ct)
    {
        var orgId = _currentUser.CurrentOrganizationId;
        if (orgId == null) return Problem(detail: "X-Organization-Id required", statusCode: StatusCodes.Status400BadRequest);
        var result = await _dealStageService.GetByPipelineIdAsync(pipelineId, orgId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Gets a specific deal stage by ID.
    /// </summary>
    /// <param name="id">The stage ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deal stage details.</returns>
    /// <response code="200">Returns the stage.</response>
    /// <response code="400">X-Organization-Id header is required.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Stage not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DealStageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DealStageDto>> GetById(Guid id, CancellationToken ct)
    {
        var orgId = _currentUser.CurrentOrganizationId;
        if (orgId == null) return Problem(detail: "X-Organization-Id required", statusCode: StatusCodes.Status400BadRequest);
        var result = await _dealStageService.GetByIdAsync(id, orgId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Creates a new deal stage in a pipeline.
    /// </summary>
    /// <remarks>
    /// Only organization owners and managers can create stages.
    /// Stages can be marked as "won" or "lost" to represent deal outcomes.
    /// </remarks>
    /// <param name="request">Stage creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created stage.</returns>
    /// <response code="200">Stage created successfully.</response>
    /// <response code="400">Invalid request data or X-Organization-Id required.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not an owner or manager.</response>
    /// <response code="404">Pipeline not found.</response>
    [HttpPost]
    [ProducesResponseType(typeof(DealStageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DealStageDto>> Create([FromBody] CreateDealStageRequest request, CancellationToken ct)
    {
        var fail = await RequireOrgOwnerOrManager(ct);
        if (fail != null) return fail;
        var orgId = _currentUser.CurrentOrganizationId!.Value;
        var result = await _dealStageService.CreateAsync(request.PipelineId, orgId, request.Name, request.DisplayOrder, request.IsWon, request.IsLost, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Updates an existing deal stage.
    /// </summary>
    /// <remarks>
    /// Only organization owners and managers can update stages.
    /// </remarks>
    /// <param name="id">The stage ID.</param>
    /// <param name="request">Stage update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated stage.</returns>
    /// <response code="200">Stage updated successfully.</response>
    /// <response code="400">X-Organization-Id header is required.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not an owner or manager.</response>
    /// <response code="404">Stage not found.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DealStageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DealStageDto>> Update(Guid id, [FromBody] UpdateDealStageRequest request, CancellationToken ct)
    {
        var fail = await RequireOrgOwnerOrManager(ct);
        if (fail != null) return fail;
        var orgId = _currentUser.CurrentOrganizationId!.Value;
        var result = await _dealStageService.UpdateAsync(id, orgId, request.Name, request.DisplayOrder, request.IsWon, request.IsLost, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Deletes a deal stage.
    /// </summary>
    /// <remarks>
    /// Only organization owners and managers can delete stages.
    /// Stages with existing deals may not be deletable.
    /// </remarks>
    /// <param name="id">The stage ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Stage deleted successfully.</response>
    /// <response code="400">X-Organization-Id header is required.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not an owner or manager.</response>
    /// <response code="404">Stage not found.</response>
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
        var result = await _dealStageService.DeleteAsync(id, orgId, ct);
        return result.ToNoContentResult();
    }
}

/// <summary>
/// Request to create a new deal stage.
/// </summary>
public record CreateDealStageRequest
{
    /// <summary>
    /// The pipeline this stage belongs to.
    /// </summary>
    [Required(ErrorMessage = "PipelineId is required")]
    public required Guid PipelineId { get; init; }

    /// <summary>
    /// Stage name.
    /// </summary>
    [Required(ErrorMessage = "Name is required")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
    public required string Name { get; init; }

    /// <summary>
    /// Display order.
    /// </summary>
    public int DisplayOrder { get; init; } = 0;

    /// <summary>
    /// Whether deals in this stage are considered won.
    /// </summary>
    public bool IsWon { get; init; } = false;

    /// <summary>
    /// Whether deals in this stage are considered lost.
    /// </summary>
    public bool IsLost { get; init; } = false;
}

/// <summary>
/// Request to update a deal stage.
/// </summary>
public record UpdateDealStageRequest
{
    /// <summary>
    /// New stage name (optional).
    /// </summary>
    [StringLength(100, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 100 characters")]
    public string? Name { get; init; }

    /// <summary>
    /// New display order (optional).
    /// </summary>
    public int? DisplayOrder { get; init; }

    /// <summary>
    /// Whether deals in this stage are considered won (optional).
    /// </summary>
    public bool? IsWon { get; init; }

    /// <summary>
    /// Whether deals in this stage are considered lost (optional).
    /// </summary>
    public bool? IsLost { get; init; }
}
