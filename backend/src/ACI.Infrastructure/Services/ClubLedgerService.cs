using System.Globalization;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>
/// The club's money ledger (see <see cref="IClubLedger"/> for what it offers). This part
/// holds the house accounts: charges, payments and credits, voids and refunds, balances
/// and statements.
/// </summary>
/// <remarks>
/// <para>
/// Club money used to be JSON documents edited in place: a void overwrote the status, a
/// payment rewrote the charge's paid amount, an opening balance lived in a column the
/// recompute had to remember to add back. Here every change is a new row. A charge is
/// paid by allocation rows, voided by a row that reverses it, and a balance is the sum of
/// a member's rows up to a date.
/// </para>
/// <para>
/// Each write is one SaveChanges, so it lands whole or not at all. Every row for a member
/// touches their house account row (<see cref="ClubHouseAccount"/>), whose Version makes
/// the second of two writes for the same member at the same moment fail instead of both
/// reading a charge as unpaid. A refused write is a 409 the screen can show; nothing of it
/// is kept.
/// </para>
/// </remarks>
public sealed partial class ClubLedgerService : IClubLedger
{
    public const decimal MaxMoney = 1_000_000_000m;
    public static readonly string[] ChargeKinds = ["Dues", "Dining", "Bar", "Event", "Spa", "Booking", "Retail", "Other"];
    /// <summary>
    /// How money comes in: a card on file, a card or TWINT (at a terminal or online), a SEPA
    /// direct debit, cash, a cheque, a bank transfer (Wire), or credit the member already holds.
    /// </summary>
    public static readonly string[] PaymentMethods = ["CardOnFile", "Card", "Twint", "DirectDebit", "Cash", "Check", "Wire", "AppliedCredit"];
    public const string AppliedCredit = "AppliedCredit";

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly ILedgerReadObserver _reads;
    private readonly IClubCatalog _catalog;
    private readonly IClubRules _rules;

    public ClubLedgerService(AppDbContext db, ICurrentUserService user, ILedgerReadObserver reads, IClubCatalog catalog, IClubRules rules)
    {
        _db = db;
        _user = user;
        _reads = reads;
        _catalog = catalog;
        _rules = rules;
    }

    /// <summary>Kinds of catalogue item a charge can be rung up from: the one-off ones.</summary>
    private static readonly string[] ChargeableKinds = [CatalogKinds.Fee, CatalogKinds.Product, CatalogKinds.Ticket, CatalogKinds.MenuItem];

    // ── reads ────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ChargeDto>> ChargesAsync(Guid? organizationId, Guid? memberId = null, CancellationToken ct = default)
    {
        var rows = await _db.ClubCharges.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && (memberId == null || c.MemberId == memberId))
            .ToListAsync(ct);
        var voids = rows.Where(r => r.ReversesChargeId != null).GroupBy(r => r.ReversesChargeId!.Value).ToDictionary(g => g.Key, g => g.First());
        var paid = await PaidByChargeAsync(organizationId, memberId, ct);
        var clients = await ClientAccountKindsAsync(organizationId, ct);
        var now = DateTime.UtcNow;
        return rows.Where(r => r.ReversesChargeId == null)
            .Select(r => ToDto(r, voids.GetValueOrDefault(r.Id), paid.GetValueOrDefault(r.Id), now) with { AccountType = clients.GetValueOrDefault(r.MemberId) })
            .OrderByDescending(c => c.PostedAtUtc).ThenByDescending(c => c.CreatedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<PaymentDto>> PaymentsAsync(Guid? organizationId, Guid? memberId = null, CancellationToken ct = default)
    {
        // Money received. Credits (an opening balance, a correction) are entries on the
        // account but not money the club took, so the payments list and the revenue it
        // feeds leave them out; the statement shows them.
        var rows = await _db.ClubPayments.AsNoTracking()
            .Where(p => p.OrganizationId == organizationId && (memberId == null || p.MemberId == memberId)
                && (p.EntryType == ClubEntryTypes.Payment || p.ReversesPaymentId != null))
            .ToListAsync(ct);
        var allocations = await _db.ClubPaymentAllocations.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId)
            .Join(_db.ClubPayments.Where(p => memberId == null || p.MemberId == memberId), a => a.PaymentId, p => p.Id, (a, _) => a)
            .ToListAsync(ct);
        var byPayment = allocations.GroupBy(a => a.PaymentId).ToDictionary(g => g.Key, g => g.ToList());
        var reversals = rows.Where(r => r.ReversesPaymentId != null).GroupBy(r => r.ReversesPaymentId!.Value).ToDictionary(g => g.Key, g => g.ToList());
        var clients = await ClientAccountKindsAsync(organizationId, ct);
        return rows.Where(r => r.ReversesPaymentId == null)
            .Select(r => ToDto(r, reversals.GetValueOrDefault(r.Id) ?? [], byPayment.GetValueOrDefault(r.Id) ?? []) with { AccountType = clients.GetValueOrDefault(r.MemberId) })
            .OrderByDescending(p => p.ReceivedAtUtc).ThenByDescending(p => p.CreatedAtUtc)
            .ToList();
    }

    public async Task<Result<HouseBalanceDto>> BalanceAsync(Guid? organizationId, Guid memberId, DateTime? asOfUtc = null, CancellationToken ct = default)
    {
        var holder = await HolderAsync(organizationId, memberId, ct);
        var name = holder?.Name ?? await LastNameOnAccountAsync(organizationId, memberId, ct);
        if (name == null) return DomainErrors.Club.MemberNotFound;
        var asOf = asOfUtc ?? DateTime.UtcNow;
        var balance = await BalanceOfAsync(organizationId, memberId, asOf, ct);
        var charges = (await ChargesAsync(organizationId, memberId, ct)).Where(c => c.PostedAtUtc <= asOf && c.Status != "Voided").ToList();
        var owed = charges.Sum(c => Math.Max(0, c.Amount - c.PaidAmount));
        var credit = await UnallocatedCreditAsync(organizationId, memberId, ct);
        return new HouseBalanceDto(memberId, name, await AccountCurrencyAsync(organizationId, memberId, ct), balance, asOf,
            owed, credit.Sum(c => c.Left), charges.Count(c => c.Amount - c.PaidAmount > 0));
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> BalancesAsync(Guid? organizationId, DateTime? asOfUtc = null, CancellationToken ct = default)
    {
        var credits = await _db.ClubPayments.AsNoTracking()
            .Where(p => p.OrganizationId == organizationId && (asOfUtc == null || p.ReceivedAtUtc <= asOfUtc))
            .GroupBy(p => p.MemberId).Select(g => new { MemberId = g.Key, Sum = g.Sum(p => p.Amount) })
            .ToListAsync(ct);
        var debits = await _db.ClubCharges.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && (asOfUtc == null || c.PostedAtUtc <= asOfUtc))
            .GroupBy(c => c.MemberId).Select(g => new { MemberId = g.Key, Sum = g.Sum(c => c.Amount) })
            .ToListAsync(ct);
        var balances = credits.ToDictionary(c => c.MemberId, c => c.Sum);
        foreach (var d in debits) balances[d.MemberId] = balances.GetValueOrDefault(d.MemberId) - d.Sum;
        return balances;
    }

    public async Task<Result<AccountStatementDto>> StatementAsync(Guid? organizationId, Guid memberId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
    {
        if (fromUtc >= toUtc || toUtc - fromUtc > TimeSpan.FromDays(5 * 366)) return DomainErrors.Club.PeriodInvalid;
        var holder = await HolderAsync(organizationId, memberId, ct);
        var name = holder?.Name ?? await LastNameOnAccountAsync(organizationId, memberId, ct);
        if (name == null) return DomainErrors.Club.MemberNotFound;

        var openingCredits = await _db.ClubPayments.Where(p => p.OrganizationId == organizationId && p.MemberId == memberId && p.ReceivedAtUtc < fromUtc).SumAsync(p => p.Amount, ct);
        var openingDebits = await _db.ClubCharges.Where(c => c.OrganizationId == organizationId && c.MemberId == memberId && c.PostedAtUtc < fromUtc).SumAsync(c => c.Amount, ct);
        var opening = openingCredits - openingDebits;

        var charges = await _db.ClubCharges.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && c.MemberId == memberId && c.PostedAtUtc >= fromUtc && c.PostedAtUtc < toUtc)
            .ToListAsync(ct);
        var payments = await _db.ClubPayments.AsNoTracking()
            .Where(p => p.OrganizationId == organizationId && p.MemberId == memberId && p.ReceivedAtUtc >= fromUtc && p.ReceivedAtUtc < toUtc)
            .ToListAsync(ct);

        var lines = charges.Select(c => (At: c.PostedAtUtc, Created: c.CreatedAtUtc, Line: new StatementLineDto
            {
                Id = c.Id, AtUtc = c.PostedAtUtc, EntryType = c.EntryType, Side = "charge",
                Description = c.ReversesChargeId != null ? $"Voided: {c.Description}" : c.Description,
                Debit = Math.Max(0, c.Amount), Credit = Math.Max(0, -c.Amount), Kind = c.Kind, Reference = c.Reference,
                Reason = c.Reason, ReversesId = c.ReversesChargeId, CreatedByName = c.CreatedByName, VatRate = c.VatRate,
                IncurredByMemberId = c.IncurredByMemberId, IncurredByName = c.IncurredByName,
            }))
            .Concat(payments.Select(p => (At: p.ReceivedAtUtc, Created: p.CreatedAtUtc, Line: new StatementLineDto
            {
                Id = p.Id, AtUtc = p.ReceivedAtUtc, EntryType = p.EntryType, Side = "payment",
                Description = PaymentWords(p), Debit = Math.Max(0, -p.Amount), Credit = Math.Max(0, p.Amount),
                Method = p.Method, Reference = p.Reference, Reason = p.Reason, ReversesId = p.ReversesPaymentId, CreatedByName = p.CreatedByName,
            })))
            .OrderBy(l => l.At).ThenBy(l => l.Created).ThenBy(l => l.Line.Id)
            .ToList();

        var running = opening;
        var withBalance = new List<StatementLineDto>(lines.Count);
        foreach (var (_, _, line) in lines)
        {
            running += line.Credit - line.Debit;
            withBalance.Add(line with { Balance = running });
        }
        return new AccountStatementDto(memberId, name, await AccountCurrencyAsync(organizationId, memberId, ct), fromUtc, toUtc,
            opening, running, lines.Sum(l => l.Line.Debit), lines.Sum(l => l.Line.Credit), withBalance);
    }

    public async Task<bool> HasOpenAccountAsync(Guid? organizationId, Guid memberId, CancellationToken ct = default) =>
        await BalanceOfAsync(organizationId, memberId, null, ct) != 0
        || await _db.ClubTabs.AnyAsync(t => t.OrganizationId == organizationId && t.MemberId == memberId && t.Status == "Open", ct)
        || await _db.ClubSubscriptions.AnyAsync(s => s.OrganizationId == organizationId && s.MemberId == memberId && s.Status != "Cancelled", ct);

    // ── charges ──────────────────────────────────────────────────────────────

    public async Task<Result<ChargeDto>> PostChargeAsync(Guid? organizationId, PostChargeRequest request, string? idempotencyKey = null, CancellationToken ct = default)
    {
        if (ReadKey(idempotencyKey, out var key) is { } badKey) return badKey;
        if (key != null && await ChargeByKeyAsync(organizationId, key, ct) is { } earlier) return await ChargeDtoAsync(organizationId, earlier, ct);

        // Rung up from the catalogue, the price, the name, the kind and the VAT rate are the
        // catalogue's: a front desk can't type a different price for a priced item.
        CatalogItemDto? item = null;
        if (request.CatalogItemId is { } itemId)
        {
            var found = await _catalog.ChargeableAsync(organizationId, itemId, ChargeableKinds, "a charge", ct);
            if (!found.IsSuccess) return found.Error;
            item = found.Value;
        }
        decimal amount;
        if (item != null)
        {
            var quantity = request.Quantity ?? 1m;
            if (quantity <= 0 || quantity > 1000 || quantity != Math.Round(quantity, 2)) return DomainErrors.Club.TabQuantityInvalid;
            if (Money(item.Price * quantity) is not { } total) return DomainErrors.Club.AmountInvalid;
            amount = total;
        }
        else if (Money(request.Amount) is { } typed) amount = typed;
        else return DomainErrors.Club.AmountInvalid;

        var named = await HolderAsync(organizationId, request.MemberId, ct);
        if (named == null) return DomainErrors.Club.MemberNotFound;
        // The account it goes on: for a member, the household primary's when their bills roll up
        // to them, or their company's when it pays its nominees' charges (not for what they hired
        // as a private client). A company's or a client's own account is billed as it is.
        var (holder, rolledUpFrom) = named.Member is { } incurredBy
            ? await BillToAsync(organizationId, incurredBy, forDues: false, ct, companyMayPay: !request.PrivateHire)
            : (named, null);
        var kind = item?.ChargeKind ?? (request.Kind ?? "").Trim();
        if (kind.Length == 0) kind = "Other";
        if (!ChargeKinds.Contains(kind)) return DomainErrors.Club.ChargeKindInvalid;
        var description = (request.Description ?? "").Trim();
        if (description.Length == 0 && item != null)
            description = request.Quantity is { } q && q != 1 ? string.Create(CultureInfo.InvariantCulture, $"{item.Name} × {q:0.##}") : item.Name;
        if (description.Length == 0) return DomainErrors.Club.DescriptionRequired;
        if (description.Length > 500) return DomainErrors.Club.TooLong("A description", 500);
        var reference = Trimmed(request.Reference);
        if (reference?.Length > 200) return DomainErrors.Club.TooLong("A reference", 200);
        if (!ReadDate(request.DueAtUtc, out var due)) return DomainErrors.Club.DateInvalid("The due date");
        if (item == null && request.VatRate is { } typedVat && (typedVat is < 0 or > 100 || typedVat != Math.Round(typedVat, 2)))
            return DomainErrors.Catalog.VatInvalid;

        var account = await AccountAsync(organizationId, holder.Id, ct);
        if (item != null && !string.Equals(item.Currency, account.Currency, StringComparison.OrdinalIgnoreCase))
            return DomainErrors.Club.CurrencyMismatch(item.Currency, account.Currency);
        var charge = NewCharge(organizationId, holder, account, ClubEntryTypes.Charge, kind, description, amount);
        charge.DueAtUtc = due;
        charge.Reference = reference;
        charge.IdempotencyKey = key;
        charge.CatalogItemId = item?.Id;
        charge.VatRate = item?.VatRate ?? request.VatRate;
        IncurredBy(charge, rolledUpFrom);
        Touch(account);
        var saved = await SaveAsync(DomainErrors.Club.AccountChanged, ct, async () =>
        {
            _db.ClubCharges.Add(charge);
            await StageBalanceAsync(organizationId, holder, -amount, ct);
        });
        if (!saved.IsSuccess)
        {
            // The same key sent twice at once: the other request posted it.
            if (key != null && await ChargeByKeyAsync(organizationId, key, ct) is { } raced) return await ChargeDtoAsync(organizationId, raced, ct);
            return saved.Error;
        }
        return ToDto(charge, null, default, DateTime.UtcNow) with { AccountType = holder.ClientKind };
    }

    /// <remarks>
    /// Charges used to be deleted, by anyone with an account: a 500 bar charge could
    /// disappear, the balance and the next statement changed, and nothing said who did it
    /// or that it had ever been there. A void is an entry that takes the charge back; the
    /// charge stays, saying who voided it and why.
    /// </remarks>
    public async Task<Result<ChargeDto>> VoidChargeAsync(Guid? organizationId, Guid chargeId, string? reason, CancellationToken ct = default)
    {
        if (ReadReason(reason, out var why) is { } noReason) return noReason;
        var charge = await _db.ClubCharges.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == chargeId && c.OrganizationId == organizationId && c.ReversesChargeId == null, ct);
        if (charge == null) return DomainErrors.Club.ChargeNotFound;
        // The account first: whatever is read after it is checked against it on saving.
        var account = await AccountAsync(organizationId, charge.MemberId, ct);
        if (await _db.ClubCharges.AnyAsync(c => c.ReversesChargeId == chargeId, ct)) return DomainErrors.Club.ChargeVoided;
        // Money paid against it would drop out of the account with it, so the payments go first.
        if (await PaidAsync(chargeId, ct) > 0) return DomainErrors.Club.ChargeHasPayments;
        await _reads.ReadAsync("charges", ct);

        var reversal = new ClubCharge
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = charge.MemberId,
            MemberName = charge.MemberName,
            EntryType = ClubEntryTypes.Void,
            Kind = charge.Kind,
            Description = charge.Description,
            Amount = -charge.Amount,
            Currency = charge.Currency,
            PostedAtUtc = DateTime.UtcNow,
            ReversesChargeId = charge.Id,
            Reason = why,
            // A void takes back the VAT the charge carried, at the same rate.
            VatRate = charge.VatRate,
            CatalogItemId = charge.CatalogItemId,
            IncurredByMemberId = charge.IncurredByMemberId,
            IncurredByName = charge.IncurredByName,
        };
        Stamp(reversal);
        Touch(account);
        var saved = await SaveAsync(DomainErrors.Club.ChargeChanged, ct, async () =>
        {
            _db.ClubCharges.Add(reversal);
            await StageBalanceAsync(organizationId, await MemberAsync(organizationId, charge.MemberId, ct), charge.Amount, ct);
        });
        if (!saved.IsSuccess) return saved.Error;
        return ToDto(charge, reversal, default, DateTime.UtcNow);
    }

    /// <remarks>
    /// A cancelled event ticket used to have to wait for whoever could void money: the ticket
    /// was paid, so its charge could not be voided until the payment was, and the member was
    /// charged for a seat they had given back. The charge is voided here as VoidChargeAsync
    /// voids it, and each payment that paid it gives that part back as house credit, by an
    /// allocation that takes its own back (with no reversal of its own: the payment stands).
    /// </remarks>
    public async Task<Result<ChargeDto>> CancelChargeAsync(Guid? organizationId, Guid chargeId, string? reason, CancellationToken ct = default)
    {
        if (ReadReason(reason, out var why) is { } noReason) return noReason;
        var charge = await _db.ClubCharges.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == chargeId && c.OrganizationId == organizationId && c.ReversesChargeId == null, ct);
        if (charge == null) return DomainErrors.Club.ChargeNotFound;
        // The account first: whatever is read after it is checked against it on saving.
        var account = await AccountAsync(organizationId, charge.MemberId, ct);
        if (await _db.ClubCharges.AnyAsync(c => c.ReversesChargeId == chargeId, ct)) return DomainErrors.Club.ChargeVoided;
        var paidBy = (await _db.ClubPaymentAllocations.AsNoTracking().Where(a => a.ChargeId == chargeId)
                .GroupBy(a => a.PaymentId).Select(g => new { PaymentId = g.Key, Net = g.Sum(a => a.Amount) })
                .ToListAsync(ct))
            .Where(p => p.Net > 0).ToList();
        await _reads.ReadAsync("charges", ct);

        var reversal = new ClubCharge
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = charge.MemberId,
            MemberName = charge.MemberName,
            EntryType = ClubEntryTypes.Void,
            Kind = charge.Kind,
            Description = charge.Description,
            Amount = -charge.Amount,
            Currency = charge.Currency,
            PostedAtUtc = DateTime.UtcNow,
            ReversesChargeId = charge.Id,
            Reason = why,
            // As VoidChargeAsync's: the VAT the charge carried is taken back at the same rate,
            // so a cancelled ticket priced from the catalogue leaves the statement's VAT.
            VatRate = charge.VatRate,
            CatalogItemId = charge.CatalogItemId,
            IncurredByMemberId = charge.IncurredByMemberId,
            IncurredByName = charge.IncurredByName,
        };
        Stamp(reversal);
        Touch(account);
        var saved = await SaveAsync(DomainErrors.Club.ChargeChanged, ct, async () =>
        {
            _db.ClubCharges.Add(reversal);
            foreach (var paid in paidBy)
                _db.ClubPaymentAllocations.Add(Allocate(organizationId, paid.PaymentId, chargeId, -paid.Net, null));
            await StageBalanceAsync(organizationId, await MemberAsync(organizationId, charge.MemberId, ct), charge.Amount, ct);
        });
        if (!saved.IsSuccess) return saved.Error;
        return ToDto(charge, reversal, default, DateTime.UtcNow);
    }

    // ── payments ─────────────────────────────────────────────────────────────

    public async Task<Result<PaymentDto>> RecordPaymentAsync(Guid? organizationId, RecordPaymentRequest request, string? idempotencyKey = null, CancellationToken ct = default)
    {
        if (ReadKey(idempotencyKey, out var key) is { } badKey) return badKey;
        if (key != null && await PaymentByKeyAsync(organizationId, key, ct) is { } earlier) return await PaymentDtoAsync(organizationId, earlier, ct);
        if (Money(request.Amount) is not { } amount) return DomainErrors.Club.AmountInvalid;
        var method = (request.Method ?? "").Trim();
        if (!PaymentMethods.Contains(method)) return DomainErrors.Club.MethodInvalid;
        if (!ReadDate(request.ReceivedAtUtc, out var received)) return DomainErrors.Club.DateInvalid("The date received");
        var reference = Trimmed(request.Reference);
        if (reference?.Length > 200) return DomainErrors.Club.TooLong("A reference", 200);
        var notes = Trimmed(request.Notes);
        if (notes?.Length > 1000) return DomainErrors.Club.TooLong("A note", 1000);

        // What it pays: one charge, as the screens send it, or several with an amount each.
        // A payment against nothing used to change no balance at all, so one is required.
        var asked = request.Allocations is { Count: > 0 } list
            ? list.Select(a => (a.ChargeId, Amount: Money(a.Amount))).ToList()
            : request.ChargeId is { } single ? [(single, (decimal?)amount)] : [];
        if (asked.Count == 0) return DomainErrors.Club.PaymentChargeRequired;
        if (asked.Any(a => a.Amount == null)) return DomainErrors.Club.AllocationsInvalid;
        var wanted = asked.GroupBy(a => a.ChargeId).Select(g => (ChargeId: g.Key, Amount: g.Sum(a => a.Amount!.Value))).ToList();
        if (wanted.Sum(a => a.Amount) != amount) return DomainErrors.Club.AllocationsInvalid;

        // A payment settles charges, and no more than they still owe: the excess used to be
        // recorded and then lost. A stale page paying a charge twice is caught here too.
        var charges = new List<ClubCharge>();
        foreach (var (chargeId, _) in wanted)
        {
            var charge = await _db.ClubCharges.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == chargeId && c.OrganizationId == organizationId && c.ReversesChargeId == null, ct);
            if (charge == null) return DomainErrors.Club.ChargeNotFound;
            if (charges.Count > 0 && charge.MemberId != charges[0].MemberId) return DomainErrors.Club.ChargeOfAnotherMember;
            charges.Add(charge);
        }
        var member = await HolderAsync(organizationId, charges[0].MemberId, ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;
        // The account first: what the charges owe is read after it, and the save is checked
        // against it, so of two payments of one charge at once the second is refused.
        var account = await AccountAsync(organizationId, member.Id, ct);
        foreach (var (chargeId, part) in wanted)
        {
            if (await _db.ClubCharges.AnyAsync(c => c.ReversesChargeId == chargeId, ct)) return DomainErrors.Club.ChargeVoided;
            var owed = charges.First(c => c.Id == chargeId).Amount - await PaidAsync(chargeId, ct);
            if (owed <= 0) return DomainErrors.Club.ChargeAlreadyPaid;
            if (part > owed) return DomainErrors.Club.Overpaid(owed, part);
        }
        await _reads.ReadAsync("charges", ct);

        if (method == AppliedCredit) return await ApplyCreditAsync(organizationId, member, account, wanted, amount, ct);

        var payment = new ClubPayment
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = member.Id,
            MemberName = member.Name,
            EntryType = ClubEntryTypes.Payment,
            Method = method,
            Amount = amount,
            Currency = account.Currency,
            ReceivedAtUtc = received ?? DateTime.UtcNow,
            Reference = reference,
            Notes = notes,
            IdempotencyKey = key,
        };
        Stamp(payment);
        var allocations = wanted.Select(w => Allocate(organizationId, payment.Id, w.ChargeId, w.Amount, null)).ToList();
        Touch(account);
        var saved = await SaveAsync(DomainErrors.Club.ChargeChanged, ct, async () =>
        {
            _db.ClubPayments.Add(payment);
            _db.ClubPaymentAllocations.AddRange(allocations);
            await StageBalanceAsync(organizationId, member, amount, ct);
        });
        if (!saved.IsSuccess)
        {
            if (key != null && await PaymentByKeyAsync(organizationId, key, ct) is { } raced) return await PaymentDtoAsync(organizationId, raced, ct);
            return saved.Error;
        }
        return ToDto(payment, [], allocations) with { AccountType = member.ClientKind };
    }

    /// <summary>
    /// Pays charges from credit the member already holds (an opening credit, a correction,
    /// money a payment left over), oldest first. No new money comes in, so the balance
    /// stays where it is; the charges show as paid.
    /// </summary>
    /// <remarks>
    /// "House credit" used to be recorded as one more payment, so a member with 50 of
    /// credit who paid a 50 charge from it ended up 50 in credit still, and the charge paid.
    /// </remarks>
    private async Task<Result<PaymentDto>> ApplyCreditAsync(Guid? organizationId, Holder member, ClubHouseAccount account,
        List<(Guid ChargeId, decimal Amount)> wanted, decimal amount, CancellationToken ct)
    {
        var credits = await UnallocatedCreditAsync(organizationId, member.Id, ct);
        var available = credits.Sum(c => c.Left);
        if (available < amount) return DomainErrors.Club.CreditTooLow(available, amount);

        var made = new List<ClubPaymentAllocation>();
        var queue = new Queue<(Guid PaymentId, decimal Left)>(credits.Select(c => (c.PaymentId, c.Left)));
        var (source, left) = queue.Dequeue();
        foreach (var (chargeId, part) in wanted)
        {
            var due = part;
            while (due > 0)
            {
                while (left <= 0) (source, left) = queue.Dequeue();
                var take = Math.Min(due, left);
                made.Add(Allocate(organizationId, source, chargeId, take, null));
                due -= take;
                left -= take;
            }
        }
        Touch(account);
        var saved = await SaveAsync(DomainErrors.Club.ChargeChanged, ct, () =>
        {
            _db.ClubPaymentAllocations.AddRange(made);
            return Task.CompletedTask;
        });
        if (!saved.IsSuccess) return saved.Error;
        return new PaymentDto
        {
            Id = made[0].Id,
            MemberId = member.Id,
            MemberName = member.Name,
            AccountType = member.ClientKind,
            EntryType = "CreditApplied",
            Amount = amount,
            Currency = account.Currency,
            Method = AppliedCredit,
            ReceivedAtUtc = made[0].CreatedAtUtc,
            ChargeId = wanted[0].ChargeId,
            AppliedToChargeIds = wanted.Select(w => w.ChargeId).ToList(),
            Allocations = wanted.Select(w => new PaymentAllocationDto(w.ChargeId, w.Amount)).ToList(),
            CreatedAtUtc = made[0].CreatedAtUtc,
            CreatedByName = _user.UserName,
        };
    }

    /// <summary>A payment taken back whole: a cheque that bounced, one recorded in error. Its charges owe again what it paid.</summary>
    public async Task<Result<PaymentDto>> VoidPaymentAsync(Guid? organizationId, Guid paymentId, string? reason, CancellationToken ct = default)
    {
        if (ReadReason(reason, out var why) is { } noReason) return noReason;
        var payment = await _db.ClubPayments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.OrganizationId == organizationId && p.ReversesPaymentId == null, ct);
        if (payment == null) return DomainErrors.Club.PaymentNotFound;
        var account = await AccountAsync(organizationId, payment.MemberId, ct);
        var reversals = await _db.ClubPayments.AsNoTracking().Where(p => p.ReversesPaymentId == paymentId).ToListAsync(ct);
        if (reversals.Any(r => r.EntryType == ClubEntryTypes.Void)) return DomainErrors.Club.PaymentVoided;
        var left = payment.Amount + reversals.Sum(r => r.Amount);
        if (left <= 0) return DomainErrors.Club.NothingToRefund;
        var allocated = await NetAllocationsAsync(paymentId, ct);
        await _reads.ReadAsync("charges", ct);

        var reversal = Reverse(payment, ClubEntryTypes.Void, left, why, payment.Method, null);
        Touch(account);
        var saved = await SaveAsync(DomainErrors.Club.ChargeChanged, ct, async () =>
        {
            _db.ClubPayments.Add(reversal);
            foreach (var (chargeId, net) in allocated)
                if (net > 0) _db.ClubPaymentAllocations.Add(Allocate(organizationId, paymentId, chargeId, -net, reversal.Id));
            await StageBalanceAsync(organizationId, await MemberAsync(organizationId, payment.MemberId, ct), -left, ct);
        });
        if (!saved.IsSuccess) return saved.Error;
        return await PaymentDtoAsync(organizationId, payment, ct);
    }

    /// <summary>
    /// Money paid back to the member, part or all of a payment. What the payment left
    /// unused goes back first; beyond that, the charges it paid (the latest first) owe
    /// again, and a manager voids them if the club is waiving them.
    /// </summary>
    public async Task<Result<PaymentDto>> RefundPaymentAsync(Guid? organizationId, Guid paymentId, RefundPaymentRequest request, CancellationToken ct = default)
    {
        var why = (request.Reason ?? "").Trim();
        if (why.Length == 0) return DomainErrors.Club.ReasonRequiredForRefund;
        if (why.Length > 500) return DomainErrors.Club.TooLong("A reason", 500);
        if (Money(request.Amount) is not { } amount) return DomainErrors.Club.AmountInvalid;
        var method = Trimmed(request.Method);
        if (method != null && (!PaymentMethods.Contains(method) || method == AppliedCredit)) return DomainErrors.Club.MethodInvalid;
        var reference = Trimmed(request.Reference);
        if (reference?.Length > 200) return DomainErrors.Club.TooLong("A reference", 200);
        return await RefundCoreAsync(organizationId, paymentId, amount, why, method, reference, null, ct);
    }

    /// <summary>
    /// The refund itself, as <see cref="RefundPaymentAsync"/> describes it; <paramref name="provider"/>,
    /// when given, marks it as one a payment provider made, kept with the provider's refund id and
    /// recorded once by it.
    /// </summary>
    private async Task<Result<PaymentDto>> RefundCoreAsync(Guid? organizationId, Guid paymentId, decimal amount, string why, string? method,
        string? reference, ProviderRefundEntry? provider, CancellationToken ct)
    {
        var payment = await _db.ClubPayments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.OrganizationId == organizationId && p.ReversesPaymentId == null, ct);
        if (payment == null) return DomainErrors.Club.PaymentNotFound;
        var account = await AccountAsync(organizationId, payment.MemberId, ct);
        var reversals = await _db.ClubPayments.AsNoTracking().Where(p => p.ReversesPaymentId == paymentId).ToListAsync(ct);
        if (reversals.Any(r => r.EntryType == ClubEntryTypes.Void)) return DomainErrors.Club.PaymentVoided;
        var left = payment.Amount + reversals.Sum(r => r.Amount);
        if (left <= 0) return DomainErrors.Club.NothingToRefund;
        if (amount > left) return DomainErrors.Club.RefundTooMuch(left, amount);
        var allocated = await _db.ClubPaymentAllocations.AsNoTracking().Where(a => a.PaymentId == paymentId).ToListAsync(ct);
        await _reads.ReadAsync("charges", ct);

        var refund = Reverse(payment, ClubEntryTypes.Refund, amount, why, method ?? payment.Method, reference);
        if (provider != null)
        {
            refund.Provider = provider.Provider;
            refund.ProviderPaymentId = provider.ProviderRefundId;
            refund.IdempotencyKey = ProviderRefundKey(provider);
            if (refund.CreatedByName == null && provider.RecordedBy != null) refund.CreatedByName = Truncate(provider.RecordedBy, 200);
        }
        var unused = left - allocated.Sum(a => a.Amount);
        var release = Math.Max(0, amount - unused);
        var released = new List<ClubPaymentAllocation>();
        // The charges it paid most recently owe again first.
        foreach (var group in allocated.GroupBy(a => a.ChargeId).OrderByDescending(g => g.Max(a => a.CreatedAtUtc)))
        {
            if (release <= 0) break;
            var net = group.Sum(a => a.Amount);
            if (net <= 0) continue;
            var take = Math.Min(net, release);
            released.Add(Allocate(organizationId, paymentId, group.Key, -take, refund.Id));
            release -= take;
        }
        Touch(account);
        var saved = await SaveAsync(DomainErrors.Club.ChargeChanged, ct, async () =>
        {
            _db.ClubPayments.Add(refund);
            _db.ClubPaymentAllocations.AddRange(released);
            await StageBalanceAsync(organizationId, await MemberAsync(organizationId, payment.MemberId, ct), -amount, ct);
        });
        if (!saved.IsSuccess)
        {
            // The same provider refund reported twice at once: the other request recorded it.
            if (provider != null && await PaymentByKeyAsync(organizationId, ProviderRefundKey(provider), ct) != null)
                return await PaymentDtoAsync(organizationId, payment, ct);
            return saved.Error;
        }
        return await PaymentDtoAsync(organizationId, payment, ct);
    }

    private static string ProviderRefundKey(ProviderRefundEntry refund) => $"{refund.Provider}-refund:{refund.ProviderRefundId}";

    // ── balances set by hand ─────────────────────────────────────────────────

    public async Task<Result<HouseBalanceDto>> AdjustBalanceAsync(Guid? organizationId, Guid memberId, decimal newBalance, string? reason, CancellationToken ct = default)
    {
        if (newBalance is <= -MaxMoney or >= MaxMoney) return DomainErrors.Club.AmountInvalid;
        var target = Math.Round(newBalance, 2, MidpointRounding.AwayFromZero);
        var why = Trimmed(reason);
        if (why?.Length > 500) return DomainErrors.Club.TooLong("A reason", 500);
        var member = await HolderAsync(organizationId, memberId, ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;

        var account = await AccountAsync(organizationId, member.Id, ct);
        var current = await BalanceOfAsync(organizationId, member.Id, null, ct);
        var difference = target - current;
        if (difference != 0)
        {
            var words = string.Create(CultureInfo.InvariantCulture, $"House balance changed by hand from {current:0.00} to {target:0.00}");
            Touch(account);
            var saved = await SaveAsync(DomainErrors.Club.AccountChanged, ct, async () =>
            {
                AddBalanceEntry(organizationId, member, account, ClubEntryTypes.Adjustment, difference, words, why);
                await StageBalanceAsync(organizationId, member, difference, ct);
            });
            if (!saved.IsSuccess) return saved.Error;
        }
        return await BalanceAsync(organizationId, member.Id, null, ct);
    }

    public async Task<Result> OpenWithBalanceAsync(Guid? organizationId, Guid memberId, decimal openingBalance, CancellationToken ct = default)
    {
        if (openingBalance is <= -MaxMoney or >= MaxMoney) return DomainErrors.Club.AmountInvalid;
        var opening = Math.Round(openingBalance, 2, MidpointRounding.AwayFromZero);
        var member = await HolderAsync(organizationId, memberId, ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;
        var account = await AccountAsync(organizationId, member.Id, ct);
        Touch(account);
        var saved = await SaveAsync(DomainErrors.Club.AccountChanged, ct, async () =>
        {
            if (opening == 0) return;
            AddBalanceEntry(organizationId, member, account, ClubEntryTypes.Opening, opening, "Opening balance", null);
            await StageBalanceAsync(organizationId, member, opening, ct);
        });
        return saved.IsSuccess ? Result.Success() : saved.Error;
    }

    /// <summary>An opening or hand-set balance as an entry: a credit when positive, an amount owed when negative.</summary>
    private void AddBalanceEntry(Guid? organizationId, Holder member, ClubHouseAccount account, string entryType, decimal amount, string words, string? reason)
    {
        if (amount > 0)
        {
            var credit = new ClubPayment
            {
                Id = Guid.NewGuid(), OrganizationId = organizationId, MemberId = member.Id, MemberName = member.Name,
                EntryType = entryType, Amount = amount, Currency = account.Currency, ReceivedAtUtc = DateTime.UtcNow,
                Description = words, Reason = reason,
            };
            Stamp(credit);
            _db.ClubPayments.Add(credit);
        }
        else
        {
            var owed = NewCharge(organizationId, member, account, entryType, "Other", words, -amount);
            owed.Reason = reason;
            _db.ClubCharges.Add(owed);
        }
    }

    // ── the house account ────────────────────────────────────────────────────

    /// <summary>
    /// The member whose account a charge for <paramref name="member"/> goes on, and who incurred
    /// it when that is someone else: the primary of their household when their household link
    /// rolls up, else the member themselves.
    /// </summary>
    /// <remarks>
    /// Roll-up billing was a switch on the Households page that nothing read, so a spouse's spa
    /// bill stayed on the spouse's account while the club invoiced the primary. The entry is put
    /// on the primary's account here, at posting, so the balance, the statement, the reminders
    /// and the overdue chasing all follow the bill without knowing about households. What is
    /// already posted stays where it is when the switch changes: an entry is never moved.
    /// </remarks>
    private async Task<(Holder BillTo, ClubMember? IncurredBy)> BillToAsync(Guid? organizationId, ClubMember member, bool forDues, CancellationToken ct,
        bool companyMayPay = true)
    {
        var self = Holder.Of(member);
        // A nominee of a corporate membership whose company pays its nominees' charges: on the
        // company's account. Not their own dues: the company pays for the seat, not for each
        // member's subscription, so a subscription a nominee still has stays theirs to pay (or
        // for the club to pause). Nor what they hire as a private client (a party of their own):
        // that is theirs, not something they ran up in the club for the company. The company's
        // rule comes before a household's roll-up, as it is the agreement the company made for
        // what its people spend.
        if (!forDues && companyMayPay)
        {
            var company = await _db.ClubCorporateNominees.AsNoTracking()
                .Where(n => n.OrganizationId == organizationId && n.MemberId == member.Id && n.RemovedAtUtc == null)
                .Join(_db.ClubCorporateMemberships.AsNoTracking()
                        .Where(c => c.Status == CorporateMembershipStatuses.Active && c.NomineeCharges == NomineeChargeRules.Company),
                    n => n.CorporateMembershipId, c => c.Id, (n, c) => (Guid?)c.CompanyId)
                .FirstOrDefaultAsync(ct);
            if (company is Guid companyId && await HolderAsync(organizationId, companyId, ct) is { Kind: ClubAccountHolders.Company } payer)
                return (payer, member);
        }
        var link = await _db.ClubHouseholdMembers.AsNoTracking()
            .FirstOrDefaultAsync(l => l.OrganizationId == organizationId && l.MemberId == member.Id, ct);
        if (link is not { RollUpBilling: true, IsPrimary: false }) return (self, null);
        var primaryId = await _db.ClubHouseholdMembers.AsNoTracking()
            .Where(l => l.OrganizationId == organizationId && l.HouseholdId == link.HouseholdId && l.IsPrimary)
            .Select(l => l.MemberId)
            .FirstOrDefaultAsync(ct);
        // A household whose primary is not a member (yet) has nobody to bill but the member.
        if (primaryId is not { } id || id == member.Id) return (self, null);
        var primary = await MemberAsync(organizationId, id, ct);
        return primary == null ? (self, null) : (Holder.Of(primary), member);
    }

    /// <summary>Records on the charge who ran it up, when it is on someone else's account.</summary>
    private static void IncurredBy(ClubCharge charge, ClubMember? member)
    {
        if (member == null) return;
        charge.IncurredByMemberId = member.Id;
        charge.IncurredByName = NameOf(member);
    }

    /// <summary>
    /// Whose house account entries go on: a member, or a client who is not one (a company, a
    /// contact) whose account was opened for them. <see cref="Id"/> is the key every entry on
    /// the account names; <see cref="Name"/> is what each entry calls it.
    /// </summary>
    private sealed record Holder(Guid Id, string Name, ClubMember? Member, string Kind)
    {
        public static Holder Of(ClubMember member) => new(member.Id, NameOf(member), member, ClubAccountHolders.Member);

        /// <summary>The account's kind as the answers say it: null for a member's own account, as the screens have always read one.</summary>
        public string? ClientKind => Kind == ClubAccountHolders.Member ? null : Kind;
    }

    /// <summary>
    /// The member whose account <paramref name="id"/> is, or the company or contact whose account
    /// was opened under it; null for anything else. A company or a contact is billed only once
    /// someone has opened their account (<see cref="OpenClientAccountAsync"/>), so a stray id in a
    /// request never becomes an account of its own.
    /// </summary>
    private async Task<Holder?> HolderAsync(Guid? organizationId, Guid? id, CancellationToken ct)
    {
        if (id is not { } key) return null;
        var member = await MemberAsync(organizationId, key, ct);
        if (member != null) return Holder.Of(member);
        var client = await _db.ClubHouseAccounts.AsNoTracking()
            .Where(a => a.MemberId == key && a.OrganizationId == organizationId && (a.CompanyId != null || a.ContactId != null))
            .Select(a => new { a.CompanyId, a.ContactId })
            .FirstOrDefaultAsync(ct);
        if (client == null) return null;
        var kind = client.CompanyId != null ? ClubAccountHolders.Company : ClubAccountHolders.Contact;
        return new Holder(key, await ClientNameAsync(organizationId, kind, key, ct), null, kind);
    }

    /// <summary>The company's or contact's name now, or the name its entries carry when the record has gone.</summary>
    private async Task<string> ClientNameAsync(Guid? organizationId, string kind, Guid id, CancellationToken ct)
    {
        var name = kind == ClubAccountHolders.Company
            ? await _db.Companies.AsNoTracking().Where(c => c.Id == id && c.OrganizationId == organizationId).Select(c => c.Name).FirstOrDefaultAsync(ct)
            : await _db.Contacts.AsNoTracking().Where(c => c.Id == id && c.OrganizationId == organizationId).Select(c => c.Name).FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(name)) return Truncate(name.Trim(), 200);
        return await LastNameOnAccountAsync(organizationId, id, ct) ?? (kind == ClubAccountHolders.Company ? "Company" : "Client");
    }

    /// <summary>The workspace's accounts that are a company's or a contact's, by key, with their kind.</summary>
    private async Task<Dictionary<Guid, string>> ClientAccountKindsAsync(Guid? organizationId, CancellationToken ct) =>
        (await _db.ClubHouseAccounts.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId && (a.CompanyId != null || a.ContactId != null))
            .Select(a => new { a.MemberId, a.CompanyId })
            .ToListAsync(ct))
        .ToDictionary(a => a.MemberId, a => a.CompanyId != null ? ClubAccountHolders.Company : ClubAccountHolders.Contact);

    /// <summary>
    /// The member's account row, read at the version this write is checked against. A
    /// member with no account yet gets one, added only if the write goes ahead (<see cref="Touch"/>).
    /// </summary>
    /// <remarks>
    /// Read it before anything the write decides on (what a charge still owes, whether it
    /// was voided): a write for the member saved after this read moves its version, and
    /// this write is then refused rather than deciding on figures that have moved.
    /// </remarks>
    private async Task<ClubHouseAccount> AccountAsync(Guid? organizationId, Guid memberId, CancellationToken ct) =>
        await _db.ClubHouseAccounts.FirstOrDefaultAsync(a => a.MemberId == memberId && a.OrganizationId == organizationId, ct)
        ?? new ClubHouseAccount
        {
            Id = Guid.NewGuid(), OrganizationId = organizationId, MemberId = memberId,
            Currency = await WorkspaceCurrencyAsync(organizationId, ct), OpenedAtUtc = DateTime.UtcNow,
        };

    /// <summary>Marks the account as written by this write, so the save is checked against the version it was read at.</summary>
    private void Touch(ClubHouseAccount account)
    {
        if (_db.Entry(account).State == EntityState.Detached) _db.ClubHouseAccounts.Add(account);
        account.LastEntryAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Puts the member's balance after this write on their record, for the lists that show
    /// it: what the saved entries add up to, and <paramref name="change"/> for the ones this
    /// write adds. The account row keeps any other write for the member out until this one
    /// has saved, so the figure cannot be overtaken.
    /// </summary>
    private async Task StageBalanceAsync(Guid? organizationId, ClubMember? member, decimal change, CancellationToken ct)
    {
        if (member == null) return;
        member.HouseAccountBalance = await BalanceOfAsync(organizationId, member.Id, null, ct) + change;
    }

    /// <summary>As for a member; a company's or a contact's account keeps no figure anywhere but its entries.</summary>
    private Task StageBalanceAsync(Guid? organizationId, Holder holder, decimal change, CancellationToken ct) =>
        StageBalanceAsync(organizationId, holder.Member, change, ct);

    private async Task<decimal> BalanceOfAsync(Guid? organizationId, Guid memberId, DateTime? asOfUtc, CancellationToken ct)
    {
        var credits = await _db.ClubPayments
            .Where(p => p.OrganizationId == organizationId && p.MemberId == memberId && (asOfUtc == null || p.ReceivedAtUtc <= asOfUtc))
            .SumAsync(p => p.Amount, ct);
        var debits = await _db.ClubCharges
            .Where(c => c.OrganizationId == organizationId && c.MemberId == memberId && (asOfUtc == null || c.PostedAtUtc <= asOfUtc))
            .SumAsync(c => c.Amount, ct);
        return credits - debits;
    }

    /// <summary>Credit entries with money not yet spent on a charge, oldest first.</summary>
    private async Task<List<(Guid PaymentId, decimal Left)>> UnallocatedCreditAsync(Guid? organizationId, Guid memberId, CancellationToken ct)
    {
        var rows = await _db.ClubPayments.AsNoTracking()
            .Where(p => p.OrganizationId == organizationId && p.MemberId == memberId)
            .ToListAsync(ct);
        var ids = rows.Where(r => r.ReversesPaymentId == null).Select(r => r.Id).ToList();
        var allocated = await _db.ClubPaymentAllocations.AsNoTracking()
            .Where(a => ids.Contains(a.PaymentId))
            .GroupBy(a => a.PaymentId).Select(g => new { PaymentId = g.Key, Sum = g.Sum(a => a.Amount) })
            .ToDictionaryAsync(g => g.PaymentId, g => g.Sum, ct);
        var reversed = rows.Where(r => r.ReversesPaymentId != null).GroupBy(r => r.ReversesPaymentId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));
        return rows.Where(r => r.ReversesPaymentId == null)
            .OrderBy(r => r.ReceivedAtUtc).ThenBy(r => r.CreatedAtUtc)
            .Select(r => (r.Id, Left: r.Amount + reversed.GetValueOrDefault(r.Id) - allocated.GetValueOrDefault(r.Id)))
            .Where(r => r.Left > 0)
            .ToList();
    }

    private async Task<decimal> PaidAsync(Guid chargeId, CancellationToken ct) =>
        await _db.ClubPaymentAllocations.Where(a => a.ChargeId == chargeId).SumAsync(a => a.Amount, ct);

    private async Task<List<(Guid ChargeId, decimal Net)>> NetAllocationsAsync(Guid paymentId, CancellationToken ct) =>
        (await _db.ClubPaymentAllocations.AsNoTracking().Where(a => a.PaymentId == paymentId)
            .GroupBy(a => a.ChargeId).Select(g => new { ChargeId = g.Key, Net = g.Sum(a => a.Amount) })
            .ToListAsync(ct))
        .Select(g => (g.ChargeId, g.Net)).ToList();

    /// <summary>What each charge has been paid, and when the payment that settled it was received.</summary>
    private async Task<Dictionary<Guid, (decimal Paid, DateTime? LastReceivedAtUtc)>> PaidByChargeAsync(Guid? organizationId, Guid? memberId, CancellationToken ct)
    {
        var rows = await _db.ClubPaymentAllocations.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId)
            .Join(_db.ClubPayments, a => a.PaymentId, p => p.Id, (a, p) => new { a.ChargeId, a.Amount, p.ReceivedAtUtc, p.MemberId })
            .Where(x => memberId == null || x.MemberId == memberId)
            .ToListAsync(ct);
        return rows.GroupBy(r => r.ChargeId).ToDictionary(
            g => g.Key,
            g => (g.Sum(r => r.Amount), g.Where(r => r.Amount > 0).Select(r => (DateTime?)r.ReceivedAtUtc).Max()));
    }

    private async Task<string?> LastNameOnAccountAsync(Guid? organizationId, Guid memberId, CancellationToken ct) =>
        await _db.ClubCharges.Where(c => c.OrganizationId == organizationId && c.MemberId == memberId)
            .OrderByDescending(c => c.CreatedAtUtc).Select(c => c.MemberName).FirstOrDefaultAsync(ct)
        ?? await _db.ClubPayments.Where(p => p.OrganizationId == organizationId && p.MemberId == memberId)
            .OrderByDescending(p => p.CreatedAtUtc).Select(p => p.MemberName).FirstOrDefaultAsync(ct);

    private async Task<string> AccountCurrencyAsync(Guid? organizationId, Guid memberId, CancellationToken ct) =>
        await _db.ClubHouseAccounts.Where(a => a.MemberId == memberId && a.OrganizationId == organizationId)
            .Select(a => a.Currency).FirstOrDefaultAsync(ct)
        ?? await WorkspaceCurrencyAsync(organizationId, ct);

    /// <summary>The workspace's currency (Organization.Currency), CHF by default.</summary>
    private async Task<string> WorkspaceCurrencyAsync(Guid? organizationId, CancellationToken ct)
    {
        if (organizationId == null) return RegionalSettings.DefaultCurrency;
        var currency = await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => o.Currency).FirstOrDefaultAsync(ct);
        return RegionalSettings.TryCurrency(currency, out var code) ? code : RegionalSettings.DefaultCurrency;
    }

    // ── saving ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Saves this write as one transaction: first what it has changed so far (the rows it
    /// locks: a house account, a tab, a card, a subscription), checked against the version
    /// each was read at, then the entries <paramref name="entries"/> adds. A write that lost a
    /// race (a version that moved, a unique index another request filled first) is let go
    /// of whole and answers <paramref name="conflict"/>.
    /// </summary>
    /// <remarks>
    /// Two saves rather than one because the in-memory provider the HTTP tests run on has no
    /// transactions and writes a save's new rows before it finds a stale version: with the
    /// locked rows saved first, a refused write has written nothing there either. On SQL
    /// Server the two are one transaction, and the first holds the locked rows until it
    /// commits, so a second write for the same member waits, then is refused.
    /// </remarks>
    private async Task<Result> SaveAsync(Error conflict, CancellationToken ct, Func<Task>? entries = null)
    {
        var transaction = entries != null && _db.Database.IsRelational() && _db.Database.CurrentTransaction == null
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            await _db.SaveChangesAsync(ct);
            if (entries != null)
            {
                await entries();
                await _db.SaveChangesAsync(ct);
            }
            if (transaction != null) await transaction.CommitAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            Forget();
            return conflict;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            Forget();
            return conflict;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }

    private void Forget()
    {
        foreach (var entry in _db.ChangeTracker.Entries()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList())
            entry.State = EntityState.Detached;
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    // ── building rows ────────────────────────────────────────────────────────

    private ClubCharge NewCharge(Guid? organizationId, Holder member, ClubHouseAccount account, string entryType, string kind, string description, decimal amount)
    {
        var charge = new ClubCharge
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = member.Id,
            MemberName = member.Name,
            EntryType = entryType,
            Kind = kind,
            Description = description,
            Amount = amount,
            Currency = account.Currency,
            PostedAtUtc = DateTime.UtcNow,
        };
        Stamp(charge);
        return charge;
    }

    private ClubPayment Reverse(ClubPayment payment, string entryType, decimal amount, string reason, string? method, string? reference)
    {
        var reversal = new ClubPayment
        {
            Id = Guid.NewGuid(),
            OrganizationId = payment.OrganizationId,
            MemberId = payment.MemberId,
            MemberName = payment.MemberName,
            EntryType = entryType,
            Method = method,
            Amount = -amount,
            Currency = payment.Currency,
            ReceivedAtUtc = DateTime.UtcNow,
            Description = payment.Description,
            Reference = reference,
            ReversesPaymentId = payment.Id,
            Reason = reason,
        };
        Stamp(reversal);
        return reversal;
    }

    private ClubPaymentAllocation Allocate(Guid? organizationId, Guid paymentId, Guid chargeId, decimal amount, Guid? reversalId) => new()
    {
        Id = Guid.NewGuid(),
        OrganizationId = organizationId,
        PaymentId = paymentId,
        ChargeId = chargeId,
        Amount = amount,
        ReversalId = reversalId,
        CreatedAtUtc = DateTime.UtcNow,
        CreatedByUserId = _user.UserId,
    };

    private void Stamp(ClubCharge row)
    {
        row.CreatedAtUtc = DateTime.UtcNow;
        row.CreatedByUserId = _user.UserId;
        row.CreatedByName = Who();
    }

    private void Stamp(ClubPayment row)
    {
        row.CreatedAtUtc = DateTime.UtcNow;
        row.CreatedByUserId = _user.UserId;
        row.CreatedByName = Who();
    }

    private string? Who() => string.IsNullOrWhiteSpace(_user.UserName) ? null : Truncate(_user.UserName.Trim(), 200);

    // ── answers ──────────────────────────────────────────────────────────────

    private static ChargeDto ToDto(ClubCharge c, ClubCharge? reversal, (decimal Paid, DateTime? LastReceivedAtUtc) paid, DateTime now)
    {
        var owed = c.Amount - paid.Paid;
        var status = reversal != null ? "Voided"
            : owed <= 0 && c.Amount > 0 ? "Paid"
            : paid.Paid > 0 ? "PartiallyPaid"
            : "Pending";
        // Nothing ever set a charge Overdue, so "Past due" read zero however late the dues
        // were. It is worked out from the due date on every read instead.
        if (status is "Pending" or "PartiallyPaid" && c.DueAtUtc is { } due && due < now && owed > 0) status = "Overdue";
        return new ChargeDto
        {
            Id = c.Id,
            MemberId = c.MemberId,
            MemberName = c.MemberName,
            EntryType = c.EntryType,
            Kind = c.Kind,
            Description = c.Description,
            Amount = c.Amount,
            Currency = c.Currency,
            PaidAmount = paid.Paid,
            Status = status,
            PostedAtUtc = c.PostedAtUtc,
            DueAtUtc = c.DueAtUtc,
            PaidAtUtc = status == "Paid" ? paid.LastReceivedAtUtc : null,
            Reference = c.Reference,
            TabId = c.TabId,
            SubscriptionId = c.SubscriptionId,
            BillingPeriodStartUtc = c.BillingPeriodStartUtc,
            CatalogItemId = c.CatalogItemId,
            VatRate = c.VatRate,
            IncurredByMemberId = c.IncurredByMemberId,
            IncurredByName = c.IncurredByName,
            Reason = c.Reason,
            VoidedAtUtc = reversal?.PostedAtUtc,
            VoidedByUserId = reversal?.CreatedByUserId,
            VoidedByName = reversal?.CreatedByName,
            VoidReason = reversal?.Reason,
            CreatedByName = c.CreatedByName,
            CreatedAtUtc = c.CreatedAtUtc,
        };
    }

    private async Task<ChargeDto> ChargeDtoAsync(Guid? organizationId, ClubCharge charge, CancellationToken ct)
    {
        var reversal = await _db.ClubCharges.AsNoTracking().FirstOrDefaultAsync(c => c.ReversesChargeId == charge.Id, ct);
        var paid = await PaidByChargeAsync(organizationId, charge.MemberId, ct);
        return ToDto(charge, reversal, paid.GetValueOrDefault(charge.Id), DateTime.UtcNow);
    }

    private static PaymentDto ToDto(ClubPayment p, IReadOnlyList<ClubPayment> reversals, IReadOnlyList<ClubPaymentAllocation> allocations)
    {
        var voided = reversals.FirstOrDefault(r => r.EntryType == ClubEntryTypes.Void);
        var refunds = reversals.Where(r => r.EntryType == ClubEntryTypes.Refund).OrderBy(r => r.ReceivedAtUtc).ToList();
        var refunded = -refunds.Sum(r => r.Amount);
        var status = voided != null ? "Voided" : refunded >= p.Amount && refunded > 0 ? "Refunded" : refunded > 0 ? "PartiallyRefunded" : null;
        var applied = allocations.Where(a => a.ReversalId == null && a.Amount > 0).OrderBy(a => a.CreatedAtUtc).Select(a => a.ChargeId).Distinct().ToList();
        return new PaymentDto
        {
            Id = p.Id,
            MemberId = p.MemberId,
            MemberName = p.MemberName,
            EntryType = p.EntryType,
            Amount = p.Amount,
            Currency = p.Currency,
            Method = p.Method,
            Reference = p.Reference,
            Notes = p.Notes,
            Provider = p.Provider,
            ProviderPaymentId = p.ProviderPaymentId,
            ReceivedAtUtc = p.ReceivedAtUtc,
            ChargeId = applied.Count > 0 ? applied[0] : null,
            AppliedToChargeIds = applied,
            Allocations = allocations.GroupBy(a => a.ChargeId).Select(g => new PaymentAllocationDto(g.Key, g.Sum(a => a.Amount)))
                .Where(a => a.Amount != 0).ToList(),
            Status = status,
            RefundedAmount = refunded,
            Refunds = refunds.Select(r => new PaymentRefundDto(r.Id, -r.Amount, r.Reason, r.Method, r.ReceivedAtUtc, r.CreatedByName, r.Provider, r.ProviderPaymentId)).ToList(),
            VoidedAtUtc = voided?.ReceivedAtUtc,
            VoidedByUserId = voided?.CreatedByUserId,
            VoidedByName = voided?.CreatedByName,
            VoidReason = voided?.Reason,
            CreatedByName = p.CreatedByName,
            CreatedAtUtc = p.CreatedAtUtc,
        };
    }

    private async Task<PaymentDto> PaymentDtoAsync(Guid? organizationId, ClubPayment payment, CancellationToken ct)
    {
        var reversals = await _db.ClubPayments.AsNoTracking().Where(p => p.ReversesPaymentId == payment.Id && p.OrganizationId == organizationId).ToListAsync(ct);
        var allocations = await _db.ClubPaymentAllocations.AsNoTracking().Where(a => a.PaymentId == payment.Id).ToListAsync(ct);
        return ToDto(payment, reversals, allocations);
    }

    private static string PaymentWords(ClubPayment p) => p.EntryType switch
    {
        ClubEntryTypes.Payment => p.Method is { } m ? $"Payment ({m})" : "Payment",
        ClubEntryTypes.Void => "Payment voided",
        ClubEntryTypes.Refund => "Refund",
        _ => p.Description ?? p.EntryType,
    };

    // ── reading requests ─────────────────────────────────────────────────────

    private async Task<ClubMember?> MemberAsync(Guid? organizationId, Guid? memberId, CancellationToken ct) =>
        memberId is not { } id ? null
        : await _db.ClubMembers.FirstOrDefaultAsync(m => m.Id == id && m.OrganizationId == organizationId, ct);

    private static string NameOf(ClubMember member) => Truncate($"{member.FirstName} {member.LastName}".Trim(), 200);

    private async Task<ClubCharge?> ChargeByKeyAsync(Guid? organizationId, string key, CancellationToken ct) =>
        await _db.ClubCharges.AsNoTracking().FirstOrDefaultAsync(c => c.OrganizationId == organizationId && c.IdempotencyKey == key, ct);

    private async Task<ClubPayment?> PaymentByKeyAsync(Guid? organizationId, string key, CancellationToken ct) =>
        await _db.ClubPayments.AsNoTracking().FirstOrDefaultAsync(p => p.OrganizationId == organizationId && p.IdempotencyKey == key, ct);

    /// <summary>A positive amount of money, to the cent; null when it is not one.</summary>
    private static decimal? Money(decimal? value)
    {
        if (value is not { } v || v <= 0 || v >= MaxMoney) return null;
        var cents = Math.Round(v, 2, MidpointRounding.AwayFromZero);
        return cents > 0 ? cents : null;
    }

    /// <summary>The Idempotency-Key, trimmed; null when none was sent.</summary>
    private static Error? ReadKey(string? raw, out string? key)
    {
        key = string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
        return key?.Length > 100 ? DomainErrors.Club.TooLong("An Idempotency-Key", 100) : null;
    }

    /// <summary>The reason a void must give: not blank, and short enough to keep.</summary>
    private static Error? ReadReason(string? raw, out string reason)
    {
        reason = (raw ?? "").Trim();
        if (reason.Length == 0) return DomainErrors.Club.ReasonRequired;
        if (reason.Length > 500) return DomainErrors.Club.TooLong("A reason", 500);
        return null;
    }

    /// <summary>An optional date: empty is none, otherwise it must read as one. Stored as UTC.</summary>
    private static bool ReadDate(string? raw, out DateTime? date)
    {
        date = null;
        var text = (raw ?? "").Trim();
        if (text.Length == 0) return true;
        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var read))
            return false;
        date = DateTime.SpecifyKind(read, DateTimeKind.Utc);
        return true;
    }

    private static string? Trimmed(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
