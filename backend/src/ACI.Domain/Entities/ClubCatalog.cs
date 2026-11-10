namespace ACI.Domain.Entities;

// The club's one price list: membership tiers and their dues, other recurring dues,
// one-off fees, products, event tickets and menu items, each with a price, a currency, a
// VAT rate and whether it is still offered.
//
// Prices used to live in three places: hard-coded in the browser (the tier dues on the
// members page), in the hospitality settings document (which nothing on the server read),
// and typed in by hand on each charge, tab item and subscription. Now the server takes a
// price from here wherever it needs one, and a Settings page edits it. What was charged
// stays as it was charged: a charge, a tab item or a subscription copies the name and the
// price when it is made, as a deal's line items copy a product, so changing a price changes
// what is charged from then on and never rewrites what already was.

/// <summary>What a catalogue item is.</summary>
public static class CatalogKinds
{
    /// <summary>
    /// A membership tier. Its price is the tier's dues for each period of its cadence, and it
    /// carries the initiation fee, the loyalty multiplier and the benefits. A member's tier
    /// is the name of one of these.
    /// </summary>
    public const string Membership = "Membership";

    /// <summary>Another amount billed every period: a locker, parking, a family add-on.</summary>
    public const string Dues = "Dues";

    /// <summary>A one-off fee: a guest fee, a late fee, a no-show fee.</summary>
    public const string Fee = "Fee";

    /// <summary>Something sold over the counter: the pro shop, spa products.</summary>
    public const string Product = "Product";

    /// <summary>An event ticket at a set price.</summary>
    public const string Ticket = "Ticket";

    /// <summary>Something on a venue's menu, added to a tab.</summary>
    public const string MenuItem = "MenuItem";

    public static readonly string[] All = [Membership, Dues, Fee, Product, Ticket, MenuItem];

    /// <summary>Kinds billed every period, which carry a cadence.</summary>
    public static bool IsRecurring(string kind) => kind is Membership or Dues;
}

/// <summary>An item in the club's price catalogue (see <see cref="CatalogKinds"/>).</summary>
/// <remarks>
/// Items are never deleted, only taken off sale (<see cref="IsActive"/> false): a member's
/// tier is the name of one, and charges and subscriptions say which item they came from.
/// </remarks>
public class ClubCatalogItem : Common.BaseEntity, Common.IVersioned
{
    public Guid? OrganizationId { get; set; }

    /// <summary><see cref="CatalogKinds"/>; fixed once the item exists.</summary>
    public string Kind { get; set; } = CatalogKinds.Product;

    /// <summary>For a tier, the name members carry (ClubMember.Tier), so at most 32 characters and one per workspace.</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>A group to show it under: a menu item's venue (Bar, Dining…), a product's shelf.</summary>
    public string? Category { get; set; }

    /// <summary>The club's own code for it, a SKU or a till's PLU.</summary>
    public string? Code { get; set; }

    /// <summary>What it costs, VAT included: for a tier or dues, for each period of its cadence.</summary>
    public decimal Price { get; set; }

    public string Currency { get; set; } = "CHF";

    /// <summary>
    /// The VAT rate in percent (8.1 is 8.1%), included in the price. Swiss rates since 2024:
    /// 8.1 standard, 2.6 reduced, 3.8 accommodation; 0 for something exempt or untaxed.
    /// </summary>
    public decimal VatRate { get; set; }

    /// <summary>Monthly, Quarterly, SemiAnnual or Annual, for a tier or dues; none for the rest.</summary>
    public string? Cadence { get; set; }

    /// <summary>A tier's one-off fee on joining, VAT included at the same rate.</summary>
    public decimal? InitiationFee { get; set; }

    /// <summary>A tier's loyalty multiplier: points a member of it earns are the base times this.</summary>
    public decimal? LoyaltyMultiplier { get; set; }

    /// <summary>A tier's benefits, as a JSON array of short sentences.</summary>
    public string? Benefits { get; set; }

    /// <summary>What a charge from it is posted as on the house account: Dues, Dining, Bar, Event, Spa, Booking, Retail or Other.</summary>
    public string ChargeKind { get; set; } = "Other";

    /// <summary>Whether it is still offered. One taken off sale stays for what already names it.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Where it is listed among its kind, lowest first: the order tiers are shown in.</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid? UpdatedByUserId { get; set; }
    public string? UpdatedByName { get; set; }
    public Guid Version { get; set; }
}
