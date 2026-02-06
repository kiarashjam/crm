using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Manages copy templates in the CRM system.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class TemplatesController : ControllerBase
{
    private readonly ITemplateService _templateService;
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    /// Initializes a new instance of the TemplatesController.
    /// </summary>
    public TemplatesController(ITemplateService templateService, ICurrentUserService currentUser)
    {
        _templateService = templateService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Retrieves paginated templates available to the user with optional search.
    /// </summary>
    /// <param name="page">Page number (1-based). Defaults to 1.</param>
    /// <param name="pageSize">Number of items per page. Defaults to 20, max 100.</param>
    /// <param name="search">Optional search term to filter by title, body, or category.</param>
    /// <param name="category">Optional category filter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of templates.</returns>
    /// <response code="200">Returns the paginated list of templates.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<TemplateDto>>> GetTemplates(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        var orgId = _currentUser.OrganizationId;
        if (userId == null) return Unauthorized();
        
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        
        var result = await _templateService.GetPagedAsync(
            userId.Value, 
            orgId,
            page,
            pageSize,
            search,
            category,
            ct);
        
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all templates available to the user (non-paginated).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of templates including user's own, shared, and system templates.</returns>
    /// <response code="200">Returns the list of templates.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("all")]
    [ProducesResponseType(typeof(IReadOnlyList<TemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<TemplateDto>>> GetAll(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        var orgId = _currentUser.OrganizationId;
        if (userId == null) return Unauthorized();
        
        var list = await _templateService.GetUserTemplatesAsync(userId.Value, orgId, ct);
        return Ok(list);
    }

    /// <summary>
    /// Retrieves a specific template by ID.
    /// </summary>
    /// <param name="id">The template ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The template if found and accessible.</returns>
    /// <response code="200">Returns the template.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User does not have access to this template.</response>
    /// <response code="404">Template not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TemplateDto>> GetById(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _templateService.GetByIdAsync(userId.Value, id, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Creates a new template.
    /// </summary>
    /// <param name="request">The template creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created template.</returns>
    /// <response code="201">Template created successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost]
    [ProducesResponseType(typeof(TemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TemplateDto>> Create(
        [FromBody] CreateTemplateRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        var orgId = _currentUser.OrganizationId;
        if (userId == null) return Unauthorized();
        
        var result = await _templateService.CreateAsync(userId.Value, orgId, request, ct);
        
        if (result.IsFailure)
        {
            return result.ToActionResult();
        }
        
        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    /// <summary>
    /// Updates an existing template.
    /// </summary>
    /// <param name="id">The template ID to update.</param>
    /// <param name="request">The template update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated template.</returns>
    /// <response code="200">Template updated successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User does not own this template or it's a system template.</response>
    /// <response code="404">Template not found.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TemplateDto>> Update(
        Guid id, 
        [FromBody] UpdateTemplateRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _templateService.UpdateAsync(userId.Value, id, request, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Deletes a template.
    /// </summary>
    /// <param name="id">The template ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Template deleted successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User does not own this template or it's a system template.</response>
    /// <response code="404">Template not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _templateService.DeleteAsync(userId.Value, id, ct);
        return result.ToNoContentResult();
    }

    /// <summary>
    /// Increments the use count for a template.
    /// </summary>
    /// <param name="id">The template ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <remarks>
    /// Call this endpoint when a template is used to generate copy.
    /// This helps track popular templates.
    /// </remarks>
    /// <response code="204">Use count incremented successfully.</response>
    [HttpPost("{id:guid}/use")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> IncrementUseCount(Guid id, CancellationToken ct)
    {
        await _templateService.IncrementUseCountAsync(id, ct);
        return NoContent();
    }
}
