using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Manages deals in the CRM system.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class DealsController : ControllerBase
{
    private readonly IDealService _dealService;
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    /// Initializes a new instance of the DealsController.
    /// </summary>
    public DealsController(IDealService dealService, ICurrentUserService currentUser)
    {
        _dealService = dealService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Retrieves deals with pagination and optional search.
    /// </summary>
    /// <param name="page">Page number (1-based). Default is 1.</param>
    /// <param name="pageSize">Number of items per page. Default is 20, max 100.</param>
    /// <param name="search">Optional search query.</param>
    /// <param name="companyId">Optional company ID to filter deals by.</param>
    /// <param name="contactId">Optional contact ID to filter deals by.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of deals.</returns>
    /// <response code="200">Returns the paginated list of deals.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DealDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<DealDto>>> GetDeals(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] Guid? companyId = null,
        [FromQuery] Guid? contactId = null,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        pageSize = Math.Clamp(pageSize, 1, 100);
        
        var result = await _dealService.GetDealsPagedAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            page,
            pageSize,
            search,
            companyId,
            contactId,
            ct);
        
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all deals for the authenticated user (non-paginated, for backward compatibility).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of deals.</returns>
    /// <response code="200">Returns the list of deals.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("all")]
    [ProducesResponseType(typeof(IReadOnlyList<DealDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<DealDto>>> GetAllDeals(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var list = await _dealService.GetDealsAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return Ok(list);
    }

    /// <summary>
    /// Searches deals by name or value (non-paginated, for backward compatibility).
    /// </summary>
    /// <param name="q">The search query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of matching deals.</returns>
    /// <response code="200">Returns the list of matching deals.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<DealDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<DealDto>>> Search(
        [FromQuery] string q, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var list = await _dealService.SearchAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            q ?? "", 
            ct);
        
        return Ok(list);
    }

    /// <summary>
    /// Retrieves a specific deal by ID.
    /// </summary>
    /// <param name="id">The deal ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The deal if found.</returns>
    /// <response code="200">Returns the deal.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Deal not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DealDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DealDto>> GetById(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _dealService.GetByIdAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return result.ToActionResult();
    }

    /// <summary>
    /// Creates a new deal.
    /// </summary>
    /// <param name="request">The deal creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created deal.</returns>
    /// <response code="200">Deal created successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost]
    [ProducesResponseType(typeof(DealDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<DealDto>> Create(
        [FromBody] CreateDealRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _dealService.CreateAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            request, 
            ct);
        
        return result.ToActionResult();
    }

    /// <summary>
    /// Updates an existing deal.
    /// </summary>
    /// <param name="id">The deal ID to update.</param>
    /// <param name="request">The deal update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated deal.</returns>
    /// <response code="200">Deal updated successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Deal not found.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DealDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DealDto>> Update(
        Guid id, 
        [FromBody] UpdateDealRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _dealService.UpdateAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            request, 
            ct);
        
        return result.ToActionResult();
    }

    /// <summary>
    /// Deletes a deal.
    /// </summary>
    /// <param name="id">The deal ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Deal deleted successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Deal not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _dealService.DeleteAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return result.ToNoContentResult();
    }
}
