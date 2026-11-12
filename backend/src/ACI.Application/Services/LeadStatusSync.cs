using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using static ACI.Application.Common.LeadStatusDerivation;

namespace ACI.Application.Services;

/// <summary>
/// Keeps a lead's status true to its pipeline, on the server, for every code path
/// that writes the pipeline: the lead editor, an e-signed contract, a merge, an API
/// caller.
/// </summary>
/// <remarks>
/// The rule itself is <see cref="LeadStatusDerivation"/>; this applies it to a lead
/// with the organisation's own status list and records what it did. The browser
/// still previews the outcome, but the status it shows after a save is the one this
/// wrote.
/// </remarks>
public interface ILeadStatusSync
{
    /// <summary>
    /// Works out the status the lead's pipeline implies and, when it should move,
    /// writes the name and its id onto the lead. Does not save. The lead's current
    /// status is the starting point, so a status sent in the same request counts.
    /// </summary>
    Task<Plan> SettleAsync(Lead lead, CancellationToken ct = default);

    /// <summary>
    /// After a save that changed the lead's status, for whatever reason: the status
    /// history row, the timeline entry explaining an automatic move, and the
    /// workspace's lead.status_changed automations.
    /// </summary>
    /// <param name="actorUserId">Who the automations and the timeline entry act for.</param>
    /// <param name="changedByUserId">Who the history credits; null when nobody in the workspace did it (a counterparty declining).</param>
    /// <param name="plan">The decision from <see cref="SettleAsync"/>, when the move came from it.</param>
    /// <param name="runAutomations">False for an import that was not asked to run them: the history still records the move.</param>
    /// <param name="fromImport">The move came from an import row, which the automations are told of.</param>
    Task AnnounceAsync(Lead lead, string? fromStatus, Guid actorUserId, Guid? changedByUserId, Plan? plan, CancellationToken ct = default, bool runAutomations = true, bool fromImport = false);

    /// <summary>
    /// For a writer outside the lead editor that has already changed the pipeline:
    /// settles the status, counts a move off "New" as the first response, rescores,
    /// saves, and announces. Null when the lead is no longer there to save.
    /// </summary>
    Task<Lead?> SaveAsync(Lead lead, Guid userId, Guid? organizationId, Guid? changedByUserId, CancellationToken ct = default);
}

public sealed class LeadStatusSync : ILeadStatusSync
{
    private readonly ILeadRepository _leads;
    private readonly ILeadStatusRepository _statuses;
    private readonly ILeadOpsRepository _ops;
    private readonly ILeadAutomation _automation;
    private readonly IActivityRepository _activities;
    private readonly IAutomationEngine? _automations;

    public LeadStatusSync(
        ILeadRepository leads,
        ILeadStatusRepository statuses,
        ILeadOpsRepository ops,
        ILeadAutomation automation,
        IActivityRepository activities,
        IAutomationEngine? automations = null)
    {
        _leads = leads;
        _statuses = statuses;
        _ops = ops;
        _automation = automation;
        _activities = activities;
        _automations = automations;
    }

    public async Task<Plan> SettleAsync(Lead lead, CancellationToken ct = default)
    {
        // A personal workspace has no status list of its own, and the browser never
        // derives there either: with nothing to choose from, inventing a status would
        // break the one promise this makes.
        var options = lead.OrganizationId is Guid orgId
            ? (await _statuses.GetByOrganizationIdAsync(orgId, ct))
                .Select(s => new StatusOption(s.Id.ToString(), s.Name, s.DisplayOrder))
                .ToList()
            : new List<StatusOption>();

        var plan = PlanStatusSync(new Context
        {
            Pipeline = ParsePipeline(lead.PipelineState),
            CurrentStatus = lead.Status,
            StatusOptions = options,
            StatusesLoaded = options.Count > 0,
            StatusesUnavailable = options.Count == 0,
            IsConverted = lead.IsConverted,
        });

        if (plan.Applies)
        {
            lead.Status = plan.To!.Name;
            // The id goes with the name, or the lead keeps an id for a status it no
            // longer has.
            if (Guid.TryParse(plan.To.Id, out var statusId)) lead.LeadStatusId = statusId;
        }
        return plan;
    }

    public async Task AnnounceAsync(Lead lead, string? fromStatus, Guid actorUserId, Guid? changedByUserId, Plan? plan, CancellationToken ct = default, bool runAutomations = true, bool fromImport = false)
    {
        if (string.Equals(fromStatus, lead.Status, StringComparison.Ordinal)) return;

        // A change of letter case alone is not a move worth a line in the history.
        if (fromStatus == null || !string.Equals(fromStatus, lead.Status, StringComparison.OrdinalIgnoreCase))
        {
            await _ops.AddStatusEventAsync(new LeadStatusEvent
            {
                Id = Guid.NewGuid(),
                LeadId = lead.Id,
                OrganizationId = lead.OrganizationId,
                FromStatus = fromStatus,
                ToStatus = lead.Status,
                ChangedByUserId = changedByUserId,
                ChangedAtUtc = DateTime.UtcNow,
            }, ct);
        }

        // Only when the pipeline moved it. The browser used to write this entry after
        // its own save, so a move made by a contract or a merge left no word of why.
        if (plan is { Applies: true } && string.Equals(plan.To!.Name, lead.Status, StringComparison.Ordinal))
        {
            var (subject, body) = DescribeChange(fromStatus ?? "", plan.To, plan.Derived);
            await _activities.AddAsync(new Activity
            {
                Id = Guid.NewGuid(),
                UserId = actorUserId,
                OrganizationId = lead.OrganizationId,
                Type = "system",
                Subject = subject,
                Body = body,
                LeadId = lead.Id,
                CreatedAtUtc = DateTime.UtcNow,
            }, ct);
        }

        if (_automations != null && runAutomations)
        {
            await _automations.DispatchAsync("lead.status_changed", new AutomationEvent(
                lead.OrganizationId, actorUserId, $"{lead.Id}:{lead.Status}", lead.Name,
                LeadId: lead.Id, Email: LeadKindRules.EmailOf(lead) ?? "", AssigneeId: lead.AssignedToUserId, FromImport: fromImport), ct);
        }
    }

    public async Task<Lead?> SaveAsync(Lead lead, Guid userId, Guid? organizationId, Guid? changedByUserId, CancellationToken ct = default)
    {
        var from = lead.Status;
        var plan = await SettleAsync(lead, ct);
        var moved = !string.Equals(from, lead.Status, StringComparison.Ordinal);
        if (moved)
        {
            // The same rule the lead editor applies: a lead whose status has left
            // "New" has been worked, so its first-response clock stops.
            if (lead.FirstResponseAtUtc == null && !string.Equals(lead.Status.Trim(), "New", StringComparison.OrdinalIgnoreCase))
            {
                lead.FirstResponseAtUtc = DateTime.UtcNow;
                lead.SlaBreached = false;
            }
            // "Reached stage" score rules read the status.
            await _automation.RescoreAsync(lead, ct);
        }

        var saved = await _leads.UpdateAsync(lead, userId, organizationId, ct);
        if (saved != null && moved)
            await AnnounceAsync(saved, from, userId, changedByUserId, plan, ct);
        return saved;
    }
}
