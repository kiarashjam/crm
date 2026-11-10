using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

// Club events and their guest lists, on the server (IClubEvents). The routes and shapes are the
// ones the events pages have always used; what changed is that the server keeps the list, the
// capacity and the waitlist, posts tickets to house accounts, and runs the door. club.members
// manages events; club.members or club.frontdesk keeps the list and the door.

/// <summary>Shared answering for the events routes.</summary>
file static class Events
{
    /// <summary>The record, with its version as an ETag when it has one, or the refusal as a sentence (400, 403, 404 or 409).</summary>
    public static ActionResult Answer<T>(ControllerBase controller, Result<T> result, Func<T, Guid?>? version = null)
    {
        if (!result.IsSuccess) return result.Error.ToProblemResult();
        if (version != null) controller.Response.SetETag(version(result.Value));
        return controller.Ok(result.Value);
    }
}

[ApiController]
[Route("api/events")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
public class EventsController : ControllerBase
{
    private readonly IClubEvents _events;
    private readonly ICurrentUserService _user;
    public EventsController(IClubEvents events, ICurrentUserService user) { _events = events; _user = user; }

    /// <summary>The workspace's events, soonest first, each with its seats taken, its waitlist and its door.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClubEventDto>>> List(CancellationToken ct) =>
        Ok(await _events.ListAsync(_user.CurrentOrganizationId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken ct) =>
        Events.Answer(this, await _events.GetAsync(_user.CurrentOrganizationId, id, ct), e => e.Version);

    /// <summary>A member's registrations across events: on their own ticket, or bringing a guest.</summary>
    [HttpGet("registrations")]
    public async Task<ActionResult<IReadOnlyList<EventRegistrationDto>>> MemberRegistrations([FromQuery] Guid memberId, CancellationToken ct) =>
        Ok(await _events.MemberRegistrationsAsync(_user.CurrentOrganizationId, memberId, ct));

    [HttpPost]
    [Requires(MemberPermissions.ClubMembers)]
    public async Task<ActionResult> Create([FromBody] ClubEventWriteRequest request, CancellationToken ct) =>
        Events.Answer(this, await _events.CreateAsync(_user.CurrentOrganizationId, request, ct), e => e.Version);

    /// <summary>Changes the fields sent. Cancelling cancels everyone on the list and takes their tickets back.</summary>
    [HttpPut("{id:guid}")]
    [Requires(MemberPermissions.ClubMembers)]
    public async Task<ActionResult> Update(Guid id, [FromBody] ClubEventWriteRequest request, CancellationToken ct)
    {
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();
        return Events.Answer(this, await _events.UpdateAsync(_user.CurrentOrganizationId, id, request, version, ct), e => e.Version);
    }

    /// <summary>Deletes an event and its list; one with tickets on house accounts is cancelled instead (409).</summary>
    [HttpDelete("{id:guid}")]
    [Requires(MemberPermissions.ClubMembers)]
    [Requires(MemberPermissions.DeleteRecords)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct) =>
        (await _events.DeleteAsync(_user.CurrentOrganizationId, id, ct)).ToNoContentResult();

    // An RSVP from the events list added a seat with no name on it: nobody to check in at the
    // door and nobody to charge. Answered with a sentence, not a bare 404 the app would read as
    // a missing feature; the list's button now adds a person to the event.
    [HttpPost("{id:guid}/rsvp")]
    [Requires(MemberPermissions.ClubMembers, MemberPermissions.FrontDesk, Any = true)]
    public ActionResult Rsvp(Guid id) => DomainErrors.ClubEvent.RsvpNeedsName.ToProblemResult();
}

/// <summary>An event's list and its door, shared by the whole workspace.</summary>
[ApiController]
[Route("api/events/{eventId:guid}/attendees")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.ClubMembers, MemberPermissions.FrontDesk, Any = true, WritesOnly = true)]
public class EventAttendeesController : ControllerBase
{
    private readonly IClubEvents _events;
    private readonly ICurrentUserService _user;
    public EventAttendeesController(IClubEvents events, ICurrentUserService user) { _events = events; _user = user; }

    /// <summary>Everyone on the list, in the order they joined it.</summary>
    [HttpGet]
    public async Task<ActionResult> List(Guid eventId, CancellationToken ct)
    {
        var result = await _events.RegistrationsAsync(_user.CurrentOrganizationId, eventId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>Adds a member, or a guest by name: a seat while there is one, else the waitlist. A seat with a price posts its ticket.</summary>
    [HttpPost]
    public async Task<ActionResult> Add(Guid eventId, [FromBody] RegisterForEventRequest request, CancellationToken ct) =>
        Events.Answer(this, await _events.RegisterAsync(_user.CurrentOrganizationId, eventId, request, ct));

    /// <summary>Someone at the door with no registration, checked in at once on a free seat.</summary>
    [HttpPost("walk-in")]
    public async Task<ActionResult> WalkIn(Guid eventId, [FromBody] RegisterForEventRequest request, CancellationToken ct) =>
        Events.Answer(this, await _events.WalkInAsync(_user.CurrentOrganizationId, eventId, request, ct));

    /// <summary>An attendee saved in a browser before the list moved to the server.</summary>
    [HttpPost("import")]
    public async Task<ActionResult> Import(Guid eventId, [FromBody] ImportRegistrationRequest request, CancellationToken ct) =>
        Events.Answer(this, await _events.ImportAsync(_user.CurrentOrganizationId, eventId, request, ct));

    /// <summary>Check in, cancel, take off the waitlist or mark a no-show: <c>{ "status": "CheckedIn" }</c>.</summary>
    [HttpPut("{attendeeId:guid}")]
    public async Task<ActionResult> SetStatus(Guid eventId, Guid attendeeId, [FromBody] RegistrationStatusRequest request, CancellationToken ct) =>
        Events.Answer(this, await _events.SetStatusAsync(_user.CurrentOrganizationId, eventId, attendeeId, request, ct));

    [HttpDelete("{attendeeId:guid}")]
    public async Task<ActionResult> Remove(Guid eventId, Guid attendeeId, CancellationToken ct) =>
        (await _events.RemoveAsync(_user.CurrentOrganizationId, eventId, attendeeId, ct)).ToNoContentResult();
}
