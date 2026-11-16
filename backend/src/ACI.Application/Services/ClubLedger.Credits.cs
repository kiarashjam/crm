using ACI.Application.Common;

namespace ACI.Application.Services;

public partial interface IClubLedger
{
    /// <summary>
    /// Puts a credit of <paramref name="amount"/> on the member's account, as an entry of its
    /// own (an Adjustment credit, which the statement shows and revenue leaves out), saying what
    /// it is for: a referral bonus the club's rules give, say. Never twice for one
    /// <paramref name="idempotencyKey"/>: the same key gets the first credit back.
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="AdjustBalanceAsync"/>, which sets a balance and so reads it first, this
    /// adds the amount whatever the balance is, so two credits for different reasons at the same
    /// moment both land.
    /// </remarks>
    Task<Result<PaymentDto>> CreditAsync(Guid? organizationId, Guid memberId, decimal amount, string description, string idempotencyKey,
        CancellationToken ct = default);
}
