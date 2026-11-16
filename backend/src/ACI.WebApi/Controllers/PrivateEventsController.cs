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

/// <summary>
/// Private events (IPrivateEvents): a client's hire of the club's spaces, its menu and services,
/// its deposit and final invoice, its contract and its guest list. Whoever runs the members
/// (club.members) arranges them; asking the deposit and posting the invoice take money.manage, as
/// any charge does; the door (club.frontdesk) adds walk-ins and checks guests in.
/// </summary>
[ApiController]
[Route("api/private-events")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[InModule(WorkspaceModules.Club)]
public class PrivateEventsController : ControllerBase
{
    private readonly IPrivateEvents _events;
    private readonly IPermissionService _permissions;
    private readonly ICurrentUserService _user;

    public PrivateEventsController(IPrivateEvents events, IPermissionService permissions, ICurrentUserService user)
    {
        _events = events;
        _permissions = permissions;
        _user = user;
    }

    private Guid Org => _user.CurrentOrganizationId!.Value;

    /// <summary>Private events, soonest first: those overlapping a period, by status, or one client's.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PrivateEventDto>>> List([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? status,
        [FromQuery] Guid? companyId, [FromQuery] Guid? contactId, CancellationToken ct) =>
        Ok(await _events.ListAsync(Org, new PrivateEventQuery
        {
            FromUtc = from?.ToUniversalTime(), ToUtc = to?.ToUniversalTime(), Status = status, CompanyId = companyId, ContactId = contactId,
        }, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken ct) => Answer(await _events.GetAsync(Org, id, ct));

    /// <summary>Makes a private event and holds its spaces; a clash with a booking refuses it (409).</summary>
    [HttpPost]
    [Requires(MemberPermissions.ClubMembers)]
    public async Task<ActionResult> Create([FromBody] PrivateEventWriteRequest request, CancellationToken ct) =>
        Answer(await _events.CreateAsync(Org, _user.UserId!.Value, request, ct));

    [HttpPut("{id:guid}")]
    [Requires(MemberPermissions.ClubMembers)]
    public async Task<ActionResult> Update(Guid id, [FromBody] PrivateEventWriteRequest request, CancellationToken ct)
    {
        var (version, unreadable) = Request.ExpectedVersion(request.Version);
        if (unreadable != null) return unreadable.ToProblemResult();
        return Answer(await _events.UpdateAsync(Org, _user.UserId!.Value, id, request, version, ct));
    }

    [HttpPost("{id:guid}/lines")]
    [Requires(MemberPermissions.ClubMembers)]
    public async Task<ActionResult> AddLine(Guid id, [FromBody] PrivateEventLineRequest request, CancellationToken ct) =>
        Answer(await _events.AddLineAsync(Org, id, request, ct));

    [HttpPut("{id:guid}/lines/{lineId:guid}")]
    [Requires(MemberPermissions.ClubMembers)]
    public async Task<ActionResult> UpdateLine(Guid id, Guid lineId, [FromBody] PrivateEventLineRequest request, CancellationToken ct) =>
        Answer(await _events.UpdateLineAsync(Org, id, lineId, request, ct));

    [HttpDelete("{id:guid}/lines/{lineId:guid}")]
    [Requires(MemberPermissions.ClubMembers)]
    public async Task<ActionResult> RemoveLine(Guid id, Guid lineId, CancellationToken ct) =>
        Answer(await _events.RemoveLineAsync(Org, id, lineId, ct));

    [HttpPost("{id:guid}/confirm")]
    [Requires(MemberPermissions.ClubMembers)]
    public async Task<ActionResult> Confirm(Guid id, CancellationToken ct) => Answer(await _events.ConfirmAsync(Org, id, ct));

    [HttpPost("{id:guid}/complete")]
    [Requires(MemberPermissions.ClubMembers)]
    public async Task<ActionResult> Complete(Guid id, CancellationToken ct) => Answer(await _events.CompleteAsync(Org, id, ct));

    /// <summary>Cancels the event and releases its spaces; a deposit is kept or given back as credit (which takes money.manage).</summary>
    [HttpPost("{id:guid}/cancel")]
    [Requires(MemberPermissions.ClubMembers)]
    public async Task<ActionResult> Cancel(Guid id, [FromBody] PrivateEventCancelRequest request, CancellationToken ct) =>
        Answer(await _events.CancelAsync(Org, id, request, await MayHandleMoneyAsync(ct), ct));

    /// <summary>Asks the deposit: a charge on the client's house account (money.manage, as any charge takes).</summary>
    [HttpPost("{id:guid}/deposit")]
    [Requires(MemberPermissions.ManageMoney, What = "ask a private event's deposit")]
    public async Task<ActionResult> Deposit(Guid id, [FromBody] PrivateEventDepositRequest request, CancellationToken ct) =>
        Answer(await _events.RequestDepositAsync(Org, id, request, ct));

    /// <summary>Posts the final invoice: the menu and services on the client's account, the deposit set against them.</summary>
    [HttpPost("{id:guid}/invoice")]
    [Requires(MemberPermissions.ManageMoney, What = "invoice a private event")]
    public async Task<ActionResult> Invoice(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PrivateEventInvoiceRequest? request, CancellationToken ct) =>
        Answer(await _events.InvoiceAsync(Org, id, request ?? new PrivateEventInvoiceRequest(), ct));

    /// <summary>Drafts the event's contract from the private event template, on its deal.</summary>
    [HttpPost("{id:guid}/contract")]
    [Requires(MemberPermissions.ClubMembers)]
    [Requires(MemberPermissions.EditRecords, What = "draft a contract")]
    public async Task<ActionResult> Contract(Guid id, CancellationToken ct) =>
        Answer(await _events.DraftContractAsync(Org, _user.UserId!.Value, id, ct));

    [HttpPost("{id:guid}/guests")]
    [Requires(MemberPermissions.ClubMembers, MemberPermissions.FrontDesk, Any = true)]
    public async Task<ActionResult> AddGuest(Guid id, [FromBody] PrivateEventGuestRequest request, CancellationToken ct) =>
        Answer(await _events.AddGuestAsync(Org, id, request, ct));

    [HttpDelete("{id:guid}/guests/{guestId:guid}")]
    [Requires(MemberPermissions.ClubMembers)]
    public async Task<ActionResult> RemoveGuest(Guid id, Guid guestId, CancellationToken ct) =>
        Answer(await _events.RemoveGuestAsync(Org, id, guestId, ct));

    /// <summary>Checks a guest in at the door.</summary>
    [HttpPost("{id:guid}/guests/{guestId:guid}/check-in")]
    [Requires(MemberPermissions.ClubMembers, MemberPermissions.FrontDesk, Any = true)]
    public async Task<ActionResult> CheckIn(Guid id, Guid guestId, CancellationToken ct) =>
        Answer(await _events.CheckInGuestAsync(Org, id, guestId, undo: false, ct));

    /// <summary>Takes back a check-in made in error.</summary>
    [HttpPost("{id:guid}/guests/{guestId:guid}/undo-check-in")]
    [Requires(MemberPermissions.ClubMembers, MemberPermissions.FrontDesk, Any = true)]
    public async Task<ActionResult> UndoCheckIn(Guid id, Guid guestId, CancellationToken ct) =>
        Answer(await _events.CheckInGuestAsync(Org, id, guestId, undo: true, ct));

    private ActionResult Answer(Result<PrivateEventDto> result)
    {
        if (!result.IsSuccess) return result.Error.ToProblemResult();
        Response.SetETag(result.Value.Version);
        return Ok(result.Value);
    }

    private async Task<bool> MayHandleMoneyAsync(CancellationToken ct) =>
        _user.UserId is Guid userId && (await _permissions.PermissionsAsync(userId, Org, ct)).Contains(MemberPermissions.ManageMoney);
}
