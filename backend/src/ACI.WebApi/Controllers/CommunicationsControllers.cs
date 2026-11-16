using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Campaigns to a club's members, delivered by email through the outbox: read by anyone in
/// the club, written and sent by those who manage members.
/// </summary>
[ApiController]
[Route("api/campaigns")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.ClubMembers, WritesOnly = true)]
[InModule(WorkspaceModules.Club)]
public class CampaignsController : ControllerBase
{
    private readonly ICampaignService _campaigns;
    private readonly ICurrentUserService _currentUser;

    public CampaignsController(ICampaignService campaigns, ICurrentUserService currentUser)
    {
        _campaigns = campaigns;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CampaignDto>>> List(CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        return Ok(await _campaigns.ListAsync(org, ct));
    }

    /// <summary>Who a campaign for this segment and purpose would reach if it were sent now.</summary>
    [HttpGet("audience")]
    public async Task<ActionResult<CampaignAudienceDto>> Audience(
        [FromQuery] string[]? tiers, [FromQuery] string[]? statuses, [FromQuery] string? purpose, CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        return (await _campaigns.AudienceAsync(org, new CampaignSegmentDto(tiers, statuses), purpose, ct)).ToActionResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CampaignDto>> Get(Guid id, CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        var found = await _campaigns.GetAsync(org, id, ct);
        if (found == null) return NotFound();
        Response.SetETag(found.Version);
        return Ok(found);
    }

    /// <summary>Every member the campaign was for, with where their message stands.</summary>
    [HttpGet("{id:guid}/recipients")]
    public async Task<ActionResult<IReadOnlyList<MemberMessageDto>>> Recipients(Guid id, CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        return (await _campaigns.RecipientsAsync(org, id, ct)).ToActionResult();
    }

    [HttpPost]
    public async Task<ActionResult<CampaignDto>> Create([FromBody] CampaignWriteRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId || _currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        var result = await _campaigns.CreateAsync(org, userId, request ?? new CampaignWriteRequest(), ct);
        if (result.IsSuccess) Response.SetETag(result.Value.Version);
        return result.ToActionResult();
    }

    /// <summary>Changes a draft or scheduled campaign; scheduledAtUtc sent as null unschedules it. 409 once it has started sending.</summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CampaignDto>> Update(Guid id, [FromBody] CampaignWriteRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId || _currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        request ??= new CampaignWriteRequest();
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();
        request.Version = version;
        var result = await _campaigns.UpdateAsync(org, id, userId, request, ct);
        if (result.IsSuccess) Response.SetETag(result.Value.Version);
        return result.ToActionResult();
    }

    /// <summary>Deletes a draft or scheduled campaign. One that has started sending is kept (409).</summary>
    [HttpDelete("{id:guid}")]
    [Requires(MemberPermissions.DeleteRecords)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        return (await _campaigns.DeleteAsync(org, id, ct)).ToNoContentResult();
    }

    /// <summary>
    /// Sends it now, once: one email per member it is for. Answers at once, 202 with the
    /// campaign sending: the scheduler works through its members in the background. 409 when
    /// it is being sent or has been, or (with If-Match) when it changed since it was read.
    /// </summary>
    [HttpPost("{id:guid}/send")]
    public async Task<ActionResult<CampaignDto>> Send(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId || _currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        var (version, versionError) = Request.ExpectedVersion(null);
        if (versionError != null) return versionError.ToProblemResult();
        var result = await _campaigns.SendAsync(org, id, userId, version, ct);
        if (result.IsFailure) return result.Error.ToProblemResult();
        Response.SetETag(result.Value.Version);
        return StatusCode(StatusCodes.Status202Accepted, result.Value);
    }

    /// <summary>
    /// Stops a campaign that is sending: members it has not reached get nothing, and its
    /// emails still waiting in the outbox do not go. 409 when it is not sending, or (with
    /// If-Match) when it changed since it was read.
    /// </summary>
    [HttpPost("{id:guid}/stop")]
    public async Task<ActionResult<CampaignDto>> Stop(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId || _currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        var (version, versionError) = Request.ExpectedVersion(null);
        if (versionError != null) return versionError.ToProblemResult();
        var result = await _campaigns.StopAsync(org, id, userId, version, ct);
        if (result.IsSuccess) Response.SetETag(result.Value.Version);
        return result.ToActionResult();
    }

    /// <summary>Sends a copy marked as a test to the signed-in person alone, counted in none of the campaign's figures.</summary>
    [HttpPost("{id:guid}/test")]
    public async Task<ActionResult<CampaignTestDto>> Test(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId || _currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        return (await _campaigns.SendTestAsync(org, id, userId, ct)).ToActionResult();
    }
}

/// <summary>
/// The communications log: what each member was sent, by campaigns and by staff, with what
/// happened to each; an email to one member; and a message recorded that went out another way.
/// </summary>
[ApiController]
[Route("api/communications")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.ClubMembers, WritesOnly = true)]
[InModule(WorkspaceModules.Club)]
public class CommunicationsController : ControllerBase
{
    private readonly IMemberMessageService _messages;
    private readonly ICurrentUserService _currentUser;

    public CommunicationsController(IMemberMessageService messages, ICurrentUserService currentUser)
    {
        _messages = messages;
        _currentUser = currentUser;
    }

    /// <summary>The newest messages, the club's or one member's (memberId).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MemberMessageDto>>> List([FromQuery] Guid? memberId, [FromQuery] int? take, CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        return Ok(await _messages.ListAsync(org, memberId, take ?? 500, ct));
    }

    /// <summary>
    /// Records a message that went out some other way: nothing is sent. The log the app had
    /// before Cadence sent anything; a client from then still records rather than sends.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<MemberMessageDto>> Log([FromBody] LogMemberMessageRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId || _currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        return (await _messages.LogAsync(org, userId, request ?? new LogMemberMessageRequest(), ct)).ToActionResult();
    }

    /// <summary>Emails one member through the outbox; marketing only with their consent.</summary>
    [HttpPost("send")]
    public async Task<ActionResult<MemberMessageDto>> Send([FromBody] SendMemberMessageRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId || _currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        return (await _messages.SendAsync(org, userId, request ?? new SendMemberMessageRequest(), ct)).ToActionResult();
    }

    /// <summary>Deletes a message staff logged; one Cadence sent, or did not send for a reason, is kept (409).</summary>
    [HttpDelete("{id:guid}")]
    [Requires(MemberPermissions.DeleteRecords)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (_currentUser.CurrentOrganizationId is not Guid org) return Unauthorized();
        return (await _messages.DeleteAsync(org, id, ct)).ToNoContentResult();
    }
}
