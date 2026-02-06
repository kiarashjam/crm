using ACI.Application.DTOs;
using ACI.Application.Interfaces;
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

    /// <summary>
    /// Initializes a new instance of the WebhookController.
    /// </summary>
    public WebhookController(
        ILeadService leadService,
        ICompanyService companyService,
        IOrganizationService organizationService,
        ICurrentUserService currentUser)
    {
        _leadService = leadService;
        _companyService = companyService;
        _organizationService = organizationService;
        _currentUser = currentUser;
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
        // Validate API key from header
        if (!Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader) || string.IsNullOrEmpty(apiKeyHeader))
        {
            return Unauthorized(new { error = "Missing or invalid X-Api-Key header" });
        }

        var apiKey = apiKeyHeader.ToString().Trim();
        if (string.IsNullOrEmpty(apiKey))
        {
            return Unauthorized(new { error = "Invalid X-Api-Key header" });
        }

        var organization = await _organizationService.GetByApiKeyAsync(apiKey, ct);
        if (organization == null)
        {
            return Unauthorized(new { error = "Invalid API key" });
        }

        // Validate required fields
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { error = "Name and Email are required" });
        }

        // Basic email validation
        if (!request.Email.Contains('@') || request.Email.Length < 3)
        {
            return BadRequest(new { error = "Invalid email format" });
        }

        // Handle company creation if CompanyName is provided
        Guid? companyId = null;
        if (!string.IsNullOrWhiteSpace(request.CompanyName))
        {
            var companyRequest = new CreateCompanyRequest { Name = request.CompanyName.Trim() };
            var companyResult = await _companyService.CreateAsync(organization.OwnerUserId, organization.Id, companyRequest, ct);
            if (companyResult.IsSuccess)
            {
                companyId = companyResult.Value.Id;
            }
        }

        // Create lead with source "webhook" (or provided source)
        var leadRequest = new CreateLeadRequest
        {
            Name = request.Name.Trim(),
            Email = request.Email.Trim(),
            Phone = request.Phone?.Trim(),
            CompanyId = companyId,
            Source = request.Source ?? "webhook",
            Status = "New"
        };

        var leadResult = await _leadService.CreateAsync(organization.OwnerUserId, organization.Id, leadRequest, ct);
        
        if (leadResult.IsFailure)
        {
            return StatusCode(500, new { error = "Failed to create lead", detail = leadResult.Error.Description });
        }

        return CreatedAtAction(nameof(CreateLeadViaWebhook), new { id = leadResult.Value.Id }, leadResult.Value);
    }

    /// <summary>
    /// Gets webhook information (URL and API key status) for an organization.
    /// </summary>
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Webhook information including URL and API key status.</returns>
    /// <response code="200">Returns the webhook information.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not authorized to view this organization's webhook info.</response>
    /// <response code="404">Organization not found.</response>
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
            {
                return result.ToActionResult();
            }
            
            var info = result.Value;
            
            // Build full webhook URL from current request
            var scheme = Request.Scheme;
            var host = Request.Host.Value;
            var webhookUrl = $"{scheme}://{host}/api/webhook/leads";
            
            // Return with full URL
            var infoWithUrl = new WebhookInfoDto(
                WebhookUrl: webhookUrl,
                ApiKey: info.ApiKey,
                ApiKeyCreatedAt: info.ApiKeyCreatedAt,
                HasApiKey: info.HasApiKey
            );
            
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
    /// <param name="organizationId">The organization ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The generated API key.</returns>
    /// <remarks>
    /// Only organization owners or managers can generate API keys.
    /// </remarks>
    /// <response code="200">API key generated successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="403">User is not authorized to generate API key for this organization.</response>
    /// <response code="404">Organization not found.</response>
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

        try
        {
            var apiKey = await _organizationService.GenerateWebhookApiKeyAsync(organizationId, userId.Value, ct);
            return Ok(new { apiKey });
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
}
