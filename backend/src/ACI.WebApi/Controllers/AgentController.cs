using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// The assistant: whether a language model is set up, the workspace's settings for it (its
/// monthly limit and morning briefs), and the chat.
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

    public AgentController(IAssistantSettingsService settings, ICurrentUserService currentUser)
    {
        _settings = settings;
        _currentUser = currentUser;
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
