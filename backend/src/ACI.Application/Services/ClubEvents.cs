using System.Text.Json.Serialization;
using ACI.Application.Common;

namespace ACI.Application.Services;

/// <summary>
/// Club events and who is coming to them: registrations with the capacity and the waitlist
/// kept by the server, ticket charges on the ledger, and the door.
/// </summary>
/// <remarks>
/// <para>
/// The guest list used to be rules in the browser over documents: capacity was checked
/// against whatever one screen had read, two people could take the last seat, a quick RSVP
/// named nobody, a cancellation moved nobody off the waitlist, and no ticket was ever
/// charged. Here every change to an event's list takes the event's row first, so changes to
/// one list are decided one after the other: of two people taking the last seat, one gets it
/// and the other the waitlist.
/// </para>
/// <list type="bullet">
/// <item><b>Seats.</b> Reserved and checked-in people take seats, and so do the unnamed RSVPs
/// the documents counted before names were kept (until staff release them). A capacity of 0 is
/// no limit.</item>
/// <item><b>The waitlist</b> is served in the order people joined it. When a seat comes free
/// before the event starts (a cancellation, a removal, more capacity, unnamed seats released)
/// the first in line gets it at once and is told through the outbox.</item>
/// <item><b>Tickets.</b> A seat with a price posts its ticket to a house account through the
/// ledger: the member's, or for a guest the member who brings them (Idempotency-Key
/// "ticket:{registrationId}"). A guest nobody brings is noted as to collect at the door. A
/// cancellation takes the ticket back by the ledger's rules: a void entry, and anything
/// already paid for it is house credit again (<see cref="IClubLedger.CancelChargeAsync"/>).
/// Prices stay on the event; a price catalogue can feed them later.</item>
/// <item><b>The door.</b> A check-in is refused when it already happened, so the member's
/// event points are given once, on the server. A walk-in is checked in at once when a seat is
/// free. A no-show (once the event has started) frees the seat, keeps the ticket, and posts the
/// event's no-show fee when it has one.</item>
/// </list>
/// </remarks>
public interface IClubEvents
{
    Task<IReadOnlyList<ClubEventDto>> ListAsync(Guid? organizationId, CancellationToken ct = default);
    Task<Result<ClubEventDto>> GetAsync(Guid? organizationId, Guid eventId, CancellationToken ct = default);
    Task<Result<ClubEventDto>> CreateAsync(Guid? organizationId, ClubEventWriteRequest request, CancellationToken ct = default);

    /// <summary>
    /// Changes the fields sent; one left out is kept, and empty text clears it. Cancelling the
    /// event cancels everyone on its list and takes their tickets back; more seats (or unnamed
    /// ones released) go to the waitlist.
    /// </summary>
    Task<Result<ClubEventDto>> UpdateAsync(Guid? organizationId, Guid eventId, ClubEventWriteRequest request, Guid? expectedVersion, CancellationToken ct = default);

    /// <summary>Deletes an event and its list; one with a ticket or a fee on the ledger is cancelled instead (409).</summary>
    Task<Result> DeleteAsync(Guid? organizationId, Guid eventId, CancellationToken ct = default);

    Task<Result<IReadOnlyList<EventRegistrationDto>>> RegistrationsAsync(Guid? organizationId, Guid eventId, CancellationToken ct = default);

    /// <summary>Every registration of one member (on their ticket, or as the host of a guest), newest event first.</summary>
    Task<IReadOnlyList<EventRegistrationDto>> MemberRegistrationsAsync(Guid? organizationId, Guid memberId, CancellationToken ct = default);

    /// <summary>Puts a member or a guest on the list: a seat while there is one, else the waitlist.</summary>
    Task<Result<EventRegistrationDto>> RegisterAsync(Guid? organizationId, Guid eventId, RegisterForEventRequest request, CancellationToken ct = default);

    /// <summary>Someone at the door with no registration: checked in at once, while a seat is free.</summary>
    Task<Result<EventRegistrationDto>> WalkInAsync(Guid? organizationId, Guid eventId, RegisterForEventRequest request, CancellationToken ct = default);

    /// <summary>Check in, cancel, take off the waitlist (Reserved) or mark a no-show.</summary>
    Task<Result<EventRegistrationDto>> SetStatusAsync(Guid? organizationId, Guid eventId, Guid registrationId, RegistrationStatusRequest request, CancellationToken ct = default);

    /// <summary>Takes someone off the list entirely; one whose ticket or fee is on the ledger, or who came, is cancelled instead (409).</summary>
    Task<Result> RemoveAsync(Guid? organizationId, Guid eventId, Guid registrationId, CancellationToken ct = default);

    /// <summary>
    /// Brings over someone a browser kept on its own list before the list moved to the server,
    /// as they were, charged nothing; an upload retried finds the copy it made.
    /// </summary>
    Task<Result<EventRegistrationDto>> ImportAsync(Guid? organizationId, Guid eventId, ImportRegistrationRequest request, CancellationToken ct = default);
}

// ── requests ──────────────────────────────────────────────────────────────────
// Dates arrive as text, as the screens send them; an amount or a count left out is kept.

/// <summary>An event to add, or the fields of one to change: a field left out is kept, and empty text clears it.</summary>
public sealed class ClubEventWriteRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Location { get; set; }
    public string? StartAtUtc { get; set; }
    public string? EndAtUtc { get; set; }
    public int? Capacity { get; set; }
    public decimal? MemberPrice { get; set; }
    public decimal? GuestPrice { get; set; }
    public decimal? NoShowFee { get; set; }
    public string? Visibility { get; set; }
    public string? Status { get; set; }
    public string? CoverEmoji { get; set; }

    /// <summary>Unnamed seats to keep: lower to release them, never higher.</summary>
    public int? UnnamedSeats { get; set; }

    /// <summary>Unnamed waitlist places to keep: lower to release them, never higher.</summary>
    public int? UnnamedWaitlist { get; set; }

    public Guid? Version { get; set; }
}

/// <summary>Who to put on an event's list: a member by id, or a guest by name (and the member who brings them).</summary>
public sealed class RegisterForEventRequest
{
    public Guid? MemberId { get; set; }

    /// <summary>A guest's name. For a member, their name comes from the member record.</summary>
    public string? MemberName { get; set; }

    public string? Email { get; set; }

    /// <summary>Member or Guest; Member when left out.</summary>
    public string? TicketTier { get; set; }

    /// <summary>For a guest: the member who brings them, whose house account the guest ticket goes on.</summary>
    public Guid? HostMemberId { get; set; }
}

public sealed class RegistrationStatusRequest
{
    /// <summary>CheckedIn, Cancelled, Reserved (off the waitlist) or NoShow.</summary>
    public string? Status { get; set; }

    /// <summary>For a cancellation: why, kept on the registration and the ticket's void.</summary>
    public string? Reason { get; set; }
}

public sealed class ImportRegistrationRequest
{
    /// <summary>The browser's own id for the row: an upload retried after a dropped connection finds the copy it made.</summary>
    public string? LocalId { get; set; }

    public Guid? MemberId { get; set; }
    public string? MemberName { get; set; }
    public string? Email { get; set; }
    public string? TicketTier { get; set; }
    public string? Status { get; set; }
    public string? RsvpAtUtc { get; set; }
    public string? CheckedInAtUtc { get; set; }
}

// ── answers ───────────────────────────────────────────────────────────────────
// The shapes the events pages have always read (src/app/api/events.ts, eventAttendees.ts),
// with what the server now keeps beside them. What a record does not have is left out.

public sealed record ClubEventDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Description { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Location { get; init; }
    public DateTime StartAtUtc { get; init; }
    public DateTime EndAtUtc { get; init; }

    /// <summary>Seats; 0 is no limit.</summary>
    public int Capacity { get; init; }

    /// <summary>Seats taken: reserved and checked in, and the unnamed seats.</summary>
    public int RegisteredCount { get; init; }

    /// <summary>On the waitlist, named and unnamed.</summary>
    public int WaitlistCount { get; init; }

    public int CheckedInCount { get; init; }
    public int NoShowCount { get; init; }
    public int WalkInCount { get; init; }
    public int UnnamedSeats { get; init; }
    public int UnnamedWaitlist { get; init; }

    /// <summary>Seats still free; left out when there is no limit.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? SeatsLeft { get; init; }

    public decimal MemberPrice { get; init; }
    public decimal GuestPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal NoShowFee { get; init; }

    /// <summary>What the tickets on house accounts come to: posted and not taken back.</summary>
    public decimal TicketRevenue { get; init; }

    public string Visibility { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string CoverEmoji { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }

    /// <summary>The version to edit the event's details from (If-Match); a registration does not change it.</summary>
    public Guid Version { get; init; }
}

/// <summary>Someone on an event's list.</summary>
public sealed record EventRegistrationDto
{
    public Guid Id { get; init; }
    public Guid EventId { get; init; }
    public string EventName { get; init; } = string.Empty;
    public DateTime EventStartAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? MemberId { get; init; }

    /// <summary>The name on the ticket.</summary>
    public string MemberName { get; init; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Email { get; init; }
    public string TicketTier { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? HostMemberId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? HostName { get; init; }

    /// <summary>Reserved, CheckedIn, Waitlisted, Cancelled or NoShow.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Registered, WalkIn, Moved or Uploaded.</summary>
    public string Source { get; init; } = string.Empty;

    public bool WalkIn { get; init; }

    /// <summary>When they registered or joined the waitlist.</summary>
    public DateTime RsvpAtUtc { get; init; }

    /// <summary>Their place in the waitlist, 1 first; left out when they are not on it.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? WaitlistPosition { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? PromotedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? CheckedInAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CheckedInByName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? NoShowAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public DateTime? CancelledAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CancelledByName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CancelReason { get; init; }

    public decimal TicketPrice { get; init; }
    public string Currency { get; init; } = string.Empty;

    /// <summary>
    /// The ticket on the ledger: free; charged (on a house account); reversed (taken back by a
    /// cancellation); waiting (charged when a seat comes); to-collect (a guest nobody brings, so
    /// no house account to put it on); not-charged (registered before tickets were charged); none.
    /// </summary>
    public string ChargeState { get; init; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? ChargeId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? NoShowFee { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? NoShowChargeId { get; init; }

    /// <summary>charged, reversed (they came after all), or left out when no fee was posted.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? NoShowFeeState { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? PointsAwarded { get; init; }
}
