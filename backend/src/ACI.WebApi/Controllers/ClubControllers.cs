using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ACI.Domain.Common;

namespace ACI.WebApi.Controllers;

[ApiController]
[Route("api/members")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.ClubMembers, WritesOnly = true)]
[InModule(WorkspaceModules.Club)]
public class MembersController : ControllerBase
{
    private readonly IClubService _club;
    private readonly ICurrentUserService _currentUser;
    public MembersController(IClubService club, ICurrentUserService currentUser)
    {
        _club = club;
        _currentUser = currentUser;
    }

    /// <summary>The club's members, narrowed by their tags and custom fields when the query string names any (tag, notTag, tagMatch, field).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MemberDto>>> List([FromQuery] FlexQueryParams? flex, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return Ok(await _club.MembersAsync(userId, _currentUser.CurrentOrganizationId, ct, (flex ?? new FlexQueryParams()).ToFilter()));
    }

    [HttpPost]
    public async Task<ActionResult<MemberDto>> Create([FromBody] MemberWriteRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _club.CreateMemberAsync(userId, _currentUser.CurrentOrganizationId, request, ct)).ToActionResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<MemberDto>> Update(Guid id, [FromBody] MemberWriteRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return (await _club.UpdateMemberAsync(id, _currentUser.CurrentOrganizationId, request, ct)).ToActionResult();
    }

    [Requires(MemberPermissions.DeleteRecords)]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return (await _club.DeleteMemberAsync(id, _currentUser.CurrentOrganizationId, ct)).ToNoContentResult();
    }

    /// <summary>Keeps this member and folds the other one into it.</summary>
    [Requires(MemberPermissions.DeleteRecords)]
    [HttpPost("{id:guid}/merge")]
    public async Task<ActionResult> Merge(Guid id, [FromBody] MergeMembersRequest request, CancellationToken ct)
    {
        if (request == null || request.OtherMemberId == Guid.Empty) return BadRequest();
        if (_currentUser.UserId == null) return Unauthorized();
        return (await _club.MergeMembersAsync(id, request.OtherMemberId, _currentUser.CurrentOrganizationId, ct)).ToNoContentResult();
    }
}

[ApiController]
[Route("api/visits")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.FrontDesk, WritesOnly = true)]
[InModule(WorkspaceModules.Club)]
public class VisitsController : ControllerBase
{
    private readonly IClubService _club;
    private readonly ICurrentUserService _currentUser;
    public VisitsController(IClubService club, ICurrentUserService currentUser)
    {
        _club = club;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<VisitDto>>> List(CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return Ok(await _club.VisitsAsync(_currentUser.CurrentOrganizationId, ct));
    }

    [HttpPost]
    public async Task<ActionResult<VisitDto>> Create([FromBody] VisitWriteRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return (await _club.CheckInAsync(_currentUser.CurrentOrganizationId, request, ct)).ToActionResult();
    }

    [Requires(MemberPermissions.DeleteRecords)]
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return (await _club.DeleteVisitAsync(id, _currentUser.CurrentOrganizationId, ct)).ToNoContentResult();
    }
}

[ApiController]
[Route("api/loyalty")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[InModule(WorkspaceModules.Club)]
public class LoyaltyController : ControllerBase
{
    private readonly IClubService _club;
    private readonly ICurrentUserService _currentUser;
    public LoyaltyController(IClubService club, ICurrentUserService currentUser)
    {
        _club = club;
        _currentUser = currentUser;
    }

    [HttpGet("ledger")]
    public async Task<ActionResult<IReadOnlyList<LoyaltyEntryDto>>> Ledger(CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return Ok(await _club.LedgerAsync(_currentUser.CurrentOrganizationId, ct));
    }

    // The front desk too: the points a check-in earns (at the desk, at the POS, at an
    // event's door) are written by the app as a ledger entry straight after the visit.
    // With club members alone, every front-desk check-in failed after the visit was saved
    // and earned nothing, though checking members in is what that preset is for. Taking
    // points away stays with whoever manages club members (Delete).
    [Requires(MemberPermissions.ClubMembers, MemberPermissions.FrontDesk, Any = true)]
    [HttpPost("ledger")]
    public async Task<ActionResult<LoyaltyEntryDto>> Add([FromBody] LoyaltyWriteRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return (await _club.AddLedgerAsync(_currentUser.CurrentOrganizationId, request, ct)).ToActionResult();
    }

    [Requires(MemberPermissions.ClubMembers)]
    [Requires(MemberPermissions.DeleteRecords)]
    [HttpDelete("ledger/{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return (await _club.DeleteLedgerAsync(id, _currentUser.CurrentOrganizationId, ct)).ToNoContentResult();
    }
}
