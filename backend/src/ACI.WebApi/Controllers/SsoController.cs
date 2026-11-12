using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Single sign-on with Google and Microsoft (see ISingleSignOn), and the accounts a person
/// signs in with that way.
/// </summary>
/// <remarks>
/// Start and callback are browser navigations, not calls from the app: they answer with
/// redirects, and a failure sends the browser back to the app's /auth/sso page with the reason
/// in the address fragment, never to a page of JSON.
/// </remarks>
[ApiController]
[Route("api/auth/sso")]
[Produces("application/json")]
public class SsoController : ControllerBase
{
    private readonly ISingleSignOn _sso;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<SsoController> _logger;

    public SsoController(ISingleSignOn sso, ICurrentUserService currentUser, ILogger<SsoController> logger)
    {
        _sso = sso;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>The ways to sign in besides the password this server offers, for the sign-in page.</summary>
    [HttpGet("/api/auth/sign-in-options")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SignInOptionsDto), StatusCodes.Status200OK)]
    public ActionResult<SignInOptionsDto> SignInOptions([FromServices] IPasskeys passkeys) =>
        Ok(new SignInOptionsDto(_sso.Providers, passkeys.IsConfigured));

    /// <summary>The providers this server offers (none until the owner sets them up).</summary>
    [HttpGet("providers")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<SsoProviderDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<SsoProviderDto>> Providers() => Ok(_sso.Providers);

    /// <summary>Sends the browser to the provider to sign in.</summary>
    /// <param name="provider">"google" or "microsoft".</param>
    /// <param name="challenge">base64url(SHA-256(secret)) of a secret the app keeps for <see cref="Complete"/>.</param>
    /// <response code="302">To the provider; or, when it cannot start, back to the app with the reason.</response>
    [HttpGet("{provider}/start")]
    [AllowAnonymous]
    [EnableRateLimiting("sso")]
    public async Task<IActionResult> Start(string provider, [FromQuery] string? challenge, CancellationToken ct)
    {
        var started = await _sso.StartAsync(provider, challenge ?? "", ct);
        if (started.IsSuccess) return Redirect(started.Value);

        var reason = started.Error.Code switch
        {
            "Sso.NotOffered" => "not_configured",
            "Sso.BadChallenge" => "bad_request",
            _ => "provider_unavailable",
        };
        return BackToApp(reason) ?? started.Error.ToProblemResult();
    }

    /// <summary>Where the provider sends the browser back. Registered with each provider as the redirect URI.</summary>
    /// <response code="302">To the app, with a one-time code or the reason it failed.</response>
    [HttpGet("{provider}/callback")]
    [AllowAnonymous]
    [EnableRateLimiting("sso")]
    public async Task<IActionResult> Callback(string provider, [FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken ct)
    {
        try
        {
            return Redirect(await _sso.ReturnFromProviderAsync(provider, code, state, error, ct));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Single sign-on with {Provider} failed on the way back", provider);
            return BackToApp("server_error") ?? Problem("The sign-in failed. Try again.");
        }
    }

    /// <summary>
    /// Trades the one-time code from the provider's round trip, and the secret whose hash
    /// started it, for a session (or the two-factor step, when 2FA is on).
    /// </summary>
    /// <response code="200">Signed in, or the code is needed next.</response>
    /// <response code="400">The code has expired or was used, or this browser did not start the sign-in.</response>
    [HttpPost("complete")]
    [AllowAnonymous]
    [EnableRateLimiting("sso")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LoginResponse>> Complete([FromBody] SsoCompleteRequest request, CancellationToken ct) =>
        (await _sso.CompleteAsync(request, ct)).ToActionResult();

    /// <summary>The Google and Microsoft accounts the signed-in person signs in with.</summary>
    [HttpGet("links")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<SsoLinkDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SsoLinkDto>>> Links(CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return Ok(await _sso.LinksAsync(userId, ct));
    }

    /// <summary>Stops one of them signing the person in, unless it is their only way in.</summary>
    [HttpDelete("links/{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Unlink(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return (await _sso.UnlinkAsync(userId, id, ct)).ToNoContentResult();
    }

    private IActionResult? BackToApp(string reason) =>
        _sso.ReturnToAppWithError(reason) is { } url ? Redirect(url) : null;
}
