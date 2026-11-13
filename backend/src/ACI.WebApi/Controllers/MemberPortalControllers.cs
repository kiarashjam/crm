using System.Text;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;

namespace ACI.WebApi.Controllers;

/// <summary>
/// The member portal for a signed-in member: their own club account. Only a member token
/// reaches these (MemberPortalClaims.Scheme); a staff token is refused, as a member token is
/// everywhere else.
/// </summary>
/// <remarks>
/// In the Club module: with the club off the portal answers that refusal, like the club's other
/// endpoints, and with the club's portal turned off it answers Portal.Off (PortalOpenAttribute).
/// </remarks>
[ApiController]
[Route("api/portal")]
[Authorize(AuthenticationSchemes = MemberPortalClaims.Scheme)]
[InModule(WorkspaceModules.Club)]
[PortalOpen]
[EnableRateLimiting("portal-member")]
[Produces("application/json")]
public class MemberPortalController : ControllerBase
{
    private readonly IMemberPortal _portal;
    private readonly IMemberPortalSignIn _signIn;

    public MemberPortalController(IMemberPortal portal, IMemberPortalSignIn signIn)
    {
        _portal = portal;
        _signIn = signIn;
    }

    /// <summary>The member the session is, which the scheme has checked still stands.</summary>
    private PortalMember Who => MemberPortalClaims.Of(User)!;

    [HttpGet("me")]
    public async Task<ActionResult<PortalMeDto>> Me(CancellationToken ct) => (await _portal.MeAsync(Who, ct)).ToActionResult();

    /// <summary>The phone and language a member may change; a phone sent as null is taken off.</summary>
    [HttpPatch("me")]
    public async Task<ActionResult<PortalMeDto>> UpdateMe([FromBody] PortalProfileRequest request, CancellationToken ct) =>
        (await _portal.UpdateMeAsync(Who, request, ct)).ToActionResult();

    /// <summary>Ends this session.</summary>
    [HttpPost("sign-out")]
    public async Task<IActionResult> EndSession(CancellationToken ct)
    {
        await _signIn.SignOutAsync(Who, ct);
        return NoContent();
    }

    /// <summary>Ends every session of the member, on every device, this one included.</summary>
    [HttpPost("sign-out-everywhere")]
    public async Task<ActionResult<object>> SignOutEverywhere(CancellationToken ct) =>
        Ok(new { ended = await _signIn.SignOutEverywhereAsync(Who, ct) });

    [HttpGet("account")]
    public async Task<ActionResult<PortalAccountDto>> Account(CancellationToken ct) => (await _portal.AccountAsync(Who, ct)).ToActionResult();

    /// <summary>A month's statement, yyyy-MM in the club's time zone.</summary>
    [HttpGet("statements/{month}")]
    public async Task<ActionResult<PortalStatementDto>> Statement(string month, CancellationToken ct) =>
        (await _portal.StatementAsync(Who, month, ct)).ToActionResult();

    [HttpGet("statements/{month}/pdf")]
    public Task<IActionResult> StatementPdf(string month, CancellationToken ct) => Pdf(month, household: false, ct);

    /// <summary>The household's statement, for its primary member.</summary>
    [HttpGet("statements/{month}/household-pdf")]
    public Task<IActionResult> HouseholdStatementPdf(string month, CancellationToken ct) => Pdf(month, household: true, ct);

    private async Task<IActionResult> Pdf(string month, bool household, CancellationToken ct)
    {
        var file = await _portal.StatementPdfAsync(Who, month, household, ct);
        if (!file.IsSuccess) return file.Error.ToProblemResult();
        return File(file.Value.Content, "application/pdf", file.Value.FileName);
    }

    [HttpGet("card")]
    public async Task<ActionResult<PortalCardDto>> Card(CancellationToken ct) => (await _portal.CardAsync(Who, ct)).ToActionResult();

    /// <summary>Where to save a card: the club's pay page for the member.</summary>
    [HttpPost("card/setup")]
    public async Task<ActionResult<PortalRedirectDto>> SetUpCard([FromBody] PortalCardSetupRequest request, CancellationToken ct) =>
        (await _portal.StartCardSetupAsync(Who, request, ct)).ToActionResult();

    [HttpDelete("card")]
    public async Task<ActionResult<PortalCardDto>> RemoveCard(CancellationToken ct) => (await _portal.RemoveCardAsync(Who, ct)).ToActionResult();

    /// <summary>Where to pay what is owed, or the charges named: the club's pay page for the member.</summary>
    [HttpPost("pay")]
    public async Task<ActionResult<PortalRedirectDto>> Pay([FromBody] PortalPayRequest request, CancellationToken ct) =>
        (await _portal.StartPaymentAsync(Who, request, ct)).ToActionResult();

    [HttpGet("events")]
    public async Task<ActionResult<IReadOnlyList<PortalEventDto>>> Events(CancellationToken ct) => (await _portal.EventsAsync(Who, ct)).ToActionResult();

    [HttpPost("events/{eventId:guid}/register")]
    public async Task<ActionResult<PortalEventDto>> Register(Guid eventId, CancellationToken ct) =>
        (await _portal.RegisterAsync(Who, eventId, ct)).ToActionResult();

    [HttpPost("registrations/{registrationId:guid}/cancel")]
    public async Task<ActionResult<PortalEventDto>> CancelRegistration(Guid registrationId, CancellationToken ct) =>
        (await _portal.CancelRegistrationAsync(Who, registrationId, ct)).ToActionResult();

    [HttpGet("resources")]
    public async Task<ActionResult<IReadOnlyList<PortalResourceDto>>> Resources(CancellationToken ct) =>
        (await _portal.ResourcesAsync(Who, ct)).ToActionResult();

    [HttpGet("reservations")]
    public async Task<ActionResult<IReadOnlyList<PortalBookingDto>>> Bookings(CancellationToken ct) =>
        (await _portal.BookingsAsync(Who, ct)).ToActionResult();

    [HttpPost("reservations")]
    public async Task<ActionResult<PortalBookingDto>> Book([FromBody] PortalBookingRequest request, CancellationToken ct)
    {
        var made = await _portal.BookAsync(Who, request, ct);
        return made.IsSuccess ? StatusCode(StatusCodes.Status201Created, made.Value) : made.ToActionResult();
    }

    [HttpPost("reservations/{reservationId:guid}/cancel")]
    public async Task<ActionResult<PortalBookingDto>> CancelBooking(Guid reservationId, CancellationToken ct) =>
        (await _portal.CancelBookingAsync(Who, reservationId, ct)).ToActionResult();

    [HttpGet("consent")]
    public async Task<ActionResult<PortalConsentDto>> Consent(CancellationToken ct) => (await _portal.ConsentAsync(Who, ct)).ToActionResult();

    /// <summary>The member's own answer for one channel and purpose, recorded with source member.</summary>
    [HttpPut("consent")]
    public async Task<ActionResult<PortalConsentDto>> SetConsent([FromBody] PortalConsentRequest request, CancellationToken ct) =>
        (await _portal.SetConsentAsync(Who, request, ct)).ToActionResult();

    [HttpGet("points")]
    public async Task<ActionResult<PortalPointsDto>> Points(CancellationToken ct) => (await _portal.PointsAsync(Who, ct)).ToActionResult();
}

/// <summary>
/// Signing in to a club's member portal: anonymous, by the portal's address (a random key the
/// club puts on its site) and the links emailed to members. Rate limited per caller's address,
/// and asking for links also per club and email address.
/// </summary>
/// <remarks>
/// In no module on purpose, like the other pages people outside the team use; with the club off,
/// the service itself answers the club's refusal (Module.Off).
/// </remarks>
[ApiController]
[AllowAnonymous]
[Route("api/public/portal")]
[Produces("application/json")]
public class PublicMemberPortalController : ControllerBase
{
    private readonly IMemberPortalSignIn _signIn;

    public PublicMemberPortalController(IMemberPortalSignIn signIn) => _signIn = signIn;

    /// <summary>The club behind a portal address, for its sign-in page.</summary>
    [HttpGet("{key}")]
    [EnableRateLimiting("portal-sign-in")]
    public async Task<ActionResult<PortalClubDto>> Club(string key, CancellationToken ct) => (await _signIn.ClubAsync(key, ct)).ToActionResult();

    /// <summary>
    /// Emails a sign-in link to the club's members at this address. The same answer whether or not
    /// the address is a member's (202), so it tells nobody who is a member.
    /// </summary>
    [HttpPost("{key}/sign-in-link")]
    [EnableRateLimiting("portal-link")]
    [RequestSizeLimit(4 * 1024)]
    public async Task<IActionResult> RequestLink(string key, [FromBody] PortalLinkRequest request, CancellationToken ct)
    {
        var asked = await _signIn.RequestLinkAsync(key, request.Email, ct);
        return asked.IsSuccess ? Accepted() : asked.Error.ToProblemResult();
    }

    /// <summary>Uses a sign-in link: a session for the member it names, and its token.</summary>
    [HttpPost("{key}/sign-in")]
    [EnableRateLimiting("portal-sign-in")]
    [RequestSizeLimit(4 * 1024)]
    public async Task<ActionResult<PortalSessionStartedDto>> SignIn(string key, [FromBody] PortalRedeemRequest request, CancellationToken ct) =>
        (await _signIn.RedeemAsync(key, request.Token, ct)).ToActionResult();

    /// <summary>
    /// The club's own portal asks for a one-time sign-in link for the member a welcome hand-over
    /// added, signed with the hand-over's secret as the hand-over itself is (X-Cadence-Timestamp,
    /// X-Cadence-Signature-256 over "{timestamp}.{body}"). The body is any JSON, a random nonce in it.
    /// </summary>
    [HttpPost("handoff/{onboardingId:guid}/link")]
    [EnableRateLimiting("portal-sign-in")]
    [RequestSizeLimit(4 * 1024)]
    public async Task<ActionResult<PortalHandoffLinkDto>> HandoffLink(Guid onboardingId, CancellationToken ct)
    {
        // The signature is over the body exactly as sent, so it is read as text, not bound.
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(ct);
        var result = await _signIn.HandoffLinkAsync(onboardingId,
            Request.Headers[WebhookSigning.TimestampHeader].ToString(),
            Request.Headers[WebhookSigning.SignatureHeader].ToString(),
            body, ct);
        return result.ToActionResult();
    }
}

/// <summary>Staff's side of the member portal: its settings, inviting members, who has used it, ending sessions.</summary>
[ApiController]
[Authorize]
[WorkspaceRequired]
[InModule(WorkspaceModules.Club)]
[Produces("application/json")]
public class MemberPortalAdminController : ControllerBase
{
    private readonly IMemberPortalAdmin _admin;
    private readonly ICurrentUserService _user;

    public MemberPortalAdminController(IMemberPortalAdmin admin, ICurrentUserService user)
    {
        _admin = admin;
        _user = user;
    }

    private Guid Org => _user.CurrentOrganizationId!.Value;

    /// <summary>Whether the portal is on, its address and rules. Every member of the team may read them.</summary>
    [HttpGet("api/member-portal/settings")]
    public async Task<ActionResult<MemberPortalSettingsDto>> Settings(CancellationToken ct)
    {
        var settings = await _admin.GetSettingsAsync(Org, ct);
        Response.SetETag(settings.Version);
        return Ok(settings);
    }

    /// <summary>Turns the portal on or off and sets its rules. Turning it off signs every member out.</summary>
    [HttpPut("api/member-portal/settings")]
    [Requires(MemberPermissions.ManageSettings, What = "change the member portal's settings")]
    public async Task<ActionResult> SaveSettings([FromBody] SaveMemberPortalSettingsRequest request, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();
        var saved = await _admin.SaveSettingsAsync(Org, userId, request, version, ct);
        if (!saved.IsSuccess) return saved.Error.ToProblemResult();
        Response.SetETag(saved.Value.Version);
        return Ok(saved.Value);
    }

    /// <summary>Every member, with when they were invited and last used the portal.</summary>
    [HttpGet("api/member-portal/members")]
    public async Task<ActionResult<IReadOnlyList<MemberPortalUseDto>>> Usage(CancellationToken ct) => Ok(await _admin.UsageAsync(Org, ct));

    /// <summary>Invites every member with an email address not invited yet, a batch at a time.</summary>
    [HttpPost("api/member-portal/invite-all")]
    [Requires(MemberPermissions.ClubMembers, What = "invite members to the member portal")]
    [EnableRateLimiting("invite-email")]
    public async Task<ActionResult<PortalInviteAllDto>> InviteAll(CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        return (await _admin.InviteAllAsync(Org, userId, ct)).ToActionResult();
    }

    [HttpGet("api/members/{memberId:guid}/portal")]
    public async Task<ActionResult<MemberPortalUseDto>> Use(Guid memberId, CancellationToken ct) =>
        (await _admin.UseAsync(Org, memberId, ct)).ToActionResult();

    /// <summary>Emails the member an invitation to the portal, with a sign-in link.</summary>
    [HttpPost("api/members/{memberId:guid}/portal/invite")]
    [Requires(MemberPermissions.ClubMembers, What = "invite members to the member portal")]
    [EnableRateLimiting("invite-email")]
    public async Task<ActionResult<MemberPortalUseDto>> Invite(Guid memberId, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        return (await _admin.InviteAsync(Org, userId, memberId, ct)).ToActionResult();
    }

    /// <summary>Ends every session of the member in the portal; their unused sign-in links stop working.</summary>
    [HttpPost("api/members/{memberId:guid}/portal/revoke")]
    [Requires(MemberPermissions.ClubMembers, What = "sign members out of the member portal")]
    public async Task<ActionResult<MemberPortalUseDto>> Revoke(Guid memberId, CancellationToken ct)
    {
        if (_user.UserId is not { } userId) return Unauthorized();
        return (await _admin.RevokeAsync(Org, userId, memberId, ct)).ToActionResult();
    }
}

/// <summary>
/// The member portal answers only while the club has it turned on: 403 Portal.Off otherwise, the
/// same for every member and whatever they ask. After the module's refusal, before anything else.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public sealed class PortalOpenAttribute : Attribute, IAsyncActionFilter, IOrderedFilter
{
    /// <summary>After the module's refusal (-4000), before the body's check (-2000).</summary>
    public int Order => -3500;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (MemberPortalClaims.Of(context.HttpContext.User) is { } member
            && !await context.HttpContext.RequestServices.GetRequiredService<IMemberPortalSignIn>()
                .IsOpenAsync(member.OrganizationId, context.HttpContext.RequestAborted))
        {
            context.Result = DomainErrors.Portal.Off.ToProblemResult();
            return;
        }
        await next();
    }
}
