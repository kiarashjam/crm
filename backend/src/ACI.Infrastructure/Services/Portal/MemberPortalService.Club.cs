using System.Globalization;
using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services.Portal;

/// <summary>Events, bookings, consent and points in the member portal.</summary>
/// <remarks>
/// <para>
/// What a member may do themselves, on top of the club's own rules for events and bookings
/// (seats and the waitlist, clashes and capacity, tickets and deposits on the ledger):
/// </para>
/// <list type="bullet">
/// <item>Sign up and book only while their membership is active.</item>
/// <item>Sign up for a published event that has not started; book only what the club offers in
/// the portal (a resource's MemberBooking), at a time in the next 180 days, for at most a day.
/// A resource the club confirms by hand takes the booking as pending.</item>
/// <item>Cancel only their own place or booking, and only up to the club's cut-off before it
/// starts (the portal's MemberCancelHours); later, they ask the club.</item>
/// </list>
/// </remarks>
public sealed partial class MemberPortalService
{
    /// <summary>How far ahead the portal shows events.</summary>
    private static readonly TimeSpan EventsAhead = TimeSpan.FromDays(365);

    /// <summary>How far ahead a member may book.</summary>
    private static readonly TimeSpan BookAhead = TimeSpan.FromDays(180);

    /// <summary>The longest booking a member may make themselves.</summary>
    private static readonly TimeSpan LongestBooking = TimeSpan.FromHours(24);

    /// <summary>Past bookings shown beside the ones to come.</summary>
    private static readonly TimeSpan BookingsBehind = TimeSpan.FromDays(90);

    private const int MaxNotes = 500;

    // ── events ──────────────────────────────────────────────────────────────────

    public async Task<Result<IReadOnlyList<PortalEventDto>>> EventsAsync(PortalMember who, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        var found = await WhoAsync(who, ct);
        if (found.IsFailure) return found.Error;
        var me = found.Value;
        var now = Now;
        var events = (await _events.ListAsync(me.OrganizationId, ct))
            .Where(e => e.Status == ClubEventStatuses.Published && e.EndAtUtc > now && e.StartAtUtc < now + EventsAhead)
            .OrderBy(e => e.StartAtUtc)
            .ToList();
        var places = await PlacesAsync(me, ct);
        return events.Select(e => ToEvent(e, places, me, now)).ToList();
    }

    public async Task<Result<PortalEventDto>> RegisterAsync(PortalMember who, Guid eventId, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        var found = await WhoAsync(who, ct);
        if (found.IsFailure) return found.Error;
        var me = found.Value;
        if (!IsActive(me.Member)) return DomainErrors.Portal.NotActive;

        var ev = await _db.ClubEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId && e.OrganizationId == me.OrganizationId, ct);
        if (ev == null) return DomainErrors.Portal.RecordNotFound;
        if (ev.Status != ClubEventStatuses.Published) return DomainErrors.Portal.EventNotOpen;
        if (ev.StartAtUtc <= Now) return DomainErrors.Portal.EventStarted;

        var registered = await _events.RegisterAsync(me.OrganizationId, eventId, new RegisterForEventRequest
        {
            MemberId = me.MemberId,
            TicketTier = TicketTiers.Member,
        }, ct);
        if (registered.IsFailure) return InMembersWords(registered.Error);
        await AnnounceAsync(AutomationCatalog.EventRegistered, me.OrganizationId, registered.Value.Id.ToString(),
            $"{registered.Value.MemberName}: {ev.Name}", me.Member.Email, me.MemberId, registered.Value.Id, ct);
        return await EventAsync(me, eventId, ct);
    }

    public async Task<Result<PortalEventDto>> CancelRegistrationAsync(PortalMember who, Guid registrationId, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        var found = await WhoAsync(who, ct);
        if (found.IsFailure) return found.Error;
        var me = found.Value;

        // Their own place only: another member's, a household member's or a guest's is not theirs to cancel.
        var registration = await _db.ClubEventRegistrations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == registrationId && r.OrganizationId == me.OrganizationId && r.MemberId == me.MemberId, ct);
        if (registration == null) return DomainErrors.Portal.RecordNotFound;
        var ev = await _db.ClubEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == registration.EventId && e.OrganizationId == me.OrganizationId, ct);
        if (ev == null) return DomainErrors.Portal.RecordNotFound;
        if (registration.Status == RegistrationStatuses.Cancelled) return DomainErrors.Portal.AlreadyCancelled;
        if (registration.Status is not (RegistrationStatuses.Reserved or RegistrationStatuses.Waitlisted)) return DomainErrors.Portal.EventStarted;
        var now = Now;
        if (ev.StartAtUtc <= now) return DomainErrors.Portal.EventStarted;
        if (ev.StartAtUtc - TimeSpan.FromHours(me.CancelHours) <= now) return DomainErrors.Portal.TooLateToCancel(me.CancelHours);

        var cancelled = await _events.SetStatusAsync(me.OrganizationId, ev.Id, registration.Id, new RegistrationStatusRequest
        {
            Status = RegistrationStatuses.Cancelled,
            Reason = "Cancelled by the member in the member portal.",
        }, ct);
        if (cancelled.IsFailure) return InMembersWords(cancelled.Error);
        return await EventAsync(me, ev.Id, ct);
    }

    private async Task<Result<PortalEventDto>> EventAsync(Who me, Guid eventId, CancellationToken ct)
    {
        var ev = await _events.GetAsync(me.OrganizationId, eventId, ct);
        if (ev.IsFailure) return DomainErrors.Portal.RecordNotFound;
        return ToEvent(ev.Value, await PlacesAsync(me, ct), me, Now);
    }

    /// <summary>The member's places on event lists, and (for a household's primary) the household's, by event.</summary>
    private sealed record Places(ILookup<Guid, EventRegistrationDto> Mine, ILookup<Guid, string> Household);

    private async Task<Places> PlacesAsync(Who me, CancellationToken ct)
    {
        var mine = (await _events.MemberRegistrationsAsync(me.OrganizationId, me.MemberId, ct))
            .Where(r => r.MemberId == me.MemberId)
            .ToLookup(r => r.EventId);
        var others = new List<(Guid EventId, string Name)>();
        foreach (var memberId in await HouseholdOthersAsync(me, ct))
        {
            foreach (var r in await _events.MemberRegistrationsAsync(me.OrganizationId, memberId, ct))
            {
                if (r.MemberId == memberId && r.Status is RegistrationStatuses.Reserved or RegistrationStatuses.Waitlisted or RegistrationStatuses.CheckedIn)
                    others.Add((r.EventId, r.MemberName));
            }
        }
        return new Places(mine, others.ToLookup(o => o.EventId, o => o.Name));
    }

    private static PortalEventDto ToEvent(ClubEventDto e, Places places, Who me, DateTime now)
    {
        // Where the member stands: a place they hold (or wait for) first, else the latest they had.
        var mine = places.Mine[e.Id]
            .OrderBy(r => r.Status is RegistrationStatuses.Reserved or RegistrationStatuses.Waitlisted or RegistrationStatuses.CheckedIn ? 0 : 1)
            .ThenByDescending(r => r.RsvpAtUtc)
            .FirstOrDefault();
        var holding = mine?.Status is RegistrationStatuses.Reserved or RegistrationStatuses.Waitlisted;
        return new PortalEventDto
        {
            Id = e.Id,
            Name = e.Name,
            Description = e.Description,
            Location = e.Location,
            StartAtUtc = e.StartAtUtc,
            EndAtUtc = e.EndAtUtc,
            CoverEmoji = e.CoverEmoji,
            MemberPrice = e.MemberPrice,
            Currency = e.Currency,
            SeatsLeft = e.SeatsLeft,
            MyStatus = mine?.Status ?? "none",
            MyRegistrationId = mine?.Id,
            WaitlistPosition = mine?.WaitlistPosition,
            CanCancel = holding && e.StartAtUtc - TimeSpan.FromHours(me.CancelHours) > now,
            HouseholdComing = places.Household[e.Id].Distinct().ToList(),
        };
    }

    // ── bookings ────────────────────────────────────────────────────────────────

    public async Task<Result<IReadOnlyList<PortalResourceDto>>> ResourcesAsync(PortalMember who, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        var found = await WhoAsync(who, ct);
        if (found.IsFailure) return found.Error;
        var resources = await _db.ClubResources.AsNoTracking()
            .Where(r => r.OrganizationId == who.OrganizationId && r.Active && r.MemberBooking != MemberBookingModes.Off)
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Name)
            .ToListAsync(ct);
        return resources.Select(r => new PortalResourceDto(
            r.Id, r.Name, r.Kind, r.Capacity, r.Description, r.DepositAmount, r.Currency, r.MemberBooking == MemberBookingModes.Instant)).ToList();
    }

    public async Task<Result<IReadOnlyList<PortalBookingDto>>> BookingsAsync(PortalMember who, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        var found = await WhoAsync(who, ct);
        if (found.IsFailure) return found.Error;
        var me = found.Value;
        var now = Now;
        var mine = (await _reservations.ReservationsAsync(me.OrganizationId, new ReservationQuery { MemberId = me.MemberId, FromUtc = now - BookingsBehind }, ct))
            .Where(r => r.MemberId == me.MemberId)
            .Select(r => ToBooking(r, me, now, mine: true));
        var theirs = new List<PortalBookingDto>();
        foreach (var memberId in await HouseholdOthersAsync(me, ct))
        {
            theirs.AddRange((await _reservations.ReservationsAsync(me.OrganizationId, new ReservationQuery { MemberId = memberId, FromUtc = now }, ct))
                .Where(r => r.MemberId == memberId && ReservationStatuses.HoldsResource(r.Status))
                .Select(r => ToBooking(r, me, now, mine: false)));
        }
        return mine.Concat(theirs).OrderBy(b => b.StartAtUtc).ToList();
    }

    public async Task<Result<PortalBookingDto>> BookAsync(PortalMember who, PortalBookingRequest request, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        var found = await WhoAsync(who, ct);
        if (found.IsFailure) return found.Error;
        var me = found.Value;
        if (!IsActive(me.Member)) return DomainErrors.Portal.NotActive;

        if (request.ResourceId is not Guid resourceId) return DomainErrors.Portal.ResourceNotBookable;
        var resource = await _db.ClubResources.AsNoTracking().FirstOrDefaultAsync(r => r.Id == resourceId && r.OrganizationId == me.OrganizationId, ct);
        if (resource == null || !resource.Active || resource.MemberBooking == MemberBookingModes.Off) return DomainErrors.Portal.ResourceNotBookable;

        var now = Now;
        if (Instant(request.StartAtUtc) is not { } start || Instant(request.EndAtUtc) is not { } end
            || start <= now || end <= start || end - start > LongestBooking || start > now + BookAhead)
            return DomainErrors.Portal.BookingTimesInvalid;
        var notes = (request.Notes ?? "").Trim();
        if (notes.Length > MaxNotes) return DomainErrors.Reservation.TooLong("A note", MaxNotes);

        var made = await _reservations.CreateAsync(me.OrganizationId, new ReservationWriteRequest
        {
            ResourceId = resource.Id,
            MemberId = me.MemberId,
            PartySize = request.PartySize ?? 1,
            StartAtUtc = start.ToString("O", CultureInfo.InvariantCulture),
            EndAtUtc = end.ToString("O", CultureInfo.InvariantCulture),
            // A resource the club confirms by hand holds the time as a request until staff do.
            Status = resource.MemberBooking == MemberBookingModes.Instant ? ReservationStatuses.Confirmed : ReservationStatuses.Pending,
            Notes = notes.Length == 0 ? null : notes,
            // A deposit goes on their own account; recording one as paid or waiving it is the club's.
            Deposit = "charge",
        }, mayHandleMoney: false, ct);
        if (made.IsFailure) return InMembersWords(made.Error);
        await AnnounceAsync(AutomationCatalog.ReservationCreated, me.OrganizationId, made.Value.Id.ToString(),
            $"{made.Value.MemberName}: {made.Value.ResourceName}", me.Member.Email, me.MemberId, made.Value.Id, ct);
        return ToBooking(made.Value, me, Now, mine: true);
    }

    public async Task<Result<PortalBookingDto>> CancelBookingAsync(PortalMember who, Guid reservationId, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        var found = await WhoAsync(who, ct);
        if (found.IsFailure) return found.Error;
        var me = found.Value;

        // Their own booking only, found by its id and theirs.
        var booking = await _db.ClubReservations.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == reservationId && r.OrganizationId == me.OrganizationId && r.MemberId == me.MemberId, ct);
        if (booking == null) return DomainErrors.Portal.RecordNotFound;
        if (booking.Status == ReservationStatuses.Cancelled) return DomainErrors.Portal.AlreadyCancelled;
        var now = Now;
        if (!ReservationStatuses.HoldsResource(booking.Status) || booking.Status == ReservationStatuses.CheckedIn || booking.StartAtUtc <= now)
            return DomainErrors.Portal.EventStarted;
        if (booking.StartAtUtc - TimeSpan.FromHours(me.CancelHours) <= now) return DomainErrors.Portal.TooLateToCancel(me.CancelHours);

        var cancelled = await _reservations.UpdateAsync(me.OrganizationId, booking.Id, new ReservationWriteRequest
        {
            Status = ReservationStatuses.Cancelled,
        }, null, ct);
        if (cancelled.IsFailure) return InMembersWords(cancelled.Error);
        return ToBooking(cancelled.Value, me, Now, mine: true);
    }

    private static PortalBookingDto ToBooking(ReservationDto r, Who me, DateTime now, bool mine) => new()
    {
        Id = r.Id,
        ResourceId = r.ResourceId,
        ResourceName = r.ResourceName,
        PartySize = r.PartySize,
        StartAtUtc = r.StartAtUtc,
        EndAtUtc = r.EndAtUtc,
        Status = r.Status,
        DepositAmount = r.DepositAmount,
        DepositState = r.DepositState,
        Currency = r.Currency,
        // A household member's notes are theirs: the primary sees when and what, not what they wrote.
        Notes = mine ? r.Notes : null,
        Mine = mine,
        ForName = mine ? null : r.MemberName,
        CanCancel = mine && r.Status is ReservationStatuses.Pending or ReservationStatuses.Confirmed
            && r.StartAtUtc - TimeSpan.FromHours(me.CancelHours) > now,
    };

    // ── consent ─────────────────────────────────────────────────────────────────

    public async Task<Result<PortalConsentDto>> ConsentAsync(PortalMember who, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        var found = await WhoAsync(who, ct);
        if (found.IsFailure) return found.Error;
        var consent = await _consent.ForMemberAsync(found.Value.OrganizationId, found.Value.MemberId, ct);
        if (consent == null) return DomainErrors.Portal.MemberNotFound;
        return ToConsent(consent);
    }

    public async Task<Result<PortalConsentDto>> SetConsentAsync(PortalMember who, PortalConsentRequest request, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        var found = await WhoAsync(who, ct);
        if (found.IsFailure) return found.Error;
        var channel = (request.Channel ?? "").Trim().ToLowerInvariant();
        var purpose = (request.Purpose ?? "").Trim().ToLowerInvariant();
        if (!ConsentChannels.All.Contains(channel) || !ConsentPurposes.All.Contains(purpose) || request.Granted == null)
            return DomainErrors.Portal.ConsentChannelInvalid;
        var recorded = await _consent.RecordByMemberAsync(found.Value.OrganizationId, found.Value.MemberId, channel, purpose, request.Granted, ct);
        if (recorded.IsFailure) return recorded.Error;
        return ToConsent(recorded.Value);
    }

    /// <summary>Where each channel and purpose stands, without who on the club's side recorded it or their note.</summary>
    private static PortalConsentDto ToConsent(MemberConsentDto consent) => new(
        consent.Current.Select(c => new PortalConsentStateDto(c.Channel, c.Purpose, c.State, c.RecordedAtUtc, c.Source)).ToList());

    // ── points ──────────────────────────────────────────────────────────────────

    public async Task<Result<PortalPointsDto>> PointsAsync(PortalMember who, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        var found = await WhoAsync(who, ct);
        if (found.IsFailure) return found.Error;
        var entries = _db.LoyaltyLedger.AsNoTracking()
            .Where(e => e.OrganizationId == who.OrganizationId && e.MemberId == who.MemberId);
        var balance = await entries.SumAsync(e => (int?)e.Points, ct) ?? 0;
        var history = await entries.OrderByDescending(e => e.CreatedAtUtc).Take(200).ToListAsync(ct);
        return new PortalPointsDto(balance, history.Select(e => new PortalPointsEntryDto(e.CreatedAtUtc, e.Kind, e.Reason, e.Points, e.Note)).ToList());
    }

    // ── pieces ──────────────────────────────────────────────────────────────────

    /// <summary>For a household's primary, the other members of the household; for anyone else, nobody.</summary>
    private async Task<IReadOnlyList<Guid>> HouseholdOthersAsync(Who me, CancellationToken ct)
    {
        var household = await _households.ForMemberAsync(me.OrganizationId, me.MemberId, ct);
        if (household == null || household.PrimaryMemberId != me.MemberId) return [];
        return household.People.Where(p => p.MemberId is Guid id && id != me.MemberId).Select(p => p.MemberId!.Value).ToList();
    }

    private static DateTime? Instant(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) ? at.UtcDateTime : null;

    /// <summary>
    /// The club's services word their refusals for staff, and one names the member whose booking
    /// is in the way. A member hears what it means for them, and nothing about anyone else.
    /// </summary>
    private static Error InMembersWords(Error error) => error.Code switch
    {
        "Reservation.Overlap.Conflict" => new Error("Portal.Taken.Conflict", "That time is already booked. Choose another time."),
        "Reservation.Capacity.Conflict" => new Error("Portal.Full.Conflict", "There is not enough room at that time for a party of that size. Choose another time or a smaller party."),
        "Resource.Inactive.Conflict" => DomainErrors.Portal.ResourceNotBookable,
        "Reservation.CannotMove.Conflict" or "Reservation.Changed.Conflict" => new Error("Portal.Changed.Conflict", "This booking changed meanwhile. Reload and look again."),
        var code when code == DomainErrors.ClubEvent.AlreadyRegistered.Code => new Error("Portal.AlreadyRegistered.Conflict", "You are already on this event's list."),
        var code when code.StartsWith("ClubEvent.", StringComparison.Ordinal) && code.EndsWith(".Conflict", StringComparison.Ordinal)
            => new Error("Portal.Event.Conflict", "This event's list changed meanwhile. Reload and look again."),
        _ => error,
    };
}
