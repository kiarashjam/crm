using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

// Keeping members and growing through them: the club's rules, members' at-risk scores and the
// concerns behind them, the renewals board, and referrals with their report. Reading takes
// being a member of the workspace, as the members list does; changing a member's records takes
// club.members, the club's rules settings.manage, and the report's spreadsheet records.export.

/// <summary>The club's retention and referral rules, the at-risk list and the scoring runs.</summary>
[ApiController]
[Route("api/retention")]
[Authorize]
[WorkspaceRequired]
[InModule(WorkspaceModules.Club)]
[Produces("application/json")]
public class RetentionController : ControllerBase
{
    private readonly IClubRetention _retention;
    private readonly ICurrentUserService _user;

    public RetentionController(IClubRetention retention, ICurrentUserService user)
    {
        _retention = retention;
        _user = user;
    }

    private Guid Org => _user.CurrentOrganizationId!.Value;

    /// <summary>The rules: score weights and thresholds, save actions, the referral bonus and the sponsor rule.</summary>
    [HttpGet("settings")]
    public async Task<ActionResult<RetentionSettingsDto>> Settings(CancellationToken ct)
    {
        var settings = await _retention.SettingsAsync(Org, ct);
        if (settings.Version is Guid version) Response.SetETag(version);
        return Ok(settings);
    }

    /// <summary>Saves the rules; a field left out keeps its value. A save from an older copy (version or If-Match) answers 409.</summary>
    [HttpPut("settings")]
    [Requires(MemberPermissions.ManageSettings, What = "change the club's retention and referral rules")]
    public async Task<ActionResult<RetentionSettingsDto>> SaveSettings([FromBody] RetentionSettingsRequest request, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();
        var result = await _retention.SaveSettingsAsync(Org, userId, request, version, ct);
        if (!result.IsSuccess) return result.Error.ToProblemResult();
        if (result.Value.Version is Guid saved) Response.SetETag(saved);
        return Ok(result.Value);
    }

    /// <summary>The members at risk (level at_risk, the default) or worth watching (watch), highest score first.</summary>
    [HttpGet("members")]
    public async Task<ActionResult<IReadOnlyList<AtRiskMemberDto>>> AtRisk([FromQuery] string? level, CancellationToken ct) =>
        Ok(await _retention.AtRiskAsync(Org, level, ct));

    /// <summary>The latest scoring runs, newest first.</summary>
    [HttpGet("runs")]
    public async Task<ActionResult<IReadOnlyList<ScoringRunDto>>> Runs(CancellationToken ct) => Ok(await _retention.RunsAsync(Org, 10, ct));

    /// <summary>
    /// Works out every active member's score now (after the weights change, say). A member who
    /// crosses into at risk sets the club's save actions off, as in the daily run.
    /// </summary>
    /// <response code="409">A scoring run is under way.</response>
    [HttpPost("run")]
    [Requires(MemberPermissions.ManageSettings, What = "work every member's at-risk score out now")]
    public async Task<ActionResult<ScoringRunDto>> Run(CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var result = await _retention.RunNowAsync(Org, userId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }
}

/// <summary>A member's at-risk score, the concerns recorded about them, and their referrals and sponsorship.</summary>
[ApiController]
[Route("api/members/{memberId:guid}")]
[Authorize]
[WorkspaceRequired]
[InModule(WorkspaceModules.Club)]
[Produces("application/json")]
public class MemberRetentionController : ControllerBase
{
    private readonly IClubRetention _retention;
    private readonly IClubReferrals _referrals;
    private readonly ICurrentUserService _user;

    public MemberRetentionController(IClubRetention retention, IClubReferrals referrals, ICurrentUserService user)
    {
        _retention = retention;
        _referrals = referrals;
        _user = user;
    }

    private Guid Org => _user.CurrentOrganizationId!.Value;

    /// <summary>The member's at-risk score as last worked out, with the signs behind it; <c>risk</c> is null before the first.</summary>
    [HttpGet("risk")]
    public async Task<ActionResult> Risk(Guid memberId, CancellationToken ct)
    {
        var result = await _retention.MemberRiskAsync(Org, memberId, ct);
        return result.IsSuccess ? Ok(new { risk = result.Value }) : result.Error.ToProblemResult();
    }

    /// <summary>Works the member's score out now; a crossing into at risk sets the club's save actions off.</summary>
    [HttpPost("risk/recompute")]
    [Requires(MemberPermissions.ClubMembers, What = "work a member's at-risk score out again")]
    public async Task<ActionResult<MemberRiskDto>> Recompute(Guid memberId, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var result = await _retention.RecomputeAsync(Org, memberId, userId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    [HttpGet("concerns")]
    public async Task<ActionResult<IReadOnlyList<ConcernDto>>> Concerns(Guid memberId, CancellationToken ct)
    {
        var result = await _retention.ConcernsAsync(Org, memberId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>Records a complaint or a concern about the member: an open one counts in their at-risk score.</summary>
    [HttpPost("concerns")]
    [Requires(MemberPermissions.ClubMembers, What = "record a complaint or concern about a member")]
    public async Task<ActionResult<ConcernDto>> AddConcern(Guid memberId, [FromBody] ConcernRequest request, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var result = await _retention.AddConcernAsync(Org, memberId, userId, request, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    [HttpPost("concerns/{concernId:guid}/resolve")]
    [Requires(MemberPermissions.ClubMembers, What = "resolve a complaint or concern")]
    public async Task<ActionResult<ConcernDto>> ResolveConcern(Guid memberId, Guid concernId, [FromBody] ResolveConcernRequest? request, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var result = await _retention.ResolveConcernAsync(Org, memberId, concernId, userId, request ?? new ResolveConcernRequest(), ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    [HttpDelete("concerns/{concernId:guid}")]
    [Requires(MemberPermissions.ClubMembers, What = "remove a complaint or concern")]
    [Requires(MemberPermissions.DeleteRecords, What = "remove a complaint or concern")]
    public async Task<ActionResult> DeleteConcern(Guid memberId, Guid concernId, CancellationToken ct) =>
        (await _retention.DeleteConcernAsync(Org, memberId, concernId, ct)).ToNoContentResult();

    /// <summary>Who brought the member in, who proposed and seconded them, and whom they brought in, proposed and seconded.</summary>
    [HttpGet("referrals")]
    public async Task<ActionResult<MemberReferralsDto>> Referrals(Guid memberId, CancellationToken ct)
    {
        var result = await _referrals.MemberAsync(Org, memberId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>Sets the member who referred this member, or with <c>referredByMemberId</c> null takes them off.</summary>
    [HttpPut("referrer")]
    [Requires(MemberPermissions.ClubMembers, What = "change who referred a member")]
    public async Task<ActionResult<MemberReferralsDto>> SetReferrer(Guid memberId, [FromBody] SetReferrerRequest request, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var result = await _referrals.SetReferrerAsync(Org, memberId, userId, request.ReferredByMemberId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>
    /// Gives the referral bonus for this member to the member who referred them, when it was
    /// missed or is pending: once, ever. A credit takes money.manage as well.
    /// </summary>
    /// <response code="409">The bonus for this member was given already.</response>
    [HttpPost("referral-bonus")]
    [Requires(MemberPermissions.ClubMembers, What = "give a referral bonus")]
    public async Task<ActionResult<ReferralBonusDto>> AwardBonus(Guid memberId, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var result = await _referrals.AwardAsync(Org, memberId, userId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }
}

/// <summary>The member who referred a member; null takes them off.</summary>
public sealed record SetReferrerRequest(Guid? ReferredByMemberId);

/// <summary>The renewals board.</summary>
[ApiController]
[Route("api/renewals")]
[Authorize]
[WorkspaceRequired]
[InModule(WorkspaceModules.Club)]
[Produces("application/json")]
public class RenewalsController : ControllerBase
{
    private readonly IClubRenewals _renewals;
    private readonly ICurrentUserService _user;

    public RenewalsController(IClubRenewals renewals, ICurrentUserService user)
    {
        _renewals = renewals;
        _user = user;
    }

    /// <summary>The renewals ending in the window, by stage.</summary>
    /// <param name="days">How far ahead, 1 to 365 days (60).</param>
    /// <param name="pastDays">How far back, 0 to 365 days (30), for renewals just made or missed.</param>
    /// <param name="owner">"me", a user id, or left out for everyone's members.</param>
    /// <param name="monthly">Count monthly dues periods as renewals too.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    public async Task<ActionResult<RenewalBoardDto>> Board([FromQuery] int? days, [FromQuery] int? pastDays, [FromQuery] string? owner,
        [FromQuery] bool? monthly, CancellationToken ct)
    {
        if (_user.UserId is not { } userId || _user.CurrentOrganizationId is not Guid org) return Unauthorized();
        var result = await _renewals.BoardAsync(org, userId, new RenewalBoardQuery(days, pastDays, owner, monthly), ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>
    /// Puts a renewal in a stage by hand (contacted, renewed, at_risk, lapsed) or back to the
    /// board's own (auto). Renewed moves a membership's renewal date on, once.
    /// </summary>
    /// <response code="409">Someone else changed the renewal (a stale version or If-Match).</response>
    [HttpPost("stage")]
    [Requires(MemberPermissions.ClubMembers, What = "move a member's renewal")]
    public async Task<ActionResult<RenewalCardDto>> SetStage([FromBody] RenewalStageRequest request, CancellationToken ct)
    {
        if (_user.UserId is not { } userId || _user.CurrentOrganizationId is not Guid org) return Unauthorized();
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();
        request.Version = version;
        var result = await _renewals.SetStageAsync(org, userId, request, ct);
        if (!result.IsSuccess) return result.Error.ToProblemResult();
        if (result.Value.Version is Guid saved) Response.SetETag(saved);
        return Ok(result.Value);
    }
}

/// <summary>Referrals: who brings members in, and whether a member may propose or second.</summary>
[ApiController]
[Route("api/referrals")]
[Authorize]
[WorkspaceRequired]
[InModule(WorkspaceModules.Club)]
[Produces("application/json")]
public class ReferralsController : ControllerBase
{
    private readonly IClubReferrals _referrals;
    private readonly ICurrentUserService _user;
    private readonly IWorkspaceCsv _csv;

    public ReferralsController(IClubReferrals referrals, ICurrentUserService user, IWorkspaceCsv csv)
    {
        _referrals = referrals;
        _user = user;
        _csv = csv;
    }

    /// <summary>The members who bring members in, and what those members bring, for a period (the last twelve months by default).</summary>
    [HttpGet("report")]
    public async Task<ActionResult<ReferralReportDto>> Report([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct)
    {
        if (_user.CurrentOrganizationId is not Guid org) return Unauthorized();
        var result = await _referrals.ReportAsync(org, new ReferralReportQuery(from?.UtcDateTime, to?.UtcDateTime), ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>The same as a spreadsheet (CSV): one row per member brought in, with their referrer.</summary>
    [HttpGet("report/export")]
    [Requires(MemberPermissions.ExportRecords, What = "export the referrals report")]
    [Produces("text/csv")]
    public async Task<ActionResult> Export([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct)
    {
        if (_user.CurrentOrganizationId is not Guid org) return Unauthorized();
        var result = await _referrals.ReportAsync(org, new ReferralReportQuery(from?.UtcDateTime, to?.UtcDateTime), ct);
        if (!result.IsSuccess) return result.Error.ToProblemResult();
        var separator = await _csv.SeparatorAsync(org, ct);
        return File(ReferralReportCsv.Write(result.Value, separator), "text/csv; charset=utf-8", ReferralReportCsv.FileName(result.Value));
    }

    /// <summary>Whether each member may propose or second by the club's rule, and if not why: the application form's hints.</summary>
    [HttpGet("standing")]
    public async Task<ActionResult<IReadOnlyList<SponsorStandingDto>>> Standing([FromQuery] Guid[] memberIds, CancellationToken ct)
    {
        if (_user.CurrentOrganizationId is not Guid org) return Unauthorized();
        return Ok(await _referrals.StandingAsync(org, memberIds.Take(50).ToList(), ct));
    }
}
