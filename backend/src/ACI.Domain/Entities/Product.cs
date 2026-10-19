namespace ACI.Domain.Entities;

/// <summary>
/// A reusable thing the workspace sells. Deal line items copy the name and price
/// at the moment they are added, so later edits here do not rewrite old deals.
/// </summary>
public class Product : Common.BaseEntity
{
    public Guid? OrganizationId { get; set; }
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Currency { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
