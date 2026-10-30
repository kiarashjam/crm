using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;

namespace ACI.Application.Tests.Services;

/// <summary>
/// A real <see cref="LeadStatusSync"/> over mocked storage, remembering what it
/// recorded, for service tests that need the status to follow the pipeline.
/// </summary>
internal sealed class StatusSyncHarness
{
    private readonly Dictionary<Guid, List<LeadStatus>> _statuses = new();

    public Mock<ILeadStatusRepository> Statuses { get; } = new();
    public Mock<ILeadOpsRepository> Ops { get; } = new();
    public Mock<IActivityRepository> Activities { get; } = new();
    public Mock<IAutomationEngine> Automations { get; } = new();

    public List<LeadStatusEvent> Events { get; } = new();
    public List<Activity> Logged { get; } = new();
    public List<(string Trigger, AutomationEvent Event)> Dispatched { get; } = new();

    public StatusSyncHarness()
    {
        Statuses.Setup(s => s.GetByOrganizationIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid org, CancellationToken _) =>
                _statuses.TryGetValue(org, out var list) ? list : (IReadOnlyList<LeadStatus>)Array.Empty<LeadStatus>());
        Ops.Setup(o => o.AddStatusEventAsync(It.IsAny<LeadStatusEvent>(), It.IsAny<CancellationToken>()))
            .Callback((LeadStatusEvent e, CancellationToken _) => Events.Add(e))
            .Returns(Task.CompletedTask);
        Activities.Setup(a => a.AddAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Activity a, CancellationToken _) => { Logged.Add(a); return a; });
        Automations.Setup(a => a.DispatchAsync(It.IsAny<string>(), It.IsAny<AutomationEvent>(), It.IsAny<CancellationToken>()))
            .Callback((string trigger, AutomationEvent e, CancellationToken _) => Dispatched.Add((trigger, e)))
            .Returns(Task.CompletedTask);
    }

    /// <summary>Give a workspace these statuses, in this order.</summary>
    public StatusSyncHarness WithStatuses(Guid organizationId, params string[] names)
    {
        _statuses[organizationId] = names
            .Select((name, i) => new LeadStatus { Id = Guid.NewGuid(), OrganizationId = organizationId, Name = name, DisplayOrder = i })
            .ToList();
        return this;
    }

    public Guid IdOf(Guid organizationId, string name) => _statuses[organizationId].Single(s => s.Name == name).Id;

    public LeadStatusSync Create(ILeadRepository leads, ILeadAutomation automation) =>
        new(leads, Statuses.Object, Ops.Object, automation, Activities.Object, Automations.Object);
}
