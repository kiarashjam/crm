using System.Globalization;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace ACI.Infrastructure.Services;

/// <summary>
/// Club events and their guest lists (see <see cref="IClubEvents"/> for the rules). This part
/// holds the reads, the event's own details, and how a change to a list is made.
/// </summary>
/// <remarks>
/// <para>
/// Every change to an event's list is decided on the list as it stands, with the event's row
/// taken first: its Version moves on every change, so of two changes to one list at the same
/// moment the second is refused on saving and decided again on what the first saved. That is
/// what makes two people taking the last seat one seat and one waitlist place, never two seats.
/// </para>
/// <para>
/// What follows from a change (a ticket posted or taken back, a no-show fee, an email to the
/// person who got a seat) is done after it is saved and before it is committed: on SQL Server
/// the whole change is one transaction, so a ticket the ledger refuses leaves no seat taken
/// and no email queued. The in-memory provider the HTTP tests run on has no transactions; the
/// order of the saves (the event's row first) keeps a lost race from writing anything there.
/// </para>
/// </remarks>
public sealed partial class ClubEventService : IClubEvents
{
    private const int MaxName = 200;
    private const int MaxEmail = 320;
    private const decimal MaxPrice = 1_000_000m;
    private const int MaxCapacity = 100_000;

    /// <summary>How many times a change that lost a race to another change of the same list is decided again.</summary>
    private const int Attempts = 4;

    private static readonly CultureInfo Words = CultureInfo.GetCultureInfo("en-GB");

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _user;
    private readonly IClubLedger _ledger;
    private readonly IOutbox _outbox;
    private readonly ICampaignRepository _messages;
    private readonly ILedgerReadObserver _reads;
    private readonly IClubRules _rules;

    public ClubEventService(AppDbContext db, ICurrentUserService user, IClubLedger ledger, IOutbox outbox, ICampaignRepository messages, ILedgerReadObserver reads, IClubRules rules)
    {
        _db = db;
        _user = user;
        _ledger = ledger;
        _outbox = outbox;
        _messages = messages;
        _reads = reads;
        _rules = rules;
    }

    // ── reads ────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ClubEventDto>> ListAsync(Guid? organizationId, CancellationToken ct = default)
    {
        var events = await _db.ClubEvents.AsNoTracking()
            .Where(e => e.OrganizationId == organizationId)
            .OrderBy(e => e.StartAtUtc).ThenBy(e => e.Name)
            .ToListAsync(ct);
        var counts = await CountsAsync(organizationId, null, ct);
        return events.Select(e => ToDto(e, counts.GetValueOrDefault(e.Id) ?? new ListCounts())).ToList();
    }

    public async Task<Result<ClubEventDto>> GetAsync(Guid? organizationId, Guid eventId, CancellationToken ct = default)
    {
        var ev = await _db.ClubEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId && e.OrganizationId == organizationId, ct);
        if (ev == null) return DomainErrors.ClubEvent.NotFound;
        var counts = await CountsAsync(organizationId, eventId, ct);
        return ToDto(ev, counts.GetValueOrDefault(eventId) ?? new ListCounts());
    }

    public async Task<Result<IReadOnlyList<EventRegistrationDto>>> RegistrationsAsync(Guid? organizationId, Guid eventId, CancellationToken ct = default)
    {
        var ev = await _db.ClubEvents.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eventId && e.OrganizationId == organizationId, ct);
        if (ev == null) return DomainErrors.ClubEvent.NotFound;
        var rows = await _db.ClubEventRegistrations.AsNoTracking()
            .Where(r => r.EventId == eventId && r.OrganizationId == organizationId)
            .ToListAsync(ct);
        return Ordered(rows).Select(r => ToDto(ev, r, rows)).ToList();
    }

    public async Task<IReadOnlyList<EventRegistrationDto>> MemberRegistrationsAsync(Guid? organizationId, Guid memberId, CancellationToken ct = default)
    {
        var rows = await _db.ClubEventRegistrations.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId && (r.MemberId == memberId || r.HostMemberId == memberId))
            .ToListAsync(ct);
        var eventIds = rows.Select(r => r.EventId).Distinct().ToList();
        var events = await _db.ClubEvents.AsNoTracking()
            .Where(e => e.OrganizationId == organizationId && eventIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, ct);
        // A member's place on a waitlist is counted among everyone on that event's list.
        var waiting = await _db.ClubEventRegistrations.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId && eventIds.Contains(r.EventId) && r.Status == RegistrationStatuses.Waitlisted)
            .ToListAsync(ct);
        return rows.Where(r => events.ContainsKey(r.EventId))
            .OrderByDescending(r => events[r.EventId].StartAtUtc).ThenBy(r => r.RegisteredAtUtc)
            .Select(r => ToDto(events[r.EventId], r, waiting.Where(w => w.EventId == r.EventId).ToList()))
            .ToList();
    }

    // ── the event itself ─────────────────────────────────────────────────────

    public async Task<Result<ClubEventDto>> CreateAsync(Guid? organizationId, ClubEventWriteRequest request, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var ev = new ClubEvent
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Currency = await CurrencyAsync(organizationId, ct),
            Status = ClubEventStatuses.Draft,
            Visibility = ClubEventVisibilities.MembersAndGuests,
            CreatedAtUtc = now,
            CreatedByUserId = _user.UserId,
            CreatedByName = Who(),
            UpdatedAtUtc = now,
            EditVersion = Guid.NewGuid(),
        };
        if (Apply(ev, request, creating: true) is { } invalid) return invalid;
        var status = (request.Status ?? "").Trim();
        if (status.Length > 0)
        {
            // A new event is a draft or published; it cannot start out cancelled or done.
            if (status is not (ClubEventStatuses.Draft or ClubEventStatuses.Published)) return DomainErrors.ClubEvent.EventStatusInvalid;
            ev.Status = status;
        }
        _db.ClubEvents.Add(ev);
        await _db.SaveChangesAsync(ct);
        return ToDto(ev, new ListCounts());
    }

    public async Task<Result<ClubEventDto>> UpdateAsync(Guid? organizationId, Guid eventId, ClubEventWriteRequest request, Guid? expectedVersion, CancellationToken ct = default)
    {
        var changed = await ChangeListAsync(organizationId, eventId, change =>
        {
            var ev = change.Event;
            if (expectedVersion != null && ev.EditVersion != expectedVersion) return Task.FromResult<Error?>(DomainErrors.ClubEvent.Changed);
            var capacityBefore = ev.Capacity;
            if (Apply(ev, request, creating: false) is { } invalid) return Task.FromResult<Error?>(invalid);

            if (request.UnnamedSeats is { } seats)
            {
                if (seats < 0 || seats > ev.UnnamedSeats) return Task.FromResult<Error?>(DomainErrors.ClubEvent.UnnamedSeatsInvalid);
                ev.UnnamedSeats = seats;
            }
            if (request.UnnamedWaitlist is { } waiting)
            {
                if (waiting < 0 || waiting > ev.UnnamedWaitlist) return Task.FromResult<Error?>(DomainErrors.ClubEvent.UnnamedSeatsInvalid);
                ev.UnnamedWaitlist = waiting;
            }
            // Lowering the capacity below the seats already taken would leave people with a
            // seat the event no longer has. An event already over (from before the move, say)
            // keeps its capacity when something else about it is edited.
            var taken = change.SeatsTaken;
            if (ev.Capacity != capacityBefore && ev.Capacity > 0 && ev.Capacity < taken)
                return Task.FromResult<Error?>(DomainErrors.ClubEvent.CapacityBelowSeats(taken));

            var status = (request.Status ?? "").Trim();
            if (status.Length > 0 && status != ev.Status)
            {
                if (!ClubEventStatuses.All.Contains(status)) return Task.FromResult<Error?>(DomainErrors.ClubEvent.EventStatusInvalid);
                if (ev.Status is ClubEventStatuses.Cancelled or ClubEventStatuses.Completed
                    || (status == ClubEventStatuses.Completed && DateTime.UtcNow < ev.StartAtUtc))
                    return Task.FromResult<Error?>(DomainErrors.ClubEvent.StatusStays(ev.Status, status));
                ev.Status = status;
                if (status == ClubEventStatuses.Cancelled) CancelEveryone(change);
            }
            ev.EditVersion = Guid.NewGuid();
            ev.UpdatedAtUtc = DateTime.UtcNow;
            // More seats, or unnamed ones released, go to whoever is waiting.
            FillFromWaitlist(change);
            return Task.FromResult<Error?>(null);
        }, ct);
        if (!changed.IsSuccess) return changed.Error;
        return ToDto(changed.Value.Event, Counts(changed.Value.Registrations));
    }

    public async Task<Result> DeleteAsync(Guid? organizationId, Guid eventId, CancellationToken ct = default)
    {
        var ev = await _db.ClubEvents.FirstOrDefaultAsync(e => e.Id == eventId && e.OrganizationId == organizationId, ct);
        if (ev == null) return DomainErrors.ClubEvent.NotFound;
        var registrations = await _db.ClubEventRegistrations
            .Where(r => r.EventId == eventId && r.OrganizationId == organizationId)
            .ToListAsync(ct);
        // A ticket or a fee on a member's account says what it was for; deleting the event
        // would leave it pointing at nothing. Cancelling takes the tickets back and keeps it.
        if (registrations.Any(r => r.ChargeId != null || r.NoShowChargeId != null)) return DomainErrors.ClubEvent.HasMoney;
        // Its guest list goes with it: nothing shows a deleted event's attendees, so their
        // names and emails would only stay in the table.
        _db.ClubEventRegistrations.RemoveRange(registrations);
        _db.ClubEvents.Remove(ev);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            Forget();
            return DomainErrors.ClubEvent.Changed;
        }
        return Result.Success();
    }

    /// <summary>The event's own details from the request: on create every one it needs, on an edit those sent.</summary>
    private static Error? Apply(ClubEvent ev, ClubEventWriteRequest request, bool creating)
    {
        if (creating || request.Name != null)
        {
            var name = (request.Name ?? "").Trim();
            if (name.Length == 0) return DomainErrors.ClubEvent.NameRequired;
            if (name.Length > MaxName) return DomainErrors.ClubEvent.TooLong("A name", MaxName);
            ev.Name = name;
        }
        if (request.Description != null)
        {
            var description = request.Description.Trim();
            if (description.Length > 4000) return DomainErrors.ClubEvent.TooLong("A description", 4000);
            ev.Description = description.Length == 0 ? null : description;
        }
        if (request.Location != null)
        {
            var location = request.Location.Trim();
            if (location.Length > 200) return DomainErrors.ClubEvent.TooLong("A location", 200);
            ev.Location = location.Length == 0 ? null : location;
        }
        if (creating || request.StartAtUtc != null || request.EndAtUtc != null)
        {
            var start = request.StartAtUtc != null ? ReadDate(request.StartAtUtc) : creating ? null : ev.StartAtUtc;
            var end = request.EndAtUtc != null ? ReadDate(request.EndAtUtc) : creating ? null : ev.EndAtUtc;
            if (start is not { } s || end is not { } e || e <= s || e - s > TimeSpan.FromDays(31)) return DomainErrors.ClubEvent.TimesInvalid;
            ev.StartAtUtc = s;
            ev.EndAtUtc = e;
        }
        if (request.Capacity is { } capacity)
        {
            if (capacity is < 0 or > MaxCapacity) return DomainErrors.ClubEvent.CapacityInvalid;
            ev.Capacity = capacity;
        }
        if (request.MemberPrice != null)
        {
            if (Price(request.MemberPrice) is not { } price) return DomainErrors.ClubEvent.PriceInvalid;
            ev.MemberPrice = price;
        }
        if (request.GuestPrice != null)
        {
            if (Price(request.GuestPrice) is not { } price) return DomainErrors.ClubEvent.PriceInvalid;
            ev.GuestPrice = price;
        }
        if (request.NoShowFee != null)
        {
            if (Price(request.NoShowFee) is not { } fee) return DomainErrors.ClubEvent.PriceInvalid;
            ev.NoShowFee = fee;
        }
        if (request.Visibility != null)
        {
            var visibility = request.Visibility.Trim();
            if (!ClubEventVisibilities.All.Contains(visibility)) return DomainErrors.ClubEvent.VisibilityInvalid;
            ev.Visibility = visibility;
        }
        if (creating || request.CoverEmoji != null)
        {
            var emoji = (request.CoverEmoji ?? "").Trim();
            if (emoji.Length > 16) return DomainErrors.ClubEvent.TooLong("A cover", 16);
            ev.CoverEmoji = emoji.Length == 0 ? "🎉" : emoji;
        }
        return null;
    }

    // ── changing a list ──────────────────────────────────────────────────────

    /// <summary>
    /// A change to one event's list: the event and its registrations as they stand (tracked),
    /// what the change adds, and what follows from it once it is saved.
    /// </summary>
    private sealed class ListChange
    {
        public ListChange(ClubEvent ev, List<ClubEventRegistration> registrations)
        {
            Event = ev;
            Registrations = registrations;
        }

        public ClubEvent Event { get; }

        /// <summary>The event's registrations, those this change adds included.</summary>
        public List<ClubEventRegistration> Registrations { get; }

        public List<ClubEventRegistration> Added { get; } = new();
        public List<ClubEventRegistration> Removed { get; } = new();
        public List<LoyaltyLedgerEntry> Points { get; } = new();
        public List<ClubEventRegistration> TicketsToPost { get; } = new();
        public List<(ClubEventRegistration Registration, string Reason)> TicketsToTakeBack { get; } = new();
        public List<ClubEventRegistration> FeesToPost { get; } = new();
        public List<(ClubEventRegistration Registration, string Reason)> FeesToTakeBack { get; } = new();
        public List<(ClubEventRegistration Registration, Notice Notice)> Notices { get; } = new();

        /// <summary>Seats taken: named people with a seat, and the unnamed seats.</summary>
        public int SeatsTaken => Registrations.Count(r => RegistrationStatuses.HoldsSeat(r.Status)) + Event.UnnamedSeats;

        /// <summary>Seats still free; null when the event has no limit.</summary>
        public int? SeatsFree => Event.Capacity > 0 ? Math.Max(0, Event.Capacity - SeatsTaken) : null;

        public bool HasSeat => SeatsFree is null or > 0;

        public void Add(ClubEventRegistration registration)
        {
            Added.Add(registration);
            Registrations.Add(registration);
        }

        public void Remove(ClubEventRegistration registration)
        {
            Removed.Add(registration);
            Registrations.Remove(registration);
        }
    }

    private enum Notice { Promoted, EventCancelled }

    /// <summary>
    /// Makes a change to an event's list: reads the event and its list, lets
    /// <paramref name="decide"/> change them (or refuse), saves with the event's row first,
    /// then does what follows (tickets, fees, points, emails) in the same transaction.
    /// </summary>
    private async Task<Result<ListChange>> ChangeListAsync(Guid? organizationId, Guid eventId, Func<ListChange, Task<Error?>> decide, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var transaction = await BeginAsync(ct);
            try
            {
                var result = await TryChangeListAsync(organizationId, eventId, decide, ct);
                if (result.IsSuccess)
                {
                    if (transaction != null) await transaction.CommitAsync(ct);
                }
                else Forget();
                return result;
            }
            catch (Exception ex) when (LostRace(ex))
            {
                // Another change to the list saved first. Read it again and decide on what it left.
                _db.ChangeTracker.Clear();
                if (attempt >= Attempts) return DomainErrors.ClubEvent.Changed;
            }
            finally
            {
                if (transaction != null) await transaction.DisposeAsync();
            }
        }
    }

    private async Task<Result<ListChange>> TryChangeListAsync(Guid? organizationId, Guid eventId, Func<ListChange, Task<Error?>> decide, CancellationToken ct)
    {
        var ev = await _db.ClubEvents.FirstOrDefaultAsync(e => e.Id == eventId && e.OrganizationId == organizationId, ct);
        if (ev == null) return DomainErrors.ClubEvent.NotFound;
        // The event's row is changed first, so it is the first thing the save writes: a change
        // that lost the race to another one is refused before it writes anything.
        ev.ListChangedAtUtc = DateTime.UtcNow;
        var registrations = await _db.ClubEventRegistrations
            .Where(r => r.EventId == eventId && r.OrganizationId == organizationId)
            .ToListAsync(ct);
        var change = new ListChange(ev, registrations);
        if (await decide(change) is { } refused) return refused;
        await _reads.ReadAsync("events", ct);

        await _db.SaveChangesAsync(ct);
        _db.ClubEventRegistrations.AddRange(change.Added);
        _db.ClubEventRegistrations.RemoveRange(change.Removed);
        _db.LoyaltyLedger.AddRange(change.Points);
        await _db.SaveChangesAsync(ct);

        if (await FollowUpAsync(organizationId, change, ct) is { } failed) return failed;
        await _db.SaveChangesAsync(ct);
        return change;
    }

    /// <summary>What follows from a saved change: tickets and fees posted or taken back on the ledger, and the emails.</summary>
    private async Task<Error?> FollowUpAsync(Guid? organizationId, ListChange change, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        foreach (var (registration, reason) in change.TicketsToTakeBack)
        {
            if (await TakeBackAsync(organizationId, registration.ChargeId!.Value, reason, ct) is { } failed) return failed;
            registration.ChargeReversedAtUtc = now;
        }
        foreach (var (registration, reason) in change.FeesToTakeBack)
        {
            if (await TakeBackAsync(organizationId, registration.NoShowChargeId!.Value, reason, ct) is { } failed) return failed;
            registration.NoShowFeeReversedAtUtc = now;
        }
        var zone = await ZoneAsync(organizationId, ct);
        foreach (var registration in change.TicketsToPost)
        {
            if (registration.TicketPrice <= 0 || PayerOf(registration) is not { } payer) continue;
            var guest = registration.TicketTier == TicketTiers.Guest ? $" (guest: {registration.Name})" : "";
            var posted = await _ledger.PostChargeAsync(organizationId, new PostChargeRequest
            {
                MemberId = payer,
                Kind = "Event",
                Description = Clip($"{registration.TicketTier} ticket: {change.Event.Name}, {When(change.Event.StartAtUtc, zone)}{guest}", 500),
                Amount = registration.TicketPrice,
            }, $"ticket:{registration.Id}", ct);
            if (!posted.IsSuccess) return posted.Error;
            registration.ChargeId = posted.Value.Id;
        }
        foreach (var registration in change.FeesToPost)
        {
            var fee = change.Event.NoShowFee;
            if (fee <= 0 || PayerOf(registration) is not { } payer) continue;
            var posted = await _ledger.PostChargeAsync(organizationId, new PostChargeRequest
            {
                MemberId = payer,
                Kind = "Event",
                Description = Clip($"No-show: {change.Event.Name}, {When(change.Event.StartAtUtc, zone)} ({registration.Name})", 500),
                Amount = fee,
            }, $"noshow:{registration.Id}", ct);
            if (!posted.IsSuccess) return posted.Error;
            registration.NoShowFee = fee;
            registration.NoShowChargeId = posted.Value.Id;
        }
        foreach (var (registration, notice) in change.Notices)
            await TellAsync(organizationId, change.Event, registration, notice, zone, ct);
        return null;
    }

    /// <summary>
    /// Takes a ticket or a fee back by the ledger's rules: a void entry, and anything already
    /// paid for it house credit again. One a manager voided already needs nothing more.
    /// </summary>
    private async Task<Error?> TakeBackAsync(Guid? organizationId, Guid chargeId, string reason, CancellationToken ct)
    {
        var cancelled = await _ledger.CancelChargeAsync(organizationId, chargeId, reason, ct);
        if (cancelled.IsSuccess || cancelled.Error == DomainErrors.Club.ChargeVoided || cancelled.Error == DomainErrors.Club.ChargeNotFound) return null;
        return cancelled.Error;
    }

    /// <summary>Who pays a registration's ticket or fee: the member on it, or the member who brings a guest.</summary>
    private static Guid? PayerOf(ClubEventRegistration registration) =>
        registration.TicketTier == TicketTiers.Guest ? registration.HostMemberId : registration.MemberId;

    // ── what changes on a list ───────────────────────────────────────────────

    /// <summary>
    /// Gives free seats to the waitlist, first come first served, while the event has not
    /// started and is still on: each one gets their ticket posted and is told.
    /// </summary>
    private static void FillFromWaitlist(ListChange change)
    {
        var ev = change.Event;
        if (ev.Status is not (ClubEventStatuses.Draft or ClubEventStatuses.Published) || DateTime.UtcNow >= ev.StartAtUtc) return;
        foreach (var next in Ordered(change.Registrations.Where(r => r.Status == RegistrationStatuses.Waitlisted)).ToList())
        {
            if (!change.HasSeat) break;
            Promote(change, next);
        }
    }

    private static void Promote(ListChange change, ClubEventRegistration registration)
    {
        registration.Status = RegistrationStatuses.Reserved;
        registration.PromotedAtUtc = DateTime.UtcNow;
        registration.UpdatedAtUtc = DateTime.UtcNow;
        change.TicketsToPost.Add(registration);
        change.Notices.Add((registration, Notice.Promoted));
    }

    private void Cancel(ListChange change, ClubEventRegistration registration, string reason)
    {
        var now = DateTime.UtcNow;
        registration.Status = RegistrationStatuses.Cancelled;
        registration.CancelledAtUtc = now;
        registration.CancelledByUserId = _user.UserId;
        registration.CancelledByName = Who();
        registration.CancelReason = reason;
        registration.UpdatedAtUtc = now;
        if (registration.ChargeId != null && registration.ChargeReversedAtUtc == null)
            change.TicketsToTakeBack.Add((registration, $"Registration cancelled: {reason}"));
    }

    /// <summary>The event is cancelled: everyone with a seat or a place in line is cancelled, their tickets taken back, and told.</summary>
    private void CancelEveryone(ListChange change)
    {
        foreach (var registration in change.Registrations.Where(r => r.Status is RegistrationStatuses.Reserved or RegistrationStatuses.Waitlisted).ToList())
        {
            Cancel(change, registration, "The event was cancelled");
            change.Notices.Add((registration, Notice.EventCancelled));
        }
    }

    // ── telling people ───────────────────────────────────────────────────────

    /// <summary>
    /// Emails the person on the ticket through the outbox: that a seat came free for them, or
    /// that the event is cancelled. A member's message is a service message (it goes whatever
    /// they said about marketing) and shows in their communications; someone with no address
    /// is not emailed.
    /// </summary>
    private async Task TellAsync(Guid? organizationId, ClubEvent ev, ClubEventRegistration registration, Notice notice, TimeZoneInfo zone, CancellationToken ct)
    {
        if (organizationId is not Guid org) return;
        var member = registration.MemberId is Guid memberId
            ? await _db.ClubMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == organizationId, ct)
            : null;
        var address = (member?.Email ?? registration.Email ?? "").Trim();
        if (!ValidationHelper.IsValidEmail(address)) return;

        var when = When(ev.StartAtUtc, zone);
        var where = string.IsNullOrWhiteSpace(ev.Location) ? "" : $" at {ev.Location}";
        var ticket = registration.ChargeId != null
            ? string.Create(CultureInfo.InvariantCulture, $"{registration.Currency} {registration.TicketPrice:N2}")
            : null;
        string subject, body;
        if (notice == Notice.Promoted)
        {
            subject = $"A place for you at {ev.Name}";
            body = $"Good news: a place has come free at {ev.Name} on {when}{where}, and it is yours."
                + (ticket != null ? $"\n\nThe ticket, {ticket}, is on {(registration.TicketTier == TicketTiers.Guest ? "your host's" : "your")} house account." : "")
                + "\n\nIf you can no longer come, please let us know, so the next person waiting can have the place.";
        }
        else
        {
            subject = $"{ev.Name} is cancelled";
            body = $"We are sorry: {ev.Name} on {when}{where} is cancelled."
                + (ticket != null && registration.ChargeReversedAtUtc != null ? $"\n\nThe ticket, {ticket}, has been taken off the house account it was on." : "")
                + "\n\nWe hope to see you at another event soon.";
        }
        var email = new OutboundEmailRequest
        {
            OrganizationId = org,
            Source = OutboundSources.Event,
            SourceId = registration.Id,
            To = [address],
            RecipientName = registration.Name,
            Subject = Clip(subject, 300),
            Body = body,
            CreatedByUserId = _user.UserId,
            ReplyToUserId = _user.UserId,
            MemberId = member?.Id,
            Purpose = member != null ? ConsentPurposes.Service : null,
        };
        if (member == null)
        {
            // A guest is no member, so nothing records it in a member's communications.
            await _outbox.QueueEmailAsync(email, ct);
            return;
        }
        await MemberMessageService.QueueAsync(_messages, _outbox, new MemberMessage
        {
            Id = Guid.NewGuid(),
            OrganizationId = org,
            MemberId = member.Id,
            MemberName = Clip($"{member.FirstName} {member.LastName}".Trim(), 200),
            Email = address,
            Channel = CampaignChannels.Email,
            Category = "Event",
            Purpose = ConsentPurposes.Service,
            Subject = Clip(subject, 300),
            Preview = Clip(body.Replace('\n', ' '), 500),
            CreatedByUserId = _user.UserId,
            CreatedAtUtc = DateTime.UtcNow,
        }, email, ct);
    }

    // ── answers ──────────────────────────────────────────────────────────────

    private sealed record ListCounts(int Seats = 0, int Waiting = 0, int CheckedIn = 0, int NoShows = 0, int WalkIns = 0, decimal Revenue = 0);

    private async Task<Dictionary<Guid, ListCounts>> CountsAsync(Guid? organizationId, Guid? eventId, CancellationToken ct)
    {
        var rows = await _db.ClubEventRegistrations.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId && (eventId == null || r.EventId == eventId))
            .GroupBy(r => new { r.EventId, r.Status, r.Source })
            .Select(g => new
            {
                g.Key.EventId,
                g.Key.Status,
                g.Key.Source,
                Count = g.Count(),
                Revenue = g.Sum(r => r.ChargeId != null && r.ChargeReversedAtUtc == null ? r.TicketPrice : 0m),
            })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.EventId).ToDictionary(g => g.Key, g => new ListCounts(
            Seats: g.Where(r => RegistrationStatuses.HoldsSeat(r.Status)).Sum(r => r.Count),
            Waiting: g.Where(r => r.Status == RegistrationStatuses.Waitlisted).Sum(r => r.Count),
            CheckedIn: g.Where(r => r.Status == RegistrationStatuses.CheckedIn).Sum(r => r.Count),
            NoShows: g.Where(r => r.Status == RegistrationStatuses.NoShow).Sum(r => r.Count),
            WalkIns: g.Where(r => r.Source == RegistrationSources.WalkIn && r.Status != RegistrationStatuses.Cancelled).Sum(r => r.Count),
            Revenue: g.Sum(r => r.Revenue)));
    }

    private static ListCounts Counts(IReadOnlyCollection<ClubEventRegistration> rows) => new(
        Seats: rows.Count(r => RegistrationStatuses.HoldsSeat(r.Status)),
        Waiting: rows.Count(r => r.Status == RegistrationStatuses.Waitlisted),
        CheckedIn: rows.Count(r => r.Status == RegistrationStatuses.CheckedIn),
        NoShows: rows.Count(r => r.Status == RegistrationStatuses.NoShow),
        WalkIns: rows.Count(r => r.Source == RegistrationSources.WalkIn && r.Status != RegistrationStatuses.Cancelled),
        Revenue: rows.Where(r => r.ChargeId != null && r.ChargeReversedAtUtc == null).Sum(r => r.TicketPrice));

    private static ClubEventDto ToDto(ClubEvent e, ListCounts counts)
    {
        var registered = counts.Seats + e.UnnamedSeats;
        return new ClubEventDto
        {
            Id = e.Id,
            Name = e.Name,
            Description = e.Description,
            Location = e.Location,
            StartAtUtc = e.StartAtUtc,
            EndAtUtc = e.EndAtUtc,
            Capacity = e.Capacity,
            RegisteredCount = registered,
            WaitlistCount = counts.Waiting + e.UnnamedWaitlist,
            CheckedInCount = counts.CheckedIn,
            NoShowCount = counts.NoShows,
            WalkInCount = counts.WalkIns,
            UnnamedSeats = e.UnnamedSeats,
            UnnamedWaitlist = e.UnnamedWaitlist,
            SeatsLeft = e.Capacity > 0 ? Math.Max(0, e.Capacity - registered) : null,
            MemberPrice = e.MemberPrice,
            GuestPrice = e.GuestPrice,
            Currency = e.Currency,
            NoShowFee = e.NoShowFee,
            TicketRevenue = counts.Revenue,
            Visibility = e.Visibility,
            Status = e.Status,
            CoverEmoji = e.CoverEmoji,
            CreatedAtUtc = e.CreatedAtUtc,
            UpdatedAtUtc = e.UpdatedAtUtc,
            Version = e.EditVersion,
        };
    }

    /// <summary>The list in the order people joined it, which is the order the waitlist is served in.</summary>
    private static IEnumerable<ClubEventRegistration> Ordered(IEnumerable<ClubEventRegistration> rows) =>
        rows.OrderBy(r => r.RegisteredAtUtc).ThenBy(r => r.CreatedAtUtc).ThenBy(r => r.Id);

    private static EventRegistrationDto ToDto(ClubEvent ev, ClubEventRegistration r, IReadOnlyCollection<ClubEventRegistration> list)
    {
        int? position = null;
        if (r.Status == RegistrationStatuses.Waitlisted)
            position = Ordered(list.Where(x => x.Status == RegistrationStatuses.Waitlisted)).TakeWhile(x => x.Id != r.Id).Count() + 1;
        var chargeState = r.ChargeId != null ? (r.ChargeReversedAtUtc != null ? "reversed" : "charged")
            : r.TicketPrice <= 0 ? "free"
            : r.Source is RegistrationSources.Moved or RegistrationSources.Uploaded ? "not-charged"
            : r.Status == RegistrationStatuses.Waitlisted ? "waiting"
            : r.Status == RegistrationStatuses.Cancelled ? "none"
            : PayerOf(r) == null ? "to-collect"
            : "none";
        return new EventRegistrationDto
        {
            Id = r.Id,
            EventId = r.EventId,
            EventName = ev.Name,
            EventStartAtUtc = ev.StartAtUtc,
            MemberId = r.MemberId,
            MemberName = r.Name,
            Email = r.Email,
            TicketTier = r.TicketTier,
            HostMemberId = r.HostMemberId,
            HostName = r.HostName,
            Status = r.Status,
            Source = r.Source,
            WalkIn = r.Source == RegistrationSources.WalkIn,
            RsvpAtUtc = r.RegisteredAtUtc,
            WaitlistPosition = position,
            PromotedAtUtc = r.PromotedAtUtc,
            CheckedInAtUtc = r.CheckedInAtUtc,
            CheckedInByName = r.CheckedInByName,
            NoShowAtUtc = r.NoShowAtUtc,
            CancelledAtUtc = r.CancelledAtUtc,
            CancelledByName = r.CancelledByName,
            CancelReason = r.CancelReason,
            TicketPrice = r.TicketPrice,
            Currency = r.Currency,
            ChargeState = chargeState,
            ChargeId = r.ChargeId,
            NoShowFee = r.NoShowFee,
            NoShowChargeId = r.NoShowChargeId,
            NoShowFeeState = r.NoShowChargeId == null ? null : r.NoShowFeeReversedAtUtc != null ? "reversed" : "charged",
            PointsAwarded = r.PointsAwarded,
        };
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task<IDbContextTransaction?> BeginAsync(CancellationToken ct) =>
        _db.Database.IsRelational() && _db.Database.CurrentTransaction == null
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;

    /// <summary>
    /// Whether a save lost a race: a version that moved under it, a unique index another
    /// request filled first, or SQL Server choosing it as a deadlock's victim.
    /// </summary>
    internal static bool LostRace(Exception ex) => ex switch
    {
        DbUpdateConcurrencyException => true,
        DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 or 1205 } } => true,
        SqlException { Number: 1205 } => true,
        InvalidOperationException { InnerException: SqlException { Number: 1205 } } => true,
        _ => false,
    };

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

    /// <summary>A moment as the club reads it, in its own time zone: "Friday 13 November 2026 at 19:00".</summary>
    private static string When(DateTime utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone).ToString("dddd d MMMM yyyy 'at' HH:mm", Words);

    private string? Who() => string.IsNullOrWhiteSpace(_user.UserName) ? null : Clip(_user.UserName.Trim(), 200);

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];

    /// <summary>A price or a fee: 0 or more, under a million, to the cent; null when it is not one.</summary>
    private static decimal? Price(decimal? value)
    {
        if (value is not { } v || v < 0 || v >= MaxPrice) return null;
        return Math.Round(v, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>A date as the screens send it, as UTC; null when it does not read as one.</summary>
    private static DateTime? ReadDate(string? raw)
    {
        var text = (raw ?? "").Trim();
        if (text.Length == 0) return null;
        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var read)
            ? DateTime.SpecifyKind(read, DateTimeKind.Utc)
            : null;
    }
}
