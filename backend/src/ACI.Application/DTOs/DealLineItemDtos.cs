using ACI.Application.Common;

namespace ACI.Application.DTOs;

/// <summary>
/// A deal line item. Property names match the client's DealLineItem
/// (src/app/api/products.ts) so no client-side mapping is needed.
/// </summary>
/// <param name="VatRate">The VAT rate in percent the unit price includes, or null when none was given.</param>
public record DealLineItemDto(Guid Id, Guid DealId, string? ProductId, string Name, decimal Quantity, decimal UnitPrice, decimal? VatRate = null);

/// <param name="VatRate">The VAT rate in percent the unit price includes (8.1, 2.6, 3.8 or 0), if known.</param>
public record CreateDealLineItemRequest(string? ProductId, string Name, decimal Quantity, decimal UnitPrice, decimal? VatRate = null);

/// <summary>
/// A partial update: only the properties sent are changed, and the VAT rate is taken off
/// when it is sent as null (a merge patch).
/// </summary>
/// <remarks>
/// The VAT rate needs the merge patch: "no rate given" and "0 %" print differently in a
/// contract's fee table, so a rate set by mistake must be removable rather than only
/// changeable to zero.
/// </remarks>
public record UpdateDealLineItemRequest : IMergePatchRequest
{
    public string? ProductId { get; init; }
    public string? Name { get; init; }
    public decimal? Quantity { get; init; }
    public decimal? UnitPrice { get; init; }
    public decimal? VatRate { get; init; }

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public ISet<string> NullsSent { get; } = new HashSet<string>(StringComparer.Ordinal);
}
