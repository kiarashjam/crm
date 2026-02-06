using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Global search across all CRM entities.
/// </summary>
/// <remarks>
/// Searches contacts, companies, leads, deals, and activities in a single query.
/// Results are grouped by entity type and limited to the user's organization.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class SearchController : ControllerBase
{
    private readonly IGlobalSearchService _searchService;
    private readonly ICurrentUserService _currentUser;

    public SearchController(IGlobalSearchService searchService, ICurrentUserService currentUser)
    {
        _searchService = searchService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Performs a global search across all CRM entities.
    /// </summary>
    /// <remarks>
    /// Searches the following entities:
    /// - Contacts (name, email, phone)
    /// - Companies (name, domain)
    /// - Leads (name, email, company)
    /// - Deals (name)
    /// - Activities (notes, subject)
    /// 
    /// Results are limited to entities the user has access to.
    /// </remarks>
    /// <param name="q">Search query string.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Search results grouped by entity type.</returns>
    /// <response code="200">Returns search results.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(GlobalSearchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<GlobalSearchResultDto>> Search([FromQuery] string? q, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var result = await _searchService.SearchAsync(userId.Value, _currentUser.CurrentOrganizationId, q ?? "", ct);
        return Ok(result);
    }
}
