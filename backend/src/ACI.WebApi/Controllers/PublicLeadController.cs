using ACI.Application.DTOs;
using ACI.Application.Services;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ACI.WebApi.Controllers;

/// <summary>
/// The only endpoints a stranger can reach: a capture form, and the email-open pixel.
/// </summary>
/// <remarks>
/// Both are rate limited (see the "public-capture" and "public-track" policies in
/// Program.cs), by form token and by lead rather than by IP: behind Azure's front
/// end the connection address may be the proxy's, and a forwarded-for header is
/// whatever the caller says it is. A per-form ceiling holds either way.
/// </remarks>
[ApiController]
[AllowAnonymous]
[Route("api/public")]
public class PublicLeadController : ControllerBase
{
    private static readonly byte[] Pixel = Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7");

    private readonly ILeadOpsService _ops;

    public PublicLeadController(ILeadOpsService ops) => _ops = ops;

    [HttpGet("capture/{token}")]
    public async Task<ActionResult<PublicCaptureFormDto>> Form(string token, CancellationToken ct)
    {
        var form = await _ops.GetPublicFormAsync(token, ct);
        return form == null ? NotFound() : Ok(form);
    }

    [HttpPost("capture/{token}")]
    [EnableRateLimiting("public-capture")]
    [RequestSizeLimit(32 * 1024)]
    public async Task<ActionResult<PublicCaptureReceipt>> Capture(string token, [FromBody] PublicCaptureRequest request, CancellationToken ct)
    {
        var result = await _ops.CaptureAsync(token, request, ct);
        if (result.IsSuccess)
            return StatusCode(StatusCodes.Status201Created, result.Value);
        return result.ToActionResult();
    }

    [HttpGet("track/{leadId:guid}")]
    [EnableRateLimiting("public-track")]
    public async Task<IActionResult> Track(Guid leadId, CancellationToken ct)
    {
        await _ops.RecordEmailOpenAsync(leadId, ct);
        return File(Pixel, "image/gif");
    }
}
