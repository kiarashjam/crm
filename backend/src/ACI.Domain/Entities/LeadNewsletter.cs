namespace ACI.Domain.Entities;

/// <summary>
/// One newsletter sent to chosen leads from the Leads page: who chose it, what it said and
/// how many it went to. Each lead's email is an outbox message whose SourceId is this row.
/// </summary>
/// <remarks>
/// Its id is the one the browser made when the person pressed Send, so the same press sent
/// twice (a double click, a retry after a dropped connection) finds this row already there
/// and sends nothing more: the database's key decides, whichever request gets there first.
/// </remarks>
public class LeadNewsletter : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }
    public string Subject { get; set; } = string.Empty;

    /// <summary>"fr" or "en".</summary>
    public string Language { get; set; } = "fr";

    /// <summary>The filters that chose the leads, as JSON (statuses in and out, source, owner, dates).</summary>
    public string AudienceJson { get; set; } = "{}";

    /// <summary>"sending" while the emails are being queued, then "sent".</summary>
    public string Status { get; set; } = LeadNewsletterStatuses.Sending;

    /// <summary>Emails queued, one per lead.</summary>
    public int Recipients { get; set; }

    /// <summary>Leads the filters chose that were not emailed: no address, do not contact, suppressed.</summary>
    public int LeftOut { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
}

public static class LeadNewsletterStatuses
{
    public const string Sending = "sending";
    public const string Sent = "sent";
}

/// <summary>
/// A meeting a lead booked on the workspace's website, as the website reported it to
/// /api/public/meetings: which lead it is on, when it is, and the timeline entry it made.
/// </summary>
/// <remarks>
/// One row per booking the website names (<see cref="BookingId"/>, unique in the workspace),
/// so a report sent again (the website's own retry) changes nothing, and a cancellation finds
/// the meeting it cancels.
/// </remarks>
public class MeetingBooking : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid LeadId { get; set; }

    /// <summary>The website's own id for the booking.</summary>
    public string BookingId { get; set; } = string.Empty;

    /// <summary>The booking code the lead was given ("RDV-7K2Q"), as the website sent it.</summary>
    public string? Code { get; set; }

    public DateTime StartsAtUtc { get; set; }

    /// <summary>The offset the website gave the time in, in minutes (+60 for Swiss winter time), to say it as the lead read it.</summary>
    public int OffsetMinutes { get; set; }

    public int? DurationMinutes { get; set; }

    /// <summary>"booked" or "cancelled".</summary>
    public string Status { get; set; } = MeetingBookingStatuses.Booked;

    /// <summary>The meeting on the lead's timeline.</summary>
    public Guid? ActivityId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public static class MeetingBookingStatuses
{
    public const string Booked = "booked";
    public const string Cancelled = "cancelled";
}
