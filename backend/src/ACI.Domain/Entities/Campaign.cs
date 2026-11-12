namespace ACI.Domain.Entities;

/// <summary>
/// A message for a segment of a club's members, sent by email through the outbox: now, or
/// at a time set for it, by a background job.
/// </summary>
/// <remarks>
/// <para>
/// Campaigns were JSON documents whose "Send" only wrote a log entry per member marked
/// "Sent", and whose planned time nothing acted on. Each member a campaign reaches is now a
/// <see cref="MemberMessage"/> with an outbox message behind it, whose delivery the mail
/// service reports back.
/// </para>
/// <para>
/// Sending starts by claiming the campaign with one conditional update (from Draft or
/// Scheduled to Sending), so two instances, two presses of Send or a retry start it once.
/// From then on it cannot be edited, scheduled or sent again; each member gets at most one
/// message from it, which a unique index on (campaign, member) guarantees even when a
/// sending that died part-way is taken up again.
/// </para>
/// </remarks>
public class Campaign : Common.BaseEntity, Common.IVersioned
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary><see cref="CampaignChannels"/>. Only email is sent; SMS and push are kept for campaigns logged before.</summary>
    public string Channel { get; set; } = CampaignChannels.Email;

    /// <summary>Welcome, Dues, Renewal, Event, Reservation, Loyalty, Announcement, Application or Other.</summary>
    public string Category { get; set; } = "Announcement";

    /// <summary><see cref="ConsentPurposes"/>: marketing reaches only members who agreed to it by email.</summary>
    public string Purpose { get; set; } = ConsentPurposes.Marketing;

    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>Which members it is for, as JSON: {"tiers":[…],"statuses":[…]}; empty lists mean everyone.</summary>
    public string Segment { get; set; } = "{}";

    /// <summary><see cref="CampaignStatuses"/>.</summary>
    public string Status { get; set; } = CampaignStatuses.Draft;

    /// <summary>When the background job sends it; set only while Scheduled.</summary>
    public DateTime? ScheduledAtUtc { get; set; }

    /// <summary>When sending started: the claim that locked it.</summary>
    public DateTime? SendingStartedAtUtc { get; set; }

    /// <summary>When every member it was for had their message queued, or was recorded as not sent to.</summary>
    public DateTime? SentAtUtc { get; set; }

    /// <summary>Who sent it: who pressed Send, or who scheduled it. Replies go to them.</summary>
    public Guid? SentByUserId { get; set; }

    /// <summary>
    /// Until when the sender working through its members holds it. If that sender dies, the
    /// claim runs out and the background job takes it up where it stopped.
    /// </summary>
    public DateTime? ClaimedUntilUtc { get; set; }

    /// <summary>The members it was for: every message, sent or not. For a campaign logged before, the members it was logged for.</summary>
    public int RecipientCount { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>A new value on every save; an edit made from an older read is refused (409).</summary>
    public Guid Version { get; set; }
}

/// <summary>Where a campaign stands.</summary>
public static class CampaignStatuses
{
    /// <summary>Being written; editable, deletable, sendable.</summary>
    public const string Draft = "Draft";

    /// <summary>Waiting for <see cref="Campaign.ScheduledAtUtc"/>; editable until it starts.</summary>
    public const string Scheduled = "Scheduled";

    /// <summary>Claimed: its members' messages are being queued. Locked.</summary>
    public const string Sending = "Sending";

    /// <summary>Every member it was for has a message; the outbox delivers them. Locked.</summary>
    public const string Sent = "Sent";

    /// <summary>
    /// Recorded as sent before Cadence could deliver campaigns: a log entry per member, and
    /// nobody received it. Kept as it was, and locked.
    /// </summary>
    public const string Logged = "Logged";

    /// <summary>Whether it can still be changed: nothing of it has been sent.</summary>
    public static bool IsEditable(string status) => status is Draft or Scheduled;
}

/// <summary>How a member message reaches them.</summary>
public static class CampaignChannels
{
    public const string Email = "Email";

    /// <summary>Not available: Cadence has no SMS provider. Only on messages logged by hand or before.</summary>
    public const string Sms = "SMS";

    /// <summary>Not available: Cadence has no push service. Only on messages logged by hand or before.</summary>
    public const string Push = "Push";

    public static readonly IReadOnlyList<string> All = [Email, Sms, Push];
}

/// <summary>
/// One message to one club member: a campaign's, one staff wrote, or one staff logged that
/// went out some other way. The member's communications log, and a campaign's recipients.
/// </summary>
/// <remarks>
/// What happened to it after it was queued is the outbox's to say (sent, delivered,
/// bounced, opened), read live from <see cref="OutboundMessageId"/>; this row says who it
/// was for, what it said, and why it was not sent when it was not.
/// </remarks>
public class MemberMessage : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }
    public Guid MemberId { get; set; }

    /// <summary>The member's name when it was written, so the log still reads after a member is removed.</summary>
    public string MemberName { get; set; } = string.Empty;

    /// <summary>The address it was for, when it was an email.</summary>
    public string? Email { get; set; }

    /// <summary>The campaign it belongs to, if any; one message per member per campaign.</summary>
    public Guid? CampaignId { get; set; }

    /// <summary>The campaign's name when it was sent (or the name a logged message gave), so the log reads on its own.</summary>
    public string? CampaignName { get; set; }

    /// <summary><see cref="CampaignChannels"/>.</summary>
    public string Channel { get; set; } = CampaignChannels.Email;

    public string Category { get; set; } = "Other";

    /// <summary><see cref="ConsentPurposes"/>; null for a message logged by hand or before Cadence sent anything.</summary>
    public string? Purpose { get; set; }

    public string Subject { get; set; } = string.Empty;

    /// <summary>The start of what it said.</summary>
    public string? Preview { get; set; }

    /// <summary><see cref="MemberMessageStatuses"/>: whether it went to the outbox, and if not, why.</summary>
    public string Status { get; set; } = MemberMessageStatuses.Queued;

    /// <summary>Why it was not sent, in words fit to show staff.</summary>
    public string? Reason { get; set; }

    /// <summary>The outbox message carrying it, when it was handed to the outbox.</summary>
    public Guid? OutboundMessageId { get; set; }

    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>What happened to a member message when it was written.</summary>
public static class MemberMessageStatuses
{
    /// <summary>Handed to the outbox; the outbox's message says what happened next.</summary>
    public const string Queued = "queued";

    /// <summary>Not sent: the member has no usable address, or another member at it already gets this campaign.</summary>
    public const string Skipped = "skipped";

    /// <summary>Not sent: no consent to marketing by email, an unsubscribe, do not contact or the suppression list.</summary>
    public const string Suppressed = "suppressed";

    /// <summary>Recorded by staff (or by Cadence before it could send): it went out some other way, or not at all.</summary>
    public const string Logged = "logged";
}
