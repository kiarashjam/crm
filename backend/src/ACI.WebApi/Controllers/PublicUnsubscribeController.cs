using ACI.Application.Configuration;
using ACI.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace ACI.WebApi.Controllers;

/// <summary>
/// The unsubscribe link in a workspace's outreach email, or in a club member's marketing
/// email, reached by the person it was sent to, who has no account. The signed token in
/// the path is the whole permission.
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
    private readonly EmailSettings _email;

    public PublicUnsubscribeController(IUnsubscribeService unsubscribe, IOptions<EmailSettings> email)
    {
        _unsubscribe = unsubscribe;
        _email = email.Value;
    }

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

    /// <summary>
    /// The address in the List-Unsubscribe header (RFC 8058). A mail app's own Unsubscribe
    /// button POSTs "List-Unsubscribe=One-Click" here, without cookies or a page, and the
    /// person is unsubscribed then and there; the body is not needed to know what to do.
    /// </summary>
    [HttpPost("{token}/one-click")]
    public async Task<IActionResult> OneClick(string token, CancellationToken ct)
    {
        var done = await _unsubscribe.UnsubscribeAsync(token, ct);
        return done == null ? NotFound() : Ok(done);
    }

    /// <summary>
    /// The same address opened in a browser, by a mail app that only knows how to open a
    /// link: it goes to the confirmation page, where the person presses Unsubscribe. A GET
    /// never unsubscribes, as above.
    /// </summary>
    [HttpGet("{token}/one-click")]
    public IActionResult OneClickPage(string token)
    {
        var page = (_email.FrontendBaseUrl ?? "").Trim().TrimEnd('/');
        if (!Uri.TryCreate(page, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return NotFound();
        return Redirect($"{page}/unsubscribe/{Uri.EscapeDataString(token)}");
    }
}
