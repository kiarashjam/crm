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
    /// Retrieves companies with pagination and optional search.
    /// </summary>
    /// <param name="page">Page number (1-based). Default is 1.</param>
    /// <param name="pageSize">Number of items per page. Default is 20, max 100.</param>
    /// <param name="search">Optional search query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A paginated list of companies.</returns>
    /// <response code="200">Returns the paginated list of companies.</response>
    /// <response code="401">User is not authenticated.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CompanyDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<CompanyDto>>> GetCompanies(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _companyService.GetCompaniesPagedAsync(
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            page,
            pageSize,
            search,
            ct);
        
        return Ok(result);
    }

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
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CompanyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyDto>> Update(
        Guid id, 
        [FromBody] UpdateCompanyRequest request, 
        CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId == null) return Unauthorized();
        
        var result = await _companyService.UpdateAsync(
            id, 
            userId.Value, 
            _currentUser.CurrentOrganizationId, 
            request, 
            ct);
        
        return result.ToActionResult();
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
