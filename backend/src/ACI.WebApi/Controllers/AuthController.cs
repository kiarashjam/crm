using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUser;

    public AuthController(IAuthService authService, ICurrentUserService currentUser)
    {
        _authService = authService;
        _currentUser = currentUser;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        if (result == null)
            return Unauthorized();
        return Ok(result);
    }

    [HttpPost("login/2fa")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> LoginWithTwoFactor([FromBody] TwoFactorLoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginWithTwoFactorAsync(request, ct);
        if (result == null)
            return Unauthorized();
        return Ok(result);
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await _authService.RegisterAsync(
            new LoginRequest(request.Email, request.Password),
            request.Name,
            ct);
        if (result == null)
            return Conflict("Email already registered.");
        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<UserInfoDto> Me()
    {
        if (!_currentUser.UserId.HasValue)
            return Unauthorized();
        // Name/email are in JWT claims; for now return what middleware populated.
        var id = _currentUser.UserId.Value;
        var name = User.Identity?.Name ?? string.Empty;
        var email = User.Claims.FirstOrDefault(c => c.Type.EndsWith("/emailaddress", StringComparison.OrdinalIgnoreCase))?.Value
            ?? User.Claims.FirstOrDefault(c => c.Type.Contains("email", StringComparison.OrdinalIgnoreCase))?.Value
            ?? string.Empty;
        return Ok(new UserInfoDto(id, name, email));
    }

    [HttpPost("2fa/setup")]
    [Authorize]
    public async Task<ActionResult<TwoFactorSetupResponse>> TwoFactorSetup(CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            return Unauthorized();
        var res = await _authService.GetTwoFactorSetupAsync(_currentUser.UserId.Value, ct);
        if (res == null)
            return NotFound();
        return Ok(res);
    }

    [HttpPost("2fa/enable")]
    [Authorize]
    public async Task<IActionResult> EnableTwoFactor([FromBody] TwoFactorEnableRequest request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            return Unauthorized();
        var ok = await _authService.EnableTwoFactorAsync(_currentUser.UserId.Value, request, ct);
        return ok ? Ok() : BadRequest("Invalid code.");
    }

    [HttpPost("2fa/disable")]
    [Authorize]
    public async Task<IActionResult> DisableTwoFactor([FromBody] TwoFactorDisableRequest request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            return Unauthorized();
        var ok = await _authService.DisableTwoFactorAsync(_currentUser.UserId.Value, request, ct);
        return ok ? Ok() : BadRequest("Invalid password or code.");
    }
}

public record RegisterRequest(string Email, string Password, string Name);
