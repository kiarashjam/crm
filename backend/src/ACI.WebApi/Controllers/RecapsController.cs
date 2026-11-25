using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

// The workspace's recap emails (IRecapEmails). In no module: a recap holds the Sales module's
// figures and the club's, each part following its own module; a workspace with both off has
// nothing to report and is sent nothing.

/// <summary>Who receives the workspace's recap emails, and how often: for those who manage the settings.</summary>
[ApiController]
[Route("api/recaps")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
public class RecapsController : ControllerBase
{
    private readonly IRecapEmails _recaps;
    private readonly ICurrentUserService _user;

    public RecapsController(IRecapEmails recaps, ICurrentUserService user)
    {
        _recaps = recaps;
        _user = user;
    }

    /// <summary>The people who may receive recaps (those who see all deals), each with their daily, weekly and monthly recap.</summary>
    [HttpGet("recipients")]
    [Requires(MemberPermissions.ManageSettings, What = "see who receives the recap emails")]
    public async Task<ActionResult<RecapRecipientsDto>> Recipients(CancellationToken ct) =>
        Ok(await _recaps.RecipientsAsync(_user.CurrentOrganizationId!.Value, ct));

    /// <summary>Switches one person's recaps on or off; a frequency left out is kept. Switching one on takes someone who sees all deals.</summary>
    /// <response code="400">They do not see all deals (Recap.NotEligible).</response>
    /// <response code="404">They are not on the workspace's team.</response>
    [HttpPut("recipients/{userId:guid}")]
    [Requires(MemberPermissions.ManageSettings, What = "choose who receives the recap emails")]
    public async Task<ActionResult<RecapRecipientDto>> SetRecipient(Guid userId, [FromBody] RecapFrequenciesRequest request, CancellationToken ct)
    {
        if (_user.UserId is not { } me) return Unauthorized();
        var result = await _recaps.SetRecipientAsync(_user.CurrentOrganizationId!.Value, me, userId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }
}

/// <summary>
/// The caller's own recap emails in the workspace (Settings › Notifications). Under /api/settings
/// with the caller's other preferences, so a read-only member can switch theirs too.
/// </summary>
[ApiController]
[Route("api/settings/recaps")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
public class MyRecapsController : ControllerBase
{
    private readonly IRecapEmails _recaps;
    private readonly ICurrentUserService _user;

    public MyRecapsController(IRecapEmails recaps, ICurrentUserService user)
    {
        _recaps = recaps;
        _user = user;
    }

    /// <summary>Which recaps the caller receives here, and whether they may (they see all deals).</summary>
    [HttpGet]
    public async Task<ActionResult<MyRecapsDto>> Mine(CancellationToken ct)
    {
        if (_user.UserId is not { } me) return Unauthorized();
        return Ok(await _recaps.MineAsync(_user.CurrentOrganizationId!.Value, me, ct));
    }

    /// <summary>Switches the caller's own recaps on or off; a frequency left out is kept.</summary>
    /// <response code="400">Switching one on, for someone who does not see all deals (Recap.NotEligible).</response>
    [HttpPut]
    public async Task<ActionResult<MyRecapsDto>> SetMine([FromBody] RecapFrequenciesRequest request, CancellationToken ct)
    {
        if (_user.UserId is not { } me) return Unauthorized();
        var result = await _recaps.SetMineAsync(_user.CurrentOrganizationId!.Value, me, request, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }
}
