using System.ComponentModel.DataAnnotations;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    [HttpPost("register")]
    [AllowAnonymous]
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
    /// <returns>Current user information.</returns>
    /// <response code="200">Returns the user information.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<UserInfoDto> Me()
    {
        if (!_currentUser.UserId.HasValue)
            return Unauthorized();
        
        var id = _currentUser.UserId.Value;
        var name = User.Identity?.Name ?? string.Empty;
        var email = User.Claims.FirstOrDefault(c => c.Type.EndsWith("/emailaddress", StringComparison.OrdinalIgnoreCase))?.Value
            ?? User.Claims.FirstOrDefault(c => c.Type.Contains("email", StringComparison.OrdinalIgnoreCase))?.Value
            ?? string.Empty;
        
        return Ok(new UserInfoDto(id, name, email));
    }

    /// <summary>
    /// Gets two-factor authentication setup information.
    /// </summary>
    /// <remarks>
    /// Returns the TOTP secret and QR code URI for setting up an authenticator app.
    /// The secret is generated on first call and persisted until 2FA is enabled.
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
