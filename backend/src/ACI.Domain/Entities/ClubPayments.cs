namespace ACI.Domain.Entities;

// How money reaches the club from outside: a payment provider (Stripe: cards, TWINT, SEPA
// direct debit) and the bank (Swiss QR-bills, matched from the bank's camt files), and how
// the books leave it (the accounting journal). None of it posts money itself: a payment that
// succeeds is recorded through the ledger (IClubLedger), once, keyed by the provider's or the
// bank's own id, so a webhook delivered twice or a bank file imported twice adds nothing.
//
// Everything is off until a club sets it up: no Stripe key, no online payment; no IBAN, no
// QR-bill on statements.

/// <summary>Where a payment came from, as kept on the ledger's payment.</summary>
public static class PaymentProviders
{
    /// <summary>Stripe: a card, TWINT or SEPA direct debit, through Checkout or a saved card.</summary>
    public const string Stripe = "stripe";

    /// <summary>A bank transfer read from the bank's camt file, usually paying a QR-bill.</summary>
    public const string Bank = "bank";
}

/// <summary>What an online payment is for.</summary>
public static class OnlinePaymentPurposes
{
    /// <summary>The member pays a statement, their open charges, or some of them on Stripe's page.</summary>
    public const string Checkout = "checkout";

    /// <summary>Dues charged to the member's saved card or account, as they agreed.</summary>
    public const string AutoPay = "autopay";

    /// <summary>Open charges charged to the saved card by staff.</summary>
    public const string SavedCard = "card";

    /// <summary>The member saves a card (or a SEPA mandate) on Stripe's page; no money moves.</summary>
    public const string SaveCard = "setup";
}

/// <summary>Where an online payment stands, as Stripe last said.</summary>
public static class OnlinePaymentStatuses
{
    /// <summary>A page or a charge started; nothing paid yet.</summary>
    public const string Open = "open";

    /// <summary>Paid by a method that takes days to clear (SEPA direct debit).</summary>
    public const string Processing = "processing";

    public const string Succeeded = "succeeded";
    public const string Failed = "failed";
    public const string Canceled = "canceled";

    /// <summary>A Checkout page nobody finished before it timed out.</summary>
    public const string Expired = "expired";
}

/// <summary>Where a line read from a bank file stands.</summary>
public static class BankTransactionStatuses
{
    /// <summary>Its reference named a member's statement or charge, and the payment was recorded.</summary>
    public const string Matched = "matched";

    /// <summary>Nothing it carried named a member: it waits for someone to say whose it is.</summary>
    public const string Unmatched = "unmatched";

    /// <summary>Someone said whose it is, and the payment was recorded.</summary>
    public const string Allocated = "allocated";

    /// <summary>Someone said it is not a member's payment (a refund from a supplier, say), with why.</summary>
    public const string Ignored = "ignored";
}

/// <summary>
/// A workspace's payment and accounting settings: the Stripe account it takes money with,
/// the bank account its QR-bills pay into, and the accounts its journal books to.
/// </summary>
/// <remarks>
/// The Stripe key and the webhook signing secret are kept encrypted (ISecretProtector) and
/// never sent back to a screen: a screen sees whether one is set, and its last characters.
/// </remarks>
public class ClubPaymentSettings : Common.BaseEntity, Common.IVersioned
{
    public Guid? OrganizationId { get; set; }

    /// <summary>The Stripe secret (or restricted) key, encrypted. None: no online payments.</summary>
    public string? StripeSecretKeyProtected { get; set; }

    /// <summary>What a screen shows of the key: its mode and last characters, "sk_live_…4Abc".</summary>
    public string? StripeKeyHint { get; set; }

    /// <summary>The signing secret of the Stripe webhook endpoint (whsec_…), encrypted.</summary>
    public string? StripeWebhookSecretProtected { get; set; }

    /// <summary>Offer TWINT on Stripe's page, for amounts in CHF (TWINT must be on in the Stripe account).</summary>
    public bool StripeOfferTwint { get; set; }

    /// <summary>Offer SEPA direct debit, for amounts in EUR (it must be on in the Stripe account).</summary>
    public bool StripeOfferSepaDebit { get; set; }

    /// <summary>
    /// The last part of the address Stripe posts this workspace's events to, so a post says
    /// whose it is before its signature is checked with that workspace's secret.
    /// </summary>
    public Guid WebhookKey { get; set; }

    /// <summary>When Stripe last accepted the key (checked on saving it).</summary>
    public DateTime? StripeCheckedAtUtc { get; set; }

    /// <summary>The club's IBAN (CH or LI), for QR-bills with a creditor reference (SCOR).</summary>
    public string? Iban { get; set; }

    /// <summary>The club's QR-IBAN, for QR-bills with a QR reference (QRR). Used before the IBAN when set.</summary>
    public string? QrIban { get; set; }

    /// <summary>
    /// Digits some banks give a club to start its QR references with (a customer number); the
    /// rest of the reference is Cadence's own.
    /// </summary>
    public string? QrReferencePrefix { get; set; }

    /// <summary>The creditor on the QR-bill: the club's name and postal address, structured as the standard requires.</summary>
    public string? CreditorName { get; set; }
    public string? CreditorStreet { get; set; }
    public string? CreditorBuildingNumber { get; set; }
    public string? CreditorPostalCode { get; set; }
    public string? CreditorTown { get; set; }
    public string? CreditorCountry { get; set; }

    /// <summary>The accounts the journal books to, as JSON over the Swiss SME chart's defaults (AccountingAccounts).</summary>
    public string? AccountsJson { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public string? UpdatedByName { get; set; }
    public Guid Version { get; set; }
}

/// <summary>
/// A member's saved way to pay at a provider: the provider's customer and payment method ids,
/// and what Stripe shows of the method (brand, last four digits, expiry) so staff can tell
/// which card it is. No card number, security code or bank detail is ever kept here: the
/// member types those on Stripe's own page.
/// </summary>
public class ClubPaymentProfile : Common.BaseEntity, Common.IVersioned
{
    public Guid? OrganizationId { get; set; }
    public Guid MemberId { get; set; }
    public string Provider { get; set; } = PaymentProviders.Stripe;

    /// <summary>The provider's customer (cus_…), made the first time the member is sent to it.</summary>
    public string? CustomerId { get; set; }

    /// <summary>The saved payment method (pm_…); none when no card is saved or it was removed.</summary>
    public string? PaymentMethodId { get; set; }

    /// <summary>card or sepa_debit.</summary>
    public string? PaymentMethodType { get; set; }

    public string? Brand { get; set; }
    public string? Last4 { get; set; }
    public int? ExpMonth { get; set; }
    public int? ExpYear { get; set; }
    public DateTime? SavedAtUtc { get; set; }

    /// <summary>Whether the member agreed to have their dues charged to the saved method as they are billed.</summary>
    public bool AutoPayDues { get; set; }

    /// <summary>When and how they agreed: on Stripe's page themselves, or as recorded by a member of staff.</summary>
    public DateTime? AutoPayAgreedAtUtc { get; set; }
    public string? AutoPayAgreedBy { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid Version { get; set; }
}

/// <summary>
/// One attempt to take money (or save a card) through a provider, with where it stands: what
/// staff see of online payments, failures included. A payment that succeeds is recorded on
/// the ledger once, and the ledger's payment is named here.
/// </summary>
public class ClubOnlinePayment : Common.BaseEntity, Common.IVersioned
{
    public Guid? OrganizationId { get; set; }
    public Guid MemberId { get; set; }
    public string MemberName { get; set; } = string.Empty;
    public string Provider { get; set; } = PaymentProviders.Stripe;

    /// <summary>checkout, autopay, card or setup (see <see cref="OnlinePaymentPurposes"/>).</summary>
    public string Purpose { get; set; } = OnlinePaymentPurposes.Checkout;

    /// <summary>open, processing, succeeded, failed, canceled or expired (see <see cref="OnlinePaymentStatuses"/>).</summary>
    public string Status { get; set; } = OnlinePaymentStatuses.Open;

    /// <summary>What it asks for; nothing for saving a card.</summary>
    public decimal Amount { get; set; }

    public string Currency { get; set; } = "CHF";
    public string Description { get; set; } = string.Empty;

    /// <summary>The charges it pays first, comma-separated; what is left over pays the member's other open charges, then stays as credit.</summary>
    public string? ChargeIds { get; set; }

    /// <summary>The statement it pays, when it pays one.</summary>
    public DateTime? StatementFromUtc { get; set; }
    public DateTime? StatementToUtc { get; set; }

    /// <summary>
    /// Claims the work, unique in the workspace: "autopay:{charge}:{try}" for a dues charge, so
    /// two servers or a retry never charge the same dues twice. None for a Checkout page.
    /// </summary>
    public string? AttemptKey { get; set; }

    public string? CheckoutSessionId { get; set; }
    public string? CheckoutUrl { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public string? PaymentIntentId { get; set; }
    public string? SetupIntentId { get; set; }
    public string? ProviderChargeId { get; set; }

    /// <summary>How it was paid: card, twint or sepa_debit.</summary>
    public string? MethodType { get; set; }

    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }

    /// <summary>What the provider says it received.</summary>
    public decimal? AmountReceived { get; set; }

    /// <summary>The ledger's payment, once recorded.</summary>
    public Guid? LedgerPaymentId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public Guid Version { get; set; }
}

/// <summary>
/// An event a provider posted (a Stripe webhook), kept by its id: a provider sends an event
/// again until it is answered, and the same id is then answered without acting twice.
/// </summary>
public class ClubProviderEvent : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public string Provider { get; set; } = PaymentProviders.Stripe;

    /// <summary>The provider's id for the event (evt_…).</summary>
    public string EventId { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    /// <summary>The object it is about (pi_…, seti_…, cs_…, ch_…).</summary>
    public string? ObjectId { get; set; }

    /// <summary>received, processed, ignored or failed.</summary>
    public string Status { get; set; } = "received";

    public string? Note { get; set; }
    public int Attempts { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
}

/// <summary>
/// A payment reference printed on a QR-bill, and what it pays: a member's statement for a
/// period, or one charge. A bank transfer that carries it is the member's payment.
/// </summary>
/// <remarks>
/// The number is the workspace's own sequence. A QR reference (QRR, with a QR-IBAN) is that
/// number in 26 digits, after the bank's prefix if it gave one, and a check digit; a creditor
/// reference (SCOR, with a plain IBAN) is the same number as an ISO 11649 "RF" reference.
/// Both are kept as they were printed, so a later change of prefix or IBAN still finds them.
/// </remarks>
public class ClubPaymentReference : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public long Number { get; set; }

    /// <summary>The 27-digit QR reference, once one was printed (a QR-IBAN was set).</summary>
    public string? QrReference { get; set; }

    /// <summary>The ISO 11649 creditor reference, "RF…".</summary>
    public string ScorReference { get; set; } = string.Empty;

    /// <summary>
    /// The member it is for. None for a contract's deposit, printed before anyone is a member:
    /// the deposit (<see cref="ContractId"/>) says whose account it goes on once there is one.
    /// </summary>
    public Guid? MemberId { get; set; }

    /// <summary>For a deposit: the contract that asks for it.</summary>
    public Guid? ContractId { get; set; }

    /// <summary>statement, charge or deposit.</summary>
    public string Purpose { get; set; } = "statement";

    public DateTime? PeriodStartUtc { get; set; }
    public DateTime? PeriodEndUtc { get; set; }
    public Guid? ChargeId { get; set; }

    /// <summary>What it asked for when it was first printed.</summary>
    public decimal AmountDue { get; set; }

    public string Currency { get; set; } = "CHF";
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>A bank file staff uploaded (camt.054 or camt.053), and what came of it.</summary>
public class ClubBankImport : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public string FileName { get; set; } = string.Empty;

    /// <summary>camt.054, camt.053 or camt.052.</summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>The bank's id for the file (GrpHdr/MsgId).</summary>
    public string? MessageId { get; set; }

    public string? AccountIban { get; set; }

    /// <summary>Credits read from it.</summary>
    public int Transactions { get; set; }

    public int Matched { get; set; }
    public int Unmatched { get; set; }

    /// <summary>Credits a file imported before had already brought in.</summary>
    public int AlreadyImported { get; set; }

    /// <summary>Debits, reversals and entries not yet booked, which are not member payments.</summary>
    public int Skipped { get; set; }

    public decimal AmountMatched { get; set; }
    public DateTime ImportedAtUtc { get; set; }
    public Guid? ImportedByUserId { get; set; }
    public string? ImportedByName { get; set; }
}

/// <summary>
/// A credit read from a bank file. The bank's own reference for it is unique in the
/// workspace, so importing the same file again, or a later file that repeats it, records
/// nothing twice.
/// </summary>
public class ClubBankTransaction : Common.BaseEntity, Common.IVersioned
{
    public Guid? OrganizationId { get; set; }
    public Guid ImportId { get; set; }

    /// <summary>The bank's reference for the credit (with its account): what makes it once only.</summary>
    public string BankReference { get; set; } = string.Empty;

    public string? AccountIban { get; set; }
    public DateTime BookedAtUtc { get; set; }
    public DateTime? ValueDateUtc { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "CHF";

    /// <summary>QRR or SCOR when it carried a structured reference.</summary>
    public string? ReferenceType { get; set; }

    public string? Reference { get; set; }
    public string? DebtorName { get; set; }
    public string? DebtorIban { get; set; }

    /// <summary>The free text the payer added.</summary>
    public string? Message { get; set; }

    /// <summary>matched, unmatched, allocated or ignored (see <see cref="BankTransactionStatuses"/>).</summary>
    public string Status { get; set; } = BankTransactionStatuses.Unmatched;

    /// <summary>Why it did not match, or why it was ignored.</summary>
    public string? Note { get; set; }

    public Guid? MemberId { get; set; }
    public string? MemberName { get; set; }
    public Guid? PaymentReferenceId { get; set; }
    public Guid? LedgerPaymentId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public string? ResolvedByName { get; set; }
    public Guid Version { get; set; }
}
