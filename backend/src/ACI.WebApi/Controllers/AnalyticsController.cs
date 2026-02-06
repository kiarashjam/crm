using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Intelligent Sales Writer analytics and conversion tracking.
/// </summary>
/// <remarks>
/// Tracks copy generation performance, response rates, and conversions.
/// Helps measure the effectiveness of AI-generated content.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;
    private readonly ICurrentUserService _currentUser;

    public AnalyticsController(IAnalyticsService analyticsService, ICurrentUserService currentUser)
    {
        _analyticsService = analyticsService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Gets Intelligent Sales Writer analytics summary.
    /// </summary>
    /// <remarks>
    /// Returns aggregated metrics including:
    /// - Total copies generated
    /// - Response rates
    /// - Most effective templates
    /// - Performance trends over time
    /// </remarks>
    /// <param name="from">Start date for the analytics period (optional).</param>
    /// <param name="to">End date for the analytics period (optional).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Analytics summary for the specified period.</returns>
    /// <response code="200">Returns the analytics summary.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(CopyAnalyticsSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CopyAnalyticsSummaryDto>> GetSummary(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var summary = await _analyticsService.GetSummaryAsync(userId.Value, _currentUser.OrganizationId, from, to, ct);
        return Ok(summary);
    }

    /// <summary>
    /// Tracks a copy event (copy, send, response).
    /// </summary>
    /// <remarks>
    /// Event types:
    /// - copy: User copied the generated text
    /// - send: User sent the copy via email/CRM
    /// - response: Recipient responded
    /// </remarks>
    /// <param name="request">Event tracking request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Event tracked successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost("track")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> TrackEvent([FromBody] TrackCopyEventRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        await _analyticsService.TrackEventAsync(userId.Value, _currentUser.OrganizationId, request, ct);
        return NoContent();
    }

    /// <summary>
    /// Gets conversion tracking data.
    /// </summary>
    /// <remarks>
    /// Returns a list of conversions (responses, meetings, deals) that resulted
    /// from AI-generated copy. Used to measure ROI of the Intelligent Sales Writer.
    /// </remarks>
    /// <param name="from">Start date for the period (optional).</param>
    /// <param name="to">End date for the period (optional).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of conversion records.</returns>
    /// <response code="200">Returns the conversion data.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("conversions")]
    [ProducesResponseType(typeof(IReadOnlyList<CopyConversionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<CopyConversionDto>>> GetConversions(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var conversions = await _analyticsService.GetConversionsAsync(userId.Value, _currentUser.OrganizationId, from, to, ct);
        return Ok(conversions);
    }

    /// <summary>
    /// Records a conversion (response/meeting/deal).
    /// </summary>
    /// <remarks>
    /// Links a successful outcome to the original AI-generated copy.
    /// Conversion types:
    /// - response: Recipient replied to the message
    /// - meeting: Meeting was scheduled
    /// - deal: Deal was created or won
    /// </remarks>
    /// <param name="request">Conversion creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created conversion record.</returns>
    /// <response code="200">Conversion recorded successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost("conversions")]
    [ProducesResponseType(typeof(CopyConversionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CopyConversionDto>> CreateConversion([FromBody] CreateConversionRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var conversion = await _analyticsService.CreateConversionAsync(userId.Value, _currentUser.OrganizationId, request, ct);
        return Ok(conversion);
    }
}
