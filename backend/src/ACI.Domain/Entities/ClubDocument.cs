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

    /// <summary>
    /// A new value on every save, and a save only lands while the row still has the
    /// value this request read.
    /// </summary>
    /// <remarks>
    /// Without it, two terminals redeeming the same gift card both read a balance of
    /// 50, both wrote 0, and 100 was spent from a 50 card; two bartenders closing the
    /// same tab both saw it open and posted it twice. Now the second save is refused
    /// and the request answers 409. A Guid set by AppDbContext rather than a SQL
    /// Server rowversion, because the in-memory provider the tests use never fills a
    /// rowversion in, so the race could not be shown there.
    /// </remarks>
    public Guid Version { get; set; }
}
