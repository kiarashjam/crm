using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ACI.WebApi.Controllers;

// The club's money, on the typed ledger (IClubLedger). The routes and the shapes they answer
// with are the ones the club screens have always used; what changed is underneath: entries
// are only added, a void or a refund is an entry that reverses the original, and a balance
// is worked out from the entries. money.manage posts; money.void voids and refunds.

/// <summary>Shared reading of the request for the money routes.</summary>
file static class Ledger
{
    /// <summary>The Idempotency-Key a retried request carries, if any.</summary>
    public static string? Key(HttpRequest request) =>
        request.Headers.TryGetValue("Idempotency-Key", out var value) ? value.ToString() : null;

    /// <summary>The record, with its version as an ETag when it has one, or the refusal as a sentence (400, 404 or 409).</summary>
    public static ActionResult Answer<T>(ControllerBase controller, Result<T> result, Func<T, Guid?>? version = null)
    {
        if (!result.IsSuccess) return result.Error.ToProblemResult();
        if (version != null) controller.Response.SetETag(version(result.Value));
        return controller.Ok(result.Value);
    }
}

[ApiController]
[Route("api/charges")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.ManageMoney, WritesOnly = true)]
public class ChargesController : ControllerBase
{
    private readonly IClubLedger _ledger;
    private readonly ICurrentUserService _user;
    public ChargesController(IClubLedger ledger, ICurrentUserService user) { _ledger = ledger; _user = user; }

    /// <summary>The workspace's charges (or one member's), each with what it has been paid and its status.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ChargeDto>>> List([FromQuery] Guid? memberId, CancellationToken ct) =>
        Ok(await _ledger.ChargesAsync(_user.CurrentOrganizationId, memberId, ct));

    /// <summary>Posts a charge to a member's house account. A retry with the same Idempotency-Key gets the first charge back.</summary>
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] PostChargeRequest request, CancellationToken ct) =>
        Ledger.Answer(this, await _ledger.PostChargeAsync(_user.CurrentOrganizationId, request, Ledger.Key(Request), ct));

    // A charge is voided, not deleted, so the record of what was charged stays. Answered
    // with a sentence rather than a bare 405, which the app would read as a missing feature.
    [HttpDelete("{id:guid}")]
    public ActionResult Delete(Guid id) => DomainErrors.Club.ChargeCannotDelete.ToProblemResult();

    /// <summary>Voids a charge with a reason: an entry that takes it back. One with payments standing against it is voided after them.</summary>
    [HttpPost("{id:guid}/void")]
    [Requires(MemberPermissions.VoidMoney, What = "void a charge")]
    public async Task<ActionResult> Void(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReasonRequest? body, CancellationToken ct) =>
        Ledger.Answer(this, await _ledger.VoidChargeAsync(_user.CurrentOrganizationId, id, body?.Reason, ct));
}

[ApiController]
[Route("api/payments")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.ManageMoney, WritesOnly = true)]
public class PaymentsController : ControllerBase
{
    private readonly IClubLedger _ledger;
    private readonly ICurrentUserService _user;
    public PaymentsController(IClubLedger ledger, ICurrentUserService user) { _ledger = ledger; _user = user; }

    /// <summary>Money received (or one member's), each with what it paid, and any void or refunds.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PaymentDto>>> List([FromQuery] Guid? memberId, CancellationToken ct) =>
        Ok(await _ledger.PaymentsAsync(_user.CurrentOrganizationId, memberId, ct));

    /// <summary>Records a payment of one charge (chargeId) or several (allocations). Never more than they owe.</summary>
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] RecordPaymentRequest request, CancellationToken ct) =>
        Ledger.Answer(this, await _ledger.RecordPaymentAsync(_user.CurrentOrganizationId, request, Ledger.Key(Request), ct));

    /// <summary>Voids a payment with a reason: its charges owe again what it paid.</summary>
    [HttpPost("{id:guid}/void")]
    [Requires(MemberPermissions.VoidMoney, What = "void a payment")]
    public async Task<ActionResult> Void(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReasonRequest? body, CancellationToken ct) =>
        Ledger.Answer(this, await _ledger.VoidPaymentAsync(_user.CurrentOrganizationId, id, body?.Reason, ct));

    /// <summary>Pays money back to the member, part or all of a payment, with a reason.</summary>
    [HttpPost("{id:guid}/refund")]
    [Requires(MemberPermissions.VoidMoney, What = "refund a payment")]
    public async Task<ActionResult> Refund(Guid id, [FromBody] RefundPaymentRequest request, CancellationToken ct) =>
        Ledger.Answer(this, await _ledger.RefundPaymentAsync(_user.CurrentOrganizationId, id, request, ct));
}

/// <summary>A member's house account: the balance at any date, and the entries for a period.</summary>
[ApiController]
[Route("api/members/{memberId:guid}")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
public class MemberAccountController : ControllerBase
{
    private readonly IClubLedger _ledger;
    private readonly ICurrentUserService _user;
    public MemberAccountController(IClubLedger ledger, ICurrentUserService user) { _ledger = ledger; _user = user; }

    /// <summary>The member's balance now, or at <paramref name="asOf"/>: negative when they owe the club.</summary>
    [HttpGet("balance")]
    public async Task<ActionResult> Balance(Guid memberId, [FromQuery] DateTime? asOf, CancellationToken ct) =>
        Ledger.Answer(this, await _ledger.BalanceAsync(_user.CurrentOrganizationId, memberId, asOf?.ToUniversalTime(), ct));

    /// <summary>The opening balance, each entry from <paramref name="from"/> to <paramref name="to"/> with the balance after it, and the closing balance.</summary>
    [HttpGet("statement")]
    public async Task<ActionResult> Statement(Guid memberId, [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken ct) =>
        Ledger.Answer(this, await _ledger.StatementAsync(_user.CurrentOrganizationId, memberId, from.ToUniversalTime(), to.ToUniversalTime(), ct));
}

[ApiController]
[Route("api/gift-cards")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
public class GiftCardsController : ControllerBase
{
    private readonly IClubLedger _ledger;
    private readonly ICurrentUserService _user;
    public GiftCardsController(IClubLedger ledger, ICurrentUserService user) { _ledger = ledger; _user = user; }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GiftCardDto>>> List(CancellationToken ct) =>
        Ok(await _ledger.GiftCardsAsync(_user.CurrentOrganizationId, ct));

    [HttpGet("redemptions")]
    public async Task<ActionResult<IReadOnlyList<GiftCardRedemptionDto>>> Redemptions(CancellationToken ct) =>
        Ok(await _ledger.GiftCardRedemptionsAsync(_user.CurrentOrganizationId, ct));

    /// <summary>One card by its code, for a till handed one.</summary>
    [HttpGet("lookup")]
    public async Task<ActionResult> Lookup([FromQuery] string? code, CancellationToken ct) =>
        Ledger.Answer(this, await _ledger.FindGiftCardAsync(_user.CurrentOrganizationId, code ?? "", ct), c => c.Version);

    [HttpPost]
    [Requires(MemberPermissions.ManageMoney)]
    public async Task<ActionResult> Create([FromBody] IssueGiftCardRequest request, CancellationToken ct) =>
        Ledger.Answer(this, await _ledger.IssueGiftCardAsync(_user.CurrentOrganizationId, request, ct), c => c.Version);

    /// <summary>Spends from the card. A retry with the same Idempotency-Key gets the first redemption back.</summary>
    [HttpPost("{id:guid}/redeem")]
    [Requires(MemberPermissions.ManageMoney, MemberPermissions.FrontDesk, Any = true)]
    public async Task<ActionResult> Redeem(Guid id, [FromBody] RedeemGiftCardRequest request, CancellationToken ct) =>
        Ledger.Answer(this, await _ledger.RedeemGiftCardAsync(_user.CurrentOrganizationId, id, request, Ledger.Key(Request), ct));

    /// <summary>Adds value to the card.</summary>
    [HttpPost("{id:guid}/top-up")]
    [Requires(MemberPermissions.ManageMoney)]
    public async Task<ActionResult> TopUp(Guid id, [FromBody] TopUpGiftCardRequest request, CancellationToken ct) =>
        Ledger.Answer(this, await _ledger.TopUpGiftCardAsync(_user.CurrentOrganizationId, id, request, Ledger.Key(Request), ct), c => c.Version);

    // Voiding takes the money permission as well as the void one, as it does for charges
    // and payments (their controllers ask for money.manage on every change).
    [HttpPost("{id:guid}/void")]
    [Requires(MemberPermissions.ManageMoney)]
    [Requires(MemberPermissions.VoidMoney, What = "void a gift card")]
    public async Task<ActionResult> Void(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReasonRequest? body, CancellationToken ct)
    {
        var (version, versionError) = Request.ExpectedVersion(body?.Version);
        if (versionError != null) return versionError.ToProblemResult();
        return Ledger.Answer(this, await _ledger.VoidGiftCardAsync(_user.CurrentOrganizationId, id, body?.Reason, version, ct), c => c.Version);
    }
}

[ApiController]
[Route("api/tabs")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.FrontDesk, WritesOnly = true)]
public class TabsController : ControllerBase
{
    private readonly IClubLedger _ledger;
    private readonly ICurrentUserService _user;
    public TabsController(IClubLedger ledger, ICurrentUserService user) { _ledger = ledger; _user = user; }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TabDto>>> List(CancellationToken ct) =>
        Ok(await _ledger.TabsAsync(_user.CurrentOrganizationId, ct));

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] OpenTabRequest request, CancellationToken ct) =>
        Ledger.Answer(this, await _ledger.OpenTabAsync(_user.CurrentOrganizationId, request, ct), t => t.Version);

    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult> AddItem(Guid id, [FromBody] AddTabItemRequest request, CancellationToken ct)
    {
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();
        return Ledger.Answer(this, await _ledger.AddTabItemAsync(_user.CurrentOrganizationId, id, request, version, ct), t => t.Version);
    }

    [HttpDelete("{tabId:guid}/items/{itemId:guid}")]
    public async Task<ActionResult> RemoveItem(Guid tabId, Guid itemId, CancellationToken ct)
    {
        var (version, versionError) = Request.ExpectedVersion(null);
        if (versionError != null) return versionError.ToProblemResult();
        return Ledger.Answer(this, await _ledger.RemoveTabItemAsync(_user.CurrentOrganizationId, tabId, itemId, version, ct), t => t.Version);
    }

    /// <summary>Closes the tab: one charge to the member's account and the points it earns. Closing a closed tab posts nothing more.</summary>
    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult> Close(Guid id, CancellationToken ct)
    {
        var (version, versionError) = Request.ExpectedVersion(null);
        if (versionError != null) return versionError.ToProblemResult();
        return Ledger.Answer(this, await _ledger.CloseTabAsync(_user.CurrentOrganizationId, id, version, ct), t => t.Version);
    }

    [HttpPost("{id:guid}/void")]
    public async Task<ActionResult> Void(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReasonRequest? body, CancellationToken ct)
    {
        var (version, versionError) = Request.ExpectedVersion(body?.Version);
        if (versionError != null) return versionError.ToProblemResult();
        return Ledger.Answer(this, await _ledger.VoidTabAsync(_user.CurrentOrganizationId, id, body?.Reason, version, ct), t => t.Version);
    }
}

[ApiController]
[Route("api/subscriptions")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.ManageMoney, WritesOnly = true)]
public class SubscriptionsController : ControllerBase
{
    private readonly IClubLedger _ledger;
    private readonly ICurrentUserService _user;
    public SubscriptionsController(IClubLedger ledger, ICurrentUserService user) { _ledger = ledger; _user = user; }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SubscriptionDto>>> List(CancellationToken ct) =>
        Ok(await _ledger.SubscriptionsAsync(_user.CurrentOrganizationId, ct));

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] SubscriptionWriteRequest request, CancellationToken ct) =>
        Ledger.Answer(this, await _ledger.CreateSubscriptionAsync(_user.CurrentOrganizationId, request, ct), s => s.Version);

    [HttpPut("{id:guid}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] SubscriptionWriteRequest request, CancellationToken ct)
    {
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();
        return Ledger.Answer(this, await _ledger.UpdateSubscriptionAsync(_user.CurrentOrganizationId, id, request, version, ct), s => s.Version);
    }

    /// <summary>Deletes a subscription that never billed; one that did is cancelled instead (409).</summary>
    [HttpDelete("{id:guid}")]
    [Requires(MemberPermissions.DeleteRecords)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct) =>
        (await _ledger.DeleteSubscriptionAsync(_user.CurrentOrganizationId, id, ct)).ToNoContentResult();

    /// <summary>Bills the subscription's next period: one dues charge, once per period.</summary>
    [HttpPost("{id:guid}/bill")]
    public async Task<ActionResult> Bill(Guid id, CancellationToken ct) =>
        Ledger.Answer(this, await _ledger.BillSubscriptionAsync(_user.CurrentOrganizationId, id, ct), s => s.Version);
}

[ApiController]
[Route("api/donations")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.ManageMoney, WritesOnly = true)]
public class DonationsController : ControllerBase
{
    private readonly IClubLedger _ledger;
    private readonly ICurrentUserService _user;
    public DonationsController(IClubLedger ledger, ICurrentUserService user) { _ledger = ledger; _user = user; }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DonationDto>>> List(CancellationToken ct) =>
        Ok(await _ledger.DonationsAsync(_user.CurrentOrganizationId, ct));

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] RecordDonationRequest request, CancellationToken ct) =>
        Ledger.Answer(this, await _ledger.RecordDonationAsync(_user.CurrentOrganizationId, request, ct), d => d.Version);

    [HttpPost("{id:guid}/acknowledge")]
    public async Task<ActionResult> Acknowledge(Guid id, CancellationToken ct)
    {
        var (version, versionError) = Request.ExpectedVersion(null);
        if (versionError != null) return versionError.ToProblemResult();
        return Ledger.Answer(this, await _ledger.AcknowledgeDonationAsync(_user.CurrentOrganizationId, id, version, ct), d => d.Version);
    }

    /// <summary>Voids a donation with a reason: an entry that takes it back, and the donation stays.</summary>
    [HttpPost("{id:guid}/void")]
    [Requires(MemberPermissions.VoidMoney, What = "void a donation")]
    public async Task<ActionResult> Void(Guid id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ReasonRequest? body, CancellationToken ct) =>
        Ledger.Answer(this, await _ledger.VoidDonationAsync(_user.CurrentOrganizationId, id, body?.Reason, ct), d => d.Version);

    // Donations used to be deleted, and a gift vanished from the giving totals with no trace.
    [HttpDelete("{id:guid}")]
    public ActionResult Delete(Guid id) => DomainErrors.Club.DonationCannotDelete.ToProblemResult();
}
