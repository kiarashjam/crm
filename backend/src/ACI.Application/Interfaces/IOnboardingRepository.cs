using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

/// <summary>Where each workspace hands new customers over, and each handoff since.</summary>
public interface IOnboardingRepository
{
    /// <summary>The workspace's settings, tracked, or null before anyone saved them.</summary>
    Task<OnboardingSettings?> GetSettingsAsync(Guid organizationId, CancellationToken ct = default);

    void AddSettings(OnboardingSettings settings);

    void Add(DealOnboarding onboarding);

    /// <summary>One handoff in the workspace, tracked.</summary>
    Task<DealOnboarding?> GetAsync(Guid id, Guid organizationId, CancellationToken ct = default);

    /// <summary>
    /// One handoff, tracked, whichever workspace it belongs to: for the receiver's reports,
    /// which name the handoff before anyone acts for its workspace.
    /// </summary>
    Task<DealOnboarding?> GetInAnyWorkspaceAsync(Guid id, CancellationToken ct = default);

    /// <summary>A deal's handoffs, newest first.</summary>
    Task<IReadOnlyList<DealOnboarding>> ForDealAsync(Guid dealId, Guid organizationId, CancellationToken ct = default);

    /// <summary>The outbox messages that carried these handoffs, by id.</summary>
    Task<IReadOnlyDictionary<Guid, OutboundMessage>> MessagesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>The workspace's club member with this email, tracked, if there is one.</summary>
    Task<ClubMember?> ClubMemberByEmailAsync(Guid organizationId, string email, CancellationToken ct = default);

    /// <summary>Keeps every change made through this repository; a concurrent change to a handoff throws ConcurrencyConflictException.</summary>
    Task SaveAsync(CancellationToken ct = default);

    /// <summary>Forget a handoff read through here, so the next read gets what is stored now.</summary>
    void Forget(DealOnboarding onboarding);
}
