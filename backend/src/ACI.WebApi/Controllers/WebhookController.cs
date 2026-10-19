using System.Security.Cryptography;
using System.Text;
using ACI.Application;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Handles webhook endpoints for external integrations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class WebhookController : ControllerBase
{
    private readonly ILeadService _leadService;
    private readonly ICompanyService _companyService;
    private readonly IOrganizationService _organizationService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILeadOpsService _leadOps;

    /// <summary>
    /// Initializes a new instance of the WebhookController.
    /// </summary>
    public WebhookController(
        ILeadService leadService,
        ICompanyService companyService,
        IOrganizationService organizationService,
        ICurrentUserService currentUser,
        ILeadOpsService leadOps)
    {
        _leadService = leadService;
        _companyService = companyService;
        _organizationService = organizationService;
        _currentUser = currentUser;
        _leadOps = leadOps;
    }

    /// <summary>
    /// Creates a lead via webhook using API key authentication.
    /// </summary>
    /// <param name="request">The lead creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created lead.</returns>
    /// <remarks>
    /// Requires X-Api-Key header with a valid organization API key.
    /// </remarks>
    /// <response code="201">Lead created successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">Missing or invalid API key.</response>
    /// <response code="500">Failed to create lead.</response>
    [HttpPost("leads")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LeadDto>> CreateLeadViaWebhook(
        [FromBody] WebhookLeadRequest request,
        CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader) || string.IsNullOrEmpty(apiKeyHeader))
            return Unauthorized(new { error = "Missing or invalid X-Api-Key header" });

        var apiKey = apiKeyHeader.ToString().Trim();
        if (string.IsNullOrEmpty(apiKey))
            return Unauthorized(new { error = "Invalid X-Api-Key header" });

        var organization = await _organizationService.GetByApiKeyAsync(apiKey, ct);
        if (organization == null)
            return Unauthorized(new { error = "Invalid API key" });

        return await CreateLeadForOrganizationAsync(organization, request, ct);
    }

    /// <summary>
    /// Creates a lead via JSON webhook using organization id and shared password (header or body field).
    /// </summary>
    /// <remarks>
    /// Send <c>X-Webhook-Password</c> or include <c>webhookPassword</c> in the JSON body.
    /// If no custom password is stored for the organization, the default password applies.
    /// </remarks>
    [HttpPost("organizations/{organizationId:guid}/leads")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LeadDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<LeadDto>> CreateLeadViaWebhookWithPassword(
        Guid organizationId,
        [FromBody] WebhookLeadRequest request,
        CancellationToken ct)
    {
        var organization = await _organizationService.GetByIdUnauthenticatedAsync(organizationId, ct);
        if (organization == null)
            return NotFound(new { error = "Organization not found" });

        var headerPassword = Request.Headers.TryGetValue("X-Webhook-Password", out var h) ? h.ToString().Trim() : null;
        var provided = !string.IsNullOrEmpty(headerPassword)
            ? headerPassword
            : request.WebhookPassword?.Trim();

        if (string.IsNullOrEmpty(provided))
            return Unauthorized(new { error = "Missing X-Webhook-Password header or webhookPassword in JSON body" });

        if (string.IsNullOrEmpty(organization.WebhookPassword))
            return Unauthorized(new { error = "This workspace has no webhook password. Set one in Settings before using this address." });

        var expected = organization.WebhookPassword;

        // Constant-time comparison to avoid leaking password length / prefix via timing.
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        if (expectedBytes.Length != providedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
            return Unauthorized(new { error = "Invalid webhook password" });

        return await CreateLeadForOrganizationAsync(organization, request, ct);
    }

    private async Task<ActionResult<LeadDto>> CreateLeadForOrganizationAsync(
        Organization organization,
        WebhookLeadRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "Name and Email are required" });

        if (!request.Email.Contains('@') || request.Email.Length < 3)
            return BadRequest(new { error = "Invalid email format" });

        // A returning prospect. Since #57 the lead service refuses a second open lead
        // with the same email, and this endpoint turned that refusal into a 500 —
        // so senders like Zapier retried, the inquiry was lost, and the company
        // created just below was orphaned on every attempt. The inquiry now goes on
        // the lead they already have, before anything else is written.
        var source = string.IsNullOrWhiteSpace(request.Source) ? "webhook" : request.Source.Trim();
        if (source.Length > 64) source = source[..64];
        var summary = string.Join("\n", new[]
        {
            $"Name: {request.Name.Trim()}",
            $"Email: {request.Email.Trim()}",
            string.IsNullOrWhiteSpace(request.Phone) ? null : $"Phone: {request.Phone.Trim()}",
            string.IsNullOrWhiteSpace(request.CompanyName) ? null : $"Company: {request.CompanyName.Trim()}",
        }.Where(line => line != null));
        var existingId = await _leadOps.AttachInquiryAsync(organization.Id, organization.OwnerUserId, request.Email.Trim(), source, summary, ct);
        if (existingId != null)
            return Ok(new { id = existingId, duplicate = true, message = "An open lead with this email already exists; the inquiry was added to it." });

        Guid? companyId = null;
        if (!string.IsNullOrWhiteSpace(request.CompanyName))
        {
            var companyRequest = new CreateCompanyRequest { Name = request.CompanyName.Trim() };
            var companyResult = await _companyService.CreateAsync(organization.OwnerUserId, organization.Id, companyRequest, ct);
            if (companyResult.IsSuccess)
                companyId = companyResult.Value.Id;
        }

        var leadRequest = new CreateLeadRequest
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone?.Trim(),
            CompanyId = companyId,
            Source = source,
            Status = "New"
        };

        var leadResult = await _leadService.CreateAsync(organization.OwnerUserId, organization.Id, leadRequest, ct);

        if (leadResult.IsFailure)
            return leadResult.Error.ToProblemResult();

        return CreatedAtAction(nameof(CreateLeadViaWebhook), new { id = leadResult.Value.Id }, leadResult.Value);
    }

    /// <summary>
    /// Gets webhook information (URL and API key status) for an organization.
    /// </summary>
    [HttpGet("organizations/{organizationId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(WebhookInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WebhookInfoDto>> GetWebhookInfo(
        Guid organizationId,
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        try
        {
            var result = await _organizationService.GetWebhookInfoAsync(organizationId, userId.Value, ct);

            if (result.IsFailure)
                return result.ToActionResult();

            var scheme = Request.Scheme;
            var host = Request.Host.Value;
            var infoWithUrl = result.Value with
            {
                WebhookUrl = $"{scheme}://{host}/api/webhook/leads",
                PasswordWebhookUrl = $"{scheme}://{host}/api/webhook/organizations/{organizationId}/leads"
            };

            return Ok(infoWithUrl);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Generates or regenerates a webhook API key for an organization.
    /// </summary>
    [HttpPost("organizations/{organizationId:guid}/generate-key")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<object>> GenerateApiKey(
        Guid organizationId,
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        var result = await _organizationService.GenerateWebhookApiKeyAsync(organizationId, userId.Value, ct);
        if (result.IsFailure)
            return result.ToActionResult();

        return Ok(new { apiKey = result.Value });
    }

    /// <summary>
    /// Sets or clears the shared JSON webhook password (owner/manager only). Empty body clears to the default.
    /// </summary>
    [HttpPut("organizations/{organizationId:guid}/webhook-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateWebhookPassword(
        Guid organizationId,
        [FromBody] UpdateWebhookPasswordRequest? body,
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        var result = await _organizationService.UpdateWebhookPasswordAsync(
            organizationId,
            userId.Value,
            body?.Password,
            ct);

        return result.ToNoContentResult();
    }

    /// <summary>Where Cadence posts lead.created, deal.stage_changed, and deal.won. An empty URL turns it off.</summary>
    [HttpPut("organizations/{organizationId:guid}/outbound")]
    [Authorize]
    public async Task<ActionResult> UpdateOutbound(
        Guid organizationId,
        [FromBody] OutboundWebhookRequest? body,
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var result = await _organizationService.UpdateOutboundWebhookAsync(
            organizationId, userId.Value, body?.Url, body?.Secret, ct);
        return result.ToNoContentResult();
    }
}

public record OutboundWebhookRequest(string? Url, string? Secret);
