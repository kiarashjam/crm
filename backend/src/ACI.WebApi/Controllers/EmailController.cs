using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Email written in the CRM's composer (lead and contact pages), sent through the
/// configured relay — SendGrid in production — from the workspace's address.
/// </summary>
/// <remarks>
/// A workspace is required. Viewers are refused by ReadOnlyMemberMiddleware, but
/// only for a request that names a workspace; without this check a Viewer could
/// leave the header off and send. Rate limited per person ("composed-email").
/// </remarks>
[ApiController]
[Authorize]
[Route("api/email")]
public class EmailController : ControllerBase
{
    private readonly IComposedEmailService _email;
    private readonly ICurrentUserService _currentUser;

    public EmailController(IComposedEmailService email, ICurrentUserService currentUser)
    {
        _email = email;
        _currentUser = currentUser;
    }

    [HttpPost("send")]
    [EnableRateLimiting("composed-email")]
    [RequestSizeLimit(256 * 1024)]
    public async Task<ActionResult<SentEmailDto>> Send([FromBody] SendEmailRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        if (_currentUser.CurrentOrganizationId is not { } organizationId)
            return Problem(detail: "X-Organization-Id required", statusCode: StatusCodes.Status400BadRequest);
        return (await _email.SendAsync(organizationId, request, _currentUser.UserId.Value, ct)).ToActionResult();
    }
}
