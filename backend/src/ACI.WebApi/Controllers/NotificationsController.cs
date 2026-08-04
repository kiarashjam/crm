using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Notifications surfaced in the header bell: task reminders, @mentions, deal
/// changes and system messages. Always scoped to the calling user.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    /// Initializes a new instance of the NotificationsController.
    /// </summary>
    public NotificationsController(INotificationService notificationService, ICurrentUserService currentUser)
    {
        _notificationService = notificationService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Retrieves the authenticated user's notifications, newest first.
    /// </summary>
    /// <param name="take">Maximum notifications to return. Defaults to 50, max 200.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The user's notifications.</returns>
    /// <response code="200">Returns the notifications.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetNotifications(
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        var items = await _notificationService.GetForUserAsync(
            userId.Value, _currentUser.CurrentOrganizationId, take, ct);

        return Ok(items);
    }

    /// <summary>
    /// Returns how many of the user's notifications are unread. Polled by the bell badge.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The unread count.</returns>
    /// <response code="200">Returns the unread count.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(UnreadCountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UnreadCountDto>> GetUnreadCount(CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        var count = await _notificationService.GetUnreadCountAsync(
            userId.Value, _currentUser.CurrentOrganizationId, ct);

        return Ok(new UnreadCountDto(count));
    }

    /// <summary>
    /// Creates a notification for the authenticated user.
    /// </summary>
    /// <param name="request">The notification to create.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created notification.</returns>
    /// <response code="201">Returns the created notification.</response>
    /// <response code="400">The request is missing a title.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost]
    [ProducesResponseType(typeof(NotificationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<NotificationDto>> CreateNotification(
        [FromBody] CreateNotificationRequest request,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        var created = await _notificationService.CreateAsync(
            userId.Value, _currentUser.CurrentOrganizationId, request, ct);

        if (created == null) return BadRequest(new { message = "Title is required." });

        return CreatedAtAction(nameof(GetNotifications), new { }, created);
    }

    /// <summary>
    /// Marks a single notification read.
    /// </summary>
    /// <param name="id">The notification id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="204">The notification is read.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">No such notification for this user.</response>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        var ok = await _notificationService.MarkReadAsync(
            id, userId.Value, _currentUser.CurrentOrganizationId, ct);

        return ok ? NoContent() : NotFound();
    }

    /// <summary>
    /// Marks every unread notification read.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="204">All notifications are read.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        await _notificationService.MarkAllReadAsync(
            userId.Value, _currentUser.CurrentOrganizationId, ct);

        return NoContent();
    }
}
