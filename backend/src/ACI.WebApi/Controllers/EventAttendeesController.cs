using System.Text.Json.Nodes;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// An event's attendee list, shared by the whole workspace (see <see cref="IEventAttendeeService"/>).
/// </summary>
[ApiController]
[Route("api/events/{eventId:guid}/attendees")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
public class EventAttendeesController : ControllerBase
{
    private readonly IEventAttendeeService _attendees;
    private readonly ICurrentUserService _user;

    public EventAttendeesController(IEventAttendeeService attendees, ICurrentUserService user)
    {
        _attendees = attendees;
        _user = user;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid eventId, CancellationToken ct)
    {
        if (_user.UserId == null) return Unauthorized();
        var result = await _attendees.ListAsync(_user.CurrentOrganizationId, eventId, ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    [HttpPost]
    public async Task<IActionResult> Add(Guid eventId, CancellationToken ct)
    {
        if (_user.UserId == null) return Unauthorized();
        var result = await _attendees.AddAsync(_user.CurrentOrganizationId, eventId, await BodyAsync(ct), ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>An attendee saved in a browser before the list moved to the server.</summary>
    [HttpPost("import")]
    public async Task<IActionResult> Import(Guid eventId, CancellationToken ct)
    {
        if (_user.UserId == null) return Unauthorized();
        var result = await _attendees.ImportAsync(_user.CurrentOrganizationId, eventId, await BodyAsync(ct), ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    /// <summary>Check in, cancel, or move off the waitlist: <c>{ "status": "CheckedIn" }</c>.</summary>
    [HttpPut("{attendeeId:guid}")]
    public async Task<IActionResult> SetStatus(Guid eventId, Guid attendeeId, CancellationToken ct)
    {
        if (_user.UserId == null) return Unauthorized();
        var result = await _attendees.SetStatusAsync(_user.CurrentOrganizationId, eventId, attendeeId, await BodyAsync(ct), ct);
        return result.IsSuccess ? Ok(result.Value) : result.Error.ToProblemResult();
    }

    [HttpDelete("{attendeeId:guid}")]
    public async Task<IActionResult> Remove(Guid eventId, Guid attendeeId, CancellationToken ct)
    {
        if (_user.UserId == null) return Unauthorized();
        var result = await _attendees.RemoveAsync(_user.CurrentOrganizationId, eventId, attendeeId, ct);
        return result.IsSuccess ? NoContent() : result.Error.ToProblemResult();
    }

    private async Task<JsonObject> BodyAsync(CancellationToken ct)
    {
        if (Request.ContentLength == 0) return new JsonObject();
        try
        {
            return await JsonNode.ParseAsync(Request.Body, cancellationToken: ct) as JsonObject ?? new JsonObject();
        }
        catch (System.Text.Json.JsonException)
        {
            return new JsonObject();
        }
    }
}
