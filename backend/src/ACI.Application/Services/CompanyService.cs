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

    /// <summary>The most rows one export writes, as for contacts.</summary>
    public const int MaxExportRows = 50_000;

    /// <inheritdoc />
    public async Task<PagedResult<CompanyDto>> GetCompaniesPagedAsync(
        Guid userId,
        Guid? organizationId,
        int page = 1,
        int pageSize = 20,
        string? search = null,
        CancellationToken ct = default)
    {
        var paged = await GetCompaniesPagedAsync(userId, organizationId, page, pageSize, new CompanyQueryOptions { Search = search }, ct);
        return PagedResult<CompanyDto>.Create(paged.Items.Select(Map).ToList(), paged.TotalCount, paged.Page, paged.PageSize);
    }

    /// <inheritdoc />
    public async Task<PagedResult<CompanyListItemDto>> GetCompaniesPagedAsync(
        Guid userId,
        Guid? organizationId,
        int page,
        int pageSize,
        CompanyQueryOptions options,
        CancellationToken ct = default)
    {
        _logger.LogDebug(
            "Getting paged companies for user {UserId}, organization {OrganizationId}, page {Page}, pageSize {PageSize}, search '{Search}'",
            userId, organizationId, page, pageSize, options.Search);

        page = Math.Max(page, 1);
        var (items, totalCount) = await _repository.GetPagedAsync(userId, organizationId, (page - 1) * pageSize, pageSize, options, ct);
        // The counts for this page's companies only; the page used to fetch them for every company.
        var stats = (await _repository.GetStatsAsync(items.Select(c => c.Id).ToList(), userId, organizationId, ct))
            .ToDictionary(s => s.CompanyId);
        var dtos = items.Select(c => stats.TryGetValue(c.Id, out var s)
            ? ListItem(c, s.ContactCount, s.DealCount, s.DealTotals)
            : ListItem(c, 0, 0, Array.Empty<CurrencyTotalDto>())).ToList();

        _logger.LogInformation(
            "Retrieved {Count} of {Total} companies for user {UserId} (page {Page})",
            items.Count, totalCount, userId, page);

        return PagedResult<CompanyListItemDto>.Create(dtos, totalCount, page, pageSize);
    }

    /// <inheritdoc />
    public Task<CompanySummaryDto> GetSummaryAsync(Guid userId, Guid? organizationId, CancellationToken ct = default) =>
        _repository.GetSummaryAsync(userId, organizationId, ct);

    /// <inheritdoc />
    public async Task<ExportFile> ExportAsync(Guid userId, Guid? organizationId, CompanyQueryOptions options, CancellationToken ct = default)
    {
        var (rows, total) = await _repository.GetExportRowsAsync(userId, organizationId, options, MaxExportRows, ct);
        var stats = new Dictionary<Guid, (Guid CompanyId, int ContactCount, int DealCount, IReadOnlyList<CurrencyTotalDto> DealTotals)>();
        foreach (var chunk in rows.Select(r => r.Id).Chunk(1000))
            foreach (var s in await _repository.GetStatsAsync(chunk, userId, organizationId, ct))
                stats[s.CompanyId] = s;

        var csv = new CsvExport("Name", "Domain", "Industry", "Size", "Website", "Location", "Contacts", "Deals", "Deal value", "Added (UTC)");
        foreach (var c in rows)
        {
            stats.TryGetValue(c.Id, out var s);
            // Each currency on its own, never added together: "CHF 40000; EUR 25000".
            var value = string.Join("; ", (s.DealTotals ?? Array.Empty<CurrencyTotalDto>()).Select(t => t.Currency + " " + CsvExport.Number(t.Value)));
            csv.Row(c.Name, c.Domain, c.Industry, c.Size, c.Website, c.Location,
                CsvExport.Number(s.ContactCount), CsvExport.Number(s.DealCount), value, CsvExport.Utc(c.CreatedAtUtc));
        }
        _logger.LogInformation("Exported {Count} of {Total} companies for user {UserId}", rows.Count, total, userId);
        return new ExportFile("companies-" + DateTime.UtcNow.ToString("yyyy-MM-dd") + ".csv", csv.ToBytes(), rows.Count, total);
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
        CancellationToken ct = default,
        int? take = null)
    {
        _logger.LogDebug("Searching companies for user {UserId} with query '{Query}'", userId, query);
        
        var trimmed = query ?? "";
        var list = take is > 0
            ? await _repository.SearchAsync(userId, organizationId, trimmed, ct, take)
            : await _repository.SearchAsync(userId, organizationId, trimmed, ct);
        
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

        // HP-5 + HIGH-2: Check for duplicate company name using efficient single-query EXISTS
        var nameExists = await _repository.ExistsByNameAsync(request.Name, userId, organizationId, ct);
        if (nameExists)
        {
            _logger.LogWarning("Company creation failed - duplicate name '{Name}'", request.Name);
            return DomainErrors.Company.DuplicateName;
        }

        try
        {
            // HP-10: Normalize empty/whitespace-only strings to null
            static string? NormalizeNullable(string? value)
            {
                var trimmed = value?.Trim();
                return string.IsNullOrEmpty(trimmed) ? null : trimmed;
            }

            var entity = new Company
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrganizationId = organizationId,
                Name = request.Name.Trim(),
                Domain = NormalizeNullable(request.Domain),
                Industry = NormalizeNullable(request.Industry),
                Size = NormalizeNullable(request.Size),
                Description = NormalizeNullable(request.Description),
                Website = NormalizeNullable(request.Website),
                Location = NormalizeNullable(request.Location),
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

        // Made from an older read: saving would write the form's older values over what
        // someone saved since. See UpdateCompanyRequest.Version.
        if (request.Version is Guid readVersion && readVersion != existing.Version)
            return DomainErrors.Company.Changed;
        
        // Validate domain format if provided
        if (!string.IsNullOrWhiteSpace(request.Domain) && !ValidationHelper.IsValidDomain(request.Domain))
        {
            _logger.LogWarning("Company update failed - invalid domain format: {Domain}", request.Domain);
            return DomainErrors.Company.DomainInvalid;
        }
        
        // HP-10: Normalize empty/whitespace-only strings to null for nullable fields
        static string? NormalizeNullable(string? value)
        {
            var trimmed = value?.Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }

        // A field sent empty, or as null, is taken off; one left out is kept.
        if (request.Name != null) existing.Name = request.Name.Trim();
        if (request.Domain != null || request.Clears(nameof(request.Domain))) existing.Domain = NormalizeNullable(request.Domain);
        if (request.Industry != null || request.Clears(nameof(request.Industry))) existing.Industry = NormalizeNullable(request.Industry);
        if (request.Size != null || request.Clears(nameof(request.Size))) existing.Size = NormalizeNullable(request.Size);
        if (request.Description != null || request.Clears(nameof(request.Description))) existing.Description = NormalizeNullable(request.Description);
        if (request.Website != null || request.Clears(nameof(request.Website))) existing.Website = NormalizeNullable(request.Website);
        if (request.Location != null || request.Clears(nameof(request.Location))) existing.Location = NormalizeNullable(request.Location);
        
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
        catch (ConcurrencyConflictException)
        {
            // Saved by someone else between this request reading the company and writing
            // it: an answer, not a server error.
            return DomainErrors.Company.Changed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating company {CompanyId}", id);
            return DomainErrors.General.ServerError;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CompanyStatsItemDto>> GetCompanyStatsAsync(
        Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        _logger.LogDebug("Getting company stats for user {UserId}, org {OrganizationId}", userId, organizationId);
        var raw = await _repository.GetStatsAsync(userId, organizationId, ct);
        return raw.Select(r => new CompanyStatsItemDto(r.CompanyId, r.ContactCount, r.DealCount, r.DealTotals)).ToList();
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

    /// <inheritdoc />
    public async Task<Result> MergeAsync(Guid keepId, Guid dropId, Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        if (keepId == dropId) return DomainErrors.Company.NotFound;
        var merged = await _repository.MergeIntoAsync(keepId, dropId, userId, organizationId, ct);
        if (!merged) return DomainErrors.Company.NotFound;
        _logger.LogInformation("Merged company {DropId} into {KeepId}", dropId, keepId);
        return Result.Success();
    }

    private static CompanyListItemDto ListItem(Company c, int contacts, int deals, IReadOnlyList<CurrencyTotalDto> totals) => new(
        c.Id, c.Name, c.Domain, c.Industry, c.Size, c.Description, c.Website, c.Location, c.CreatedAtUtc, c.UpdatedAtUtc,
        contacts, deals, totals, c.Version);

    /// <summary>A list item as the plain company, for the callers that ask for that.</summary>
    private static CompanyDto Map(CompanyListItemDto c) => new(
        c.Id, c.Name, c.Domain, c.Industry, c.Size, c.Description, c.Website, c.Location, c.CreatedAtUtc, c.UpdatedAtUtc, c.Version);

    private static CompanyDto Map(Company c) => new(
        c.Id, 
        c.Name, 
        c.Domain, 
        c.Industry, 
        c.Size,
        c.Description,
        c.Website,
        c.Location,
        c.CreatedAtUtc,
        c.UpdatedAtUtc,
        c.Version
    );
}
