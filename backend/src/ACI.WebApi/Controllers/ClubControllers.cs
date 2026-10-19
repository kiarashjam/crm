using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

[ApiController]
[Route("api/members")]
[Authorize]
[Produces("application/json")]
public class MembersController : ControllerBase
{
    private readonly IClubService _club;
    private readonly ICurrentUserService _currentUser;
    public MembersController(IClubService club, ICurrentUserService currentUser)
    {
        _club = club;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MemberDto>>> List(CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return Ok(await _club.MembersAsync(userId, _currentUser.CurrentOrganizationId, ct));
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

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return (await _club.DeleteMemberAsync(id, _currentUser.CurrentOrganizationId, ct)).ToNoContentResult();
    }
}

[ApiController]
[Route("api/visits")]
[Authorize]
[Produces("application/json")]
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
[Produces("application/json")]
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

    [HttpPost("ledger")]
    public async Task<ActionResult<LoyaltyEntryDto>> Add([FromBody] LoyaltyWriteRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return (await _club.AddLedgerAsync(_currentUser.CurrentOrganizationId, request, ct)).ToActionResult();
    }

    [HttpDelete("ledger/{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        return (await _club.DeleteLedgerAsync(id, _currentUser.CurrentOrganizationId, ct)).ToNoContentResult();
    }
}
