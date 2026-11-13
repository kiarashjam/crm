namespace ACI.Domain.Entities;

/// <summary>Where a contract's deposit stands.</summary>
public static class ContractDepositStatuses
{
    /// <summary>Asked for and not paid yet. A payment that failed leaves it here, to be tried again.</summary>
    public const string Awaiting = "awaiting";

    /// <summary>Paid by a method that takes days to clear (a SEPA direct debit).</summary>
    public const string Processing = "processing";

    /// <summary>The money came in, by Stripe or by a bank transfer quoting the deposit's reference.</summary>
    public const string Paid = "paid";

    /// <summary>All of it was paid back before it was put on anyone's account.</summary>
    public const string Refunded = "refunded";

    /// <summary>The contract was declined or voided before anything was paid: nothing can be paid any more.</summary>
    public const string Cancelled = "cancelled";

    public static readonly IReadOnlyList<string> All = [Awaiting, Processing, Paid, Refunded, Cancelled];
}

/// <summary>
/// The deposit a membership contract asks for on signing, and what became of it: whether it
/// was paid, how, and on which member's account it was put.
/// </summary>
/// <remarks>
/// <para>
/// The signer pays it on the signing page (Stripe Checkout), or by a bank transfer quoting the
/// QR-bill's reference when the club takes no payments online. Nobody is a member yet at that
/// point: the member is made when the club countersigns. So the money is kept here, as paid,
/// and put on the member's account (the ledger) once both have happened, in whichever order,
/// paying their initiation fee and first dues first. The ledger records it under the
/// provider's own id ("stripe:pi_…", "bank:…"), so a webhook delivered twice, a bank file
/// imported again or a countersignature retried never records it twice.
/// </para>
/// <para>
/// Kept apart from the contract's own row on purpose: a payment arriving must never touch the
/// signature record or its row version, or a countersignature could be refused because Stripe
/// had just said the deposit was paid.
/// </para>
/// </remarks>
public class ContractDeposit : Common.BaseEntity, Common.IVersioned
{
    public Guid? OrganizationId { get; set; }

    /// <summary>The contract that asks for it: one deposit per contract.</summary>
    public Guid ContractId { get; set; }

    /// <summary>What it asks for, in <see cref="Currency"/>, frozen when the contract was sent.</summary>
    public decimal Amount { get; set; }

    /// <summary>The club's currency, which the member's account is kept in.</summary>
    public string Currency { get; set; } = "CHF";

    /// <summary><see cref="ContractDepositStatuses"/>.</summary>
    public string Status { get; set; } = ContractDepositStatuses.Awaiting;

    /// <summary>stripe or bank (PaymentProviders), once money came in.</summary>
    public string? Provider { get; set; }

    /// <summary>The provider's id for the money (Stripe's payment intent, the bank's reference for the credit).</summary>
    public string? ProviderPaymentId { get; set; }

    /// <summary>How it was paid: card, twint, sepa_debit or bank.</summary>
    public string? MethodType { get; set; }

    /// <summary>What came in, which may differ from what was asked for (a transfer of another amount).</summary>
    public decimal? AmountPaid { get; set; }

    /// <summary>What was paid back before the deposit was put on an account (a refund made in Stripe).</summary>
    public decimal AmountRefunded { get; set; }

    public DateTime? PaidAtUtc { get; set; }

    /// <summary>Stripe's page the signer was last sent to, reused while it lasts.</summary>
    public string? CheckoutSessionId { get; set; }
    public string? CheckoutUrl { get; set; }
    public DateTime? CheckoutExpiresAtUtc { get; set; }

    /// <summary>How many Stripe pages were made for it, which keys each one apart at Stripe.</summary>
    public int CheckoutAttempts { get; set; }

    /// <summary>Why the last payment did not go through, as the provider said; cleared once it does.</summary>
    public string? FailureMessage { get; set; }

    /// <summary>The QR reference printed on its QR-bill (ClubPaymentReference), once one was.</summary>
    public Guid? PaymentReferenceId { get; set; }

    /// <summary>The member whose account it went on, and the ledger's payment, once it was put there.</summary>
    public Guid? MemberId { get; set; }
    public Guid? LedgerPaymentId { get; set; }
    public DateTime? RecordedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    /// <summary>A new value on every save: the webhook, the bank file and the countersignature never write over each other.</summary>
    public Guid Version { get; set; }
}
