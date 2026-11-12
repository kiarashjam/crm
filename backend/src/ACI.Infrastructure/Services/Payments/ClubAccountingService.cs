using ACI.Application.Common;
using ACI.Application.Common.Payments;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services.Payments;

/// <inheritdoc cref="IClubAccounting"/>
/// <remarks>
/// <para>
/// The journal is the ledger's entries as double-entry bookings, each with one account debited
/// and one credited for the same amount, in the accounts the club set (Swiss SME chart by
/// default):
/// </para>
/// <list type="bullet">
/// <item>A charge: receivables to its kind's revenue account, with its VAT rate (gross, with the
/// VAT code; or, in the net layout, the VAT on a line of its own to VAT owed). Its void books
/// the same back. A balance brought over books against the opening balances account, one set
/// by hand against corrections.</item>
/// <item>Money received: the bank, cash or card clearing account to receivables for what it
/// paid when it came in, and to customer prepayments for what it left as credit. Credit used
/// later for a charge moves from prepayments to receivables. A void or a refund books the money
/// back out, to receivables for the charges that owe again.</item>
/// <item>Gift cards: the sale to the gift card liability, a card spent or expired out of it.</item>
/// </list>
/// <para>
/// Every entry is booked in the period it counts in, and an allocation made with its payment or
/// its reversal is booked with it whatever its own time, so the journals of two periods add up
/// to the journal of both: receivables then equal what open charges owe, and prepayments the
/// credit members hold.
/// </para>
/// </remarks>
public sealed class ClubAccountingService : IClubAccounting
{
    public const string Gross = "gross";
    public const string Net = "net";

    /// <summary>An allocation saved within this of its payment was made with it, in the same request.</summary>
    private static readonly TimeSpan SameWrite = TimeSpan.FromSeconds(10);

    private readonly AppDbContext _db;
    private readonly ClubPaymentSettingsService _settings;

    public ClubAccountingService(AppDbContext db, ClubPaymentSettingsService settings)
    {
        _db = db;
        _settings = settings;
    }

    public async Task<Result<JournalDto>> JournalAsync(Guid organizationId, DateTime fromUtc, DateTime toUtc, string layout, CancellationToken ct = default)
    {
        layout = string.IsNullOrWhiteSpace(layout) ? Gross : layout.Trim().ToLowerInvariant();
        if (layout is not (Gross or Net)) return DomainErrors.Payments.JournalLayoutInvalid;
        if (fromUtc >= toUtc || toUtc - fromUtc > TimeSpan.FromDays(367)) return DomainErrors.Payments.JournalPeriodInvalid;
        var config = await _settings.ConfigAsync(organizationId, ct);
        var accounts = config.Accounts;
        var zone = RegionalSettings.ZoneOf(config.TimeZone);
        DateOnly Day(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone));
        var rows = new List<JournalRowDto>();

        void Book(DateTime at, string prefix, Guid id, string debit, string credit, decimal amount, string currency, string text, string? member, string type,
            decimal? vatRate = null)
        {
            if (amount == 0) return;
            if (amount < 0) (debit, credit, amount) = (credit, debit, -amount);
            decimal? vat = vatRate is > 0 ? Math.Round(amount * vatRate.Value / (100 + vatRate.Value), 2, MidpointRounding.AwayFromZero) : null;
            var row = new JournalRowDto
            {
                DateUtc = at, Date = Day(at), Document = $"{prefix}-{id.ToString("N")[..8].ToUpperInvariant()}", Debit = debit, Credit = credit,
                Amount = amount, Currency = currency, Text = Clip(text, 250), Member = member, Type = type, EntryId = id,
            };
            if (vat is not { } vatAmount)
            {
                rows.Add(row);
                return;
            }
            if (layout == Gross)
            {
                rows.Add(row with { VatCode = accounts.VatCodeFor(vatRate), VatRate = vatRate, VatAmount = vatAmount });
                return;
            }
            // Net: the revenue side without its VAT, and the VAT on a line of its own to VAT owed.
            var revenueIsCredit = credit != accounts.Receivables;
            rows.Add(row with { Amount = amount - vatAmount, VatRate = vatRate });
            rows.Add(row with
            {
                Debit = revenueIsCredit ? debit : accounts.VatOwed,
                Credit = revenueIsCredit ? accounts.VatOwed : credit,
                Amount = vatAmount, VatRate = vatRate, VatAmount = vatAmount, Type = type + "Vat", Text = Clip($"VAT {vatRate:0.0#}%: {text}", 250),
            });
        }

        // ── charges, their voids, balances brought over or set by hand ──────
        var charges = await _db.ClubCharges.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && c.PostedAtUtc >= fromUtc && c.PostedAtUtc < toUtc).ToListAsync(ct);
        var reversedIds = charges.Where(c => c.ReversesChargeId != null).Select(c => c.ReversesChargeId!.Value).ToList();
        var originals = await _db.ClubCharges.AsNoTracking().Where(c => reversedIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        string CounterFor(string entryType, string kind) => entryType switch
        {
            ClubEntryTypes.Opening => accounts.OpeningBalances,
            ClubEntryTypes.Adjustment => accounts.Corrections,
            _ => accounts.RevenueFor(kind),
        };
        foreach (var charge in charges)
        {
            var text = $"{charge.Description} · {charge.MemberName}";
            if (charge.ReversesChargeId is { } reversed)
            {
                var original = originals.GetValueOrDefault(reversed);
                var counter = CounterFor(original?.EntryType ?? ClubEntryTypes.Charge, charge.Kind);
                Book(charge.PostedAtUtc, "V", charge.Id, counter, accounts.Receivables, -charge.Amount, charge.Currency, $"Voided: {text}", charge.MemberName,
                    "ChargeVoid", original?.EntryType == ClubEntryTypes.Charge || original == null ? charge.VatRate : null);
                continue;
            }
            var (prefix, type) = charge.EntryType switch
            {
                ClubEntryTypes.Opening => ("O", "Opening"),
                ClubEntryTypes.Adjustment => ("A", "Correction"),
                _ => ("C", "Charge"),
            };
            Book(charge.PostedAtUtc, prefix, charge.Id, accounts.Receivables, CounterFor(charge.EntryType, charge.Kind), charge.Amount, charge.Currency,
                text, charge.MemberName, type, charge.EntryType == ClubEntryTypes.Charge ? charge.VatRate : null);
        }

        // ── money in and out, and credit moving onto charges ────────────────
        var payments = await _db.ClubPayments.AsNoTracking()
            .Where(p => p.OrganizationId == organizationId && p.ReceivedAtUtc >= fromUtc && p.ReceivedAtUtc < toUtc).ToListAsync(ct);
        var paymentIds = payments.Select(p => p.Id).ToList();
        var reversedPaymentIds = payments.Where(p => p.ReversesPaymentId != null).Select(p => p.ReversesPaymentId!.Value).ToList();
        var originalPayments = await _db.ClubPayments.AsNoTracking().Where(p => reversedPaymentIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var linked = await _db.ClubPaymentAllocations.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId && (paymentIds.Contains(a.PaymentId) || (a.ReversalId != null && paymentIds.Contains(a.ReversalId.Value))))
            .ToListAsync(ct);
        string MoneyAccount(ClubPayment p) => p.EntryType switch
        {
            ClubEntryTypes.Opening => accounts.OpeningBalances,
            ClubEntryTypes.Adjustment => accounts.CreditCorrections,
            _ => p.Method switch
            {
                "Cash" => accounts.Cash,
                "Card" or "Twint" or "DirectDebit" or "CardOnFile" => accounts.CardClearing,
                _ => accounts.Bank,
            },
        };
        foreach (var payment in payments)
        {
            var member = payment.MemberName;
            if (payment.ReversesPaymentId is { } reversesId)
            {
                // A void or a refund: the money goes back out the way it came (or the refund's
                // own way); the charges it paid owe again, the rest comes out of the credit.
                var original = originalPayments.GetValueOrDefault(reversesId);
                var outAccount = payment.Method != null && original != null && payment.Method != original.Method
                    ? MoneyAccount(new ClubPayment { EntryType = ClubEntryTypes.Payment, Method = payment.Method })
                    : original != null ? MoneyAccount(original) : accounts.Bank;
                var amount = -payment.Amount;
                var released = -linked.Where(a => a.ReversalId == payment.Id).Sum(a => a.Amount);
                var type = payment.EntryType == ClubEntryTypes.Refund ? "Refund" : "PaymentVoid";
                var prefix = payment.EntryType == ClubEntryTypes.Refund ? "R" : "W";
                var words = $"{(type == "Refund" ? "Refund" : "Payment voided")} · {member}";
                Book(payment.ReceivedAtUtc, prefix, payment.Id, accounts.Receivables, outAccount, Math.Min(released, amount), payment.Currency, words, member, type);
                Book(payment.ReceivedAtUtc, prefix, payment.Id, accounts.CustomerPrepayments, outAccount, amount - Math.Min(released, amount), payment.Currency, words, member, type);
                continue;
            }
            var paidNow = linked.Where(a => a.PaymentId == payment.Id && a.ReversalId == null && a.Amount > 0 && (a.CreatedAtUtc - payment.CreatedAtUtc).Duration() <= SameWrite)
                .Sum(a => a.Amount);
            var (paymentPrefix, paymentType, paymentWords) = payment.EntryType switch
            {
                ClubEntryTypes.Opening => ("O", "Opening", $"Credit brought over · {member}"),
                ClubEntryTypes.Adjustment => ("A", "Correction", $"Credit set by hand · {member}"),
                _ => ("P", "Payment", $"Payment {payment.Method}{(payment.Reference != null ? $" {payment.Reference}" : "")} · {member}"),
            };
            Book(payment.ReceivedAtUtc, paymentPrefix, payment.Id, MoneyAccount(payment), accounts.Receivables, Math.Min(paidNow, payment.Amount), payment.Currency, paymentWords, member, paymentType);
            Book(payment.ReceivedAtUtc, paymentPrefix, payment.Id, MoneyAccount(payment), accounts.CustomerPrepayments, payment.Amount - Math.Min(paidNow, payment.Amount), payment.Currency,
                paymentWords, member, paymentType);
        }

        // Credit used on a charge later (AppliedCredit), or given back as credit when a charge
        // is cancelled: allocations of the period that went with no payment or reversal above.
        var standalone = await _db.ClubPaymentAllocations.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId && a.ReversalId == null && a.CreatedAtUtc >= fromUtc && a.CreatedAtUtc < toUtc)
            .Join(_db.ClubPayments, a => a.PaymentId, p => p.Id, (a, p) => new { Allocation = a, p.CreatedAtUtc, p.MemberName, p.Currency })
            .ToListAsync(ct);
        foreach (var row in standalone)
        {
            var a = row.Allocation;
            var madeWithPayment = a.Amount > 0 && (a.CreatedAtUtc - row.CreatedAtUtc).Duration() <= SameWrite;
            if (madeWithPayment) continue;
            Book(a.CreatedAtUtc, "K", a.Id, accounts.CustomerPrepayments, accounts.Receivables, a.Amount, row.Currency,
                a.Amount > 0 ? $"Credit used on a charge · {row.MemberName}" : $"Cancelled charge's payment kept as credit · {row.MemberName}",
                row.MemberName, a.Amount > 0 ? "CreditApplied" : "CreditReleased");
        }

        // ── gift cards ──────────────────────────────────────────────────────
        var cards = await _db.ClubGiftCardTransactions.AsNoTracking()
            .Where(t => t.OrganizationId == organizationId && t.AtUtc >= fromUtc && t.AtUtc < toUtc)
            .Join(_db.ClubGiftCards, t => t.GiftCardId, c => c.Id, (t, c) => new { Transaction = t, c.Code })
            .ToListAsync(ct);
        foreach (var row in cards)
        {
            var t = row.Transaction;
            var (counter, type) = t.Type switch
            {
                GiftCardTransactionTypes.Issue => (accounts.GiftCardSales, "GiftCardIssue"),
                GiftCardTransactionTypes.TopUp => (accounts.GiftCardSales, "GiftCardTopUp"),
                GiftCardTransactionTypes.Redeem => (accounts.GiftCardRedemptions, "GiftCardRedeem"),
                GiftCardTransactionTypes.Expiry => (accounts.GiftCardBreakage, "GiftCardExpiry"),
                GiftCardTransactionTypes.Void => (accounts.GiftCardSales, "GiftCardVoid"),
                _ => (accounts.GiftCardSales, "GiftCardAdjustment"),
            };
            // Value added to a card is owed by the club (credit the liability); value taken off it is not any more.
            Book(t.AtUtc, "G", t.Id, counter, accounts.GiftCardLiability, t.Amount, t.Currency, $"Gift card {row.Code}: {type[8..]}", null, type);
        }

        var ordered = rows.OrderBy(r => r.DateUtc).ThenBy(r => r.Document).ThenBy(r => r.Type).ToList();
        var totals = ordered.SelectMany(r => new[] { (Account: r.Debit, Debit: r.Amount, Credit: 0m), (Account: r.Credit, Debit: 0m, Credit: r.Amount) })
            .GroupBy(x => x.Account)
            .Select(g => new JournalAccountDto(g.Key, g.Sum(x => x.Debit), g.Sum(x => x.Credit), g.Sum(x => x.Debit) - g.Sum(x => x.Credit)))
            .OrderBy(a => a.Account, StringComparer.Ordinal)
            .ToList();
        return new JournalDto(fromUtc, toUtc, layout, config.Currency, ordered, totals, accounts);
    }

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];
}
