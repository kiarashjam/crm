using ACI.Application.DTOs;
using ACI.Application.Services;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

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
    public async Task<ActionResult<LeadDto>> Capture(string token, [FromBody] PublicCaptureRequest request, CancellationToken ct)
    {
        var result = await _ops.CaptureAsync(token, request, ct);
        if (result.IsSuccess)
            return StatusCode(StatusCodes.Status201Created, result.Value);
        return result.ToActionResult();
    }

    [HttpGet("track/{leadId:guid}")]
    public async Task<IActionResult> Track(Guid leadId, CancellationToken ct)
    {
        await _ops.RecordEmailOpenAsync(leadId, ct);
        return File(Pixel, "image/gif");
    }
}
