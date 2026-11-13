using System.Globalization;
using ACI.Application.Common;
using ACI.Application.Common.I18n;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ACI.Infrastructure.Services;

/// <summary>Corporate memberships and their nominees (see <see cref="ICorporateMemberships"/> for the rules).</summary>
/// <remarks>
/// <para>
/// A change to the nominees takes the membership's row first (its NomineesChangedAtUtc moves, and
/// AppDbContext stamps a new Version), and the seats are counted after that read: of two additions
/// for the last seat at the same moment, the second is refused on saving, counted again on what the
/// first saved, and answered "all seats are taken". On SQL Server the change is one transaction and a
/// filtered unique index keeps a member to one seat; the in-memory provider the HTTP tests run on has
/// neither, and the order of the saves (the membership's row first) keeps a lost race from writing.
/// </para>
/// <para>
/// Money goes through the ledger only: the dues are a subscription on the company's account, and what
/// nominees run up is put on that account by the ledger itself when the membership says the company
/// pays (ClubLedgerService.BillToAsync), so nothing here posts a charge.
/// </para>
/// </remarks>
public sealed class CorporateMembershipService : ICorporateMemberships
{
    private const int MaxSeats = 500;
    private const int Attempts = 4;
    private const int DueWithinDays = 60;
    private static readonly string[] SeatHolders = ["Active", "Pending"];

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IClubLedger _ledger;
    private readonly ILedgerReadObserver _reads;

    public CorporateMembershipService(AppDbContext db, ICurrentUserService user, IClubLedger ledger, ILedgerReadObserver reads)
    {
        _db = db;
        _user = user;
        _ledger = ledger;
        _reads = reads;
    }

    // ── reads ────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<CorporateMembershipDto>> ListAsync(Guid organizationId, CorporateMembershipQuery query, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var rows = _db.ClubCorporateMemberships.AsNoTracking().Where(m => m.OrganizationId == organizationId);
        if (query.CompanyId is Guid company) rows = rows.Where(m => m.CompanyId == company);
        if (!string.IsNullOrWhiteSpace(query.Status)) rows = rows.Where(m => m.Status == query.Status.Trim());
        if (query.RenewingWithinDays is int days)
        {
            var until = now.AddDays(Math.Clamp(days, 0, 3660));
            rows = rows.Where(m => m.Status == CorporateMembershipStatuses.Active && m.RenewsAtUtc <= until);
        }
        var list = await rows.OrderBy(m => m.Status).ThenBy(m => m.RenewsAtUtc).ThenBy(m => m.Name).ToListAsync(ct);
        var answers = new List<CorporateMembershipDto>(list.Count);
        foreach (var membership in list) answers.Add(await ToDtoAsync(organizationId, membership, now, ct, withAccount: false));
        return answers;
    }

    public async Task<Result<CorporateMembershipDto>> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default)
    {
        var membership = await _db.ClubCorporateMemberships.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id && m.OrganizationId == organizationId, ct);
        if (membership == null) return DomainErrors.Corporate.NotFound;
        return await ToDtoAsync(organizationId, membership, DateTime.UtcNow, ct, withAccount: true);
    }

    public async Task<MemberCorporateSeatDto?> SeatOfMemberAsync(Guid organizationId, Guid memberId, CancellationToken ct = default) =>
        await _db.ClubCorporateNominees.AsNoTracking()
            .Where(n => n.OrganizationId == organizationId && n.MemberId == memberId && n.RemovedAtUtc == null)
            .Join(_db.ClubCorporateMemberships.AsNoTracking(), n => n.CorporateMembershipId, m => m.Id,
                (n, m) => new MemberCorporateSeatDto(m.Id, m.Name, m.CompanyId, m.CompanyName, m.NomineeCharges, m.RenewsAtUtc, n.Id))
            .FirstOrDefaultAsync(ct);

    // ── the membership ───────────────────────────────────────────────────────

    public async Task<Result<CorporateMembershipDto>> CreateAsync(Guid organizationId, CorporateMembershipWriteRequest request, bool mayHandleMoney, CancellationToken ct = default)
    {
        if (request.Dues != null && !mayHandleMoney)
            return DomainErrors.Permission.Missing(Domain.Common.MemberPermissions.ManageMoney, "set a corporate membership's dues");
        if (request.CompanyId is not Guid companyId) return DomainErrors.Corporate.CompanyNotFound;
        var companyName = await _db.Companies.AsNoTracking().Where(c => c.Id == companyId && c.OrganizationId == organizationId)
            .Select(c => c.Name).FirstOrDefaultAsync(ct);
        if (companyName == null) return DomainErrors.Corporate.CompanyNotFound;

        var now = DateTime.UtcNow;
        var membership = new ClubCorporateMembership
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CompanyId = companyId,
            CompanyName = Clip(companyName.Trim(), 200),
            Name = Clip($"{companyName.Trim()} corporate", 200),
            Seats = 1,
            StartsAtUtc = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc),
            CreatedAtUtc = now,
            CreatedByUserId = _user.UserId,
            CreatedByName = Who(),
            UpdatedAtUtc = now,
            EditVersion = Guid.NewGuid(),
        };
        membership.RenewsAtUtc = membership.StartsAtUtc.AddMonths(membership.TermMonths);
        if (await ApplyAsync(organizationId, membership, request, creating: true, ct) is { } invalid) return invalid;

        // The company's account first: what its nominees run up goes there from the first day, and
        // its dues need it. Opening it posts nothing.
        var account = await _ledger.OpenClientAccountAsync(organizationId, ClubAccountHolders.Company, companyId, ct);
        if (!account.IsSuccess) return account.Error;
        _db.ClubCorporateMemberships.Add(membership);
        await _db.SaveChangesAsync(ct);

        if (request.Dues != null)
        {
            var dues = await StartOrChangeDuesAsync(organizationId, membership.Id, request.Dues, ct);
            if (!dues.IsSuccess)
            {
                // Dues refused (a price not in the catalogue, an amount that is not one): the
                // membership is not left behind without them, as if it had been made.
                _db.ChangeTracker.Clear();
                if (await _db.ClubCorporateMemberships.FirstOrDefaultAsync(m => m.Id == membership.Id, ct) is { } made)
                {
                    _db.ClubCorporateMemberships.Remove(made);
                    await _db.SaveChangesAsync(ct);
                }
                return dues.Error;
            }
        }
        return await GetAsync(organizationId, membership.Id, ct);
    }

    public async Task<Result<CorporateMembershipDto>> UpdateAsync(Guid organizationId, Guid id, CorporateMembershipWriteRequest request, Guid? expectedVersion, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            var membership = await _db.ClubCorporateMemberships.FirstOrDefaultAsync(m => m.Id == id && m.OrganizationId == organizationId, ct);
            if (membership == null) return DomainErrors.Corporate.NotFound;
            // The version the details were read at, not the row's: a nominee change moves the row's.
            if ((expectedVersion ?? request.Version) is Guid version && membership.EditVersion != version) return DomainErrors.Corporate.Changed;
            if (membership.Status == CorporateMembershipStatuses.Ended) return DomainErrors.Corporate.Ended;
            if (await ApplyAsync(organizationId, membership, request, creating: false, ct) is { } invalid)
            {
                _db.ChangeTracker.Clear();
                return invalid;
            }
            // Fewer seats than nominees would leave someone holding a seat that is not there.
            var seated = await _db.ClubCorporateNominees.CountAsync(n => n.CorporateMembershipId == id && n.RemovedAtUtc == null, ct);
            if (membership.Seats < seated)
            {
                _db.ChangeTracker.Clear();
                return DomainErrors.Corporate.SeatsBelowNominees(seated);
            }
            membership.EditVersion = Guid.NewGuid();
            membership.UpdatedAtUtc = DateTime.UtcNow;
            try
            {
                await _db.SaveChangesAsync(ct);
                return await GetAsync(organizationId, id, ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // A nominee change took the row meanwhile: the seats are counted again on what it left.
                _db.ChangeTracker.Clear();
                if (attempt >= Attempts) return DomainErrors.Corporate.Changed;
            }
        }
    }

    public async Task<Result<CorporateMembershipDto>> SetDuesAsync(Guid organizationId, Guid id, CorporateDuesRequest request, CancellationToken ct = default)
    {
        var dues = await StartOrChangeDuesAsync(organizationId, id, request, ct);
        return dues.IsSuccess ? await GetAsync(organizationId, id, ct) : dues.Error;
    }

    /// <summary>
    /// The dues as a subscription on the company's account: started the first time, changed after
    /// (a new amount or cadence applies from the next period, as for any subscription).
    /// </summary>
    private async Task<Result> StartOrChangeDuesAsync(Guid organizationId, Guid id, CorporateDuesRequest request, CancellationToken ct)
    {
        var membership = await _db.ClubCorporateMemberships.FirstOrDefaultAsync(m => m.Id == id && m.OrganizationId == organizationId, ct);
        if (membership == null) return DomainErrors.Corporate.NotFound;
        if (membership.Status == CorporateMembershipStatuses.Ended) return DomainErrors.Corporate.Ended;
        var first = (request.FirstBillingAtUtc ?? "").Trim();

        if (membership.DuesSubscriptionId is Guid running
            && await _db.ClubSubscriptions.AsNoTracking().AnyAsync(s => s.Id == running && s.Status != "Cancelled", ct))
        {
            var changed = await _ledger.UpdateSubscriptionAsync(organizationId, running, new SubscriptionWriteRequest
            {
                Amount = request.Amount,
                Cadence = request.Cadence,
                NextChargeAtUtc = first.Length == 0 ? null : first,
            }, null, ct);
            return changed.IsSuccess ? Result.Success() : changed.Error;
        }

        var account = await _ledger.OpenClientAccountAsync(organizationId, ClubAccountHolders.Company, membership.CompanyId, ct);
        if (!account.IsSuccess) return account.Error;
        var started = await _ledger.CreateSubscriptionAsync(organizationId, new SubscriptionWriteRequest
        {
            MemberId = membership.CompanyId,
            Name = Clip($"{membership.Name} dues", 200),
            Amount = request.Amount,
            // A corporate membership is usually billed once a year; the catalogue item says otherwise when it does.
            Cadence = string.IsNullOrWhiteSpace(request.Cadence) && request.CatalogItemId == null ? "Annual" : request.Cadence,
            CatalogItemId = request.CatalogItemId,
            NextChargeAtUtc = first.Length > 0 ? first : membership.StartsAtUtc.ToString("O", CultureInfo.InvariantCulture),
            Notes = $"Dues of the corporate membership {membership.Name}",
        }, ct);
        if (!started.IsSuccess) return started.Error;
        membership.DuesSubscriptionId = started.Value.Id;
        membership.UpdatedAtUtc = DateTime.UtcNow;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A nominee change saved the row meanwhile: note the subscription on what it left.
            _db.ChangeTracker.Clear();
            var again = await _db.ClubCorporateMemberships.FirstAsync(m => m.Id == id, ct);
            again.DuesSubscriptionId = started.Value.Id;
            await _db.SaveChangesAsync(ct);
        }
        return Result.Success();
    }

    public async Task<Result<CorporateMembershipDto>> RenewAsync(Guid organizationId, Guid id, Guid? expectedVersion, CancellationToken ct = default) =>
        await ChangeAsync(organizationId, id, expectedVersion, membership =>
        {
            // From the date it was due, so a renewal pressed late does not shorten the next term.
            membership.RenewsAtUtc = membership.RenewsAtUtc.AddMonths(membership.TermMonths);
            membership.NotRenewing = false;
            membership.NotRenewingReason = null;
            membership.LastRenewedAtUtc = DateTime.UtcNow;
            return null;
        }, ct);

    public async Task<Result<CorporateMembershipDto>> MarkNotRenewingAsync(Guid organizationId, Guid id, string? reason, Guid? expectedVersion, CancellationToken ct = default)
    {
        var why = (reason ?? "").Trim();
        if (why.Length is 0 or > 500) return DomainErrors.Corporate.ReasonRequired;
        return await ChangeAsync(organizationId, id, expectedVersion, membership =>
        {
            membership.NotRenewing = true;
            membership.NotRenewingReason = why;
            return null;
        }, ct);
    }

    public async Task<Result<CorporateMembershipDto>> EndAsync(Guid organizationId, Guid id, string? reason, bool mayHandleMoney, CancellationToken ct = default)
    {
        var why = (reason ?? "").Trim();
        if (why.Length is 0 or > 500) return DomainErrors.Corporate.ReasonRequired;
        var membership = await _db.ClubCorporateMemberships.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id && m.OrganizationId == organizationId, ct);
        if (membership == null) return DomainErrors.Corporate.NotFound;
        if (membership.Status == CorporateMembershipStatuses.Ended) return DomainErrors.Corporate.Ended;
        var duesRunning = membership.DuesSubscriptionId is Guid subscriptionId
            && await _db.ClubSubscriptions.AsNoTracking().AnyAsync(s => s.Id == subscriptionId && s.Status != "Cancelled", ct);
        if (duesRunning && !mayHandleMoney)
            return DomainErrors.Permission.Missing(Domain.Common.MemberPermissions.ManageMoney, "end a corporate membership whose dues are still running (stopping them handles money)");

        // The dues first: an ended membership left billing the company is worse than one that
        // stays active a moment longer because its end was refused.
        if (duesRunning)
        {
            var cancelled = await _ledger.UpdateSubscriptionAsync(organizationId, membership.DuesSubscriptionId!.Value,
                new SubscriptionWriteRequest { Status = "Cancelled" }, null, ct);
            if (!cancelled.IsSuccess) return cancelled.Error;
        }
        return await SeatChangeAsync(organizationId, id, async (tracked, seated, now) =>
        {
            foreach (var nominee in seated)
                Remove(nominee, now, "The corporate membership ended", null);
            tracked.Status = CorporateMembershipStatuses.Ended;
            tracked.EndedAtUtc = now;
            tracked.EndedReason = why;
            tracked.EndedByName = Who();
            tracked.EditVersion = Guid.NewGuid();
            await Task.CompletedTask;
            return null;
        }, allowEnded: false, ct);
    }

    // ── nominees ─────────────────────────────────────────────────────────────

    public async Task<Result<CorporateMembershipDto>> AddNomineeAsync(Guid organizationId, Guid id, NomineeRequest request, CancellationToken ct = default)
    {
        if (request.MemberId is not Guid memberId) return DomainErrors.Corporate.MemberNotFound;
        return await SeatChangeAsync(organizationId, id, async (membership, seated, now) =>
        {
            if (seated.Any(n => n.MemberId == memberId)) return DomainErrors.Corporate.AlreadySeated(membership.Name);
            if (seated.Count >= membership.Seats) return DomainErrors.Corporate.SeatsFull(membership.Seats);
            var seat = await NewSeatAsync(organizationId, membership, memberId, now, ct);
            if (!seat.IsSuccess) return seat.Error;
            _db.ClubCorporateNominees.Add(seat.Value);
            return null;
        }, allowEnded: false, ct);
    }

    public async Task<Result<CorporateMembershipDto>> SwapNomineeAsync(Guid organizationId, Guid id, Guid nomineeId, NomineeRequest request, CancellationToken ct = default)
    {
        if (request.MemberId is not Guid memberId) return DomainErrors.Corporate.MemberNotFound;
        var reason = (request.Reason ?? "").Trim();
        if (reason.Length > 500) return DomainErrors.Corporate.TooLong("A reason", 500);
        return await SeatChangeAsync(organizationId, id, async (membership, seated, now) =>
        {
            var leaving = seated.FirstOrDefault(n => n.Id == nomineeId);
            if (leaving == null) return DomainErrors.Corporate.NomineeNotFound;
            if (seated.Any(n => n.MemberId == memberId)) return DomainErrors.Corporate.AlreadySeated(membership.Name);
            var seat = await NewSeatAsync(organizationId, membership, memberId, now, ct);
            if (!seat.IsSuccess) return seat.Error;
            Remove(leaving, now, reason.Length == 0 ? $"Seat passed to {seat.Value.MemberName}" : reason, memberId);
            _db.ClubCorporateNominees.Add(seat.Value);
            return null;
        }, allowEnded: false, ct);
    }

    public async Task<Result<CorporateMembershipDto>> RemoveNomineeAsync(Guid organizationId, Guid id, Guid nomineeId, string? reason, CancellationToken ct = default)
    {
        var why = (reason ?? "").Trim();
        if (why.Length > 500) return DomainErrors.Corporate.TooLong("A reason", 500);
        return await SeatChangeAsync(organizationId, id, (membership, seated, now) =>
        {
            var leaving = seated.FirstOrDefault(n => n.Id == nomineeId);
            if (leaving == null) return Task.FromResult<Error?>(DomainErrors.Corporate.NomineeNotFound);
            Remove(leaving, now, why.Length == 0 ? null : why, null);
            return Task.FromResult<Error?>(null);
        }, allowEnded: false, ct);
    }

    /// <summary>A seat for the member, once they are found to be a member here who can hold one, and nobody else's nominee.</summary>
    private async Task<Result<ClubCorporateNominee>> NewSeatAsync(Guid organizationId, ClubCorporateMembership membership, Guid memberId, DateTime now, CancellationToken ct)
    {
        var member = await _db.ClubMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == organizationId && m.ErasedAtUtc == null, ct);
        if (member == null) return DomainErrors.Corporate.MemberNotFound;
        if (!SeatHolders.Contains(member.Status)) return DomainErrors.Corporate.MemberNotEligible;
        var elsewhere = await _db.ClubCorporateNominees.AsNoTracking()
            .Where(n => n.OrganizationId == organizationId && n.MemberId == memberId && n.RemovedAtUtc == null && n.CorporateMembershipId != membership.Id)
            .Join(_db.ClubCorporateMemberships.AsNoTracking(), n => n.CorporateMembershipId, m => m.Id, (n, m) => m.Name)
            .FirstOrDefaultAsync(ct);
        if (elsewhere != null) return DomainErrors.Corporate.AlreadySeated(elsewhere);
        return new ClubCorporateNominee
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CorporateMembershipId = membership.Id,
            MemberId = member.Id,
            MemberName = Clip($"{member.FirstName} {member.LastName}".Trim(), 200),
            AddedAtUtc = now,
            AddedByUserId = _user.UserId,
            AddedByName = Who(),
        };
    }

    private void Remove(ClubCorporateNominee nominee, DateTime now, string? reason, Guid? replacedBy)
    {
        nominee.RemovedAtUtc = now;
        nominee.RemovedByUserId = _user.UserId;
        nominee.RemovedByName = Who();
        nominee.RemovedReason = reason == null ? null : Clip(reason, 500);
        nominee.ReplacedByMemberId = replacedBy;
    }

    /// <summary>
    /// A change to the nominees: the membership's row is taken first and saved before anything
    /// else, then the seats are decided on what it holds, and the change saved after it. A change
    /// that lost a race is tried again on what the other one left.
    /// </summary>
    private async Task<Result<CorporateMembershipDto>> SeatChangeAsync(Guid organizationId, Guid id,
        Func<ClubCorporateMembership, List<ClubCorporateNominee>, DateTime, Task<Error?>> change, bool allowEnded, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var transaction = await BeginAsync(ct);
            try
            {
                var membership = await _db.ClubCorporateMemberships.FirstOrDefaultAsync(m => m.Id == id && m.OrganizationId == organizationId, ct);
                if (membership == null) return DomainErrors.Corporate.NotFound;
                if (!allowEnded && membership.Status == CorporateMembershipStatuses.Ended) return DomainErrors.Corporate.Ended;
                var now = DateTime.UtcNow;
                membership.NomineesChangedAtUtc = now;
                membership.UpdatedAtUtc = now;
                var seated = await _db.ClubCorporateNominees
                    .Where(n => n.CorporateMembershipId == id && n.OrganizationId == organizationId && n.RemovedAtUtc == null)
                    .ToListAsync(ct);
                await _reads.ReadAsync("corporate", ct);
                // The membership's row on its own first: of two changes at once, the second fails
                // here, before it has written anything, and is counted again.
                await _db.SaveChangesAsync(ct);
                if (await change(membership, seated, now) is { } refused)
                {
                    Forget();
                    return refused;
                }
                await _db.SaveChangesAsync(ct);
                if (transaction != null) await transaction.CommitAsync(ct);
                break;
            }
            catch (Exception ex) when (ClubEventService.LostRace(ex))
            {
                _db.ChangeTracker.Clear();
                if (attempt >= Attempts) return DomainErrors.Corporate.Changed;
            }
            finally
            {
                if (transaction != null) await transaction.DisposeAsync();
            }
        }
        _db.ChangeTracker.Clear();
        return await GetAsync(organizationId, id, ct);
    }

    /// <summary>A change to the membership's own details, from the version the screen read (If-Match).</summary>
    private async Task<Result<CorporateMembershipDto>> ChangeAsync(Guid organizationId, Guid id, Guid? expectedVersion,
        Func<ClubCorporateMembership, Error?> change, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var membership = await _db.ClubCorporateMemberships.FirstOrDefaultAsync(m => m.Id == id && m.OrganizationId == organizationId, ct);
            if (membership == null) return DomainErrors.Corporate.NotFound;
            if (expectedVersion is Guid version && membership.EditVersion != version) return DomainErrors.Corporate.Changed;
            if (membership.Status == CorporateMembershipStatuses.Ended) return DomainErrors.Corporate.Ended;
            if (change(membership) is { } refused)
            {
                _db.ChangeTracker.Clear();
                return refused;
            }
            membership.EditVersion = Guid.NewGuid();
            membership.UpdatedAtUtc = DateTime.UtcNow;
            try
            {
                await _db.SaveChangesAsync(ct);
                return await GetAsync(organizationId, id, ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                _db.ChangeTracker.Clear();
                if (attempt >= Attempts) return DomainErrors.Corporate.Changed;
            }
        }
    }

    // ── reading the request ──────────────────────────────────────────────────

    private async Task<Error?> ApplyAsync(Guid organizationId, ClubCorporateMembership membership, CorporateMembershipWriteRequest request, bool creating, CancellationToken ct)
    {
        if (request.Name != null)
        {
            var name = request.Name.Trim();
            if (name.Length == 0) return DomainErrors.Corporate.NameRequired;
            if (name.Length > 200) return DomainErrors.Corporate.TooLong("A name", 200);
            membership.Name = name;
        }
        if (request.Seats != null)
        {
            if (request.Seats is not (>= 1 and <= MaxSeats)) return DomainErrors.Corporate.SeatsInvalid;
            membership.Seats = request.Seats.Value;
        }
        if (request.NomineeCharges != null)
        {
            var rule = request.NomineeCharges.Trim().ToLowerInvariant();
            if (!NomineeChargeRules.All.Contains(rule)) return DomainErrors.Corporate.RuleInvalid;
            membership.NomineeCharges = rule;
        }
        if (request.TermMonths != null)
        {
            if (request.TermMonths is not (>= 1 and <= 60)) return DomainErrors.Corporate.TermInvalid;
            membership.TermMonths = request.TermMonths.Value;
        }
        if (request.StartsAtUtc != null)
        {
            if (ReadDate(request.StartsAtUtc) is not DateTime start) return DomainErrors.Corporate.DatesInvalid;
            membership.StartsAtUtc = start;
            // A new membership's renewal is a term after its start, unless a date is given.
            if (creating && request.RenewsAtUtc == null) membership.RenewsAtUtc = start.AddMonths(membership.TermMonths);
        }
        else if (creating && request.TermMonths != null && request.RenewsAtUtc == null)
            membership.RenewsAtUtc = membership.StartsAtUtc.AddMonths(membership.TermMonths);
        if (request.RenewsAtUtc != null)
        {
            if (ReadDate(request.RenewsAtUtc) is not DateTime renews) return DomainErrors.Corporate.DatesInvalid;
            membership.RenewsAtUtc = renews;
        }
        if (membership.RenewsAtUtc <= membership.StartsAtUtc) return DomainErrors.Corporate.DatesInvalid;
        if (request.BillingContactId != null)
        {
            if (request.BillingContactId == Guid.Empty) membership.BillingContactId = null;
            else
            {
                var exists = await _db.Contacts.AsNoTracking().AnyAsync(c => c.Id == request.BillingContactId && c.OrganizationId == organizationId, ct);
                if (!exists) return DomainErrors.Corporate.ContactNotFound;
                membership.BillingContactId = request.BillingContactId;
            }
        }
        if (request.BillingEmail != null)
        {
            var email = request.BillingEmail.Trim();
            if (email.Length > 0 && (email.Length > 320 || !ValidationHelper.IsValidEmail(email))) return DomainErrors.Corporate.EmailInvalid;
            membership.BillingEmail = email.Length == 0 ? null : email;
        }
        if (request.Language != null)
        {
            var language = request.Language.Trim();
            if (language.Length > 0 && AppLanguages.Normalise(language) is null) return DomainErrors.Corporate.LanguageInvalid;
            membership.Language = language.Length == 0 ? null : AppLanguages.Normalise(language);
        }
        if (request.Notes != null)
        {
            var notes = request.Notes.Trim();
            if (notes.Length > 2000) return DomainErrors.Corporate.TooLong("Notes", 2000);
            membership.Notes = notes.Length == 0 ? null : notes;
        }
        return null;
    }

    // ── answers ──────────────────────────────────────────────────────────────

    private async Task<CorporateMembershipDto> ToDtoAsync(Guid organizationId, ClubCorporateMembership m, DateTime now, CancellationToken ct, bool withAccount)
    {
        var nominees = await _db.ClubCorporateNominees.AsNoTracking()
            .Where(n => n.CorporateMembershipId == m.Id && n.OrganizationId == organizationId)
            .ToListAsync(ct);
        var memberIds = nominees.Select(n => n.MemberId).Distinct().ToList();
        var members = await _db.ClubMembers.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId && memberIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Status, x.Email, x.ErasedAtUtc })
            .ToDictionaryAsync(x => x.Id, ct);
        var ownDues = (await _db.ClubSubscriptions.AsNoTracking()
                .Where(s => s.OrganizationId == organizationId && memberIds.Contains(s.MemberId) && (s.Status == "Active" || s.Status == "PastDue"))
                .Select(s => s.MemberId).ToListAsync(ct))
            .ToHashSet();
        CorporateNomineeDto Nominee(ClubCorporateNominee n)
        {
            var member = members.GetValueOrDefault(n.MemberId);
            var erased = member?.ErasedAtUtc != null;
            return new CorporateNomineeDto
            {
                Id = n.Id,
                MemberId = n.MemberId,
                MemberName = n.MemberName,
                MemberStatus = member?.Status,
                MemberEmail = erased || string.IsNullOrWhiteSpace(member?.Email) ? null : member!.Email,
                AddedAtUtc = n.AddedAtUtc,
                AddedByName = n.AddedByName,
                RemovedAtUtc = n.RemovedAtUtc,
                RemovedReason = n.RemovedReason,
                ReplacedByMemberId = n.ReplacedByMemberId,
                OwnDuesRunning = n.RemovedAtUtc == null && ownDues.Contains(n.MemberId),
            };
        }

        CorporateDuesDto? dues = null;
        if (m.DuesSubscriptionId is Guid subscriptionId)
            dues = await _db.ClubSubscriptions.AsNoTracking().Where(s => s.Id == subscriptionId)
                .Select(s => new CorporateDuesDto(s.Id, s.Name, s.Amount, s.Currency, s.Cadence, s.Status, s.NextChargeAtUtc))
                .FirstOrDefaultAsync(ct);
        string? contactName = null;
        if (m.BillingContactId is Guid contactId)
            contactName = await _db.Contacts.AsNoTracking().Where(c => c.Id == contactId).Select(c => c.Name).FirstOrDefaultAsync(ct);
        ClientAccountDto? account = null;
        if (withAccount)
        {
            var read = await _ledger.ClientAccountAsync(organizationId, ClubAccountHolders.Company, m.CompanyId, ct);
            if (read.IsSuccess) account = read.Value;
        }

        var current = nominees.Where(n => n.RemovedAtUtc == null).OrderBy(n => n.AddedAtUtc).ThenBy(n => n.MemberName).Select(Nominee).ToList();
        var days = (int)Math.Floor((m.RenewsAtUtc - now).TotalDays);
        return new CorporateMembershipDto
        {
            Id = m.Id,
            CompanyId = m.CompanyId,
            CompanyName = m.CompanyName,
            Name = m.Name,
            Seats = m.Seats,
            SeatsUsed = current.Count,
            Status = m.Status,
            NomineeCharges = m.NomineeCharges,
            StartsAtUtc = m.StartsAtUtc,
            RenewsAtUtc = m.RenewsAtUtc,
            TermMonths = m.TermMonths,
            NotRenewing = m.NotRenewing,
            NotRenewingReason = m.NotRenewingReason,
            LastRenewedAtUtc = m.LastRenewedAtUtc,
            RenewalState = m.Status == CorporateMembershipStatuses.Ended ? "ended"
                : m.NotRenewing ? "not_renewing"
                : m.RenewsAtUtc < now ? "overdue"
                : days <= DueWithinDays ? "due"
                : "current",
            DaysToRenewal = days,
            Dues = dues,
            BillingContactId = m.BillingContactId,
            BillingContactName = contactName,
            BillingEmail = m.BillingEmail,
            Language = m.Language,
            Notes = m.Notes,
            Nominees = current,
            FormerNominees = nominees.Where(n => n.RemovedAtUtc != null).OrderByDescending(n => n.RemovedAtUtc).Select(Nominee).ToList(),
            Account = account,
            EndedAtUtc = m.EndedAtUtc,
            EndedReason = m.EndedReason,
            CreatedAtUtc = m.CreatedAtUtc,
            Version = m.EditVersion,
        };
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<IDbContextTransaction?> BeginAsync(CancellationToken ct) =>
        _db.Database.IsRelational() && _db.Database.CurrentTransaction == null
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;

    private void Forget()
    {
        foreach (var entry in _db.ChangeTracker.Entries()
                     .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList())
            entry.State = EntityState.Detached;
    }

    private string? Who() => string.IsNullOrWhiteSpace(_user.UserName) ? null : Clip(_user.UserName.Trim(), 200);

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];

    /// <summary>A date as the screens send it; stored as UTC.</summary>
    private static DateTime? ReadDate(string? raw)
    {
        var text = (raw ?? "").Trim();
        if (text.Length == 0) return null;
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var read)
            ? DateTime.SpecifyKind(read, DateTimeKind.Utc)
            : null;
    }
}
