using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>Who is on an event's list, and the door: registering, walk-ins, check-ins, cancellations and no-shows.</summary>
public sealed partial class ClubEventService
{
    /// <summary>The person on a ticket, read and checked before the list is.</summary>
    private sealed record Person(Guid? MemberId, string Name, string? Email, string Tier, Guid? HostMemberId, string? HostName, string? MemberTier);

    public Task<Result<EventRegistrationDto>> RegisterAsync(Guid? organizationId, Guid eventId, RegisterForEventRequest request, CancellationToken ct = default) =>
        RegisterCoreAsync(organizationId, eventId, request, walkIn: false, ct);

    public Task<Result<EventRegistrationDto>> WalkInAsync(Guid? organizationId, Guid eventId, RegisterForEventRequest request, CancellationToken ct = default) =>
        RegisterCoreAsync(organizationId, eventId, request, walkIn: true, ct);

    private async Task<Result<EventRegistrationDto>> RegisterCoreAsync(Guid? organizationId, Guid eventId, RegisterForEventRequest request, bool walkIn, CancellationToken ct)
    {
        var person = await PersonAsync(organizationId, request, ct);
        if (!person.IsSuccess) return person.Error;
        var p = person.Value;
        var points = walkIn && p.MemberId != null ? await CheckInPointsAsync(organizationId, p.MemberTier, ct) : 0;
        ClubEventRegistration? added = null;
        var changed = await ChangeListAsync(organizationId, eventId, change =>
        {
            var ev = change.Event;
            if (ev.Status is ClubEventStatuses.Cancelled or ClubEventStatuses.Completed) return Task.FromResult<Error?>(DomainErrors.ClubEvent.NotOpen(ev.Status));
            // The same member twice on one list was a duplicate seat and a second ticket.
            if (p.MemberId != null && change.Registrations.Any(r => r.MemberId == p.MemberId
                    && r.Status is RegistrationStatuses.Reserved or RegistrationStatuses.CheckedIn or RegistrationStatuses.Waitlisted))
                return Task.FromResult<Error?>(DomainErrors.ClubEvent.AlreadyRegistered);
            // Someone at the door is let in on a free seat; ahead of the day, a full event puts
            // them on the waitlist. This is decided on the list as it stands now, and the save
            // is refused if another change got in first, so the last seat goes to one person.
            if (walkIn && !change.HasSeat) return Task.FromResult<Error?>(DomainErrors.ClubEvent.Full(ev.Capacity));
            var now = DateTime.UtcNow;
            added = new ClubEventRegistration
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                EventId = ev.Id,
                MemberId = p.MemberId,
                Name = p.Name,
                Email = p.Email,
                TicketTier = p.Tier,
                HostMemberId = p.HostMemberId,
                HostName = p.HostName,
                Status = walkIn ? RegistrationStatuses.CheckedIn : change.HasSeat ? RegistrationStatuses.Reserved : RegistrationStatuses.Waitlisted,
                Source = walkIn ? RegistrationSources.WalkIn : RegistrationSources.Registered,
                RegisteredAtUtc = now,
                TicketPrice = p.Tier == TicketTiers.Guest ? ev.GuestPrice : ev.MemberPrice,
                Currency = ev.Currency,
                CreatedAtUtc = now,
                CreatedByUserId = _user.UserId,
                CreatedByName = Who(),
                UpdatedAtUtc = now,
            };
            change.Add(added);
            if (RegistrationStatuses.HoldsSeat(added.Status)) change.TicketsToPost.Add(added);
            if (walkIn) MarkCheckedIn(change, added, points);
            return Task.FromResult<Error?>(null);
        }, ct);
        if (!changed.IsSuccess) return changed.Error;
        // The workspace's rules for someone put on the list (a seat, the waitlist or the door),
        // once the change is committed and outside its attempts.
        if (_automations != null && organizationId is Guid org && _user.UserId is Guid actor)
            await _automations.DispatchAsync(AutomationCatalog.EventRegistered, new AutomationEvent(
                org, actor, added!.Id.ToString(), $"{added.Name}: {changed.Value.Event.Name}",
                Email: added.Email, MemberId: added.MemberId, RecordId: added.Id), ct);
        return ToDto(changed.Value.Event, added!, changed.Value.Registrations);
    }

    public async Task<Result<EventRegistrationDto>> SetStatusAsync(Guid? organizationId, Guid eventId, Guid registrationId, RegistrationStatusRequest request, CancellationToken ct = default)
    {
        var next = (request.Status ?? "").Trim();
        if (next is not (RegistrationStatuses.CheckedIn or RegistrationStatuses.Cancelled or RegistrationStatuses.Reserved or RegistrationStatuses.NoShow))
            return DomainErrors.ClubEvent.StatusInvalid;
        var reason = (request.Reason ?? "").Trim();
        if (reason.Length > 500) return DomainErrors.ClubEvent.TooLong("A reason", 500);
        // The tier the member has now sets the points a check-in earns, as it did on the page.
        var points = next == RegistrationStatuses.CheckedIn
            ? await CheckInPointsAsync(organizationId, await TierOfAsync(organizationId, eventId, registrationId, ct), ct)
            : 0;

        ClubEventRegistration? subject = null;
        var changed = await ChangeListAsync(organizationId, eventId, change =>
        {
            var ev = change.Event;
            subject = change.Registrations.FirstOrDefault(r => r.Id == registrationId);
            if (subject == null) return Task.FromResult<Error?>(DomainErrors.ClubEvent.RegistrationNotFound);
            return Task.FromResult(next switch
            {
                RegistrationStatuses.CheckedIn => CheckIn(change, subject, points),
                RegistrationStatuses.Cancelled => CancelOne(change, subject, reason.Length == 0 ? "Cancelled by the club" : reason),
                RegistrationStatuses.Reserved => GiveSeat(change, subject),
                _ => MarkNoShow(change, subject),
            });
        }, ct);
        if (!changed.IsSuccess) return changed.Error;
        return ToDto(changed.Value.Event, subject!, changed.Value.Registrations);
    }

    public async Task<Result> RemoveAsync(Guid? organizationId, Guid eventId, Guid registrationId, CancellationToken ct = default)
    {
        var changed = await ChangeListAsync(organizationId, eventId, change =>
        {
            var registration = change.Registrations.FirstOrDefault(r => r.Id == registrationId);
            if (registration == null) return Task.FromResult<Error?>(DomainErrors.ClubEvent.RegistrationNotFound);
            // A ticket or a fee on a house account names this registration, and someone who
            // came earned their points here: both are records of what happened, so they stay.
            if (registration.ChargeId != null || registration.NoShowChargeId != null || registration.Status == RegistrationStatuses.CheckedIn)
                return Task.FromResult<Error?>(DomainErrors.ClubEvent.RegistrationHasMoney);
            var heldSeat = RegistrationStatuses.HoldsSeat(registration.Status);
            change.Remove(registration);
            if (heldSeat) FillFromWaitlist(change);
            return Task.FromResult<Error?>(null);
        }, ct);
        return changed.IsSuccess ? Result.Success() : changed.Error;
    }

    public async Task<Result<EventRegistrationDto>> ImportAsync(Guid? organizationId, Guid eventId, ImportRegistrationRequest request, CancellationToken ct = default)
    {
        var status = (request.Status ?? "").Trim();
        if (status.Length == 0) status = RegistrationStatuses.Reserved;
        if (status is not (RegistrationStatuses.Reserved or RegistrationStatuses.CheckedIn or RegistrationStatuses.Waitlisted or RegistrationStatuses.Cancelled))
            return DomainErrors.ClubEvent.StatusInvalid;
        var localId = (request.LocalId ?? "").Trim();
        if (localId.Length > 100) return DomainErrors.ClubEvent.TooLong("A browser's id for an attendee", 100);
        var tier = (request.TicketTier ?? "").Trim();
        if (tier.Length == 0) tier = TicketTiers.Member;
        if (!TicketTiers.All.Contains(tier)) return DomainErrors.ClubEvent.TicketInvalid;
        var name = (request.MemberName ?? "").Trim();
        var email = (request.Email ?? "").Trim();
        // A member who has left stays on the list under the name the browser kept, rather
        // than failing the upload.
        Guid? memberId = null;
        if (request.MemberId is Guid asked
            && await _db.ClubMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == asked && m.OrganizationId == organizationId, ct) is { } member)
        {
            memberId = member.Id;
            name = $"{member.FirstName} {member.LastName}".Trim();
            if (!string.IsNullOrWhiteSpace(member.Email)) email = member.Email.Trim();
        }
        if (name.Length == 0) return DomainErrors.ClubEvent.GuestNameRequired;
        if (name.Length > MaxName) return DomainErrors.ClubEvent.TooLong("A name", MaxName);
        if (email.Length > MaxEmail) return DomainErrors.ClubEvent.TooLong("An email", MaxEmail);

        ClubEventRegistration? row = null;
        var changed = await ChangeListAsync(organizationId, eventId, change =>
        {
            var ev = change.Event;
            // An upload retried after a dropped connection finds the copy it made.
            row = localId.Length == 0 ? null : change.Registrations.FirstOrDefault(r => r.ImportedFromId == localId);
            if (row != null) return Task.FromResult<Error?>(null);
            var now = DateTime.UtcNow;
            row = new ClubEventRegistration
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                EventId = ev.Id,
                MemberId = memberId,
                Name = name,
                Email = email.Length == 0 ? null : email,
                TicketTier = tier,
                Status = status,
                Source = RegistrationSources.Uploaded,
                RegisteredAtUtc = ReadDate(request.RsvpAtUtc) ?? now,
                CheckedInAtUtc = status == RegistrationStatuses.CheckedIn ? ReadDate(request.CheckedInAtUtc) ?? now : null,
                CancelledAtUtc = status == RegistrationStatuses.Cancelled ? now : null,
                TicketPrice = tier == TicketTiers.Guest ? ev.GuestPrice : ev.MemberPrice,
                Currency = ev.Currency,
                ImportedFromId = localId.Length == 0 ? null : localId,
                CreatedAtUtc = now,
                CreatedByUserId = _user.UserId,
                CreatedByName = Who(),
                UpdatedAtUtc = now,
            };
            // The browser counted this person on the server when it entered them, so they are
            // one of the event's unnamed seats (or places in line): named now, not counted twice.
            if (RegistrationStatuses.HoldsSeat(status) && ev.UnnamedSeats > 0) ev.UnnamedSeats--;
            if (status == RegistrationStatuses.Waitlisted && ev.UnnamedWaitlist > 0) ev.UnnamedWaitlist--;
            change.Add(row);
            return Task.FromResult<Error?>(null);
        }, ct);
        if (!changed.IsSuccess) return changed.Error;
        return ToDto(changed.Value.Event, row!, changed.Value.Registrations);
    }

    // ── the rules of each change ─────────────────────────────────────────────

    /// <remarks>
    /// The page gave a member their event points for every check-in it saw succeed, so two
    /// door hosts both checking someone in gave the points twice. A check-in that already
    /// happened is refused (409), and the points are given here, once, with the check-in.
    /// </remarks>
    private Error? CheckIn(ListChange change, ClubEventRegistration registration, int points)
    {
        if (change.Event.Status == ClubEventStatuses.Cancelled) return DomainErrors.ClubEvent.NotOpen(change.Event.Status);
        switch (registration.Status)
        {
            case RegistrationStatuses.CheckedIn:
                return DomainErrors.ClubEvent.AlreadyCheckedIn;
            case RegistrationStatuses.Cancelled:
                return DomainErrors.ClubEvent.CannotMove(registration.Status, RegistrationStatuses.CheckedIn);
            case RegistrationStatuses.Waitlisted:
                // Someone waiting who turns up gets a free seat at the door, and their ticket.
                if (!change.HasSeat) return DomainErrors.ClubEvent.Full(change.Event.Capacity);
                registration.PromotedAtUtc = DateTime.UtcNow;
                change.TicketsToPost.Add(registration);
                break;
            case RegistrationStatuses.NoShow:
                // Marked a no-show, then came after all: their seat back if there is one, and the fee taken back.
                if (!change.HasSeat) return DomainErrors.ClubEvent.Full(change.Event.Capacity);
                if (registration.NoShowChargeId != null && registration.NoShowFeeReversedAtUtc == null)
                    change.FeesToTakeBack.Add((registration, "Came after being marked a no-show"));
                break;
        }
        MarkCheckedIn(change, registration, points);
        return null;
    }

    /// <summary>
    /// What a member earns for coming to an event: the club's event check-in points times
    /// their tier's multiplier, from the hospitality settings and the price catalogue, as a
    /// tab's close and a venue check-in work theirs out.
    /// </summary>
    /// <remarks>
    /// The door gave 50 points times a multiplier written into the code (Silver 1.25, Gold
    /// 1.5, Platinum 2), so a tier the club added or repriced earned at the old rate or at 1,
    /// and the setting changed nothing. Read before the list is: the read can give a new
    /// workspace its default tiers, which is no part of a change to the list.
    /// </remarks>
    private async Task<int> CheckInPointsAsync(Guid? organizationId, string? memberTier, CancellationToken ct) =>
        (await _rules.ReadAsync(organizationId, ct)).PointsForEventCheckIn(memberTier);

    private void MarkCheckedIn(ListChange change, ClubEventRegistration registration, int points)
    {
        var now = DateTime.UtcNow;
        registration.Status = RegistrationStatuses.CheckedIn;
        registration.CheckedInAtUtc = now;
        registration.CheckedInByUserId = _user.UserId;
        registration.CheckedInByName = Who();
        registration.UpdatedAtUtc = now;
        if (registration.MemberId is not Guid memberId || registration.PointsAwarded != null) return;
        registration.PointsAwarded = points;
        // A club that gives no points for coming writes no entry of nothing, as a visit does not.
        if (points <= 0) return;
        var entry = new LoyaltyLedgerEntry
        {
            Id = Guid.NewGuid(),
            OrganizationId = registration.OrganizationId,
            MemberId = memberId,
            MemberName = registration.Name,
            Kind = "Earned",
            Reason = "EventAttendance",
            Points = points,
            Note = Clip(change.Event.Name, 500),
            CreatedAtUtc = now,
        };
        change.Points.Add(entry);
        registration.LoyaltyEntryId = entry.Id;
    }

    private Error? CancelOne(ListChange change, ClubEventRegistration registration, string reason)
    {
        if (registration.Status is not (RegistrationStatuses.Reserved or RegistrationStatuses.Waitlisted))
            return DomainErrors.ClubEvent.CannotMove(registration.Status, RegistrationStatuses.Cancelled);
        var heldSeat = RegistrationStatuses.HoldsSeat(registration.Status);
        Cancel(change, registration, reason);
        // The seat it gave back goes to the first in line, who is told.
        if (heldSeat) FillFromWaitlist(change);
        return null;
    }

    private static Error? GiveSeat(ListChange change, ClubEventRegistration registration)
    {
        if (change.Event.Status is ClubEventStatuses.Cancelled or ClubEventStatuses.Completed) return DomainErrors.ClubEvent.NotOpen(change.Event.Status);
        if (registration.Status != RegistrationStatuses.Waitlisted)
            return DomainErrors.ClubEvent.CannotMove(registration.Status, RegistrationStatuses.Reserved);
        if (!change.HasSeat) return DomainErrors.ClubEvent.Full(change.Event.Capacity);
        Promote(change, registration);
        return null;
    }

    private static Error? MarkNoShow(ListChange change, ClubEventRegistration registration)
    {
        if (registration.Status != RegistrationStatuses.Reserved)
            return DomainErrors.ClubEvent.CannotMove(registration.Status, RegistrationStatuses.NoShow);
        if (DateTime.UtcNow < change.Event.StartAtUtc) return DomainErrors.ClubEvent.NotStarted;
        registration.Status = RegistrationStatuses.NoShow;
        registration.NoShowAtUtc = DateTime.UtcNow;
        registration.UpdatedAtUtc = DateTime.UtcNow;
        // The ticket stands; the event's no-show fee, when it has one, goes on the account too.
        if (change.Event.NoShowFee > 0) change.FeesToPost.Add(registration);
        return null;
    }

    // ── who is on a ticket ───────────────────────────────────────────────────

    /// <summary>
    /// A member is looked up in this workspace, and their name and email come from the member
    /// record; a guest needs a name, and may be brought by a member, whose account their
    /// ticket goes on.
    /// </summary>
    private async Task<Result<Person>> PersonAsync(Guid? organizationId, RegisterForEventRequest request, CancellationToken ct)
    {
        var tier = (request.TicketTier ?? "").Trim();
        if (tier.Length == 0) tier = TicketTiers.Member;
        if (!TicketTiers.All.Contains(tier)) return DomainErrors.ClubEvent.TicketInvalid;
        var email = (request.Email ?? "").Trim();
        if (email.Length > MaxEmail) return DomainErrors.ClubEvent.TooLong("An email", MaxEmail);

        if (tier == TicketTiers.Member)
        {
            if (request.MemberId is not Guid memberId) return DomainErrors.ClubEvent.MemberRequired;
            var member = await _db.ClubMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == organizationId, ct);
            if (member == null) return DomainErrors.ClubEvent.MemberNotFound;
            var memberEmail = string.IsNullOrWhiteSpace(member.Email) ? null : Clip(member.Email.Trim(), MaxEmail);
            return new Person(member.Id, Clip($"{member.FirstName} {member.LastName}".Trim(), MaxName), memberEmail, tier, null, null, member.Tier);
        }

        var name = (request.MemberName ?? "").Trim();
        if (name.Length == 0) return DomainErrors.ClubEvent.GuestNameRequired;
        if (name.Length > MaxName) return DomainErrors.ClubEvent.TooLong("A name", MaxName);
        Guid? hostId = null;
        string? hostName = null;
        if (request.HostMemberId is Guid asked)
        {
            var host = await _db.ClubMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == asked && m.OrganizationId == organizationId, ct);
            if (host == null) return DomainErrors.ClubEvent.HostNotFound;
            hostId = host.Id;
            hostName = Clip($"{host.FirstName} {host.LastName}".Trim(), MaxName);
        }
        return new Person(null, name, email.Length == 0 ? null : email, tier, hostId, hostName, null);
    }

    /// <summary>The tier of the member on a registration, read before the list is, for the points a check-in earns.</summary>
    private async Task<string?> TierOfAsync(Guid? organizationId, Guid eventId, Guid registrationId, CancellationToken ct)
    {
        var memberId = await _db.ClubEventRegistrations.AsNoTracking()
            .Where(r => r.Id == registrationId && r.EventId == eventId && r.OrganizationId == organizationId)
            .Select(r => r.MemberId).FirstOrDefaultAsync(ct);
        return memberId == null ? null
            : await _db.ClubMembers.AsNoTracking().Where(m => m.Id == memberId && m.OrganizationId == organizationId).Select(m => m.Tier).FirstOrDefaultAsync(ct);
    }
}
