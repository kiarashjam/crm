namespace ACI.Domain.Entities;

/// <summary>
/// Activity (call, meeting, email, note) linked to contact or deal; timestamped.
/// </summary>
public class Activity : Common.BaseEntity
{
    public Guid UserId { get; set; }
    /// <summary>Type: call, meeting, email, note.</summary>
    public string Type { get; set; } = "note";
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public Guid? ContactId { get; set; }
    public Guid? DealId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public Contact? Contact { get; set; }
    public Deal? Deal { get; set; }
}
