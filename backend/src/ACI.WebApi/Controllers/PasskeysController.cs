using System.IdentityModel.Tokens.Jwt;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Passkeys (WebAuthn): the signed-in person's own (list, add, remove), signing in with one,
/// and using one in place of the authenticator code. Each ceremony is two calls: options for
/// navigator.credentials, then the browser's answer with the ceremony's id.
/// </summary>
[ApiController]
[Route("api/auth/passkeys")]
[Produces("application/json")]
public class PasskeysController : ControllerBase
{
    private readonly IPasskeys _passkeys;
    private readonly ICurrentUserService _currentUser;

    public PasskeysController(IPasskeys passkeys, ICurrentUserService currentUser)
    {
        _passkeys = passkeys;
        _currentUser = currentUser;
    }

    /// <summary>The signed-in person's passkeys.</summary>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<PasskeyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PasskeyDto>>> List(CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return Ok(await _passkeys.ListAsync(userId, ct));
    }

    /// <summary>
    /// Starts adding a passkey. Takes the account's password; an account without one (made by
    /// single sign-on) needs to have signed in within the last 15 minutes instead.
    /// </summary>
    /// <response code="200">The options for navigator.credentials.create, and the ceremony's id.</response>
    /// <response code="400">Wrong or missing password, a sign-in too long ago, or 20 passkeys already.</response>
    /// <response code="503">Passkeys are not set up on this server.</response>
    [HttpPost("registration/options")]
    [Authorize]
    [ProducesResponseType(typeof(PasskeyCeremonyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PasskeyCeremonyDto>> RegistrationOptions([FromBody] PasskeyRegistrationOptionsRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _passkeys.RegistrationOptionsAsync(userId, request, SessionIssuedAt(), ct)).ToActionResult();
    }

    /// <summary>Finishes adding a passkey with the browser's answer.</summary>
    [HttpPost("registration")]
    [Authorize]
    [ProducesResponseType(typeof(PasskeyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PasskeyDto>> Register([FromBody] PasskeyRegistrationRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _passkeys.RegisterAsync(userId, request, ct)).ToActionResult();
    }

    /// <summary>Removes one of the signed-in person's passkeys.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _passkeys.RemoveAsync(userId, id, ct)).ToNoContentResult();
    }

    /// <summary>Options for signing in with a passkey instead of a password.</summary>
    [HttpPost("sign-in/options")]
    [AllowAnonymous]
    [EnableRateLimiting("passkey")]
    [ProducesResponseType(typeof(PasskeyCeremonyDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PasskeyCeremonyDto>> SignInOptions(CancellationToken ct) =>
        (await _passkeys.SignInOptionsAsync(ct)).ToActionResult();

    /// <summary>Signs in with the browser's answer: the same session a password gives.</summary>
    /// <response code="200">Signed in.</response>
    /// <response code="401">The passkey was not recognised, or its counter went backwards.</response>
    /// <response code="429">Too many failed tries for the account; wait 15 minutes.</response>
    [HttpPost("sign-in")]
    [AllowAnonymous]
    [EnableRateLimiting("passkey")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> SignIn([FromBody] PasskeyAssertionRequest request, CancellationToken ct) =>
        (await _passkeys.SignInAsync(request, ct)).ToActionResult();

    /// <summary>Options for using a passkey in place of the authenticator code, for the step token sign-in handed out.</summary>
    [HttpPost("second-factor/options")]
    [AllowAnonymous]
    [EnableRateLimiting("passkey")]
    [ProducesResponseType(typeof(PasskeyCeremonyDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PasskeyCeremonyDto>> SecondFactorOptions([FromBody] PasskeySecondFactorOptionsRequest request, CancellationToken ct) =>
        (await _passkeys.SecondFactorOptionsAsync(request, ct)).ToActionResult();

    /// <summary>Finishes a two-factor sign-in with a passkey instead of the code.</summary>
    [HttpPost("second-factor")]
    [AllowAnonymous]
    [EnableRateLimiting("passkey")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> SecondFactor([FromBody] PasskeySecondFactorRequest request, CancellationToken ct) =>
        (await _passkeys.SecondFactorAsync(request, ct)).ToActionResult();

    /// <summary>When the caller's session began (its token's iat), if it says.</summary>
    private DateTime? SessionIssuedAt() =>
        long.TryParse(User.FindFirst(JwtRegisteredClaimNames.Iat)?.Value, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            : null;
}
