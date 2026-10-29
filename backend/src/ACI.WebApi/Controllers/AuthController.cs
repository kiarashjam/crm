using System.ComponentModel.DataAnnotations;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Handles user authentication, registration, and two-factor authentication.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUser;

    public AuthController(IAuthService authService, ICurrentUserService currentUser)
    {
        _authService = authService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Authenticates a user with email and password.
    /// </summary>
    /// <remarks>
    /// If two-factor authentication is enabled, the response will include
    /// a TwoFactorToken that must be used with the /login/2fa endpoint.
    /// </remarks>
    /// <param name="request">Login credentials.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Login response with JWT token or 2FA requirement.</returns>
    /// <response code="200">Login successful or 2FA required.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Completes login with two-factor authentication code.
    /// </summary>
    /// <remarks>
    /// Use the TwoFactorToken received from the initial login attempt
    /// along with the TOTP code from the authenticator app.
    /// </remarks>
    /// <param name="request">Two-factor login request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Login response with JWT token.</returns>
    /// <response code="200">Two-factor authentication successful.</response>
    /// <response code="401">Invalid token or code.</response>
    [HttpPost("login/2fa")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> LoginWithTwoFactor([FromBody] TwoFactorLoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginWithTwoFactorAsync(request, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Registers a new user account.
    /// </summary>
    /// <param name="request">Registration request with email, password, and name.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Login response with JWT token for the new account.</returns>
    /// <response code="200">Registration successful.</response>
    /// <response code="409">Email is already registered.</response>
    /// <response code="429">Too many sign-ups from this address; try again in a few minutes.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("register")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterAsync(
            new LoginRequest { Email = request.Email, Password = request.Password },
            request.Name,
            ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Gets information about the currently authenticated user.
    /// </summary>
    /// <remarks>
    /// Read from the database, not the token, so a confirmation made since sign-in shows.
    /// </remarks>
    /// <returns>Current user information.</returns>
    /// <response code="200">Returns the user information.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserInfoDto>> Me(CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            return Unauthorized();

        var result = await _authService.GetCurrentUserAsync(_currentUser.UserId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Confirms the account's email address using the token from the confirmation link.
    /// </summary>
    /// <remarks>
    /// Anonymous: the link is often opened on a phone, where the person is not signed in.
    /// The token is the proof, and it works once.
    /// </remarks>
    /// <response code="200">Address confirmed.</response>
    /// <response code="400">The link is invalid, used, or expired.</response>
    [HttpPost("verify-email")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request, CancellationToken ct)
    {
        var result = await _authService.VerifyEmailAsync(request, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Sends the signed-in user a new confirmation link.
    /// </summary>
    /// <response code="200">Sent, or the address was already confirmed.</response>
    /// <response code="429">Several were sent recently; wait and try again.</response>
    [HttpPost("verify-email/resend")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ResendVerificationEmail(CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            return Unauthorized();

        var result = await _authService.ResendVerificationEmailAsync(_currentUser.UserId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Says whether two-factor authentication is on for the current user.
    /// </summary>
    /// <remarks>
    /// What the Settings page asks when it opens. It never carries the secret.
    /// </remarks>
    /// <response code="200">Returns whether 2FA is enabled.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("2fa/status")]
    [Authorize]
    [ProducesResponseType(typeof(TwoFactorStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TwoFactorStatusResponse>> TwoFactorStatus(CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            return Unauthorized();

        var result = await _authService.GetTwoFactorStatusAsync(_currentUser.UserId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Gets two-factor authentication setup information.
    /// </summary>
    /// <remarks>
    /// Returns the TOTP secret and QR code URI for setting up an authenticator app.
    /// The secret is generated on first call and persisted until 2FA is enabled. Once it
    /// is enabled, the secret is not returned again (Secret and OtpauthUri are null).
    /// </remarks>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Two-factor setup information with QR code URI.</returns>
    /// <response code="200">Returns 2FA setup information.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">User not found.</response>
    [HttpPost("2fa/setup")]
    [Authorize]
    [ProducesResponseType(typeof(TwoFactorSetupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TwoFactorSetupResponse>> TwoFactorSetup(CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            return Unauthorized();
        
        var result = await _authService.GetTwoFactorSetupAsync(_currentUser.UserId.Value, ct);
        return result.ToActionResult();
    }

    /// <summary>
    /// Enables two-factor authentication for the current user.
    /// </summary>
    /// <remarks>
    /// Requires a valid TOTP code from the authenticator app to verify setup.
    /// Call /2fa/setup first to get the secret and QR code.
    /// </remarks>
    /// <param name="request">Request containing the verification code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="200">Two-factor authentication enabled successfully.</response>
    /// <response code="400">Invalid verification code.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost("2fa/enable")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EnableTwoFactor([FromBody] TwoFactorEnableRequest request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            return Unauthorized();
        
        var result = await _authService.EnableTwoFactorAsync(_currentUser.UserId.Value, request, ct);
        
        if (result.IsSuccess)
            return Ok();
        
        return result.ToNoContentResult();
    }

    /// <summary>
    /// Disables two-factor authentication for the current user.
    /// </summary>
    /// <remarks>
    /// Requires both the password and a valid TOTP code for security.
    /// </remarks>
    /// <param name="request">Request containing password and verification code.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="200">Two-factor authentication disabled successfully.</response>
    /// <response code="400">Invalid password or verification code.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost("2fa/disable")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> DisableTwoFactor([FromBody] TwoFactorDisableRequest request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            return Unauthorized();
        
        var result = await _authService.DisableTwoFactorAsync(_currentUser.UserId.Value, request, ct);
        
        if (result.IsSuccess)
            return Ok();
        
        return result.ToNoContentResult();
    }

    /// <summary>
    /// Sends a password reset email if an account exists for the given address.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        var result = await _authService.RequestPasswordResetAsync(request, ct);
        if (!result.IsSuccess)
            return result.ToActionResult();

        return Ok(new
        {
            message = "If an account exists for that email, you will receive password reset instructions shortly.",
        });
    }

    /// <summary>
    /// Completes password reset using the token from the email link.
    /// </summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var result = await _authService.ResetPasswordAsync(request, ct);
        return result.ToActionResult();
    }
}

/// <summary>
/// Request model for user registration.
/// </summary>
public record RegisterRequest
{
    /// <summary>
    /// User's email address.
    /// </summary>
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(254, ErrorMessage = "Email cannot exceed 254 characters")]
    public required string Email { get; init; }

    /// <summary>
    /// User's password.
    /// </summary>
    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters")]
    public required string Password { get; init; }

    /// <summary>
    /// User's display name.
    /// </summary>
    [Required(ErrorMessage = "Name is required")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Name must be between 1 and 200 characters")]
    public required string Name { get; init; }
}
