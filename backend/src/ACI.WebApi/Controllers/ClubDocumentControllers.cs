using System.Text.Json.Nodes;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.WebApi.Extensions;
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

[ApiController]
[Route("api/campaigns")]
[Authorize]
[WorkspaceRequired]
public class CampaignsController : ControllerBase
{
    private readonly IClubDocumentService _docs;
    private readonly ICurrentUserService _user;
    public CampaignsController(IClubDocumentService docs, ICurrentUserService user) { _docs = docs; _user = user; }

    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.ListAsync(org, "campaigns", ct)); }
    [HttpPost] public async Task<IActionResult> Create(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.CreateAsync(org, "campaigns", await ClubDocs.Body(Request, ct), ct)); }
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.UpdateAsync(org, "campaigns", id, await ClubDocs.Body(Request, ct), ct)); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return await _docs.DeleteAsync(org, "campaigns", id, ct) ? NoContent() : NotFound(); }
    [HttpPost("{id:guid}/send")] public async Task<IActionResult> Send(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); var row = await _docs.SendCampaignAsync(org, id, ct); return row == null ? NotFound() : Ok(row); }
}

[ApiController]
[Route("api/events")]
[Authorize]
[WorkspaceRequired]
public class EventsController : ControllerBase
{
    private readonly IClubDocumentService _docs;
    private readonly ICurrentUserService _user;
    public EventsController(IClubDocumentService docs, ICurrentUserService user) { _docs = docs; _user = user; }

    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.ListAsync(org, "events", ct)); }
    [HttpPost] public async Task<IActionResult> Create(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.CreateAsync(org, "events", await ClubDocs.Body(Request, ct), ct)); }
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.UpdateAsync(org, "events", id, await ClubDocs.Body(Request, ct), ct)); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return await _docs.DeleteAsync(org, "events", id, ct) ? NoContent() : NotFound(); }
    [HttpPost("{id:guid}/rsvp")] public async Task<IActionResult> Rsvp(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); var row = await _docs.RsvpAsync(org, id, ct); return row == null ? NotFound() : Ok(row); }
}

[ApiController]
[Route("api/donations")]
[Authorize]
[WorkspaceRequired]
public class DonationsController : ControllerBase
{
    private readonly IClubDocumentService _docs;
    private readonly ICurrentUserService _user;
    public DonationsController(IClubDocumentService docs, ICurrentUserService user) { _docs = docs; _user = user; }

    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.ListAsync(org, "donations", ct)); }
    [HttpPost] public async Task<IActionResult> Create(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.CreateAsync(org, "donations", await ClubDocs.Body(Request, ct), ct)); }
    [HttpPost("{id:guid}/acknowledge")] public async Task<IActionResult> Acknowledge(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); var row = await _docs.AcknowledgeDonationAsync(org, id, ct); return row == null ? NotFound() : Ok(row); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return await _docs.DeleteAsync(org, "donations", id, ct) ? NoContent() : NotFound(); }
}

[ApiController]
[Route("api/communications")]
[Authorize]
[WorkspaceRequired]
public class CommunicationsController : ControllerBase
{
    private readonly IClubDocumentService _docs;
    private readonly ICurrentUserService _user;
    public CommunicationsController(IClubDocumentService docs, ICurrentUserService user) { _docs = docs; _user = user; }

    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.ListAsync(org, "communications", ct)); }
    [HttpPost] public async Task<IActionResult> Create(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.CreateAsync(org, "communications", await ClubDocs.Body(Request, ct), ct)); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return await _docs.DeleteAsync(org, "communications", id, ct) ? NoContent() : NotFound(); }
}

[ApiController]
[Route("api/charges")]
[Authorize]
[WorkspaceRequired]
public class ChargesController : ControllerBase
{
    private readonly IClubDocumentService _docs;
    private readonly ICurrentUserService _user;
    public ChargesController(IClubDocumentService docs, ICurrentUserService user) { _docs = docs; _user = user; }

    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.ListAsync(org, "charges", ct)); }
    [HttpPost] public async Task<IActionResult> Create(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.CreateAsync(org, "charges", await ClubDocs.Body(Request, ct), ct)); }
    // A charge is voided, not deleted, so the record of what was charged stays. Answered
    // with a sentence rather than a bare 405, which the app would read as a missing feature.
    [HttpDelete("{id:guid}")] public IActionResult Delete(Guid id) => DomainErrors.Club.ChargeCannotDelete.ToProblemResult();
    [HttpPost("{id:guid}/void")][ManagersOnly("void a charge")] public async Task<IActionResult> Void(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.VoidChargeAsync(org, id, await ClubDocs.Body(Request, ct), ct)); }
}

[ApiController]
[Route("api/payments")]
[Authorize]
[WorkspaceRequired]
public class PaymentsController : ControllerBase
{
    private readonly IClubDocumentService _docs;
    private readonly ICurrentUserService _user;
    public PaymentsController(IClubDocumentService docs, ICurrentUserService user) { _docs = docs; _user = user; }

    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.ListAsync(org, "payments", ct)); }
    [HttpPost] public async Task<IActionResult> Create(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.CreateAsync(org, "payments", await ClubDocs.Body(Request, ct), ct)); }
    [HttpPost("{id:guid}/void")][ManagersOnly("void a payment")] public async Task<IActionResult> Void(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.VoidPaymentAsync(org, id, await ClubDocs.Body(Request, ct), ct)); }
}

[ApiController]
[Route("api/applications")]
[Authorize]
[WorkspaceRequired]
public class ApplicationsController : ControllerBase
{
    private readonly IClubDocumentService _docs;
    private readonly ICurrentUserService _user;
    public ApplicationsController(IClubDocumentService docs, ICurrentUserService user) { _docs = docs; _user = user; }

    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.ListAsync(org, "applications", ct)); }
    [HttpPost] public async Task<IActionResult> Create(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.CreateAsync(org, "applications", await ClubDocs.Body(Request, ct), ct)); }
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.UpdateAsync(org, "applications", id, await ClubDocs.Body(Request, ct), ct)); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return await _docs.DeleteAsync(org, "applications", id, ct) ? NoContent() : NotFound(); }
}

[ApiController]
[Route("api/resources")]
[Authorize]
[WorkspaceRequired]
public class ResourcesController : ControllerBase
{
    private readonly IClubDocumentService _docs;
    private readonly ICurrentUserService _user;
    public ResourcesController(IClubDocumentService docs, ICurrentUserService user) { _docs = docs; _user = user; }
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.ListAsync(org, "resources", ct)); }
}

[ApiController]
[Route("api/reservations")]
[Authorize]
[WorkspaceRequired]
public class ReservationsController : ControllerBase
{
    private readonly IClubDocumentService _docs;
    private readonly ICurrentUserService _user;
    public ReservationsController(IClubDocumentService docs, ICurrentUserService user) { _docs = docs; _user = user; }

    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.ListAsync(org, "reservations", ct)); }
    [HttpPost] public async Task<IActionResult> Create(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.CreateAsync(org, "reservations", await ClubDocs.Body(Request, ct), ct)); }
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.UpdateAsync(org, "reservations", id, await ClubDocs.Body(Request, ct), ct)); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return await _docs.DeleteAsync(org, "reservations", id, ct) ? NoContent() : NotFound(); }
}

[ApiController]
[Route("api/households")]
[Authorize]
[WorkspaceRequired]
public class HouseholdsController : ControllerBase
{
    private readonly IClubDocumentService _docs;
    private readonly ICurrentUserService _user;
    public HouseholdsController(IClubDocumentService docs, ICurrentUserService user) { _docs = docs; _user = user; }

    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.ListAsync(org, "households", ct)); }
    [HttpPost] public async Task<IActionResult> Create(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.CreateAsync(org, "households", await ClubDocs.Body(Request, ct), ct)); }
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.UpdateAsync(org, "households", id, await ClubDocs.Body(Request, ct), ct)); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return await _docs.DeleteAsync(org, "households", id, ct) ? NoContent() : NotFound(); }
}

[ApiController]
[Route("api/household-members")]
[Authorize]
[WorkspaceRequired]
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
[Route("api/subscriptions")]
[Authorize]
[WorkspaceRequired]
public class SubscriptionsController : ControllerBase
{
    private readonly IClubDocumentService _docs;
    private readonly ICurrentUserService _user;
    public SubscriptionsController(IClubDocumentService docs, ICurrentUserService user) { _docs = docs; _user = user; }

    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.ListAsync(org, "subscriptions", ct)); }
    [HttpPost] public async Task<IActionResult> Create(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.CreateAsync(org, "subscriptions", await ClubDocs.Body(Request, ct), ct)); }
    [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.UpdateAsync(org, "subscriptions", id, await ClubDocs.Body(Request, ct), ct)); }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return await _docs.DeleteAsync(org, "subscriptions", id, ct) ? NoContent() : NotFound(); }
    [HttpPost("{id:guid}/bill")] public async Task<IActionResult> Bill(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.BillSubscriptionAsync(org, id, ct)); }
}

[ApiController]
[Route("api/gift-cards")]
[Authorize]
[WorkspaceRequired]
public class GiftCardsController : ControllerBase
{
    private readonly IClubDocumentService _docs;
    private readonly ICurrentUserService _user;
    public GiftCardsController(IClubDocumentService docs, ICurrentUserService user) { _docs = docs; _user = user; }

    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.ListAsync(org, "gift-cards", ct)); }
    [HttpGet("redemptions")] public async Task<IActionResult> Redemptions(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.ListAsync(org, "gift-card-redemptions", ct)); }
    [HttpPost] public async Task<IActionResult> Create(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.CreateAsync(org, "gift-cards", await ClubDocs.Body(Request, ct), ct)); }
    [HttpPost("{id:guid}/redeem")] public async Task<IActionResult> Redeem(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.RedeemGiftCardAsync(org, id, await ClubDocs.Body(Request, ct), Request.Headers["Idempotency-Key"].ToString(), ct)); }
    [HttpPost("{id:guid}/void")][ManagersOnly("void a gift card")] public async Task<IActionResult> Void(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.VoidGiftCardAsync(org, id, await ClubDocs.Body(Request, ct), ct)); }
}

[ApiController]
[Route("api/tabs")]
[Authorize]
[WorkspaceRequired]
public class TabsController : ControllerBase
{
    private readonly IClubDocumentService _docs;
    private readonly ICurrentUserService _user;
    public TabsController(IClubDocumentService docs, ICurrentUserService user) { _docs = docs; _user = user; }

    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.ListAsync(org, "tabs", ct)); }
    [HttpPost] public async Task<IActionResult> Create(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.CreateAsync(org, "tabs", await ClubDocs.Body(Request, ct), ct)); }
    [HttpPost("{id:guid}/items")] public async Task<IActionResult> AddItem(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.AddTabItemAsync(org, id, await ClubDocs.Body(Request, ct), ct)); }
    [HttpDelete("{tabId:guid}/items/{itemId:guid}")] public async Task<IActionResult> RemoveItem(Guid tabId, Guid itemId, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.RemoveTabItemAsync(org, tabId, itemId, ct)); }
    [HttpPost("{id:guid}/close")] public async Task<IActionResult> Close(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.CloseTabAsync(org, id, ct)); }
    [HttpPost("{id:guid}/void")] public async Task<IActionResult> Void(Guid id, CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return ClubDocs.Answer(await _docs.VoidTabAsync(org, id, await ClubDocs.Body(Request, ct), ct)); }
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
    [HttpPut("settings")][ManagersOnly("change the hospitality settings")] public async Task<IActionResult> Save(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.SaveSettingsAsync(org, await ClubDocs.Body(Request, ct), ct)); }
    [HttpPost("settings/reset")][ManagersOnly("reset the hospitality settings")] public async Task<IActionResult> Reset(CancellationToken ct) { if (!ClubDocs.User(_user, out var org)) return Unauthorized(); return Ok(await _docs.ResetSettingsAsync(org, ct)); }
}
