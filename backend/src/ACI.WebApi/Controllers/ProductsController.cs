using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Controllers;

/// <summary>The workspace price list. Deal lines copy a product; they do not stay linked to it.</summary>
[ApiController]
[Route("api/products")]
[Authorize]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _products;
    private readonly ICurrentUserService _currentUser;

    public ProductsController(IProductService products, ICurrentUserService currentUser)
    {
        _products = products;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> List(CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        return Ok(await _products.ListAsync(userId, _currentUser.CurrentOrganizationId, ct));
    }

    [HttpPost]
    public async Task<ActionResult<ProductDto>> Create([FromBody] SaveProductRequest request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var result = await _products.CreateAsync(userId, _currentUser.CurrentOrganizationId, request, ct);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } userId) return Unauthorized();
        var result = await _products.DeleteAsync(id, userId, _currentUser.CurrentOrganizationId, ct);
        return result.ToNoContentResult();
    }
}
