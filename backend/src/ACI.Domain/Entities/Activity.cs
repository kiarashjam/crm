namespace ACI.Domain.Entities;

/// <summary>
/// Activity (call, meeting, email, note, task, follow_up, deadline, video, demo) linked to contact, deal, or lead; timestamped.
/// </summary>
public class Activity : Common.BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    /// <summary>Type: call, meeting, email, note, task, follow_up, deadline, video, demo.</summary>
    public string Type { get; set; } = "note";
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public Guid? ContactId { get; set; }
    public Guid? DealId { get; set; }
    public Guid? LeadId { get; set; }
    public string? Participants { get; set; }

    /// <summary>
    /// When it happened. A call made this morning and written down tonight happened this
    /// morning, and the timeline, "last activity" and the first-response clock go by this.
    /// The moment it was written down is <see cref="CreatedAtUtc"/>. Never ahead of the
    /// time it is logged: what is planned is a task.
    /// </summary>
    public DateTime OccurredAtUtc { get; set; }

    /// <summary>How a call or a meeting went, from <c>ActivityFacts</c>: "no_answer", "no_show"…</summary>
    public string? Outcome { get; set; }

    /// <summary>How long a call or a meeting took, in minutes.</summary>
    public int? DurationMinutes { get; set; }

    /// <summary>"inbound" or "outbound", for a call or an email: who reached out to whom.</summary>
    public string? Direction { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }

    public User User { get; set; } = null!;
    public User? UpdatedByUser { get; set; }
    public Organization? Organization { get; set; }
    public Contact? Contact { get; set; }
    public Deal? Deal { get; set; }
    public Lead? Lead { get; set; }
}
