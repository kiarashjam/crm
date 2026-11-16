namespace ACI.Domain.Entities;

// Club events and the club's bookable resources, in typed tables.
//
// They used to be JSON documents (ClubDocuments of kind events, event-attendees, resources
// and reservations) whose rules ran in the browser: capacity was checked against whatever
// one screen had read, two people could take the last seat, two front desks could book the
// one spa suite for the same hour, a booking found its member by matching email text, and
// nothing was ever charged for a ticket. Here the server keeps the rules.
//
// An event's row is what every change to its guest list takes first (its Version moves on
// each one), so two registrations for the last seat are decided one after the other: one
// gets the seat, the other the waitlist. A resource's row does the same for its bookings,
// so two bookings of one slot cannot both be saved. Money (tickets, deposits, no-show fees)
// is posted to the member's house account through the ledger (IClubLedger), never here.

/// <summary>Where an event stands.</summary>
public static class ClubEventStatuses
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Cancelled = "Cancelled";
    public const string Completed = "Completed";
    public static readonly string[] All = [Draft, Published, Cancelled, Completed];
}

/// <summary>Who an event is for.</summary>
public static class ClubEventVisibilities
{
    public const string MembersOnly = "MembersOnly";
    public const string MembersAndGuests = "MembersAndGuests";
    public const string Public = "Public";
    public static readonly string[] All = [MembersOnly, MembersAndGuests, Public];
}

/// <summary>Where a person on an event's list stands.</summary>
public static class RegistrationStatuses
{
    /// <summary>Has a seat and is expected at the door.</summary>
    public const string Reserved = "Reserved";

    /// <summary>Came, and was checked in at the door.</summary>
    public const string CheckedIn = "CheckedIn";

    /// <summary>Waiting for a seat; the first in line gets one when someone cancels.</summary>
    public const string Waitlisted = "Waitlisted";

    public const string Cancelled = "Cancelled";

    /// <summary>Had a seat and did not come.</summary>
    public const string NoShow = "NoShow";

    public static readonly string[] All = [Reserved, CheckedIn, Waitlisted, Cancelled, NoShow];

    /// <summary>Whether a registration in this status takes one of the event's seats.</summary>
    public static bool HoldsSeat(string status) => status is Reserved or CheckedIn;
}

/// <summary>The ticket a person comes on.</summary>
public static class TicketTiers
{
    public const string Member = "Member";
    public const string Guest = "Guest";
    public static readonly string[] All = [Member, Guest];
}

/// <summary>How a person came onto an event's list.</summary>
public static class RegistrationSources
{
    /// <summary>Registered ahead, by staff.</summary>
    public const string Registered = "Registered";

    /// <summary>Came to the door without a registration and was checked in there.</summary>
    public const string WalkIn = "WalkIn";

    /// <summary>Copied from the attendee documents when the list moved to these tables; nothing was ever charged for it.</summary>
    public const string Moved = "Moved";

    /// <summary>Uploaded from a list one browser kept before the list moved to the server; nothing was charged for it either.</summary>
    public const string Uploaded = "Uploaded";
}

/// <summary>What a bookable resource is.</summary>
public static class ResourceKinds
{
    public static readonly string[] All = ["Dining", "Meeting", "Spa", "Court", "Studio", "Lounge"];
}

/// <summary>How a resource is shared between bookings at the same time.</summary>
public static class BookingModes
{
    /// <summary>One booking at a time, whatever its size: a meeting room, a court, a spa suite.</summary>
    public const string Exclusive = "Exclusive";

    /// <summary>Bookings share it up to its capacity, counted in people: a dining room, a lounge, a studio class.</summary>
    public const string Shared = "Shared";

    public static readonly string[] All = [Exclusive, Shared];

    /// <summary>What a resource of this kind is, unless someone says otherwise.</summary>
    public static string For(string kind) => kind is "Dining" or "Lounge" or "Studio" ? Shared : Exclusive;
}

/// <summary>Where a booking stands.</summary>
public static class ReservationStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string CheckedIn = "CheckedIn";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
    public const string NoShow = "NoShow";
    public static readonly string[] All = [Pending, Confirmed, CheckedIn, Completed, Cancelled, NoShow];

    /// <summary>Whether a booking in this status holds its resource for its time.</summary>
    public static bool HoldsResource(string status) => status is Pending or Confirmed or CheckedIn;

    /// <summary>The statuses that hold a resource, for a query to match on.</summary>
    public static readonly string[] Holding = [Pending, Confirmed, CheckedIn];
}

/// <summary>A club event: a dinner, a tasting, a class, a gala.</summary>
public class ClubEvent : Common.BaseEntity, Common.IVersioned
{
    public Guid? OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Location { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }

    /// <summary>Seats; 0 is no limit.</summary>
    public int Capacity { get; set; }

    public decimal MemberPrice { get; set; }
    public decimal GuestPrice { get; set; }

    /// <summary>The workspace's currency when the event was made: what its prices are in.</summary>
    public string Currency { get; set; } = "CHF";

    /// <summary>Posted to the member's account when staff mark them a no-show; 0 is none.</summary>
    public decimal NoShowFee { get; set; }

    public string Visibility { get; set; } = ClubEventVisibilities.MembersAndGuests;
    public string Status { get; set; } = ClubEventStatuses.Draft;
    public string CoverEmoji { get; set; } = "🎉";

    /// <summary>
    /// Seats taken by RSVPs from the events list before names were kept: the documents
    /// counted them and named nobody. They still take seats, so the event is not sold
    /// past its capacity; staff release them when they know those people are not coming.
    /// </summary>
    public int UnnamedSeats { get; set; }

    /// <summary>The same for the waitlist: counted, never named, so nobody to promote or tell.</summary>
    public int UnnamedWaitlist { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Moved by every change to the guest list, which takes this row first (see <see cref="Version"/>).</summary>
    public DateTime? ListChangedAtUtc { get; set; }

    /// <summary>
    /// The version a screen edits the event's own details from (If-Match): new on each such
    /// edit, and not when someone registers, so a guest list that is busy does not refuse
    /// the events manager's change of the description.
    /// </summary>
    public Guid EditVersion { get; set; }

    /// <summary>
    /// New on every save of the row, a registration included: what makes two changes to the
    /// guest list at the same moment one after the other (AppDbContext stamps it).
    /// </summary>
    public Guid Version { get; set; }
}

/// <summary>A person on an event's list: a member, or a guest by name.</summary>
public class ClubEventRegistration : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid EventId { get; set; }

    /// <summary>The member on the ticket; none for a guest.</summary>
    public Guid? MemberId { get; set; }

    /// <summary>The name on the ticket: the member's when they registered, or the guest's.</summary>
    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }

    /// <summary>Member or Guest (see <see cref="TicketTiers"/>).</summary>
    public string TicketTier { get; set; } = TicketTiers.Member;

    /// <summary>For a guest: the member who brings them, whose account the guest ticket goes on.</summary>
    public Guid? HostMemberId { get; set; }
    public string? HostName { get; set; }

    /// <summary>See <see cref="RegistrationStatuses"/>.</summary>
    public string Status { get; set; } = RegistrationStatuses.Reserved;

    /// <summary>See <see cref="RegistrationSources"/>.</summary>
    public string Source { get; set; } = RegistrationSources.Registered;

    /// <summary>When they registered or joined the waitlist: the waitlist is served in this order.</summary>
    public DateTime RegisteredAtUtc { get; set; }

    public DateTime? PromotedAtUtc { get; set; }
    public DateTime? CheckedInAtUtc { get; set; }
    public Guid? CheckedInByUserId { get; set; }
    public string? CheckedInByName { get; set; }
    public DateTime? NoShowAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public string? CancelledByName { get; set; }
    public string? CancelReason { get; set; }

    /// <summary>The ticket's price when it was charged (or would have been): the event's member or guest price.</summary>
    public decimal TicketPrice { get; set; }

    public string Currency { get; set; } = "CHF";

    /// <summary>The ticket's charge on the house account (the member's, or the host's for a guest).</summary>
    public Guid? ChargeId { get; set; }

    /// <summary>When the ticket's charge was taken back, by a cancellation.</summary>
    public DateTime? ChargeReversedAtUtc { get; set; }

    /// <summary>The no-show fee posted when staff marked them a no-show, and its charge.</summary>
    public decimal? NoShowFee { get; set; }
    public Guid? NoShowChargeId { get; set; }
    public DateTime? NoShowFeeReversedAtUtc { get; set; }

    /// <summary>The points the check-in earned, once, and their entry in the loyalty ledger.</summary>
    public int? PointsAwarded { get; set; }
    public Guid? LoyaltyEntryId { get; set; }

    /// <summary>For an uploaded row: the browser's own id for it, so an upload retried after a dropped connection adds it once.</summary>
    public string? ImportedFromId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>Something members book for a time: a dining room, a meeting room, a court, the spa.</summary>
public class ClubResource : Common.BaseEntity, Common.IVersioned
{
    public Guid? OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Dining, Meeting, Spa, Court, Studio or Lounge.</summary>
    public string Kind { get; set; } = "Dining";

    /// <summary>People it holds at once.</summary>
    public int Capacity { get; set; }

    public string? Description { get; set; }

    /// <summary>Exclusive or Shared (see <see cref="BookingModes"/>).</summary>
    public string BookingMode { get; set; } = BookingModes.Exclusive;

    /// <summary>Asked of a member when they book; 0 is none.</summary>
    public decimal DepositAmount { get; set; }

    /// <summary>Posted to the member's account when staff mark a booking a no-show; 0 is none.</summary>
    public decimal NoShowFee { get; set; }

    public string Currency { get; set; } = "CHF";

    /// <summary>Whether it can be booked. One with bookings is set inactive rather than deleted.</summary>
    public bool Active { get; set; } = true;

    /// <summary>
    /// Whether members book it themselves in the member portal, and whether such a booking is
    /// confirmed at once or waits for staff (<see cref="MemberBookingModes"/>). Off until the
    /// club says otherwise: not every room is one a member should take without asking.
    /// </summary>
    public string MemberBooking { get; set; } = MemberBookingModes.Off;

    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Moved by every change to its bookings, which takes this row first (see <see cref="Version"/>).</summary>
    public DateTime? BookingsChangedAtUtc { get; set; }

    /// <summary>The version a screen edits the resource's settings from (If-Match): a booking does not change it.</summary>
    public Guid EditVersion { get; set; }

    /// <summary>New on every save of the row, a booking included: two bookings of one slot are decided one after the other.</summary>
    public Guid Version { get; set; }
}

/// <summary>A booking of a resource for a time.</summary>
public class ClubReservation : Common.BaseEntity, Common.IVersioned
{
    public Guid? OrganizationId { get; set; }

    /// <summary>The resource booked. Not a foreign key: a booking moved from a document whose resource had gone keeps it, under its old name.</summary>
    public Guid ResourceId { get; set; }

    /// <summary>The resource's name when it was booked, so the booking still reads after a rename.</summary>
    public string ResourceName { get; set; } = string.Empty;

    /// <summary>The member it is for; none for a guest booked by name.</summary>
    public Guid? MemberId { get; set; }

    public string MemberName { get; set; } = string.Empty;
    public string? MemberEmail { get; set; }
    public int PartySize { get; set; } = 1;
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }

    /// <summary>See <see cref="ReservationStatuses"/>.</summary>
    public string Status { get; set; } = ReservationStatuses.Pending;

    public string? Notes { get; set; }

    /// <summary>The deposit asked when it was booked (the resource's then); 0 is none.</summary>
    public decimal DepositAmount { get; set; }

    public string Currency { get; set; } = "CHF";

    /// <summary>The deposit's charge on the member's account, and the payment that paid it when it was paid at the desk.</summary>
    public Guid? DepositChargeId { get; set; }
    public Guid? DepositPaymentId { get; set; }

    /// <summary>When the deposit was given back: the booking was kept (checked in or completed) or cancelled.</summary>
    public DateTime? DepositReleasedAtUtc { get; set; }

    /// <summary>A deposit someone allowed to handle money let go, and why.</summary>
    public bool DepositWaived { get; set; }
    public string? DepositWaivedReason { get; set; }
    public string? DepositWaivedByName { get; set; }

    /// <summary>The no-show fee posted when staff marked it a no-show, and its charge.</summary>
    public decimal? NoShowFee { get; set; }
    public Guid? NoShowChargeId { get; set; }
    public DateTime? NoShowFeeReversedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }
    public string? CancelledByName { get; set; }

    /// <summary>
    /// For a space a private event holds: the event (ClubPrivateEvent). Such a booking takes the
    /// whole resource for the event's time, and it is moved, released and kept by the event, not
    /// changed from the bookings page.
    /// </summary>
    public Guid? PrivateEventId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid Version { get; set; }
}
