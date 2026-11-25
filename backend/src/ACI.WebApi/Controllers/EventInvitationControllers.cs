using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Invitations to an event (IEventInvitations): choose an audience of members, write the letter
/// in English, German and French, preview and test it, send it, follow who answered, and remind
/// those who did not. Read by anyone in the club; sent by those who manage members.
/// </summary>
[ApiController]
[Route("api/events/{eventId:guid}/invitations")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.ClubMembers, WritesOnly = true)]
[InModule(WorkspaceModules.Club)]
public class EventInvitationsController : ControllerBase
{
    private readonly IEventInvitations _invitations;
    private readonly ICurrentUserService _user;

    public EventInvitationsController(IEventInvitations invitations, ICurrentUserService user)
    {
        _invitations = invitations;
        _user = user;
    }

    /// <summary>Everyone invited, with where each stands (no answer, coming, waitlist, declined; after the event, came or no-show), and the counts.</summary>
    [HttpGet]
    public async Task<ActionResult<EventInvitationsDto>> List(Guid eventId, CancellationToken ct)
    {
        if (_user.CurrentOrganizationId is not Guid org) return Unauthorized();
        return (await _invitations.ListAsync(org, eventId, ct)).ToActionResult();
    }

    /// <summary>The default letters in each language, the placeholders, the sender, and whether the event can still be invited to.</summary>
    [HttpGet("setup")]
    public async Task<ActionResult<InvitationSetupDto>> Setup(Guid eventId, CancellationToken ct)
    {
        if (_user.UserId is not Guid userId || _user.CurrentOrganizationId is not Guid org) return Unauthorized();
        return (await _invitations.SetupAsync(org, userId, eventId, ct)).ToActionResult();
    }

    /// <summary>Who invitations with this audience would reach now, and who is left out and why: the counts the send keeps to.</summary>
    [HttpPost("audience")]
    [RequestSizeLimit(64 * 1024)]
    public async Task<ActionResult<MemberAudienceDto>> Audience(Guid eventId, [FromBody] MemberAudienceFilter? audience, CancellationToken ct)
    {
        if (_user.UserId is not Guid userId || _user.CurrentOrganizationId is not Guid org) return Unauthorized();
        return (await _invitations.AudienceAsync(org, userId, eventId, audience, ct)).ToActionResult();
    }

    /// <summary>The invitation as one member (or a sample person) will read it, in their language; its reply link a sample.</summary>
    [HttpPost("preview")]
    [RequestSizeLimit(256 * 1024)]
    public async Task<ActionResult<InvitationPreviewDto>> Preview(Guid eventId, [FromBody] InvitationPreviewRequest request, CancellationToken ct)
    {
        if (_user.UserId is not Guid userId || _user.CurrentOrganizationId is not Guid org) return Unauthorized();
        return (await _invitations.PreviewAsync(org, userId, eventId, request ?? new InvitationPreviewRequest(), ct)).ToActionResult();
    }

    /// <summary>The invitation as the preview shows it, marked as a test, to the caller's own address.</summary>
    [HttpPost("test")]
    [EnableRateLimiting("composed-email")]
    [RequestSizeLimit(256 * 1024)]
    public async Task<ActionResult<InvitationTestDto>> Test(Guid eventId, [FromBody] InvitationPreviewRequest request, CancellationToken ct)
    {
        if (_user.UserId is not Guid userId || _user.CurrentOrganizationId is not Guid org) return Unauthorized();
        return (await _invitations.SendTestAsync(org, userId, eventId, request ?? new InvitationPreviewRequest(), ct)).ToActionResult();
    }

    /// <summary>
    /// Queues one invitation per member the audience chooses, each with their own reply link,
    /// through the outbox at a steady rate. The same sendId sent again (a double click, a retry)
    /// sends nothing more and answers with the first send.
    /// </summary>
    [HttpPost("send")]
    [EnableRateLimiting("composed-email")]
    [RequestSizeLimit(256 * 1024)]
    public async Task<ActionResult<InvitationSendDto>> Send(Guid eventId, [FromBody] InvitationSendRequest request, CancellationToken ct)
    {
        if (_user.UserId is not Guid userId || _user.CurrentOrganizationId is not Guid org) return Unauthorized();
        return (await _invitations.SendAsync(org, userId, eventId, request ?? new InvitationSendRequest(), ct)).ToActionResult();
    }

    /// <summary>Reminds those who have not answered: at most one reminder per member and event, however often it is pressed.</summary>
    [HttpPost("remind")]
    [EnableRateLimiting("composed-email")]
    public async Task<ActionResult<InvitationRemindDto>> Remind(Guid eventId, CancellationToken ct)
    {
        if (_user.UserId is not Guid userId || _user.CurrentOrganizationId is not Guid org) return Unauthorized();
        return (await _invitations.RemindAsync(org, userId, eventId, ct)).ToActionResult();
    }

    /// <summary>How many of those with a seat are likely to come, from each member's own past show rate: an estimate.</summary>
    [HttpGet("~/api/events/{eventId:guid}/expected-attendance")]
    public async Task<ActionResult<ExpectedAttendanceDto>> ExpectedAttendance(Guid eventId, CancellationToken ct)
    {
        if (_user.CurrentOrganizationId is not Guid org) return Unauthorized();
        return (await _invitations.ExpectedAttendanceAsync(org, eventId, ct)).ToActionResult();
    }
}

/// <summary>
/// The reply link in a member's invitation to an event, opened by the member, who has no account
/// here: the token in the path is the whole permission. GET shows the club, the event and their
/// answer so far; POST answers ("coming" or "declined"). Rate limited per link.
/// </summary>
/// <remarks>
/// In no module on purpose, so a link already sent is answered with a reason, not a bare refusal;
/// the service refuses what a switched-off Club module would (Module.Off).
/// </remarks>
[ApiController]
[AllowAnonymous]
[Route("api/public/invitations")]
[Produces("application/json")]
[EnableRateLimiting("public-invitation")]
public class PublicInvitationsController : ControllerBase
{
    private readonly IEventInvitations _invitations;
    public PublicInvitationsController(IEventInvitations invitations) => _invitations = invitations;

    [HttpGet("{token}")]
    public async Task<ActionResult<InvitationPageDto>> Page(string token, CancellationToken ct) =>
        (await _invitations.PageAsync(token, ct)).ToActionResult();

    [HttpPost("{token}")]
    [RequestSizeLimit(4 * 1024)]
    public async Task<ActionResult<InvitationPageDto>> Answer(string token, [FromBody] InvitationAnswerRequest request, CancellationToken ct) =>
        (await _invitations.AnswerAsync(token, request?.Answer, ct)).ToActionResult();
}
