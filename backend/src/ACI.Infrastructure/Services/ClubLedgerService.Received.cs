using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>
/// Money that came in from outside the club: a bank transfer matched from the bank's file, a
/// payment Stripe took. Unlike a payment entered at the desk, it is already in the club's
/// account, so it is recorded whatever the charges still owe: what pays nothing is credit.
/// </summary>
/// <remarks>
/// <para>
/// A payment entered by hand must pay exactly the charges it names, as a typing slip used to
/// lose the excess. Money from outside cannot be sent back by refusing it: a member who
/// transfers 200 against a 150 statement has paid 200. So the charges named are paid first,
/// then the member's other open charges, oldest first, and the rest stays on the account,
/// where a refund or the next charge (AppliedCredit) finds it.
/// </para>
/// <para>
/// Each is keyed by the provider's own id ("stripe:pi_…", "bank:…"), which the database holds
/// once per workspace: a webhook delivered twice, or a bank file imported twice, gets the
/// first payment back.
/// </para>
/// </remarks>
public sealed partial class ClubLedgerService
{
    /// <summary>Methods money from outside comes by.</summary>
    private static readonly string[] ReceivedMethods = ["Wire", "Card", "Twint", "DirectDebit", "CardOnFile"];

    public async Task<Result<PaymentDto>> RecordReceivedPaymentAsync(Guid? organizationId, ReceivedPaymentRequest request, CancellationToken ct = default)
    {
        if (ReadKey(request.IdempotencyKey, out var key) is { } badKey) return badKey;
        if (key == null) throw new ArgumentException("Money from outside is recorded by its provider's id, as the idempotency key.", nameof(request));
        if (Money(request.Amount) is not { } amount) return DomainErrors.Club.AmountInvalid;
        if (!ReceivedMethods.Contains(request.Method)) return DomainErrors.Club.MethodInvalid;

        // Two writes for the member at once (a payment at the desk, say) leave one to try
        // again: the money came in, so it is recorded, after the other.
        for (var attempt = 0; ; attempt++)
        {
            if (await PaymentByKeyAsync(organizationId, key, ct) is { } earlier) return await PaymentDtoAsync(organizationId, earlier, ct);
            // A member's account, or a company's or a client's (a bank transfer quoting a company
            // statement's reference pays the company's account).
            var member = await HolderAsync(organizationId, request.MemberId, ct);
            if (member == null) return DomainErrors.Club.MemberNotFound;
            var account = await AccountAsync(organizationId, member.Id, ct);
            var currency = (request.Currency ?? "").Trim().ToUpperInvariant();
            if (!string.Equals(currency, account.Currency, StringComparison.OrdinalIgnoreCase))
                return DomainErrors.Club.ReceivedCurrencyMismatch(currency, account.Currency);

            // What each open charge still owes, read after the account, in the order it is paid.
            var open = await OpenChargesAsync(organizationId, member.Id, ct);
            var first = request.PayFirst.ToList();
            var ordered = open.Where(c => first.Contains(c.Charge.Id)).OrderBy(c => first.IndexOf(c.Charge.Id))
                .Concat(open.Where(c => !first.Contains(c.Charge.Id)).OrderBy(c => c.Charge.DueAtUtc ?? c.Charge.PostedAtUtc).ThenBy(c => c.Charge.PostedAtUtc))
                .ToList();
            await _reads.ReadAsync("charges", ct);

            var payment = new ClubPayment
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                MemberId = member.Id,
                MemberName = member.Name,
                EntryType = ClubEntryTypes.Payment,
                Method = request.Method,
                Amount = amount,
                Currency = account.Currency,
                ReceivedAtUtc = request.ReceivedAtUtc == default ? DateTime.UtcNow : DateTime.SpecifyKind(request.ReceivedAtUtc, DateTimeKind.Utc),
                Reference = Trimmed(request.Reference) is { } reference ? Truncate(reference, 200) : null,
                Notes = Trimmed(request.Notes) is { } notes ? Truncate(notes, 1000) : null,
                Provider = Trimmed(request.Provider),
                ProviderPaymentId = Trimmed(request.ProviderPaymentId) is { } id ? Truncate(id, 200) : null,
                IdempotencyKey = key,
            };
            Stamp(payment);
            if (payment.CreatedByName == null && Trimmed(request.RecordedBy) is { } by) payment.CreatedByName = Truncate(by, 200);

            var allocations = new List<ClubPaymentAllocation>();
            var left = amount;
            foreach (var (charge, owed) in ordered)
            {
                if (left <= 0) break;
                var take = Math.Min(left, owed);
                allocations.Add(Allocate(organizationId, payment.Id, charge.Id, take, null));
                left -= take;
            }

            Touch(account);
            var saved = await SaveAsync(DomainErrors.Club.ChargeChanged, ct, async () =>
            {
                _db.ClubPayments.Add(payment);
                _db.ClubPaymentAllocations.AddRange(allocations);
                await StageBalanceAsync(organizationId, member, amount, ct);
            });
            if (saved.IsSuccess) return ToDto(payment, [], allocations);
            if (await PaymentByKeyAsync(organizationId, key, ct) is { } raced) return await PaymentDtoAsync(organizationId, raced, ct);
            if (attempt >= 4) return saved.Error;
        }
    }

    public async Task<Result<PaymentDto>> RecordProviderRefundAsync(Guid? organizationId, Guid paymentId, ProviderRefundEntry refund, CancellationToken ct = default)
    {
        var payment = await _db.ClubPayments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.OrganizationId == organizationId && p.ReversesPaymentId == null, ct);
        if (payment == null) return DomainErrors.Club.PaymentNotFound;
        if (await PaymentByKeyAsync(organizationId, ProviderRefundKey(refund), ct) != null) return await PaymentDtoAsync(organizationId, payment, ct);
        if (Money(refund.Amount) is not { } amount) return DomainErrors.Club.AmountInvalid;
        var why = string.IsNullOrWhiteSpace(refund.Reason) ? "Refunded" : Truncate(refund.Reason.Trim(), 500);
        return await RefundCoreAsync(organizationId, paymentId, amount, why, null, Truncate(refund.ProviderRefundId, 200), refund, ct);
    }

    /// <summary>The member's charges that still owe something, with what each owes: not voided, paid less than their amount.</summary>
    private async Task<List<(ClubCharge Charge, decimal Owed)>> OpenChargesAsync(Guid? organizationId, Guid memberId, CancellationToken ct)
    {
        var rows = await _db.ClubCharges.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && c.MemberId == memberId)
            .ToListAsync(ct);
        var voided = rows.Where(r => r.ReversesChargeId != null).Select(r => r.ReversesChargeId!.Value).ToHashSet();
        var ids = rows.Where(r => r.ReversesChargeId == null).Select(r => r.Id).ToList();
        var paid = await _db.ClubPaymentAllocations.AsNoTracking()
            .Where(a => ids.Contains(a.ChargeId))
            .GroupBy(a => a.ChargeId).Select(g => new { ChargeId = g.Key, Sum = g.Sum(a => a.Amount) })
            .ToDictionaryAsync(g => g.ChargeId, g => g.Sum, ct);
        return rows.Where(r => r.ReversesChargeId == null && !voided.Contains(r.Id) && r.Amount > 0)
            .Select(r => (Charge: r, Owed: r.Amount - paid.GetValueOrDefault(r.Id)))
            .Where(r => r.Owed > 0)
            .ToList();
    }
}
