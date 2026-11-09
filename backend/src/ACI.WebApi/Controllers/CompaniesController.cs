using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>
/// Manages companies in the CRM system.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;
    private readonly ICurrentUserService _currentUser;

    /// <summary>
    /// Initializes a new instance of the CompaniesController.
    /// </summary>
    public CompaniesController(ICompanyService companyService, ICurrentUserService currentUser)
    {
        _companyService = companyService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Retrieves companies with pagination, filters and sorting, all applied to the whole
    /// workspace, each with its contact and deal counts.
    /// </summary>
    /// <param name="page">Page number (1-based). Default is 1.</param>
    /// <param name="pageSize">Number of items per page. Default is 20, max 100.</param>
    /// <param name="search">Optional search query.</param>
    /// <param name="industry">Only companies in this industry, ignoring case.</param>
    /// <param name="hasIndustry">False: only companies with no industry. True: only those with one.</param>
    /// <param name="size">Only companies of this size, as stored ("11-50").</param>
    /// <param name="sortBy">name (the default), createdAt, contacts or deals.</param>
    /// <param name="sortDir">asc (the default) or desc.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of companies.</returns>
    /// <response code="200">Returns the paginated list of companies.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CompanyListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<CompanyListItemDto>>> GetCompanies(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? industry = null,
        [FromQuery] bool? hasIndustry = null,
        [FromQuery] string? size = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        // HP-11: Clamp pageSize to prevent overloading
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = await _companyService.GetCompaniesPagedAsync(
            userId.Value,
            _currentUser.CurrentOrganizationId,
            page,
            pageSize,
            Options(search, industry, hasIndustry, size, sortBy, sortDir),
            ct);

        return Ok(result);
    }

    /// <summary>
    /// The figures above the companies list: how many companies, with contacts, with deals,
    /// added in the last 7 days, the deals' value per currency, and the industries they are in.
    /// </summary>
    /// <response code="200">The figures, over the whole workspace.</response>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(CompanySummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CompanySummaryDto>> GetSummary(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        return Ok(await _companyService.GetSummaryAsync(userId.Value, _currentUser.CurrentOrganizationId, ct));
    }

    /// <summary>
    /// Every company the list shows with the same filters and order, as a CSV file. Takes
    /// the list's parameters, less the page. X-Total-Count says how many match, and
    /// X-Exported-Count how many the file holds (at most 50,000).
    /// </summary>
    /// <response code="200">The CSV file.</response>
    [HttpGet("export")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> Export(
        [FromQuery] string? search = null,
        [FromQuery] string? industry = null,
        [FromQuery] bool? hasIndustry = null,
        [FromQuery] string? size = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] string? sortDir = null,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var file = await _companyService.ExportAsync(
            userId.Value, _currentUser.CurrentOrganizationId, Options(search, industry, hasIndustry, size, sortBy, sortDir), ct);
        Response.Headers["X-Total-Count"] = file.Total.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Response.Headers["X-Exported-Count"] = file.Exported.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return File(file.Content, "text/csv; charset=utf-8", file.FileName);
    }

    private static CompanyQueryOptions Options(string? search, string? industry, bool? hasIndustry, string? size, string? sortBy, string? sortDir) => new()
    {
        Search = search,
        Industry = industry,
        HasIndustry = hasIndustry,
        Size = size,
        SortBy = string.IsNullOrWhiteSpace(sortBy) ? "name" : sortBy,
        SortDir = string.IsNullOrWhiteSpace(sortDir) ? "asc" : sortDir,
    };

    /// <summary>
    /// Retrieves all companies for the authenticated user (non-paginated, for backward compatibility).
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of companies.</returns>
    /// <response code="200">Returns the list of companies.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("all")]
    [ProducesResponseType(typeof(IReadOnlyList<CompanyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<CompanyDto>>> GetAllCompanies(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var list = await _companyService.GetCompaniesAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        return Ok(list);
    }

    /// <summary>
    /// Searches companies by name or domain (non-paginated, for backward compatibility).
    /// </summary>
    /// <param name="q">The search query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of matching companies.</returns>
    /// <response code="200">Returns the list of matching companies.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<CompanyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<CompanyDto>>> Search(
        [FromQuery] string? q, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var list = await _companyService.SearchAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            q ?? "", 
            ct);
        
        return Ok(list);
    }

    /// <summary>
    /// Retrieves per-company statistics (contact count, deal count, total deal value).
    /// HP-7: Replaces the need for the frontend to fetch all contacts and deals.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A list of stats per company.</returns>
    /// <response code="200">Returns company statistics.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(IReadOnlyList<CompanyStatsItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<CompanyStatsItemDto>>> GetStats(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var stats = await _companyService.GetCompanyStatsAsync(
            userId.Value,
            _currentUser.CurrentOrganizationId,
            ct);
        
        return Ok(stats);
    }

    /// <summary>
    /// Retrieves a specific company by ID.
    /// </summary>
    /// <param name="id">The company ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The company if found.</returns>
    /// <response code="200">Returns the company.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Company not found.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(CompanyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyDto>> GetById(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _companyService.GetByIdAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            ct);
        
        if (result.IsSuccess) Response.SetETag(result.Value.Version);
        return result.ToActionResult();
    }

    /// <summary>
    /// Creates a new company.
    /// </summary>
    /// <param name="request">The company creation request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The created company.</returns>
    /// <response code="200">Company created successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpPost]
    [ProducesResponseType(typeof(CompanyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CompanyDto>> Create(
        [FromBody] CreateCompanyRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _companyService.CreateAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            request, 
            ct);
        
        return result.ToActionResult();
    }

    /// <summary>
    /// Updates an existing company.
    /// </summary>
    /// <param name="id">The company ID to update.</param>
    /// <param name="request">The company update request.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated company.</returns>
    /// <response code="200">Company updated successfully.</response>
    /// <response code="400">Invalid request data.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Company not found.</response>
    /// <response code="409">Someone saved the company after the version this edit sends (in <c>version</c> or If-Match).</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CompanyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompanyDto>> Update(
        Guid id, 
        [FromBody] UpdateCompanyRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        // The version the edit was made from, from the body or from If-Match.
        var (version, versionError) = Request.ExpectedVersion(request.Version);
        if (versionError != null) return versionError.ToProblemResult();

        var result = await _companyService.UpdateAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            request with { Version = version }, 
            ct);

        if (result.IsSuccess) Response.SetETag(result.Value.Version);
        return result.ToActionResult();
    }

    /// <summary>Keeps this company and folds the other one into it.</summary>
    [HttpPost("{id:guid}/merge")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Merge(Guid id, [FromBody] MergeCompaniesRequest request, CancellationToken ct)
    {
        if (request == null || request.OtherCompanyId == Guid.Empty)
            return BadRequest();
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        var result = await _companyService.MergeAsync(id, request.OtherCompanyId, userId.Value, _currentUser.CurrentOrganizationId, ct);
        return result.ToNoContentResult();
    }

    /// <summary>
    /// Deletes a company.
    /// </summary>
    /// <param name="id">The company ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">Company deleted successfully.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Company not found.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();

        var result = await _companyService.DeleteAsync(
            id,
            userId.Value,
            _currentUser.CurrentOrganizationId,
            ct);

        return result.ToNoContentResult();
    }
}
