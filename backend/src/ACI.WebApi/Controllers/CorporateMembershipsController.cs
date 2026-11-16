using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Memberships held by companies (ICorporateMemberships): their seats and nominees, their dues
/// on the company's house account, their renewal, and the company's statement. Whoever runs the
/// members (club.members) changes them; their dues take money.manage.
/// </summary>
[ApiController]
[Route("api/corporate-memberships")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.ClubMembers, WritesOnly = true)]
[InModule(WorkspaceModules.Club)]
public class CorporateMembershipsController : ControllerBase
{
    private readonly ICorporateMemberships _corporate;
    private readonly IClubBilling _billing;
    private readonly IPermissionService _permissions;
    private readonly ICurrentUserService _user;

    public CorporateMembershipsController(ICorporateMemberships corporate, IClubBilling billing, IPermissionService permissions, ICurrentUserService user)
    {
        _corporate = corporate;
        _billing = billing;
        _permissions = permissions;
        _user = user;
    }

    private Guid Org => _user.CurrentOrganizationId!.Value;

    /// <summary>The workspace's corporate memberships: one company's, by status, or those whose renewal is near.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CorporateMembershipDto>>> List([FromQuery] Guid? companyId, [FromQuery] string? status,
        [FromQuery] int? renewingWithinDays, CancellationToken ct) =>
        Ok(await _corporate.ListAsync(Org, new CorporateMembershipQuery { CompanyId = companyId, Status = status, RenewingWithinDays = renewingWithinDays }, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken ct) => Answer(await _corporate.GetAsync(Org, id, ct));

    /// <summary>Makes a corporate membership for a company; dues sent with it start on the company's account (money.manage).</summary>
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CorporateMembershipWriteRequest request, CancellationToken ct) =>
        Answer(await _corporate.CreateAsync(Org, request, await MayHandleMoneyAsync(ct), ct));

    /// <summary>Changes the membership's details, from the version read (body or If-Match).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] CorporateMembershipWriteRequest request, CancellationToken ct)
    {
        var (version, unreadable) = Request.ExpectedVersion(request.Version);
        if (unreadable != null) return unreadable.ToProblemResult();
        return Answer(await _corporate.UpdateAsync(Org, id, request, version, ct));
    }

    /// <summary>Starts or changes the dues billed to the company.</summary>
    [HttpPut("{id:guid}/dues")]
    [Requires(MemberPermissions.ManageMoney, What = "set a corporate membership's dues")]
    public async Task<ActionResult> Dues(Guid id, [FromBody] CorporateDuesRequest request, CancellationToken ct) =>
        Answer(await _corporate.SetDuesAsync(Org, id, request, ct));

    [HttpPost("{id:guid}/nominees")]
    public async Task<ActionResult> AddNominee(Guid id, [FromBody] NomineeRequest request, CancellationToken ct) =>
        Answer(await _corporate.AddNomineeAsync(Org, id, request, ct));

    /// <summary>Gives a nominee's seat to another member, in one step.</summary>
    [HttpPost("{id:guid}/nominees/{nomineeId:guid}/swap")]
    public async Task<ActionResult> Swap(Guid id, Guid nomineeId, [FromBody] NomineeRequest request, CancellationToken ct) =>
        Answer(await _corporate.SwapNomineeAsync(Org, id, nomineeId, request, ct));

    [HttpPost("{id:guid}/nominees/{nomineeId:guid}/remove")]
    public async Task<ActionResult> Remove(Guid id, Guid nomineeId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] NomineeRequest? request, CancellationToken ct) =>
        Answer(await _corporate.RemoveNomineeAsync(Org, id, nomineeId, request?.Reason, ct));

    [HttpPost("{id:guid}/renew")]
    public async Task<ActionResult> Renew(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReasonRequest? request, CancellationToken ct)
    {
        var (version, unreadable) = Request.ExpectedVersion(request?.Version);
        if (unreadable != null) return unreadable.ToProblemResult();
        return Answer(await _corporate.RenewAsync(Org, id, version, ct));
    }

    [HttpPost("{id:guid}/not-renewing")]
    public async Task<ActionResult> NotRenewing(Guid id, [FromBody] ReasonRequest request, CancellationToken ct)
    {
        var (version, unreadable) = Request.ExpectedVersion(request.Version);
        if (unreadable != null) return unreadable.ToProblemResult();
        return Answer(await _corporate.MarkNotRenewingAsync(Org, id, request.Reason, version, ct));
    }

    /// <summary>Ends the membership: its nominees leave their seats and its dues stop (which takes money.manage while they run).</summary>
    [HttpPost("{id:guid}/end")]
    public async Task<ActionResult> End(Guid id, [FromBody] ReasonRequest request, CancellationToken ct) =>
        Answer(await _corporate.EndAsync(Org, id, request.Reason, await MayHandleMoneyAsync(ct), ct));

    /// <summary>The company's statement for the period as a PDF: its own charges and its nominees', in the membership's language.</summary>
    [HttpGet("{id:guid}/statement.pdf")]
    [Produces("application/pdf", "application/json")]
    public async Task<ActionResult> StatementPdf(Guid id, [FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] string? language, CancellationToken ct)
    {
        var membership = await _corporate.GetAsync(Org, id, ct);
        if (!membership.IsSuccess) return membership.Error.ToProblemResult();
        var file = await _billing.ClientStatementPdfAsync(Org, membership.Value.CompanyId, from.ToUniversalTime(), to.ToUniversalTime(),
            language ?? membership.Value.Language, membership.Value.BillingEmail, ct);
        if (!file.IsSuccess) return file.Error.ToProblemResult();
        return File(file.Value.Content, "application/pdf", file.Value.FileName);
    }

    private ActionResult Answer(Result<CorporateMembershipDto> result)
    {
        if (!result.IsSuccess) return result.Error.ToProblemResult();
        Response.SetETag(result.Value.Version);
        return Ok(result.Value);
    }

    private async Task<bool> MayHandleMoneyAsync(CancellationToken ct) =>
        _user.UserId is Guid userId && (await _permissions.PermissionsAsync(userId, Org, ct)).Contains(MemberPermissions.ManageMoney);
}

/// <summary>The corporate seat a member holds, for their page.</summary>
[ApiController]
[Route("api/members/{memberId:guid}/corporate")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[InModule(WorkspaceModules.Club)]
public class MemberCorporateSeatController : ControllerBase
{
    private readonly ICorporateMemberships _corporate;
    private readonly ICurrentUserService _user;

    public MemberCorporateSeatController(ICorporateMemberships corporate, ICurrentUserService user)
    {
        _corporate = corporate;
        _user = user;
    }

    /// <summary>The seat, or 204 when the member holds none.</summary>
    [HttpGet]
    public async Task<ActionResult> Get(Guid memberId, CancellationToken ct)
    {
        var seat = await _corporate.SeatOfMemberAsync(_user.CurrentOrganizationId!.Value, memberId, ct);
        return seat == null ? NoContent() : Ok(seat);
    }
}
