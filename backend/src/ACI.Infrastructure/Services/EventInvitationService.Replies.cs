using ACI.Application.Common;
using ACI.Application.Common.I18n;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services;

/// <summary>
/// The member's side of an invitation: the public reply page their link opens, and their
/// answer; and the event's expected attendance.
/// </summary>
/// <remarks>
/// <para>
/// The page is reached by someone with no account, and the token in the link is the whole
/// permission: it is looked up by its hash in every workspace (the one place this reads across
/// them), and from then on the page acts for the invitation's own workspace alone. It shows the
/// club, the event and the member's own answer, nothing about anyone else, and refuses once the
/// event has started, for a cancelled event, for an erased member and while the club has turned
/// its Club module off.
/// </para>
/// <para>
/// "I'll come" goes through the event's own registration (IClubEvents.RegisterAsync): a seat
/// while there is one, else the waitlist, the ticket on the member's account, the club's
/// automations for a registration. "I can't come" after that cancels the registration as staff
/// do (SetStatusAsync), which takes the ticket back and gives the seat to the first in line.
/// </para>
/// </remarks>
public sealed partial class EventInvitationService
{
    /// <summary>The link a preview and a test carry, which the reply page shows as a sample.</summary>
    public const string SampleToken = "sample";

    public async Task<Result<InvitationPageDto>> PageAsync(string token, CancellationToken ct = default)
    {
        var found = await FindAsync(token, ct);
        if (found.IsFailure) return found.Error;
        var (invitation, member, ev) = found.Value;
        using var acting = _tenant.ActFor(invitation.OrganizationId, null);
        return await PageOfAsync(invitation, member, ev, ct);
    }

    public async Task<Result<InvitationPageDto>> AnswerAsync(string token, string? answer, CancellationToken ct = default)
    {
        var said = (answer ?? "").Trim().ToLowerInvariant();
        if (said is not (ClubEventInvitationAnswers.Coming or ClubEventInvitationAnswers.Declined))
            return new Error("Invitation.AnswerInvalid", "Answer \"coming\" or \"declined\".");
        var found = await FindAsync(token, ct);
        if (found.IsFailure) return found.Error;
        var (invitation, member, ev) = found.Value;
        var org = invitation.OrganizationId!.Value;
        using var acting = _tenant.ActFor(org, null);

        var list = await RegistrationsAsync(org, ev.Id, ct);
        var current = Active(list, member.Id);
        Guid? registrationId = invitation.RegistrationId;
        if (said == ClubEventInvitationAnswers.Coming)
        {
            if (current != null) registrationId = current.Id;
            // As the member portal does: signing up is for members whose membership is active. One who
            // lapsed after the invitation went is not put on the list and charged by an old link.
            else if (!IsActive(member)) return NotActive;
            else
            {
                // Through the event's own rules, as staff adding them would: a seat while there is
                // one, else the waitlist; the ticket on their account; the club's automations.
                var registered = await _events.RegisterAsync(org, ev.Id, new RegisterForEventRequest { MemberId = member.Id, TicketTier = TicketTiers.Member }, ct);
                if (registered.IsSuccess) registrationId = registered.Value.Id;
                else if (registered.Error == DomainErrors.ClubEvent.AlreadyRegistered
                         && Active(await RegistrationsAsync(org, ev.Id, CancellationToken.None), member.Id) is { } meanwhile)
                    // The same answer from a second tab, a moment before this one.
                    registrationId = meanwhile.Id;
                else return registered.Error;
            }
        }
        else if (current is { Status: RegistrationStatuses.Reserved or RegistrationStatuses.Waitlisted })
        {
            // The club's cut-off for cancelling, as in the member portal: later, the member asks the club,
            // so a seat (and its ticket) is not given back hours before the event without anyone knowing.
            var cancelHours = await CancelHoursAsync(org, ct);
            if (ev.StartAtUtc - TimeSpan.FromHours(cancelHours) <= DateTime.UtcNow) return TooLateToCancel(cancelHours);
            // As staff cancelling them: the ticket taken back, the seat to the first in line, who is told.
            var cancelled = await _events.SetStatusAsync(org, ev.Id, current.Id,
                new RegistrationStatusRequest { Status = RegistrationStatuses.Cancelled, Reason = "Answered \"I can't come\" to their invitation" }, ct);
            // Refused because the same answer from a second tab cancelled it a moment before: done.
            if (cancelled.IsFailure && Active(await RegistrationsAsync(org, ev.Id, CancellationToken.None), member.Id)
                    is { Status: RegistrationStatuses.Reserved or RegistrationStatuses.Waitlisted })
                return cancelled.Error;
        }

        // Read afresh: the registration above saved through the same unit of work, and may have
        // cleared it after a race.
        var row = await _db.ClubEventInvitations.FirstAsync(i => i.Id == invitation.Id, CancellationToken.None);
        row.Answer = said;
        row.AnsweredAtUtc = DateTime.UtcNow;
        row.UpdatedAtUtc = row.AnsweredAtUtc.Value;
        row.RegistrationId = registrationId;
        await _db.SaveChangesAsync(CancellationToken.None);
        _logger.LogInformation("Invitation {InvitationId} to event {EventId} answered: {Answer}", invitation.Id, ev.Id, said);
        return await PageOfAsync(row, member, ev, CancellationToken.None);
    }

    /// <summary>
    /// The invitation a reply link names, with its member and event, or why the page refuses:
    /// not one of ours (404, as for an erased member, so it says nothing more), the Club module
    /// off (403), the event cancelled, started or taken back to a draft (409).
    /// </summary>
    private async Task<Result<(ClubEventInvitation Invitation, ClubMember Member, ClubEvent Event)>> FindAsync(string token, CancellationToken ct)
    {
        if (!InvitationReplyToken.LooksLikeOne(token)) return LinkNotFound;
        var hash = InvitationReplyToken.Hash(token);
        // Across workspaces, on purpose: the link names no workspace until it is found. Only the
        // invitation whose hash matches is read, and the page acts for its workspace from here on.
        var invitation = await _db.ClubEventInvitations.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(i => i.TokenHash == hash || i.ReminderTokenHash == hash, ct);
        if (invitation?.OrganizationId is not Guid org) return LinkNotFound;
        using var acting = _tenant.ActFor(org, null);
        // Outside the modules on purpose, so a link already sent is answered with a reason, but
        // what a switched-off Club module refuses is refused here too.
        if (!await _modules.IsOnAsync(org, WorkspaceModules.Club, ct)) return DomainErrors.Modules.Off(WorkspaceModules.Club);
        var member = await _db.ClubMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == invitation.MemberId && m.OrganizationId == org, ct);
        if (member == null || member.ErasedAtUtc != null) return LinkNotFound;
        var ev = await EventAsync(org, invitation.EventId, ct);
        if (ev == null) return LinkNotFound;
        if (ev.Status == ClubEventStatuses.Cancelled)
            return new Error("Invitation.Cancelled.Conflict", "This event was cancelled, so there is nothing to answer.");
        if (ev.Status == ClubEventStatuses.Completed || DateTime.UtcNow >= ev.StartAtUtc)
            return new Error("Invitation.Started.Conflict", "This event has started, so replies to its invitation are closed.");
        // Taken back to a draft after the invitations went (being rescheduled, say): closed, as the
        // event's page tells staff (Closed), rather than putting members on its list meanwhile.
        if (ev.Status == ClubEventStatuses.Draft)
            return new Error("Invitation.Closed.Conflict", "Replies to this invitation are closed for now.");
        return (invitation, member, ev);
    }

    private static readonly Error NotActive = new("Invitation.NotActive.Conflict",
        "Your membership is not active at the moment, so signing up for events is closed. Please contact the club.");

    private static Error TooLateToCancel(int hours) => new("Invitation.TooLate.Conflict",
        $"Places can be cancelled here up to {hours} hour{(hours == 1 ? "" : "s")} before the event starts. Please contact the club to cancel.");

    /// <summary>Signing up is open to members whose membership is active, as in the member portal.</summary>
    private static bool IsActive(ClubMember m) => string.Equals(m.Status, "Active", StringComparison.OrdinalIgnoreCase);

    /// <summary>The club's cut-off for members cancelling a place themselves (the member portal's setting; 24 hours by default).</summary>
    private async Task<int> CancelHoursAsync(Guid org, CancellationToken ct) =>
        await _db.MemberPortalSettings.AsNoTracking().Where(s => s.OrganizationId == org)
            .Select(s => (int?)s.MemberCancelHours).FirstOrDefaultAsync(ct) ?? DefaultCancelHours;

    /// <summary>The cut-off a club that never set one has: the member portal's default.</summary>
    public const int DefaultCancelHours = 24;

    private static readonly Error LinkNotFound = new("Invitation.NotFound",
        "This link is not valid. It may have been copied incompletely: please use the link in the invitation email.");

    /// <summary>What the page shows: the club, the event, and where the member stands now.</summary>
    private async Task<InvitationPageDto> PageOfAsync(ClubEventInvitation invitation, ClubMember member, ClubEvent ev, CancellationToken ct)
    {
        var org = invitation.OrganizationId!.Value;
        var club = await ClubAsync(org, ct);
        var list = await RegistrationsAsync(org, ev.Id, ct);
        var current = Active(list, member.Id);
        string? place = current?.Status switch
        {
            RegistrationStatuses.Reserved or RegistrationStatuses.CheckedIn => "seat",
            RegistrationStatuses.Waitlisted => "waitlist",
            _ => null,
        };
        int? position = current?.Status == RegistrationStatuses.Waitlisted
            ? list.Where(r => r.Status == RegistrationStatuses.Waitlisted)
                .OrderBy(r => r.RegisteredAtUtc).ThenBy(r => r.CreatedAtUtc).ThenBy(r => r.Id)
                .TakeWhile(r => r.Id != current.Id).Count() + 1
            : null;
        // On the list (staff put them there, or they said yes) is a yes, whatever was answered before it.
        var answer = place != null ? ClubEventInvitationAnswers.Coming
            : invitation.Answer == ClubEventInvitationAnswers.Declined ? ClubEventInvitationAnswers.Declined
            : ClubEventInvitationAnswers.None;
        var first = string.IsNullOrWhiteSpace(member.FirstName) ? $"{member.FirstName} {member.LastName}".Trim() : member.FirstName.Trim();
        return new InvitationPageDto(
            club.Name?.Trim() ?? "", first, AppLanguages.ForMember(member.PreferredLanguage), club.Locale, club.ZoneName,
            ev.Name, ev.Description, ev.Location, ev.StartAtUtc, ev.EndAtUtc, ev.CoverEmoji, ev.MemberPrice, ev.Currency,
            answer, place, position, await CancelHoursAsync(org, ct));
    }

    /// <summary>The member's place on the list that still holds: a seat, the waitlist, or checked in.</summary>
    private static ClubEventRegistration? Active(IEnumerable<ClubEventRegistration> list, Guid memberId) =>
        list.Where(r => r.MemberId == memberId
                && r.Status is RegistrationStatuses.Reserved or RegistrationStatuses.Waitlisted or RegistrationStatuses.CheckedIn)
            .OrderByDescending(r => r.RegisteredAtUtc).ThenByDescending(r => r.Id)
            .FirstOrDefault();

    // ── expected attendance ──────────────────────────────────────────────────

    /// <summary>The show rate the club assumes while it has no history of its own.</summary>
    public const decimal DefaultShowRate = 0.9m;

    public async Task<Result<ExpectedAttendanceDto>> ExpectedAttendanceAsync(Guid organizationId, Guid eventId, CancellationToken ct = default)
    {
        var ev = await EventAsync(organizationId, eventId, ct);
        if (ev == null) return DomainErrors.ClubEvent.NotFound;
        var seated = (await RegistrationsAsync(organizationId, eventId, ct))
            .Where(r => RegistrationStatuses.HoldsSeat(r.Status))
            .ToList();
        var now = DateTime.UtcNow;
        // Each member's past at the club's events: registered and checked in, against registered
        // and did not come. A walk-in never registered, and a cancellation is no answer either way.
        var past = await (from r in _db.ClubEventRegistrations.AsNoTracking()
                          join e in _db.ClubEvents.AsNoTracking() on r.EventId equals e.Id
                          where r.OrganizationId == organizationId && r.MemberId != null && r.EventId != eventId
                              && e.StartAtUtc < now && r.Source != RegistrationSources.WalkIn
                              && (r.Status == RegistrationStatuses.CheckedIn || r.Status == RegistrationStatuses.NoShow)
                          select new { MemberId = r.MemberId!.Value, r.Status })
            .ToListAsync(ct);
        return Estimate(
            seated.Select(r => (r.MemberId, r.Status == RegistrationStatuses.CheckedIn)).ToList(),
            ev.UnnamedSeats,
            past.Select(p => (p.MemberId, p.Status == RegistrationStatuses.CheckedIn)).ToList());
    }

    /// <summary>
    /// The estimate itself: someone checked in counts in full; a member with a seat counts at
    /// their own show rate, or the club's when they have no history; a guest and an unnamed seat
    /// at the club's. The club's rate is all its members' check-ins against their no-shows, or
    /// <see cref="DefaultShowRate"/> while there are none.
    /// </summary>
    /// <param name="seated">Who holds a seat now: the member, when it is one, and whether they have come already.</param>
    /// <param name="history">Each registration of a member at an earlier event that ended in a check-in (true) or a no-show.</param>
    public static ExpectedAttendanceDto Estimate(
        IReadOnlyList<(Guid? MemberId, bool CheckedIn)> seated, int unnamedSeats, IReadOnlyList<(Guid MemberId, bool Came)> history)
    {
        var came = history.Count(h => h.Came);
        var hasHistory = history.Count > 0;
        var clubRate = hasHistory ? (decimal)came / history.Count : DefaultShowRate;
        var rates = history.GroupBy(h => h.MemberId).ToDictionary(g => g.Key, g => (decimal)g.Count(h => h.Came) / g.Count());
        decimal expected = 0;
        int members = 0, withHistory = 0, guests = 0, checkedIn = 0;
        foreach (var (memberId, inAlready) in seated)
        {
            if (memberId != null) members++;
            else guests++;
            if (inAlready)
            {
                checkedIn++;
                expected += 1;
                continue;
            }
            if (memberId is Guid id && rates.TryGetValue(id, out var own))
            {
                withHistory++;
                expected += own;
            }
            else expected += clubRate;
        }
        expected += unnamedSeats * clubRate;
        return new ExpectedAttendanceDto(
            Math.Round(expected, 1, MidpointRounding.AwayFromZero), seated.Count + unnamedSeats, members, withHistory, guests, checkedIn,
            Math.Round(clubRate, 4, MidpointRounding.AwayFromZero), hasHistory);
    }
}
