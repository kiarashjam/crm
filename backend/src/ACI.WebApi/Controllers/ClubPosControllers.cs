using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ACI.WebApi.Controllers;

// The club's POS, connected (IClubPos): Lightspeed Restaurant K-Series, whose closed tickets
// come in by a scheduled import and are posted to members' house accounts through the ledger.
// Connecting it, its settings and dealing with tickets take money.manage; every member of the
// workspace may read the tickets and whether a POS is connected, as they read the charges.

/// <summary>The POS connection, its import and the tickets that wait for staff.</summary>
[ApiController]
[Route("api/pos")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.ManageMoney, WritesOnly = true)]
public class PosController : ControllerBase
{
    private readonly IClubPos _pos;
    private readonly ICurrentUserService _user;

    public PosController(IClubPos pos, ICurrentUserService user)
    {
        _pos = pos;
        _user = user;
    }

    private Guid Org => _user.CurrentOrganizationId!.Value;

    /// <summary>Whether a POS is connected and importing, and how many tickets wait for staff.</summary>
    [HttpGet("status")]
    public async Task<ActionResult<PosStatusDto>> Status(CancellationToken ct) => Ok(await _pos.StatusAsync(Org, ct));

    /// <summary>The POS settings, without its tokens. Only for someone who may change them.</summary>
    [HttpGet("settings")]
    [Requires(MemberPermissions.ManageMoney, What = "see the POS settings")]
    public async Task<ActionResult<PosSettingsDto>> Settings(CancellationToken ct)
    {
        var settings = await _pos.SettingsAsync(Org, ct);
        Response.SetETag(settings.Version);
        return Ok(settings);
    }

    [HttpPut("settings")]
    public async Task<ActionResult> SaveSettings([FromBody] PosSettingsRequest request, CancellationToken ct)
    {
        var (version, unreadable) = Request.ExpectedVersion(request.Version);
        if (unreadable != null) return unreadable.ToProblemResult();
        var saved = await _pos.SaveSettingsAsync(Org, _user.UserId!.Value, request, version, ct);
        if (!saved.IsSuccess) return saved.Error.ToProblemResult();
        Response.SetETag(saved.Value.Version);
        return Ok(saved.Value);
    }

    /// <summary>Where to send the browser to let Cadence read the club's Lightspeed account.</summary>
    [HttpPost("connect")]
    public async Task<ActionResult> Connect(CancellationToken ct)
    {
        var url = await _pos.ConnectUrlAsync(Org, _user.UserId!.Value, ct);
        return url.IsSuccess ? Ok(new { url = url.Value }) : url.Error.ToProblemResult();
    }

    /// <summary>
    /// Finishes connecting: Lightspeed sent the browser back to the POS tickets page with a code
    /// and the state it was given, and the page passes them on. Only whoever started connecting
    /// finishes it, in the workspace they started it in.
    /// </summary>
    [HttpPost("connect/complete")]
    public async Task<ActionResult> CompleteConnect([FromBody] PosConnectRequest request, CancellationToken ct)
    {
        var done = await _pos.CompleteConnectAsync(Org, _user.UserId!.Value, request.State, request.Code, ct);
        if (!done.IsSuccess) return done.Error.ToProblemResult();
        Response.SetETag(done.Value.Version);
        return Ok(done.Value);
    }

    /// <summary>Forgets the account's tokens and stops the import. Tickets already imported stay.</summary>
    [HttpPost("disconnect")]
    public async Task<ActionResult> Disconnect(CancellationToken ct)
    {
        var done = await _pos.DisconnectAsync(Org, _user.UserId!.Value, ct);
        return done.IsSuccess ? Ok(done.Value) : done.Error.ToProblemResult();
    }

    /// <summary>The connected account's business locations, to choose whose tickets to import.</summary>
    [HttpGet("locations")]
    [Requires(MemberPermissions.ManageMoney, What = "set up the POS")]
    public async Task<ActionResult> Locations(CancellationToken ct)
    {
        var locations = await _pos.LocationsAsync(Org, ct);
        return locations.IsSuccess ? Ok(locations.Value) : locations.Error.ToProblemResult();
    }

    /// <summary>Imports what the POS closed since the last import, now, and posts what it can.</summary>
    [HttpPost("import")]
    public async Task<ActionResult> Import(CancellationToken ct)
    {
        var result = await _pos.ImportNowAsync(Org, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>Tickets, newest first: those waiting for staff (the default), one status, or all.</summary>
    [HttpGet("tickets")]
    public async Task<ActionResult<IReadOnlyList<PosTicketDto>>> Tickets([FromQuery] string? status, [FromQuery] int? take, CancellationToken ct) =>
        Ok(await _pos.TicketsAsync(Org, status, take ?? 100, ct));

    /// <summary>Says whose a ticket is, posts it to them, and (by default) remembers the POS's customer on it as theirs.</summary>
    [HttpPost("tickets/{id:guid}/match")]
    public async Task<ActionResult> Match(Guid id, [FromBody] PosMatchRequest request, CancellationToken ct)
    {
        var (version, unreadable) = Request.ExpectedVersion(request.Version);
        if (unreadable != null) return unreadable.ToProblemResult();
        request.Version = version;
        var result = await _pos.MatchAsync(Org, _user.UserId!.Value, id, request, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>Tries a ticket again, with the members' codes and the POS settings as they are now.</summary>
    [HttpPost("tickets/{id:guid}/retry")]
    public async Task<ActionResult> Retry(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PosIgnoreRequest? body, CancellationToken ct)
    {
        var (version, unreadable) = Request.ExpectedVersion(body?.Version);
        if (unreadable != null) return unreadable.ToProblemResult();
        var result = await _pos.RetryAsync(Org, id, version, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>Sets a ticket aside as not a member's, with why.</summary>
    [HttpPost("tickets/{id:guid}/ignore")]
    public async Task<ActionResult> Ignore(Guid id, [FromBody] PosIgnoreRequest request, CancellationToken ct)
    {
        var (version, unreadable) = Request.ExpectedVersion(request.Version);
        if (unreadable != null) return unreadable.ToProblemResult();
        var result = await _pos.IgnoreAsync(Org, _user.UserId!.Value, id, request.Reason, version, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }
}

/// <summary>The codes that name a member at the POS: a member number, a card's code, or the POS's own customer.</summary>
[ApiController]
[Route("api/members/{memberId:guid}/codes")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.ClubMembers, WritesOnly = true)]
public class MemberCodesController : ControllerBase
{
    private readonly IClubPos _pos;
    private readonly ICurrentUserService _user;

    public MemberCodesController(IClubPos pos, ICurrentUserService user)
    {
        _pos = pos;
        _user = user;
    }

    [HttpGet]
    public async Task<ActionResult> List(Guid memberId, CancellationToken ct)
    {
        var codes = await _pos.MemberCodesAsync(_user.CurrentOrganizationId!.Value, memberId, ct);
        return codes.IsSuccess ? Ok(codes.Value) : codes.Error.ToProblemResult();
    }

    /// <summary>Gives the member a code: the one sent, as the card or the POS shows it, or a new one Cadence makes.</summary>
    [HttpPost]
    public async Task<ActionResult> Add(Guid memberId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] MemberCodeRequest? request, CancellationToken ct)
    {
        var code = await _pos.AddMemberCodeAsync(_user.CurrentOrganizationId!.Value, memberId, request ?? new MemberCodeRequest(), ct);
        return code.IsSuccess ? Ok(code.Value) : code.Error.ToProblemResult();
    }

    [HttpDelete("{codeId:guid}")]
    public async Task<ActionResult> Remove(Guid memberId, Guid codeId, CancellationToken ct) =>
        (await _pos.RemoveMemberCodeAsync(_user.CurrentOrganizationId!.Value, memberId, codeId, ct)).ToNoContentResult();
}
