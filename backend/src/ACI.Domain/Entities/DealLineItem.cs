namespace ACI.Domain.Entities;

/// <summary>
/// One line of a deal's breakdown: what is being sold, how many, at what price.
/// </summary>
/// <remarks>
/// A line item belongs to its deal and nothing else, so it carries no workspace
/// of its own: it is read and changed through its deal, and follows it if the deal
/// moves to another workspace. Changing line items never rewrites the deal's value;
/// the client sets that from the total when the person asks it to.
/// </remarks>
public class DealLineItem : Common.BaseEntity
{
    public Guid DealId { get; set; }

    /// <summary>
    /// The catalog product it came from, if any. Opaque for now: the catalog still
    /// lives in each browser, so this is the client's id, kept only as a hint. The
    /// line's own name and price are what count.
    /// </summary>
    public string? ProductId { get; set; }

    public string Name { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
