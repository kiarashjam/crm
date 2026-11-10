using System.Globalization;
using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>
/// Sales (see <see cref="IClubLedger.PostSaleAsync"/>): what a member bought at one go, posted
/// as a charge per kind and VAT rate, a payment for what was paid at the till, and the points,
/// in one save.
/// </summary>
/// <remarks>
/// <para>
/// The POS screen used to post one charge typed in by hand, with no lines, no VAT and no way to
/// say it was paid there and then: a dinner paid by card at the table either went on the house
/// account (and was billed again with the statement) or was not recorded at all, and a comp was
/// a smaller number with nothing to say why. A sale keeps its lines, and settles them as they
/// were settled at the till.
/// </para>
/// <para>
/// One charge per kind and VAT rate rather than one for the bill: a statement's VAT summary and
/// the journal read the rate off each charge, so a bill with drinks at 8.1% and a book at 2.6%
/// posted as one charge with no rate left both out. A sale does not depend on what the account
/// holds, so a write for the same member at the same moment only makes it try again.
/// </para>
/// </remarks>
public sealed partial class ClubLedgerService
{
    /// <summary>Kinds of catalogue item the house-charge screen rings up.</summary>
    private static readonly string[] SaleKinds = [CatalogKinds.MenuItem, CatalogKinds.Product, CatalogKinds.Fee];

    private const int MaxSaleLines = 200;

    /// <summary>A line as the sale will save it, before it has a sale.</summary>
    private sealed class SaleLineDraft
    {
        public Guid? CatalogItemId;
        public string Name = string.Empty;
        public string? Sku;
        public decimal Quantity;
        public decimal UnitPrice;
        public decimal Gross;
        public decimal Discount;
        public decimal? VatRate;
        public string Kind = "Other";
        public bool EarnsPoints;
        public string? Currency;
        public decimal Total => Gross - Discount;
    }

    public async Task<Result<SaleDto>> PostSaleAsync(Guid? organizationId, SaleRequest request, string? idempotencyKey = null, CancellationToken ct = default)
    {
        if (ReadKey(idempotencyKey, out var key) is { } badKey) return badKey;
        if (await EarlierSaleAsync(organizationId, key, request.PosTicketId, ct) is { } earlier) return await SaleDtoAsync(earlier, ct);

        // The points rules first: reading them may save a new workspace's first tiers.
        var rules = await _rules.ReadAsync(organizationId, ct);
        var venue = (request.Venue ?? "").Trim();
        if (!TabVenues.Contains(venue)) return DomainErrors.Club.VenueInvalid;
        var venueKind = venue switch { "Bar" => "Bar", "Spa" => "Spa", _ => "Dining" };

        // ── the lines ────────────────────────────────────────────────────────
        if (request.Lines is not { Count: > 0 and <= MaxSaleLines } asked) return DomainErrors.Sale.LinesRequired;
        var lines = new List<SaleLineDraft>(asked.Count);
        foreach (var line in asked)
        {
            var draft = new SaleLineDraft { EarnsPoints = line.EarnsPoints };
            if (line.CatalogItemId is { } itemId)
            {
                // From the catalogue the name, the price, the VAT rate and the kind are its own:
                // a till can't ring up a priced item at another price.
                var found = await _catalog.ChargeableAsync(organizationId, itemId, SaleKinds, "a sale", ct);
                if (!found.IsSuccess) return found.Error;
                var item = found.Value;
                draft.CatalogItemId = item.Id;
                draft.Name = item.Name;
                draft.UnitPrice = item.Price;
                draft.VatRate = item.VatRate;
                draft.Kind = string.IsNullOrWhiteSpace(item.ChargeKind) ? venueKind : item.ChargeKind;
                draft.Currency = item.Currency;
            }
            else
            {
                if (!request.AllowLinesOutsideCatalog) return DomainErrors.Sale.CatalogOnly;
                draft.Name = (line.Name ?? "").Trim();
                if (draft.Name.Length is 0 or > 200) return DomainErrors.Sale.LineNameRequired;
                if (line.UnitPrice is not { } price || price < 0 || price > 100_000 || price != Math.Round(price, 2))
                    return DomainErrors.Sale.LinePriceInvalid;
                draft.UnitPrice = price;
                if (line.VatRate is { } vat && (vat is < 0 or > 100 || vat != Math.Round(vat, 2))) return DomainErrors.Catalog.VatInvalid;
                draft.VatRate = line.VatRate;
                draft.Kind = Trimmed(line.Kind) ?? venueKind;
                if (!ChargeKinds.Contains(draft.Kind)) return DomainErrors.Sale.LineKindInvalid;
                draft.Sku = Trimmed(line.Sku) is { } sku ? Truncate(sku, 100) : null;
                draft.Currency = Trimmed(request.Currency)?.ToUpperInvariant();
            }
            var quantity = line.Quantity ?? 1m;
            if (quantity <= 0 || quantity > 1000 || quantity != Math.Round(quantity, 2)) return DomainErrors.Club.TabQuantityInvalid;
            draft.Quantity = quantity;
            draft.Gross = Math.Round(quantity * draft.UnitPrice, 2, MidpointRounding.AwayFromZero);
            var lineDiscount = line.Discount ?? 0m;
            if (lineDiscount < 0 || lineDiscount > draft.Gross || lineDiscount != Math.Round(lineDiscount, 2)) return DomainErrors.Sale.DiscountInvalid;
            draft.Discount = lineDiscount;
            lines.Add(draft);
        }

        // ── the discount, spread over the lines ──────────────────────────────
        if (request.DiscountAmount != null && request.DiscountPercent != null) return DomainErrors.Sale.DiscountInvalid;
        var beforeDiscount = lines.Sum(l => l.Total);
        decimal saleDiscount = 0;
        if (request.DiscountAmount is { } off)
        {
            if (off < 0 || off > beforeDiscount || off != Math.Round(off, 2)) return DomainErrors.Sale.DiscountInvalid;
            saleDiscount = off;
        }
        else if (request.DiscountPercent is { } percent)
        {
            if (percent is < 0 or > 100) return DomainErrors.Sale.DiscountInvalid;
            saleDiscount = Math.Round(beforeDiscount * percent / 100m, 2, MidpointRounding.AwayFromZero);
        }
        Spread(lines, saleDiscount);
        var discount = lines.Sum(l => l.Discount);
        var reason = Trimmed(request.DiscountReason);
        // A comp used to be a smaller number on a line with nothing to say who gave it or why.
        if (discount > 0 && reason == null) return DomainErrors.Sale.DiscountReasonRequired;
        if (reason?.Length > 500) return DomainErrors.Club.TooLong("A discount's reason", 500);
        var subtotal = lines.Sum(l => l.Gross);
        var total = lines.Sum(l => l.Total);
        if (total < 0) return DomainErrors.Sale.NegativeTotal;
        if (total >= MaxMoney) return DomainErrors.Club.AmountInvalid;

        // ── how it was settled ───────────────────────────────────────────────
        var tenders = new List<(string Method, decimal Amount)>();
        string tenderLabel;
        if (request.Tenders is { Count: > 0 } several)
        {
            foreach (var tender in several)
            {
                var method = (tender.Method ?? "").Trim();
                if (!SaleTenders.All.Contains(method)) return DomainErrors.Sale.TenderInvalid;
                if (Money(tender.Amount) is not { } amount) return DomainErrors.Club.AmountInvalid;
                tenders.Add((method, amount));
            }
            tenders = tenders.GroupBy(t => t.Method).Select(g => (g.Key, g.Sum(t => t.Amount))).ToList();
            tenderLabel = tenders.Count == 1 ? tenders[0].Method : SaleTenders.Mixed;
        }
        else
        {
            var method = (request.Tender ?? "").Trim();
            if (!SaleTenders.All.Contains(method)) return DomainErrors.Sale.TenderInvalid;
            if (total > 0) tenders.Add((method, total));
            tenderLabel = method;
        }
        var tendered = tenders.Sum(t => t.Amount);
        if (tendered != total) return DomainErrors.Sale.TendersDoNotAddUp(total, tendered);
        var paidTenders = tenders.Where(t => t.Method != SaleTenders.HouseAccount).ToList();
        var paid = paidTenders.Sum(t => t.Amount);

        var reference = Trimmed(request.Reference);
        if (reference?.Length > 200) return DomainErrors.Club.TooLong("A reference", 200);
        var notes = Trimmed(request.Notes);
        if (notes?.Length > 1000) return DomainErrors.Club.TooLong("A note", 1000);
        var label = VenueLabel(venue);

        // ── post it, trying again if another write for the member lands first ─
        for (var attempt = 0; ; attempt++)
        {
            var member = await MemberAsync(organizationId, request.MemberId, ct);
            if (member == null) return DomainErrors.Club.MemberNotFound;
            // The sale and its points are the buyer's; its charges go on the household primary's
            // account when the buyer's bills roll up to them.
            var (billTo, rolledUpFrom) = await BillToAsync(organizationId, member, ct);
            var account = await AccountAsync(organizationId, billTo.Id, ct);
            foreach (var line in lines)
                if (line.Currency != null && !string.Equals(line.Currency, account.Currency, StringComparison.OrdinalIgnoreCase))
                    return DomainErrors.Club.CurrencyMismatch(line.Currency, account.Currency);
            await _reads.ReadAsync("sales", ct);

            var now = DateTime.UtcNow;
            var who = Who() ?? (Trimmed(request.RecordedBy) is { } by ? Truncate(by, 200) : null);
            var sale = new ClubSale
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                MemberId = member.Id,
                MemberName = NameOf(member),
                BilledMemberId = rolledUpFrom != null ? billTo.Id : null,
                Source = Truncate(request.Source, 20),
                PosTicketId = request.PosTicketId,
                Venue = venue,
                Tender = tenderLabel,
                Subtotal = subtotal,
                Discount = discount,
                DiscountReason = discount > 0 ? reason : null,
                Total = total,
                PaidAmount = paid,
                OwedAmount = total - paid,
                Currency = account.Currency,
                Reference = reference,
                StaffName = Trimmed(request.StaffName) is { } staff ? Truncate(staff, 200) : null,
                TableName = Trimmed(request.TableName) is { } table ? Truncate(table, 100) : null,
                Notes = notes,
                IdempotencyKey = key,
                SoldAtUtc = request.SoldAtUtc is { } soldAt ? DateTime.SpecifyKind(soldAt, DateTimeKind.Utc) : now,
                CreatedAtUtc = now,
                CreatedByUserId = _user.UserId,
                CreatedByName = who,
            };
            var saleLines = lines.Select((l, i) => new ClubSaleLine
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                SaleId = sale.Id,
                CatalogItemId = l.CatalogItemId,
                Name = l.Name,
                Sku = l.Sku,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                Discount = l.Discount,
                Total = l.Total,
                VatRate = l.VatRate,
                Kind = l.Kind,
                EarnsPoints = l.EarnsPoints,
                SortOrder = i,
            }).ToList();

            // A charge for each kind and VAT rate, in the order the lines first name them.
            var charges = new List<ClubCharge>();
            foreach (var group in lines.Where(l => l.Total > 0).GroupBy(l => (l.Kind, l.VatRate)))
            {
                var items = group.ToList();
                var words = string.Join(", ", items.Select(l => l.Quantity == 1 ? l.Name
                    : string.Create(CultureInfo.InvariantCulture, $"{l.Quantity:0.##} × {l.Name}")));
                var heading = reference != null ? $"{label}, receipt {reference}" : label;
                var charge = NewCharge(organizationId, billTo, account, ClubEntryTypes.Charge, group.Key.Kind, Clip($"{heading}: {words}", 500),
                    items.Sum(l => l.Total));
                charge.VatRate = group.Key.VatRate;
                charge.CatalogItemId = items.Select(l => l.CatalogItemId).Distinct().Count() == 1 ? items[0].CatalogItemId : null;
                charge.Reference = reference;
                if (items.Any(l => l.Discount > 0)) charge.Reason = Clip($"Discount: {reason}", 500);
                charge.CreatedByName ??= who;
                IncurredBy(charge, rolledUpFrom);
                charges.Add(charge);
            }

            // What was paid at the till pays its own charges, in their order.
            var payments = new List<ClubPayment>();
            var allocations = new List<ClubPaymentAllocation>();
            var room = charges.ToDictionary(c => c.Id, c => c.Amount);
            foreach (var (method, amount) in paidTenders)
            {
                var payment = new ClubPayment
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    MemberId = billTo.Id,
                    MemberName = NameOf(billTo),
                    EntryType = ClubEntryTypes.Payment,
                    Method = method,
                    Amount = amount,
                    Currency = account.Currency,
                    ReceivedAtUtc = now,
                    Reference = reference,
                    Notes = Clip(request.Source == SaleSources.Desk ? $"Paid at the till: {label}" : $"Paid at the POS: {label}", 1000),
                };
                Stamp(payment);
                payment.CreatedByName ??= who;
                payments.Add(payment);
                var left = amount;
                foreach (var charge in charges)
                {
                    if (left <= 0) break;
                    var take = Math.Min(left, room[charge.Id]);
                    if (take <= 0) continue;
                    allocations.Add(Allocate(organizationId, payment.Id, charge.Id, take, null));
                    room[charge.Id] -= take;
                    left -= take;
                }
            }

            // The points are the club's settings: base points per unit spent, times the buyer's
            // tier multiplier; a tip or a rounding earns none.
            var points = rules.PointsForSpend(lines.Where(l => l.EarnsPoints).Sum(l => l.Total), member.Tier);
            LoyaltyLedgerEntry? earned = null;
            if (points > 0)
            {
                earned = new LoyaltyLedgerEntry
                {
                    Id = Guid.NewGuid(),
                    OrganizationId = organizationId,
                    MemberId = member.Id,
                    MemberName = NameOf(member),
                    Kind = "Earned",
                    Reason = venue == "Bar" ? "BarPurchase" : "DiningPurchase",
                    Points = points,
                    Note = Clip(reference != null ? $"{label} sale, receipt {reference}" : $"{label} sale — {lines.Count} items", 500),
                    CreatedAtUtc = now,
                };
            }
            sale.PointsAwarded = points;
            sale.LoyaltyEntryId = earned?.Id;
            sale.ChargeIds = charges.Count > 0 ? string.Join(",", charges.Select(c => c.Id)) : null;
            sale.PaymentIds = payments.Count > 0 ? string.Join(",", payments.Select(p => p.Id)) : null;

            if (charges.Count > 0) Touch(account);
            var saved = await SaveAsync(DomainErrors.Sale.Changed, ct, async () =>
            {
                _db.ClubSales.Add(sale);
                _db.ClubSaleLines.AddRange(saleLines);
                _db.ClubCharges.AddRange(charges);
                _db.ClubPayments.AddRange(payments);
                _db.ClubPaymentAllocations.AddRange(allocations);
                if (earned != null) _db.LoyaltyLedger.Add(earned);
                if (charges.Count > 0) await StageBalanceAsync(organizationId, billTo, paid - total, ct);
            });
            if (saved.IsSuccess) return ToDto(sale, saleLines);
            // The same sale sent twice at once: the other request posted it.
            if (await EarlierSaleAsync(organizationId, key, request.PosTicketId, ct) is { } raced) return await SaleDtoAsync(raced, ct);
            if (attempt >= 2) return saved.Error;
        }
    }

    public async Task<IReadOnlyList<SaleDto>> SalesAsync(Guid? organizationId, Guid? memberId = null, int take = 50, CancellationToken ct = default)
    {
        var sales = await _db.ClubSales.AsNoTracking()
            .Where(s => s.OrganizationId == organizationId && (memberId == null || s.MemberId == memberId))
            .OrderByDescending(s => s.CreatedAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);
        var ids = sales.Select(s => s.Id).ToList();
        var lines = await _db.ClubSaleLines.AsNoTracking().Where(l => ids.Contains(l.SaleId)).ToListAsync(ct);
        var bySale = lines.GroupBy(l => l.SaleId).ToDictionary(g => g.Key, g => g.ToList());
        return sales.Select(s => ToDto(s, bySale.GetValueOrDefault(s.Id) ?? [])).ToList();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>The sale already posted for this key, or for this POS ticket.</summary>
    private async Task<ClubSale?> EarlierSaleAsync(Guid? organizationId, string? key, Guid? posTicketId, CancellationToken ct)
    {
        if (key != null && await _db.ClubSales.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.IdempotencyKey == key, ct) is { } byKey)
            return byKey;
        return posTicketId is { } ticketId
            ? await _db.ClubSales.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.PosTicketId == ticketId, ct)
            : null;
    }

    private async Task<SaleDto> SaleDtoAsync(ClubSale sale, CancellationToken ct) =>
        ToDto(sale, await _db.ClubSaleLines.AsNoTracking().Where(l => l.SaleId == sale.Id).ToListAsync(ct));

    /// <summary>
    /// Spreads a discount on the whole sale over its lines, in proportion to what each still
    /// comes to, to the cent: the cents rounding leaves go to the lines that lost the most, so
    /// the parts add up to the discount exactly.
    /// </summary>
    private static void Spread(List<SaleLineDraft> lines, decimal discount)
    {
        if (discount <= 0) return;
        var basis = lines.Sum(l => l.Total);
        if (basis <= 0) return;
        var shares = lines.Select(l => discount * l.Total / basis).ToList();
        var cents = shares.Select(s => Math.Floor(s * 100) / 100).ToList();
        var left = discount - cents.Sum();
        foreach (var i in Enumerable.Range(0, lines.Count).OrderByDescending(i => shares[i] - cents[i]).ThenBy(i => i))
        {
            if (left <= 0) break;
            if (lines[i].Total - cents[i] < 0.01m) continue;
            cents[i] += 0.01m;
            left -= 0.01m;
        }
        for (var i = 0; i < lines.Count; i++) lines[i].Discount += cents[i];
    }

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

    private static IReadOnlyList<Guid> Ids(string? csv) =>
        (csv ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty).ToList();

    private static SaleDto ToDto(ClubSale s, IEnumerable<ClubSaleLine> lines) => new()
    {
        Id = s.Id,
        MemberId = s.MemberId,
        MemberName = s.MemberName,
        BilledMemberId = s.BilledMemberId,
        Source = s.Source,
        PosTicketId = s.PosTicketId,
        Venue = s.Venue,
        Tender = s.Tender,
        Subtotal = s.Subtotal,
        Discount = s.Discount,
        DiscountReason = s.DiscountReason,
        Total = s.Total,
        PaidAmount = s.PaidAmount,
        OwedAmount = s.OwedAmount,
        Currency = s.Currency,
        PointsAwarded = s.PointsAwarded,
        ChargeIds = Ids(s.ChargeIds),
        PaymentIds = Ids(s.PaymentIds),
        Reference = s.Reference,
        StaffName = s.StaffName,
        TableName = s.TableName,
        Notes = s.Notes,
        SoldAtUtc = s.SoldAtUtc,
        CreatedAtUtc = s.CreatedAtUtc,
        CreatedByName = s.CreatedByName,
        Lines = lines.OrderBy(l => l.SortOrder).Select(l => new SaleLineDto
        {
            Id = l.Id, CatalogItemId = l.CatalogItemId, Name = l.Name, Sku = l.Sku, Quantity = l.Quantity, UnitPrice = l.UnitPrice,
            Discount = l.Discount, Total = l.Total, VatRate = l.VatRate, Kind = l.Kind, EarnsPoints = l.EarnsPoints,
        }).ToList(),
    };
}
