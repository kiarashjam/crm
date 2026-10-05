using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// Service for performing global searches across leads, contacts, companies, and deals.
/// </summary>
public class GlobalSearchService : IGlobalSearchService
{
    private const int MaxPerType = 10;
    private readonly ILeadService _leadService;
    private readonly IContactService _contactService;
    private readonly ICompanyService _companyService;
    private readonly IDealService _dealService;
    private readonly ILogger<GlobalSearchService> _logger;

    public GlobalSearchService(
        ILeadService leadService,
        IContactService contactService,
        ICompanyService companyService,
        IDealService dealService,
        ILogger<GlobalSearchService> logger)
    {
        _leadService = leadService;
        _contactService = contactService;
        _companyService = companyService;
        _dealService = dealService;
        _logger = logger;
    }

    public async Task<GlobalSearchResultDto> SearchAsync(Guid userId, Guid? organizationId, string query, CancellationToken ct = default)
    {
        var q = (query ?? "").Trim();
        
        _logger.LogDebug("Global search for user {UserId}, query: '{Query}'", userId, q);

        if (q.Length < 2)
            return new GlobalSearchResultDto([], [], [], []);

        // One DbContext cannot run queries in parallel. Each search is capped in SQL
        // so four small sequential reads stay faster than four full-table loads.
        var leads = (await _leadService.SearchAsync(userId, organizationId, q, ct, MaxPerType)).ToList();
        var contacts = (await _contactService.SearchAsync(userId, organizationId, q, false, ct, MaxPerType)).ToList();
        var companies = (await _companyService.SearchAsync(userId, organizationId, q, ct, MaxPerType)).ToList();
        var deals = (await _dealService.SearchAsync(userId, organizationId, q, ct, MaxPerType)).ToList();

        _logger.LogInformation(
            "Global search completed for user {UserId}: {LeadCount} leads, {ContactCount} contacts, {CompanyCount} companies, {DealCount} deals",
            userId, leads.Count, contacts.Count, companies.Count, deals.Count);

        return new GlobalSearchResultDto(leads, contacts, companies, deals);
    }
}
