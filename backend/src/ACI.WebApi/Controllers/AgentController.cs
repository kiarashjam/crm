using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Assistant;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ACI.WebApi.Controllers;

/// <summary>
/// The assistant: whether a language model is set up, the workspace's settings for it (its
/// monthly limit and morning briefs), the chat, and the caller's morning brief.
/// </summary>
/// <remarks>
/// In no module of its own: it answers about the Sales module's records and the Club module's,
/// offering only the tools of the modules the workspace has on (AssistantTools).
/// </remarks>
[ApiController]
[Route("api/agent")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
public class AgentController : ControllerBase
{
    private readonly IAssistantSettingsService _settings;
    private readonly ICurrentUserService _currentUser;
    private readonly AssistantChat _chat;
    private readonly IMorningBrief _brief;

    public AgentController(IAssistantSettingsService settings, ICurrentUserService currentUser, AssistantChat chat, IMorningBrief brief)
    {
        _settings = settings;
        _currentUser = currentUser;
        _chat = chat;
        _brief = brief;
    }

    /// <summary>
    /// Asks the assistant. The language model answers from the tools the caller may use, reading
    /// the records as the caller sees them, and proposes changes rather than making them: each
    /// comes back as a proposal the app shows for the caller to confirm, and only that confirm
    /// sends it to the endpoint that makes it.
    /// </summary>
    /// <response code="200">The answer, with any proposed changes and what was looked up.</response>
    /// <response code="429">Asked too fast, the workspace's team is asking too much at once, or its monthly AI allowance is used up.</response>
    /// <response code="502">The language model did not answer properly (errorCode Assistant.ModelFailed).</response>
    /// <response code="503">No language model is set up on the server (errorCode Assistant.NotConfigured).</response>
    [HttpPost("chat")]
    [EnableRateLimiting("assistant")]
    [ProducesResponseType(typeof(AssistantChatReply), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> Chat([FromBody] AssistantChatRequest request, CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org || _currentUser.UserId is not Guid user) return Unauthorized();
        var result = await _chat.AskAsync(org, user, _currentUser.UserName, request, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>
    /// The caller's morning brief: their day in this workspace worked out now (tasks due, deals
    /// gone quiet, and as their role allows the club's day), and the brief that went out this
    /// morning, as it was written, when one did. Built from the records as the caller may see
    /// them; it calls no language model.
    /// </summary>
    [HttpGet("brief")]
    [ProducesResponseType(typeof(MorningBriefView), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> Brief(CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org || _currentUser.UserId is not Guid user) return Unauthorized();
        return Ok(await _brief.TodayAsync(org, user, ct));
    }

    /// <summary>
    /// The workspace's settings for the assistant, with this month's calls to the model so far.
    /// Anyone in the workspace may read them; the app reads <c>configured</c> to decide whether
    /// the chat asks the model or answers from its built-in rules.
    /// </summary>
    [HttpGet("settings")]
    [ProducesResponseType(typeof(AssistantSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> GetSettings(CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org || _currentUser.UserId is not Guid user) return Unauthorized();
        var result = await _settings.GetAsync(org, user, ct);
        if (!result.IsSuccess) return result.Error.ToProblemResult();
        Response.SetETag(result.Value.Version);
        return Ok(result.Value);
    }

    /// <summary>
    /// Changes the workspace's monthly limit on calls to the model (0 up to the server's maximum,
    /// or back to the server's default) and whether its people get a morning brief. Send the
    /// version read, in the body or If-Match; a stale one gets 409.
    /// </summary>
    [HttpPut("settings")]
    [Requires(MemberPermissions.ManageSettings, What = "change the assistant's settings")]
    [ProducesResponseType(typeof(AssistantSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> SaveSettings([FromBody] UpdateAssistantSettingsRequest request, CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org || _currentUser.UserId is not Guid user) return Unauthorized();
        var (expected, badVersion) = Request.ExpectedVersion(request.Version);
        if (badVersion != null) return badVersion.ToProblemResult();
        var result = await _settings.UpdateAsync(org, user, request, expected, ct);
        if (!result.IsSuccess) return result.Error.ToProblemResult();
        Response.SetETag(result.Value.Version);
        return Ok(result.Value);
    }
}
