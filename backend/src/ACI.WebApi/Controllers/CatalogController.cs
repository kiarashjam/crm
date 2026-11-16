using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.WebApi.Extensions;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// The club's price catalogue: membership tiers, dues, fees, products, tickets and menu
/// items. Everyone in the workspace reads it (a till needs the menu, the members page the
/// tiers); whoever manages the settings or the money changes it.
/// </summary>
[ApiController]
[Route("api/catalog")]
[Authorize]
[WorkspaceRequired]
[Produces("application/json")]
[InModule(WorkspaceModules.Club)]
public class CatalogController : ControllerBase
{
    private readonly IClubCatalog _catalog;
    private readonly ICurrentUserService _user;

    public CatalogController(IClubCatalog catalog, ICurrentUserService user)
    {
        _catalog = catalog;
        _user = user;
    }

    /// <summary>The items, of one kind or all; those taken off sale too unless <paramref name="activeOnly"/>.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CatalogItemDto>>> List([FromQuery] string? kind, [FromQuery] bool activeOnly, CancellationToken ct) =>
        Ok(await _catalog.ListAsync(_user.CurrentOrganizationId, string.IsNullOrWhiteSpace(kind) ? null : kind.Trim(), !activeOnly, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult> Get(Guid id, CancellationToken ct) =>
        Answer(await _catalog.GetAsync(_user.CurrentOrganizationId, id, ct));

    [HttpPost]
    [Requires(MemberPermissions.ManageSettings, MemberPermissions.ManageMoney, Any = true, What = "change the price catalogue")]
    public async Task<ActionResult> Create([FromBody] CatalogItemWriteRequest request, CancellationToken ct) =>
        Answer(await _catalog.CreateAsync(_user.CurrentOrganizationId, request, ct));

    /// <summary>Changes an item; a field left out is kept. Taking one off sale is <c>isActive: false</c>: items are never deleted.</summary>
    [HttpPut("{id:guid}")]
    [Requires(MemberPermissions.ManageSettings, MemberPermissions.ManageMoney, Any = true, What = "change the price catalogue")]
    public async Task<ActionResult> Update(Guid id, [FromBody] CatalogItemWriteRequest request, CancellationToken ct)
    {
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();
        return Answer(await _catalog.UpdateAsync(_user.CurrentOrganizationId, id, request, version, ct));
    }

    private ActionResult Answer(Application.Common.Result<CatalogItemDto> result)
    {
        if (!result.IsSuccess) return result.Error.ToProblemResult();
        Response.SetETag(result.Value.Version);
        return Ok(result.Value);
    }
}
