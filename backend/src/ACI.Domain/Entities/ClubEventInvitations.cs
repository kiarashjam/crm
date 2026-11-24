namespace ACI.Domain.Entities;

// Invitations to a club event: staff choose an audience of members and send each one an email
// with a reply link of their own. The member answers on a public page, "I'll come" (which
// registers them through the event's own rules: a seat while there is one, else the waitlist)
// or "I can't come", and may change their mind until the event starts.

/// <summary>
/// One "Invite members" send for an event: who pressed it, what it said, and how many it went
/// to. Each member's invitation (<see cref="ClubEventInvitation"/>) names it.
/// </summary>
/// <remarks>
/// Its id is the one the browser made when the person pressed Send, so the same press sent
/// twice (a double click, a retry after a dropped connection) finds this row already there and
/// sends nothing more: the database's key decides, whichever request gets there first.
/// </remarks>
public class ClubEventInvitationSend : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid EventId { get; set; }

    /// <summary>The filters that chose the members, as JSON (the audience of campaigns, MemberAudienceFilter).</summary>
    public string AudienceJson { get; set; } = "{}";

    /// <summary>The subject and words of each language's letter, with their placeholders, as JSON.</summary>
    public string LettersJson { get; set; } = "{}";

    /// <summary>"sending" while the emails are being queued, then "sent".</summary>
    public string Status { get; set; } = ClubEventInvitationSendStatuses.Sending;

    /// <summary>Invitations queued, one per member.</summary>
    public int Recipients { get; set; }

    /// <summary>Members the filters chose who were not invited: no address, no consent, suppressed, already invited...</summary>
    public int LeftOut { get; set; }

    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
}

public static class ClubEventInvitationSendStatuses
{
    public const string Sending = "sending";
    public const string Sent = "sent";
}

/// <summary>
/// One member invited to one event: the email that invited them, their reply link (kept only as
/// a hash), and their answer. One per member and event.
/// </summary>
/// <remarks>
/// The reply link's token is random and is never stored, only its SHA-256, so the table holds
/// nothing that answers for a member. It works until the event starts. A reminder carries a
/// second token of its own (<see cref="ReminderTokenHash"/>), so the first email's link still
/// works beside it. An answer of "I'll come" registers the member through the event's own
/// rules (<see cref="RegistrationId"/>); whether they have a seat, are waiting, came or did not
/// is read from that registration.
/// </remarks>
public class ClubEventInvitation : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid EventId { get; set; }
    public Guid MemberId { get; set; }

    /// <summary>The send that invited them.</summary>
    public Guid SendId { get; set; }

    /// <summary>The member's name when they were invited, so the list still reads should their record change.</summary>
    public string MemberName { get; set; } = string.Empty;

    /// <summary>The address the invitation went to.</summary>
    public string? Email { get; set; }

    /// <summary>The language it was written in (en, de, fr): the member's own, which their reply page opens in.</summary>
    public string Language { get; set; } = "en";

    /// <summary>SHA-256 of the reply link's token, lowercase hex.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>SHA-256 of the reminder's own reply link token, once a reminder went.</summary>
    public string? ReminderTokenHash { get; set; }

    /// <summary><see cref="ClubEventInvitationAnswers"/>: what the member said, if anything.</summary>
    public string Answer { get; set; } = ClubEventInvitationAnswers.None;

    public DateTime? AnsweredAtUtc { get; set; }

    /// <summary>The registration their "I'll come" made, or found when they were already on the list.</summary>
    public Guid? RegistrationId { get; set; }

    /// <summary>The invitation email in the outbox.</summary>
    public Guid? OutboundMessageId { get; set; }

    /// <summary>When the one reminder was claimed for them: at most one per member and event.</summary>
    public DateTime? RemindedAtUtc { get; set; }

    public Guid? ReminderMessageId { get; set; }

    public DateTime InvitedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>What a member answered on their reply page.</summary>
public static class ClubEventInvitationAnswers
{
    /// <summary>No answer yet.</summary>
    public const string None = "none";

    /// <summary>"I'll come": registered, with a seat or on the waitlist.</summary>
    public const string Coming = "coming";

    /// <summary>"I can't come".</summary>
    public const string Declined = "declined";
}
