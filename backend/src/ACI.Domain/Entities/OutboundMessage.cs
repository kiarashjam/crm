namespace ACI.Domain.Entities;

/// <summary>
/// One email or integration event a workspace sends, from the moment it is queued
/// until it has gone, or will not go.
/// </summary>
/// <remarks>
/// The composer, the assistant, sequences, automations and the outbound webhook each
/// used to send in their own way: some inside the request that saved a lead, some
/// with no retry, some leaving no trace. Now every one of them writes a row here and
/// only the outbox hands it on, so each message is checked against the same
/// suppression list, retried the same way and recorded the same way, and can be
/// found again by its id when a reply, a bounce or a delivery report comes back.
/// </remarks>
public class OutboundMessage : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }

    /// <summary><see cref="OutboundChannels"/>: an email, or an event posted to the workspace's own URL.</summary>
    public string Channel { get; set; } = OutboundChannels.Email;

    /// <summary><see cref="OutboundSources"/>: what queued it.</summary>
    public string Source { get; set; } = OutboundSources.Composer;

    /// <summary>
    /// The record behind the sender, when there is one: the enrollment for a sequence
    /// step, the rule for an automation, the campaign for a mailing.
    /// </summary>
    public Guid? SourceId { get; set; }

    /// <summary><see cref="OutboundStatuses"/>.</summary>
    public string Status { get; set; } = OutboundStatuses.Queued;

    /// <summary>Attempts made so far, the one in progress included.</summary>
    public int Attempts { get; set; }

    /// <summary>When a queued message is next due; null once it is no longer queued.</summary>
    public DateTime? NextAttemptAtUtc { get; set; }

    /// <summary>
    /// Until when one dispatcher holds it. Another instance leaves it alone until then;
    /// if the holder dies mid-attempt, the claim runs out and it is tried again.
    /// </summary>
    public DateTime? ClaimedUntilUtc { get; set; }

    /// <summary>Why the last attempt did not go through, in words a person can act on.</summary>
    public string? LastError { get; set; }

    /// <summary>The receiver's HTTP status for a webhook, or the mail server's reply code.</summary>
    public int? LastStatusCode { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }

    /// <summary>
    /// Who queued it: the person who pressed Send, who enrolled the lead, or who set the
    /// rule off. The timeline entry is theirs, and they hear about a message that fails.
    /// </summary>
    public Guid? CreatedByUserId { get; set; }

    // ── Email ─────────────────────────────────────────────────────────────────

    /// <summary>Shown beside the sender address: the workspace's name.</summary>
    public string? FromName { get; set; }

    /// <summary>
    /// The workspace's own sender address, set only when its domain is one the mail
    /// service has verified. Null: the configured sender address.
    /// </summary>
    public string? FromAddress { get; set; }

    /// <summary>Where replies go, comma separated: the person behind it, and the reply-capture address when there is one.</summary>
    public string? ReplyTo { get; set; }

    /// <summary>Recipients, comma separated.</summary>
    public string? To { get; set; }
    public string? Cc { get; set; }
    public string? Bcc { get; set; }

    /// <summary>The recipient's name, for a message to one person.</summary>
    public string? RecipientName { get; set; }

    /// <summary>The subject as sent, merge fields filled.</summary>
    public string? Subject { get; set; }

    /// <summary>The words as written, merge fields filled, without the footer: what the timeline shows.</summary>
    public string? Body { get; set; }

    /// <summary>The plain-text part exactly as sent, footer included.</summary>
    public string? TextBody { get; set; }

    /// <summary>The HTML part exactly as sent.</summary>
    public string? HtmlBody { get; set; }

    /// <summary>
    /// For the List-Unsubscribe header, when the message is for one address: the one-click
    /// address of RFC 8058 when this API has a public https address
    /// (<see cref="OneClickUnsubscribe"/>), otherwise the unsubscribe page.
    /// </summary>
    public string? UnsubscribeUrl { get; set; }

    /// <summary>
    /// Whether <see cref="UnsubscribeUrl"/> takes the one-click POST a mail app sends for its
    /// own Unsubscribe button, so the message also says List-Unsubscribe-Post.
    /// </summary>
    public bool OneClickUnsubscribe { get; set; }

    /// <summary>
    /// What a message to a club member is for (<see cref="ConsentPurposes"/>): marketing goes
    /// only to a member who agreed to it by email and has not withdrawn; a service message
    /// (a statement, a booking, a receipt) goes whatever they said. Null for the sales
    /// outreach the outbox carried before, whose rules are unchanged.
    /// </summary>
    public string? Purpose { get; set; }

    /// <summary>
    /// The Message-ID header, angle brackets included. A reply names it in In-Reply-To
    /// or References, and the mail service's reports carry it, so both can be matched.
    /// </summary>
    public string? MessageIdHeader { get; set; }

    /// <summary>
    /// The random part of the reply-capture address (reply+token@…), when replies are
    /// captured. A reply sent there is matched by it even when a mail app drops the
    /// In-Reply-To header.
    /// </summary>
    public string? ReplyToken { get; set; }

    /// <summary>
    /// A person wrote it (the composer, the assistant): its timeline entry counts as
    /// their response — the first-response clock stops and the person's sequence ends,
    /// as when they log an email by hand. Sequence and automation mail is automated.
    /// </summary>
    public bool CountsAsResponse { get; set; }

    public Guid? LeadId { get; set; }
    public Guid? ContactId { get; set; }
    public Guid? DealId { get; set; }

    /// <summary>A club member it was sent to; for member mailings, which have no lead or contact.</summary>
    public Guid? MemberId { get; set; }

    /// <summary>The timeline entry written when it went, so there is one per message.</summary>
    public Guid? ActivityId { get; set; }

    // ── What the mail service reports back ─────────────────────────────────────

    /// <summary>delivered, deferred, blocked, bounced, dropped or spam_report, from the mail service's event reports.</summary>
    public string? DeliveryStatus { get; set; }
    public string? DeliveryDetail { get; set; }
    public DateTime? DeliveryUpdatedAtUtc { get; set; }
    public DateTime? OpenedAtUtc { get; set; }

    /// <summary>When the first reply to it came back.</summary>
    public DateTime? RepliedAtUtc { get; set; }

    // ── Webhook ───────────────────────────────────────────────────────────────

    /// <summary>lead.created, deal.stage_changed, deal.won.</summary>
    public string? EventName { get; set; }

    /// <summary>The event's data as JSON; the envelope around it is built for each attempt.</summary>
    public string? PayloadJson { get; set; }
}

/// <summary>What the outbox carries.</summary>
public static class OutboundChannels
{
    public const string Email = "email";
    public const string Webhook = "webhook";
}

/// <summary>Where an outbound message stands.</summary>
public static class OutboundStatuses
{
    /// <summary>Waiting for its first or next attempt.</summary>
    public const string Queued = "queued";

    /// <summary>Handed to the mail server, or taken by the receiver.</summary>
    public const string Sent = "sent";

    /// <summary>Given up on: refused for good, or still failing after the last retry.</summary>
    public const string Failed = "failed";

    /// <summary>
    /// Taken back before it went: by the person who wrote it, or because what it was for
    /// went first (the workspace's webhook address, the lead or contact, or the sequence,
    /// stopped by a reply, say).
    /// </summary>
    public const string Cancelled = "cancelled";

    /// <summary>
    /// Not sent: by the time it was due, someone it was for had asked not to be emailed,
    /// or their address had bounced.
    /// </summary>
    public const string Suppressed = "suppressed";
}

/// <summary>What queued an outbound message.</summary>
public static class OutboundSources
{
    public const string Composer = "composer";
    public const string Assistant = "assistant";
    public const string Sequence = "sequence";
    public const string Automation = "automation";
    /// <summary>A campaign's email to one of its members; <see cref="OutboundMessage.SourceId"/> is the campaign.</summary>
    public const string Campaign = "campaign";
    /// <summary>
    /// A campaign sent to the person who pressed "Send a test to me", marked as a test and
    /// counted in none of the campaign's figures.
    /// </summary>
    public const string CampaignTest = "campaign_test";
    /// <summary>A message staff wrote to one member, from the Communications page or the member's own.</summary>
    public const string Member = "member";
    /// <summary>A member's statement, as a PDF: emailed on request or by the monthly run.</summary>
    public const string Statement = "statement";
    /// <summary>A reminder of charges a member has not paid by their due date, from the billing run.</summary>
    public const string PaymentReminder = "reminder";
    /// <summary>
    /// An event's email to someone on its list: a seat that came free for them, or the event
    /// cancelled; <see cref="OutboundMessage.SourceId"/> is their registration.
    /// </summary>
    public const string Event = "event";
    /// <summary>A link for a member to pay their account online or save a card, sent by staff.</summary>
    public const string PaymentLink = "payment_link";
    public const string Webhook = "webhook";
}

/// <summary>
/// A file that goes with an outbox email, such as a statement's PDF: kept with the message
/// so every attempt, and a retry hours later, sends the same file.
/// </summary>
public class OutboundAttachment : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid OutboundMessageId { get; set; }

    /// <summary>What the recipient sees and saves.</summary>
    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; }
}
