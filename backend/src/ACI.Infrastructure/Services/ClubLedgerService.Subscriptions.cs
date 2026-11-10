using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>
/// Subscriptions: a member's dues or another recurring amount. Billing one posts a charge
/// for one period, and a period is billed once: the charge carries the subscription and
/// the period it bills, which the database will not take twice.
/// </summary>
public sealed partial class ClubLedgerService
{
    public static readonly string[] Cadences = ["Monthly", "Quarterly", "SemiAnnual", "Annual"];

    public async Task<IReadOnlyList<SubscriptionDto>> SubscriptionsAsync(Guid? organizationId, CancellationToken ct = default) =>
        (await _db.ClubSubscriptions.AsNoTracking().Where(s => s.OrganizationId == organizationId).ToListAsync(ct))
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(ToDto)
            .ToList();

    public async Task<Result<SubscriptionDto>> CreateSubscriptionAsync(Guid? organizationId, SubscriptionWriteRequest request, CancellationToken ct = default)
    {
        var member = await MemberAsync(organizationId, request.MemberId, ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;
        var name = (request.Name ?? "").Trim();
        if (name.Length == 0) return DomainErrors.Club.NameRequired;
        if (name.Length > 200) return DomainErrors.Club.TooLong("A name", 200);
        if (Money(request.Amount) is not { } amount) return DomainErrors.Club.AmountInvalid;
        var cadence = string.IsNullOrWhiteSpace(request.Cadence) ? "Monthly" : request.Cadence.Trim();
        if (!Cadences.Contains(cadence)) return DomainErrors.Club.CadenceInvalid;
        if (!ReadDate(request.NextChargeAtUtc, out var next)) return DomainErrors.Club.DateInvalid("The next billing date");
        var notes = Trimmed(request.Notes);
        if (notes?.Length > 1000) return DomainErrors.Club.TooLong("A note", 1000);

        var now = DateTime.UtcNow;
        var sub = new ClubSubscription
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = member.Id,
            MemberName = NameOf(member),
            Name = name,
            Amount = amount,
            Currency = await AccountCurrencyAsync(organizationId, member.Id, ct),
            Cadence = cadence,
            // A new subscription runs; pausing or cancelling one is a change of its own.
            Status = "Active",
            StartedAtUtc = now,
            NextChargeAtUtc = next ?? now.AddDays(CadenceDays(cadence)),
            Notes = notes,
            CreatedAtUtc = now,
            CreatedByUserId = _user.UserId,
            CreatedByName = Who(),
        };
        _db.ClubSubscriptions.Add(sub);
        var saved = await SaveAsync(DomainErrors.Club.SubscriptionChanged, ct);
        if (!saved.IsSuccess) return saved.Error;
        return ToDto(sub);
    }

    /// <remarks>
    /// Only what a person decides: the name, the amount, the cadence, the next billing date,
    /// pausing, resuming or cancelling, and the notes. The member, the start and the billing
    /// history are the subscription's own. PastDue is the billing's to say.
    /// </remarks>
    public async Task<Result<SubscriptionDto>> UpdateSubscriptionAsync(Guid? organizationId, Guid subscriptionId, SubscriptionWriteRequest request, Guid? expectedVersion = null, CancellationToken ct = default)
    {
        var sub = await _db.ClubSubscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId && s.OrganizationId == organizationId, ct);
        if (sub == null) return DomainErrors.Club.SubscriptionNotFound;
        if ((expectedVersion ?? request.Version) is { } version && version != sub.Version) return DomainErrors.Club.SubscriptionChanged;
        if (request.Status != null && request.Status.Trim() is not ("Active" or "Paused" or "Cancelled"))
            return DomainErrors.Club.SubscriptionStatusInvalid;
        if (request.Amount != null && Money(request.Amount) == null) return DomainErrors.Club.AmountInvalid;
        if (request.Cadence != null && !Cadences.Contains(request.Cadence.Trim())) return DomainErrors.Club.CadenceInvalid;
        if (!ReadDate(request.NextChargeAtUtc, out var next)) return DomainErrors.Club.DateInvalid("The next billing date");
        if (request.Name != null)
        {
            var name = request.Name.Trim();
            if (name.Length == 0) return DomainErrors.Club.NameRequired;
            if (name.Length > 200) return DomainErrors.Club.TooLong("A name", 200);
            sub.Name = name;
        }
        if (request.Notes?.Trim().Length > 1000) return DomainErrors.Club.TooLong("A note", 1000);

        if (request.Status != null) sub.Status = request.Status.Trim();
        if (Money(request.Amount) is { } amount) sub.Amount = amount;
        if (request.Cadence != null) sub.Cadence = request.Cadence.Trim();
        if (next != null) sub.NextChargeAtUtc = next;
        if (request.Notes != null) sub.Notes = Trimmed(request.Notes);
        sub.UpdatedAtUtc = DateTime.UtcNow;
        var saved = await SaveAsync(DomainErrors.Club.SubscriptionChanged, ct);
        if (!saved.IsSuccess) return saved.Error;
        return ToDto(sub);
    }

    public async Task<Result<SubscriptionDto>> BillSubscriptionAsync(Guid? organizationId, Guid subscriptionId, CancellationToken ct = default)
    {
        var sub = await _db.ClubSubscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId && s.OrganizationId == organizationId, ct);
        if (sub == null) return DomainErrors.Club.SubscriptionNotFound;
        // This used to answer 200 with the subscription as it was, and the screen said
        // "Cycle charged" for a charge that was never posted.
        if (sub.Status is "Cancelled" or "Paused") return DomainErrors.Club.SubscriptionNotBillable(sub.Status);
        var member = await MemberAsync(organizationId, sub.MemberId, ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;
        var account = await AccountAsync(organizationId, member.Id, ct);
        var now = DateTime.UtcNow;
        var period = sub.NextChargeAtUtc ?? now;
        if (await _db.ClubCharges.AnyAsync(c => c.SubscriptionId == sub.Id && c.BillingPeriodStartUtc == period, ct))
            return DomainErrors.Club.PeriodBilled;
        await _reads.ReadAsync("subscriptions", ct);

        var charge = NewCharge(organizationId, member, account, ClubEntryTypes.Charge, "Dues", sub.Name, sub.Amount);
        charge.DueAtUtc = now.AddDays(14);
        charge.SubscriptionId = sub.Id;
        charge.BillingPeriodStartUtc = period;
        Touch(account);
        sub.LastChargedAtUtc = now;
        sub.NextChargeAtUtc = period.AddDays(CadenceDays(sub.Cadence));
        sub.Status = "Active";
        sub.UpdatedAtUtc = now;

        // As with a tab: the subscription is saved first, against the copy this request
        // read, then its charge, so two presses of Bill at once post one dues charge, not two.
        var saved = await SaveAsync(DomainErrors.Club.SubscriptionChanged, ct, async () =>
        {
            _db.ClubCharges.Add(charge);
            await StageBalanceAsync(organizationId, member, -sub.Amount, ct);
        });
        if (!saved.IsSuccess) return saved.Error;
        return ToDto(sub);
    }

    public async Task<Result> DeleteSubscriptionAsync(Guid? organizationId, Guid subscriptionId, CancellationToken ct = default)
    {
        var sub = await _db.ClubSubscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId && s.OrganizationId == organizationId, ct);
        if (sub == null) return DomainErrors.Club.SubscriptionNotFound;
        // Its charges name it: deleting it would leave dues on the account that say they
        // came from a subscription nobody can find.
        if (await _db.ClubCharges.AnyAsync(c => c.SubscriptionId == sub.Id, ct)) return DomainErrors.Club.SubscriptionHasCharges;
        _db.ClubSubscriptions.Remove(sub);
        var saved = await SaveAsync(DomainErrors.Club.SubscriptionChanged, ct);
        return saved.IsSuccess ? Result.Success() : saved.Error;
    }

    /// <remarks>
    /// Days, as billing has counted them, so a "month" is 30 days. The billing run due in a
    /// later release moves this to calendar months; until then the dates stay as the club
    /// has seen them.
    /// </remarks>
    private static int CadenceDays(string cadence) => cadence switch
    {
        "Quarterly" => 91,
        "SemiAnnual" => 182,
        "Annual" => 365,
        _ => 30,
    };

    private static SubscriptionDto ToDto(ClubSubscription s) => new()
    {
        Id = s.Id,
        MemberId = s.MemberId,
        MemberName = s.MemberName,
        Name = s.Name,
        Amount = s.Amount,
        Currency = s.Currency,
        Cadence = s.Cadence,
        Status = s.Status,
        StartedAtUtc = s.StartedAtUtc,
        NextChargeAtUtc = s.NextChargeAtUtc,
        LastChargedAtUtc = s.LastChargedAtUtc,
        Notes = s.Notes,
        Version = s.Version,
    };
}
