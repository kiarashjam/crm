namespace ACI.Domain.Entities;

// The club's money, in typed tables where entries are only ever added.
//
// A member's house account is two kinds of entry: what they owe (ClubCharge) and what was
// paid or credited to them (ClubPayment). A posted entry is never changed or deleted. A
// void or a refund is an entry of its own that reverses the original, saying why and who,
// and an opening balance or a balance corrected by hand is an entry too. So the balance at
// any date is the sum of the entries up to that date, and a statement already sent never
// changes. Gift cards keep their own small ledger (ClubGiftCardTransaction), as their
// value is the club's debt to whoever holds the card, not a member's account.
//
// Only what can still change carries a Version: an open tab, a subscription's settings,
// a gift card's status, a donation's thank-you, and the house account row that every
// entry for a member touches, which is what makes two payments of one charge at the same
// moment one payment and a 409. See ClubLedgerService for the rules.

/// <summary>What an entry on a house account, or a donation, is.</summary>
public static class ClubEntryTypes
{
    /// <summary>Something the member bought or owes. ClubCharge only.</summary>
    public const string Charge = "Charge";

    /// <summary>Money received from the member. ClubPayment only.</summary>
    public const string Payment = "Payment";

    /// <summary>A balance brought over from before the ledger: owed (a charge) or in credit (a payment).</summary>
    public const string Opening = "Opening";

    /// <summary>A balance corrected by hand: more owed (a charge) or a credit (a payment).</summary>
    public const string Adjustment = "Adjustment";

    /// <summary>Takes back the whole of an entry: a charge posted in error, a cheque that bounced, a donation entered twice.</summary>
    public const string Void = "Void";

    /// <summary>Money paid back to the member: part or all of a payment. ClubPayment only.</summary>
    public const string Refund = "Refund";

    /// <summary>A gift to one of the club's causes. ClubDonation only.</summary>
    public const string Donation = "Donation";
}

/// <summary>What a gift card transaction is.</summary>
public static class GiftCardTransactionTypes
{
    public const string Issue = "Issue";
    public const string Redeem = "Redeem";
    public const string TopUp = "TopUp";

    /// <summary>What was left when the card expired, written off. Dated at the expiry.</summary>
    public const string Expiry = "Expiry";

    /// <summary>What was left when the card was voided, written off.</summary>
    public const string Void = "Void";

    /// <summary>
    /// Only from the move of the old gift card records: the difference between what those
    /// said was left on a card and what their issue and redemptions add up to.
    /// </summary>
    public const string Adjustment = "Adjustment";
}

/// <summary>
/// A member's house account: the one row per member that every entry on their account
/// touches, so entries for one member are written one at a time.
/// </summary>
/// <remarks>
/// It holds no money of its own. The balance is always worked out from the entries;
/// ClubMember.HouseAccountBalance keeps the latest figure for the lists that show it.
/// </remarks>
public class ClubHouseAccount : Common.BaseEntity, Common.IVersioned
{
    public Guid? OrganizationId { get; set; }
    public Guid MemberId { get; set; }

    /// <summary>The currency the account is kept in: the workspace's when it was opened.</summary>
    public string Currency { get; set; } = "CHF";

    public DateTime OpenedAtUtc { get; set; }
    public DateTime? LastEntryAtUtc { get; set; }
    public Guid Version { get; set; }
}

/// <summary>An amount a member owes, or (as a <see cref="ClubEntryTypes.Void"/>) one taken back.</summary>
public class ClubCharge : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid MemberId { get; set; }

    /// <summary>The member's name when it was posted, so the line still reads after a rename or a merge.</summary>
    public string MemberName { get; set; } = string.Empty;

    /// <summary>Charge, Opening, Adjustment or Void (see <see cref="ClubEntryTypes"/>).</summary>
    public string EntryType { get; set; } = ClubEntryTypes.Charge;

    /// <summary>Dues, Dining, Bar, Event, Spa, Booking, Retail or Other.</summary>
    public string Kind { get; set; } = "Other";

    public string Description { get; set; } = string.Empty;

    /// <summary>What it adds to what the member owes: negative for a void.</summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "CHF";

    /// <summary>When it counts on the account: what a balance or a statement at a date goes by.</summary>
    public DateTime PostedAtUtc { get; set; }

    public DateTime? DueAtUtc { get; set; }
    public string? Reference { get; set; }

    /// <summary>For a void: the charge it takes back.</summary>
    public Guid? ReversesChargeId { get; set; }

    /// <summary>Why it was voided, or why the balance was corrected.</summary>
    public string? Reason { get; set; }

    /// <summary>The tab whose close posted it.</summary>
    public Guid? TabId { get; set; }

    /// <summary>The subscription that billed it, and the period it bills.</summary>
    public Guid? SubscriptionId { get; set; }
    public DateTime? BillingPeriodStartUtc { get; set; }

    /// <summary>The catalogue item it was charged from, when it was: its name, price and VAT rate were copied.</summary>
    public Guid? CatalogItemId { get; set; }

    /// <summary>The VAT rate in percent included in the amount, when it is known (a charge from the catalogue); none otherwise.</summary>
    public decimal? VatRate { get; set; }

    /// <summary>From the request's Idempotency-Key: a retry with the same key gets this charge back.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// The member who ran it up, when that is not the member whose account it is on: a
    /// household member whose bills roll up to the household's primary. <see cref="MemberId"/>
    /// is always the account it is billed to, so balances, statements, reminders and overdue
    /// dues follow the bill; this keeps who incurred it, for the line to say so.
    /// </summary>
    public Guid? IncurredByMemberId { get; set; }

    /// <summary>Their name when it was posted, so the line still reads after a rename or a merge.</summary>
    public string? IncurredByName { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
}

/// <summary>
/// Money received or credited to a member's account, or (as a void or a refund) money
/// taken back.
/// </summary>
public class ClubPayment : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;

    /// <summary>Payment, Opening, Adjustment, Void or Refund (see <see cref="ClubEntryTypes"/>).</summary>
    public string EntryType { get; set; } = ClubEntryTypes.Payment;

    /// <summary>How the money came or went back: CardOnFile, Cash, Check, Wire or AppliedCredit. None for a credit.</summary>
    public string? Method { get; set; }

    /// <summary>What it takes off what the member owes: negative for a void or a refund.</summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "CHF";

    /// <summary>When it counts on the account.</summary>
    public DateTime ReceivedAtUtc { get; set; }

    /// <summary>What the entry is, in words: "Opening balance", "House balance changed by hand from -100.00 to 50.00".</summary>
    public string? Description { get; set; }

    public string? Reference { get; set; }
    public string? Notes { get; set; }

    /// <summary>For a void or a refund: the payment it takes back.</summary>
    public Guid? ReversesPaymentId { get; set; }

    public string? Reason { get; set; }
    public string? IdempotencyKey { get; set; }

    /// <summary>
    /// Where the money came from outside the club, when it did: stripe or bank (see
    /// PaymentProviders). None for a payment taken at the desk or entered by hand.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>
    /// The provider's id for it: Stripe's payment intent (pi_…) or refund (re_…), or the
    /// bank's reference for the transfer.
    /// </summary>
    public string? ProviderPaymentId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
}

/// <summary>
/// How much of a payment (or a credit) went to a charge. A void or refund that gives the
/// money back adds a negative allocation, so a charge's paid amount is always the sum of
/// its allocations.
/// </summary>
public class ClubPaymentAllocation : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid PaymentId { get; set; }
    public Guid ChargeId { get; set; }

    /// <summary>Positive when the payment pays the charge; negative when a void or refund takes that back.</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// The void or refund (a ClubPayment) that took it back; none for an allocation made when
    /// the payment was, and none for one that gave a cancelled charge's money back as credit
    /// (IClubLedger.CancelChargeAsync), where the payment itself stands.
    /// </summary>
    public Guid? ReversalId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

/// <summary>Dues or another amount billed to a member every month, quarter, half year or year.</summary>
public class ClubSubscription : Common.BaseEntity, Common.IVersioned
{
    public Guid? OrganizationId { get; set; }
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CHF";

    /// <summary>Monthly, Quarterly, SemiAnnual or Annual.</summary>
    public string Cadence { get; set; } = "Monthly";

    /// <summary>Active, Paused, Cancelled or PastDue.</summary>
    public string Status { get; set; } = "Active";

    public DateTime StartedAtUtc { get; set; }

    /// <summary>The start of the next period to bill. Each period runs to the same day of the month a cadence later.</summary>
    public DateTime? NextChargeAtUtc { get; set; }

    public DateTime? LastChargedAtUtc { get; set; }

    /// <summary>
    /// The day of the month its periods start on, kept so a subscription that starts on the
    /// 31st bills on the 30th in a 30-day month and on the 31st again after. None for one
    /// from before calendar billing: its next date's day is taken when it is next billed.
    /// </summary>
    public int? BillingAnchorDay { get; set; }

    /// <summary>The catalogue item (a tier or dues) it was started from, when it was; its name, price and cadence were copied.</summary>
    public Guid? CatalogItemId { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid Version { get; set; }
}

/// <summary>A gift card. What is left on it is the sum of its transactions.</summary>
public class ClubGiftCard : Common.BaseEntity, Common.IVersioned
{
    public Guid? OrganizationId { get; set; }

    /// <summary>The server's, unique in the workspace.</summary>
    public string Code { get; set; } = string.Empty;

    public decimal FaceValue { get; set; }
    public string Currency { get; set; } = "CHF";
    public Guid? PurchaserMemberId { get; set; }
    public string PurchaserName { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string? RecipientEmail { get; set; }
    public string? Message { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }

    /// <summary>Active or Voided. Redeemed and Expired are worked out from the transactions and the clock.</summary>
    public string Status { get; set; } = "Active";

    /// <summary>Moved by every transaction, so two terminals spending the card at once cannot both save.</summary>
    public DateTime? LastTransactionAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public Guid Version { get; set; }
}

/// <summary>A movement of a gift card's value: positive adds to what is left on it, negative takes from it.</summary>
public class ClubGiftCardTransaction : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid GiftCardId { get; set; }

    /// <summary>Issue, Redeem, TopUp, Expiry, Void or Adjustment (see <see cref="GiftCardTransactionTypes"/>).</summary>
    public string Type { get; set; } = GiftCardTransactionTypes.Redeem;

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CHF";

    /// <summary>When it counts: what the card's value at a date goes by.</summary>
    public DateTime AtUtc { get; set; }

    public string? Reason { get; set; }

    /// <summary>For a redemption: what it paid for, in the words of whoever took it.</summary>
    public string? AppliedTo { get; set; }

    public string? RedeemedByName { get; set; }
    public Guid? MemberId { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
}

/// <summary>A bar, dining or spa tab: items added while it is open, posted to the house account when it closes.</summary>
public class ClubTab : Common.BaseEntity, Common.IVersioned
{
    public Guid? OrganizationId { get; set; }
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;

    /// <summary>The member's tier when the tab was opened: it sets the points the close earns.</summary>
    public string MemberTier { get; set; } = string.Empty;

    /// <summary>Bar, Dining, Spa, Rooftop or PoolDeck.</summary>
    public string Venue { get; set; } = string.Empty;

    public string? ServerName { get; set; }
    public string? Notes { get; set; }

    /// <summary>Open, Closed or Voided.</summary>
    public string Status { get; set; } = "Open";

    public DateTime OpenedAtUtc { get; set; }

    /// <summary>What the items still on it come to.</summary>
    public decimal Total { get; set; }

    public string Currency { get; set; } = "CHF";
    public DateTime? ClosedAtUtc { get; set; }
    public Guid? ClosedByUserId { get; set; }
    public string? ClosedByName { get; set; }

    /// <summary>The charge the close posted, and the points it earned.</summary>
    public Guid? ChargeId { get; set; }
    public Guid? LoyaltyEntryId { get; set; }
    public int? PointsAwarded { get; set; }

    public DateTime? VoidedAtUtc { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public string? VoidedByName { get; set; }
    public string? VoidReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public Guid Version { get; set; }
}

/// <summary>An item on a tab. One taken off an open tab stays, marked removed, saying who took it off.</summary>
public class ClubTabItem : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid TabId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }

    /// <summary>The menu item it was rung up from, when it was: its name, price and VAT rate were copied.</summary>
    public Guid? CatalogItemId { get; set; }

    /// <summary>The VAT rate in percent included in its price, when it came from the catalogue.</summary>
    public decimal? VatRate { get; set; }

    public DateTime AddedAtUtc { get; set; }
    public Guid? AddedByUserId { get; set; }
    public string? AddedByName { get; set; }
    public DateTime? RemovedAtUtc { get; set; }
    public Guid? RemovedByUserId { get; set; }
    public string? RemovedByName { get; set; }
}

/// <summary>A gift to one of the club's causes, or (as a void) one taken back.</summary>
public class ClubDonation : Common.BaseEntity, Common.IVersioned
{
    public Guid? OrganizationId { get; set; }
    public Guid? MemberId { get; set; }
    public string? MemberName { get; set; }

    /// <summary>Donation or Void (see <see cref="ClubEntryTypes"/>).</summary>
    public string EntryType { get; set; } = ClubEntryTypes.Donation;

    public string Cause { get; set; } = "Other";

    /// <summary>Negative for a void.</summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "CHF";
    public DateTime ReceivedAtUtc { get; set; }
    public bool IsAnonymous { get; set; }
    public string? Message { get; set; }

    /// <summary>Whether the club has said thank you: the one thing about a donation that changes.</summary>
    public bool Acknowledged { get; set; }
    public DateTime? AcknowledgedAtUtc { get; set; }
    public string? AcknowledgedByName { get; set; }

    /// <summary>For a void: the donation it takes back.</summary>
    public Guid? ReversesDonationId { get; set; }

    public string? Reason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public Guid Version { get; set; }
}
