using System.Security.Cryptography;
using System.Text;
using ACI.Application.Common;
using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ACI.WebApi.Controllers;

/// <summary>
/// What the mail service posts back after the outbox sends: replies (SendGrid Inbound
/// Parse) and delivery reports (SendGrid Event Webhook).
/// </summary>
/// <remarks>
/// <para>
/// Nobody signs in here; the mail service does not. Inbound Parse is not signed either,
/// so the address it posts to carries a secret (Email:InboundParseSecret), and the
/// endpoint answers 404 to any other address, or when no secret is set at all. The
/// Event Webhook may carry a secret in its address (Email:EventWebhookSecret), a
/// signature checked with the key SendGrid shows (Email:EventWebhookVerificationKey),
/// or both; with neither set it answers 404. The secrets are kept out of the request
/// log (SecretPathRedactionMiddleware).
/// </para>
/// <para>
/// A post that matches nothing is still answered 200: the mail service would otherwise
/// post it again for days.
/// </para>
/// </remarks>
[ApiController]
[AllowAnonymous]
[Route("api/public/email")]
public class PublicEmailController : ControllerBase
{
    /// <summary>SendGrid's own limit for an inbound message, with room for the form around it.</summary>
    private const long MaxInbound = 40L * 1024 * 1024;

    private readonly IInboundEmailService _inbound;
    private readonly EmailSettings _settings;

    public PublicEmailController(IInboundEmailService inbound, IOptions<EmailSettings> settings)
    {
        _inbound = inbound;
        _settings = settings.Value;
    }

    /// <summary>A reply, posted by SendGrid Inbound Parse as multipart form data.</summary>
    [HttpPost("inbound/{secret}")]
    [RequestSizeLimit(MaxInbound)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxInbound, ValueLengthLimit = (int)MaxInbound)]
    public async Task<IActionResult> Inbound(string secret, CancellationToken ct)
    {
        if (!SameSecret(_settings.InboundParseSecret, secret)) return NotFound();
        if (!Request.HasFormContentType) return BadRequest();
        var form = await Request.ReadFormAsync(ct);
        string? Field(string name) => form.TryGetValue(name, out var value) && !string.IsNullOrEmpty(value.ToString()) ? value.ToString() : null;

        var reply = InboundParseReader.Read(new InboundParseReader.Fields(
            Field("email"), Field("headers"), Field("from"), Field("to"), Field("cc"), Field("subject"), Field("text"), Field("html"), Field("envelope")));
        if (reply == null) return Ok(new { matched = false });
        var outcome = await _inbound.ReceiveReplyAsync(reply, ct);
        return Ok(new { matched = outcome != InboundReplyOutcome.NotMatched });
    }

    /// <summary>Delivery reports, posted by SendGrid's Event Webhook as a JSON array.</summary>
    [HttpPost("events")]
    [HttpPost("events/{secret}")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> Events(string? secret, CancellationToken ct)
    {
        var configuredSecret = (_settings.EventWebhookSecret ?? "").Trim();
        var key = (_settings.EventWebhookVerificationKey ?? "").Trim();
        if (configuredSecret.Length == 0 && key.Length == 0) return NotFound();
        if (configuredSecret.Length > 0 && !SameSecret(configuredSecret, secret)) return NotFound();

        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, ct);
        var body = buffer.ToArray();
        if (key.Length > 0 && !InboundMail.IsSignedBy(key,
                Request.Headers["X-Twilio-Email-Event-Webhook-Signature"].ToString(),
                Request.Headers["X-Twilio-Email-Event-Webhook-Timestamp"].ToString(), body))
            return Unauthorized();

        var events = InboundMail.ParseEvents(Encoding.UTF8.GetString(body), OutboxEmailTags.MessageIdArg);
        var matched = await _inbound.ReceiveEventsAsync(events, ct);
        return Ok(new { received = events.Count, matched });
    }

    /// <summary>Compared in fixed time, so the secret cannot be guessed a character at a time.</summary>
    private static bool SameSecret(string? configured, string? given)
    {
        var expected = (configured ?? "").Trim();
        if (expected.Length == 0 || string.IsNullOrEmpty(given)) return false;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given));
    }
}
