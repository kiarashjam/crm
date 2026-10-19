namespace ACI.Domain.Entities;

/// <summary>
/// One club record whose shape is owned by the screen that edits it:
/// an event, a charge, a reservation, a gift card, and the rest.
/// </summary>
public class ClubDocument : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public DateTime UpdatedAtUtc { get; set; }
}
