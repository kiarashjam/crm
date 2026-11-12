using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

// The club's bookable resources and their bookings, on the server (IClubReservations): a clash
// or more people than a resource holds is a 409 naming what is in the way, a booking is for a
// member by id, and deposits and no-show fees go through the ledger. club.members manages the
// resources; club.frontdesk takes the bookings.

/// <summary>Shared answering for the bookings routes.</summary>
file static class Bookings
{
    public static ActionResult Answer<T>(ControllerBase controller, Result<T> result, Func<T, Guid?>? version = null)
    {
        if (!result.IsSuccess) return result.Error.ToProblemResult();
        if (version != null) controller.Response.SetETag(version(result.Value));
        return controller.Ok(result.Value);
    }
}

[ApiController]
[Route("api/resources")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.ClubMembers, WritesOnly = true)]
public class ResourcesController : ControllerBase
{
    private readonly IClubReservations _bookings;
    private readonly ICurrentUserService _user;
    public ResourcesController(IClubReservations bookings, ICurrentUserService user) { _bookings = bookings; _user = user; }

    /// <summary>What can be booked, bookable or not.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClubResourceDto>>> List(CancellationToken ct) =>
        Ok(await _bookings.ResourcesAsync(_user.CurrentOrganizationId, ct));

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] ResourceWriteRequest request, CancellationToken ct) =>
        Bookings.Answer(this, await _bookings.CreateResourceAsync(_user.CurrentOrganizationId, request, ct), r => r.Version);

    /// <summary>The usual spaces of a club, for a workspace that has none yet.</summary>
    [HttpPost("defaults")]
    public async Task<ActionResult<IReadOnlyList<ClubResourceDto>>> AddUsual(CancellationToken ct) =>
        Ok(await _bookings.AddUsualResourcesAsync(_user.CurrentOrganizationId, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] ResourceWriteRequest request, CancellationToken ct)
    {
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();
        return Bookings.Answer(this, await _bookings.UpdateResourceAsync(_user.CurrentOrganizationId, id, request, version, ct), r => r.Version);
    }

    /// <summary>Deletes a resource no booking names; one with bookings is made not bookable instead (409).</summary>
    [HttpDelete("{id:guid}")]
    [Requires(MemberPermissions.DeleteRecords)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct) =>
        (await _bookings.DeleteResourceAsync(_user.CurrentOrganizationId, id, ct)).ToNoContentResult();
}

[ApiController]
[Route("api/reservations")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.FrontDesk, WritesOnly = true)]
public class ReservationsController : ControllerBase
{
    private readonly IClubReservations _bookings;
    private readonly ICurrentUserService _user;
    private readonly IPermissionService _permissions;

    public ReservationsController(IClubReservations bookings, ICurrentUserService user, IPermissionService permissions)
    {
        _bookings = bookings;
        _user = user;
        _permissions = permissions;
    }

    /// <summary>Bookings, soonest first: all, one member's, or those overlapping <paramref name="from"/> to <paramref name="to"/>.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ReservationDto>>> List([FromQuery] Guid? memberId, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        Ok(await _bookings.ReservationsAsync(_user.CurrentOrganizationId,
            new ReservationQuery { MemberId = memberId, FromUtc = from?.ToUniversalTime(), ToUtc = to?.ToUniversalTime() }, ct));

    /// <summary>Books a resource. Recording its deposit as paid, or waiving it, takes the money permission too.</summary>
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] ReservationWriteRequest request, CancellationToken ct)
    {
        var mayHandleMoney = _user.UserId is Guid userId
            && (await _permissions.PermissionsAsync(userId, _user.CurrentOrganizationId, ct)).Contains(MemberPermissions.ManageMoney);
        return Bookings.Answer(this, await _bookings.CreateAsync(_user.CurrentOrganizationId, request, mayHandleMoney, ct), r => r.Version);
    }

    /// <summary>Changes the fields sent, the status with what follows from it (a deposit given back, a no-show fee).</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] ReservationWriteRequest request, CancellationToken ct)
    {
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();
        return Bookings.Answer(this, await _bookings.UpdateAsync(_user.CurrentOrganizationId, id, request, version, ct), r => r.Version);
    }

    /// <summary>Deletes a booking with no money on the ledger; one with a deposit or a fee is cancelled instead (409).</summary>
    [HttpDelete("{id:guid}")]
    [Requires(MemberPermissions.DeleteRecords)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct) =>
        (await _bookings.DeleteAsync(_user.CurrentOrganizationId, id, ct)).ToNoContentResult();
}
