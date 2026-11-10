using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// A member's consent to be contacted, per channel and purpose, with every answer kept:
/// read by anyone in the workspace, recorded by those who manage members.
/// </summary>
[ApiController]
[Route("api/members/{memberId:guid}/consent")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.ClubMembers, WritesOnly = true)]
public class MemberConsentController : ControllerBase
{
    private readonly IConsentService _consent;
    private readonly ICurrentUserService _currentUser;

    public MemberConsentController(IConsentService consent, ICurrentUserService currentUser)
    {
        _consent = consent;
        _currentUser = currentUser;
    }

    /// <summary>Where each channel and purpose stands, and the history of answers, newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<MemberConsentDto>> Get(Guid memberId, CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        var found = await _consent.ForMemberAsync(org, memberId, ct);
        return found == null ? NotFound() : Ok(found);
    }

    /// <summary>Records an answer the member gave; the one it replaces stays in the history.</summary>
    [HttpPost]
    public async Task<ActionResult<MemberConsentDto>> Record(Guid memberId, [FromBody] RecordConsentRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId || _currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        return (await _consent.RecordForMemberAsync(org, memberId, userId, request ?? new RecordConsentRequest(), ct)).ToActionResult();
    }
}
