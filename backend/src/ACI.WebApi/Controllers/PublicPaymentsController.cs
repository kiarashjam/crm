using ACI.Application.Services;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ACI.WebApi.Controllers;

/// <summary>
/// What a member who has no account, and Stripe, reach: the page a pay link opens, starting
/// Stripe's page from it, and Stripe's webhook.
/// </summary>
/// <remarks>
/// <para>
/// A pay link's token is the whole permission, and only for what it names: one member's
/// account, some of their charges, or saving a card. Opening the page changes nothing (mail
/// scanners open every link); pressing Pay starts Stripe's page. The token is kept out of the
/// request log (SecretPathRedactionMiddleware).
/// </para>
/// <para>
/// Stripe's webhook is answered by its signature alone, checked with the signing secret of the
/// workspace whose key is in the address. A post that matches no workspace is a 404, one whose
/// signature fails a 400, and one that failed half-way a 500 so Stripe posts it again.
/// </para>
/// </remarks>
[ApiController]
[AllowAnonymous]
[Route("api/public")]
public class PublicPaymentsController : ControllerBase
{
    /// <summary>Larger than any Stripe event.</summary>
    private const long MaxEventBytes = 1024 * 1024;

    private readonly IClubPayments _payments;

    public PublicPaymentsController(IClubPayments payments) => _payments = payments;

    /// <summary>What the pay link asks for: the club, whom it is for, what and how much.</summary>
    [HttpGet("pay/{token}")]
    [EnableRateLimiting("public-pay")]
    public async Task<ActionResult> Read(string token, CancellationToken ct)
    {
        var page = await _payments.ReadPayLinkAsync(token, ct);
        return page.IsSuccess ? Ok(page.Value) : page.Error.ToProblemResult();
    }

    /// <summary>Starts Stripe's page for the link, and says where it is.</summary>
    [HttpPost("pay/{token}/checkout")]
    [EnableRateLimiting("public-pay")]
    public async Task<ActionResult> Checkout(string token, CancellationToken ct)
    {
        var started = await _payments.StartCheckoutAsync(token, ct);
        return started.IsSuccess ? Ok(started.Value) : started.Error.ToProblemResult();
    }

    /// <summary>An event Stripe posts for the workspace whose webhook key this is.</summary>
    [HttpPost("payments/stripe/{key:guid}")]
    [RequestSizeLimit(MaxEventBytes)]
    public async Task<IActionResult> StripeWebhook(Guid key, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, ct);
        var outcome = await _payments.HandleStripeWebhookAsync(key, buffer.ToArray(), Request.Headers["Stripe-Signature"].ToString(), ct);
        return StatusCode(outcome.StatusCode, new { result = outcome.Result });
    }
}
