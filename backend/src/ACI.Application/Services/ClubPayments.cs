using System.Text.Json.Serialization;
using ACI.Application.Common;
using ACI.Application.Common.Payments;

namespace ACI.Application.Services;

// Money from outside the club and the books it keeps: the payment and bank settings, the
// Swiss QR-bill on statements, the bank's camt files, Stripe (cards, TWINT, SEPA direct debit)
// and the accounting journal. Every payment these take is recorded through the ledger
// (IClubLedger.RecordReceivedPaymentAsync), once, keyed by the provider's or the bank's id.

/// <summary>A workspace's payment and accounting settings: settings.manage changes them.</summary>
public interface IClubPaymentSettings
{
    Task<PaymentSettingsDto> GetAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// Saves the settings, each checked: an IBAN's check digits, a QR-IBAN's bank number, the
    /// creditor's structured address, the account numbers. A new Stripe key is tried with
    /// Stripe before it is kept. A secret left out is kept; an empty one is removed.
    /// </summary>
    Task<Result<PaymentSettingsDto>> SaveAsync(Guid organizationId, Guid userId, PaymentSettingsRequest request, Guid? expectedVersion, CancellationToken ct = default);

    /// <summary>What a money screen needs to know: whether online payments and QR-bills are set up. No secret in it.</summary>
    Task<PaymentStatusDto> StatusAsync(Guid organizationId, CancellationToken ct = default);
}

/// <summary>The Swiss QR-bill a statement or an invoice is printed with.</summary>
public interface IClubQrBills
{
    /// <summary>
    /// The QR-bill for a member's statement of [fromUtc, toUtc) that asks for
    /// <paramref name="amountDue"/>, with a reference that names the member and the statement
    /// (the same one every time the statement is printed). Null when the club has no bank
    /// details, nothing is due, or the currency is not CHF or EUR: no slip is printed then.
    /// Its message ("Statement October 2026") is in <paramref name="language"/>, the statement's.
    /// </summary>
    Task<QrBill?> ForStatementAsync(Guid organizationId, Guid memberId, DateTime fromUtc, DateTime toUtc, decimal amountDue, string currency,
        string? language = null, CancellationToken ct = default);

    /// <summary>
    /// The QR-bill for an invoice of one charge that asks for <paramref name="amountDue"/>, with
    /// a reference that names the charge (the same one every time it is printed), so the bank's
    /// payment pays that charge first. Null as for a statement.
    /// </summary>
    Task<QrBill?> ForChargeAsync(Guid organizationId, Guid memberId, Guid chargeId, decimal amountDue, string currency, string message, CancellationToken ct = default);

    /// <summary>What a reference printed on a QR-bill pays, for staff looking one up.</summary>
    Task<Result<PaymentReferenceDto>> FindAsync(Guid organizationId, string reference, CancellationToken ct = default);
}

/// <summary>The bank's camt files: the payments they bring in, matched by the QR-bill's reference.</summary>
public interface IClubBankFiles
{
    /// <summary>
    /// Reads a camt.054 (or camt.053, camt.052) file and records each credit whose reference
    /// names a member's statement or charge through the ledger, once: the bank's own
    /// reference for a credit is kept, so the same file, or a later one repeating a credit,
    /// adds nothing. Credits that name nobody wait in the unmatched list.
    /// </summary>
    Task<Result<BankImportDto>> ImportAsync(Guid organizationId, Guid userId, string fileName, Stream content, CancellationToken ct = default);

    Task<IReadOnlyList<BankImportDto>> ImportsAsync(Guid organizationId, int take = 20, CancellationToken ct = default);

    /// <summary>Credits read from bank files, newest first: by status (unmatched, matched, allocated, ignored) or file.</summary>
    Task<IReadOnlyList<BankTransactionDto>> TransactionsAsync(Guid organizationId, string? status, Guid? importId, CancellationToken ct = default);

    /// <summary>Says whose an unmatched credit is, and records it as their payment.</summary>
    Task<Result<BankTransactionDto>> AllocateAsync(Guid organizationId, Guid userId, Guid transactionId, Guid memberId, Guid? expectedVersion, CancellationToken ct = default);

    /// <summary>Says an unmatched credit is no member's payment, and why.</summary>
    Task<Result<BankTransactionDto>> IgnoreAsync(Guid organizationId, Guid userId, Guid transactionId, string? reason, Guid? expectedVersion, CancellationToken ct = default);
}

/// <summary>
/// Online payments through a payment provider (Stripe first): pay links a member pays on
/// Stripe's page, a saved card for dues, refunds through the provider, and what the provider
/// tells the club back.
/// </summary>
public interface IClubPayments
{
    /// <summary>
    /// A link the member pays on: what they owe now (purpose account), some of their charges
    /// (purpose charges), or a page to save a card (purpose setup, with autoPay for their dues).
    /// The link opens Cadence's own page, which starts Stripe's when the member presses Pay, so
    /// the amount is what is owed then and the link does not run out with Stripe's page.
    /// </summary>
    Task<Result<PayLinkDto>> CreatePayLinkAsync(Guid organizationId, Guid userId, Guid memberId, PayLinkRequest request, CancellationToken ct = default);

    /// <summary>The same link, emailed to the member through the outbox.</summary>
    Task<Result<PayLinkDto>> EmailPayLinkAsync(Guid organizationId, Guid userId, Guid memberId, PayLinkRequest request, CancellationToken ct = default);

    /// <summary>What a pay link asks for, for the public page. Reading it changes nothing.</summary>
    Task<Result<PublicPayPageDto>> ReadPayLinkAsync(string token, CancellationToken ct = default);

    /// <summary>Starts Stripe's page for a pay link and says where it is.</summary>
    Task<Result<CheckoutStartedDto>> StartCheckoutAsync(string token, CancellationToken ct = default);

    /// <summary>The member's saved card, whether their dues are charged to it, and their recent online payments.</summary>
    Task<Result<MemberPaymentProfileDto>> ProfileAsync(Guid organizationId, Guid memberId, CancellationToken ct = default);

    /// <summary>Removes the saved card (at Stripe too); dues stop being charged to it.</summary>
    Task<Result<MemberPaymentProfileDto>> RemoveCardAsync(Guid organizationId, Guid userId, Guid memberId, CancellationToken ct = default);

    /// <summary>Turns charging dues to the saved card on (the member agreed, as recorded) or off.</summary>
    Task<Result<MemberPaymentProfileDto>> SetAutoPayAsync(Guid organizationId, Guid userId, Guid memberId, AutoPayRequest request, CancellationToken ct = default);

    /// <summary>Charges open charges to the saved card now: all of them, or those named.</summary>
    Task<Result<OnlinePaymentDto>> ChargeSavedCardAsync(Guid organizationId, Guid userId, Guid memberId, ChargeSavedCardRequest request, CancellationToken ct = default);

    /// <summary>Online payments, newest first: one member's, or by status (failed ones, say).</summary>
    Task<IReadOnlyList<OnlinePaymentDto>> OnlinePaymentsAsync(Guid organizationId, Guid? memberId, string? status, CancellationToken ct = default);

    /// <summary>
    /// Pays money back through Stripe for a payment Stripe took, and records the refund on the
    /// ledger with Stripe's refund id. Nothing is recorded if Stripe refuses.
    /// </summary>
    Task<Result<PaymentDto>> RefundAsync(Guid organizationId, Guid userId, Guid paymentId, RefundPaymentRequest request, CancellationToken ct = default);

    /// <summary>
    /// An event Stripe posted for the workspace whose webhook address carries
    /// <paramref name="endpointKey"/>: its signature checked with that workspace's secret, and
    /// acted on once by its id.
    /// </summary>
    Task<WebhookOutcome> HandleStripeWebhookAsync(Guid endpointKey, byte[] body, string? signatureHeader, CancellationToken ct = default);

    /// <summary>
    /// Charges dues to the saved card of every member who agreed to it, once per charge, in
    /// every workspace with Stripe set up: the payment job's work. Returns how many it tried.
    /// </summary>
    Task<int> CollectAutoPayAsync(DateTime utcNow, CancellationToken ct = default);
}

/// <summary>The accounting journal: the ledger's entries for a period as bookings, for Bexio, Abacus or another program.</summary>
public interface IClubAccounting
{
    /// <summary>The bookings for [fromUtc, toUtc), with the workspace's accounts, and what each account moved by.</summary>
    Task<Result<JournalDto>> JournalAsync(Guid organizationId, DateTime fromUtc, DateTime toUtc, string layout, CancellationToken ct = default);
}

/// <summary>How a webhook post was answered: the HTTP status and a word for the log.</summary>
public sealed record WebhookOutcome(int StatusCode, string Result);

// ── requests ──────────────────────────────────────────────────────────────────

/// <summary>
/// The settings to save. A secret left out (null) is kept and an empty one removed; every
/// other field is saved as sent, an empty one clearing it.
/// </summary>
public sealed class PaymentSettingsRequest
{
    public string? StripeSecretKey { get; set; }
    public string? StripeWebhookSecret { get; set; }
    public bool? StripeOfferTwint { get; set; }
    public bool? StripeOfferSepaDebit { get; set; }
    public string? Iban { get; set; }
    public string? QrIban { get; set; }
    public string? QrReferencePrefix { get; set; }
    public string? CreditorName { get; set; }
    public string? CreditorStreet { get; set; }
    public string? CreditorBuildingNumber { get; set; }
    public string? CreditorPostalCode { get; set; }
    public string? CreditorTown { get; set; }
    public string? CreditorCountry { get; set; }
    public AccountingAccountsRequest? Accounts { get; set; }
    public Guid? Version { get; set; }
}

public sealed class PayLinkRequest
{
    /// <summary>account (what is owed now), charges (those named) or setup (save a card).</summary>
    public string? Purpose { get; set; }

    public List<Guid>? ChargeIds { get; set; }

    /// <summary>For setup: the member is asked to agree to their dues being charged to the card.</summary>
    public bool AutoPay { get; set; }
}

public sealed class AutoPayRequest
{
    public bool Enabled { get; set; }

    /// <summary>Turning it on: the member agreed, which whoever turns it on confirms.</summary>
    public bool MemberAgreed { get; set; }

    public Guid? Version { get; set; }
}

public sealed class ChargeSavedCardRequest
{
    /// <summary>The charges to pay; all of the member's open charges when none are named.</summary>
    public List<Guid>? ChargeIds { get; set; }
}

public sealed class AllocateBankTransactionRequest
{
    public Guid MemberId { get; set; }
    public Guid? Version { get; set; }
}

// ── answers ───────────────────────────────────────────────────────────────────

/// <summary>The settings as a screen sees them: no secret, only whether one is set and the key's last characters.</summary>
public sealed record PaymentSettingsDto
{
    public bool StripeConfigured { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? StripeKeyHint { get; init; }

    /// <summary>test or live, from the key.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? StripeMode { get; init; }

    public bool StripeWebhookSecretSet { get; init; }
    public bool StripeOfferTwint { get; init; }
    public bool StripeOfferSepaDebit { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? StripeCheckedAtUtc { get; init; }

    /// <summary>Where Stripe is to post this workspace's events, and which events to send.</summary>
    public string WebhookUrl { get; init; } = string.Empty;
    public IReadOnlyList<string> WebhookEvents { get; init; } = Array.Empty<string>();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Iban { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? QrIban { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? QrReferencePrefix { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CreditorName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CreditorStreet { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CreditorBuildingNumber { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CreditorPostalCode { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CreditorTown { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CreditorCountry { get; init; }

    /// <summary>Whether statements carry a QR-bill, and if not, why not.</summary>
    public bool QrBillReady { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? QrBillProblem { get; init; }

    public AccountingAccounts Accounts { get; init; } = AccountingAccounts.Default;

    /// <summary>The workspace's currency, which online payments and QR-bills are in.</summary>
    public string Currency { get; init; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? UpdatedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? UpdatedByName { get; init; }
    public Guid Version { get; init; }
}

/// <summary>What the money screens show and hide by: no secret, no address.</summary>
/// <param name="OnlinePayments">A Stripe key is set: pay links and saved cards work.</param>
/// <param name="Mode">test or live, from the Stripe key.</param>
/// <param name="Methods">What Stripe's page offers in the workspace's currency: card, twint, sepa_debit.</param>
/// <param name="WebhookConfigured">The webhook secret is set, so Stripe's word that a payment went through is heard.</param>
/// <param name="QrBills">Statements carry a Swiss QR-bill.</param>
/// <param name="QrReferenceType">QRR (QR-IBAN) or SCOR (IBAN).</param>
public sealed record PaymentStatusDto(
    bool OnlinePayments, string? Mode, IReadOnlyList<string> Methods, bool WebhookConfigured,
    bool QrBills, string? QrReferenceType, string Currency);

/// <param name="MemberId">The member it is for; none for a contract's deposit (<paramref name="ContractId"/>), printed before anyone was a member.</param>
public sealed record PaymentReferenceDto(
    Guid Id, string Reference, string Type, Guid? MemberId, string? MemberName, string Purpose,
    DateTime? PeriodStartUtc, DateTime? PeriodEndUtc, Guid? ChargeId, decimal AmountDue, string Currency, DateTime CreatedAtUtc,
    Guid? ContractId = null);

public sealed record BankImportDto(
    Guid Id, string FileName, string Format, string? MessageId, string? AccountIban, int Transactions, int Matched,
    int Unmatched, int AlreadyImported, int Skipped, decimal AmountMatched, DateTime ImportedAtUtc, string? ImportedByName);

public sealed record BankTransactionDto
{
    public Guid Id { get; init; }
    public Guid ImportId { get; init; }
    public string BankReference { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? AccountIban { get; init; }
    public DateTime BookedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? ValueDateUtc { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ReferenceType { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Reference { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? DebtorName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? DebtorIban { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Message { get; init; }
    public string Status { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Note { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? MemberId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? MemberName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? LedgerPaymentId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? ResolvedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? ResolvedByName { get; init; }
    public Guid Version { get; init; }
}

public sealed record PayLinkDto(
    string Url, string Purpose, decimal Amount, string Currency, DateTime ExpiresAtUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EmailStatus = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? EmailReason = null);

/// <summary>What the public pay page shows: the club, whom it is for, what and how much. No account details.</summary>
/// <param name="Language">The member's language (en, de, fr), which the page reads in unless the visitor picks another.</param>
/// <param name="Locale">The workspace's number format ("de-CH"), so the page writes the amount as the club's statements do.</param>
public sealed record PublicPayPageDto(
    string ClubName, string MemberFirstName, string Purpose, string Description, decimal Amount, string Currency,
    IReadOnlyList<string> Methods, bool Available,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Reason,
    bool AutoPay,
    string Language = "en",
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Locale = null);

public sealed record CheckoutStartedDto(string Url);

public sealed record SavedCardDto(string Type, string? Brand, string? Last4, int? ExpMonth, int? ExpYear, DateTime? SavedAtUtc);

public sealed record MemberPaymentProfileDto
{
    public Guid MemberId { get; init; }
    public bool OnlinePayments { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SavedCardDto? Card { get; init; }
    public bool AutoPayDues { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? AutoPayAgreedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? AutoPayAgreedBy { get; init; }
    public IReadOnlyList<OnlinePaymentDto> Payments { get; init; } = Array.Empty<OnlinePaymentDto>();
    public Guid Version { get; init; }
}

public sealed record OnlinePaymentDto
{
    public Guid Id { get; init; }
    public Guid MemberId { get; init; }
    public string MemberName { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string Purpose { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? MethodType { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? PaymentIntentId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? FailureCode { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? FailureMessage { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? AmountReceived { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? LedgerPaymentId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CreatedByName { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? CompletedAtUtc { get; init; }
}

/// <summary>One booking: one account debited and one credited with the same amount.</summary>
public sealed record JournalRowDto
{
    public DateTime DateUtc { get; init; }

    /// <summary>The day in the workspace's time zone, as the CSV prints it.</summary>
    public DateOnly Date { get; init; }

    public string Document { get; init; } = string.Empty;
    public string Debit { get; init; } = string.Empty;
    public string Credit { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? VatCode { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? VatRate { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? VatAmount { get; init; }
    public string Text { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Member { get; init; }
    public string Type { get; init; } = string.Empty;
    public Guid EntryId { get; init; }
}

/// <summary>What an account moved by over the period: debits less credits.</summary>
public sealed record JournalAccountDto(string Account, decimal Debits, decimal Credits, decimal Net);

public sealed record JournalDto(
    DateTime FromUtc, DateTime ToUtc, string Layout, string Currency, IReadOnlyList<JournalRowDto> Rows,
    IReadOnlyList<JournalAccountDto> Accounts, AccountingAccounts Mapping);
