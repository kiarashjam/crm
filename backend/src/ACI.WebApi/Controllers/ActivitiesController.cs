using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Manages activities (calls, meetings, emails, notes) in the CRM system.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ActivitiesController : ControllerBase
{
    private readonly IActivityService _activityService;
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    /// Initializes a new instance of the ActivitiesController.
    /// </summary>
    public ActivitiesController(IActivityService activityService, ICurrentUserService currentUser)
    {
        _activityService = activityService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Retrieves paginated activities for the authenticated user with optional search.
    /// </summary>
    /// <param name="page">Page number (1-based). Defaults to 1.</param>
    /// <param name="pageSize">Number of items per page. Defaults to 20, max 100.</param>
    /// <param name="search">Optional search term to filter by subject, description, or type.</param>
    /// <param name="type">Optional activity type filter (call, meeting, email, note).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of activities.</returns>
    /// <response code="200">Returns the paginated list of activities.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<ActivityDto>>> GetActivities(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? type = null,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        
        var result = await _activityService.GetPagedAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId,
            page,
            pageSize,
            search,
            type,
            ct);
        
        return Ok(result);
    }

    /// <summary>
    /// Retrieves all activities for the authenticated user (non-paginated).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of activities.</returns>
    /// <response code="200">Returns the list of activities.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("all")]
    [ProducesResponseType(typeof(IReadOnlyList<ActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ActivityDto>>> GetAllActivities(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var list = await _activityService.GetByUserIdAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return Ok(list);
    }

    /// <summary>
    /// Retrieves all activities linked to a specific contact.
    /// </summary>
    /// <param name="contactId">The contact ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of activities linked to the contact.</returns>
    /// <response code="200">Returns the list of activities.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("contact/{contactId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<ActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ActivityDto>>> GetByContact(
        Guid contactId, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var list = await _activityService.GetByContactIdAsync(
            contactId, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return Ok(list);
    }

    /// <summary>
    /// Retrieves all activities linked to a specific deal.
    /// </summary>
    /// <param name="dealId">The deal ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of activities linked to the deal.</returns>
    /// <response code="200">Returns the list of activities.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("deal/{dealId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<ActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ActivityDto>>> GetByDeal(
        Guid dealId, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var list = await _activityService.GetByDealIdAsync(
            dealId, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return Ok(list);
    }

    /// <summary>
    /// Retrieves all activities linked to a specific lead.
    /// </summary>
    /// <param name="leadId">The lead ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of activities linked to the lead.</returns>
    /// <response code="200">Returns the list of activities.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("lead/{leadId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<ActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ActivityDto>>> GetByLead(
        Guid leadId, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var list = await _activityService.GetByLeadIdAsync(
            leadId, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return Ok(list);
    }

    /// <summary>
    /// Returns activity counts per user for all members in the current user's organization.
    /// Used by team management pages to display real performance metrics.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A dictionary mapping user IDs to their activity counts.</returns>
    /// <response code="200">Returns the activity counts per user.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("org-member-counts")]
    [ProducesResponseType(typeof(IReadOnlyDictionary<Guid, int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyDictionary<Guid, int>>> GetOrgMemberCounts(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        var counts = await _activityService.GetOrgMemberActivityCountsAsync(
            _currentUser.CurrentOrganizationId,
            ct);

        return Ok(counts);
    }

    /// <summary>
    /// Creates a new activity.
    /// </summary>
    /// <param name="request">The activity creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created activity.</returns>
    /// <remarks>
    /// Valid activity types: call, meeting, email, note, task, follow_up, deadline, video, demo.
    /// At least one of ContactId, DealId, or LeadId must be specified.
    /// </remarks>
    /// <response code="200">Activity created successfully.</response>
    /// <response code="400">Invalid request data or missing related entity.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ActivityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ActivityDto>> Create(
        [FromBody] CreateActivityRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _activityService.CreateAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            request, 
            ct);
        
        return result.ToActionResult();
    }

    /// <summary>
    /// Updates an existing activity.
    /// </summary>
    /// <param name="id">The activity ID to update.</param>
    /// <param name="request">The activity update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated activity.</returns>
    /// <response code="200">Activity updated successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Activity not found.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ActivityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActivityDto>> Update(
        Guid id,
        [FromBody] UpdateActivityRequest request,
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _activityService.UpdateAsync(
            id,
            userId.Value,
            _currentUser.CurrentOrganizationId,
            request,
            ct);
        
        return result.ToActionResult();
    }

    /// <summary>
    /// Deletes an activity.
    /// </summary>
    /// <param name="id">The activity ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Activity deleted successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Activity not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _activityService.DeleteAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return result.ToNoContentResult();
    }
}
