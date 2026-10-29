using ACI.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ACI.WebApi.Controllers;

/// <summary>
/// The unsubscribe link in a workspace's outreach email, reached by the person it
/// was sent to, who has no account. The signed token in the path is the whole
/// permission.
/// </summary>
/// <remarks>
/// GET only says what the link would stop; POST does it. Mail scanners open every
/// link in a message, so a GET that unsubscribed would unsubscribe whole companies.
/// Rate limited per token ("public-unsubscribe").
/// </remarks>
[ApiController]
[AllowAnonymous]
[Route("api/public/unsubscribe")]
[EnableRateLimiting("public-unsubscribe")]
public class PublicUnsubscribeController : ControllerBase
{
    private readonly IUnsubscribeService _unsubscribe;

    public PublicUnsubscribeController(IUnsubscribeService unsubscribe) => _unsubscribe = unsubscribe;

    [HttpGet("{token}")]
    public async Task<ActionResult<UnsubscribeDto>> Preview(string token, CancellationToken ct)
    {
        var found = await _unsubscribe.PreviewAsync(token, ct);
        return found == null ? NotFound() : Ok(found);
    }

    [HttpPost("{token}")]
    public async Task<ActionResult<UnsubscribeDto>> Unsubscribe(string token, CancellationToken ct)
    {
        var done = await _unsubscribe.UnsubscribeAsync(token, ct);
        return done == null ? NotFound() : Ok(done);
    }
}
