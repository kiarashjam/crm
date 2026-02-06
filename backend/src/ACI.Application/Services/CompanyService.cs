using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// Service for managing companies in the CRM system.
/// </summary>
public class CompanyService : ICompanyService
{
    private readonly ICompanyRepository _repository;
    private readonly ILogger<CompanyService> _logger;

    public CompanyService(ICompanyRepository repository, ILogger<CompanyService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<PagedResult<CompanyDto>> GetCompaniesPagedAsync(
        Guid userId, 
        Guid? organizationId, 
        int page = 1, 
        int pageSize = 20, 
        string? search = null,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting paged companies for user {UserId}, organization {OrganizationId}, page {Page}, pageSize {PageSize}, search '{Search}'",
            userId, organizationId, page, pageSize, search);

        var skip = (page - 1) * pageSize;
        var (items, totalCount) = await _repository.GetPagedAsync(userId, organizationId, skip, pageSize, search, ct);
        
        var dtos = items.Select(Map).ToList();
        
        _logger.LogInformation(
            "Retrieved {Count} of {Total} companies for user {UserId} (page {Page})",
            items.Count, totalCount, userId, page);

        return PagedResult<CompanyDto>.Create(dtos, totalCount, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CompanyDto>> GetCompaniesAsync(
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting companies for user {UserId} in organization {OrganizationId}", userId, organizationId);
        
        var list = await _repository.GetByUserIdAsync(userId, organizationId, ct);
        
        _logger.LogDebug("Retrieved {Count} companies for user {UserId}", list.Count, userId);
        
        return list.Select(Map).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CompanyDto>> SearchAsync(
        Guid userId, 
        Guid? organizationId, 
        string query, 
        CancellationToken ct = default)
    {
        _logger.LogDebug("Searching companies for user {UserId} with query '{Query}'", userId, query);
        
        var list = await _repository.SearchAsync(userId, organizationId, query ?? "", ct);
        
        _logger.LogDebug("Search returned {Count} companies for query '{Query}'", list.Count, query);
        
        return list.Select(Map).ToList();
    }

    /// <inheritdoc />
    public async Task<Result<CompanyDto>> GetByIdAsync(
        Guid id, 
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogDebug("Getting company {CompanyId} for user {UserId}", id, userId);
        
        var entity = await _repository.GetByIdAsync(id, userId, organizationId, ct);
        
        if (entity == null)
        {
            _logger.LogWarning("Company {CompanyId} not found for user {UserId}", id, userId);
            return DomainErrors.Company.NotFound;
        }
        
        _logger.LogDebug("Successfully retrieved company {CompanyId}", id);
        return Map(entity);
    }

    /// <inheritdoc />
    public async Task<Result<CompanyDto>> CreateAsync(
        Guid userId, 
        Guid? organizationId, 
        CreateCompanyRequest request, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Creating company for user {UserId} with name '{Name}'", userId, request.Name);
        
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            _logger.LogWarning("Company creation failed - name is required");
            return DomainErrors.Company.NameRequired;
        }

        // Validate domain format if provided
        if (!string.IsNullOrWhiteSpace(request.Domain) && !ValidationHelper.IsValidDomain(request.Domain))
        {
            _logger.LogWarning("Company creation failed - invalid domain format: {Domain}", request.Domain);
            return DomainErrors.Company.DomainInvalid;
        }

        try
        {
            var entity = new Company
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrganizationId = organizationId,
                Name = request.Name.Trim(),
                Domain = request.Domain?.Trim(),
                Industry = request.Industry?.Trim(),
                Size = request.Size?.Trim(),
                CreatedAtUtc = DateTime.UtcNow,
            };
            
            entity = await _repository.AddAsync(entity, ct);
            
            _logger.LogInformation("Successfully created company {CompanyId} with name '{Name}'", entity.Id, entity.Name);
            return Map(entity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating company for user {UserId}", userId);
            return DomainErrors.General.ServerError;
        }
    }

    /// <inheritdoc />
    public async Task<Result<CompanyDto>> UpdateAsync(
        Guid id, 
        Guid userId, 
        Guid? organizationId, 
        UpdateCompanyRequest request, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Updating company {CompanyId} for user {UserId}", id, userId);
        
        var existing = await _repository.GetByIdAsync(id, userId, organizationId, ct);
        
        if (existing == null)
        {
            _logger.LogWarning("Company {CompanyId} not found for update", id);
            return DomainErrors.Company.NotFound;
        }
        
        // Validate domain format if provided
        if (!string.IsNullOrWhiteSpace(request.Domain) && !ValidationHelper.IsValidDomain(request.Domain))
        {
            _logger.LogWarning("Company update failed - invalid domain format: {Domain}", request.Domain);
            return DomainErrors.Company.DomainInvalid;
        }
        
        if (request.Name != null) existing.Name = request.Name.Trim();
        if (request.Domain != null) existing.Domain = request.Domain.Trim();
        if (request.Industry != null) existing.Industry = request.Industry.Trim();
        if (request.Size != null) existing.Size = request.Size.Trim();
        
        try
        {
            existing = await _repository.UpdateAsync(existing, userId, organizationId, ct);
            
            if (existing == null)
            {
                _logger.LogWarning("Company {CompanyId} update returned null", id);
                return DomainErrors.Company.NotFound;
            }
            
            _logger.LogInformation("Successfully updated company {CompanyId}", id);
            return Map(existing);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating company {CompanyId}", id);
            return DomainErrors.General.ServerError;
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(
        Guid id, 
        Guid userId, 
        Guid? organizationId, 
        CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting company {CompanyId} for user {UserId}", id, userId);
        
        try
        {
            var deleted = await _repository.DeleteAsync(id, userId, organizationId, ct);
            
            if (!deleted)
            {
                _logger.LogWarning("Company {CompanyId} not found for deletion", id);
                return DomainErrors.Company.NotFound;
            }
            
            _logger.LogInformation("Successfully deleted company {CompanyId}", id);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting company {CompanyId}", id);
            return DomainErrors.General.ServerError;
        }
    }

    private static CompanyDto Map(Company c) => new(
        c.Id, 
        c.Name, 
        c.Domain, 
        c.Industry, 
        c.Size
    );
}
