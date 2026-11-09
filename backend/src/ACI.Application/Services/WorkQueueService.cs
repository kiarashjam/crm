using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;

namespace ACI.Application.Services;

/// <summary>
/// A rep's work: the open tasks, leads and deals that are theirs (see WorkScope), or with
/// the team switch every one in the workspace.
/// </summary>
/// <remarks>
/// <para>
/// The assistant's "do this next" queue was worked out from the whole workspace's lists,
/// so every rep was told to do every other rep's work. The server now says which records
/// are the rep's, and leaves out what is finished: completed and cancelled tasks,
/// converted leads, won deals and lost ones whose reason is recorded. What each record's
/// next step is stays where it is decided, in the assistant's rules.
/// </para>
/// <para>
/// Each list is cut at <see cref="MaxPerList"/> at most, the most pressing first (tasks by
/// due date, deals by close date, leads by when they were last touched), and
/// <see cref="WorkQueueDto.Truncated"/> says when any was cut.
/// </para>
/// </remarks>
public sealed class WorkQueueService : IWorkQueueService
{
    public const int DefaultPerList = 500;
    public const int MaxPerList = 1000;

    private readonly ITaskService _tasks;
    private readonly ILeadService _leads;
    private readonly IDealService _deals;

    public WorkQueueService(ITaskService tasks, ILeadService leads, IDealService deals)
    {
        _tasks = tasks;
        _leads = leads;
        _deals = deals;
    }

    public async Task<WorkQueueDto> GetAsync(Guid userId, Guid? organizationId, string? scope, int limit, CancellationToken ct = default)
    {
        // Mine unless the team was asked for: this is the rep's own queue.
        var mine = !string.Equals(scope?.Trim(), WorkScope.Team, StringComparison.OrdinalIgnoreCase);
        var take = Math.Clamp(limit, 1, MaxPerList);
        var (tasks, moreTasks) = await _tasks.GetOpenWorkAsync(userId, organizationId, mine, take, ct);
        var (leads, moreLeads) = await _leads.GetOpenWorkAsync(userId, organizationId, mine, take, ct);
        var (deals, moreDeals) = await _deals.GetOpenWorkAsync(userId, organizationId, mine, take, ct);
        return new WorkQueueDto(mine ? WorkScope.Mine : WorkScope.Team, tasks, leads, deals, moreTasks || moreLeads || moreDeals);
    }
}
