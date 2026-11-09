namespace ACI.Domain.Entities;

/// <summary>
/// An address a workspace no longer emails because the mail service reported it:
/// it bounced, the mail service dropped it as invalid, or the person marked the
/// workspace's email as spam.
/// </summary>
/// <remarks>
/// A person who unsubscribes or is marked do not contact is kept on their contact
/// (<see cref="Contact.DoNotContact"/>), where staff see and change it. This holds the
/// rest of the suppression list: what the mail service tells us about an address,
/// which no contact may exist for. The outbox reads both before every email.
/// </remarks>
public class EmailSuppression : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }

    /// <summary>The address, trimmed and lower-cased.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary><see cref="EmailSuppressionReasons"/>.</summary>
    public string Reason { get; set; } = EmailSuppressionReasons.Bounced;

    /// <summary>What the mail service said, e.g. "550 5.1.1 The email account does not exist".</summary>
    public string? Detail { get; set; }

    /// <summary>The message the report was about, when it could be matched.</summary>
    public Guid? OutboundMessageId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>Why an address is on a workspace's suppression list.</summary>
public static class EmailSuppressionReasons
{
    /// <summary>The receiving server refused it for good: the mailbox does not exist, say.</summary>
    public const string Bounced = "bounced";

    /// <summary>The person marked the workspace's email as spam.</summary>
    public const string SpamReport = "spam_report";

    /// <summary>The mail service would not send to it at all, e.g. an invalid address.</summary>
    public const string Invalid = "invalid";

    /// <summary>
    /// The person unsubscribed through the mail service's own link or list, not the
    /// workspace's (which marks them do not contact instead).
    /// </summary>
    public const string Unsubscribed = "unsubscribed";
}
