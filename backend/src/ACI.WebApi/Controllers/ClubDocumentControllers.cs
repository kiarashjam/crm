using System.Text.Json.Nodes;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.WebApi.Extensions;
using ACI.Domain.Common;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>Shared read of the signed-in workspace for the club document routes.</summary>
file static class ClubDocs
{
    public static bool User(ICurrentUserService current, out Guid? organizationId)
    {
        organizationId = current.CurrentOrganizationId;
        return current.UserId != null;
    }

    public static async Task<JsonObject> Body(HttpRequest request, CancellationToken ct)
    {
        // An action such as "void this tab" may be sent with no body at all.
        using var reader = new StreamReader(request.Body);
        var text = await reader.ReadToEndAsync(ct);
        if (string.IsNullOrWhiteSpace(text)) return new JsonObject();
        return JsonNode.Parse(text) as JsonObject ?? new JsonObject();
    }

    /// <summary>The record, or the refusal as a sentence the screen shows (400, 404 or 409).</summary>
    public static IActionResult Answer(Result<JsonObject> result) =>
        result.IsSuccess ? new OkObjectResult(result.Value) : result.Error.ToProblemResult();
}

// Campaigns and communications are typed now: CommunicationsControllers.cs. So is the
// club's money (charges, payments, subscriptions, gift cards, tabs, donations):
// ClubLedgerControllers.cs. And events with their guest lists (ClubEventControllers.cs), and
// the bookable resources with their bookings (ClubReservationControllers.cs).

[ApiController]
[Route("api/applications")]
[Authorize]
[WorkspaceRequired]
[Requires(MemberPermissions.ClubMembers, WritesOnly = true)]
public class ApplicationsController : ControllerBase
{
    private readonly IClubDocumentService _docs;
    private readonly ICurrentUserService _user;
    public ApplicationsController(IClubDocumentService docs, ICurrentUserService user) { _docs = docs; _user = user; }

    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.ListAsync(org, "applications", ct)); }
    [HttpPost] public async Task<IActionResult> Create(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.CreateAsync(org, "applications", await ClubDocs.Body(Request, ct), ct)); }
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.UpdateAsync(org, "applications", id, await ClubDocs.Body(Request, ct), ct)); }
    [HttpDelete("{id:guid}")][Requires(MemberPermissions.DeleteRecords)] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return await _docs.DeleteAsync(org, "applications", id, ct) ? NoContent() : NotFound(); }
}

[ApiController]
[Route("api/households")]
[Authorize]
[WorkspaceRequired]
[Requires(MemberPermissions.ClubMembers, WritesOnly = true)]
public class HouseholdsController : ControllerBase
{
    private readonly IClubDocumentService _docs;
    private readonly ICurrentUserService _user;
    public HouseholdsController(IClubDocumentService docs, ICurrentUserService user) { _docs = docs; _user = user; }

    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.ListAsync(org, "households", ct)); }
    [HttpPost] public async Task<IActionResult> Create(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.CreateAsync(org, "households", await ClubDocs.Body(Request, ct), ct)); }
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.UpdateAsync(org, "households", id, await ClubDocs.Body(Request, ct), ct)); }
    [HttpDelete("{id:guid}")][Requires(MemberPermissions.DeleteRecords)] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return await _docs.DeleteAsync(org, "households", id, ct) ? NoContent() : NotFound(); }
}

[ApiController]
[Route("api/household-members")]
[Authorize]
[WorkspaceRequired]
[Requires(MemberPermissions.ClubMembers, WritesOnly = true)]
public class HouseholdMembersController : ControllerBase
{
    private readonly IClubDocumentService _docs;
    private readonly ICurrentUserService _user;
    public HouseholdMembersController(IClubDocumentService docs, ICurrentUserService user) { _docs = docs; _user = user; }

    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.ListAsync(org, "household-members", ct)); }
    [HttpPost] public async Task<IActionResult> Create(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.CreateAsync(org, "household-members", await ClubDocs.Body(Request, ct), ct)); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return await _docs.DeleteAsync(org, "household-members", id, ct) ? NoContent() : NotFound(); }
}

[ApiController]
[Route("api/hospitality")]
[Authorize]
[WorkspaceRequired]
public class HospitalityController : ControllerBase
{
    private readonly IClubDocumentService _docs;
    private readonly ICurrentUserService _user;
    public HospitalityController(IClubDocumentService docs, ICurrentUserService user) { _docs = docs; _user = user; }

    [HttpGet("settings")] public async Task<IActionResult> Get(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.GetSettingsAsync(org, ct)); }
    [HttpPut("settings")][Requires(MemberPermissions.ManageSettings, What = "change the hospitality settings")] public async Task<IActionResult> Save(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.SaveSettingsAsync(org, await ClubDocs.Body(Request, ct), ct)); }
    [HttpPost("settings/reset")][Requires(MemberPermissions.ManageSettings, What = "reset the hospitality settings")] public async Task<IActionResult> Reset(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.ResetSettingsAsync(org, ct)); }
}
