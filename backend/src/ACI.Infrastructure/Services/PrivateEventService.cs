using System.Globalization;
using System.Text;
using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>Private events (see <see cref="IPrivateEvents"/> for the rules).</summary>
/// <remarks>
/// <para>
/// The event's row is saved against the version a screen read (EditVersion, If-Match) for its own
/// details, and against its row Version for its money, so two final invoices pressed at once post
/// once: every charge carries an idempotency key of the event's, and the second save of the row is
/// refused. Spaces are the reservations service's (holds keyed by the event), money the ledger's,
/// the contract the contract flow's; this service only decides what to ask of them.
/// </para>
/// </remarks>
public sealed class PrivateEventService : IPrivateEvents
{
    private const int MaxGuests = 10_000;
    private const decimal MaxAmount = 1_000_000m;
    private static readonly string[] LineKinds = [CatalogKinds.MenuItem, CatalogKinds.Product, CatalogKinds.Fee, CatalogKinds.Ticket];

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IClubLedger _ledger;
    private readonly IClubReservations _spaces;
    private readonly IClubCatalog _catalog;
    private readonly IContractService _contracts;
    private readonly IWorkspaceRecords _records;
    private readonly IWorkspaceModules _modules;

    public PrivateEventService(AppDbContext db, ICurrentUserService user, IClubLedger ledger, IClubReservations spaces, IClubCatalog catalog,
        IContractService contracts, IWorkspaceRecords records, IWorkspaceModules modules)
    {
        _db = db;
        _user = user;
        _ledger = ledger;
        _spaces = spaces;
        _catalog = catalog;
        _contracts = contracts;
        _records = records;
        _modules = modules;
    }

    // ── reads ────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<PrivateEventDto>> ListAsync(Guid organizationId, PrivateEventQuery query, CancellationToken ct = default)
    {
        var rows = _db.ClubPrivateEvents.AsNoTracking().Where(e => e.OrganizationId == organizationId);
        if (query.FromUtc is DateTime from) rows = rows.Where(e => e.EndAtUtc > from);
        if (query.ToUtc is DateTime to) rows = rows.Where(e => e.StartAtUtc < to);
        if (!string.IsNullOrWhiteSpace(query.Status)) rows = rows.Where(e => e.Status == query.Status.Trim());
        if (query.CompanyId is Guid company) rows = rows.Where(e => e.CompanyId == company);
        if (query.ContactId is Guid contact) rows = rows.Where(e => e.ContactId == contact);
        var list = await rows.OrderBy(e => e.StartAtUtc).Take(500).ToListAsync(ct);
        var answers = new List<PrivateEventDto>(list.Count);
        foreach (var e in list) answers.Add(await ToDtoAsync(organizationId, e, ct));
        return answers;
    }

    public async Task<Result<PrivateEventDto>> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default)
    {
        var e = await _db.ClubPrivateEvents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId, ct);
        return e == null ? DomainErrors.PrivateEvent.NotFound : await ToDtoAsync(organizationId, e, ct);
    }

    // ── the event ────────────────────────────────────────────────────────────

    public async Task<Result<PrivateEventDto>> CreateAsync(Guid organizationId, Guid userId, PrivateEventWriteRequest request, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var e = new ClubPrivateEvent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Currency = await CurrencyAsync(organizationId, ct),
            CreatedAtUtc = now,
            CreatedByUserId = _user.UserId,
            CreatedByName = Who(),
            UpdatedAtUtc = now,
            EditVersion = Guid.NewGuid(),
        };
        if (await ApplyAsync(organizationId, userId, e, request, creating: true, ct) is { } invalid) return invalid;
        var lines = new List<ClubPrivateEventLine>();
        foreach (var asked in request.Lines ?? [])
        {
            var line = await LineAsync(organizationId, e, asked, lines.Count, ct);
            if (!line.IsSuccess) return line.Error;
            lines.Add(line.Value);
        }
        _db.ClubPrivateEvents.Add(e);
        _db.ClubPrivateEventLines.AddRange(lines);
        await _db.SaveChangesAsync(ct);

        // Its spaces, held for its time: a clash refuses the event as a whole, rather than leaving
        // one made that has nowhere to be.
        var spaces = request.ResourceIds ?? [];
        if (spaces.Count > 0)
        {
            var held = await _spaces.HoldForPrivateEventAsync(organizationId, e.Id, HoldLabel(e), spaces, e.StartAtUtc, e.EndAtUtc, HoldStatus(e), ct);
            if (!held.IsSuccess)
            {
                _db.ChangeTracker.Clear();
                var made = await _db.ClubPrivateEvents.FirstAsync(x => x.Id == e.Id, ct);
                _db.ClubPrivateEventLines.RemoveRange(await _db.ClubPrivateEventLines.Where(l => l.PrivateEventId == e.Id).ToListAsync(ct));
                _db.ClubPrivateEvents.Remove(made);
                await _db.SaveChangesAsync(ct);
                return held.Error;
            }
        }
        return await GetAsync(organizationId, e.Id, ct);
    }

    public async Task<Result<PrivateEventDto>> UpdateAsync(Guid organizationId, Guid userId, Guid id, PrivateEventWriteRequest request, Guid? expectedVersion, CancellationToken ct = default)
    {
        var e = await _db.ClubPrivateEvents.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId, ct);
        if (e == null) return DomainErrors.PrivateEvent.NotFound;
        if ((expectedVersion ?? request.Version) is Guid version && e.EditVersion != version) return DomainErrors.PrivateEvent.Changed;
        if (e.Status is PrivateEventStatuses.Cancelled or PrivateEventStatuses.Completed) return DomainErrors.PrivateEvent.NotWhileStatus(e.Status);
        var before = PrivateEventRows.Snapshot(_db, e);
        if (await ApplyAsync(organizationId, userId, e, request, creating: false, ct) is { } invalid)
        {
            _db.ChangeTracker.Clear();
            return invalid;
        }
        // Its spaces follow its time, the spaces asked for, and its title (the diary's label).
        var respace = e.StartAtUtc != before.StartAtUtc || e.EndAtUtc != before.EndAtUtc || e.Title != before.Title || request.ResourceIds != null;
        var spaces = respace ? request.ResourceIds ?? await HeldResourcesAsync(organizationId, id, ct) : null;
        e.EditVersion = Guid.NewGuid();
        e.UpdatedAtUtc = DateTime.UtcNow;
        var refused = await PrivateEventRows.SaveWithSpacesAsync(_db, e, before, spaces is null || (spaces.Count == 0 && request.ResourceIds == null) ? null
            : () => HoldAsync(organizationId, e, spaces, HoldStatus(e), ct), ct);
        return refused ?? await GetAsync(organizationId, id, ct);
    }

    public async Task<Result<PrivateEventDto>> ConfirmAsync(Guid organizationId, Guid id, CancellationToken ct = default)
    {
        var e = await _db.ClubPrivateEvents.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId, ct);
        if (e == null) return DomainErrors.PrivateEvent.NotFound;
        if (e.Status != PrivateEventStatuses.Enquiry) return DomainErrors.PrivateEvent.NotWhileStatus(e.Status);
        // Agreed: its spaces' holds are confirmed with it.
        var before = PrivateEventRows.Snapshot(_db, e);
        var spaces = await HeldResourcesAsync(organizationId, id, ct);
        e.Status = PrivateEventStatuses.Confirmed;
        e.ConfirmedAtUtc = DateTime.UtcNow;
        e.EditVersion = Guid.NewGuid();
        e.UpdatedAtUtc = DateTime.UtcNow;
        var refused = await PrivateEventRows.SaveWithSpacesAsync(_db, e, before,
            spaces.Count == 0 ? null : () => HoldAsync(organizationId, e, spaces, ReservationStatuses.Confirmed, ct), ct);
        return refused ?? await GetAsync(organizationId, id, ct);
    }

    public async Task<Result<PrivateEventDto>> CompleteAsync(Guid organizationId, Guid id, CancellationToken ct = default)
    {
        var e = await _db.ClubPrivateEvents.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId, ct);
        if (e == null) return DomainErrors.PrivateEvent.NotFound;
        if (e.Status is not (PrivateEventStatuses.Enquiry or PrivateEventStatuses.Confirmed)) return DomainErrors.PrivateEvent.NotWhileStatus(e.Status);
        var before = PrivateEventRows.Snapshot(_db, e);
        e.Status = PrivateEventStatuses.Completed;
        e.EditVersion = Guid.NewGuid();
        e.UpdatedAtUtc = DateTime.UtcNow;
        var refused = await PrivateEventRows.SaveWithSpacesAsync(_db, e, before, () => ReleaseAsync(organizationId, id, ReservationStatuses.Completed, ct), ct);
        return refused ?? await GetAsync(organizationId, id, ct);
    }

    private async Task<Error?> HoldAsync(Guid organizationId, ClubPrivateEvent e, IReadOnlyList<Guid> spaces, string status, CancellationToken ct)
    {
        var held = await _spaces.HoldForPrivateEventAsync(organizationId, e.Id, HoldLabel(e), spaces, e.StartAtUtc, e.EndAtUtc, status, ct);
        return held.IsSuccess ? null : held.Error;
    }

    private async Task<Error?> ReleaseAsync(Guid organizationId, Guid id, string status, CancellationToken ct)
    {
        var released = await _spaces.ReleasePrivateEventAsync(organizationId, id, status, ct);
        return released.IsSuccess ? null : released.Error;
    }

    public async Task<Result<PrivateEventDto>> CancelAsync(Guid organizationId, Guid id, PrivateEventCancelRequest request, bool mayHandleMoney, CancellationToken ct = default)
    {
        var why = (request.Reason ?? "").Trim();
        if (why.Length is 0 or > 500) return DomainErrors.PrivateEvent.ReasonRequired;
        var e = await _db.ClubPrivateEvents.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId, ct);
        if (e == null) return DomainErrors.PrivateEvent.NotFound;
        if (e.Status is PrivateEventStatuses.Cancelled or PrivateEventStatuses.Completed) return DomainErrors.PrivateEvent.NotWhileStatus(e.Status);
        if (e.InvoicedAtUtc != null) return DomainErrors.PrivateEvent.Invoiced;

        // A deposit asked is kept or given back as the club decides: nothing moves without saying so.
        if (e.DepositChargeId is Guid deposit && e.DepositSettledAtUtc == null)
        {
            var choice = (request.Deposit ?? "").Trim().ToLowerInvariant();
            if (choice is not ("keep" or "release")) return DomainErrors.PrivateEvent.DepositChoiceRequired;
            if (choice == "release")
            {
                if (!mayHandleMoney)
                    return DomainErrors.Permission.Missing(Domain.Common.MemberPermissions.ManageMoney, "give a private event's deposit back");
                var cancelled = await _ledger.CancelChargeAsync(organizationId, deposit, "Private event cancelled: the deposit is given back as house credit", ct);
                if (!cancelled.IsSuccess && cancelled.Error.Code != DomainErrors.Club.ChargeVoided.Code) return cancelled.Error;
                e.DepositSettledAtUtc = DateTime.UtcNow;
            }
        }
        var before = PrivateEventRows.Snapshot(_db, e);
        e.Status = PrivateEventStatuses.Cancelled;
        e.CancelledAtUtc = DateTime.UtcNow;
        e.CancelReason = why;
        e.CancelledByName = Who();
        e.EditVersion = Guid.NewGuid();
        e.UpdatedAtUtc = DateTime.UtcNow;
        var refused = await PrivateEventRows.SaveWithSpacesAsync(_db, e, before, () => ReleaseAsync(organizationId, id, ReservationStatuses.Cancelled, ct), ct);
        return refused ?? await GetAsync(organizationId, id, ct);
    }

    // ── menu and services ────────────────────────────────────────────────────

    public async Task<Result<PrivateEventDto>> AddLineAsync(Guid organizationId, Guid id, PrivateEventLineRequest request, CancellationToken ct = default)
    {
        var e = await OpenForLinesAsync(organizationId, id, ct);
        if (!e.IsSuccess) return e.Error;
        var count = await _db.ClubPrivateEventLines.CountAsync(l => l.PrivateEventId == id, ct);
        var line = await LineAsync(organizationId, e.Value, request, count, ct);
        if (!line.IsSuccess) return line.Error;
        _db.ClubPrivateEventLines.Add(line.Value);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(organizationId, id, ct);
    }

    public async Task<Result<PrivateEventDto>> UpdateLineAsync(Guid organizationId, Guid id, Guid lineId, PrivateEventLineRequest request, CancellationToken ct = default)
    {
        var e = await OpenForLinesAsync(organizationId, id, ct);
        if (!e.IsSuccess) return e.Error;
        var line = await _db.ClubPrivateEventLines.FirstOrDefaultAsync(l => l.Id == lineId && l.PrivateEventId == id, ct);
        if (line == null) return DomainErrors.PrivateEvent.LineNotFound;
        if (request.Quantity is { } quantity)
        {
            if (Quantity(quantity) is not { } q) return DomainErrors.PrivateEvent.QuantityInvalid;
            line.Quantity = q;
        }
        // A catalogue item's price and VAT are the catalogue's, as for any charge; a typed line's change.
        if (line.CatalogItemId == null)
        {
            if (request.UnitPrice is { } price)
            {
                if (Amount(price, allowZero: true) is not { } p) return DomainErrors.PrivateEvent.AmountInvalid;
                line.UnitPrice = p;
            }
            if (request.VatRate is { } vat)
            {
                if (vat is < 0 or > 100) return DomainErrors.PrivateEvent.VatInvalid;
                line.VatRate = vat;
            }
            if (request.Name != null)
            {
                var name = request.Name.Trim();
                if (name.Length == 0) return DomainErrors.PrivateEvent.TitleRequired;
                if (name.Length > 200) return DomainErrors.PrivateEvent.TooLong("A line's name", 200);
                line.Name = name;
            }
        }
        await _db.SaveChangesAsync(ct);
        return await GetAsync(organizationId, id, ct);
    }

    public async Task<Result<PrivateEventDto>> RemoveLineAsync(Guid organizationId, Guid id, Guid lineId, CancellationToken ct = default)
    {
        var e = await OpenForLinesAsync(organizationId, id, ct);
        if (!e.IsSuccess) return e.Error;
        var line = await _db.ClubPrivateEventLines.FirstOrDefaultAsync(l => l.Id == lineId && l.PrivateEventId == id, ct);
        if (line == null) return DomainErrors.PrivateEvent.LineNotFound;
        _db.ClubPrivateEventLines.Remove(line);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(organizationId, id, ct);
    }

    private async Task<Result<ClubPrivateEvent>> OpenForLinesAsync(Guid organizationId, Guid id, CancellationToken ct)
    {
        var e = await _db.ClubPrivateEvents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId, ct);
        if (e == null) return DomainErrors.PrivateEvent.NotFound;
        if (e.Status is PrivateEventStatuses.Cancelled or PrivateEventStatuses.Completed) return DomainErrors.PrivateEvent.NotWhileStatus(e.Status);
        if (e.InvoicedAtUtc != null) return DomainErrors.PrivateEvent.Invoiced;
        return e;
    }

    private async Task<Result<ClubPrivateEventLine>> LineAsync(Guid organizationId, ClubPrivateEvent e, PrivateEventLineRequest request, int order, CancellationToken ct)
    {
        if (Quantity(request.Quantity ?? 1m) is not { } quantity) return DomainErrors.PrivateEvent.QuantityInvalid;
        var line = new ClubPrivateEventLine
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            PrivateEventId = e.Id,
            Quantity = quantity,
            SortOrder = order,
            CreatedAtUtc = DateTime.UtcNow,
        };
        if (request.CatalogItemId is Guid itemId)
        {
            // From the catalogue: its name, price and VAT, as a charge rung up from it takes them.
            var found = await _catalog.ChargeableAsync(organizationId, itemId, LineKinds, "a private event", ct);
            if (!found.IsSuccess) return found.Error;
            var item = found.Value;
            if (!string.Equals(item.Currency, e.Currency, StringComparison.OrdinalIgnoreCase))
                return DomainErrors.Club.CurrencyMismatch(item.Currency, e.Currency);
            line.CatalogItemId = item.Id;
            line.Name = Clip(item.Name, 200);
            line.UnitPrice = item.Price;
            line.VatRate = item.VatRate;
            return line;
        }
        var name = (request.Name ?? "").Trim();
        if (name.Length == 0) return DomainErrors.PrivateEvent.TitleRequired;
        if (name.Length > 200) return DomainErrors.PrivateEvent.TooLong("A line's name", 200);
        if (Amount(request.UnitPrice, allowZero: true) is not { } price) return DomainErrors.PrivateEvent.AmountInvalid;
        if (request.VatRate is { } vat && vat is < 0 or > 100) return DomainErrors.PrivateEvent.VatInvalid;
        line.Name = name;
        line.UnitPrice = price;
        line.VatRate = request.VatRate is { } rate ? Math.Round(rate, 2) : null;
        return line;
    }

    // ── money ────────────────────────────────────────────────────────────────

    public async Task<Result<PrivateEventDto>> RequestDepositAsync(Guid organizationId, Guid id, PrivateEventDepositRequest request, CancellationToken ct = default)
    {
        if (Amount(request.Amount, allowZero: false) is not { } amount) return DomainErrors.PrivateEvent.AmountInvalid;
        var e = await _db.ClubPrivateEvents.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId, ct);
        if (e == null) return DomainErrors.PrivateEvent.NotFound;
        if (e.Status is PrivateEventStatuses.Cancelled or PrivateEventStatuses.Completed) return DomainErrors.PrivateEvent.NotWhileStatus(e.Status);
        if (e.InvoicedAtUtc != null) return DomainErrors.PrivateEvent.Invoiced;
        if (e.DepositChargeId != null) return DomainErrors.PrivateEvent.DepositAsked;
        var payer = await PayerAsync(organizationId, e, ct);
        if (!payer.IsSuccess) return payer.Error;

        var zone = await ZoneAsync(organizationId, ct);
        var charged = await _ledger.PostChargeAsync(organizationId, new PostChargeRequest
        {
            MemberId = payer.Value,
            Kind = "Event",
            Description = Clip($"Deposit: {e.Title}, {When(e, zone, CultureInfo.InvariantCulture)}", 500),
            Amount = amount,
            DueAtUtc = request.DueAtUtc,
        }, $"pe-deposit:{e.Id}", ct);
        if (!charged.IsSuccess) return charged.Error;
        e.DepositAmount = amount;
        e.DepositChargeId = charged.Value.Id;
        e.DepositRequestedAtUtc = DateTime.UtcNow;
        e.BillToAccountId ??= charged.Value.MemberId;
        e.UpdatedAtUtc = DateTime.UtcNow;
        if (!await TrySaveAsync(ct))
        {
            // Asked at the same moment by someone else: the deposit's key gave both the one charge.
            var again = await _db.ClubPrivateEvents.FirstAsync(x => x.Id == id, ct);
            again.DepositAmount ??= amount;
            again.DepositChargeId ??= charged.Value.Id;
            again.DepositRequestedAtUtc ??= DateTime.UtcNow;
            again.BillToAccountId ??= charged.Value.MemberId;
            if (!await TrySaveAsync(ct)) return DomainErrors.PrivateEvent.Changed;
        }
        return await GetAsync(organizationId, id, ct);
    }

    /// <remarks>
    /// The lines go on the client's account as one charge per VAT rate, so the statement's VAT is
    /// right. The deposit is then set against them: its charge is taken back (CancelChargeAsync), what
    /// was paid on it becomes credit, and that credit pays the invoice. The deposit was only ever an
    /// advance on the invoice, so its money ends where the invoice's VAT is, once.
    /// </remarks>
    public async Task<Result<PrivateEventDto>> InvoiceAsync(Guid organizationId, Guid id, PrivateEventInvoiceRequest request, CancellationToken ct = default)
    {
        var e = await _db.ClubPrivateEvents.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId, ct);
        if (e == null) return DomainErrors.PrivateEvent.NotFound;
        if (e.Status == PrivateEventStatuses.Cancelled) return DomainErrors.PrivateEvent.NotWhileStatus(e.Status);
        if (e.InvoicedAtUtc != null) return DomainErrors.PrivateEvent.Invoiced;
        var lines = await _db.ClubPrivateEventLines.Where(l => l.PrivateEventId == id).OrderBy(l => l.SortOrder).ToListAsync(ct);
        var billable = lines.Where(l => Total(l) > 0).ToList();
        if (billable.Count == 0) return DomainErrors.PrivateEvent.NothingToInvoice;
        var payer = await PayerAsync(organizationId, e, ct);
        if (!payer.IsSuccess) return payer.Error;

        var charges = new List<ChargeDto>();
        foreach (var group in billable.GroupBy(l => l.VatRate).OrderByDescending(g => g.Sum(Total)))
        {
            var names = string.Join(", ", group.Select(l => l.Quantity == 1 ? l.Name : string.Create(CultureInfo.InvariantCulture, $"{l.Quantity:0.##} × {l.Name}")));
            var rate = group.Key is decimal r ? r.ToString("0.##", CultureInfo.InvariantCulture) : "none";
            var charged = await _ledger.PostChargeAsync(organizationId, new PostChargeRequest
            {
                MemberId = payer.Value,
                Kind = "Event",
                Description = Clip($"{e.Title}: {names}", 500),
                Amount = group.Sum(Total),
                VatRate = group.Key,
                DueAtUtc = request.DueAtUtc,
            }, $"pe-invoice:{e.Id}:{rate}", ct);
            if (!charged.IsSuccess) return charged.Error;
            charges.Add(charged.Value);
            foreach (var line in group) line.ChargeId = charged.Value.Id;
        }

        if (e.DepositChargeId is Guid deposit && e.DepositSettledAtUtc == null)
        {
            var paid = await _db.ClubPaymentAllocations.Where(a => a.ChargeId == deposit).SumAsync(a => a.Amount, ct);
            var cancelled = await _ledger.CancelChargeAsync(organizationId, deposit, "Set against the private event's final invoice", ct);
            if (!cancelled.IsSuccess && cancelled.Error.Code != DomainErrors.Club.ChargeVoided.Code) return cancelled.Error;
            if (paid > 0)
            {
                // What the deposit paid pays the invoice, the largest charge first.
                var allocations = new List<PaymentAllocationRequest>();
                var left = paid;
                foreach (var charge in charges)
                {
                    if (left <= 0) break;
                    var take = Math.Min(left, charge.Amount - charge.PaidAmount);
                    if (take <= 0) continue;
                    allocations.Add(new PaymentAllocationRequest { ChargeId = charge.Id, Amount = take });
                    left -= take;
                }
                if (allocations.Count > 0)
                {
                    var applied = await _ledger.RecordPaymentAsync(organizationId, new RecordPaymentRequest
                    {
                        Method = "AppliedCredit",
                        Amount = allocations.Sum(a => a.Amount),
                        Allocations = allocations,
                        Notes = "The private event's deposit",
                    }, null, ct);
                    if (!applied.IsSuccess) return applied.Error;
                }
            }
            e.DepositSettledAtUtc = DateTime.UtcNow;
        }
        e.InvoicedAtUtc = DateTime.UtcNow;
        e.BillToAccountId ??= charges[0].MemberId;
        e.UpdatedAtUtc = DateTime.UtcNow;
        if (!await TrySaveAsync(ct)) return DomainErrors.PrivateEvent.Changed;
        return await GetAsync(organizationId, id, ct);
    }

    /// <summary>
    /// The account the event's money goes on: the company's (opened if need be), else the linked
    /// contact's member account, else the contact's own (opened if need be). Once money is on an
    /// account it stays the account.
    /// </summary>
    private async Task<Result<Guid>> PayerAsync(Guid organizationId, ClubPrivateEvent e, CancellationToken ct)
    {
        if (e.BillToAccountId is Guid fixedAccount) return fixedAccount;
        if (e.CompanyId is Guid company)
        {
            var opened = await _ledger.OpenClientAccountAsync(organizationId, ClubAccountHolders.Company, company, ct);
            return opened.IsSuccess ? company : opened.Error;
        }
        if (e.ContactId is Guid contact)
        {
            var member = await _db.ClubMembers.AsNoTracking()
                .Where(m => m.OrganizationId == organizationId && m.ContactId == contact && m.ErasedAtUtc == null)
                .Select(m => (Guid?)m.Id).FirstOrDefaultAsync(ct);
            if (member is Guid memberId) return memberId;
            var opened = await _ledger.OpenClientAccountAsync(organizationId, ClubAccountHolders.Contact, contact, ct);
            return opened.IsSuccess ? contact : opened.Error;
        }
        return DomainErrors.PrivateEvent.ClientRequired;
    }

    // ── contract ─────────────────────────────────────────────────────────────

    public async Task<Result<PrivateEventDto>> DraftContractAsync(Guid organizationId, Guid userId, Guid id, CancellationToken ct = default)
    {
        var e = await _db.ClubPrivateEvents.FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId, ct);
        if (e == null) return DomainErrors.PrivateEvent.NotFound;
        if (e.Status is PrivateEventStatuses.Cancelled or PrivateEventStatuses.Completed) return DomainErrors.PrivateEvent.NotWhileStatus(e.Status);
        if (e.DealId is not Guid dealId) return DomainErrors.PrivateEvent.DealRequiredForContract;
        // Contracts are the Sales side's: with it turned off, nobody could send or sign one.
        if (!await _modules.IsOnAsync(organizationId, Domain.Common.WorkspaceModules.Sales, ct)) return DomainErrors.Modules.Off(Domain.Common.WorkspaceModules.Sales);
        if (e.ContractId is Guid existing
            && await _db.Contracts.AsNoTracking().Where(c => c.Id == existing).Select(c => c.Status).FirstOrDefaultAsync(ct) is { } status
            && status is not (ContractStatuses.Voided or ContractStatuses.Declined))
            return DomainErrors.PrivateEvent.ContractExists;

        var org = await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => new { o.Locale, o.TimeZone }).FirstOrDefaultAsync(ct);
        var culture = ContractDealFields.CultureOf(org?.Locale);
        var zone = RegionalSettings.ZoneOf(org?.TimeZone);
        var lines = await _db.ClubPrivateEventLines.AsNoTracking().Where(l => l.PrivateEventId == id).OrderBy(l => l.SortOrder).ToListAsync(ct);
        var spaces = await _db.ClubReservations.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId && r.PrivateEventId == id && ReservationStatuses.Holding.Contains(r.Status))
            .OrderBy(r => r.ResourceName).Select(r => r.ResourceName).ToListAsync(ct);
        string Money(decimal amount) => ContractDealFields.Money(amount, e.Currency, culture);
        var table = new StringBuilder();
        foreach (var line in lines)
        {
            table.Append(string.Create(culture, $"{line.Quantity:0.##} × {line.Name} at {Money(line.UnitPrice)} = {Money(Total(line))}"));
            if (line.VatRate is decimal vat) table.Append(string.Create(culture, $" (incl. {vat:0.0#}% VAT)"));
            table.Append('\n');
        }
        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["event.title"] = e.Title,
            ["event.when"] = When(e, zone, culture),
            ["event.spaces"] = spaces.Count == 0 ? "To be agreed with the Club" : string.Join(", ", spaces),
            ["event.guests"] = e.GuestCount.ToString(culture),
            ["event.lines"] = lines.Count == 0 ? "To be agreed with the Club." : table.ToString().TrimEnd('\n'),
            ["event.total"] = Money(lines.Sum(Total)),
            ["event.depositClause"] = e.DepositAmount is decimal deposit
                ? $"\nA deposit of {Money(deposit)} is payable before the event. It is set against the final invoice."
                : "",
            ["contract.cancellationTerms"] = "Cancellations are handled as set out in the Club's terms for private events, which the Client has received.",
        };
        // The Client is the company hiring, or the event's contact; the deal's contact signs for it.
        var client = e.CompanyId is Guid companyId
            ? await _db.Companies.AsNoTracking().Where(c => c.Id == companyId).Select(c => c.Name).FirstOrDefaultAsync(ct)
            : e.ContactId is Guid contactId
                ? await _db.Contacts.AsNoTracking().Where(c => c.Id == contactId).Select(c => c.Name).FirstOrDefaultAsync(ct)
                : null;
        if (!string.IsNullOrWhiteSpace(client)) values["lead.name"] = client.Trim();
        var drafted = await _contracts.CreateDraftAsync(userId, organizationId, new CreateContractDraftRequest
        {
            DealId = dealId,
            Title = Clip($"Private event agreement — {e.Title}", 200),
            TemplateOverride = ContractTemplate.PrivateEventTemplate,
            Values = values,
        }, ct);
        if (drafted.IsFailure) return drafted.Error;
        e.ContractId = drafted.Value.Id;
        e.UpdatedAtUtc = DateTime.UtcNow;
        if (!await TrySaveAsync(ct)) return DomainErrors.PrivateEvent.Changed;
        return await GetAsync(organizationId, id, ct);
    }

    // ── guests ───────────────────────────────────────────────────────────────

    public async Task<Result<PrivateEventDto>> AddGuestAsync(Guid organizationId, Guid id, PrivateEventGuestRequest request, CancellationToken ct = default)
    {
        var e = await _db.ClubPrivateEvents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId, ct);
        if (e == null) return DomainErrors.PrivateEvent.NotFound;
        if (e.Status == PrivateEventStatuses.Cancelled) return DomainErrors.PrivateEvent.NotWhileStatus(e.Status);
        var name = (request.Name ?? "").Trim();
        Guid? memberId = null;
        if (request.MemberId is Guid asked)
        {
            var member = await _db.ClubMembers.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == asked && m.OrganizationId == organizationId && m.ErasedAtUtc == null, ct);
            if (member == null) return DomainErrors.PrivateEvent.MemberNotFound;
            memberId = member.Id;
            if (name.Length == 0) name = $"{member.FirstName} {member.LastName}".Trim();
        }
        if (name.Length == 0) return DomainErrors.PrivateEvent.TitleRequired;
        if (name.Length > 200) return DomainErrors.PrivateEvent.TooLong("A guest's name", 200);
        var email = (request.Email ?? "").Trim();
        if (email.Length > 0 && (email.Length > 320 || !ValidationHelper.IsValidEmail(email))) return DomainErrors.Corporate.EmailInvalid;
        var notes = (request.Notes ?? "").Trim();
        if (notes.Length > 500) return DomainErrors.PrivateEvent.TooLong("A note", 500);
        _db.ClubPrivateEventGuests.Add(new ClubPrivateEventGuest
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            PrivateEventId = id,
            Name = name,
            Email = email.Length == 0 ? null : email,
            MemberId = memberId,
            Notes = notes.Length == 0 ? null : notes,
            AddedAtUtc = DateTime.UtcNow,
            AddedByName = Who(),
        });
        await _db.SaveChangesAsync(ct);
        return await GetAsync(organizationId, id, ct);
    }

    public async Task<Result<PrivateEventDto>> RemoveGuestAsync(Guid organizationId, Guid id, Guid guestId, CancellationToken ct = default)
    {
        var guest = await _db.ClubPrivateEventGuests.FirstOrDefaultAsync(g => g.Id == guestId && g.PrivateEventId == id && g.OrganizationId == organizationId, ct);
        if (guest == null) return DomainErrors.PrivateEvent.GuestNotFound;
        _db.ClubPrivateEventGuests.Remove(guest);
        await _db.SaveChangesAsync(ct);
        return await GetAsync(organizationId, id, ct);
    }

    public async Task<Result<PrivateEventDto>> CheckInGuestAsync(Guid organizationId, Guid id, Guid guestId, bool undo, CancellationToken ct = default)
    {
        var e = await _db.ClubPrivateEvents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.OrganizationId == organizationId, ct);
        if (e == null) return DomainErrors.PrivateEvent.NotFound;
        if (e.Status == PrivateEventStatuses.Cancelled) return DomainErrors.PrivateEvent.NotWhileStatus(e.Status);
        var guest = await _db.ClubPrivateEventGuests.FirstOrDefaultAsync(g => g.Id == guestId && g.PrivateEventId == id && g.OrganizationId == organizationId, ct);
        if (guest == null) return DomainErrors.PrivateEvent.GuestNotFound;
        if (undo)
        {
            guest.CheckedInAtUtc = null;
            guest.CheckedInByName = null;
        }
        else if (guest.CheckedInAtUtc == null)
        {
            // Checked in once: a second press at the door (two staff, a double tap) keeps the first time.
            guest.CheckedInAtUtc = DateTime.UtcNow;
            guest.CheckedInByName = Who();
        }
        await _db.SaveChangesAsync(ct);
        return await GetAsync(organizationId, id, ct);
    }

    // ── reading the request ──────────────────────────────────────────────────

    private async Task<Error?> ApplyAsync(Guid organizationId, Guid userId, ClubPrivateEvent e, PrivateEventWriteRequest request, bool creating, CancellationToken ct)
    {
        if (creating || request.Title != null)
        {
            var title = (request.Title ?? "").Trim();
            if (title.Length == 0) return DomainErrors.PrivateEvent.TitleRequired;
            if (title.Length > 200) return DomainErrors.PrivateEvent.TooLong("A title", 200);
            e.Title = title;
        }
        var clientChanges = request.CompanyId != null || request.ContactId != null;
        if (clientChanges && !creating && e.BillToAccountId != null
            && ((request.CompanyId != null && (request.CompanyId == Guid.Empty ? null : request.CompanyId) != e.CompanyId)
                || (request.ContactId != null && (request.ContactId == Guid.Empty ? null : request.ContactId) != e.ContactId)))
            return DomainErrors.PrivateEvent.ClientHasMoney;
        if (request.CompanyId != null)
        {
            if (request.CompanyId == Guid.Empty) e.CompanyId = null;
            else if (!await _db.Companies.AsNoTracking().AnyAsync(c => c.Id == request.CompanyId && c.OrganizationId == organizationId, ct))
                return DomainErrors.PrivateEvent.CompanyNotFound;
            else e.CompanyId = request.CompanyId;
        }
        if (request.ContactId != null)
        {
            if (request.ContactId == Guid.Empty) e.ContactId = null;
            else if (!await _db.Contacts.AsNoTracking().AnyAsync(c => c.Id == request.ContactId && c.OrganizationId == organizationId, ct))
                return DomainErrors.PrivateEvent.ContactNotFound;
            else e.ContactId = request.ContactId;
        }
        if (e.CompanyId == null && e.ContactId == null) return DomainErrors.PrivateEvent.ClientRequired;
        if (request.DealId != null)
        {
            if (request.DealId == Guid.Empty) e.DealId = null;
            // Checked as the person asking: a private deal they may not open cannot be linked.
            else if (!await _records.ExistsAsync(RecordTypes.Deal, request.DealId.Value, userId, organizationId, ct))
                return DomainErrors.PrivateEvent.DealNotFound;
            else e.DealId = request.DealId;
        }
        var start = request.StartAtUtc != null ? ReadDate(request.StartAtUtc) : (creating ? null : e.StartAtUtc);
        var end = request.EndAtUtc != null ? ReadDate(request.EndAtUtc) : (creating ? null : e.EndAtUtc);
        if (start is not DateTime s || end is not DateTime en || en <= s || en - s > TimeSpan.FromDays(14)) return DomainErrors.PrivateEvent.TimesInvalid;
        e.StartAtUtc = s;
        e.EndAtUtc = en;
        if (request.GuestCount != null)
        {
            if (request.GuestCount is not (>= 0 and <= MaxGuests)) return DomainErrors.PrivateEvent.GuestCountInvalid;
            e.GuestCount = request.GuestCount.Value;
        }
        if (request.Notes != null)
        {
            var notes = request.Notes.Trim();
            if (notes.Length > 2000) return DomainErrors.PrivateEvent.TooLong("Notes", 2000);
            e.Notes = notes.Length == 0 ? null : notes;
        }
        return null;
    }

    // ── answers ──────────────────────────────────────────────────────────────

    private async Task<PrivateEventDto> ToDtoAsync(Guid organizationId, ClubPrivateEvent e, CancellationToken ct)
    {
        var lines = await _db.ClubPrivateEventLines.AsNoTracking().Where(l => l.PrivateEventId == e.Id).OrderBy(l => l.SortOrder).ToListAsync(ct);
        var guests = await _db.ClubPrivateEventGuests.AsNoTracking().Where(g => g.PrivateEventId == e.Id).OrderBy(g => g.Name).ToListAsync(ct);
        var spaces = await _db.ClubReservations.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId && r.PrivateEventId == e.Id && r.Status != ReservationStatuses.Cancelled)
            .OrderBy(r => r.ResourceName)
            .Select(r => new PrivateEventSpaceDto(r.Id, r.ResourceId, r.ResourceName, r.Status))
            .ToListAsync(ct);
        var company = e.CompanyId is Guid companyId
            ? await _db.Companies.AsNoTracking().Where(c => c.Id == companyId).Select(c => c.Name).FirstOrDefaultAsync(ct) : null;
        var contact = e.ContactId is Guid contactId
            ? await _db.Contacts.AsNoTracking().Where(c => c.Id == contactId).Select(c => c.Name).FirstOrDefaultAsync(ct) : null;
        string? deal = null;
        if (e.DealId is Guid dealId && _user.UserId is Guid userId && await _records.ExistsAsync(RecordTypes.Deal, dealId, userId, organizationId, ct))
            deal = await _db.Deals.AsNoTracking().Where(d => d.Id == dealId).Select(d => d.Name).FirstOrDefaultAsync(ct);
        var contractStatus = e.ContractId is Guid contractId
            ? await _db.Contracts.AsNoTracking().Where(c => c.Id == contractId).Select(c => c.Status).FirstOrDefaultAsync(ct) : null;

        return new PrivateEventDto
        {
            Id = e.Id,
            Title = e.Title,
            CompanyId = e.CompanyId,
            CompanyName = company,
            ContactId = e.ContactId,
            ContactName = contact,
            DealId = e.DealId,
            DealName = deal,
            StartAtUtc = e.StartAtUtc,
            EndAtUtc = e.EndAtUtc,
            GuestCount = e.GuestCount,
            Status = e.Status,
            Notes = e.Notes,
            Currency = e.Currency,
            Spaces = spaces,
            Lines = lines.Select(l => new PrivateEventLineDto(l.Id, l.Name, l.Quantity, l.UnitPrice, Total(l), l.VatRate, l.CatalogItemId, l.ChargeId)).ToList(),
            Guests = guests.Select(g => new PrivateEventGuestDto(g.Id, g.Name, g.Email, g.MemberId, g.Notes, g.CheckedInAtUtc, g.CheckedInByName)).ToList(),
            GuestsCheckedIn = guests.Count(g => g.CheckedInAtUtc != null),
            Money = await MoneyAsync(organizationId, e, lines, company, contact, ct),
            ContractId = e.ContractId,
            ContractStatus = contractStatus,
            ConfirmedAtUtc = e.ConfirmedAtUtc,
            CancelledAtUtc = e.CancelledAtUtc,
            CancelReason = e.CancelReason,
            CreatedAtUtc = e.CreatedAtUtc,
            Version = e.EditVersion,
        };
    }

    private async Task<PrivateEventMoneyDto> MoneyAsync(Guid organizationId, ClubPrivateEvent e, List<ClubPrivateEventLine> lines, string? company, string? contact, CancellationToken ct)
    {
        var invoiceIds = lines.Where(l => l.ChargeId != null).Select(l => l.ChargeId!.Value).Distinct().ToList();
        var chargeIds = invoiceIds.Concat(e.DepositChargeId is Guid d ? [d] : []).ToList();
        var paid = chargeIds.Count == 0 ? new Dictionary<Guid, decimal>()
            : await _db.ClubPaymentAllocations.AsNoTracking().Where(a => chargeIds.Contains(a.ChargeId))
                .GroupBy(a => a.ChargeId).Select(g => new { g.Key, Sum = g.Sum(a => a.Amount) })
                .ToDictionaryAsync(g => g.Key, g => g.Sum, ct);
        var invoiceTotal = invoiceIds.Count == 0 ? 0m
            : await _db.ClubCharges.AsNoTracking().Where(c => invoiceIds.Contains(c.Id)).SumAsync(c => c.Amount, ct);
        var depositPaid = e.DepositChargeId is Guid deposit ? paid.GetValueOrDefault(deposit) : 0m;
        var depositState = e.DepositChargeId == null ? "none"
            : e.DepositSettledAtUtc != null ? (e.InvoicedAtUtc != null ? "set_against_invoice" : "released")
            : e.Status == PrivateEventStatuses.Cancelled ? "kept"
            : depositPaid >= (e.DepositAmount ?? 0) ? "paid"
            : "charged";

        // Whose account: the one money went on, or the one it would go on now.
        string? kind = null;
        string? name = null;
        var account = e.BillToAccountId;
        if (account is Guid fixedAccount)
        {
            kind = fixedAccount == e.CompanyId ? ClubAccountHolders.Company : fixedAccount == e.ContactId ? ClubAccountHolders.Contact : ClubAccountHolders.Member;
            name = kind == ClubAccountHolders.Company ? company : kind == ClubAccountHolders.Contact ? contact
                : await _db.ClubMembers.AsNoTracking().Where(m => m.Id == fixedAccount).Select(m => m.FirstName + " " + m.LastName).FirstOrDefaultAsync(ct);
        }
        else if (e.CompanyId is Guid c)
        {
            (kind, account, name) = (ClubAccountHolders.Company, c, company);
        }
        else if (e.ContactId is Guid contactId)
        {
            var member = await _db.ClubMembers.AsNoTracking()
                .Where(m => m.OrganizationId == organizationId && m.ContactId == contactId && m.ErasedAtUtc == null)
                .Select(m => new { m.Id, Name = m.FirstName + " " + m.LastName }).FirstOrDefaultAsync(ct);
            (kind, account, name) = member != null ? (ClubAccountHolders.Member, member.Id, member.Name) : (ClubAccountHolders.Contact, contactId, contact);
        }

        return new PrivateEventMoneyDto
        {
            BillToKind = kind,
            BillToAccountId = account,
            BillToName = name?.Trim(),
            LinesTotal = lines.Sum(Total),
            DepositAmount = e.DepositAmount,
            DepositState = depositState,
            DepositChargeId = e.DepositChargeId,
            DepositPaid = depositPaid,
            Invoiced = e.InvoicedAtUtc != null,
            InvoiceChargeIds = invoiceIds,
            InvoiceTotal = invoiceTotal,
            InvoicePaid = invoiceIds.Sum(id => paid.GetValueOrDefault(id)),
        };
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<List<Guid>> HeldResourcesAsync(Guid organizationId, Guid id, CancellationToken ct) =>
        await _db.ClubReservations.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId && r.PrivateEventId == id && ReservationStatuses.Holding.Contains(r.Status))
            .Select(r => r.ResourceId).ToListAsync(ct);

    private static string HoldLabel(ClubPrivateEvent e) => PrivateEventRows.HoldLabel(e);

    private static string HoldStatus(ClubPrivateEvent e) =>
        e.Status == PrivateEventStatuses.Confirmed ? ReservationStatuses.Confirmed : ReservationStatuses.Pending;

    private static decimal Total(ClubPrivateEventLine line) => Math.Round(line.Quantity * line.UnitPrice, 2, MidpointRounding.AwayFromZero);

    private static decimal? Quantity(decimal value) =>
        value is <= 0 or > 10_000 || value != Math.Round(value, 2) ? null : value;

    private static decimal? Amount(decimal? value, bool allowZero)
    {
        if (value is not { } v || v < 0 || (!allowZero && v == 0) || v >= MaxAmount) return null;
        return Math.Round(v, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>"Saturday 14 November 2026, 18:00 to 23:00" on the club's clock.</summary>
    private static string When(ClubPrivateEvent e, TimeZoneInfo zone, CultureInfo culture)
    {
        var start = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(e.StartAtUtc, DateTimeKind.Utc), zone);
        var end = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(e.EndAtUtc, DateTimeKind.Utc), zone);
        return start.Date == end.Date
            ? $"{start.ToString("dddd d MMMM yyyy", culture)}, {start:HH:mm} to {end:HH:mm}"
            : $"{start.ToString("dddd d MMMM yyyy HH:mm", culture)} to {end.ToString("dddd d MMMM yyyy HH:mm", culture)}";
    }

    private async Task<bool> TrySaveAsync(CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.ChangeTracker.Clear();
            return false;
        }
    }

    private async Task<string> CurrencyAsync(Guid organizationId, CancellationToken ct)
    {
        var currency = await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => o.Currency).FirstOrDefaultAsync(ct);
        return RegionalSettings.TryCurrency(currency, out var code) ? code : RegionalSettings.DefaultCurrency;
    }

    private async Task<TimeZoneInfo> ZoneAsync(Guid organizationId, CancellationToken ct) =>
        RegionalSettings.ZoneOf(await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => o.TimeZone).FirstOrDefaultAsync(ct));

    private string? Who() => string.IsNullOrWhiteSpace(_user.UserName) ? null : Clip(_user.UserName.Trim(), 200);

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];

    private static DateTime? ReadDate(string? raw)
    {
        var text = (raw ?? "").Trim();
        if (text.Length == 0) return null;
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var read)
            ? DateTime.SpecifyKind(read, DateTimeKind.Utc)
            : null;
    }
}

/// <inheritdoc cref="IPrivateEventContracts"/>
/// <remarks>Its own small class, reading only the database: the contract flow calls it, and the
/// private events service calls the contract flow, so one depending on the other both ways would
/// be a loop.</remarks>
public sealed class PrivateEventContracts : IPrivateEventContracts
{
    private readonly AppDbContext _db;
    private readonly IClubReservations _spaces;

    public PrivateEventContracts(AppDbContext db, IClubReservations spaces)
    {
        _db = db;
        _spaces = spaces;
    }

    public Task<bool> IsPrivateEventContractAsync(Guid organizationId, Guid contractId, CancellationToken ct = default) =>
        _db.ClubPrivateEvents.AsNoTracking().AnyAsync(e => e.OrganizationId == organizationId && e.ContractId == contractId, ct);

    /// <remarks>Countersigned: the event is agreed and its spaces' holds confirmed with it, as staff confirming it would.</remarks>
    public async Task ContractCountersignedAsync(Guid organizationId, Guid contractId, CancellationToken ct = default)
    {
        var e = await _db.ClubPrivateEvents.FirstOrDefaultAsync(x => x.OrganizationId == organizationId && x.ContractId == contractId, ct);
        if (e is not { Status: PrivateEventStatuses.Enquiry }) return;
        var spaces = await _db.ClubReservations.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId && r.PrivateEventId == e.Id && ReservationStatuses.Holding.Contains(r.Status))
            .Select(r => r.ResourceId).ToListAsync(ct);
        var before = PrivateEventRows.Snapshot(_db, e);
        e.Status = PrivateEventStatuses.Confirmed;
        e.ConfirmedAtUtc = DateTime.UtcNow;
        e.EditVersion = Guid.NewGuid();
        e.UpdatedAtUtc = DateTime.UtcNow;
        var refused = await PrivateEventRows.SaveWithSpacesAsync(_db, e, before, spaces.Count == 0 ? null : async () =>
        {
            var held = await _spaces.HoldForPrivateEventAsync(organizationId, e.Id, PrivateEventRows.HoldLabel(e), spaces, e.StartAtUtc, e.EndAtUtc,
                ReservationStatuses.Confirmed, ct);
            return held.IsSuccess ? null : held.Error;
        }, ct);
        if (refused != null) throw new InvalidOperationException($"The private event {e.Id} could not be confirmed: {refused.Description}");
    }
}

/// <summary>Saving a private event's row together with its spaces' holds.</summary>
/// <remarks>
/// The event's row is saved first and the bookings service then moves, confirms or releases its
/// holds, in one transaction on SQL Server: a clash rolls the event back, and a second change to the
/// same event at the same moment waits on its row and is then refused by its version. The bookings
/// service clears what this context tracks as it works, so nothing of the event is left unsaved when
/// it is called (a change made after it would be lost). The in-memory provider the tests run on has
/// no transactions: there the row is put back as it was by hand.
/// </remarks>
internal static class PrivateEventRows
{
    /// <summary>The event as it stands now, to put back if its spaces are refused.</summary>
    public static ClubPrivateEvent Snapshot(AppDbContext db, ClubPrivateEvent e) => (ClubPrivateEvent)db.Entry(e).CurrentValues.ToObject();

    /// <summary>How the diary labels the event's holds.</summary>
    public static string HoldLabel(ClubPrivateEvent e)
    {
        var label = $"Private event: {e.Title}";
        return label.Length <= 200 ? label : label[..200];
    }

    /// <summary>Saves the tracked event, then asks for its spaces; null when both went through, else why not (and nothing changed).</summary>
    public static async Task<Error?> SaveWithSpacesAsync(AppDbContext db, ClubPrivateEvent e, ClubPrivateEvent before,
        Func<Task<Error?>>? spaces, CancellationToken ct)
    {
        var transaction = db.Database.IsRelational() && db.Database.CurrentTransaction == null
            ? await db.Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();
                return DomainErrors.PrivateEvent.Changed;
            }
            var refused = spaces == null ? null : await spaces();
            if (refused == null)
            {
                if (transaction != null) await transaction.CommitAsync(ct);
                return null;
            }
            db.ChangeTracker.Clear();
            if (transaction != null) await transaction.RollbackAsync(ct);
            else if (await db.ClubPrivateEvents.FirstOrDefaultAsync(x => x.Id == e.Id, ct) is { } row)
            {
                db.Entry(row).CurrentValues.SetValues(before);
                await db.SaveChangesAsync(ct);
            }
            return refused;
        }
        finally
        {
            if (transaction != null) await transaction.DisposeAsync();
        }
    }
}
