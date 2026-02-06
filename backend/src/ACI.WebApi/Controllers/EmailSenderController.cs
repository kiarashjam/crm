using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Manages email sending and SMTP configuration.
/// </summary>
/// <remarks>
/// Provides endpoints for sending emails via configured SMTP servers,
/// managing SMTP settings, and testing email connectivity.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class EmailSenderController : ControllerBase
{
    private readonly IEmailSenderService _emailService;
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    /// Initializes a new instance of the EmailSenderController.
    /// </summary>
    public EmailSenderController(IEmailSenderService emailService, ICurrentUserService currentUser)
    {
        _emailService = emailService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Sends an email using configured SMTP settings.
    /// </summary>
    /// <remarks>
    /// Requires SMTP settings to be configured for the user or organization.
    /// Supports plain text and HTML email content.
    /// </remarks>
    /// <param name="request">The email send request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result of the email send operation.</returns>
    /// <response code="200">Email sent successfully.</response>
    /// <response code="400">Failed to send email (check result for details).</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost("send")]
    [ProducesResponseType(typeof(SendEmailResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SendEmailResult), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SendEmailResult>> SendEmail([FromBody] SendEmailRequest request, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        var result = await _emailService.SendEmailAsync(request, userId.Value, _currentUser.OrganizationId, ct);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }
        
        return Ok(result);
    }

    /// <summary>
    /// Gets current SMTP settings.
    /// </summary>
    /// <remarks>
    /// The password is masked in the response for security.
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Current SMTP settings if configured.</returns>
    /// <response code="200">Returns the SMTP settings.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">SMTP settings not configured.</response>
    [HttpGet("settings")]
    [ProducesResponseType(typeof(SmtpSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SmtpSettings>> GetSettings(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        var settings = await _emailService.GetSmtpSettingsAsync(userId.Value, _currentUser.OrganizationId, ct);
        
        if (settings == null)
        {
            return NotFound(new { message = "SMTP settings not configured" });
        }
        
        return Ok(settings);
    }

    /// <summary>
    /// Saves SMTP settings.
    /// </summary>
    /// <remarks>
    /// Automatically tests the connection before saving.
    /// The IsValid and LastTestedAt fields are set based on the test result.
    /// The password is masked in the response.
    /// </remarks>
    /// <param name="settings">The SMTP settings to save.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The saved settings with test results.</returns>
    /// <response code="200">Settings saved successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost("settings")]
    [ProducesResponseType(typeof(SmtpSettings), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SmtpSettings>> SaveSettings([FromBody] SmtpSettings settings, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        // Test the connection first
        var isValid = await _emailService.TestSmtpConnectionAsync(settings, ct);
        settings.IsValid = isValid;
        settings.LastTestedAt = DateTime.UtcNow;

        await _emailService.SaveSmtpSettingsAsync(settings, userId.Value, _currentUser.OrganizationId, ct);

        // Return settings with masked password
        return Ok(new SmtpSettings
        {
            Id = settings.Id,
            Host = settings.Host,
            Port = settings.Port,
            Username = settings.Username,
            Password = "********",
            UseSsl = settings.UseSsl,
            FromEmail = settings.FromEmail,
            FromName = settings.FromName,
            LastTestedAt = settings.LastTestedAt,
            IsValid = settings.IsValid,
        });
    }

    /// <summary>
    /// Tests SMTP connection without saving.
    /// </summary>
    /// <remarks>
    /// Use this to validate settings before saving.
    /// Returns connection latency for monitoring purposes.
    /// </remarks>
    /// <param name="request">SMTP settings to test.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Test result including success status and latency.</returns>
    /// <response code="200">Test completed (check Success field for result).</response>
    [HttpPost("test")]
    [ProducesResponseType(typeof(TestSmtpResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<TestSmtpResult>> TestConnection([FromBody] TestSmtpRequest request, CancellationToken ct)
    {
        var settings = new SmtpSettings
        {
            Host = request.Host,
            Port = request.Port,
            Username = request.Username,
            Password = request.Password,
            UseSsl = request.UseSsl,
            FromEmail = request.FromEmail,
        };

        var sw = Stopwatch.StartNew();
        var isValid = await _emailService.TestSmtpConnectionAsync(settings, ct);
        sw.Stop();

        return Ok(new TestSmtpResult
        {
            Success = isValid,
            Error = isValid ? null : "Failed to connect to SMTP server. Please check your settings.",
            LatencyMs = (int)sw.ElapsedMilliseconds,
        });
    }
}
