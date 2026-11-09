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
    private readonly IDealBoardService _board;
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    /// Initializes a new instance of the DealsController.
    /// </summary>
    public DealsController(IDealService dealService, IDealBoardService board, ICurrentUserService currentUser)
    {
        _dealService = dealService;
        _board = board;
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
        
        if (result.IsSuccess) Response.SetETag(result.Value.Version);
        return result.ToActionResult();
    }

    /// <summary>
    /// Creates a new deal.
    /// </summary>
    /// <param name="request">The deal creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created deal.</returns>
    /// <response code="200">Deal created successfully.</response>
    /// <response code="400">Invalid request data, or a company, contact, pipeline or stage that is not in this workspace.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">The owner is not a member of this workspace.</response>
    [HttpPost]
    [ProducesResponseType(typeof(DealDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
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
    /// <response code="400">Invalid request data, or a company, contact, pipeline or stage that is not in this workspace.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">The owner is not a member of this workspace, or only its owner or a manager may change its privacy.</response>
    /// <response code="404">Deal not found.</response>
    /// <response code="409">Someone saved the deal after the version this edit sends (in <c>version</c> or If-Match).</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DealDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DealDto>> Update(
        Guid id, 
        [FromBody] UpdateDealRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        // The version the edit was made from, from the body or from If-Match.
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();

        var result = await _dealService.UpdateAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            request with { Version = version }, 
            ct);

        if (result.IsSuccess) Response.SetETag(result.Value.Version);
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

    /// <summary>
    /// The deals board for one pipeline: its stages, the first cards in each, and each
    /// stage's count, money per currency, weighted value and stale deals.
    /// </summary>
    /// <remarks>
    /// The filters (search, owner, amount, close date, stale) narrow the columns; the
    /// summary is the whole pipeline. Without a pipeline, the workspace's first. A
    /// workspace without pipelines gets the default stages.
    /// </remarks>
    /// <response code="200">The board.</response>
    /// <response code="400">An unknown sort.</response>
    /// <response code="404">The pipeline is not one of this workspace's.</response>
    [HttpGet("board")]
    [ProducesResponseType(typeof(DealBoardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DealBoardDto>> GetBoard([FromQuery] DealBoardQuery query, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var result = await _board.GetBoardAsync(userId.Value, _currentUser.CurrentOrganizationId, query, ct);
        return result.ToActionResult();
    }

    /// <summary>More cards of one board column, after the first <paramref name="skip"/>, with the board's filters and order.</summary>
    /// <param name="column">The column's key, as the board gave it.</param>
    /// <param name="skip">Cards already shown.</param>
    /// <param name="query">The board's pipeline, filters, order and page size.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The next cards.</response>
    /// <response code="404">The pipeline, or the column, is not on this workspace's board.</response>
    [HttpGet("board/column")]
    [ProducesResponseType(typeof(DealBoardColumnPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DealBoardColumnPageDto>> GetBoardColumn(
        [FromQuery] string column, [FromQuery] int skip, [FromQuery] DealBoardQuery query, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var result = await _board.GetColumnAsync(userId.Value, _currentUser.CurrentOrganizationId, query, column ?? string.Empty, skip, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// The stages a deal has been in, newest first: when it came into each, from where,
    /// who moved it, and when it moved on.
    /// </summary>
    /// <response code="200">The history; a deal from before history was kept may have none.</response>
    /// <response code="404">Not a deal this person can see.</response>
    [HttpGet("{id:guid}/stage-history")]
    [ProducesResponseType(typeof(IReadOnlyList<DealStageChangeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<DealStageChangeDto>>> GetStageHistory(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var result = await _dealService.GetStageHistoryAsync(id, userId.Value, _currentUser.CurrentOrganizationId, ct);
        return result.ToActionResult();
    }

    /// <summary>Keeps this deal and folds the other one into it.</summary>
    /// <response code="204">Merged; the other deal is gone and everything on it is on this one.</response>
    /// <response code="403">The other deal is private and this person may not make this one private.</response>
    /// <response code="404">Either deal is not one this person can see, or they are the same deal.</response>
    [HttpPost("{id:guid}/merge")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Merge(Guid id, [FromBody] MergeDealsRequest request, CancellationToken ct)
    {
        if (request == null || request.OtherDealId == Guid.Empty)
            return BadRequest();
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var result = await _dealService.MergeAsync(id, request.OtherDealId, userId.Value, _currentUser.CurrentOrganizationId, ct);
        return result.ToNoContentResult();
    }
}
