using System.Globalization;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace ACI.Infrastructure.Persistence;

/// <summary>
/// Club money in the audit trail: charges, payments, gift cards, tabs, subscriptions,
/// donations, and balances set by hand.
/// </summary>
/// <remarks>
/// <para>
/// The ledger only adds rows, so most lines come from a row being added: a charge posted,
/// a payment recorded, a void or a refund (written on the record it takes back), an
/// allocation (written on the charge it pays, as "Status changed from Pending to
/// Partially paid"), a gift card transaction or a tab item (written on the card or the
/// tab). The records that still change, a tab, a subscription, a gift card's status and a
/// donation's thank-you, add lines when they do. Each line is on the record a person
/// would look it up by, so a charge's history says who paid it and who voided it.
/// </para>
/// <para>
/// As with the other records, only what a person would call a money event writes a line.
/// Bookkeeping that moves by itself (the house account row, a balance kept for the lists,
/// the next billing date) does not.
/// </para>
/// </remarks>
internal static partial class AuditTrail
{
    private static readonly Spec ChargeSpec = new("charge", "Charge", nameof(ClubCharge.Description),
        [nameof(ClubCharge.Description)], e => Join(Value(e, nameof(ClubCharge.Description)), Value(e, nameof(ClubCharge.MemberName))));

    private static readonly Spec PaymentSpec = new("payment", "Payment", nameof(ClubPayment.MemberName),
        [nameof(ClubPayment.Reference)], e => Join(Value(e, nameof(ClubPayment.MemberName)), Value(e, nameof(ClubPayment.Method))));

    private static readonly Spec GiftCardSpec = new("gift_card", "Gift card", nameof(ClubGiftCard.Code),
        [nameof(ClubGiftCard.RecipientName), nameof(ClubGiftCard.RecipientEmail), nameof(ClubGiftCard.PurchaserName), nameof(ClubGiftCard.ExpiresAtUtc)],
        e => Join(Value(e, nameof(ClubGiftCard.Code)), Value(e, nameof(ClubGiftCard.RecipientName))));

    private static readonly Spec TabSpec = new("tab", "Tab", nameof(ClubTab.Venue),
        [nameof(ClubTab.MemberId), nameof(ClubTab.Venue)], e => Join(Value(e, nameof(ClubTab.Venue)), Value(e, nameof(ClubTab.MemberName))));

    private static readonly Spec SubscriptionSpec = new("subscription", "Subscription", nameof(ClubSubscription.Name),
        [nameof(ClubSubscription.Name), nameof(ClubSubscription.Amount), nameof(ClubSubscription.Cadence), nameof(ClubSubscription.MemberId), nameof(ClubSubscription.Notes)],
        e => Join(Value(e, nameof(ClubSubscription.Name)), Value(e, nameof(ClubSubscription.MemberName))));

    private static readonly Spec DonationSpec = new("donation", "Donation", nameof(ClubDonation.Cause),
        [nameof(ClubDonation.Amount), nameof(ClubDonation.Cause), nameof(ClubDonation.MemberId)],
        e => Join(Value(e, nameof(ClubDonation.Cause)), Value(e, nameof(ClubDonation.MemberName))));

    private static readonly Spec MemberBalanceSpec = new("member", "Member", nameof(ClubCharge.MemberName), [],
        e => Value(e, nameof(ClubCharge.MemberName)));

    private static readonly Dictionary<string, string> LedgerFieldNames = new(StringComparer.Ordinal)
    {
        [nameof(ClubGiftCard.ExpiresAtUtc)] = "expiry date",
        [nameof(ClubGiftCard.RecipientName)] = "recipient",
        [nameof(ClubGiftCard.RecipientEmail)] = "recipient email",
        [nameof(ClubGiftCard.PurchaserName)] = "purchaser",
        [nameof(ClubTab.MemberId)] = "member",
    };

    /// <summary>What the lines about a charge, a gift card or a tab need to know about it, read once per save.</summary>
    private sealed record ChargeFacts(decimal Amount, string Description, string MemberName, decimal PaidBefore);
    private sealed record CardFacts(string Code, string RecipientName, decimal LeftBefore);
    private sealed record TabFacts(string Venue, string MemberName);

    /// <summary>Lines for a row of the money ledger. False when the entry is not one.</summary>
    private static bool CollectLedger(EntityEntry entry, List<Pending> pending, Lookups lookups)
    {
        switch (entry.Entity)
        {
            case ClubCharge charge when entry.State == EntityState.Added:
                CollectChargeRow(entry, charge, pending);
                return true;
            case ClubPayment payment when entry.State == EntityState.Added:
                CollectPaymentRow(entry, payment, pending);
                return true;
            case ClubPaymentAllocation allocation when entry.State == EntityState.Added:
                lookups.ChargeIds.Add(allocation.ChargeId);
                // One line per charge for all the allocations this save adds to it.
                if (!pending.Any(p => p.Entry.Entity is ClubPaymentAllocation a && a.ChargeId == allocation.ChargeId))
                    pending.Add(new(entry, ChargeSpec, "status_changed", l => ChargeStatusLine(l, allocation.ChargeId),
                        allocation.ChargeId, l => l.Charges.TryGetValue(allocation.ChargeId, out var c) ? Join(c.Description, c.MemberName) : null));
                return true;
            case ClubGiftCard card:
                CollectGiftCard(entry, card, pending);
                return true;
            case ClubGiftCardTransaction transaction when entry.State == EntityState.Added:
                CollectCardTransaction(entry, transaction, pending, lookups);
                return true;
            case ClubTab tab:
                CollectTab(entry, tab, pending);
                return true;
            case ClubTabItem item:
                CollectTabItem(entry, item, pending, lookups);
                return true;
            case ClubSubscription:
                CollectSubscription(entry, pending);
                return true;
            case ClubDonation donation:
                CollectDonation(entry, donation, pending);
                return true;
            case ClubCharge or ClubPayment or ClubPaymentAllocation or ClubGiftCardTransaction or ClubHouseAccount:
                // Ledger rows are only ever added; the house account row is bookkeeping.
                return true;
            default:
                return false;
        }
    }

    private static void CollectChargeRow(EntityEntry entry, ClubCharge charge, List<Pending> pending)
    {
        switch (charge.EntryType)
        {
            case ClubEntryTypes.Void when charge.ReversesChargeId is { } original:
                pending.Add(new(entry, ChargeSpec, "voided", _ => VoidWords(charge.Reason), original));
                break;
            case ClubEntryTypes.Opening or ClubEntryTypes.Adjustment:
                pending.Add(new(entry, MemberBalanceSpec, "updated", _ => BalanceWords(charge.EntryType, -charge.Amount, charge.Description), charge.MemberId));
                break;
            default:
                pending.Add(new(entry, ChargeSpec, "created", _ => $"Charge of {Money(charge.Amount)} posted"));
                break;
        }
    }

    private static void CollectPaymentRow(EntityEntry entry, ClubPayment payment, List<Pending> pending)
    {
        switch (payment.EntryType)
        {
            case ClubEntryTypes.Void when payment.ReversesPaymentId is { } original:
                pending.Add(new(entry, PaymentSpec, "voided", _ => VoidWords(payment.Reason), original));
                break;
            case ClubEntryTypes.Refund when payment.ReversesPaymentId is { } refunded:
                pending.Add(new(entry, PaymentSpec, "updated", _ => $"Refunded {Money(-payment.Amount)}: {payment.Reason}", refunded));
                break;
            case ClubEntryTypes.Opening or ClubEntryTypes.Adjustment:
                pending.Add(new(entry, MemberBalanceSpec, "updated", _ => BalanceWords(payment.EntryType, payment.Amount, payment.Description), payment.MemberId));
                break;
            default:
                pending.Add(new(entry, PaymentSpec, "created", _ => $"Payment of {Money(payment.Amount)} recorded"));
                break;
        }
    }

    private static string BalanceWords(string entryType, decimal change, string? description) =>
        entryType == ClubEntryTypes.Opening
            ? $"Opening balance: {Money(Math.Abs(change))} {(change < 0 ? "owed" : "in credit")}"
            : description ?? $"House balance changed by hand by {Money(change)}";

    /// <summary>"Status changed from Pending to Partially paid; paid 40.00 of 100.00", from the allocations this save adds.</summary>
    private static string ChargeStatusLine(Lookups lookups, Guid chargeId)
    {
        if (!lookups.Charges.TryGetValue(chargeId, out var charge)) return "Payment applied";
        var after = charge.PaidBefore + lookups.AddedAllocations.GetValueOrDefault(chargeId);
        var (from, to) = (PaidStatus(charge.PaidBefore, charge.Amount), PaidStatus(after, charge.Amount));
        var paid = $"paid {Money(after)} of {Money(charge.Amount)}";
        return from != to ? $"{Moved("Status", Pretty(from), Pretty(to))}; {paid}" : Sentence(paid);
    }

    private static string PaidStatus(decimal paid, decimal amount) =>
        paid <= 0 ? "Pending" : paid >= amount ? "Paid" : "PartiallyPaid";

    private static void CollectGiftCard(EntityEntry entry, ClubGiftCard card, List<Pending> pending)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                pending.Add(new(entry, GiftCardSpec, "created", _ => $"Gift card issued for {Money(card.FaceValue)}"));
                break;
            case EntityState.Modified:
                // A void is told by its transaction, which says why and what it wrote off.
                AddEdited(entry, GiftCardSpec, pending);
                break;
        }
    }

    private static void CollectCardTransaction(EntityEntry entry, ClubGiftCardTransaction transaction, List<Pending> pending, Lookups lookups)
    {
        var cardId = transaction.GiftCardId;
        string? Label(Lookups l) => l.Cards.TryGetValue(cardId, out var c) ? Join(c.Code, c.RecipientName) : null;
        switch (transaction.Type)
        {
            case GiftCardTransactionTypes.Redeem:
                lookups.CardIds.Add(cardId);
                pending.Add(new(entry, GiftCardSpec, "updated", l => $"Redeemed {Money(-transaction.Amount)}, {Money(LeftAfter(l, cardId))} left", cardId, Label));
                break;
            case GiftCardTransactionTypes.TopUp:
                lookups.CardIds.Add(cardId);
                pending.Add(new(entry, GiftCardSpec, "updated", l => $"Topped up {Money(transaction.Amount)}, {Money(LeftAfter(l, cardId))} left", cardId, Label));
                break;
            case GiftCardTransactionTypes.Expiry:
                lookups.CardIds.Add(cardId);
                pending.Add(new(entry, GiftCardSpec, "status_changed", _ => $"Expired: {Money(-transaction.Amount)} written off", cardId, Label));
                break;
            case GiftCardTransactionTypes.Void:
                lookups.CardIds.Add(cardId);
                var written = transaction.Amount < 0 ? $" ({Money(-transaction.Amount)} written off)" : "";
                pending.Add(new(entry, GiftCardSpec, "voided", _ => VoidWords(transaction.Reason) + written, cardId, Label));
                break;
            // Issue: the card's own "created" line says it.
        }
    }

    private static decimal LeftAfter(Lookups lookups, Guid cardId) =>
        (lookups.Cards.TryGetValue(cardId, out var card) ? card.LeftBefore : 0) + lookups.AddedCardValue.GetValueOrDefault(cardId);

    private static void CollectTab(EntityEntry entry, ClubTab tab, List<Pending> pending)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                pending.Add(new(entry, TabSpec, "created", _ => "Tab opened"));
                break;
            case EntityState.Modified:
                var was = entry.Property(nameof(ClubTab.Status)).OriginalValue as string ?? "";
                if (was != tab.Status)
                {
                    if (tab.Status == "Voided") pending.Add(new(entry, TabSpec, "voided", _ => VoidWords(tab.VoidReason)));
                    else if (tab.Status == "Closed") pending.Add(new(entry, TabSpec, "status_changed", _ => $"Closed: {Money(tab.Total)} posted to the house account"));
                    else pending.Add(new(entry, TabSpec, "status_changed", _ => Moved("Status", Pretty(was), Pretty(tab.Status))));
                }
                AddEdited(entry, TabSpec, pending);
                break;
        }
    }

    private static void CollectTabItem(EntityEntry entry, ClubTabItem item, List<Pending> pending, Lookups lookups)
    {
        var tabId = item.TabId;
        string? Label(Lookups l) => l.Tabs.TryGetValue(tabId, out var t) ? Join(t.Venue, t.MemberName) : null;
        var line = $"{item.Name} × {item.Quantity.ToString("0.##", CultureInfo.InvariantCulture)} at {Money(item.UnitPrice)}";
        if (entry.State == EntityState.Added)
        {
            lookups.TabIds.Add(tabId);
            pending.Add(new(entry, TabSpec, "updated", _ => $"Added {line}", tabId, Label));
        }
        else if (entry.State == EntityState.Modified && entry.Property(nameof(ClubTabItem.RemovedAtUtc)).OriginalValue == null && item.RemovedAtUtc != null)
        {
            lookups.TabIds.Add(tabId);
            pending.Add(new(entry, TabSpec, "updated", _ => $"Removed {line}", tabId, Label));
        }
    }

    private static void CollectSubscription(EntityEntry entry, List<Pending> pending)
    {
        var sub = (ClubSubscription)entry.Entity;
        switch (entry.State)
        {
            case EntityState.Added:
                pending.Add(new(entry, SubscriptionSpec, "created", _ => $"Subscription of {Money(sub.Amount)} {Words(sub.Cadence)} created"));
                break;
            case EntityState.Deleted:
                pending.Add(new(entry, SubscriptionSpec, "deleted", _ => $"Subscription deleted ({Money(sub.Amount)} {Words(sub.Cadence)})"));
                break;
            case EntityState.Modified:
                var was = entry.Property(nameof(ClubSubscription.Status)).OriginalValue as string ?? "";
                if (was != sub.Status) pending.Add(new(entry, SubscriptionSpec, "status_changed", _ => Moved("Status", Pretty(was), Pretty(sub.Status))));
                AddEdited(entry, SubscriptionSpec, pending);
                break;
        }
    }

    private static void CollectDonation(EntityEntry entry, ClubDonation donation, List<Pending> pending)
    {
        switch (entry.State)
        {
            case EntityState.Added when donation.ReversesDonationId is { } original:
                pending.Add(new(entry, DonationSpec, "voided", _ => VoidWords(donation.Reason), original));
                break;
            case EntityState.Added:
                pending.Add(new(entry, DonationSpec, "created", _ => $"Donation of {Money(donation.Amount)} received"));
                break;
            case EntityState.Modified:
                if (donation.Acknowledged && entry.Property(nameof(ClubDonation.Acknowledged)).OriginalValue is false)
                    pending.Add(new(entry, DonationSpec, "status_changed", _ => "Acknowledged"));
                AddEdited(entry, DonationSpec, pending);
                break;
        }
    }

    /// <summary>"Updated recipient and expiry date": the fields of the spec this save changed.</summary>
    private static void AddEdited(EntityEntry entry, Spec spec, List<Pending> pending)
    {
        var edited = spec.Fields.Where(f => Differs(entry.Property(f)))
            .Select(f => LedgerFieldNames.TryGetValue(f, out var name) ? name : FieldName(f)).ToList();
        if (edited.Count > 0) pending.Add(new(entry, spec, "updated", _ => $"Updated {JoinList(edited)}"));
    }

    /// <summary>
    /// Fills in the facts the money lines need: each charge an allocation pays (its amount
    /// and what it was paid before this save), each gift card a transaction moves (what was
    /// left on it before), each tab an item is added to or taken off.
    /// </summary>
    private static async Task FillLedgerAsync(AppDbContext db, Lookups lookups, CancellationToken ct)
    {
        if (lookups.ChargeIds.Count > 0)
        {
            var ids = lookups.ChargeIds.ToList();
            var charges = await db.ClubCharges.AsNoTracking().IgnoreQueryFilters().Where(c => ids.Contains(c.Id))
                .Select(c => new { c.Id, c.Amount, c.Description, c.MemberName }).ToListAsync(ct);
            var paid = await db.ClubPaymentAllocations.AsNoTracking().IgnoreQueryFilters().Where(a => ids.Contains(a.ChargeId))
                .Select(a => new { a.ChargeId, a.Amount }).ToListAsync(ct);
            FillCharges(db, lookups, charges.Select(c => (c.Id, c.Amount, c.Description, c.MemberName)),
                paid.GroupBy(p => p.ChargeId).ToDictionary(g => g.Key, g => g.Sum(p => p.Amount)));
        }
        if (lookups.CardIds.Count > 0)
        {
            var ids = lookups.CardIds.ToList();
            var cards = await db.ClubGiftCards.AsNoTracking().IgnoreQueryFilters().Where(c => ids.Contains(c.Id))
                .Select(c => new { c.Id, c.Code, c.RecipientName }).ToListAsync(ct);
            var left = await db.ClubGiftCardTransactions.AsNoTracking().IgnoreQueryFilters().Where(t => ids.Contains(t.GiftCardId))
                .Select(t => new { t.GiftCardId, t.Amount }).ToListAsync(ct);
            FillCards(db, lookups, cards.Select(c => (c.Id, c.Code, c.RecipientName)),
                left.GroupBy(l => l.GiftCardId).ToDictionary(g => g.Key, g => g.Sum(l => l.Amount)));
        }
        if (lookups.TabIds.Count > 0)
        {
            var ids = lookups.TabIds.ToList();
            var tabs = await db.ClubTabs.AsNoTracking().IgnoreQueryFilters().Where(t => ids.Contains(t.Id))
                .Select(t => new { t.Id, t.Venue, t.MemberName }).ToListAsync(ct);
            foreach (var t in tabs) lookups.Tabs[t.Id] = new TabFacts(t.Venue, t.MemberName);
        }
    }

    /// <inheritdoc cref="FillLedgerAsync"/>
    private static void FillLedger(AppDbContext db, Lookups lookups)
    {
        if (lookups.ChargeIds.Count > 0)
        {
            var ids = lookups.ChargeIds.ToList();
            var charges = db.ClubCharges.AsNoTracking().IgnoreQueryFilters().Where(c => ids.Contains(c.Id))
                .Select(c => new { c.Id, c.Amount, c.Description, c.MemberName }).ToList();
            var paid = db.ClubPaymentAllocations.AsNoTracking().IgnoreQueryFilters().Where(a => ids.Contains(a.ChargeId))
                .Select(a => new { a.ChargeId, a.Amount }).ToList();
            FillCharges(db, lookups, charges.Select(c => (c.Id, c.Amount, c.Description, c.MemberName)),
                paid.GroupBy(p => p.ChargeId).ToDictionary(g => g.Key, g => g.Sum(p => p.Amount)));
        }
        if (lookups.CardIds.Count > 0)
        {
            var ids = lookups.CardIds.ToList();
            var cards = db.ClubGiftCards.AsNoTracking().IgnoreQueryFilters().Where(c => ids.Contains(c.Id))
                .Select(c => new { c.Id, c.Code, c.RecipientName }).ToList();
            var left = db.ClubGiftCardTransactions.AsNoTracking().IgnoreQueryFilters().Where(t => ids.Contains(t.GiftCardId))
                .Select(t => new { t.GiftCardId, t.Amount }).ToList();
            FillCards(db, lookups, cards.Select(c => (c.Id, c.Code, c.RecipientName)),
                left.GroupBy(l => l.GiftCardId).ToDictionary(g => g.Key, g => g.Sum(l => l.Amount)));
        }
        if (lookups.TabIds.Count > 0)
        {
            var ids = lookups.TabIds.ToList();
            foreach (var t in db.ClubTabs.AsNoTracking().IgnoreQueryFilters().Where(t => ids.Contains(t.Id)).Select(t => new { t.Id, t.Venue, t.MemberName }).ToList())
                lookups.Tabs[t.Id] = new TabFacts(t.Venue, t.MemberName);
        }
    }

    // The ids came from rows this save writes, in its own workspace, so the reads above look
    // past the workspace filter only because a save made with none (the migration's tools,
    // a test) would otherwise find nothing to describe.
    private static void FillCharges(AppDbContext db, Lookups lookups, IEnumerable<(Guid Id, decimal Amount, string Description, string MemberName)> saved, Dictionary<Guid, decimal> paid)
    {
        foreach (var (id, amount, description, memberName) in saved)
            lookups.Charges[id] = new ChargeFacts(amount, description, memberName, paid.GetValueOrDefault(id));
        // A charge posted in the same save as its payment is not in the database yet.
        foreach (var added in db.ChangeTracker.Entries<ClubCharge>().Where(e => e.State == EntityState.Added && lookups.ChargeIds.Contains(e.Entity.Id)))
            lookups.Charges[added.Entity.Id] = new ChargeFacts(added.Entity.Amount, added.Entity.Description, added.Entity.MemberName, 0);
        foreach (var allocation in db.ChangeTracker.Entries<ClubPaymentAllocation>().Where(e => e.State == EntityState.Added))
            lookups.AddedAllocations[allocation.Entity.ChargeId] = lookups.AddedAllocations.GetValueOrDefault(allocation.Entity.ChargeId) + allocation.Entity.Amount;
    }

    private static void FillCards(AppDbContext db, Lookups lookups, IEnumerable<(Guid Id, string Code, string RecipientName)> saved, Dictionary<Guid, decimal> left)
    {
        foreach (var (id, code, recipient) in saved)
            lookups.Cards[id] = new CardFacts(code, recipient, left.GetValueOrDefault(id));
        foreach (var transaction in db.ChangeTracker.Entries<ClubGiftCardTransaction>().Where(e => e.State == EntityState.Added))
            lookups.AddedCardValue[transaction.Entity.GiftCardId] = lookups.AddedCardValue.GetValueOrDefault(transaction.Entity.GiftCardId) + transaction.Entity.Amount;
    }

    private static string VoidWords(string? reason) => string.IsNullOrWhiteSpace(reason) ? "Voided" : $"Voided: {reason}";

    private static string? Value(EntityEntry entry, string property) =>
        entry.Property(property).CurrentValue?.ToString() is { Length: > 0 } text ? text : null;

    private static string? Join(params string?[] parts)
    {
        var present = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
        return present.Length == 0 ? null : string.Join(" — ", present);
    }

    private static string Money(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture);

    private static string Pretty(string status) => status.Length == 0 ? status : Sentence(Words(status));
}
