using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ActivitiesController : ControllerBase
{
    private readonly IActivityService _activityService;
    private readonly ICurrentUserService _currentUser;

    public ActivitiesController(IActivityService activityService, ICurrentUserService currentUser)
    {
        _activityService = activityService;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ActivityDto>>> GetActivities(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var list = await _activityService.GetByUserIdAsync(userId.Value, ct);
        return Ok(list);
    }

    [HttpGet("contact/{contactId:guid}")]
    public async Task<ActionResult<IReadOnlyList<ActivityDto>>> GetByContact(Guid contactId, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var list = await _activityService.GetByContactIdAsync(contactId, userId.Value, ct);
        return Ok(list);
    }

    [HttpGet("deal/{dealId:guid}")]
    public async Task<ActionResult<IReadOnlyList<ActivityDto>>> GetByDeal(Guid dealId, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var list = await _activityService.GetByDealIdAsync(dealId, userId.Value, ct);
        return Ok(list);
    }

    [HttpPost]
    public async Task<ActionResult<ActivityDto>> Create([FromBody] CreateActivityRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var activity = await _activityService.CreateAsync(userId.Value, request, ct);
        return activity == null ? BadRequest() : Ok(activity);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var deleted = await _activityService.DeleteAsync(id, userId.Value, ct);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
