using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Checks email copy for spam indicators.
/// </summary>
/// <remarks>
/// Analyzes copy content for common spam triggers and provides
/// a score and suggestions for improvement. Helps ensure emails
/// reach recipients' inboxes instead of spam folders.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class SpamCheckController : ControllerBase
{
    private readonly ISpamCheckService _spamCheckService;

    /// <summary>
    /// Initializes a new instance of the SpamCheckController.
    /// </summary>
    public SpamCheckController(ISpamCheckService spamCheckService)
    {
        _spamCheckService = spamCheckService;
    }

    /// <summary>
    /// Checks copy for spam score and issues.
    /// </summary>
    /// <remarks>
    /// Returns a spam score (0-100) where lower is better.
    /// Also provides a list of specific issues found and suggestions
    /// for improving deliverability.
    /// 
    /// Common issues detected:
    /// - Excessive capitalization
    /// - Spam trigger words
    /// - Too many links
    /// - Missing unsubscribe link
    /// - Subject line issues
    /// </remarks>
    /// <param name="request">Content to check for spam indicators.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Spam check results with score and issues.</returns>
    /// <response code="200">Returns the spam check results.</response>
    [HttpPost]
    [ProducesResponseType(typeof(SpamCheckResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SpamCheckResponse>> Check([FromBody] SpamCheckRequest request, CancellationToken ct)
    {
        var result = await _spamCheckService.CheckAsync(request, ct);
        return Ok(result);
    }
}
