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
/// Welcome &amp; portal: where new customers are handed over when their lead becomes a
/// deal, the welcome email that goes with them, and each handoff's progress.
/// </summary>
[ApiController]
[Route("api/onboarding")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
public class OnboardingController : ControllerBase
{
    private readonly IOnboardingService _onboarding;
    private readonly ICurrentUserService _currentUser;

    public OnboardingController(IOnboardingService onboarding, ICurrentUserService currentUser)
    {
        _onboarding = onboarding;
        _currentUser = currentUser;
    }

    /// <summary>The workspace's settings. Everyone may read them (the secret is never shown); changing them needs Manage settings.</summary>
    [HttpGet("settings")]
    public async Task<ActionResult<OnboardingSettingsDto>> Settings(CancellationToken ct)
    {
        if (_currentUser.UserId is null || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        return (await _onboarding.GetSettingsAsync(org, ct)).ToActionResult();
    }

    [HttpPut("settings")]
    [Requires(MemberPermissions.ManageSettings)]
    [RequestSizeLimit(512 * 1024)]
    public async Task<ActionResult<OnboardingSettingsDto>> SaveSettings([FromBody] SaveOnboardingSettingsRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        return (await _onboarding.SaveSettingsAsync(org, userId, request, ct)).ToActionResult();
    }

    /// <summary>A new signing secret, shown this once. Handoffs already made report with tokens from the old one, which stop working.</summary>
    [HttpPost("settings/secret")]
    [Requires(MemberPermissions.ManageSettings)]
    public async Task<ActionResult<OnboardingSecretDto>> NewSecret(CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        return (await _onboarding.NewSecretAsync(org, userId, ct)).ToActionResult();
    }

    /// <summary>Posts a signed <c>onboarding.ping</c> to the portal now, and says what it answered.</summary>
    [HttpPost("settings/test")]
    [Requires(MemberPermissions.ManageSettings)]
    [EnableRateLimiting("composed-email")]
    public async Task<ActionResult<OnboardingPingDto>> Test(CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        return (await _onboarding.PingAsync(org, userId, ct)).ToActionResult();
    }

    /// <summary>The welcome email for someone, filled as it will be sent; the portal's own links are stand-ins.</summary>
    [HttpPost("preview")]
    [RequestSizeLimit(512 * 1024)]
    public async Task<ActionResult<OnboardingPreviewDto>> Preview([FromBody] OnboardingPreviewRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        return (await _onboarding.PreviewAsync(org, userId, request, ct)).ToActionResult();
    }

    [HttpGet("/api/deals/{dealId:guid}/onboarding")]
    public async Task<ActionResult<IReadOnlyList<OnboardingDto>>> ForDeal(Guid dealId, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        return (await _onboarding.ForDealAsync(dealId, org, userId, ct)).ToActionResult();
    }

    /// <summary>Hands the deal's contact over to the portal: it opens their account and sends the welcome email.</summary>
    [HttpPost("/api/deals/{dealId:guid}/onboarding")]
    [Requires(MemberPermissions.EditRecords)]
    [RequestSizeLimit(512 * 1024)]
    public async Task<ActionResult<OnboardingDto>> Start(Guid dealId, [FromBody] StartOnboardingRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        var result = await _onboarding.StartAsync(dealId, org, userId, request, ct);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : result.ToActionResult();
    }

    /// <summary>Hands it over again: after a failure, or to send the welcome email once more.</summary>
    [HttpPost("{id:guid}/resend")]
    [Requires(MemberPermissions.EditRecords)]
    public async Task<ActionResult<OnboardingDto>> Resend(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId || _currentUser.CurrentOrganizationId is not { } org) return Unauthorized();
        return (await _onboarding.ResendAsync(id, org, userId, ct)).ToActionResult();
    }
}

/// <summary>
/// Where a portal reports on a handoff: anonymous, and proven by the handoff's callback
/// token (<c>Authorization: Bearer …</c>), which only the signed handoff carried.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public")]
[Produces("application/json")]
public class PublicOnboardingController : ControllerBase
{
    private readonly IOnboardingService _onboarding;
    public PublicOnboardingController(IOnboardingService onboarding) => _onboarding = onboarding;

    [HttpPost("onboarding/{id:guid}/events")]
    [EnableRateLimiting("public-onboarding")]
    [RequestSizeLimit(16 * 1024)]
    public async Task<ActionResult<OnboardingReportReceipt>> Report(Guid id, [FromBody] OnboardingReportRequest request, CancellationToken ct)
    {
        var header = Request.Headers.Authorization.ToString();
        var token = header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? header["Bearer ".Length..].Trim() : null;
        return (await _onboarding.ReportAsync(id, token, request, ct)).ToActionResult();
    }
}
