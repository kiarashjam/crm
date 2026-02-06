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
        
        var leadsTask = _leadService.SearchAsync(userId, organizationId, q, ct);
        var contactsTask = _contactService.SearchAsync(userId, organizationId, q, false, ct); // Don't include archived contacts
        var companiesTask = _companyService.SearchAsync(userId, organizationId, q, ct);
        var dealsTask = _dealService.SearchAsync(userId, organizationId, q, ct);

        await Task.WhenAll(leadsTask, contactsTask, companiesTask, dealsTask);

        var leads = (await leadsTask).Take(MaxPerType).ToList();
        var contacts = (await contactsTask).Take(MaxPerType).ToList();
        var companies = (await companiesTask).Take(MaxPerType).ToList();
        var deals = (await dealsTask).Take(MaxPerType).ToList();

        _logger.LogInformation(
            "Global search completed for user {UserId}: {LeadCount} leads, {ContactCount} contacts, {CompanyCount} companies, {DealCount} deals",
            userId, leads.Count, contacts.Count, companies.Count, deals.Count);

        return new GlobalSearchResultDto(leads, contacts, companies, deals);
    }
}
