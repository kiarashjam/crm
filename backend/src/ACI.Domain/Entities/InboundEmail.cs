namespace ACI.Domain.Entities;

/// <summary>
/// A reply that came back to a message the workspace sent, matched to that message
/// and written on the record's timeline.
/// </summary>
/// <remarks>
/// Kept so that the same reply delivered twice (the mail service retries when it
/// gets no answer in time) is recognised by its own Message-ID and written once.
/// Mail that matches nothing the workspace sent is not kept at all.
/// </remarks>
public class InboundEmail : Common.BaseEntity
{
    public Guid OrganizationId { get; set; }

    /// <summary>The message it answers.</summary>
    public Guid OutboundMessageId { get; set; }

    /// <summary>Its own Message-ID header, when it had one.</summary>
    public string? MessageIdHeader { get; set; }

    public string FromAddress { get; set; } = string.Empty;
    public string? Subject { get; set; }

    /// <summary>How it was matched: "token" (the reply address), "in-reply-to" or "references".</summary>
    public string MatchedBy { get; set; } = string.Empty;

    /// <summary>The timeline entry it became.</summary>
    public Guid? ActivityId { get; set; }

    public DateTime ReceivedAtUtc { get; set; }
}
