using System.Text.Json.Serialization;
using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

/// <summary>
/// The deposit a membership contract asks for: collected on the signing page (Stripe Checkout,
/// or a Swiss QR-bill when the club takes no payments online), kept until the signer is a
/// member, then put on their account, once, paying their first charges.
/// </summary>
/// <remarks>
/// <para>
/// <b>Asking</b>: a contract that admits a member at a tier can ask for a deposit
/// (<see cref="CheckAskAsync"/>), in the club's currency, by someone who may take payments
/// (money.manage), when the club can be paid: Stripe set up, or the bank details a QR-bill
/// needs. The amount is frozen with the contract's text when it is sent (<see cref="OpenAsync"/>).
/// </para>
/// <para>
/// <b>Paying</b>: once the signer has signed, the signing page offers Stripe's page
/// (<see cref="StartCheckoutAsync"/>) and the QR-bill (<see cref="QrBillAsync"/>). Stripe's
/// word that the payment went through (<see cref="HandleStripeEventAsync"/>) or a bank file's
/// transfer quoting the deposit's reference (<see cref="RecordBankTransferAsync"/>) marks it
/// paid. Each is keyed by the provider's own id: a webhook delivered twice, or a file imported
/// again, changes nothing, and a word that comes late (failed after paid) does not undo it.
/// </para>
/// <para>
/// <b>Putting it on the account</b> (<see cref="SettleAsync"/>): when the deposit is paid and
/// the signer has been admitted through the club's approval (the countersignature, or a
/// reviewer's approve), in either order, while the contract stands. A deposit paid on a contract
/// that was voided or declined is the signer's to get back: it is never put on an account by
/// itself, and the contract's trail tells staff to pay it back. A paid deposit never admits
/// anyone on its own. The approval's dues and initiation fee, left pending when whoever approved may not charge
/// accounts, are finished through the deposit hook (IClubApplications.AdmitForDepositAsync),
/// because the applicant has paid for them; the deposit is then recorded on the ledger under
/// the provider's id, paying the initiation fee and the first dues first, and what is left
/// stays on the account as credit.
/// </para>
/// </remarks>
public interface IContractDeposits
{
    /// <summary>
    /// Whether a contract may ask for this deposit: the club turned on, someone who may take
    /// payments, a positive amount, and a way for the signer to pay. Answers the amount to the
    /// cent and the club's currency.
    /// </summary>
    Task<Result<DepositAsk>> CheckAskAsync(Guid organizationId, Guid userId, decimal amount, CancellationToken ct = default);

    /// <summary>The deposit's row, made when a contract asking for one is sent (once, however often it is sent).</summary>
    Task OpenAsync(Contract contract, CancellationToken ct = default);

    /// <summary>
    /// A declined or voided contract: a deposit not paid yet can no longer be paid. One already
    /// paid stays, to be refunded, and the contract's trail says so.
    /// </summary>
    Task CloseAsync(Contract contract, CancellationToken ct = default);

    /// <summary>The deposits of these contracts, by contract id, as staff see them.</summary>
    Task<IReadOnlyDictionary<Guid, ContractDepositDto>> ForContractsAsync(Guid organizationId, IReadOnlyCollection<Guid> contractIds, CancellationToken ct = default);

    /// <summary>What the signing page shows of the deposit: how much, whether it is paid, how it can be paid.</summary>
    Task<PublicDepositDto?> PublicAsync(Contract contract, CancellationToken ct = default);

    /// <summary>
    /// Starts Stripe's page for the deposit, once the signer has signed, and says where it is;
    /// <paramref name="signingPageUrl"/> is where Stripe sends them back to.
    /// </summary>
    Task<Result<CheckoutStartedDto>> StartCheckoutAsync(Contract contract, string signingPageUrl, CancellationToken ct = default);

    /// <summary>The deposit as an invoice with its Swiss QR-bill, once the signer has signed, in the reader's language.</summary>
    Task<Result<ContractDocument>> QrBillAsync(Contract contract, string? language, CancellationToken ct = default);

    /// <summary>
    /// Acts on a Stripe event about a deposit (its metadata names one); null when the event is
    /// not about a deposit, else a word for the event's log.
    /// </summary>
    Task<string?> HandleStripeEventAsync(Guid organizationId, ProviderEvent stripeEvent, CancellationToken ct = default);

    /// <summary>
    /// A refund Stripe reports on a payment no account holds yet: a deposit paid back before
    /// it was put on one. Null when the payment is no deposit's.
    /// </summary>
    Task<string?> RecordProviderRefundAsync(Guid organizationId, string paymentIntentId, IReadOnlyList<ProviderRefund> refunds, CancellationToken ct = default);

    /// <summary>
    /// A bank transfer quoting a deposit's reference: the deposit paid, and put on the member's
    /// account when there is one. Answers what became of the transfer.
    /// </summary>
    Task<DepositTransferOutcome> RecordBankTransferAsync(Guid organizationId, Guid contractId, DepositTransfer transfer, CancellationToken ct = default);

    /// <summary>
    /// Puts the contract's paid deposit on the account of the member its approval admitted, once
    /// (see the remarks on this interface). Nothing when it is not paid, already recorded, the
    /// contract was withdrawn, or nobody has been admitted yet. Never throws for what it cannot
    /// do: it says so on the contract's trail and leaves the deposit to be put on the account later.
    /// </summary>
    /// <param name="userId">Who is acting (the countersigner, a reviewer), or null for a payment arriving.</param>
    Task<DepositSettlement?> SettleAsync(Guid organizationId, Guid contractId, Guid? userId, CancellationToken ct = default);

    /// <summary>After an application was approved: settles the deposits of the contracts behind it.</summary>
    Task SettleForApplicationAsync(Guid organizationId, Guid applicationId, Guid? userId, CancellationToken ct = default);
}

/// <summary>A deposit a contract may ask for: the amount to the cent, in the club's currency.</summary>
public sealed record DepositAsk(decimal Amount, string Currency);

/// <summary>A credit read from the bank's file that quotes a deposit's reference.</summary>
public sealed record DepositTransfer(string BankReference, decimal Amount, string Currency, DateTime ReceivedAtUtc, string? DebtorName, string FileName);

/// <summary>What became of a transfer quoting a deposit's reference.</summary>
/// <param name="Taken">The transfer is the deposit's payment (false: the deposit was paid before, or cannot take it).</param>
/// <param name="Note">Why it was not taken, or where it is kept, for the bank file's list.</param>
public sealed record DepositTransferOutcome(bool Taken, Guid? MemberId, string? MemberName, Guid? LedgerPaymentId, string? Note);

/// <summary>What putting a deposit on an account did.</summary>
public sealed record DepositSettlement(Guid MemberId, string MemberName, Guid LedgerPaymentId, decimal Amount, string Currency, bool MoneyFinished);

/// <summary>A contract's deposit, as staff see it on the contract.</summary>
public sealed record ContractDepositDto
{
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? AmountPaid { get; init; }
    public decimal AmountRefunded { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? PaidAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Provider { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? MethodType { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? FailureMessage { get; init; }

    /// <summary>The member whose account it went on, and the ledger's payment, once it did.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? MemberId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? MemberName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? LedgerPaymentId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? RecordedAtUtc { get; init; }

    /// <summary>The QR reference printed on its QR-bill, once one was.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Reference { get; init; }
}

/// <summary>What the signing page shows of a deposit: no ids, nothing of anyone's account.</summary>
/// <param name="Payable">The signer can pay it now: they have signed, it is not paid, and the contract stands.</param>
/// <param name="Methods">What Stripe's page offers (card, twint, sepa_debit); none when the club takes no payments online.</param>
/// <param name="QrBill">A QR-bill for it can be downloaded.</param>
/// <param name="Iban">The account a transfer goes to, with <paramref name="Reference"/>, for typing into e-banking; only with a QR-bill.</param>
/// <param name="Locale">The club's number format ("de-CH"), so the page writes the amount as its documents do.</param>
public sealed record PublicDepositDto(
    decimal Amount, string Currency, string Status, DateTime? PaidAtUtc, bool Payable,
    IReadOnlyList<string> Methods, bool QrBill,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Iban,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Reference,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Creditor,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? FailureMessage,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Locale);
