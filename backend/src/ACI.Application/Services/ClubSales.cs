using System.Text.Json.Serialization;
using ACI.Application.Common;

namespace ACI.Application.Services;

/// <summary>
/// Sales: what a member bought at one go, at the club's own house-charge screen or on a ticket
/// the POS closed, posted through the ledger line by line.
/// </summary>
/// <remarks>
/// <para>
/// A sale is posted in one transaction: a charge on the house account for each kind and VAT rate
/// on it (so a statement's and the journal's VAT come out right for a mixed bill), a payment of
/// those charges for whatever was paid at the till (card, cash or TWINT), and the points the
/// spend earns by the club's settings (base points per unit spent, times the member's tier
/// multiplier). What was put on the house account stays owing. Nothing of it is saved when any
/// of it is refused, and the same Idempotency-Key posts it once.
/// </para>
/// <para>
/// A discount needs a reason, which is kept on the sale and on each charge it reduced, so a comp
/// is never a line with no trace. Prices include VAT, and a discount takes its VAT with it.
/// </para>
/// </remarks>
public partial interface IClubLedger
{
    /// <summary>Posts a sale: its charges, what was paid at the till and its points, together or not at all.</summary>
    Task<Result<SaleDto>> PostSaleAsync(Guid? organizationId, SaleRequest request, string? idempotencyKey = null, CancellationToken ct = default);

    /// <summary>The latest sales, the workspace's or one member's (as buyer), newest first.</summary>
    Task<IReadOnlyList<SaleDto>> SalesAsync(Guid? organizationId, Guid? memberId = null, int take = 50, CancellationToken ct = default);
}

/// <summary>A line of a sale: an item from the catalogue, or (from the POS) a line as its ticket has it.</summary>
public sealed class SaleLineRequest
{
    /// <summary>A product, menu item or fee from the catalogue: its name, price, VAT rate and charge kind are used, not what was sent.</summary>
    public Guid? CatalogItemId { get; set; }

    /// <summary>For a line not from the catalogue (the POS's): its name, price (VAT included), VAT rate and article number.</summary>
    public string? Name { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? VatRate { get; set; }
    public string? Sku { get; set; }

    /// <summary>The charge kind it is posted under; by default the venue's (Bar, Dining, Spa).</summary>
    public string? Kind { get; set; }

    /// <summary>How many, 1 when left out; to the hundredth (a half portion).</summary>
    public decimal? Quantity { get; set; }

    /// <summary>Taken off this line, VAT included: the POS's discount on it.</summary>
    public decimal? Discount { get; set; }

    /// <summary>Whether the line counts toward the points; a tip or a cash rounding does not.</summary>
    public bool EarnsPoints { get; set; } = true;
}

/// <summary>A way part (or all) of a sale was settled.</summary>
public sealed class SaleTenderRequest
{
    /// <summary>HouseAccount, Card, Cash or Twint.</summary>
    public string? Method { get; set; }

    public decimal? Amount { get; set; }
}

/// <summary>A sale to post. The ledger's own request: the house-charge screen sends a narrower one (DeskSaleRequest).</summary>
public sealed class SaleRequest
{
    /// <summary>Who bought it, and earns its points. Its charges go on their account, or their household primary's when their bills roll up.</summary>
    public Guid? MemberId { get; set; }

    /// <summary>Bar, Dining, Spa, Rooftop or PoolDeck.</summary>
    public string? Venue { get; set; }

    public List<SaleLineRequest>? Lines { get; set; }

    /// <summary>Taken off the whole sale, VAT included; spread over the lines in proportion.</summary>
    public decimal? DiscountAmount { get; set; }

    /// <summary>Or taken off as a percentage of the whole sale.</summary>
    public decimal? DiscountPercent { get; set; }

    /// <summary>Why: required whenever anything is taken off, on a line or the whole sale.</summary>
    public string? DiscountReason { get; set; }

    /// <summary>The one way the whole sale is settled: HouseAccount, Card, Cash or Twint.</summary>
    public string? Tender { get; set; }

    /// <summary>Or several ways, adding up to the total (a POS ticket paid partly on the house account).</summary>
    public List<SaleTenderRequest>? Tenders { get; set; }

    /// <summary>
    /// Whether lines not from the catalogue are taken. The house-charge screen rings up the
    /// catalogue only; a ticket from the POS has the POS's own lines.
    /// </summary>
    [JsonIgnore] public bool AllowLinesOutsideCatalog { get; set; }

    /// <summary>desk, or the POS it came from.</summary>
    [JsonIgnore] public string Source { get; set; } = Domain.Entities.SaleSources.Desk;

    /// <summary>The POS ticket it posts, when it does.</summary>
    [JsonIgnore] public Guid? PosTicketId { get; set; }

    /// <summary>When it was sold, when not now: a POS ticket's close time.</summary>
    [JsonIgnore] public DateTime? SoldAtUtc { get; set; }

    /// <summary>The currency lines not from the catalogue are priced in: it must be the account's.</summary>
    [JsonIgnore] public string? Currency { get; set; }

    /// <summary>The POS's receipt number.</summary>
    public string? Reference { get; set; }

    public string? StaffName { get; set; }
    public string? TableName { get; set; }
    public string? Notes { get; set; }

    /// <summary>The name on the entries when nobody is signed in: "Lightspeed".</summary>
    [JsonIgnore] public string? RecordedBy { get; set; }
}

/// <summary>What the house-charge screen sends: lines from the catalogue, how it was paid, any discount with its reason.</summary>
public sealed class DeskSaleRequest
{
    public Guid? MemberId { get; set; }
    public string? Venue { get; set; }
    public List<DeskSaleLine>? Lines { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? DiscountPercent { get; set; }
    public string? DiscountReason { get; set; }

    /// <summary>HouseAccount, Card, Cash or Twint.</summary>
    public string? Tender { get; set; }

    public string? Notes { get; set; }
}

public sealed class DeskSaleLine
{
    public Guid? CatalogItemId { get; set; }
    public decimal? Quantity { get; set; }
}

public sealed record SaleLineDto
{
    public Guid Id { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? CatalogItemId { get; init; }
    public string Name { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Sku { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal Discount { get; init; }
    public decimal Total { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? VatRate { get; init; }
    public string Kind { get; init; } = string.Empty;
    public bool EarnsPoints { get; init; }
}

public sealed record SaleDto
{
    public Guid Id { get; init; }
    public Guid MemberId { get; init; }
    public string MemberName { get; init; } = string.Empty;

    /// <summary>The account its charges went on when it is not the buyer's: their household primary's.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? BilledMemberId { get; init; }

    public string Source { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public Guid? PosTicketId { get; init; }
    public string Venue { get; init; } = string.Empty;
    public string Tender { get; init; } = string.Empty;
    public decimal Subtotal { get; init; }
    public decimal Discount { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? DiscountReason { get; init; }
    public decimal Total { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal OwedAmount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public int PointsAwarded { get; init; }
    public IReadOnlyList<Guid> ChargeIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<Guid> PaymentIds { get; init; } = Array.Empty<Guid>();
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Reference { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? StaffName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? TableName { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Notes { get; init; }
    public DateTime SoldAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CreatedByName { get; init; }
    public IReadOnlyList<SaleLineDto> Lines { get; init; } = Array.Empty<SaleLineDto>();
}
