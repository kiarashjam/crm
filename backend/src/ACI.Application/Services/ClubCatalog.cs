using System.Text.Json.Serialization;
using ACI.Application.Common;

namespace ACI.Application.Services;

/// <summary>
/// The club's one price catalogue: membership tiers and their dues, other dues, fees,
/// products, tickets and menu items (see ClubCatalogItem in ACI.Domain).
/// </summary>
/// <remarks>
/// <para>
/// How to build on it. Wherever the server needs a price it takes it from here: a new
/// member's dues and initiation fee from their tier, a charge or a tab item rung up from an
/// item, a subscription started from a tier or dues item. What is charged copies the name,
/// the price and the VAT rate, so changing a price changes what is charged from then on and
/// never what already was. A tier's multiplier sets the points its members earn
/// (<see cref="IClubRules"/>).
/// </para>
/// <para>
/// Event tickets stay on the event for now. When events take their price from here, an
/// event names a Ticket item (or one per kind of ticket) and the registration posts its
/// charge with that item's id (<see cref="PostChargeRequest.CatalogItemId"/>), which brings
/// the name, the price, the VAT rate and the charge kind Event with it.
/// </para>
/// <para>
/// Items are never deleted, only taken off sale, so whatever names one keeps reading. A
/// workspace's first read finds its tiers there already: the migration carried in the ones
/// it had, and a workspace made later is given the four default tiers by the same rule.
/// </para>
/// </remarks>
public interface IClubCatalog
{
    /// <summary>The workspace's items, of one kind or all, tiers first in their order, then each kind by its order and name.</summary>
    Task<IReadOnlyList<CatalogItemDto>> ListAsync(Guid? organizationId, string? kind = null, bool includeInactive = true, CancellationToken ct = default);

    Task<Result<CatalogItemDto>> GetAsync(Guid? organizationId, Guid id, CancellationToken ct = default);

    Task<Result<CatalogItemDto>> CreateAsync(Guid? organizationId, CatalogItemWriteRequest request, CancellationToken ct = default);

    /// <summary>
    /// Changes an item: a field left out is kept. A renamed tier renames it on every member
    /// who has it, in the same save, so members keep their tier.
    /// </summary>
    Task<Result<CatalogItemDto>> UpdateAsync(Guid? organizationId, Guid id, CatalogItemWriteRequest request, Guid? expectedVersion = null, CancellationToken ct = default);

    /// <summary>The workspace's membership tiers, those taken off sale included, in their order.</summary>
    Task<IReadOnlyList<CatalogItemDto>> TiersAsync(Guid? organizationId, CancellationToken ct = default);

    /// <summary>The tier with this name (any case), on sale or not; null when the workspace has none of that name.</summary>
    Task<CatalogItemDto?> TierAsync(Guid? organizationId, string? name, CancellationToken ct = default);

    /// <summary>
    /// One item to charge from: on sale, of one of <paramref name="kinds"/>. A refusal says
    /// why it can't be used for <paramref name="use"/> ("a tab", "a charge").
    /// </summary>
    Task<Result<CatalogItemDto>> ChargeableAsync(Guid? organizationId, Guid id, IReadOnlyCollection<string> kinds, string use, CancellationToken ct = default);
}

public sealed record CatalogItemDto
{
    public Guid Id { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Description { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Category { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Code { get; init; }

    /// <summary>VAT included; for a tier or dues, for each period of <see cref="Cadence"/>.</summary>
    public decimal Price { get; init; }

    public string Currency { get; init; } = string.Empty;

    /// <summary>Percent, included in the price: 8.1 is 8.1%.</summary>
    public decimal VatRate { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Cadence { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? InitiationFee { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public decimal? LoyaltyMultiplier { get; init; }
    public IReadOnlyList<string> Benefits { get; init; } = Array.Empty<string>();
    public string ChargeKind { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? UpdatedByName { get; init; }
    public Guid Version { get; init; }
}

/// <summary>An item to add, or the fields of one to change: a field left out is kept, and empty text clears it.</summary>
public sealed class CatalogItemWriteRequest
{
    public string? Kind { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Code { get; set; }
    public decimal? Price { get; set; }
    public string? Currency { get; set; }
    public decimal? VatRate { get; set; }
    public string? Cadence { get; set; }
    public decimal? InitiationFee { get; set; }
    public decimal? LoyaltyMultiplier { get; set; }
    public List<string>? Benefits { get; set; }
    public string? ChargeKind { get; set; }
    public bool? IsActive { get; set; }
    public int? SortOrder { get; set; }
    public Guid? Version { get; set; }
}
