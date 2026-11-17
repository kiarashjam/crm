using ACI.Application.DTOs;
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
/// A newsletter to chosen leads from the Leads page, inviting each to book a meeting on the
/// workspace's website with a link and a code of their own.
/// </summary>
[ApiController]
[Route("api/leads/newsletter")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[InModule(WorkspaceModules.Sales)]
public class LeadNewsletterController : ControllerBase
{
    private readonly ILeadNewsletterService _newsletters;
    private readonly ICurrentUserService _currentUser;

    public LeadNewsletterController(ILeadNewsletterService newsletters, ICurrentUserService currentUser)
    {
        _newsletters = newsletters;
        _currentUser = currentUser;
    }

    /// <summary>Whether each lead can be given a booking link here (and if not, why), who it goes out as, and its fields.</summary>
    [HttpGet("setup")]
    public async Task<ActionResult<NewsletterSetupDto>> Setup(CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        return (await _newsletters.SetupAsync(org, userId, ct)).ToActionResult();
    }

    /// <summary>Who a newsletter with these filters would reach now, and who is left out and why: the counts the send keeps to.</summary>
    [HttpPost("audience")]
    [RequestSizeLimit(64 * 1024)]
    public async Task<ActionResult<NewsletterAudienceDto>> Audience([FromBody] NewsletterAudienceRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is null || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        return (await _newsletters.AudienceAsync(org, request, ct)).ToActionResult();
    }

    /// <summary>The email as one lead will read it, its booking link a test one.</summary>
    [HttpPost("preview")]
    [RequestSizeLimit(512 * 1024)]
    public async Task<ActionResult<NewsletterPreviewDto>> Preview([FromBody] NewsletterEmailRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        return (await _newsletters.PreviewAsync(org, userId, request, ct)).ToActionResult();
    }

    /// <summary>The email as the preview shows it, marked as a test, to the caller's own address and nobody else.</summary>
    [HttpPost("test")]
    [Requires(MemberPermissions.EditRecords)]
    [EnableRateLimiting("composed-email")]
    [RequestSizeLimit(512 * 1024)]
    public async Task<ActionResult<NewsletterTestDto>> Test([FromBody] NewsletterEmailRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        return (await _newsletters.SendTestAsync(org, userId, request, ct)).ToActionResult();
    }

    /// <summary>
    /// Queues one email per lead the filters choose, through the outbox, at a steady rate. The
    /// same sendId sent again (a double click, a retry) sends nothing more and answers with the first send.
    /// </summary>
    [HttpPost("send")]
    [Requires(MemberPermissions.EditRecords)]
    [EnableRateLimiting("composed-email")]
    [RequestSizeLimit(512 * 1024)]
    public async Task<ActionResult<NewsletterSendDto>> Send([FromBody] NewsletterSendRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        return (await _newsletters.SendAsync(org, userId, request, ct)).ToActionResult();
    }
}

/// <summary>
/// Where the workspace's website reports a meeting a lead booked or cancelled: anonymous, and
/// proven by its signature with the workspace's onboarding secret (X-Cadence-Timestamp and
/// X-Cadence-Signature-256, as the CRM signs its own handoffs).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public")]
[Produces("application/json")]
public class PublicMeetingsController : ControllerBase
{
    private readonly IMeetingBookingService _meetings;
    public PublicMeetingsController(IMeetingBookingService meetings) => _meetings = meetings;

    [HttpPost("meetings")]
    [EnableRateLimiting("public-meetings")]
    [RequestSizeLimit(16 * 1024)]
    public async Task<ActionResult<MeetingReportReceipt>> Report(CancellationToken ct)
    {
        // The signature is over the body as sent, so it is read as it came, not as bound.
        string body;
        using (var reader = new StreamReader(Request.Body, System.Text.Encoding.UTF8))
            body = await reader.ReadToEndAsync(ct);
        var result = await _meetings.ReportAsync(
            body,
            Request.Headers[ACI.Application.Common.WebhookSigning.TimestampHeader].ToString(),
            Request.Headers[ACI.Application.Common.WebhookSigning.SignatureHeader].ToString(),
            ct);
        // Who is refused is told nothing more: not whether the workspace exists, nor what was wrong.
        if (result.IsFailure && result.Error.Code == "General.Unauthorized") return Unauthorized();
        return result.ToActionResult();
    }
}
