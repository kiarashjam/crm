using System.Text.Json.Serialization;
using ACI.Application.Common;

namespace ACI.Application.Services;

/// <summary>
/// The club's bookable resources (a dining room, a meeting room, a court, the spa) and their
/// bookings, with conflicts and capacity checked by the server.
/// </summary>
/// <remarks>
/// <para>
/// Bookings used to be documents the browser checked, if at all: two front desks could book
/// the one spa suite for the same hour, a party of 14 went into a room for 10, and a booking
/// found its member by matching email text. Here every booking change takes its resource's
/// row first, so two bookings of one slot are decided one after the other, and the second is
/// refused with a 409 that names the booking it would clash with.
/// </para>
/// <list type="bullet">
/// <item><b>Conflicts.</b> A resource booked <c>Exclusive</c> takes one booking at a time; one
/// booked <c>Shared</c> takes bookings until the people in them reach its capacity at any
/// moment. A party bigger than the resource is refused either way. Pending, confirmed and
/// checked-in bookings hold their time; cancelled, completed and no-show ones do not.</item>
/// <item><b>Members.</b> A booking is for a member by id, whose name and email come from their
/// record, or for a guest by name.</item>
/// <item><b>Deposits.</b> A resource can ask a deposit. On booking it is charged to the
/// member's house account, or recorded as paid at the desk (a charge and its payment, which
/// takes the money permission), or waived with a reason (which does too). The deposit secures
/// the booking: it is given back (its charge taken back by the ledger's rules, anything paid
/// for it house credit again) when the booking is checked in, completed or cancelled, and kept
/// when the member does not come.</item>
/// <item><b>No-show fees.</b> Marking a booking a no-show posts the resource's no-show fee to
/// the member's account; checking them in after all takes it back.</item>
/// </list>
/// </remarks>
public interface IClubReservations
{
    Task<IReadOnlyList<ClubResourceDto>> ResourcesAsync(Guid? organizationId, CancellationToken ct = default);
    Task<Result<ClubResourceDto>> CreateResourceAsync(Guid? organizationId, ResourceWriteRequest request, CancellationToken ct = default);
    Task<Result<ClubResourceDto>> UpdateResourceAsync(Guid? organizationId, Guid resourceId, ResourceWriteRequest request, Guid? expectedVersion, CancellationToken ct = default);

    /// <summary>Deletes a resource no booking names; one with bookings is made not bookable instead (409).</summary>
    Task<Result> DeleteResourceAsync(Guid? organizationId, Guid resourceId, CancellationToken ct = default);

    /// <summary>
    /// The usual spaces of a club (dining rooms, a boardroom, the spa suite, a court, a studio,
    /// a lounge), for a workspace that has none yet; a workspace with resources is left as it is.
    /// </summary>
    Task<IReadOnlyList<ClubResourceDto>> AddUsualResourcesAsync(Guid? organizationId, CancellationToken ct = default);

    /// <summary>Bookings, soonest first: all, one member's, or those overlapping a period.</summary>
    Task<IReadOnlyList<ReservationDto>> ReservationsAsync(Guid? organizationId, ReservationQuery query, CancellationToken ct = default);

    /// <summary>Books a resource; a clash with another booking, or more people than it holds, is refused (409).</summary>
    /// <param name="mayHandleMoney">Whether the caller may record the deposit as paid or waive it (money.manage).</param>
    Task<Result<ReservationDto>> CreateAsync(Guid? organizationId, ReservationWriteRequest request, bool mayHandleMoney, CancellationToken ct = default);

    /// <summary>Changes the fields sent (one left out is kept, empty notes clear them), the status with what follows from it included.</summary>
    Task<Result<ReservationDto>> UpdateAsync(Guid? organizationId, Guid reservationId, ReservationWriteRequest request, Guid? expectedVersion, CancellationToken ct = default);

    /// <summary>Deletes a booking with no money on the ledger; one with a deposit or a fee is cancelled instead (409).</summary>
    Task<Result> DeleteAsync(Guid? organizationId, Guid reservationId, CancellationToken ct = default);

    /// <summary>
    /// Holds <paramref name="resourceIds"/> for a private event from <paramref name="startUtc"/> to
    /// <paramref name="endUtc"/>, each whole (the hold takes its full capacity), as bookings of its
    /// own in <paramref name="status"/> (Pending while it is an enquiry, Confirmed once agreed). The
    /// event's holds are made to match in one step: new spaces held, held ones moved, dropped ones
    /// released. A clash with another booking refuses the whole change (409, naming the booking in
    /// the way) and leaves the event's holds as they were.
    /// </summary>
    Task<Result<IReadOnlyList<ReservationDto>>> HoldForPrivateEventAsync(Guid? organizationId, Guid privateEventId, string label,
        IReadOnlyList<Guid> resourceIds, DateTime startUtc, DateTime endUtc, string status, CancellationToken ct = default);

    /// <summary>Lets go of a private event's holds: cancelled with the event, or completed with it (<paramref name="status"/>).</summary>
    Task<Result> ReleasePrivateEventAsync(Guid? organizationId, Guid privateEventId, string status, CancellationToken ct = default);
}

public sealed class ReservationQuery
{
    public Guid? MemberId { get; set; }

    /// <summary>Bookings that end after this…</summary>
    public DateTime? FromUtc { get; set; }

    /// <summary>…and start before this.</summary>
    public DateTime? ToUtc { get; set; }
}

/// <summary>A resource to add, or the fields of one to change: a field left out is kept.</summary>
public sealed class ResourceWriteRequest
{
    public string? Name { get; set; }
    public string? Kind { get; set; }
    public int? Capacity { get; set; }

    /// <summary>Empty text clears it.</summary>
    public string? Description { get; set; }

    /// <summary>Exclusive or Shared; when a new resource leaves it out, what its kind usually is.</summary>
    public string? BookingMode { get; set; }

    public decimal? DepositAmount { get; set; }
    public decimal? NoShowFee { get; set; }
    public bool? Active { get; set; }

    /// <summary>off, request (members ask, staff confirm) or instant: whether members book it in the member portal.</summary>
    public string? MemberBooking { get; set; }

    public int? SortOrder { get; set; }
    public Guid? Version { get; set; }
}

/// <summary>A booking to make, or the fields of one to change: a field left out is kept.</summary>
public sealed class ReservationWriteRequest
{
    public Guid? ResourceId { get; set; }

    /// <summary>The member it is for. Leave it out and give <see cref="MemberName"/> for a guest.</summary>
    public Guid? MemberId { get; set; }

    /// <summary>A guest's name; for a member the record's name is used.</summary>
    public string? MemberName { get; set; }

    public string? MemberEmail { get; set; }

    /// <summary>On an edit: true makes the booking a guest's (no member), keeping the name and email sent.</summary>
    public bool? Guest { get; set; }

    public int? PartySize { get; set; }
    public string? StartAtUtc { get; set; }
    public string? EndAtUtc { get; set; }
    public string? Status { get; set; }

    /// <summary>Empty text clears them.</summary>
    public string? Notes { get; set; }

    /// <summary>
    /// When the resource asks a deposit: "charge" (to the member's house account, what a booking
    /// does when this is left out), "paid" (taken at the desk, with <see cref="DepositMethod"/>),
    /// or "waive" (with <see cref="DepositReason"/>). Only when the booking is made.
    /// </summary>
    public string? Deposit { get; set; }

    public string? DepositMethod { get; set; }
    public string? DepositReference { get; set; }
    public string? DepositReason { get; set; }
    public Guid? Version { get; set; }
}

public sealed record ClubResourceDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public int Capacity { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Description { get; init; }
    public string BookingMode { get; init; } = string.Empty;
    public decimal DepositAmount { get; init; }
    public decimal NoShowFee { get; init; }
    public string Currency { get; init; } = string.Empty;
    public bool Active { get; init; }

    /// <summary>off, request or instant: whether members book it in the member portal (MemberBookingModes).</summary>
    public string MemberBooking { get; init; } = "off";

    public int SortOrder { get; init; }

    /// <summary>The version to edit the resource's settings from (If-Match); a booking does not change it.</summary>
    public Guid Version { get; init; }
}

public sealed record ReservationDto
{
    public Guid Id { get; init; }
    public Guid ResourceId { get; init; }
    public string ResourceName { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? MemberId { get; init; }
    public string MemberName { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? MemberEmail { get; init; }
    public int PartySize { get; init; }
    public DateTime StartAtUtc { get; init; }
    public DateTime EndAtUtc { get; init; }
    public string Status { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Notes { get; init; }
    public string Currency { get; init; } = string.Empty;

    /// <summary>The deposit asked when it was booked; 0 when none was.</summary>
    public decimal DepositAmount { get; init; }

    /// <summary>
    /// none (no deposit asked); charged (on the member's account, not yet paid); paid; waived;
    /// released (given back: the booking was kept or cancelled); kept (the member did not come).
    /// </summary>
    public string DepositState { get; init; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? DepositChargeId { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? DepositWaivedReason { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? NoShowFee { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? NoShowChargeId { get; init; }

    /// <summary>charged or reversed (they came after all); left out when no fee was posted.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? NoShowFeeState { get; init; }

    /// <summary>For a space a private event holds: the event, where the hold is changed.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? PrivateEventId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CreatedByName { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public Guid Version { get; init; }
}
