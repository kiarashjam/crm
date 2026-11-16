using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// The club's billing: what billing now would post, billing it, and the runs so far (what
/// each billed, chased, lapsed and sent, and what failed).
/// </summary>
[ApiController]
[Route("api/billing")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[InModule(WorkspaceModules.Club)]
public class BillingController : ControllerBase
{
    private readonly IClubBilling _billing;
    private readonly ICurrentUserService _user;

    public BillingController(IClubBilling billing, ICurrentUserService user)
    {
        _billing = billing;
        _user = user;
    }

    /// <summary>The latest runs, newest first: the daily ones and those someone started.</summary>
    [HttpGet("runs")]
    [Requires(MemberPermissions.ManageMoney, MemberPermissions.ManageSettings, Any = true, What = "see the billing runs")]
    public async Task<ActionResult<IReadOnlyList<BillingRunDto>>> Runs([FromQuery] int take = 20, CancellationToken ct = default) =>
        Ok(await _billing.RunsAsync(_user.CurrentOrganizationId!.Value, take, ct));

    /// <summary>Every period that has started and is not billed yet, with what it comes to. Posts nothing.</summary>
    [HttpGet("preview")]
    [Requires(MemberPermissions.ManageMoney, MemberPermissions.ManageSettings, Any = true, What = "see what billing would post")]
    public async Task<ActionResult<BillingPreviewDto>> Preview(CancellationToken ct) =>
        Ok(await _billing.PreviewAsync(_user.CurrentOrganizationId!.Value, ct));

    /// <summary>Bills now every period that has started, as the daily run does when automatic billing is on. Recorded as a run.</summary>
    [HttpPost("run")]
    [Requires(MemberPermissions.ManageMoney, What = "bill what is due")]
    public async Task<ActionResult> Run(CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var run = await _billing.RunNowAsync(_user.CurrentOrganizationId!.Value, userId, ct);
        return run.IsSuccess ? Ok(run.Value) : run.Error.ToProblemResult();
    }
}
