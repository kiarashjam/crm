using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ACI.Infrastructure.Services;

/// <summary>
/// The club's bookable resources and their bookings (see <see cref="IClubReservations"/> for
/// the rules).
/// </summary>
/// <remarks>
/// <para>
/// A booking change takes its resource's row first (its Version moves on every booking), and
/// the conflict check is made on the bookings as they stand after that read: of two bookings
/// of one slot at the same moment, the second is refused on saving, checked again on what the
/// first saved, and answered with the clash. On SQL Server the change, its deposit and its fee
/// are one transaction; the in-memory provider the HTTP tests run on has none, and the order of
/// the saves (the resource's row first) keeps a lost race from writing anything there.
/// </para>
/// <para>
/// Times in a refusal are said in the club's own time zone (Organization.TimeZone), as the
/// front desk reads them.
/// </para>
/// </remarks>
public sealed class ClubReservationService : IClubReservations
{
    private const int MaxName = 200;
    private const int MaxEmail = 320;
    private const int MaxPeople = 10_000;
    private const decimal MaxAmount = 1_000_000m;
    private const int Attempts = 4;
    private static readonly string[] DepositMethods = ["CardOnFile", "Cash", "Check", "Wire"];
    private static readonly CultureInfo Words = CultureInfo.GetCultureInfo("en-GB");

    /// <summary>What a club usually has to book: what the reservations page seeded on its first read before.</summary>
    private static readonly (string Name, string Kind, int Capacity)[] Usual =
    [
        ("Main Dining Room", "Dining", 40),
        ("Private Dining Room", "Dining", 12),
        ("Boardroom", "Meeting", 10),
        ("Spa Suite", "Spa", 1),
        ("Tennis Court", "Court", 4),
        ("Fitness Studio", "Studio", 12),
        ("Lounge", "Lounge", 20),
    ];

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IClubLedger _ledger;
    private readonly ILedgerReadObserver _reads;
    private readonly IAutomationEngine? _automations;

    public ClubReservationService(AppDbContext db, ICurrentUserService user, IClubLedger ledger, ILedgerReadObserver reads, IAutomationEngine? automations = null)
    {
        _db = db;
        _user = user;
        _ledger = ledger;
        _reads = reads;
        _automations = automations;
    }

    // ── resources ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ClubResourceDto>> ResourcesAsync(Guid? organizationId, CancellationToken ct = default) =>
        (await _db.ClubResources.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId)
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Name)
            .ToListAsync(ct))
        .Select(ToDto).ToList();

    public async Task<Result<ClubResourceDto>> CreateResourceAsync(Guid? organizationId, ResourceWriteRequest request, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var resource = new ClubResource
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Currency = await CurrencyAsync(organizationId, ct),
            CreatedAtUtc = now,
            CreatedByUserId = _user.UserId,
            UpdatedAtUtc = now,
            EditVersion = Guid.NewGuid(),
        };
        if (Apply(resource, request, creating: true) is { } invalid) return invalid;
        _db.ClubResources.Add(resource);
        await _db.SaveChangesAsync(ct);
        return ToDto(resource);
    }

    public async Task<Result<ClubResourceDto>> UpdateResourceAsync(Guid? organizationId, Guid resourceId, ResourceWriteRequest request, Guid? expectedVersion, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            var resource = await _db.ClubResources.FirstOrDefaultAsync(r => r.Id == resourceId && r.OrganizationId == organizationId, ct);
            if (resource == null) return DomainErrors.Reservation.ResourceNotFound;
            // The version the settings were read at, not the row's: a booking moves the row's
            // and leaves the settings as they were.
            if (expectedVersion != null && resource.EditVersion != expectedVersion) return DomainErrors.Reservation.ResourceChanged;
            if (Apply(resource, request, creating: false) is { } invalid)
            {
                Forget();
                return invalid;
            }
            resource.EditVersion = Guid.NewGuid();
            resource.UpdatedAtUtc = DateTime.UtcNow;
            try
            {
                await _db.SaveChangesAsync(ct);
                return ToDto(resource);
            }
            catch (DbUpdateConcurrencyException)
            {
                // A booking took the row meanwhile; the settings are checked again on what it left.
                _db.ChangeTracker.Clear();
                if (attempt >= Attempts) return DomainErrors.Reservation.ResourceChanged;
            }
        }
    }

    public async Task<Result> DeleteResourceAsync(Guid? organizationId, Guid resourceId, CancellationToken ct = default)
    {
        var resource = await _db.ClubResources.FirstOrDefaultAsync(r => r.Id == resourceId && r.OrganizationId == organizationId, ct);
        if (resource == null) return DomainErrors.Reservation.ResourceNotFound;
        if (await _db.ClubReservations.AnyAsync(r => r.OrganizationId == organizationId && r.ResourceId == resourceId, ct))
            return DomainErrors.Reservation.ResourceHasBookings;
        _db.ClubResources.Remove(resource);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            Forget();
            return DomainErrors.Reservation.ResourceChanged;
        }
        return Result.Success();
    }

    /// <remarks>
    /// The page used to seed these on its first read, a write on a GET that two screens opening
    /// at once could do twice. Now someone asks for them, and each has an id worked out from the
    /// workspace and its name, so a second request at the same moment adds nothing more.
    /// </remarks>
    public async Task<IReadOnlyList<ClubResourceDto>> AddUsualResourcesAsync(Guid? organizationId, CancellationToken ct = default)
    {
        if (!await _db.ClubResources.AnyAsync(r => r.OrganizationId == organizationId, ct))
        {
            var currency = await CurrencyAsync(organizationId, ct);
            var now = DateTime.UtcNow;
            var order = 0;
            foreach (var (name, kind, capacity) in Usual)
            {
                var id = new Guid(MD5.HashData(Encoding.UTF8.GetBytes($"club-resource:{organizationId}:{name}")));
                if (await _db.ClubResources.IgnoreQueryFilters().AnyAsync(r => r.Id == id, ct)) continue;
                _db.ClubResources.Add(new ClubResource
                {
                    Id = id,
                    OrganizationId = organizationId,
                    Name = name,
                    Kind = kind,
                    Capacity = capacity,
                    BookingMode = BookingModes.For(kind),
                    Currency = currency,
                    SortOrder = order++,
                    CreatedAtUtc = now,
                    CreatedByUserId = _user.UserId,
                    UpdatedAtUtc = now,
                    EditVersion = Guid.NewGuid(),
                });
            }
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ClubEventService.LostRace(ex))
            {
                // Someone else added them at the same moment.
                Forget();
            }
        }
        return await ResourcesAsync(organizationId, ct);
    }

    private static Error? Apply(ClubResource resource, ResourceWriteRequest request, bool creating)
    {
        if (creating || request.Name != null)
        {
            var name = (request.Name ?? "").Trim();
            if (name.Length == 0) return DomainErrors.Reservation.ResourceNameRequired;
            if (name.Length > MaxName) return DomainErrors.Reservation.TooLong("A name", MaxName);
            resource.Name = name;
        }
        if (creating || request.Kind != null)
        {
            var kind = (request.Kind ?? "").Trim();
            if (!ResourceKinds.All.Contains(kind)) return DomainErrors.Reservation.KindInvalid;
            resource.Kind = kind;
        }
        if (creating || request.Capacity != null)
        {
            if (request.Capacity is not (>= 1 and <= MaxPeople)) return DomainErrors.Reservation.CapacityInvalid;
            resource.Capacity = request.Capacity.Value;
        }
        if (request.Description != null)
        {
            var description = request.Description.Trim();
            if (description.Length > 1000) return DomainErrors.Reservation.TooLong("A description", 1000);
            resource.Description = description.Length == 0 ? null : description;
        }
        if (request.BookingMode != null)
        {
            var mode = request.BookingMode.Trim();
            if (!BookingModes.All.Contains(mode)) return DomainErrors.Reservation.ModeInvalid;
            resource.BookingMode = mode;
        }
        else if (creating) resource.BookingMode = BookingModes.For(resource.Kind);
        if (request.DepositAmount != null)
        {
            if (Amount(request.DepositAmount) is not { } deposit) return DomainErrors.Reservation.AmountInvalid;
            resource.DepositAmount = deposit;
        }
        if (request.NoShowFee != null)
        {
            if (Amount(request.NoShowFee) is not { } fee) return DomainErrors.Reservation.AmountInvalid;
            resource.NoShowFee = fee;
        }
        if (request.Active is { } active) resource.Active = active;
        if (request.SortOrder is { } sort) resource.SortOrder = Math.Clamp(sort, 0, 10_000);
        return null;
    }

    // ── bookings ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ReservationDto>> ReservationsAsync(Guid? organizationId, ReservationQuery query, CancellationToken ct = default)
    {
        var rows = await _db.ClubReservations.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId
                && (query.MemberId == null || r.MemberId == query.MemberId)
                && (query.FromUtc == null || r.EndAtUtc > query.FromUtc)
                && (query.ToUtc == null || r.StartAtUtc < query.ToUtc))
            .OrderBy(r => r.StartAtUtc).ThenBy(r => r.CreatedAtUtc)
            .ToListAsync(ct);
        var paid = await PaidAsync(rows.Where(r => r.DepositChargeId != null).Select(r => r.DepositChargeId!.Value).ToList(), ct);
        return rows.Select(r => ToDto(r, paid)).ToList();
    }

    public async Task<Result<ReservationDto>> CreateAsync(Guid? organizationId, ReservationWriteRequest request, bool mayHandleMoney, CancellationToken ct = default)
    {
        var made = await MakeAsync(organizationId, request, mayHandleMoney, ct);
        // The workspace's rules for a new booking, once it is committed: outside the booking's
        // own attempts, so nothing a rule does can make the booking be tried again.
        if (made.IsSuccess && _automations != null && organizationId is Guid org && _user.UserId is Guid actor)
        {
            var booking = made.Value;
            await _automations.DispatchAsync(AutomationCatalog.ReservationCreated, new AutomationEvent(
                org, actor, booking.Id.ToString(), $"{booking.MemberName}: {booking.ResourceName}",
                Email: booking.MemberEmail, MemberId: booking.MemberId, RecordId: booking.Id), ct);
        }
        return made;
    }

    private async Task<Result<ReservationDto>> MakeAsync(Guid? organizationId, ReservationWriteRequest request, bool mayHandleMoney, CancellationToken ct)
    {
        if (request.ResourceId is not Guid resourceId) return DomainErrors.Reservation.ResourceNotFound;
        var person = await PersonAsync(organizationId, request.MemberId, request.MemberName, request.MemberEmail, ct);
        if (!person.IsSuccess) return person.Error;
        var party = request.PartySize ?? 1;
        if (party is < 1 or > MaxPeople) return DomainErrors.Reservation.PartyInvalid;
        if (Times(ReadDate(request.StartAtUtc), ReadDate(request.EndAtUtc)) is not { } window) return DomainErrors.Reservation.TimesInvalid;
        var (start, end) = window;
        var status = (request.Status ?? "").Trim();
        if (status.Length == 0) status = ReservationStatuses.Pending;
        // A booking starts out held: pending, confirmed, or (taken at the door) checked in.
        if (!ReservationStatuses.All.Contains(status) || !ReservationStatuses.HoldsResource(status)) return DomainErrors.Reservation.StatusInvalid;
        if (Notes(request.Notes, out var notes) is { } badNotes) return badNotes;
        var option = (request.Deposit ?? "").Trim().ToLowerInvariant();
        if (option.Length == 0) option = "charge";
        if (option is not ("charge" or "paid" or "waive")) return DomainErrors.Reservation.DepositOptionInvalid;
        var method = (request.DepositMethod ?? "").Trim();
        var reference = (request.DepositReference ?? "").Trim();
        var waiveReason = (request.DepositReason ?? "").Trim();
        if (reference.Length > 200) return DomainErrors.Reservation.TooLong("A reference", 200);
        if (waiveReason.Length > 500) return DomainErrors.Reservation.TooLong("A reason", 500);
        var zone = await ZoneAsync(organizationId, ct);
        var id = Guid.NewGuid();

        for (var attempt = 1; ; attempt++)
        {
            var transaction = await BeginAsync(ct);
            try
            {
                var resource = await _db.ClubResources.FirstOrDefaultAsync(r => r.Id == resourceId && r.OrganizationId == organizationId, ct);
                if (resource == null) return DomainErrors.Reservation.ResourceNotFound;
                if (!resource.Active) return DomainErrors.Reservation.ResourceInactive;
                // The resource's row first: of two bookings of one slot, the second is refused on
                // saving and checked again on what the first saved.
                resource.BookingsChangedAtUtc = DateTime.UtcNow;
                if (await ClashAsync(organizationId, resource, null, start, end, party, zone, ct) is { } clash) return Refuse(clash);

                // A deposit holds a booking until the member comes, and is given back when they are
                // checked in. A booking made already checked in (a table taken at the door) has
                // nothing left to secure, and since only a later change of status gives a deposit
                // back, one charged here stayed on the member's account.
                var deposit = status == ReservationStatuses.CheckedIn ? 0m : resource.DepositAmount;
                var waived = false;
                if (deposit > 0)
                {
                    if (option is "paid" or "waive" && !mayHandleMoney)
                        return Refuse(DomainErrors.Permission.Missing(MemberPermissions.ManageMoney, "record a deposit as paid or waive one"));
                    if (option == "waive")
                    {
                        if (waiveReason.Length == 0) return Refuse(DomainErrors.Reservation.WaiveReasonRequired);
                        waived = true;
                    }
                    else if (person.Value.MemberId == null) return Refuse(DomainErrors.Reservation.DepositNeedsMember);
                    else if (option == "paid" && !DepositMethods.Contains(method)) return Refuse(DomainErrors.Club.MethodInvalid);
                }
                await _reads.ReadAsync("reservations", ct);
                await _db.SaveChangesAsync(ct);

                var now = DateTime.UtcNow;
                var booking = new ClubReservation
                {
                    Id = id,
                    OrganizationId = organizationId,
                    ResourceId = resource.Id,
                    ResourceName = resource.Name,
                    MemberId = person.Value.MemberId,
                    MemberName = person.Value.Name,
                    MemberEmail = person.Value.Email,
                    PartySize = party,
                    StartAtUtc = start,
                    EndAtUtc = end,
                    Status = status,
                    Notes = notes,
                    DepositAmount = deposit,
                    Currency = resource.Currency,
                    DepositWaived = waived,
                    DepositWaivedReason = waived ? waiveReason : null,
                    DepositWaivedByName = waived ? Who() : null,
                    CreatedAtUtc = now,
                    CreatedByUserId = _user.UserId,
                    CreatedByName = Who(),
                    UpdatedAtUtc = now,
                };
                _db.ClubReservations.Add(booking);
                await _db.SaveChangesAsync(ct);

                if (deposit > 0 && !waived)
                {
                    var charged = await _ledger.PostChargeAsync(organizationId, new PostChargeRequest
                    {
                        MemberId = booking.MemberId,
                        Kind = "Booking",
                        Description = Clip($"Deposit: {resource.Name}, {Span(start, end, zone)}", 500),
                        Amount = deposit,
                    }, $"deposit:{booking.Id}", ct);
                    if (!charged.IsSuccess) return Refuse(charged.Error);
                    booking.DepositChargeId = charged.Value.Id;
                    if (option == "paid")
                    {
                        var payment = await _ledger.RecordPaymentAsync(organizationId, new RecordPaymentRequest
                        {
                            Amount = deposit,
                            Method = method,
                            Reference = reference.Length == 0 ? null : reference,
                            Notes = "Deposit taken at the desk",
                            ChargeId = charged.Value.Id,
                        }, $"deposit-payment:{booking.Id}", ct);
                        if (!payment.IsSuccess) return Refuse(payment.Error);
                        booking.DepositPaymentId = payment.Value.Id;
                    }
                    await _db.SaveChangesAsync(ct);
                }
                if (transaction != null) await transaction.CommitAsync(ct);
                var paid = await PaidAsync(booking.DepositChargeId is Guid c ? [c] : [], ct);
                return ToDto(booking, paid);
            }
            catch (Exception ex) when (ClubEventService.LostRace(ex))
            {
                _db.ChangeTracker.Clear();
                if (attempt >= Attempts) return DomainErrors.Reservation.ResourceChanged;
            }
            finally
            {
                if (transaction != null) await transaction.DisposeAsync();
            }
        }
    }

    public async Task<Result<ReservationDto>> UpdateAsync(Guid? organizationId, Guid reservationId, ReservationWriteRequest request, Guid? expectedVersion, CancellationToken ct = default)
    {
        if (request.PartySize is { } askedParty && askedParty is < 1 or > MaxPeople) return DomainErrors.Reservation.PartyInvalid;
        DateTime? askedStart = null, askedEnd = null;
        if (request.StartAtUtc != null && (askedStart = ReadDate(request.StartAtUtc)) == null) return DomainErrors.Reservation.TimesInvalid;
        if (request.EndAtUtc != null && (askedEnd = ReadDate(request.EndAtUtc)) == null) return DomainErrors.Reservation.TimesInvalid;
        var askedStatus = (request.Status ?? "").Trim();
        if (askedStatus.Length > 0 && !ReservationStatuses.All.Contains(askedStatus)) return DomainErrors.Reservation.StatusInvalid;
        string? notes = null;
        if (request.Notes != null && Notes(request.Notes, out notes) is { } badNotes) return badNotes;
        Result<Person>? person = null;
        if (request.MemberId != null || request.Guest == true)
        {
            person = await PersonAsync(organizationId, request.Guest == true ? null : request.MemberId, request.MemberName, request.MemberEmail, ct);
            if (!person.IsSuccess) return person.Error;
        }
        var zone = await ZoneAsync(organizationId, ct);

        for (var attempt = 1; ; attempt++)
        {
            var transaction = await BeginAsync(ct);
            try
            {
                var before = await _db.ClubReservations.AsNoTracking().FirstOrDefaultAsync(r => r.Id == reservationId && r.OrganizationId == organizationId, ct);
                if (before == null) return DomainErrors.Reservation.NotFound;
                if (expectedVersion != null && before.Version != expectedVersion) return DomainErrors.Reservation.Changed;

                var resourceId = request.ResourceId ?? before.ResourceId;
                var status = askedStatus.Length > 0 ? askedStatus : before.Status;
                if (status != before.Status && !CanMove(before.Status, status))
                    return DomainErrors.Reservation.CannotMove(before.Status, status);
                var start = askedStart ?? before.StartAtUtc;
                var end = askedEnd ?? before.EndAtUtc;
                if (Times(start, end) == null) return DomainErrors.Reservation.TimesInvalid;
                // A no-show posts the resource's fee and keeps the deposit, so it waits for the
                // booking's start, as an event's does: before that the member has missed nothing,
                // and a slip in the status list charged them for it.
                if (status == ReservationStatuses.NoShow && before.Status != ReservationStatuses.NoShow && DateTime.UtcNow < start)
                    return DomainErrors.Reservation.NotStarted;
                var party = request.PartySize ?? before.PartySize;
                var moved = resourceId != before.ResourceId || start != before.StartAtUtc || end != before.EndAtUtc || party > before.PartySize;
                var needsCheck = ReservationStatuses.HoldsResource(status) && (moved || !ReservationStatuses.HoldsResource(before.Status));

                // The resource's row before the booking's, so it is the first thing the save
                // writes: a booking that lost a race to another one writes nothing.
                ClubResource? resource = null;
                if (needsCheck || status == ReservationStatuses.NoShow)
                {
                    resource = await _db.ClubResources.FirstOrDefaultAsync(r => r.Id == resourceId && r.OrganizationId == organizationId, ct);
                    // A booking moved from a document can name a resource that did not come over.
                    // Its time cannot be checked against one, but a no-show needs it only for the
                    // fee, and with no resource there is none to post.
                    if (resource == null && needsCheck) return DomainErrors.Reservation.ResourceNotFound;
                    if (resource != null && needsCheck)
                    {
                        if (!resource.Active && resourceId != before.ResourceId) return DomainErrors.Reservation.ResourceInactive;
                        resource.BookingsChangedAtUtc = DateTime.UtcNow;
                        if (await ClashAsync(organizationId, resource, reservationId, start, end, party, zone, ct) is { } clash) return Refuse(clash);
                    }
                }
                var booking = await _db.ClubReservations.FirstAsync(r => r.Id == reservationId && r.OrganizationId == organizationId, ct);
                if (booking.Version != before.Version) throw new DbUpdateConcurrencyException("The booking changed while it was read.");
                if (resource != null && resourceId != booking.ResourceId)
                {
                    booking.ResourceId = resource.Id;
                    booking.ResourceName = resource.Name;
                }
                if (person is { IsSuccess: true })
                {
                    booking.MemberId = person.Value.MemberId;
                    booking.MemberName = person.Value.Name;
                    booking.MemberEmail = person.Value.Email;
                }
                booking.StartAtUtc = start;
                booking.EndAtUtc = end;
                booking.PartySize = party;
                if (request.Notes != null) booking.Notes = notes;

                // What a status change does with the money.
                var releaseDeposit = false;
                var takeBackFee = false;
                var postFee = 0m;
                if (status != booking.Status)
                {
                    if (status is ReservationStatuses.Cancelled or ReservationStatuses.CheckedIn or ReservationStatuses.Completed
                        && booking.DepositChargeId != null && booking.DepositReleasedAtUtc == null)
                        releaseDeposit = true;
                    if (status == ReservationStatuses.CheckedIn && booking.Status == ReservationStatuses.NoShow
                        && booking.NoShowChargeId != null && booking.NoShowFeeReversedAtUtc == null)
                        takeBackFee = true;
                    if (status == ReservationStatuses.NoShow && booking.MemberId != null && booking.NoShowChargeId == null && resource is { NoShowFee: > 0 })
                        postFee = resource.NoShowFee;
                    if (status == ReservationStatuses.Cancelled)
                    {
                        booking.CancelledAtUtc = DateTime.UtcNow;
                        booking.CancelledByName = Who();
                    }
                    booking.Status = status;
                }
                booking.UpdatedAtUtc = DateTime.UtcNow;
                await _reads.ReadAsync("reservations", ct);
                await _db.SaveChangesAsync(ct);

                if (releaseDeposit)
                {
                    var why = status == ReservationStatuses.Cancelled ? "Booking cancelled: the deposit is given back" : "Booking kept: the deposit is given back";
                    if (await TakeBackAsync(organizationId, booking.DepositChargeId!.Value, why, ct) is { } failed) return Refuse(failed);
                    booking.DepositReleasedAtUtc = DateTime.UtcNow;
                }
                if (takeBackFee)
                {
                    if (await TakeBackAsync(organizationId, booking.NoShowChargeId!.Value, "Came after being marked a no-show", ct) is { } failed) return Refuse(failed);
                    booking.NoShowFeeReversedAtUtc = DateTime.UtcNow;
                }
                if (postFee > 0)
                {
                    var posted = await _ledger.PostChargeAsync(organizationId, new PostChargeRequest
                    {
                        MemberId = booking.MemberId,
                        Kind = "Booking",
                        Description = Clip($"No-show: {booking.ResourceName}, {Span(booking.StartAtUtc, booking.EndAtUtc, zone)}", 500),
                        Amount = postFee,
                    }, $"noshow:{booking.Id}", ct);
                    if (!posted.IsSuccess) return Refuse(posted.Error);
                    booking.NoShowFee = postFee;
                    booking.NoShowChargeId = posted.Value.Id;
                }
                await _db.SaveChangesAsync(ct);
                if (transaction != null) await transaction.CommitAsync(ct);
                var paid = await PaidAsync(booking.DepositChargeId is Guid c ? [c] : [], ct);
                return ToDto(booking, paid);
            }
            catch (Exception ex) when (ClubEventService.LostRace(ex))
            {
                _db.ChangeTracker.Clear();
                if (attempt >= Attempts) return DomainErrors.Reservation.Changed;
            }
            finally
            {
                if (transaction != null) await transaction.DisposeAsync();
            }
        }
    }

    public async Task<Result> DeleteAsync(Guid? organizationId, Guid reservationId, CancellationToken ct = default)
    {
        var booking = await _db.ClubReservations.FirstOrDefaultAsync(r => r.Id == reservationId && r.OrganizationId == organizationId, ct);
        if (booking == null) return DomainErrors.Reservation.NotFound;
        // A deposit or a fee on the member's account names this booking: it stays, cancelled.
        if (booking.DepositChargeId != null || booking.NoShowChargeId != null) return DomainErrors.Reservation.HasMoney;
        _db.ClubReservations.Remove(booking);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            Forget();
            return DomainErrors.Reservation.Changed;
        }
        return Result.Success();
    }

    // ── rules ────────────────────────────────────────────────────────────────

    /// <summary>Where a booking can go from where it is.</summary>
    private static bool CanMove(string from, string to) => (from, to) switch
    {
        (ReservationStatuses.Pending, _) => to is ReservationStatuses.Confirmed or ReservationStatuses.CheckedIn or ReservationStatuses.Completed or ReservationStatuses.Cancelled or ReservationStatuses.NoShow,
        (ReservationStatuses.Confirmed, _) => to is ReservationStatuses.Pending or ReservationStatuses.CheckedIn or ReservationStatuses.Completed or ReservationStatuses.Cancelled or ReservationStatuses.NoShow,
        // A check-in made in error can be undone; the deposit it gave back stays given back.
        (ReservationStatuses.CheckedIn, _) => to is ReservationStatuses.Confirmed or ReservationStatuses.Completed,
        // Marked a no-show, then came after all.
        (ReservationStatuses.NoShow, _) => to is ReservationStatuses.CheckedIn,
        _ => false,
    };

    /// <summary>
    /// Whether <paramref name="party"/> people fit in <paramref name="resource"/> from
    /// <paramref name="start"/> to <paramref name="end"/>, beside the bookings that hold it then;
    /// the refusal names what is in the way.
    /// </summary>
    private async Task<Error?> ClashAsync(Guid? organizationId, ClubResource resource, Guid? except, DateTime start, DateTime end, int party, TimeZoneInfo zone, CancellationToken ct)
    {
        if (party > resource.Capacity) return DomainErrors.Reservation.TooBig(resource.Name, resource.Capacity, party);
        var holding = await _db.ClubReservations.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId && r.ResourceId == resource.Id && (except == null || r.Id != except)
                && (r.Status == ReservationStatuses.Pending || r.Status == ReservationStatuses.Confirmed || r.Status == ReservationStatuses.CheckedIn)
                && r.StartAtUtc < end && r.EndAtUtc > start)
            .OrderBy(r => r.StartAtUtc).ThenBy(r => r.CreatedAtUtc)
            .ToListAsync(ct);
        if (holding.Count == 0) return null;
        if (resource.BookingMode != BookingModes.Shared)
        {
            var first = holding[0];
            return DomainErrors.Reservation.Overlaps(resource.Name, Span(first.StartAtUtc, first.EndAtUtc, zone), first.MemberName, StatusWords(first.Status));
        }
        // Shared: how many are there at the busiest moment of the time asked. That moment is
        // one where a booking begins (or the time asked begins), so only those are counted.
        var moments = holding.Select(r => r.StartAtUtc < start ? start : r.StartAtUtc).Append(start).Distinct();
        var busiest = moments
            .Select(at => (At: at, There: holding.Where(r => r.StartAtUtc <= at && r.EndAtUtc > at).ToList()))
            .OrderByDescending(m => m.There.Sum(r => r.PartySize)).ThenBy(m => m.At)
            .First();
        var booked = busiest.There.Sum(r => r.PartySize);
        return booked + party <= resource.Capacity ? null
            : DomainErrors.Reservation.Full(resource.Name, resource.Capacity, booked, At(busiest.At, zone), busiest.There.Count, party);
    }

    /// <summary>A booking is for a member, whose name and email come from their record, or for a guest by name.</summary>
    private sealed record Person(Guid? MemberId, string Name, string? Email);

    private async Task<Result<Person>> PersonAsync(Guid? organizationId, Guid? memberId, string? guestName, string? guestEmail, CancellationToken ct)
    {
        if (memberId is Guid id)
        {
            var member = await _db.ClubMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id && m.OrganizationId == organizationId, ct);
            if (member == null) return DomainErrors.Reservation.MemberNotFound;
            var email = string.IsNullOrWhiteSpace(member.Email) ? null : Clip(member.Email.Trim(), MaxEmail);
            return new Person(member.Id, Clip($"{member.FirstName} {member.LastName}".Trim(), MaxName), email);
        }
        var name = (guestName ?? "").Trim();
        if (name.Length == 0) return DomainErrors.Reservation.NameRequired;
        if (name.Length > MaxName) return DomainErrors.Reservation.TooLong("A name", MaxName);
        var address = (guestEmail ?? "").Trim();
        if (address.Length > MaxEmail) return DomainErrors.Reservation.TooLong("An email", MaxEmail);
        return new Person(null, name, address.Length == 0 ? null : address);
    }

    /// <summary>
    /// Takes a deposit or a fee back by the ledger's rules: a void entry, and anything already
    /// paid for it house credit again. One a manager voided already needs nothing more.
    /// </summary>
    private async Task<Error?> TakeBackAsync(Guid? organizationId, Guid chargeId, string reason, CancellationToken ct)
    {
        var cancelled = await _ledger.CancelChargeAsync(organizationId, chargeId, reason, ct);
        if (cancelled.IsSuccess || cancelled.Error == DomainErrors.Club.ChargeVoided || cancelled.Error == DomainErrors.Club.ChargeNotFound) return null;
        return cancelled.Error;
    }

    // ── answers ──────────────────────────────────────────────────────────────

    private static ClubResourceDto ToDto(ClubResource r) => new()
    {
        Id = r.Id,
        Name = r.Name,
        Kind = r.Kind,
        Capacity = r.Capacity,
        Description = r.Description,
        BookingMode = r.BookingMode,
        DepositAmount = r.DepositAmount,
        NoShowFee = r.NoShowFee,
        Currency = r.Currency,
        Active = r.Active,
        SortOrder = r.SortOrder,
        Version = r.EditVersion,
    };

    private static ReservationDto ToDto(ClubReservation r, IReadOnlyDictionary<Guid, decimal> paid)
    {
        var depositState = r.DepositWaived ? "waived"
            : r.DepositChargeId is not Guid charge ? "none"
            : r.DepositReleasedAtUtc != null ? "released"
            : r.Status == ReservationStatuses.NoShow ? "kept"
            : paid.GetValueOrDefault(charge) >= r.DepositAmount ? "paid"
            : "charged";
        return new ReservationDto
        {
            Id = r.Id,
            ResourceId = r.ResourceId,
            ResourceName = r.ResourceName,
            MemberId = r.MemberId,
            MemberName = r.MemberName,
            MemberEmail = r.MemberEmail,
            PartySize = r.PartySize,
            StartAtUtc = r.StartAtUtc,
            EndAtUtc = r.EndAtUtc,
            Status = r.Status,
            Notes = r.Notes,
            Currency = r.Currency,
            DepositAmount = r.DepositAmount,
            DepositState = depositState,
            DepositChargeId = r.DepositChargeId,
            DepositWaivedReason = r.DepositWaivedReason,
            NoShowFee = r.NoShowFee,
            NoShowChargeId = r.NoShowChargeId,
            NoShowFeeState = r.NoShowChargeId == null ? null : r.NoShowFeeReversedAtUtc != null ? "reversed" : "charged",
            CreatedByName = r.CreatedByName,
            CreatedAtUtc = r.CreatedAtUtc,
            UpdatedAtUtc = r.UpdatedAtUtc,
            Version = r.Version,
        };
    }

    /// <summary>What each of these charges has been paid, net of what was taken back.</summary>
    private async Task<IReadOnlyDictionary<Guid, decimal>> PaidAsync(IReadOnlyCollection<Guid> chargeIds, CancellationToken ct) =>
        chargeIds.Count == 0 ? new Dictionary<Guid, decimal>()
        : await _db.ClubPaymentAllocations.AsNoTracking()
            .Where(a => chargeIds.Contains(a.ChargeId))
            .GroupBy(a => a.ChargeId).Select(g => new { ChargeId = g.Key, Paid = g.Sum(a => a.Amount) })
            .ToDictionaryAsync(g => g.ChargeId, g => g.Paid, ct);

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>A refusal decided part-way: nothing of the change is kept.</summary>
    private Error Refuse(Error error)
    {
        Forget();
        return error;
    }

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

    private async Task<string> CurrencyAsync(Guid? organizationId, CancellationToken ct)
    {
        if (organizationId == null) return RegionalSettings.DefaultCurrency;
        var currency = await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => o.Currency).FirstOrDefaultAsync(ct);
        return RegionalSettings.TryCurrency(currency, out var code) ? code : RegionalSettings.DefaultCurrency;
    }

    private async Task<TimeZoneInfo> ZoneAsync(Guid? organizationId, CancellationToken ct)
    {
        var zone = organizationId == null ? null
            : await _db.Organizations.AsNoTracking().Where(o => o.Id == organizationId).Select(o => o.TimeZone).FirstOrDefaultAsync(ct);
        return RegionalSettings.ZoneOf(zone);
    }

    private static DateTime Local(DateTime utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);

    /// <summary>"on Sat 14 Nov from 14:00 to 15:00", or with both days when it runs past midnight.</summary>
    private static string Span(DateTime startUtc, DateTime endUtc, TimeZoneInfo zone)
    {
        var start = Local(startUtc, zone);
        var end = Local(endUtc, zone);
        return start.Date == end.Date
            ? $"on {start.ToString("ddd d MMM", Words)} from {start.ToString("HH:mm", Words)} to {end.ToString("HH:mm", Words)}"
            : $"from {start.ToString("ddd d MMM HH:mm", Words)} to {end.ToString("ddd d MMM HH:mm", Words)}";
    }

    private static string At(DateTime utc, TimeZoneInfo zone) => Local(utc, zone).ToString("ddd d MMM, HH:mm", Words);

    private static string StatusWords(string status) => status switch
    {
        ReservationStatuses.CheckedIn => "checked in",
        _ => status.ToLowerInvariant(),
    };

    private string? Who() => string.IsNullOrWhiteSpace(_user.UserName) ? null : Clip(_user.UserName.Trim(), 200);

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];

    private static Error? Notes(string? raw, out string? notes)
    {
        var text = (raw ?? "").Trim();
        notes = text.Length == 0 ? null : text;
        return text.Length > 2000 ? DomainErrors.Reservation.TooLong("Notes", 2000) : null;
    }

    /// <summary>A booking's times when they make one: the end after the start, at most 14 days later.</summary>
    private static (DateTime Start, DateTime End)? Times(DateTime? start, DateTime? end) =>
        start is { } s && end is { } e && e > s && e - s <= TimeSpan.FromDays(14) ? (s, e) : null;

    private static decimal? Amount(decimal? value)
    {
        if (value is not { } v || v < 0 || v >= MaxAmount) return null;
        return Math.Round(v, 2, MidpointRounding.AwayFromZero);
    }

    private static DateTime? ReadDate(string? raw)
    {
        var text = (raw ?? "").Trim();
        if (text.Length == 0) return null;
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var read)
            ? DateTime.SpecifyKind(read, DateTimeKind.Utc)
            : null;
    }
}
