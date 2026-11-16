namespace ACI.Domain.Entities;

// What members buy at the club's bars, restaurants and shop, and how it reaches their
// house account: rung up at the club's own house-charge screen (a club without a POS), or
// imported from the POS the club runs (Lightspeed Restaurant K-Series), ticket by ticket.
//
// Neither posts money itself. A sale is posted through the ledger (IClubLedger) in one
// transaction: a charge for each VAT rate on it, a payment for what was paid at the till,
// and the points it earns. A ticket from the POS is imported once, keyed by the POS's own
// id for it, and posted as a sale once it is known whose it is; a ticket nobody's code is
// on waits for staff to say whose it is.

/// <summary>How a sale was settled at the till.</summary>
public static class SaleTenders
{
    /// <summary>Put on the member's house account, to be paid with their statement.</summary>
    public const string HouseAccount = "HouseAccount";

    public const string Card = "Card";
    public const string Cash = "Cash";
    public const string Twint = "Twint";

    /// <summary>For a POS ticket settled two ways: part on the house account, part paid at the POS.</summary>
    public const string Mixed = "Mixed";

    /// <summary>What a sale can be settled with, one per tender.</summary>
    public static readonly string[] All = [HouseAccount, Card, Cash, Twint];
}

/// <summary>Where a sale was rung up.</summary>
public static class SaleSources
{
    /// <summary>The club's own house-charge screen.</summary>
    public const string Desk = "desk";
}

/// <summary>The POS systems the club can import closed tickets from.</summary>
public static class PosProviders
{
    /// <summary>Lightspeed Restaurant K-Series (formerly iKentoo), read through its Financial API.</summary>
    public const string LightspeedK = "lightspeed-k";
}

/// <summary>Where a ticket imported from the POS stands.</summary>
public static class PosTicketStatuses
{
    /// <summary>On a member's house account: owed, or recorded as paid at the POS.</summary>
    public const string Posted = "posted";

    /// <summary>It needs a member (it was put on the house account, or names a customer) and none was found: staff say whose it is.</summary>
    public const string Unmatched = "unmatched";

    /// <summary>Its member is known, but it could not be posted: the reason is in the note, and it can be tried again.</summary>
    public const string Failed = "failed";

    /// <summary>Paid at the POS with nobody's name on it, or cancelled there: not a member's, so nothing to post.</summary>
    public const string Skipped = "skipped";

    /// <summary>Staff said it is not a member's, with why.</summary>
    public const string Ignored = "ignored";
}

/// <summary>What a member's code is.</summary>
public static class MemberCodeKinds
{
    /// <summary>A member number, or the code on a member's card or QR code, that the POS records on a ticket.</summary>
    public const string Card = "card";

    /// <summary>The POS's own id for a customer, remembered when staff matched one of their tickets to the member.</summary>
    public const string PosCustomer = "pos-customer";
}

/// <summary>
/// A sale: what a member bought at one go, line by line, how it was settled and what it
/// posted to the ledger. The record of the sale itself, kept beside the ledger's entries,
/// so the lines and the reason for a discount stay readable after they are posted.
/// </summary>
public class ClubSale : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }

    /// <summary>Who bought it, and earned its points.</summary>
    public Guid MemberId { get; set; }

    public string MemberName { get; set; } = string.Empty;

    /// <summary>
    /// The account its charges went on, when it is not the buyer's own: a household member's
    /// purchase goes on the primary's account when their bills roll up.
    /// </summary>
    public Guid? BilledMemberId { get; set; }

    /// <summary>desk, or the POS it came from (see <see cref="PosProviders"/>).</summary>
    public string Source { get; set; } = SaleSources.Desk;

    /// <summary>The ticket from the POS it was posted for, when it was.</summary>
    public Guid? PosTicketId { get; set; }

    /// <summary>Bar, Dining, Spa, Rooftop or PoolDeck: where it was sold, which sets the points' reason.</summary>
    public string Venue { get; set; } = string.Empty;

    /// <summary>HouseAccount, Card, Cash, Twint, or Mixed (see <see cref="SaleTenders"/>).</summary>
    public string Tender { get; set; } = SaleTenders.HouseAccount;

    /// <summary>What the lines came to before any discount, VAT included.</summary>
    public decimal Subtotal { get; set; }

    /// <summary>What was taken off, in all.</summary>
    public decimal Discount { get; set; }

    /// <summary>Why, in the words of whoever gave the discount. Required whenever there is one.</summary>
    public string? DiscountReason { get; set; }

    /// <summary>What the member was charged: the subtotal less the discount.</summary>
    public decimal Total { get; set; }

    /// <summary>Paid at the till (card, cash or TWINT), recorded as payments of its charges.</summary>
    public decimal PaidAmount { get; set; }

    /// <summary>Left owing on the house account.</summary>
    public decimal OwedAmount { get; set; }

    public string Currency { get; set; } = "CHF";

    public int PointsAwarded { get; set; }
    public Guid? LoyaltyEntryId { get; set; }

    /// <summary>The ledger charges it posted, one per VAT rate and kind, comma-separated.</summary>
    public string? ChargeIds { get; set; }

    /// <summary>The ledger payments it recorded for what was paid at the till, comma-separated.</summary>
    public string? PaymentIds { get; set; }

    /// <summary>The POS's receipt number, for a ticket from the POS.</summary>
    public string? Reference { get; set; }

    public string? StaffName { get; set; }
    public string? TableName { get; set; }
    public string? Notes { get; set; }

    /// <summary>From the request's Idempotency-Key, or the POS ticket: the same key posts the sale once.</summary>
    public string? IdempotencyKey { get; set; }

    /// <summary>When it was sold: now at the desk, the ticket's close time from the POS. Its entries are posted when it is recorded.</summary>
    public DateTime SoldAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
}

/// <summary>A line of a sale: an item from the catalogue, or a line from the POS's ticket.</summary>
public class ClubSaleLine : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid SaleId { get; set; }

    /// <summary>The catalogue item it was rung up from; its name, price and VAT rate were copied.</summary>
    public Guid? CatalogItemId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>The POS's article number for it, when it came from the POS.</summary>
    public string? Sku { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>VAT included.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>The line's part of the sale's discount.</summary>
    public decimal Discount { get; set; }

    /// <summary>Quantity times unit price, less the discount: what the line was charged.</summary>
    public decimal Total { get; set; }

    /// <summary>The VAT rate in percent included in the price, when known.</summary>
    public decimal? VatRate { get; set; }

    /// <summary>The charge kind it was posted under: Bar, Dining, Spa, Retail…</summary>
    public string Kind { get; set; } = "Other";

    /// <summary>Whether it counts toward the points: a tip or a cash rounding does not.</summary>
    public bool EarnsPoints { get; set; } = true;

    public int SortOrder { get; set; }
}

/// <summary>
/// A code that names a member at the POS: a member number or the code on their card (a QR
/// code holds the same), or the POS's own id for them once staff matched one of their
/// tickets. A ticket that carries one is the member's.
/// </summary>
public class ClubMemberCode : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid MemberId { get; set; }

    /// <summary>card or pos-customer (see <see cref="MemberCodeKinds"/>).</summary>
    public string Kind { get; set; } = MemberCodeKinds.Card;

    /// <summary>As it is matched: upper case, without spaces. A POS customer's is "{provider}:{their id}".</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>What staff called it: "Member card", "Lightspeed customer".</summary>
    public string? Label { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
}

/// <summary>
/// A workspace's POS connection: the Lightspeed account it reads closed tickets from (its
/// tokens kept encrypted and never sent back to a screen), the business location, how the
/// POS's payment methods map to the house account and the ways of paying, and where the
/// import has got to.
/// </summary>
public class ClubPosSettings : Common.BaseEntity, Common.IVersioned
{
    public Guid? OrganizationId { get; set; }
    public string Provider { get; set; } = PosProviders.LightspeedK;

    /// <summary>Whether closed tickets are imported on a schedule. Off until someone with money.manage turns it on.</summary>
    public bool Enabled { get; set; }

    /// <summary>The OAuth refresh token Lightspeed gave when the club connected, encrypted. None: not connected.</summary>
    public string? RefreshTokenProtected { get; set; }

    /// <summary>The latest access token, encrypted, and when it stops working.</summary>
    public string? AccessTokenProtected { get; set; }
    public DateTime? AccessTokenExpiresAtUtc { get; set; }

    public DateTime? ConnectedAtUtc { get; set; }
    public string? ConnectedByName { get; set; }

    /// <summary>The POS's business location whose tickets are imported (Lightspeed's businessLocationId), and its name.</summary>
    public string? LocationId { get; set; }
    public string? LocationName { get; set; }

    /// <summary>Tickets closed before this are not imported: set when the club connects, so nothing from before is billed.</summary>
    public DateTime? ImportFromUtc { get; set; }

    /// <summary>
    /// How far the import has read every ticket the POS closed: each import reads again from a
    /// while before it, and moves it on only past the days it read in full.
    /// </summary>
    public DateTime? LastClosedAtUtc { get; set; }

    /// <summary>
    /// How the POS's payment methods are read, by their code or name as JSON:
    /// {"ACCOUNT":"HouseAccount","VISA":"Card"}. A method not listed is read from its name.
    /// </summary>
    public string? MethodMapJson { get; set; }

    /// <summary>Where the POS's sales are counted as sold (Bar, Dining, Spa, Rooftop, PoolDeck): the charge kind and the points' reason.</summary>
    public string DefaultVenue { get; set; } = "Dining";

    /// <summary>One import at a time: the import that set this has the workspace until then.</summary>
    public DateTime? ClaimedUntilUtc { get; set; }

    public DateTime? LastImportAtUtc { get; set; }

    /// <summary>Why the last import failed, in the POS's words; none when it worked.</summary>
    public string? LastImportError { get; set; }

    /// <summary>Tickets the last import brought in.</summary>
    public int LastImportCount { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public string? UpdatedByName { get; set; }
    public Guid Version { get; set; }
}

/// <summary>
/// A closed ticket read from the POS. The POS's own id for it is unique in the workspace, so
/// reading the same ticket again, as every import reads back a while, adds nothing.
/// </summary>
public class ClubPosTicket : Common.BaseEntity, Common.IVersioned
{
    public Guid? OrganizationId { get; set; }
    public string Provider { get; set; } = PosProviders.LightspeedK;

    /// <summary>The POS's id for the ticket (with its location): what makes it once only.</summary>
    public string ExternalId { get; set; } = string.Empty;

    public string? ReceiptNumber { get; set; }
    public string? LocationId { get; set; }
    public DateTime ClosedAtUtc { get; set; }
    public string Currency { get; set; } = "CHF";

    /// <summary>What its lines came to, VAT included.</summary>
    public decimal Total { get; set; }

    /// <summary>What was put on the house account.</summary>
    public decimal OnAccountAmount { get; set; }

    /// <summary>What was paid at the POS.</summary>
    public decimal PaidAmount { get; set; }

    /// <summary>Tips paid with it. A tip put on the house account is owed with the ticket; one paid at the POS is the staff's, not posted.</summary>
    public decimal TipAmount { get; set; }

    /// <summary>How it was paid, in words: "Card 42.00, House account 18.00".</summary>
    public string? TendersText { get; set; }

    public string? StaffName { get; set; }
    public string? TableName { get; set; }
    public string? CustomerName { get; set; }

    /// <summary>What the POS recorded that could name a member: a member code, an email.</summary>
    public string? CustomerReference { get; set; }

    /// <summary>The POS's own id for the customer on the ticket, when there was one.</summary>
    public string? CustomerId { get; set; }

    /// <summary>The ticket as read, its lines and payments, as JSON (PosTicketData).</summary>
    public string? DataJson { get; set; }

    /// <summary>posted, unmatched, failed, skipped or ignored (see <see cref="PosTicketStatuses"/>).</summary>
    public string Status { get; set; } = PosTicketStatuses.Unmatched;

    /// <summary>Why it is not posted, or what was done.</summary>
    public string? Note { get; set; }

    /// <summary>Times it was tried.</summary>
    public int Attempts { get; set; }

    /// <summary>The member it is (or is to be) posted for, and how that was found: code, customer, email or staff.</summary>
    public Guid? MemberId { get; set; }
    public string? MemberName { get; set; }
    public string? MatchedBy { get; set; }

    /// <summary>The sale it was posted as.</summary>
    public Guid? SaleId { get; set; }

    public int? PointsAwarded { get; set; }
    public DateTime ImportedAtUtc { get; set; }
    public DateTime? PostedAtUtc { get; set; }

    /// <summary>Who matched or ignored it, when staff did.</summary>
    public DateTime? ResolvedAtUtc { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolvedByName { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
    public Guid Version { get; set; }
}
