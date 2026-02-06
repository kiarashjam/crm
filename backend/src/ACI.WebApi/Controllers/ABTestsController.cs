using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Manages A/B tests for email copy variants.
/// </summary>
/// <remarks>
/// A/B testing allows comparing different copy versions to determine
/// which performs better based on open rates, click rates, and conversions.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class ABTestsController : ControllerBase
{
    private readonly IABTestService _abTestService;
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    /// Initializes a new instance of the ABTestsController.
    /// </summary>
    public ABTestsController(IABTestService abTestService, ICurrentUserService currentUser)
    {
        _abTestService = abTestService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Gets all A/B tests for the user.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of A/B tests.</returns>
    /// <response code="200">Returns the list of A/B tests.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ABTestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ABTestDto>>> GetAll(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var tests = await _abTestService.GetTestsAsync(userId.Value, _currentUser.OrganizationId, ct);
        return Ok(tests);
    }

    /// <summary>
    /// Gets a specific A/B test by ID.
    /// </summary>
    /// <param name="id">The A/B test ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The A/B test if found.</returns>
    /// <response code="200">Returns the A/B test.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">A/B test not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ABTestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ABTestDto>> GetById(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var test = await _abTestService.GetByIdAsync(userId.Value, id, ct);
        return test == null ? NotFound() : Ok(test);
    }

    /// <summary>
    /// Creates a new A/B test.
    /// </summary>
    /// <remarks>
    /// An A/B test contains multiple copy variants that can be compared.
    /// Track events on each variant to measure performance.
    /// </remarks>
    /// <param name="request">A/B test creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created A/B test.</returns>
    /// <response code="201">A/B test created successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ABTestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ABTestDto>> Create([FromBody] CreateABTestRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var created = await _abTestService.CreateAsync(userId.Value, _currentUser.OrganizationId, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Updates an existing A/B test.
    /// </summary>
    /// <param name="id">The A/B test ID to update.</param>
    /// <param name="request">A/B test update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated A/B test.</returns>
    /// <response code="200">A/B test updated successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">A/B test not found.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ABTestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ABTestDto>> Update(Guid id, [FromBody] UpdateABTestRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        try
        {
            var updated = await _abTestService.UpdateAsync(userId.Value, id, request, ct);
            return Ok(updated);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Deletes an A/B test.
    /// </summary>
    /// <param name="id">The A/B test ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">A/B test deleted successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        await _abTestService.DeleteAsync(userId.Value, id, ct);
        return NoContent();
    }

    /// <summary>
    /// Tracks an event for a variant.
    /// </summary>
    /// <remarks>
    /// Event types:
    /// - sent: Copy was sent to recipient
    /// - opened: Email was opened
    /// - clicked: Link in email was clicked
    /// - replied: Recipient replied
    /// - converted: Lead converted to opportunity
    /// </remarks>
    /// <param name="variantId">The variant ID.</param>
    /// <param name="eventType">The type of event to track.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Updated variant with new metrics.</returns>
    /// <response code="200">Event tracked successfully.</response>
    /// <response code="404">Variant not found.</response>
    [HttpPost("variants/{variantId:guid}/track")]
    [ProducesResponseType(typeof(ABTestVariantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ABTestVariantDto>> TrackEvent(Guid variantId, [FromQuery] string eventType, CancellationToken ct)
    {
        try
        {
            var variant = await _abTestService.TrackVariantEventAsync(variantId, eventType, ct);
            return Ok(variant);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }
}
