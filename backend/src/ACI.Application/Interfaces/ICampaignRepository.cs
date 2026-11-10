using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

/// <summary>A campaign the scheduler should take up: scheduled with its time come, or sending with its sender gone.</summary>
public sealed record DueCampaign(Guid Id, Guid OrganizationId);

/// <summary>What the outbox says happened to one message, from which a member message's status is worked out.</summary>
public sealed record OutboundDelivery(
    Guid Id, string Status, string? DeliveryStatus, string? DeliveryDetail, string? LastError,
    DateTime? SentAtUtc, DateTime? DeliveryUpdatedAtUtc, DateTime? OpenedAtUtc);

/// <summary>How many of a campaign's messages share the facts a status is worked out from.</summary>
public sealed record CampaignTally(Guid CampaignId, string Status, string? OutboundStatus, string? DeliveryStatus, bool Opened, int Count);

/// <summary>Campaigns and the messages to members they and staff send.</summary>
public interface ICampaignRepository
{
    Task<IReadOnlyList<Campaign>> ListAsync(Guid organizationId, CancellationToken ct = default);

    /// <summary>The campaign, tracked, for an edit.</summary>
    Task<Campaign?> GetAsync(Guid organizationId, Guid id, CancellationToken ct = default);

    /// <summary>The campaign as it is now in the database, untracked.</summary>
    Task<Campaign?> ReadAsync(Guid organizationId, Guid id, CancellationToken ct = default);

    Task AddAsync(Campaign campaign, CancellationToken ct = default);

    /// <summary>Saves an edit; throws <see cref="Common.ConcurrencyConflictException"/> when the campaign changed since it was read.</summary>
    Task SaveAsync(Campaign campaign, Guid? expectedVersion, CancellationToken ct = default);

    /// <summary>Deletes a campaign that is still a draft or scheduled; false when it started sending meanwhile.</summary>
    Task<bool> DeleteUnsentAsync(Guid organizationId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Starts sending at the press of Send, in one conditional update: a draft or a scheduled
    /// campaign (at <paramref name="expectedVersion"/> when one is given) becomes Sending,
    /// held until <paramref name="claimedUntilUtc"/>. False when it was in neither state, or
    /// had changed: it is being sent, has been, or someone edited it meanwhile.
    /// </summary>
    Task<bool> TryClaimToSendAsync(Guid organizationId, Guid id, Guid? expectedVersion, Guid sentByUserId, DateTime utcNow, DateTime claimedUntilUtc, CancellationToken ct = default);

    /// <summary>The scheduler's claim: a scheduled campaign whose time has come, or a sending whose claim ran out.</summary>
    Task<bool> TryClaimDueAsync(Guid id, DateTime utcNow, DateTime claimedUntilUtc, CancellationToken ct = default);

    /// <summary>Holds the claim longer while a large campaign is still being worked through.</summary>
    Task ExtendClaimAsync(Guid id, DateTime claimedUntilUtc, CancellationToken ct = default);

    /// <summary>Lets the claim run out at <paramref name="retryAtUtc"/>, so the scheduler takes it up again then.</summary>
    Task ReleaseClaimAsync(Guid id, DateTime retryAtUtc, CancellationToken ct = default);

    /// <summary>A claimed campaign is sent: every member it was for has a message.</summary>
    Task FinishAsync(Guid id, DateTime sentAtUtc, int recipientCount, CancellationToken ct = default);

    /// <summary>Across workspaces, for the scheduler: campaigns due, or whose sender stopped.</summary>
    Task<IReadOnlyList<DueCampaign>> DueAsync(DateTime utcNow, int take, CancellationToken ct = default);

    // ── Member messages ───────────────────────────────────────────────────────

    /// <summary>The newest messages, all of the club's or one member's or one campaign's.</summary>
    Task<IReadOnlyList<MemberMessage>> MessagesAsync(Guid organizationId, Guid? memberId, Guid? campaignId, int take, CancellationToken ct = default);

    Task<MemberMessage?> GetMessageAsync(Guid organizationId, Guid id, CancellationToken ct = default);

    /// <summary>The members a campaign already has a message for: the address it was for, and whether it went to the outbox.</summary>
    Task<IReadOnlyList<(Guid MemberId, string? Email, string Status)>> RecipientsOfAsync(Guid campaignId, CancellationToken ct = default);

    /// <summary>
    /// Adds a member message. False when its campaign already has one for that member: the
    /// unique index refused it, and nothing of it is kept. Any other failure to save throws.
    /// </summary>
    Task<bool> AddMessageAsync(MemberMessage message, CancellationToken ct = default);

    Task SaveMessageAsync(MemberMessage message, CancellationToken ct = default);

    Task RemoveMessageAsync(MemberMessage message, CancellationToken ct = default);

    /// <summary>Forgets a message this unit of work holds, after the transaction it was written in rolled back.</summary>
    void Forget(MemberMessage message);

    /// <summary>What the outbox says about these messages.</summary>
    Task<IReadOnlyList<OutboundDelivery>> DeliveriesAsync(IReadOnlyCollection<Guid> outboundIds, CancellationToken ct = default);

    /// <summary>Counted per campaign, by what each message's status is worked out from.</summary>
    Task<IReadOnlyList<CampaignTally>> TallyAsync(Guid organizationId, IReadOnlyCollection<Guid> campaignIds, CancellationToken ct = default);

    /// <summary>Runs <paramref name="work"/> in one transaction, committed only when it returns true.</summary>
    Task<bool> InTransactionAsync(Func<Task<bool>> work, CancellationToken ct = default);
}
