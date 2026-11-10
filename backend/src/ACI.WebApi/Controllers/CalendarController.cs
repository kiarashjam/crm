using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// The sales calendar: tasks by due time, logged meetings, and the meeting dates on
/// leads. Club events and reservations are read from their own endpoints.
/// </summary>
[ApiController]
[Route("api/calendar")]
[Authorize]
[Produces("application/json")]
public class CalendarController : ControllerBase
{
    private readonly ICalendarService _calendar;
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    /// Initializes a new instance of the CalendarController.
    /// </summary>
    public CalendarController(ICalendarService calendar, ICurrentUserService currentUser)
    {
        _calendar = calendar;
        _currentUser = currentUser;
    }

    /// <summary>
    /// What is on the calendar from <paramref name="from"/> up to <paramref name="to"/>.
    /// </summary>
    /// <param name="from">The first moment, UTC.</param>
    /// <param name="to">The moment after the last, UTC; at most 45 days after <paramref name="from"/>.</param>
    /// <param name="scope">"mine" (the default): my tasks, the meetings I logged and my leads' meetings. "team": everyone's.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">The entries, by start time.</response>
    /// <response code="400">The span is longer than 45 days, or ends before it starts.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(CalendarDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CalendarDto>> Get(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] string? scope = null,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var result = await _calendar.GetAsync(userId.Value, _currentUser.CurrentOrganizationId, from, to, scope, ct);
        return result.ToActionResult();
    }
}
