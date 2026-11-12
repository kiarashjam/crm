using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// The house-charge screen's sales: lines from the catalogue, how the member paid (on their
/// house account, by card, cash or TWINT) and any discount with its reason, posted through
/// the ledger with the points the club's settings give. The front desk rings them up.
/// </summary>
[ApiController]
[Route("api/sales")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[Requires(MemberPermissions.FrontDesk, WritesOnly = true)]
[InModule(WorkspaceModules.Club)]
public class SalesController : ControllerBase
{
    private readonly IClubLedger _ledger;
    private readonly ICurrentUserService _user;

    public SalesController(IClubLedger ledger, ICurrentUserService user)
    {
        _ledger = ledger;
        _user = user;
    }

    /// <summary>The latest sales, or one member's, newest first, from the house-charge screen and the POS.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SaleDto>>> List([FromQuery] Guid? memberId, [FromQuery] int? take, CancellationToken ct) =>
        Ok(await _ledger.SalesAsync(_user.CurrentOrganizationId, memberId, take ?? 50, ct));

    /// <summary>
    /// Posts a sale rung up at the house-charge screen. Every line is a catalogue item, at its
    /// price and VAT rate; a retry with the same Idempotency-Key gets the first sale back.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] DeskSaleRequest body, CancellationToken ct)
    {
        var request = new SaleRequest
        {
            MemberId = body.MemberId,
            Venue = body.Venue,
            Lines = body.Lines?.Select(l => new SaleLineRequest { CatalogItemId = l.CatalogItemId, Quantity = l.Quantity }).ToList(),
            DiscountAmount = body.DiscountAmount,
            DiscountPercent = body.DiscountPercent,
            DiscountReason = body.DiscountReason,
            Tender = body.Tender,
            Notes = body.Notes,
            Source = SaleSources.Desk,
            // The front desk rings up the catalogue at its prices; anything else is a charge
            // posted by hand, by someone who takes payments.
            AllowLinesOutsideCatalog = false,
        };
        // A line with no catalogue item is refused by the ledger, with the sentence that says so.
        var key = Request.Headers.TryGetValue("Idempotency-Key", out var value) ? value.ToString() : null;
        var sale = await _ledger.PostSaleAsync(_user.CurrentOrganizationId, request, key, ct);
        return sale.IsSuccess ? Ok(sale.Value) : sale.Error.ToProblemResult();
    }
}
