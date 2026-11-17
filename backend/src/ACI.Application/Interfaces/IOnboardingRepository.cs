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

    /// <summary>
    /// The workspace's club member a handoff is for, tracked, if there is one: the one linked to
    /// the deal's contact or lead (a countersigned membership contract may have admitted them
    /// before the handoff), else the one with this email.
    /// </summary>
    Task<ClubMember?> ClubMemberForAsync(Guid organizationId, string email, Guid? contactId, Guid? leadId, CancellationToken ct = default);

    /// <summary>
    /// Who referred the person a handoff is for, as the CRM knows it: the "Referred by" contact of
    /// the lead the deal came from, else the referrer of their club member here. Null when nobody
    /// is recorded, or the referrer has no email to be found by.
    /// </summary>
    /// <summary>The latest handoff of the person who came from this lead, or is this club member; null when none was made.</summary>
    Task<DealOnboarding?> LatestHandoffForAsync(Guid organizationId, Guid? leadId, Guid? clubMemberId, CancellationToken ct = default);

    Task<(string Name, string Email)?> ReferrerOfAsync(Guid organizationId, Guid dealId, Guid? leadId, Guid? clubMemberId, CancellationToken ct = default);

    /// <summary>Keeps every change made through this repository; a concurrent change to a handoff throws ConcurrencyConflictException.</summary>
    Task SaveAsync(CancellationToken ct = default);

    /// <summary>Forget a handoff read through here, so the next read gets what is stored now.</summary>
    void Forget(DealOnboarding onboarding);
}
