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
    private readonly IActivityService _activities;
    private readonly ITaskService _tasks;
    private readonly ILogger<GlobalSearchService> _logger;

    public GlobalSearchService(
        ILeadService leadService,
        IContactService contactService,
        ICompanyService companyService,
        IDealService dealService,
        IActivityService activities,
        ITaskService tasks,
        ILogger<GlobalSearchService> logger)
    {
        _leadService = leadService;
        _contactService = contactService;
        _companyService = companyService;
        _dealService = dealService;
        _activities = activities;
        _tasks = tasks;
        _logger = logger;
    }

    public async Task<GlobalSearchResultDto> SearchAsync(Guid userId, Guid? organizationId, string query, CancellationToken ct = default)
    {
        var q = (query ?? "").Trim();
        
        _logger.LogDebug("Global search for user {UserId}, query: '{Query}'", userId, q);

        if (q.Length < 2)
            return new GlobalSearchResultDto([], [], [], [], [], []);

        // One DbContext cannot run queries in parallel. Each search is capped in SQL
        // so four small sequential reads stay faster than four full-table loads.
        var leads = (await _leadService.SearchAsync(userId, organizationId, q, ct, MaxPerType)).ToList();
        var contacts = (await _contactService.SearchAsync(userId, organizationId, q, false, ct, MaxPerType)).ToList();
        var companies = (await _companyService.SearchAsync(userId, organizationId, q, ct, MaxPerType)).ToList();
        var deals = (await _dealService.SearchAsync(userId, organizationId, q, ct, MaxPerType)).ToList();
        var activityPage = await _activities.GetPagedAsync(userId, organizationId, 1, MaxPerType, q, null, ct);
        var taskPage = await _tasks.GetPagedAsync(userId, organizationId, 1, MaxPerType, q, null, ct);
        var activities = activityPage.Items
            .Select(a => new SearchHitDto(a.Id, string.IsNullOrWhiteSpace(a.Subject) ? a.Type : a.Subject!, a.Type))
            .ToList();
        var tasks = taskPage.Items
            .Select(t => new SearchHitDto(t.Id, t.Title, t.Status.ToString()))
            .ToList();

        _logger.LogInformation(
            "Global search completed for user {UserId}: {LeadCount} leads, {ContactCount} contacts, {CompanyCount} companies, {DealCount} deals, {ActivityCount} activities, {TaskCount} tasks",
            userId, leads.Count, contacts.Count, companies.Count, deals.Count, activities.Count, tasks.Count);

        return new GlobalSearchResultDto(leads, contacts, companies, deals, activities, tasks);
    }
}
