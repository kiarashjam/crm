namespace ACI.Application.Services;

/// <summary>
/// A payment provider the club takes money through: Stripe first. The club's own secret key is
/// passed with each call, as every workspace has its own account. Card numbers never pass
/// through here: members type them on the provider's own page (Checkout), and what comes back
/// are the provider's ids and what it shows of a card (brand, last four digits, expiry).
/// </summary>
/// <remarks>
/// Amounts are in the currency's major unit (francs, euros) on this side; the provider turns
/// them into its own minor units. A refusal (a declined card, a key Stripe does not accept)
/// is a <see cref="ProviderFailure"/> with the provider's words; a provider that could not be
/// reached is one too, marked so, as trying again later may work.
/// </remarks>
public interface IPaymentProvider
{
    /// <summary>"stripe".</summary>
    string Name { get; }

    /// <summary>Whether the provider accepts the key: a read it allows, which changes nothing.</summary>
    Task<ProviderResult<bool>> CheckKeyAsync(string secretKey, CancellationToken ct = default);

    /// <summary>Makes the provider's customer for a member, returning its id.</summary>
    Task<ProviderResult<string>> CreateCustomerAsync(string secretKey, ProviderCustomerRequest request, CancellationToken ct = default);

    /// <summary>Starts the provider's hosted page: to pay (mode payment) or to save a card (mode setup).</summary>
    Task<ProviderResult<ProviderCheckout>> CreateCheckoutAsync(string secretKey, ProviderCheckoutRequest request, CancellationToken ct = default);

    /// <summary>
    /// Charges a saved card or mandate without the member present. A decline is not a failure
    /// of the call: the payment comes back failed, with the provider's reason.
    /// </summary>
    Task<ProviderResult<ProviderPaymentIntent>> ChargeSavedMethodAsync(string secretKey, ProviderChargeRequest request, CancellationToken ct = default);

    Task<ProviderResult<ProviderSetup>> GetSetupAsync(string secretKey, string setupId, CancellationToken ct = default);

    Task<ProviderResult<ProviderRefund>> RefundAsync(string secretKey, ProviderRefundRequest request, CancellationToken ct = default);

    /// <summary>
    /// The refunds made on a payment, as the provider lists them. A refund made in the provider's
    /// own dashboard is read from here: the charge a webhook reports it with does not list them.
    /// </summary>
    Task<ProviderResult<IReadOnlyList<ProviderRefund>>> ListRefundsAsync(string secretKey, string paymentIntentId, CancellationToken ct = default);

    /// <summary>Removes a saved card or mandate from the customer, so it can no longer be charged.</summary>
    Task<ProviderResult<bool>> DetachPaymentMethodAsync(string secretKey, string paymentMethodId, CancellationToken ct = default);

    /// <summary>
    /// The event a webhook post carries, once its signature is checked with the endpoint's
    /// secret and its time is recent; null with the reason when it is not to be trusted.
    /// </summary>
    ProviderEvent? ReadEvent(string webhookSecret, byte[] body, string? signatureHeader, DateTime utcNow, out string? problem);
}

/// <summary>A provider's answer: the value, or why there is none.</summary>
public sealed class ProviderResult<T>
{
    private ProviderResult(T? value, ProviderFailure? failure)
    {
        Value = value;
        Failure = failure;
    }

    public T? Value { get; }
    public ProviderFailure? Failure { get; }
    public bool IsSuccess => Failure == null;

    public static ProviderResult<T> Ok(T value) => new(value, null);
    public static ProviderResult<T> Fail(ProviderFailure failure) => new(default, failure);
}

/// <summary>Why a provider said no, in its own words; <see cref="Unreachable"/> when it did not answer at all.</summary>
public sealed record ProviderFailure(string Message, string? Code = null, bool Unreachable = false, int? StatusCode = null);

public sealed record ProviderCustomerRequest(string Name, string? Email, IReadOnlyDictionary<string, string> Metadata, string IdempotencyKey);

/// <param name="Mode">payment or setup.</param>
/// <param name="MethodTypes">card, twint, sepa_debit: what the page offers.</param>
/// <param name="ConsentText">Words shown above the button: what the member agrees to by saving a card.</param>
public sealed record ProviderCheckoutRequest(
    string Mode,
    decimal Amount,
    string Currency,
    string Description,
    string? CustomerId,
    string? CustomerEmail,
    IReadOnlyList<string> MethodTypes,
    string SuccessUrl,
    string CancelUrl,
    DateTime ExpiresAtUtc,
    IReadOnlyDictionary<string, string> Metadata,
    string ClientReferenceId,
    string IdempotencyKey,
    string? ConsentText = null);

public sealed record ProviderChargeRequest(
    string CustomerId,
    string PaymentMethodId,
    string MethodType,
    decimal Amount,
    string Currency,
    string Description,
    IReadOnlyDictionary<string, string> Metadata,
    string IdempotencyKey);

public sealed record ProviderRefundRequest(string PaymentIntentId, decimal Amount, string Currency, string? Reason, IReadOnlyDictionary<string, string> Metadata, string IdempotencyKey);

/// <summary>A hosted page: where to send the member, and what it became once they finished.</summary>
/// <param name="Status">open, complete or expired.</param>
/// <param name="PaymentStatus">paid, unpaid (a method that takes days) or no_payment_required (saving a card).</param>
public sealed record ProviderCheckout(
    string Id, string? Url, string Mode, string? Status, string? PaymentStatus, string? PaymentIntentId, string? SetupId,
    string? CustomerId, DateTime? ExpiresAtUtc, IReadOnlyDictionary<string, string> Metadata);

/// <summary>A payment at the provider.</summary>
/// <param name="Status">succeeded, processing, requires_payment_method (failed), requires_action, canceled, …</param>
public sealed record ProviderPaymentIntent(
    string Id, string Status, decimal Amount, decimal AmountReceived, string Currency, string? LatestChargeId,
    string? MethodType, string? FailureCode, string? FailureMessage, string? CustomerId, string? PaymentMethodId,
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>A saved card or mandate, as the provider shows it: no number, only what tells cards apart.</summary>
public sealed record ProviderPaymentMethod(string Id, string Type, string? Brand, string? Last4, int? ExpMonth, int? ExpYear);

public sealed record ProviderSetup(string Id, string Status, string? CustomerId, ProviderPaymentMethod? PaymentMethod, IReadOnlyDictionary<string, string> Metadata);

/// <param name="Status">succeeded, pending, failed, canceled or requires_action.</param>
public sealed record ProviderRefund(string Id, string Status, decimal Amount, string Currency, string? PaymentIntentId);

/// <summary>A charge with the refunds made on it, as a refund made in the provider's own dashboard reports it.</summary>
public sealed record ProviderCharge(string Id, string? PaymentIntentId, string Currency, IReadOnlyList<ProviderRefund> Refunds);

/// <summary>A webhook event, its object read into whichever of these it is.</summary>
public sealed record ProviderEvent(
    string Id, string Type, string? ObjectId,
    ProviderPaymentIntent? PaymentIntent = null,
    ProviderSetup? Setup = null,
    ProviderCheckout? Checkout = null,
    ProviderCharge? Charge = null);
