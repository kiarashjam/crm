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
/// <remarks>
/// <para>
/// A subscription bills in advance: each charge is for the period that starts on its date
/// and runs to the same day a cadence later (see <see cref="BillingPeriods"/>), and is due
/// the club's payment terms after it is billed. There is no proration. A subscription
/// starts its own cycle on its first date, so it never bills part of a period; a new amount
/// or cadence applies from the next period billed; cancelling stops billing and refunds
/// nothing of a period already billed; and periods that start while it is paused are not
/// billed: on resuming it picks up at the next period that starts on or after that day.
/// </para>
/// </remarks>
public sealed partial class ClubLedgerService
{
    public static readonly string[] Cadences = ["Monthly", "Quarterly", "SemiAnnual", "Annual"];

    /// <summary>Kinds of catalogue item a subscription can start from.</summary>
    private static readonly string[] SubscriptionKinds = [CatalogKinds.Membership, CatalogKinds.Dues];

    public async Task<IReadOnlyList<SubscriptionDto>> SubscriptionsAsync(Guid? organizationId, CancellationToken ct = default) =>
        (await _db.ClubSubscriptions.AsNoTracking().Where(s => s.OrganizationId == organizationId).ToListAsync(ct))
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(ToDto)
            .ToList();

    public async Task<Result<SubscriptionDto>> CreateSubscriptionAsync(Guid? organizationId, SubscriptionWriteRequest request, CancellationToken ct = default)
    {
        // From the catalogue: the tier's or the dues' name, price and cadence, unless the
        // request sets its own (a member on a negotiated rate, say). It copies them, so a
        // later change of price in the catalogue does not change what this one bills.
        CatalogItemDto? item = null;
        if (request.CatalogItemId is { } itemId)
        {
            var found = await _catalog.ChargeableAsync(organizationId, itemId, SubscriptionKinds, "a subscription", ct);
            if (!found.IsSuccess) return found.Error;
            item = found.Value;
        }
        var member = await MemberAsync(organizationId, request.MemberId, ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;
        var name = (request.Name ?? "").Trim();
        if (name.Length == 0 && item != null) name = item.Kind == CatalogKinds.Membership ? $"{item.Name} dues" : item.Name;
        if (name.Length == 0) return DomainErrors.Club.NameRequired;
        if (name.Length > 200) return DomainErrors.Club.TooLong("A name", 200);
        if (Money(request.Amount ?? item?.Price) is not { } amount) return DomainErrors.Club.AmountInvalid;
        var cadence = string.IsNullOrWhiteSpace(request.Cadence) ? item?.Cadence ?? "Monthly" : request.Cadence.Trim();
        if (!Cadences.Contains(cadence)) return DomainErrors.Club.CadenceInvalid;
        if (!ReadDate(request.NextChargeAtUtc, out var next)) return DomainErrors.Club.DateInvalid("The next billing date");
        var notes = Trimmed(request.Notes);
        if (notes?.Length > 1000) return DomainErrors.Club.TooLong("A note", 1000);
        var currency = await AccountCurrencyAsync(organizationId, member.Id, ct);
        if (item != null && !string.Equals(item.Currency, currency, StringComparison.OrdinalIgnoreCase))
            return DomainErrors.Club.CurrencyMismatch(item.Currency, currency);

        var now = DateTime.UtcNow;
        // Dues are billed in advance: the first period starts on the day the subscription
        // does, unless another first date is given. It used to start a cycle later (thirty
        // days on), so a member's first month was never billed.
        var first = next ?? DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
        var sub = new ClubSubscription
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = member.Id,
            MemberName = NameOf(member),
            Name = name,
            Amount = amount,
            Currency = currency,
            Cadence = cadence,
            // A new subscription runs; pausing or cancelling one is a change of its own.
            Status = "Active",
            StartedAtUtc = now,
            NextChargeAtUtc = first,
            BillingAnchorDay = first.Day,
            CatalogItemId = item?.Id,
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

        var now = DateTime.UtcNow;
        var resuming = sub.Status == "Paused" && request.Status?.Trim() == "Active";
        if (request.Status != null) sub.Status = request.Status.Trim();
        if (Money(request.Amount) is { } amount) sub.Amount = amount;
        if (request.Cadence != null) sub.Cadence = request.Cadence.Trim();
        if (next != null)
        {
            // A first date picked by hand starts the cycle again on its day.
            sub.NextChargeAtUtc = next;
            sub.BillingAnchorDay = next.Value.Day;
        }
        else if (resuming && sub.NextChargeAtUtc is { } due && due < now)
        {
            // Periods that started while it was paused are not billed: it picks up at the
            // next period that starts from today. Left where it was, the next billing run
            // would have billed every month of the pause.
            var anchor = sub.BillingAnchorDay ?? due.Day;
            sub.NextChargeAtUtc = BillingPeriods.FirstOnOrAfter(due, sub.Cadence, anchor, DateTime.SpecifyKind(now.Date, DateTimeKind.Utc));
            sub.BillingAnchorDay = anchor;
        }
        if (request.Notes != null) sub.Notes = Trimmed(request.Notes);
        sub.UpdatedAtUtc = now;
        var saved = await SaveAsync(DomainErrors.Club.SubscriptionChanged, ct);
        if (!saved.IsSuccess) return saved.Error;
        return ToDto(sub);
    }

    public Task<Result<SubscriptionDto>> BillSubscriptionAsync(Guid? organizationId, Guid subscriptionId, CancellationToken ct = default) =>
        BillSubscriptionAsync(organizationId, subscriptionId, early: false, ct);

    public async Task<Result<SubscriptionDto>> BillSubscriptionAsync(Guid? organizationId, Guid subscriptionId, bool early, CancellationToken ct = default)
    {
        // The club's payment terms, read before anything here is changed.
        var rules = await _rules.ReadAsync(organizationId, ct);
        var sub = await _db.ClubSubscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId && s.OrganizationId == organizationId, ct);
        if (sub == null) return DomainErrors.Club.SubscriptionNotFound;
        // This used to answer 200 with the subscription as it was, and the screen said
        // "Cycle charged" for a charge that was never posted.
        if (sub.Status is "Cancelled" or "Paused") return DomainErrors.Club.SubscriptionNotBillable(sub.Status);
        var member = await MemberAsync(organizationId, sub.MemberId, ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;
        var now = DateTime.UtcNow;
        var period = sub.NextChargeAtUtc ?? now;
        if (period > now)
        {
            // Bill pressed on an annual subscription 277 days early, or pressed twice, used to
            // charge the member for a year, and the next year with the second press. A period
            // is billed once it has started; early, only when asked, and one period ahead at most.
            if (!early) return DomainErrors.Club.PeriodNotStarted(period);
            if (await _db.ClubCharges.AnyAsync(c => c.SubscriptionId == sub.Id && c.ReversesChargeId == null && c.BillingPeriodStartUtc > now, ct))
                return DomainErrors.Club.BilledAhead;
        }
        var billed = await BillPeriodAsync(organizationId, sub, member, period, now, rules.Billing.PaymentTermsDays,
            await VatRateOfAsync(organizationId, sub, ct), ct);
        if (!billed.IsSuccess) return billed.Error;
        return ToDto(sub);
    }

    public async Task<Result<IReadOnlyList<ChargeDto>>> BillDueAsync(Guid? organizationId, Guid subscriptionId, DateTime asOfUtc, int maxPeriods, CancellationToken ct = default)
    {
        var rules = await _rules.ReadAsync(organizationId, ct);
        var posted = new List<ChargeDto>();
        decimal? vatRate = null;
        var vatRead = false;
        // A few more tries than periods: a period someone billed at the same moment is read
        // again and skipped rather than ending the subscription's billing for the night.
        for (var attempt = 0; posted.Count < maxPeriods && attempt < maxPeriods + 3; attempt++)
        {
            var sub = await _db.ClubSubscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId && s.OrganizationId == organizationId, ct);
            if (sub == null) return DomainErrors.Club.SubscriptionNotFound;
            if (sub.Status is not ("Active" or "PastDue")) break;
            var period = sub.NextChargeAtUtc ?? asOfUtc;
            if (period > asOfUtc) break;
            var member = await MemberAsync(organizationId, sub.MemberId, ct);
            if (member == null) return DomainErrors.Club.MemberNotFound;
            if (!vatRead)
            {
                vatRate = await VatRateOfAsync(organizationId, sub, ct);
                vatRead = true;
            }
            var now = DateTime.UtcNow;
            var billed = await BillPeriodAsync(organizationId, sub, member, period, now, rules.Billing.PaymentTermsDays, vatRate, ct);
            if (billed.IsSuccess)
            {
                posted.Add(ToDto(billed.Value, null, default, now));
                continue;
            }
            if (billed.Error.Code == DomainErrors.Club.PeriodBilled.Code)
            {
                // The period has its charge, and the date did not move on with it (two saves
                // that did not finish together): move it on now, and carry on from there.
                var tracked = await _db.ClubSubscriptions.FirstAsync(s => s.Id == subscriptionId, ct);
                var anchor = tracked.BillingAnchorDay ?? period.Day;
                tracked.NextChargeAtUtc = BillingPeriods.Next(period, tracked.Cadence, anchor);
                tracked.BillingAnchorDay = anchor;
                tracked.UpdatedAtUtc = now;
                await SaveAsync(DomainErrors.Club.SubscriptionChanged, ct);
                continue;
            }
            // Changed at the same moment by someone else: read it again.
            if (billed.Error.Code.EndsWith(".Conflict", StringComparison.Ordinal)) continue;
            return billed.Error;
        }
        return posted;
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

    /// <summary>
    /// Posts the charge for the period that starts at <paramref name="period"/> and moves the
    /// subscription on to the next one. The subscription is saved first, against the copy this
    /// request read, then its charge, so two bills of one period at once post one charge.
    /// </summary>
    private async Task<Result<ClubCharge>> BillPeriodAsync(Guid? organizationId, ClubSubscription sub, ClubMember member, DateTime period,
        DateTime now, int paymentTermsDays, decimal? vatRate, CancellationToken ct)
    {
        var account = await AccountAsync(organizationId, member.Id, ct);
        if (await _db.ClubCharges.AnyAsync(c => c.SubscriptionId == sub.Id && c.BillingPeriodStartUtc == period, ct))
            return DomainErrors.Club.PeriodBilled;
        await _reads.ReadAsync("subscriptions", ct);

        var anchor = sub.BillingAnchorDay ?? period.Day;
        var next = BillingPeriods.Next(period, sub.Cadence, anchor);
        var charge = NewCharge(organizationId, member, account, ClubEntryTypes.Charge, "Dues",
            Truncate($"{sub.Name} · {BillingPeriods.Label(period, next)}", 500), sub.Amount);
        // Due the club's payment terms after it is billed, or after its period starts when
        // it is billed early.
        charge.DueAtUtc = (period > now ? period : now).AddDays(paymentTermsDays);
        charge.SubscriptionId = sub.Id;
        charge.BillingPeriodStartUtc = period;
        charge.CatalogItemId = sub.CatalogItemId;
        charge.VatRate = vatRate;
        Touch(account);
        sub.BillingAnchorDay = anchor;
        sub.LastChargedAtUtc = now;
        sub.NextChargeAtUtc = next;
        sub.UpdatedAtUtc = now;
        // A past-due subscription stays past due when its next period is billed: the billing
        // run sets it active again once its dues are paid, not when more are added.

        var saved = await SaveAsync(DomainErrors.Club.SubscriptionChanged, ct, async () =>
        {
            _db.ClubCharges.Add(charge);
            await StageBalanceAsync(organizationId, member, -sub.Amount, ct);
        });
        return saved.IsSuccess ? charge : saved.Error;
    }

    /// <summary>
    /// The VAT rate a subscription's dues carry: its catalogue item's rate now (a rate is the
    /// law's at the time, where the price is what the member agreed), or none when it was not
    /// started from the catalogue.
    /// </summary>
    private async Task<decimal?> VatRateOfAsync(Guid? organizationId, ClubSubscription sub, CancellationToken ct) =>
        sub.CatalogItemId is { } itemId && await _catalog.GetAsync(organizationId, itemId, ct) is { IsSuccess: true } found
            ? found.Value.VatRate
            : null;

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
        CatalogItemId = s.CatalogItemId,
        Notes = s.Notes,
        Version = s.Version,
    };
}
