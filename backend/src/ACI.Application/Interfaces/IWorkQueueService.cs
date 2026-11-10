using ACI.Application.DTOs;

namespace ACI.Application.Interfaces;

/// <summary>The open tasks, leads and deals that are a rep's work, or the team's.</summary>
public interface IWorkQueueService
{
    /// <param name="scope">"mine" (the default) or "team".</param>
    /// <param name="limit">The most of each kind to return.</param>
    Task<WorkQueueDto> GetAsync(Guid userId, Guid? organizationId, string? scope, int limit, CancellationToken ct = default);
}
