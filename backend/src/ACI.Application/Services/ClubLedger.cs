using System.Text.Json.Serialization;
using ACI.Application.Common;

namespace ACI.Application.Services;

/// <summary>
/// The club's money ledger: members' house accounts (charges, payments, credits, voids and
/// refunds), gift cards, tabs, subscriptions and donations, in typed tables where entries
/// are only ever added.
/// </summary>
/// <remarks>
/// <para>
/// How to build on it. Billing runs, statements as PDF, payment providers, POS
/// integrations, event tickets and club reports all post and read money through this
/// interface, never through the tables or the old ClubDocuments.
/// </para>
/// <list type="bullet">
/// <item><b>Post a charge</b>: <see cref="PostChargeAsync"/>. Event tickets, POS tickets and
/// billing runs post one each; send an Idempotency-Key (any string unique to the thing being
/// charged, such as "ticket:{registrationId}") and a retry gets the first charge back.</item>
/// <item><b>Record a payment with allocations</b>: <see cref="RecordPaymentAsync"/>. A payment
/// pays one or more of the member's charges and never more than they owe (an overpayment is
/// refused, not lost). A payment provider's webhook records one with its own reference and
/// the provider's id as the Idempotency-Key. Method AppliedCredit pays a charge from credit
/// the member already holds, adding no new money.</item>
/// <item><b>Void and refund</b>: <see cref="VoidChargeAsync"/>, <see cref="VoidPaymentAsync"/>,
/// <see cref="RefundPaymentAsync"/>. Each adds an entry that reverses the original, with a
/// reason and who did it; nothing is edited or deleted. A charge with payments standing
/// against it is voided after them.</item>
/// <item><b>Cancel what will not happen</b>: <see cref="CancelChargeAsync"/> takes back the charge
/// for a cancelled event ticket or a booking's deposit given back: a void entry as above, and
/// whatever was paid against it is house credit again, so a cancellation never waits for a
/// refund. Paying that credit out stays a refund, by someone allowed to.</item>
/// <item><b>Correct a balance</b>: <see cref="AdjustBalanceAsync"/>, an entry of its own.</item>
/// <item><b>Read a balance</b>: <see cref="BalanceAsync"/> at any date, and
/// <see cref="BalancesAsync"/> for every member at once. A balance is credits less what is
/// owed: negative when the member owes the club.</item>
/// <item><b>Entries for a member and period</b>: <see cref="StatementAsync"/>, the opening
/// balance, each entry with the balance after it, and the closing balance. A statement PDF
/// or a reminder email is built on it.</item>
/// <item><b>Gift cards</b>: issue, redeem, top up, void and expire, each a transaction on the
/// card; <see cref="GiftCardBalanceAsync"/> at any date.</item>
/// <item><b>Tabs</b>: open, add and remove items, close (one charge and the points, once) and void.</item>
/// <item><b>Subscriptions</b>: <see cref="BillSubscriptionAsync(Guid?, Guid, bool, CancellationToken)"/>
/// bills a period once (a second bill of the same period is refused by the database), and
/// <see cref="BillDueAsync"/> bills every period that has started, which the daily billing
/// run leans on. Periods are calendar months, quarters, half years and years.</item>
/// <item><b>Prices from the catalogue</b>: a charge, a tab item or a subscription may name a
/// catalogue item (<see cref="IClubCatalog"/>); the price, the name and the VAT rate are then
/// the catalogue's, copied when it is made.</item>
/// </list>
/// <para>
/// Every write answers 409 (a code ending ".Conflict") when the record moved on under it:
/// voided or closed already, or changed by someone else at the same moment. Records that
/// can still change (a tab, a subscription, a gift card, a donation) take the version they
/// were read at, from the body or If-Match, and refuse a save from an older one.
/// </para>
/// </remarks>
public interface IClubLedger
{
    // ── house accounts ────────────────────────────────────────────────────────
    Task<IReadOnlyList<ChargeDto>> ChargesAsync(Guid? organizationId, Guid? memberId = null, CancellationToken ct = default);
    Task<IReadOnlyList<PaymentDto>> PaymentsAsync(Guid? organizationId, Guid? memberId = null, CancellationToken ct = default);
    Task<Result<ChargeDto>> PostChargeAsync(Guid? organizationId, PostChargeRequest request, string? idempotencyKey = null, CancellationToken ct = default);
    Task<Result<PaymentDto>> RecordPaymentAsync(Guid? organizationId, RecordPaymentRequest request, string? idempotencyKey = null, CancellationToken ct = default);
    Task<Result<ChargeDto>> VoidChargeAsync(Guid? organizationId, Guid chargeId, string? reason, CancellationToken ct = default);

    /// <summary>
    /// Takes back a charge for something that is not going to happen after all: the ticket of
    /// a registration cancelled, the deposit of a booking given back. A void entry, as
    /// <see cref="VoidChargeAsync"/> makes, except that money paid against the charge is not a
    /// reason to refuse: it becomes credit on the member's account again, to pay another
    /// charge (AppliedCredit) or to be refunded by someone allowed to. Nothing is paid out here.
    /// </summary>
    Task<Result<ChargeDto>> CancelChargeAsync(Guid? organizationId, Guid chargeId, string? reason, CancellationToken ct = default);
    Task<Result<PaymentDto>> VoidPaymentAsync(Guid? organizationId, Guid paymentId, string? reason, CancellationToken ct = default);
    Task<Result<PaymentDto>> RefundPaymentAsync(Guid? organizationId, Guid paymentId, RefundPaymentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Sets the member's balance to <paramref name="newBalance"/> by an entry for the
    /// difference: a credit when it rises, a charge when it falls. Nothing when it is the same.
    /// </summary>
    Task<Result<HouseBalanceDto>> AdjustBalanceAsync(Guid? organizationId, Guid memberId, decimal newBalance, string? reason, CancellationToken ct = default);

    /// <summary>The balance a member opens with: an entry dated now, owed when negative and a credit when positive.</summary>
    Task<Result> OpenWithBalanceAsync(Guid? organizationId, Guid memberId, decimal openingBalance, CancellationToken ct = default);

    Task<Result<HouseBalanceDto>> BalanceAsync(Guid? organizationId, Guid memberId, DateTime? asOfUtc = null, CancellationToken ct = default);

    /// <summary>Every member's balance at <paramref name="asOfUtc"/> (now when null), by member id. A member with no entries is left out.</summary>
    Task<IReadOnlyDictionary<Guid, decimal>> BalancesAsync(Guid? organizationId, DateTime? asOfUtc = null, CancellationToken ct = default);

    Task<Result<AccountStatementDto>> StatementAsync(Guid? organizationId, Guid memberId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    /// <summary>
    /// Whether the member still has money with the club: a balance either way, an open tab, or
    /// a subscription that is running, paused or past due.
    /// </summary>
    Task<bool> HasOpenAccountAsync(Guid? organizationId, Guid memberId, CancellationToken ct = default);

    // ── gift cards ────────────────────────────────────────────────────────────
    Task<IReadOnlyList<GiftCardDto>> GiftCardsAsync(Guid? organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<GiftCardRedemptionDto>> GiftCardRedemptionsAsync(Guid? organizationId, CancellationToken ct = default);
    Task<Result<GiftCardDto>> FindGiftCardAsync(Guid? organizationId, string code, CancellationToken ct = default);
    Task<Result<GiftCardDto>> IssueGiftCardAsync(Guid? organizationId, IssueGiftCardRequest request, CancellationToken ct = default);
    Task<Result<GiftCardRedemptionDto>> RedeemGiftCardAsync(Guid? organizationId, Guid giftCardId, RedeemGiftCardRequest request, string? idempotencyKey = null, CancellationToken ct = default);
    Task<Result<GiftCardDto>> TopUpGiftCardAsync(Guid? organizationId, Guid giftCardId, TopUpGiftCardRequest request, string? idempotencyKey = null, CancellationToken ct = default);
    Task<Result<GiftCardDto>> VoidGiftCardAsync(Guid? organizationId, Guid giftCardId, string? reason, Guid? expectedVersion = null, CancellationToken ct = default);
    Task<Result<decimal>> GiftCardBalanceAsync(Guid? organizationId, Guid giftCardId, DateTime? asOfUtc = null, CancellationToken ct = default);

    /// <summary>
    /// Writes off what is left on every card whose expiry has passed, as an Expiry
    /// transaction dated at the expiry, once per card. Across every workspace: it is the
    /// expiry job's (GiftCardExpiryBackgroundService). Returns how many cards it expired.
    /// </summary>
    Task<int> ExpireGiftCardsAsync(DateTime asOfUtc, CancellationToken ct = default);

    // ── tabs ──────────────────────────────────────────────────────────────────
    Task<IReadOnlyList<TabDto>> TabsAsync(Guid? organizationId, CancellationToken ct = default);
    Task<Result<TabDto>> OpenTabAsync(Guid? organizationId, OpenTabRequest request, CancellationToken ct = default);
    Task<Result<TabDto>> AddTabItemAsync(Guid? organizationId, Guid tabId, AddTabItemRequest request, Guid? expectedVersion = null, CancellationToken ct = default);
    Task<Result<TabDto>> RemoveTabItemAsync(Guid? organizationId, Guid tabId, Guid itemId, Guid? expectedVersion = null, CancellationToken ct = default);
    Task<Result<TabDto>> CloseTabAsync(Guid? organizationId, Guid tabId, Guid? expectedVersion = null, CancellationToken ct = default);
    Task<Result<TabDto>> VoidTabAsync(Guid? organizationId, Guid tabId, string? reason, Guid? expectedVersion = null, CancellationToken ct = default);

    // ── subscriptions ─────────────────────────────────────────────────────────
    Task<IReadOnlyList<SubscriptionDto>> SubscriptionsAsync(Guid? organizationId, CancellationToken ct = default);
    Task<Result<SubscriptionDto>> CreateSubscriptionAsync(Guid? organizationId, SubscriptionWriteRequest request, CancellationToken ct = default);
    Task<Result<SubscriptionDto>> UpdateSubscriptionAsync(Guid? organizationId, Guid subscriptionId, SubscriptionWriteRequest request, Guid? expectedVersion = null, CancellationToken ct = default);

    /// <summary>
    /// Posts the charge for the subscription's next period (its next billing date, or now
    /// when it has none) and moves the date on by its cadence. A period is billed once, and
    /// only once it has started: one that has not is refused (409).
    /// </summary>
    Task<Result<SubscriptionDto>> BillSubscriptionAsync(Guid? organizationId, Guid subscriptionId, CancellationToken ct = default);

    /// <summary>
    /// As <see cref="BillSubscriptionAsync(Guid?, Guid, CancellationToken)"/>; with
    /// <paramref name="early"/>, bills the next period before it starts, but never more than
    /// that one period ahead: a second early bill is refused (409).
    /// </summary>
    Task<Result<SubscriptionDto>> BillSubscriptionAsync(Guid? organizationId, Guid subscriptionId, bool early, CancellationToken ct = default);

    /// <summary>
    /// Bills every period of the subscription that has started by <paramref name="asOfUtc"/>,
    /// oldest first, one charge each, at most <paramref name="maxPeriods"/> of them; each is
    /// saved on its own. Returns the charges posted, which is none for a subscription that is
    /// paused, cancelled or not yet due. A period billed by someone else at the same moment is
    /// skipped, not billed twice.
    /// </summary>
    Task<Result<IReadOnlyList<ChargeDto>>> BillDueAsync(Guid? organizationId, Guid subscriptionId, DateTime asOfUtc, int maxPeriods, CancellationToken ct = default);

    /// <summary>Deletes a subscription that never billed anything; one that did is cancelled instead (409).</summary>
    Task<Result> DeleteSubscriptionAsync(Guid? organizationId, Guid subscriptionId, CancellationToken ct = default);

    // ── donations ─────────────────────────────────────────────────────────────
    Task<IReadOnlyList<DonationDto>> DonationsAsync(Guid? organizationId, CancellationToken ct = default);
    Task<Result<DonationDto>> RecordDonationAsync(Guid? organizationId, RecordDonationRequest request, CancellationToken ct = default);
    Task<Result<DonationDto>> AcknowledgeDonationAsync(Guid? organizationId, Guid donationId, Guid? expectedVersion = null, CancellationToken ct = default);
    Task<Result<DonationDto>> VoidDonationAsync(Guid? organizationId, Guid donationId, string? reason, CancellationToken ct = default);
}

/// <summary>
/// Called after the ledger has read a record it is about to change, before it writes.
/// Does nothing in the app; a test holds two requests here so both read before either
/// writes, which makes a race that is a matter of luck in production happen every time.
/// </summary>
/// <remarks>
/// The events and bookings services (IClubEvents, IClubReservations) report here too, once a
/// change to an event's list or a resource's bookings has read what it decides on.
/// </remarks>
public interface ILedgerReadObserver
{
    /// <param name="kind">"charges", "gift-cards", "tabs", "subscriptions", "events" or "reservations".</param>
    Task ReadAsync(string kind, CancellationToken ct = default);
}

/// <inheritdoc cref="ILedgerReadObserver"/>
public sealed class NoLedgerReadObserver : ILedgerReadObserver
{
    public Task ReadAsync(string kind, CancellationToken ct = default) => Task.CompletedTask;
}

// ── requests ──────────────────────────────────────────────────────────────────
// Dates arrive as text, as the screens send them: empty is "none", anything else must read
// as a date. A typed date would turn the empty string some screens send into a 400.

public sealed class PostChargeRequest
{
    public Guid? MemberId { get; set; }
    public string? Kind { get; set; }
    public string? Description { get; set; }
    public decimal? Amount { get; set; }
    public string? DueAtUtc { get; set; }
    public string? Reference { get; set; }

    /// <summary>
    /// A fee, product, ticket or menu item from the catalogue: the charge is then its price
    /// times <see cref="Quantity"/>, with its name, charge kind and VAT rate, and any amount
    /// or kind sent is not used.
    /// </summary>
    public Guid? CatalogItemId { get; set; }

    /// <summary>How many of the catalogue item, 1 when left out.</summary>
    public decimal? Quantity { get; set; }

    /// <summary>
    /// For a charge typed in by hand: the VAT rate in percent the amount includes, when the
    /// club wants it on the statement. A catalogue item's own rate is used instead of this.
    /// </summary>
    public decimal? VatRate { get; set; }
}

public sealed class PaymentAllocationRequest
{
    public Guid ChargeId { get; set; }
    public decimal Amount { get; set; }
}

public sealed class RecordPaymentRequest
{
    public decimal? Amount { get; set; }
    public string? Method { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string? ReceivedAtUtc { get; set; }

    /// <summary>The one charge the whole payment pays, as the screens send it.</summary>
    public Guid? ChargeId { get; set; }

    /// <summary>Or the charges it pays, and how much of it each takes: they add up to the amount.</summary>
    public List<PaymentAllocationRequest>? Allocations { get; set; }
}

public sealed class RefundPaymentRequest
{
    public decimal? Amount { get; set; }
    public string? Reason { get; set; }
    public string? Method { get; set; }
    public string? Reference { get; set; }
}

public sealed class ReasonRequest
{
    public string? Reason { get; set; }
    public Guid? Version { get; set; }
}

public sealed class IssueGiftCardRequest
{
    public Guid? PurchaserMemberId { get; set; }
    public string? PurchaserName { get; set; }
    public string? RecipientName { get; set; }
    public string? RecipientEmail { get; set; }
    public decimal? FaceValue { get; set; }
    public string? ExpiresAtUtc { get; set; }
    public string? Message { get; set; }
}

public sealed class RedeemGiftCardRequest
{
    public decimal? Amount { get; set; }
    public string? RedeemedByName { get; set; }
    public string? AppliedTo { get; set; }
}

public sealed class TopUpGiftCardRequest
{
    public decimal? Amount { get; set; }
    public string? Reason { get; set; }
}

public sealed class OpenTabRequest
{
    public Guid? MemberId { get; set; }
    public string? Venue { get; set; }
    public string? ServerName { get; set; }
    public string? Notes { get; set; }
}

public sealed class AddTabItemRequest
{
    public string? Name { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }

    /// <summary>A menu item or product from the catalogue: its name, price and VAT rate are used, not what was typed.</summary>
    public Guid? CatalogItemId { get; set; }

    public Guid? Version { get; set; }
}

/// <summary>A subscription to add, or the fields of one to change: a field left out is kept.</summary>
public sealed class SubscriptionWriteRequest
{
    public Guid? MemberId { get; set; }
    public string? Name { get; set; }
    public decimal? Amount { get; set; }
    public string? Cadence { get; set; }
    public string? Status { get; set; }
    public string? Notes { get; set; }
    public string? NextChargeAtUtc { get; set; }

    /// <summary>
    /// On adding one: a tier or dues item from the catalogue, whose name, price and cadence
    /// it starts with unless they are sent too.
    /// </summary>
    public Guid? CatalogItemId { get; set; }

    public Guid? Version { get; set; }
}

/// <summary>Billing a subscription by hand.</summary>
public sealed class BillSubscriptionRequest
{
    /// <summary>Bill the next period before it starts (one period ahead at most).</summary>
    public bool Early { get; set; }
}

public sealed class RecordDonationRequest
{
    public Guid? MemberId { get; set; }
    public string? Cause { get; set; }
    public decimal? Amount { get; set; }
    public string? ReceivedAtUtc { get; set; }
    public bool? IsAnonymous { get; set; }
    public string? Message { get; set; }
}

// ── answers ───────────────────────────────────────────────────────────────────
// The shapes the club screens have always read (src/app/api/charges.ts and the rest), now
// worked out from the ledger. What a record does not have is left out rather than sent as
// null, as the documents left it out: the screens test for a field being there.

public sealed record ChargeDto
{
    public Guid Id { get; init; }
    public Guid MemberId { get; init; }
    public string MemberName { get; init; } = string.Empty;

    /// <summary>Charge, Opening or Adjustment: an opening balance owed is paid like any charge.</summary>
    public string EntryType { get; init; } = string.Empty;

    public string Kind { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal PaidAmount { get; init; }

    /// <summary>Pending, PartiallyPaid, Paid, Overdue or Voided, worked out from its payments, voids and due date.</summary>
    public string Status { get; init; } = string.Empty;

    public DateTime PostedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? DueAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? PaidAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Reference { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? TabId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? SubscriptionId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? BillingPeriodStartUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? CatalogItemId { get; init; }

    /// <summary>The VAT rate in percent included in the amount, when known.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? VatRate { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Reason { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? VoidedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? VoidedByUserId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? VoidedByName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? VoidReason { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CreatedByName { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed record PaymentAllocationDto(Guid ChargeId, decimal Amount);

public sealed record PaymentRefundDto(Guid Id, decimal Amount, string? Reason, string? Method, DateTime AtUtc, string? ByName);

public sealed record PaymentDto
{
    public Guid Id { get; init; }
    public Guid MemberId { get; init; }
    public string MemberName { get; init; } = string.Empty;

    /// <summary>Payment; or, for a house credit applied to a charge, CreditApplied.</summary>
    public string EntryType { get; init; } = string.Empty;

    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Method { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Reference { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Notes { get; init; }
    public DateTime ReceivedAtUtc { get; init; }

    /// <summary>The first charge it paid, as the screens read it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? ChargeId { get; init; }

    public IReadOnlyList<Guid> AppliedToChargeIds { get; init; } = Array.Empty<Guid>();

    /// <summary>What it pays each charge now, voids and refunds taken off.</summary>
    public IReadOnlyList<PaymentAllocationDto> Allocations { get; init; } = Array.Empty<PaymentAllocationDto>();

    /// <summary>Voided, Refunded or PartiallyRefunded; left out while it stands in full.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Status { get; init; }

    public decimal RefundedAmount { get; init; }
    public IReadOnlyList<PaymentRefundDto> Refunds { get; init; } = Array.Empty<PaymentRefundDto>();
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? VoidedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? VoidedByUserId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? VoidedByName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? VoidReason { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CreatedByName { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

/// <summary>A member's house balance: negative when they owe the club.</summary>
/// <remarks>
/// <see cref="Balance"/> is the one at <see cref="AsOfUtc"/>. <see cref="Owed"/>,
/// <see cref="Credit"/> and <see cref="OpenCharges"/> are what the account holds now (for a
/// past date, the charges posted by then, as paid today): a statement or a reminder for an
/// earlier date works from <see cref="IClubLedger.StatementAsync"/> instead.
/// </remarks>
public sealed record HouseBalanceDto(
    Guid MemberId, string MemberName, string Currency, decimal Balance, DateTime AsOfUtc,
    decimal Owed, decimal Credit, int OpenCharges);

/// <summary>One entry on a statement, with the balance after it.</summary>
public sealed record StatementLineDto
{
    public Guid Id { get; init; }
    public DateTime AtUtc { get; init; }

    /// <summary>Charge, Payment, Opening, Adjustment, Void or Refund.</summary>
    public string EntryType { get; init; } = string.Empty;

    /// <summary>"charge" for an entry of what is owed, "payment" for a payment or credit.</summary>
    public string Side { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    /// <summary>What it adds to what is owed (never negative).</summary>
    public decimal Debit { get; init; }

    /// <summary>What it takes off what is owed (never negative).</summary>
    public decimal Credit { get; init; }

    public decimal Balance { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Kind { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Method { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Reference { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Reason { get; init; }

    /// <summary>For a void or a refund: the entry it takes back.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? ReversesId { get; init; }

    /// <summary>For a charge (or its void) whose VAT rate is known: the percent included in it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? VatRate { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CreatedByName { get; init; }
}

/// <summary>A member's entries from <see cref="FromUtc"/> (included) to <see cref="ToUtc"/> (not included).</summary>
public sealed record AccountStatementDto(
    Guid MemberId, string MemberName, string Currency, DateTime FromUtc, DateTime ToUtc,
    decimal OpeningBalance, decimal ClosingBalance, decimal TotalDebits, decimal TotalCredits,
    IReadOnlyList<StatementLineDto> Lines);

public sealed record GiftCardDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? PurchaserMemberId { get; init; }
    public string PurchaserName { get; init; } = string.Empty;
    public string RecipientName { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? RecipientEmail { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Message { get; init; }
    public decimal FaceValue { get; init; }

    /// <summary>What is left on it: the sum of its transactions.</summary>
    public decimal RemainingValue { get; init; }

    public decimal ToppedUpValue { get; init; }
    public string Currency { get; init; } = string.Empty;

    /// <summary>Active, Redeemed, Expired or Voided.</summary>
    public string Status { get; init; } = string.Empty;

    public DateTime IssuedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? ExpiresAtUtc { get; init; }

    /// <summary>What was left when it was voided, written off.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? WrittenOffValue { get; init; }

    /// <summary>What was left when it expired, written off.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? ExpiredValue { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? VoidReason { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? VoidedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? VoidedByUserId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? VoidedByName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CreatedByName { get; init; }
    public Guid Version { get; init; }
}

public sealed record GiftCardRedemptionDto
{
    public Guid Id { get; init; }
    public Guid GiftCardId { get; init; }
    public string GiftCardCode { get; init; } = string.Empty;

    /// <summary>What was spent: positive.</summary>
    public decimal Amount { get; init; }

    public string RedeemedByName { get; init; } = string.Empty;
    public DateTime RedeemedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? AppliedTo { get; init; }
}

public sealed record TabItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? CatalogItemId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? VatRate { get; init; }
    public DateTime AddedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? AddedByUserId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? AddedByName { get; init; }
}

public sealed record TabDto
{
    public Guid Id { get; init; }
    public Guid MemberId { get; init; }
    public string MemberName { get; init; } = string.Empty;
    public string MemberTier { get; init; } = string.Empty;
    public string Venue { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ServerName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Notes { get; init; }

    /// <summary>The items still on it.</summary>
    public IReadOnlyList<TabItemDto> Items { get; init; } = Array.Empty<TabItemDto>();

    public string Status { get; init; } = string.Empty;
    public DateTime OpenedAtUtc { get; init; }
    public decimal Total { get; init; }
    public string Currency { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? ClosedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? ClosedByUserId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ClosedByName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? ChargeId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? LoyaltyEntryId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? PointsAwarded { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? VoidedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? VoidedByUserId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? VoidedByName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? VoidReason { get; init; }
    public Guid Version { get; init; }
}

public sealed record SubscriptionDto
{
    public Guid Id { get; init; }
    public Guid MemberId { get; init; }
    public string MemberName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Cadence { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime StartedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? NextChargeAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? LastChargedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? CatalogItemId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Notes { get; init; }
    public Guid Version { get; init; }
}

public sealed record DonationDto
{
    public Guid Id { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? MemberId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? MemberName { get; init; }
    public string Cause { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public DateTime ReceivedAtUtc { get; init; }
    public bool IsAnonymous { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Message { get; init; }
    public bool Acknowledged { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? AcknowledgedAtUtc { get; init; }

    /// <summary>Voided, or left out while it stands.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Status { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? VoidReason { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? VoidedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? VoidedByName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CreatedByName { get; init; }
    public Guid Version { get; init; }
}
