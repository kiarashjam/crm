using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

/// <summary>
/// Who is coming to a club event: one club document per attendee, kind "event-attendees".
/// </summary>
/// <remarks>
/// The list used to live only in each browser. The events manager registered forty
/// members on a laptop, the door host opened the same event on an iPad and found
/// nobody to check in, and clearing the browser lost the list. Only the counts reached
/// the server, sent as "the count I last read, plus one".
/// <para>
/// Here the server keeps the list and the event's <c>registeredCount</c> and
/// <c>waitlistCount</c> together, in the same request, and decides reserved or
/// waitlisted against the event's capacity the way <c>POST /api/events/{id}/rsvp</c>
/// already does (a capacity of 0 means no limit). The counts are adjusted rather than
/// recounted from the rows, because they also hold RSVPs taken from the events list,
/// which have no name attached.
/// </para>
/// </remarks>
public interface IEventAttendeeService
{
    Task<Result<JsonArray>> ListAsync(Guid? organizationId, Guid eventId, CancellationToken ct = default);
    Task<Result<JsonObject>> AddAsync(Guid? organizationId, Guid eventId, JsonObject body, CancellationToken ct = default);
    Task<Result<JsonObject>> SetStatusAsync(Guid? organizationId, Guid eventId, Guid attendeeId, JsonObject body, CancellationToken ct = default);
    Task<Result> RemoveAsync(Guid? organizationId, Guid eventId, Guid attendeeId, CancellationToken ct = default);

    /// <summary>
    /// Brings over an attendee that a browser kept before the list moved here, as it was,
    /// without counting it again: the browser added it to the event's counts on the
    /// server when it was entered.
    /// </summary>
    Task<Result<JsonObject>> ImportAsync(Guid? organizationId, Guid eventId, JsonObject body, CancellationToken ct = default);
}

public sealed class EventAttendeeService : IEventAttendeeService
{
    private const string EventsKind = "events";
    private const string AttendeesKind = "event-attendees";
    private const int MaxName = 200;
    private const int MaxEmail = 320;
    private const int MaxLocalId = 100;
    private static readonly string[] Statuses = ["Reserved", "CheckedIn", "Waitlisted", "Cancelled"];
    private static readonly string[] Tickets = ["Member", "Guest"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IClubDocumentRepository _docs;
    private readonly IClubRepository _club;

    public EventAttendeeService(IClubDocumentRepository docs, IClubRepository club)
    {
        _docs = docs;
        _club = club;
    }

    public async Task<Result<JsonArray>> ListAsync(Guid? organizationId, Guid eventId, CancellationToken ct = default)
    {
        if (await LoadAsync(organizationId, EventsKind, eventId, ct) == null) return DomainErrors.EventAttendee.EventNotFound;
        var list = new JsonArray();
        foreach (var attendee in (await AttendeesAsync(organizationId, eventId, ct)).OrderBy(a => Text(a, "rsvpAtUtc"), StringComparer.Ordinal))
            list.Add(attendee);
        return list;
    }

    public async Task<Result<JsonObject>> AddAsync(Guid? organizationId, Guid eventId, JsonObject body, CancellationToken ct = default)
    {
        var ev = await LoadAsync(organizationId, EventsKind, eventId, ct);
        if (ev == null) return DomainErrors.EventAttendee.EventNotFound;
        var person = await PersonAsync(organizationId, body, lenient: false, ct);
        if (!person.IsSuccess) return person.Error;

        var registered = Int(ev, "registeredCount");
        var capacity = Int(ev, "capacity");
        var full = capacity > 0 && registered >= capacity;
        var attendee = person.Value;
        attendee["eventId"] = eventId.ToString();
        attendee["status"] = full ? "Waitlisted" : "Reserved";
        attendee["rsvpAtUtc"] = DateTime.UtcNow.ToString("o");
        if (full) ev["waitlistCount"] = Int(ev, "waitlistCount") + 1;
        else ev["registeredCount"] = registered + 1;

        var id = Guid.NewGuid();
        attendee["id"] = id.ToString();
        await SaveAsync(organizationId, AttendeesKind, id, attendee, ct);
        await SaveAsync(organizationId, EventsKind, eventId, ev, ct);
        return attendee;
    }

    public async Task<Result<JsonObject>> SetStatusAsync(Guid? organizationId, Guid eventId, Guid attendeeId, JsonObject body, CancellationToken ct = default)
    {
        var ev = await LoadAsync(organizationId, EventsKind, eventId, ct);
        if (ev == null) return DomainErrors.EventAttendee.EventNotFound;
        var attendee = await LoadAttendeeAsync(organizationId, eventId, attendeeId, ct);
        if (attendee == null) return DomainErrors.EventAttendee.NotFound;
        var next = Text(body, "status");
        if (!Statuses.Contains(next)) return DomainErrors.EventAttendee.StatusInvalid;
        var current = Text(attendee, "status");
        // The page gives a member their event points for every check-in it sees succeed.
        // With the list shared, a second door host's screen can still show someone as
        // reserved after they were checked in, so a repeat is refused, not answered as done.
        if (current == next && next == "CheckedIn") return DomainErrors.EventAttendee.AlreadyCheckedIn;
        if (current == next) return attendee;

        var wasRegistered = IsRegistered(current);
        var willBeRegistered = IsRegistered(next);
        var registered = Int(ev, "registeredCount");
        var capacity = Int(ev, "capacity");
        // Off the waitlist (or back from a cancellation) only while there is a seat.
        if (!wasRegistered && willBeRegistered && capacity > 0 && registered >= capacity)
            return DomainErrors.EventAttendee.EventFull;

        attendee["status"] = next;
        if (next == "CheckedIn") attendee["checkedInAtUtc"] = DateTime.UtcNow.ToString("o");
        else attendee.Remove("checkedInAtUtc");
        var countsChanged = Recount(ev, current, next);

        await SaveAsync(organizationId, AttendeesKind, attendeeId, attendee, ct);
        if (countsChanged) await SaveAsync(organizationId, EventsKind, eventId, ev, ct);
        return attendee;
    }

    public async Task<Result> RemoveAsync(Guid? organizationId, Guid eventId, Guid attendeeId, CancellationToken ct = default)
    {
        var ev = await LoadAsync(organizationId, EventsKind, eventId, ct);
        if (ev == null) return DomainErrors.EventAttendee.EventNotFound;
        var attendee = await LoadAttendeeAsync(organizationId, eventId, attendeeId, ct);
        if (attendee == null) return DomainErrors.EventAttendee.NotFound;
        if (!await _docs.DeleteAsync(attendeeId, organizationId, AttendeesKind, ct)) return DomainErrors.EventAttendee.NotFound;
        if (Recount(ev, Text(attendee, "status"), "Cancelled"))
            await SaveAsync(organizationId, EventsKind, eventId, ev, ct);
        return Result.Success();
    }

    public async Task<Result<JsonObject>> ImportAsync(Guid? organizationId, Guid eventId, JsonObject body, CancellationToken ct = default)
    {
        if (await LoadAsync(organizationId, EventsKind, eventId, ct) == null) return DomainErrors.EventAttendee.EventNotFound;
        var status = Text(body, "status");
        if (status.Length == 0) status = "Reserved";
        if (!Statuses.Contains(status)) return DomainErrors.EventAttendee.StatusInvalid;
        // The browser's own id for the row: an upload retried after a dropped connection
        // finds the copy it already made instead of adding the person twice.
        var localId = Text(body, "localId").Trim();
        if (localId.Length > MaxLocalId) return DomainErrors.EventAttendee.TooLong;
        if (localId.Length > 0)
            foreach (var existing in await AttendeesAsync(organizationId, eventId, ct))
                if (Text(existing, "importedFromId") == localId)
                    return existing;

        var person = await PersonAsync(organizationId, body, lenient: true, ct);
        if (!person.IsSuccess) return person.Error;
        var attendee = person.Value;
        var id = Guid.NewGuid();
        attendee["id"] = id.ToString();
        attendee["eventId"] = eventId.ToString();
        attendee["status"] = status;
        attendee["rsvpAtUtc"] = (Date(body, "rsvpAtUtc") ?? DateTime.UtcNow).ToString("o");
        if (status == "CheckedIn")
            attendee["checkedInAtUtc"] = (Date(body, "checkedInAtUtc") ?? DateTime.UtcNow).ToString("o");
        if (localId.Length > 0) attendee["importedFromId"] = localId;
        await SaveAsync(organizationId, AttendeesKind, id, attendee, ct);
        return attendee;
    }

    /// <summary>
    /// The person on the ticket. A member is looked up in this workspace, and their name
    /// and email come from the member record; a guest needs a name.
    /// </summary>
    /// <param name="lenient">For an import: a member who is no longer here stays on the
    /// list under the name the browser kept, rather than failing the upload.</param>
    private async Task<Result<JsonObject>> PersonAsync(Guid? organizationId, JsonObject body, bool lenient, CancellationToken ct)
    {
        var ticket = Text(body, "ticketTier");
        if (ticket.Length == 0) ticket = "Member";
        if (!Tickets.Contains(ticket)) return DomainErrors.EventAttendee.TicketInvalid;
        var name = Text(body, "memberName").Trim();
        var email = Text(body, "email").Trim();
        var person = new JsonObject { ["ticketTier"] = ticket };

        var memberIdText = Text(body, "memberId");
        if (memberIdText.Length > 0)
        {
            var member = Guid.TryParse(memberIdText, out var memberId)
                ? await _club.MemberAsync(memberId, organizationId, ct)
                : null;
            if (member != null)
            {
                person["memberId"] = member.Id.ToString();
                name = $"{member.FirstName} {member.LastName}".Trim();
                if (!string.IsNullOrWhiteSpace(member.Email)) email = member.Email.Trim();
            }
            else if (!lenient) return DomainErrors.EventAttendee.MemberNotFound;
        }

        if (name.Length == 0) return DomainErrors.EventAttendee.NameRequired;
        if (name.Length > MaxName || email.Length > MaxEmail) return DomainErrors.EventAttendee.TooLong;
        person["memberName"] = name;
        if (email.Length > 0) person["email"] = email;
        return person;
    }

    private static bool IsRegistered(string status) => status is "Reserved" or "CheckedIn";

    /// <summary>Moves the event's counts for one attendee going from one status to another.</summary>
    private static bool Recount(JsonObject ev, string from, string to)
    {
        var registered = Int(ev, "registeredCount");
        var waitlist = Int(ev, "waitlistCount");
        var nextRegistered = registered + (IsRegistered(to) ? 1 : 0) - (IsRegistered(from) ? 1 : 0);
        var nextWaitlist = waitlist + (to == "Waitlisted" ? 1 : 0) - (from == "Waitlisted" ? 1 : 0);
        nextRegistered = Math.Max(0, nextRegistered);
        nextWaitlist = Math.Max(0, nextWaitlist);
        if (nextRegistered == registered && nextWaitlist == waitlist) return false;
        ev["registeredCount"] = nextRegistered;
        ev["waitlistCount"] = nextWaitlist;
        return true;
    }

    private async Task<List<JsonObject>> AttendeesAsync(Guid? organizationId, Guid eventId, CancellationToken ct)
    {
        var id = eventId.ToString();
        var rows = new List<JsonObject>();
        foreach (var row in await _docs.ListAsync(organizationId, AttendeesKind, ct))
            if (Parse(row.Payload) is { } attendee && Text(attendee, "eventId") == id)
                rows.Add(attendee);
        return rows;
    }

    private async Task<JsonObject?> LoadAttendeeAsync(Guid? organizationId, Guid eventId, Guid attendeeId, CancellationToken ct)
    {
        var attendee = await LoadAsync(organizationId, AttendeesKind, attendeeId, ct);
        return attendee != null && Text(attendee, "eventId") == eventId.ToString() ? attendee : null;
    }

    private async Task<JsonObject?> LoadAsync(Guid? organizationId, string kind, Guid id, CancellationToken ct)
    {
        var row = await _docs.GetAsync(id, organizationId, kind, ct);
        return row == null ? null : Parse(row.Payload);
    }

    private Task SaveAsync(Guid? organizationId, string kind, Guid id, JsonObject obj, CancellationToken ct) =>
        _docs.SaveAsync(new ClubDocument
        {
            Id = id,
            OrganizationId = organizationId,
            Kind = kind,
            Payload = obj.ToJsonString(Json),
            UpdatedAtUtc = DateTime.UtcNow,
        }, ct);

    private static JsonObject? Parse(string payload) => JsonNode.Parse(payload) as JsonObject;

    private static string Text(JsonObject obj, string key) => obj[key]?.ToString() ?? "";

    private static int Int(JsonObject obj, string key) =>
        obj[key] is JsonValue value && value.TryGetValue<int>(out var n) ? n : int.TryParse(Text(obj, key), out var p) ? p : 0;

    private static DateTime? Date(JsonObject obj, string key) =>
        DateTime.TryParse(Text(obj, key), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d)
            ? d
            : null;
}
