using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services;

/// <summary>Which modules a workspace has on, and turning them on and off (see <see cref="IWorkspaceModules"/>).</summary>
public sealed class WorkspaceModuleService : IWorkspaceModules
{
    private readonly AppDbContext _db;
    private readonly IOrganizationRepository _organizations;
    private readonly IClubRules _clubRules;
    private readonly ILogger<WorkspaceModuleService> _logger;

    // One read per request: a page's requests each ask, and the answer cannot change under
    // a request that is already running in any way that matters.
    private readonly Dictionary<Guid, (bool SalesOff, bool ClubOff)> _read = new();

    public WorkspaceModuleService(AppDbContext db, IOrganizationRepository organizations, IClubRules clubRules, ILogger<WorkspaceModuleService> logger)
    {
        _db = db;
        _organizations = organizations;
        _clubRules = clubRules;
        _logger = logger;
    }

    public async Task<bool> IsOnAsync(Guid organizationId, string module, CancellationToken ct = default)
    {
        if (!_read.TryGetValue(organizationId, out var off))
        {
            // Organizations are not filtered to a workspace (they are how a person finds theirs).
            var row = await _db.Organizations.AsNoTracking()
                .Where(o => o.Id == organizationId)
                .Select(o => new { o.SalesModuleOff, o.ClubModuleOff })
                .FirstOrDefaultAsync(ct);
            off = row == null ? (false, false) : (row.SalesModuleOff, row.ClubModuleOff);
            _read[organizationId] = off;
        }
        return module switch
        {
            WorkspaceModules.Sales => !off.SalesOff,
            WorkspaceModules.Club => !off.ClubOff,
            _ => true,
        };
    }

    public async Task<Result<WorkspaceModulesDto>> SetAsync(Guid organizationId, Guid userId, SetWorkspaceModulesRequest request, CancellationToken ct = default)
    {
        // What the workspace uses changes every member's menu and refuses whole areas of the
        // API, so it is for whoever manages the settings, as the currency and time zone are.
        var permissions = await _organizations.PermissionsAsync(userId, organizationId, ct);
        if (permissions == null) return DomainErrors.Organization.NotMember;
        if (!permissions.Contains(MemberPermissions.ManageSettings))
            return DomainErrors.Permission.Missing(MemberPermissions.ManageSettings, "turn the workspace's modules on or off");

        var org = await _db.Organizations.FirstOrDefaultAsync(o => o.Id == organizationId, ct);
        if (org == null) return DomainErrors.Organization.NotFound;

        var salesOn = request.Sales ?? !org.SalesModuleOff;
        var clubOn = request.Club ?? !org.ClubModuleOff;
        if (!salesOn && !clubOn) return DomainErrors.Modules.OneStaysOn;

        if (!salesOn && !org.SalesModuleOff)
        {
            var running = await SalesStillRunningAsync(organizationId, ct);
            if (running.Count > 0) return DomainErrors.Modules.StillRunning(WorkspaceModules.Sales, running);
        }
        if (!clubOn && !org.ClubModuleOff)
        {
            var running = await ClubStillRunningAsync(organizationId, ct);
            if (running.Count > 0) return DomainErrors.Modules.StillRunning(WorkspaceModules.Club, running);
        }

        if (org.SalesModuleOff == !salesOn && org.ClubModuleOff == !clubOn) return new WorkspaceModulesDto(salesOn, clubOn);
        org.SalesModuleOff = !salesOn;
        org.ClubModuleOff = !clubOn;
        await _db.SaveChangesAsync(ct);
        _read[organizationId] = (org.SalesModuleOff, org.ClubModuleOff);
        _logger.LogInformation("Modules of organization {OrganizationId} set to sales {Sales}, club {Club} by {UserId}",
            organizationId, salesOn ? "on" : "off", clubOn ? "on" : "off", userId);
        return new WorkspaceModulesDto(salesOn, clubOn);
    }

    /// <summary>
    /// What the club does on its own, which nobody could see or stop with its pages gone: the
    /// daily billing run's switches, and campaigns waiting to go out or going out.
    /// </summary>
    private async Task<IReadOnlyList<string>> ClubStillRunningAsync(Guid organizationId, CancellationToken ct)
    {
        var running = new List<string>();
        var billing = (await _clubRules.ReadAsync(organizationId, ct)).Billing;
        if (billing.AutomaticBilling) running.Add("automatic billing");
        if (billing.RemindersEnabled) running.Add("payment reminders");
        if (billing.LapseEnabled) running.Add("lapsing members who do not pay");
        if (billing.MonthlyStatements) running.Add("monthly statements");
        if (running.Count > 0) running[^1] += " (Club settings, Billing)";

        var campaigns = await _db.Campaigns.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId
                && (c.Status == CampaignStatuses.Scheduled || c.Status == CampaignStatuses.Sending))
            .CountAsync(ct);
        if (campaigns > 0)
            running.Add(campaigns == 1 ? "a campaign scheduled or sending (Campaigns)" : $"{campaigns} campaigns scheduled or sending (Campaigns)");
        return running;
    }

    /// <summary>What sales does on its own: the outreach sequences that email leads on their schedule.</summary>
    private async Task<IReadOnlyList<string>> SalesStillRunningAsync(Guid organizationId, CancellationToken ct)
    {
        var enrolled = await _db.OutreachEnrollments.AsNoTracking()
            .Where(e => e.OrganizationId == organizationId && e.Status == "active")
            .CountAsync(ct);
        return enrolled == 0 ? []
            : [enrolled == 1 ? "a lead still in an outreach sequence (Sequences)" : $"{enrolled} leads still in outreach sequences (Sequences)"];
    }
}
