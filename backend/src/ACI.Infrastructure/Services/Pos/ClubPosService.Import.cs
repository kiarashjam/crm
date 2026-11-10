using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services.Pos;

/// <summary>
/// The import: closed tickets read from the POS, each saved once by the POS's id for it, then
/// matched to a member and posted to them through the ledger.
/// </summary>
/// <remarks>
/// <para>
/// One import at a time per workspace: it starts by claiming the settings row with one
/// conditional UPDATE (a lease of <see cref="Lease"/>), which two servers, the scheduled run
/// and someone pressing "Import now" cannot all win. A run that dies leaves the lease to run
/// out, and the next one carries on.
/// </para>
/// <para>
/// Each run reads from <see cref="Lookback"/> before the latest ticket it has seen: a till that
/// was offline sends its tickets when it is back, closed hours before. Reading the same ticket
/// twice adds nothing, as its id is unique in the workspace. It also tries again the tickets
/// still waiting for a member, so one whose member was given their code meanwhile is posted
/// without anyone touching it.
/// </para>
/// </remarks>
public sealed partial class ClubPosService
{
    /// <summary>How long a run has the workspace before another may start.</summary>
    public static readonly TimeSpan Lease = TimeSpan.FromMinutes(10);

    /// <summary>How far back before the latest ticket seen each run reads again.</summary>
    public static readonly TimeSpan Lookback = TimeSpan.FromHours(6);

    /// <summary>How often the scheduled import runs for a workspace.</summary>
    public static readonly TimeSpan Every = TimeSpan.FromMinutes(5);

    /// <summary>Waiting tickets closed within this are tried again by each run.</summary>
    private static readonly TimeSpan RetryWindow = TimeSpan.FromDays(14);

    /// <summary>Pages read in one run at most (100 tickets each): a backlog beyond it is read by the next.</summary>
    private const int MaxPages = 50;

    /// <summary>The in-memory test database has no conditional UPDATE: claims are taken one at a time instead.</summary>
    private static readonly SemaphoreSlim InMemoryClaims = new(1, 1);

    public async Task<Result<PosImportDto>> ImportNowAsync(Guid organizationId, CancellationToken ct = default)
    {
        var row = await _db.ClubPosSettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);
        if (row?.RefreshTokenProtected == null) return DomainErrors.Pos.NotConnected;
        if (row.LocationId == null) return DomainErrors.Pos.SettingsInvalid("Choose the location whose tickets to import first.");
        var now = DateTime.UtcNow;
        var until = now.Add(Lease);
        if (!await TryClaimAsync(row.Id, now, until, ct)) return DomainErrors.Pos.ImportRunning;
        return await RunClaimedAsync(organizationId, row.Id, until, ct);
    }

    public async Task<int> ImportDueAsync(DateTime utcNow, CancellationToken ct = default)
    {
        // Across workspaces: which have the import on and are due. The job has no workspace of
        // its own; each run below acts for its workspace alone.
        var dueBefore = utcNow - Every + TimeSpan.FromSeconds(30);
        var due = await _db.ClubPosSettings.IgnoreQueryFilters().AsNoTracking()
            .Where(s => s.OrganizationId != null && s.Enabled && s.RefreshTokenProtected != null && s.LocationId != null
                && (s.ClaimedUntilUtc == null || s.ClaimedUntilUtc <= utcNow)
                && (s.LastImportAtUtc == null || s.LastImportAtUtc <= dueBefore))
            .Select(s => new { s.Id, OrganizationId = s.OrganizationId!.Value })
            .ToListAsync(ct);
        var imported = 0;
        foreach (var workspace in due)
        {
            var until = utcNow.Add(Lease);
            if (!await TryClaimAsync(workspace.Id, utcNow, until, ct)) continue;
            _db.ChangeTracker.Clear();
            using var acting = _db.ActFor(workspace.OrganizationId, null);
            var result = await RunClaimedAsync(workspace.OrganizationId, workspace.Id, until, ct);
            if (result.IsSuccess) imported += result.Value.Imported;
        }
        return imported;
    }

    /// <summary>The import itself, with the claim taken; the claim is let go at the end, whatever happened.</summary>
    private async Task<Result<PosImportDto>> RunClaimedAsync(Guid organizationId, Guid settingsId, DateTime until, CancellationToken ct)
    {
        var counts = new Counts();
        string? error = null;
        DateTime? latest = null;
        try
        {
            var row = await _db.ClubPosSettings.AsNoTracking().FirstAsync(s => s.Id == settingsId, ct);
            var provider = Provider(row.Provider);
            var token = await AccessTokenAsync(row, provider, ct);
            if (!token.IsSuccess)
            {
                error = token.Error.Description;
                return token.Error;
            }

            var now = DateTime.UtcNow;
            var start = row.ImportFromUtc ?? row.ConnectedAtUtc ?? now.AddDays(-1);
            var from = row.LastClosedAtUtc is { } seen && seen - Lookback > start ? seen - Lookback : start;
            string? page = null;
            for (var pages = 0; pages < MaxPages; pages++)
            {
                var read = await provider.ClosedTicketsAsync(token.Value, row.LocationId!, from, now, page, ct);
                if (read.Failure is { } failure)
                {
                    error = failure.Message;
                    return failure.Unreachable ? DomainErrors.Pos.ProviderUnreachable(failure.Message) : DomainErrors.Pos.ProviderRefused(failure.Message);
                }
                foreach (var data in read.Value!.Tickets)
                {
                    counts.Read++;
                    if (data.ClosedAtUtc < start) continue;
                    latest = latest is { } l && l >= data.ClosedAtUtc ? l : data.ClosedAtUtc;
                    if (await SaveNewTicketAsync(organizationId, provider.Name, data, ct)) counts.Imported++;
                }
                page = read.Value.NextPageToken;
                if (page == null) break;
            }

            // What came in, and what still waits for a member: matched and posted now if it can be.
            var settings = await _db.ClubPosSettings.AsNoTracking().FirstAsync(s => s.Id == settingsId, ct);
            var window = now - RetryWindow;
            var waiting = await _db.ClubPosTickets
                .Where(t => t.OrganizationId == organizationId && t.SaleId == null
                    && (t.Attempts == 0 || ((t.Status == PosTicketStatuses.Unmatched || t.Status == PosTicketStatuses.Failed) && t.ClosedAtUtc >= window)))
                .OrderBy(t => t.ClosedAtUtc)
                .Take(1000)
                .ToListAsync(ct);
            foreach (var ticket in waiting)
            {
                var outcome = await ProcessAsync(organizationId, ticket, settings, null, null, countAttempt: ticket.Attempts == 0, ct);
                counts.Add(outcome);
            }
            return new PosImportDto(counts.Read, counts.Imported, counts.Posted, counts.Unmatched, counts.Failed, counts.Skipped, DateTime.UtcNow);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "The POS import for workspace {OrganizationId} failed", organizationId);
            error = "The import stopped part way. The next import carries on.";
            return DomainErrors.Pos.ProviderUnreachable(error);
        }
        finally
        {
            await FinishAsync(settingsId, until, latest, counts.Imported, error);
        }
    }

    /// <summary>Saves a ticket the workspace does not have yet; false when it has it (read again, or by another run).</summary>
    private async Task<bool> SaveNewTicketAsync(Guid organizationId, string provider, PosTicketData data, CancellationToken ct)
    {
        var externalId = Clip(data.ExternalId, 200);
        if (await _db.ClubPosTickets.AnyAsync(t => t.OrganizationId == organizationId && t.Provider == provider && t.ExternalId == externalId, ct))
            return false;
        var house = new List<string>();
        var ticket = new ClubPosTicket
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Provider = provider,
            ExternalId = externalId,
            ReceiptNumber = data.ReceiptNumber is { } receipt ? Clip(receipt, 100) : null,
            LocationId = data.LocationId is { } location ? Clip(location, 100) : null,
            ClosedAtUtc = data.ClosedAtUtc,
            Currency = Clip((data.Currency ?? await WorkspaceCurrencyAsync(organizationId, ct)).ToUpperInvariant(), 3),
            Total = data.Lines.Sum(l => l.Total),
            StaffName = data.StaffName is { } staff ? Clip(staff, 200) : null,
            TableName = data.TableName is { } table ? Clip(table, 100) : null,
            CustomerName = data.CustomerName is { } name ? Clip(name, 200) : null,
            CustomerId = data.CustomerId is { } customer ? Clip(customer, 100) : null,
            CustomerReference = Reference(data),
            DataJson = JsonSerializer.Serialize(data, Json),
            Status = PosTicketStatuses.Unmatched,
            ImportedAtUtc = DateTime.UtcNow,
        };
        _db.ClubPosTickets.Add(ticket);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            _db.Entry(ticket).State = EntityState.Detached;
            return false;
        }
    }

    /// <summary>Claims the workspace's import: one conditional UPDATE, so of two runs exactly one sees a row change.</summary>
    private async Task<bool> TryClaimAsync(Guid settingsId, DateTime utcNow, DateTime until, CancellationToken ct)
    {
        var claimable = _db.ClubPosSettings.IgnoreQueryFilters()
            .Where(s => s.Id == settingsId && (s.ClaimedUntilUtc == null || s.ClaimedUntilUtc <= utcNow));
        if (_db.Database.IsRelational())
            return await claimable.ExecuteUpdateAsync(set => set.SetProperty(s => s.ClaimedUntilUtc, until), ct) == 1;

        await InMemoryClaims.WaitAsync(ct);
        try
        {
            var row = await claimable.FirstOrDefaultAsync(ct);
            if (row == null) return false;
            row.ClaimedUntilUtc = until;
            await _db.SaveChangesAsync(ct);
            _db.Entry(row).State = EntityState.Detached;
            return true;
        }
        finally
        {
            InMemoryClaims.Release();
        }
    }

    /// <summary>Lets go of the claim and records how the run went, unless another run has the workspace by now.</summary>
    private async Task FinishAsync(Guid settingsId, DateTime until, DateTime? latest, int imported, string? error)
    {
        var now = DateTime.UtcNow;
        var clipped = error != null ? Clip(error, 500) : null;
        try
        {
            var query = _db.ClubPosSettings.IgnoreQueryFilters().Where(s => s.Id == settingsId && s.ClaimedUntilUtc == until);
            if (_db.Database.IsRelational())
            {
                if (latest is { } seen)
                    await query.ExecuteUpdateAsync(set => set
                        .SetProperty(s => s.ClaimedUntilUtc, (DateTime?)null)
                        .SetProperty(s => s.LastImportAtUtc, now)
                        .SetProperty(s => s.LastImportError, clipped)
                        .SetProperty(s => s.LastImportCount, imported)
                        .SetProperty(s => s.LastClosedAtUtc, s => s.LastClosedAtUtc == null || s.LastClosedAtUtc < seen ? (DateTime?)seen : s.LastClosedAtUtc));
                else
                    await query.ExecuteUpdateAsync(set => set
                        .SetProperty(s => s.ClaimedUntilUtc, (DateTime?)null)
                        .SetProperty(s => s.LastImportAtUtc, now)
                        .SetProperty(s => s.LastImportError, clipped)
                        .SetProperty(s => s.LastImportCount, imported));
                return;
            }
            var row = await query.FirstOrDefaultAsync();
            if (row == null) return;
            row.ClaimedUntilUtc = null;
            row.LastImportAtUtc = now;
            row.LastImportError = clipped;
            row.LastImportCount = imported;
            if (latest != null && (row.LastClosedAtUtc == null || row.LastClosedAtUtc < latest)) row.LastClosedAtUtc = latest;
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // The claim runs out by itself; the next run carries on from the tickets saved.
            _logger.LogError(ex, "The POS import could not record how it went");
        }
    }

    // ── matching and posting one ticket ──────────────────────────────────────

    /// <summary>What came of a ticket; Changed when someone else dealt with it at the same moment.</summary>
    private enum Outcome { Posted, Unmatched, Failed, Skipped, Changed }

    private sealed class Counts
    {
        public int Read;
        public int Imported;
        public int Posted;
        public int Unmatched;
        public int Failed;
        public int Skipped;

        public void Add(Outcome outcome)
        {
            switch (outcome)
            {
                case Outcome.Posted: Posted++; break;
                case Outcome.Unmatched: Unmatched++; break;
                case Outcome.Failed: Failed++; break;
                case Outcome.Skipped: Skipped++; break;
            }
        }
    }

    /// <summary>
    /// Works out whose the ticket is and posts it to them as a sale: owing for what was put on
    /// the house account, paid for what was paid at the POS, with the points. A ticket that needs
    /// a member and has none waits for staff; one paid with nobody on it is not a member's.
    /// </summary>
    /// <param name="member">Who staff said it is, when they matched it.</param>
    private async Task<Outcome> ProcessAsync(Guid organizationId, ClubPosTicket ticket, ClubPosSettings settings, ClubMember? member,
        string? matchedBy, bool countAttempt, CancellationToken ct)
    {
        var data = ReadData(ticket);
        if (countAttempt) ticket.Attempts++;
        ticket.UpdatedAtUtc = DateTime.UtcNow;
        if (data == null) return await SettleAsync(ticket, PosTicketStatuses.Failed, "The ticket as read from the POS could not be read again.", ct);

        var map = MapOf(settings.MethodMapJson);
        var payments = data.Payments.Select(p => (Payment: p, Means: MeansOf(p, map))).ToList();
        ticket.OnAccountAmount = payments.Where(p => p.Means == SaleTenders.HouseAccount).Sum(p => p.Payment.Amount + p.Payment.Tip);
        ticket.PaidAmount = payments.Where(p => p.Means is not (null or SaleTenders.HouseAccount or Skip)).Sum(p => p.Payment.Amount);
        ticket.TipAmount = payments.Sum(p => p.Payment.Tip);
        ticket.TendersText = Clip(string.Join(", ", payments.Select(p => $"{p.Payment.Name ?? p.Payment.Code ?? "Payment"} {p.Payment.Amount:0.00}")), 500);
        if (ticket.TendersText.Length == 0) ticket.TendersText = null;

        if (data.Cancelled) return await SettleAsync(ticket, PosTicketStatuses.Skipped, "Cancelled at the POS.", ct);
        if (payments.Where(p => p.Means == Skip).Select(p => p.Payment).FirstOrDefault() is { } skipped)
            return await SettleAsync(ticket, PosTicketStatuses.Skipped, $"Paid by {skipped.Name ?? skipped.Code}, which the POS settings say is not a member's sale.", ct);

        var hasHouse = payments.Any(p => p.Means == SaleTenders.HouseAccount);
        if (member == null)
        {
            (member, matchedBy) = await LinkAsync(organizationId, ticket.Provider, data, ct);
            if (member == null && ticket.MemberId is { } earlier && ticket.MatchedBy == "staff")
                (member, matchedBy) = (await _db.ClubMembers.FirstOrDefaultAsync(m => m.Id == earlier && m.OrganizationId == organizationId, ct), "staff");
        }
        if (member == null)
        {
            if (hasHouse) return await SettleAsync(ticket, PosTicketStatuses.Unmatched, "Put on the house account, and no member's code is on it. Say whose it is.", ct);
            if (HasCustomer(data))
                return await SettleAsync(ticket, PosTicketStatuses.Unmatched, "The POS put a customer on it that no member's code names. Say whose it is, or set it aside.", ct);
            return await SettleAsync(ticket, PosTicketStatuses.Skipped, "Paid at the POS with no member on it.", ct);
        }
        ticket.MemberId = member.Id;
        ticket.MemberName = Clip($"{member.FirstName} {member.LastName}".Trim(), 200);
        ticket.MatchedBy = matchedBy;

        if (payments.Where(p => p.Means == null).Select(p => p.Payment).FirstOrDefault() is { } unknown)
            return await SettleAsync(ticket, PosTicketStatuses.Failed,
                $"Paid by '{unknown.Name ?? unknown.Code}', which is not known as the house account, card, cash or TWINT. Say what it is in the POS settings, then try again.", ct);

        var built = BuildSale(ticket, data, settings, member, payments.Select(p => (p.Payment, p.Means!)).ToList());
        if (built.Problem != null) return await SettleAsync(ticket, built.Problem.Value.Status, built.Problem.Value.Note, ct);

        // What was decided so far is saved before the ledger posts: the ledger lets go of
        // whatever else is waiting to be saved when it refuses a sale, and the ticket's state
        // (and a customer staff just matched) must not go with it. Saving it first is also the
        // claim: of two people dealing with the ticket at once, the second is refused here.
        if (!await SaveTicketAsync(ticket, ct)) return Outcome.Changed;
        var sale = await _ledger.PostSaleAsync(organizationId, built.Request!, null, ct);
        if (!sale.IsSuccess) return await SettleAsync(ticket, PosTicketStatuses.Failed, sale.Error.Description, ct);
        ticket.SaleId = sale.Value.Id;
        ticket.PointsAwarded = sale.Value.PointsAwarded;
        ticket.PostedAtUtc ??= DateTime.UtcNow;
        var words = sale.Value.OwedAmount > 0 && sale.Value.PaidAmount > 0 ? "Partly on the house account, partly paid at the POS."
            : sale.Value.OwedAmount > 0 ? "On the house account." : "Recorded as paid at the POS.";
        return await SettleAsync(ticket, PosTicketStatuses.Posted, words, ct);
    }

    /// <summary>Saves where the ticket stands; a ticket someone else dealt with meanwhile is left as they left it.</summary>
    private async Task<Outcome> SettleAsync(ClubPosTicket ticket, string status, string note, CancellationToken ct)
    {
        ticket.Status = status;
        ticket.Note = Clip(note, 500);
        if (!await SaveTicketAsync(ticket, ct)) return Outcome.Changed;
        return status switch
        {
            PosTicketStatuses.Posted => Outcome.Posted,
            PosTicketStatuses.Unmatched => Outcome.Unmatched,
            PosTicketStatuses.Failed => Outcome.Failed,
            _ => Outcome.Skipped,
        };
    }

    /// <summary>
    /// Saves the ticket and whatever waits with it; false, with nothing of it kept, when someone
    /// else changed the ticket meanwhile (or took the customer staff were matching).
    /// </summary>
    private async Task<bool> SaveTicketAsync(ClubPosTicket ticket, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            foreach (var entry in _db.ChangeTracker.Entries()
                         .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList())
                entry.State = EntityState.Detached;
            _db.Entry(ticket).State = EntityState.Detached;
            return false;
        }
    }

    /// <summary>
    /// The member the ticket names: by a code of theirs the POS recorded (a member number, the
    /// code on their card), by the POS's customer when staff matched one of that customer's
    /// tickets to them, or by the customer's email when exactly one member has it.
    /// </summary>
    private async Task<(ClubMember? Member, string? How)> LinkAsync(Guid organizationId, string provider, PosTicketData data, CancellationToken ct)
    {
        var codes = data.CustomerCodes.Select(NormalizeCode).Where(c => c.Length > 0).Distinct().ToList();
        if (codes.Count > 0)
        {
            var owners = await _db.ClubMemberCodes.AsNoTracking()
                .Where(c => c.OrganizationId == organizationId && c.Kind == MemberCodeKinds.Card && codes.Contains(c.Code))
                .Select(c => c.MemberId).Distinct().ToListAsync(ct);
            if (owners.Count == 1 && await MemberAsync(organizationId, owners[0], ct) is { } byCode) return (byCode, "code");
        }
        if (data.CustomerId is { } customerId)
        {
            var key = CustomerCode(provider, customerId);
            var owner = await _db.ClubMemberCodes.AsNoTracking()
                .Where(c => c.OrganizationId == organizationId && c.Kind == MemberCodeKinds.PosCustomer && c.Code == key)
                .Select(c => (Guid?)c.MemberId).FirstOrDefaultAsync(ct);
            if (owner is { } id && await MemberAsync(organizationId, id, ct) is { } byCustomer) return (byCustomer, "customer");
        }
        if (data.CustomerEmail is { } email && email.Contains('@'))
        {
            var wanted = email.Trim().ToLowerInvariant();
            var matches = await _db.ClubMembers.Where(m => m.OrganizationId == organizationId && m.Email.ToLower() == wanted).Take(2).ToListAsync(ct);
            if (matches.Count == 1) return (matches[0], "email");
        }
        return (null, null);
    }

    private sealed record Built(SaleRequest? Request, (string Status, string Note)? Problem);

    /// <summary>
    /// The sale to post for a ticket: its lines as the POS has them (the same item and rate
    /// together, a line taken back set against it), what was put on the house account and what
    /// was paid at the POS, a tip on the house account owed with it, and a few cents of cash
    /// rounding left with the POS.
    /// </summary>
    private static Built BuildSale(ClubPosTicket ticket, PosTicketData data, ClubPosSettings settings, ClubMember member,
        List<(PosTicketPayment Payment, string Means)> payments)
    {
        var grouped = data.Lines
            .GroupBy(l => (Name: l.Name.Trim(), l.VatRate))
            .Select(g => new
            {
                g.Key.Name,
                g.Key.VatRate,
                Quantity = g.Sum(l => l.Quantity),
                Total = g.Sum(l => l.Total),
                Discount = g.Sum(l => l.Discount),
                Sku = g.Select(l => l.Sku).FirstOrDefault(s => s != null),
                DiscountName = g.Select(l => l.DiscountName).FirstOrDefault(d => d != null),
            })
            .Where(l => l.Total != 0)
            .ToList();
        var total = grouped.Sum(l => l.Total);
        if (total < 0)
            return new Built(null, (PosTicketStatuses.Failed,
                "A refund at the POS. Refunds are not posted on their own: void the charge it takes back, or record a credit, on the house account page."));
        if (total == 0) return new Built(null, (PosTicketStatuses.Skipped, "Nothing to post: the ticket comes to nothing."));

        // Lines taken back (a negative line left after the same item's) come off the others as a discount.
        var takenBack = -grouped.Where(l => l.Total < 0).Sum(l => l.Total);
        var lines = new List<SaleLineRequest>();
        foreach (var line in grouped.Where(l => l.Total > 0))
        {
            var discount = Math.Max(0, line.Discount);
            var gross = line.Total + discount;
            var quantity = line.Quantity > 0 && line.Quantity == Math.Round(line.Quantity, 2) && line.Quantity <= 1000 ? line.Quantity : 1m;
            var unit = Math.Round(gross / quantity, 2, MidpointRounding.AwayFromZero);
            var name = line.Name.Length > 0 ? line.Name : "Item";
            if (Math.Round(unit * quantity, 2, MidpointRounding.AwayFromZero) != gross || unit > 100_000)
            {
                // A price the quantity does not divide to the cent: the line as one, its quantity in its name.
                name = quantity != 1 ? $"{quantity:0.##} × {name}" : name;
                quantity = 1m;
                unit = gross;
            }
            lines.Add(new SaleLineRequest
            {
                Name = Clip(name, 200), Sku = line.Sku, Quantity = quantity, UnitPrice = unit, VatRate = line.VatRate,
                Discount = discount > 0 ? discount : null,
            });
        }
        var houseTips = payments.Where(p => p.Means == SaleTenders.HouseAccount).Sum(p => p.Payment.Tip);
        if (houseTips > 0)
            lines.Add(new SaleLineRequest { Name = "Tip", Quantity = 1, UnitPrice = houseTips, Kind = "Other", EarnsPoints = false });

        // How it was settled, each way netted (change given back is a negative cash payment); a tip
        // paid at the POS is the staff's, not the member's spend.
        var tenders = payments
            .GroupBy(p => p.Means)
            .Select(g => new SaleTenderRequest
            {
                Method = g.Key,
                Amount = g.Sum(p => p.Payment.Amount) + (g.Key == SaleTenders.HouseAccount ? houseTips : 0),
            })
            .Where(t => t.Amount > 0)
            .ToList();
        // The lines taken back are already in the total; the ledger takes them off as the discount.
        var due = total + houseTips;
        var tendered = tenders.Sum(t => t.Amount ?? 0);
        var gap = due - tendered;
        if (tenders.Count == 0)
            return new Built(null, (PosTicketStatuses.Failed, "The ticket has no payments, so it is not known whether it was put on the house account or paid."));
        if (Math.Abs(gap) > 0.05m)
            return new Built(null, (PosTicketStatuses.Failed,
                $"The ticket's lines come to {due:0.00} and its payments to {tendered:0.00}, so it is not known what the member owes."));
        // A few cents of cash rounding (Switzerland rounds cash to five centimes): the payments
        // are made to match the lines, so the VAT on each line stays as the POS charged it.
        if (gap != 0)
        {
            var largest = tenders.OrderByDescending(t => t.Amount).First();
            largest.Amount += gap;
        }

        var reasons = grouped.Where(l => l.Discount > 0).Select(l => l.DiscountName).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().ToList();
        string? reason = null;
        if (lines.Any(l => l.Discount > 0) || takenBack > 0)
            reason = reasons.Count > 0 ? $"Given at the POS: {string.Join(", ", reasons)}" : "Given at the POS";
        return new Built(new SaleRequest
        {
            MemberId = member.Id,
            Venue = settings.DefaultVenue,
            Lines = lines,
            DiscountAmount = takenBack > 0 ? takenBack : null,
            DiscountReason = reason,
            Tenders = tenders,
            AllowLinesOutsideCatalog = true,
            Source = ticket.Provider,
            PosTicketId = ticket.Id,
            SoldAtUtc = ticket.ClosedAtUtc,
            Currency = ticket.Currency,
            Reference = ticket.ReceiptNumber,
            StaffName = ticket.StaffName,
            TableName = ticket.TableName,
            RecordedBy = "Lightspeed",
        }, null);
    }

    /// <summary>
    /// What a POS payment method means: as the settings map its code or name, else read from its
    /// name (card brands, cash, TWINT, an account); null when it can't be told.
    /// </summary>
    internal static string? MeansOf(PosTicketPayment payment, IReadOnlyDictionary<string, string> map)
    {
        foreach (var key in new[] { payment.Code, payment.Name })
            if (!string.IsNullOrWhiteSpace(key) && map.TryGetValue(key.Trim(), out var mapped)) return mapped;
        var words = $" {payment.Code} {payment.Name} ".ToLowerInvariant().Replace('-', ' ').Replace('_', ' ');
        bool Has(params string[] parts) => parts.Any(p => words.Contains(p, StringComparison.Ordinal));
        if (Has("twint")) return SaleTenders.Twint;
        if (Has(" house", "account", "konto", "compte", " conto", "member", "mitglied", "membre", "on room")) return SaleTenders.HouseAccount;
        if (Has(" cash", "bargeld", " bar ", "espèces", "especes", "contanti")) return SaleTenders.Cash;
        if (Has("card", "karte", "carte", "visa", "master", "maestro", "amex", "american express", "debit", "credit", "postfinance", "v pay", "vpay",
                "apple pay", "google pay", "samsung pay", "worldline", "terminal", "eft")) return SaleTenders.Card;
        return null;
    }

    private static bool HasCustomer(PosTicketData data) =>
        data.CustomerId != null || data.CustomerEmail != null || data.CustomerCodes.Count > 0;

    private static string? Reference(PosTicketData data)
    {
        var parts = data.CustomerCodes.Concat(data.CustomerEmail != null ? [data.CustomerEmail] : []).ToList();
        return parts.Count > 0 ? Clip(string.Join(", ", parts), 500) : null;
    }

    private static PosTicketData? ReadData(ClubPosTicket ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket.DataJson)) return null;
        try
        {
            return JsonSerializer.Deserialize<PosTicketData>(ticket.DataJson, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<ClubMember?> MemberAsync(Guid organizationId, Guid memberId, CancellationToken ct) =>
        await _db.ClubMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == organizationId, ct);

    private async Task<string> WorkspaceCurrencyAsync(Guid organizationId, CancellationToken ct)
    {
        var currency = await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => o.Currency).FirstOrDefaultAsync(ct);
        return RegionalSettings.TryCurrency(currency, out var code) ? code : RegionalSettings.DefaultCurrency;
    }
}
