using System.Collections.Concurrent;
using ACI.Application.Interfaces;

namespace ACI.WebApi.Services;

/// <summary>Each workspace's last portal ping, in memory: one singleton, written by the daily ping and by "Test the connection".</summary>
public sealed class PortalPings : IPortalPings
{
    private readonly ConcurrentDictionary<Guid, PortalPingResult> _last = new();

    public void Record(Guid organizationId, PortalPingResult result) => _last[organizationId] = result;

    public PortalPingResult? For(Guid organizationId) => _last.TryGetValue(organizationId, out var result) ? result : null;

    public IReadOnlyDictionary<Guid, PortalPingResult> Snapshot() => new Dictionary<Guid, PortalPingResult>(_last);

    public void Forget(Guid organizationId) => _last.TryRemove(organizationId, out _);
}
