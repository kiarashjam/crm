using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ACI.Domain.Common;
using ACI.WebApi.Filters;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Email written in the CRM's composer (lead and contact pages) or confirmed from the
/// sales assistant, sent through the outbox and the configured relay — SendGrid in
/// production — under the workspace's name.
/// </summary>
/// <remarks>
/// A workspace is required. Viewers are refused by ReadOnlyMemberMiddleware, but
/// only for a request that names a workspace; without this check a Viewer could
/// leave the header off and send. Rate limited per person ("composed-email").
/// </remarks>
[ApiController]
[Authorize]
[Route("api/email")]
[Requires(MemberPermissions.EditRecords, WritesOnly = true)]
public class EmailController : ControllerBase
{
    private readonly IComposedEmailService _email;
    private readonly IOutbox _outbox;
    private readonly ICurrentUserService _currentUser;

    public EmailController(IComposedEmailService email, IOutbox outbox, ICurrentUserService currentUser)
    {
        _email = email;
        _outbox = outbox;
        _currentUser = currentUser;
    }

    /// <summary>
    /// 200 when the mail server took it. 202 with status "queued" when it did not take
    /// it yet: the outbox keeps trying, and it can be cancelled until it goes.
    /// </summary>
    [HttpPost("send")]
    [EnableRateLimiting("composed-email")]
    [RequestSizeLimit(256 * 1024)]
    public async Task<ActionResult<SentEmailDto>> Send([FromBody] SendEmailRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        if (_currentUser.CurrentOrganizationId is not { } organizationId)
            return Problem(detail: "X-Organization-Id required", statusCode: StatusCodes.Status400BadRequest);
        var result = await _email.SendAsync(organizationId, request, _currentUser.UserId.Value, ct);
        if (result.IsSuccess && result.Value.Status == "queued") return Accepted(result.Value);
        return result.ToActionResult();
    }

    /// <summary>Takes back a queued email before it goes: whoever sent it, or a manager.</summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult> Cancel(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId == null) return Unauthorized();
        if (_currentUser.CurrentOrganizationId is not { } organizationId)
            return Problem(detail: "X-Organization-Id required", statusCode: StatusCodes.Status400BadRequest);
        return (await _outbox.CancelAsync(id, organizationId, _currentUser.UserId.Value, ct)).ToNoContentResult();
    }
}
