using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Intelligent Sales Writer - generates CRM-ready copy using AI or templates.
/// </summary>
/// <remarks>
/// Supports multiple generation modes:
/// - Basic generation with type and goal
/// - Generation with recipient context for personalization
/// - Rewriting with different tones (shorter, friendlier, persuasive)
/// - Multi-language generation
/// </remarks>
[ApiController]
[Route("api/copy")]
[Authorize]
[Produces("application/json")]
public class CopyController : ControllerBase
{
    private readonly ICopyGeneratorApplicationService _generatorService;
    private readonly ISendToCrmService _sendToCrmService;
    private readonly ICurrentUserService _currentUser;

    public CopyController(
        ICopyGeneratorApplicationService generatorService,
        ISendToCrmService sendToCrmService,
        ICurrentUserService currentUser)
    {
        _generatorService = generatorService;
        _sendToCrmService = sendToCrmService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Generates copy based on type, goal, and context.
    /// </summary>
    /// <param name="request">Generation request with copy type, goal, and context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The generated copy text.</returns>
    /// <response code="200">Returns the generated copy.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<object>> Generate([FromBody] GenerateCopyRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var copy = await _generatorService.GenerateAsync(userId.Value, request, ct);
        return Ok(new { copy });
    }

    /// <summary>
    /// Generates copy with recipient context for personalization and optional subject line.
    /// </summary>
    /// <remarks>
    /// Uses recipient information (name, company, role) to personalize the generated copy.
    /// Also generates an appropriate subject line for email types.
    /// </remarks>
    /// <param name="request">Generation request with recipient context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Generated copy with optional subject line.</returns>
    /// <response code="200">Returns the generated copy and subject.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost("generate-with-recipient")]
    [ProducesResponseType(typeof(GenerateCopyWithSubjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GenerateCopyWithSubjectResponse>> GenerateWithRecipient(
        [FromBody] GenerateCopyWithRecipientRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _generatorService.GenerateWithRecipientAsync(userId.Value, request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Rewrites existing copy with a different tone/style.
    /// </summary>
    /// <remarks>
    /// Supported rewrite modes:
    /// - shorter: More concise version
    /// - friendlier: More casual and warm
    /// - persuasive: More compelling and action-oriented
    /// - professional: More formal and business-like
    /// </remarks>
    /// <param name="request">Rewrite request with original copy and target mode.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The rewritten copy.</returns>
    /// <response code="200">Returns the rewritten copy.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost("rewrite")]
    [ProducesResponseType(typeof(RewriteCopyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RewriteCopyResponse>> Rewrite(
        [FromBody] RewriteCopyRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var copy = await _generatorService.RewriteAsync(userId.Value, request, ct);
        return Ok(new RewriteCopyResponse(copy));
    }

    /// <summary>
    /// Generates copy in a specific language.
    /// </summary>
    /// <remarks>
    /// Supports multiple languages including English, Spanish, French, German, etc.
    /// The generated copy will be fully translated to the target language.
    /// </remarks>
    /// <param name="request">Generation request with target language.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Generated copy in the specified language.</returns>
    /// <response code="200">Returns the generated copy.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost("generate-multilang")]
    [ProducesResponseType(typeof(GenerateCopyWithSubjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GenerateCopyWithSubjectResponse>> GenerateMultiLanguage(
        [FromBody] GenerateCopyMultiLanguageRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _generatorService.GenerateInLanguageAsync(userId.Value, request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Sends generated copy to CRM and creates a history record.
    /// </summary>
    /// <remarks>
    /// Records the sent copy in history for tracking and analytics.
    /// Can be linked to a contact, lead, or deal.
    /// </remarks>
    /// <param name="request">Send request with copy and recipient information.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result of the send operation.</returns>
    /// <response code="200">Copy sent successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost("send")]
    [ProducesResponseType(typeof(SendToCrmResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SendToCrmResult>> SendToCrm([FromBody] SendToCrmRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var result = await _sendToCrmService.SendAsync(userId.Value, _currentUser.CurrentOrganizationId, request, ct);
        return Ok(result);
    }
}
