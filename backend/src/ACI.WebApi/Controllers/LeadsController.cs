using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Manages leads in the CRM system.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class LeadsController : ControllerBase
{
    private readonly ILeadService _leadService;
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    /// Initializes a new instance of the LeadsController.
    /// </summary>
    public LeadsController(ILeadService leadService, ICurrentUserService currentUser)
    {
        _leadService = leadService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Retrieves leads with pagination and optional search.
    /// </summary>
    /// <param name="page">Page number (1-based). Default is 1.</param>
    /// <param name="pageSize">Number of items per page. Default is 20, max 100.</param>
    /// <param name="search">Optional search query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of leads.</returns>
    /// <response code="200">Returns the paginated list of leads.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<LeadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<LeadDto>>> GetLeads(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _leadService.GetLeadsPagedAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            page,
            pageSize,
            search,
            ct);
        
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all leads for the authenticated user (non-paginated, for backward compatibility).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of leads.</returns>
    /// <response code="200">Returns the list of leads.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("all")]
    [ProducesResponseType(typeof(IReadOnlyList<LeadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<LeadDto>>> GetAllLeads(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var list = await _leadService.GetLeadsAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return Ok(list);
    }

    /// <summary>
    /// Searches leads by name, email, or company (non-paginated, for backward compatibility).
    /// </summary>
    /// <param name="q">The search query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of matching leads.</returns>
    /// <response code="200">Returns the list of matching leads.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<LeadDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<LeadDto>>> Search(
        [FromQuery] string q, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var list = await _leadService.SearchAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            q ?? "", 
            ct);
        
        return Ok(list);
    }

    /// <summary>
    /// Retrieves a specific lead by ID.
    /// </summary>
    /// <param name="id">The lead ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The lead if found.</returns>
    /// <response code="200">Returns the lead.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Lead not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeadDto>> GetById(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _leadService.GetByIdAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return result.ToActionResult();
    }

    /// <summary>
    /// Creates a new lead.
    /// </summary>
    /// <param name="request">The lead creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created lead.</returns>
    /// <response code="200">Lead created successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LeadDto>> Create(
        [FromBody] CreateLeadRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _leadService.CreateAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            request, 
            ct);
        
        return result.ToActionResult();
    }

    /// <summary>
    /// Updates an existing lead.
    /// </summary>
    /// <param name="id">The lead ID to update.</param>
    /// <param name="request">The lead update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated lead.</returns>
    /// <response code="200">Lead updated successfully.</response>
    /// <response code="400">Invalid request data or lead is already converted.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Lead not found.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeadDto>> Update(
        Guid id, 
        [FromBody] UpdateLeadRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _leadService.UpdateAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            request, 
            ct);
        
        return result.ToActionResult();
    }

    /// <summary>
    /// Deletes a lead.
    /// </summary>
    /// <param name="id">The lead ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Lead deleted successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Lead not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _leadService.DeleteAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return result.ToNoContentResult();
    }

    /// <summary>
    /// Converts a lead to a contact and/or deal.
    /// </summary>
    /// <remarks>
    /// This endpoint allows converting a lead to one or more of:
    /// - A new or existing contact
    /// - A new or existing deal
    /// - A new or existing company
    /// 
    /// At least one conversion action must be specified.
    /// Once converted, leads become read-only.
    /// </remarks>
    /// <param name="id">The lead ID to convert.</param>
    /// <param name="request">The conversion request specifying what to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The IDs of created/linked entities.</returns>
    /// <response code="200">Lead converted successfully.</response>
    /// <response code="400">Invalid request or lead already converted.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Lead not found.</response>
    [HttpPost("{id:guid}/convert")]
    [ProducesResponseType(typeof(ConvertLeadResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConvertLeadResult>> Convert(
        Guid id, 
        [FromBody] ConvertLeadRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _leadService.ConvertAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            request, 
            ct);
        
        return result.ToActionResult();
    }
}
