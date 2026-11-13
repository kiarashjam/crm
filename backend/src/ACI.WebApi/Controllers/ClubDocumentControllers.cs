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
// ClubLedgerControllers.cs. And events with their guest lists (ClubEventControllers.cs), the
// bookable resources with their bookings (ClubReservationControllers.cs), and applications and
// households (ClubMembershipControllers.cs).

[ApiController]
[Route("api/hospitality")]
[Authorize]
[WorkspaceRequired]
[InModule(WorkspaceModules.Club)]
public class HospitalityController : ControllerBase
{
    private readonly IClubDocumentService _docs;
    private readonly ICurrentUserService _user;
    public HospitalityController(IClubDocumentService docs, ICurrentUserService user) { _docs = docs; _user = user; }

    [HttpGet("settings")] public async Task<IActionResult> Get(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.GetSettingsAsync(org, ct)); }
    [HttpPut("settings")][Requires(MemberPermissions.ManageSettings, What = "change the hospitality settings")] public async Task<IActionResult> Save(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.SaveSettingsAsync(org, await ClubDocs.Body(Request, ct), ct)); }
    [HttpPost("settings/reset")][Requires(MemberPermissions.ManageSettings, What = "reset the hospitality settings")] public async Task<IActionResult> Reset(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.ResetSettingsAsync(org, ct)); }
}
