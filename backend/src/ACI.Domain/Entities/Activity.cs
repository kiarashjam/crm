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
