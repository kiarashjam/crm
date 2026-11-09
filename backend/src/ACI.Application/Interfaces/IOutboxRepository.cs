using ACI.Domain.Entities;

namespace ACI.Application.Interfaces;

/// <summary>A queued message due for an attempt, with the attempt count it was read at.</summary>
public sealed record DueOutboundMessage(Guid Id, int Attempts);

/// <summary>
/// The outbox's storage: outbound messages, the suppression list the mail service
/// fills, and the replies matched back to messages.
/// </summary>
public interface IOutboxRepository
{
    Task AddAsync(OutboundMessage message, CancellationToken ct = default);

    /// <summary>One message, tracked, so changes to it are kept by <see cref="SaveAsync"/>.</summary>
    Task<OutboundMessage?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Keeps the changes made to messages read through this repository.</summary>
    Task SaveAsync(CancellationToken ct = default);

    /// <summary>Queued messages due now and held by nobody, the longest waiting first.</summary>
    Task<IReadOnlyList<DueOutboundMessage>> DueAsync(DateTime utcNow, int take, CancellationToken ct = default);

    /// <summary>
    /// Atomically take a due message for one attempt: true for exactly one caller. It
    /// moves only while it is still queued, due, held by nobody and on
    /// <paramref name="attempts"/>, and comes out held until <paramref name="claimedUntilUtc"/>
    /// with its attempt counted.
    /// </summary>
    Task<bool> TryClaimAsync(Guid id, int attempts, DateTime utcNow, DateTime claimedUntilUtc, CancellationToken ct = default);

    /// <summary>
    /// Atomically cancel a queued message that nobody is sending at this moment. False
    /// when it has gone, has already stopped, or is being attempted right now.
    /// </summary>
    Task<bool> TryCancelAsync(Guid id, DateTime utcNow, CancellationToken ct = default);

    /// <summary>Whether something queued for this sender record (a sequence enrollment, say) has still not gone.</summary>
    Task<bool> HasQueuedAsync(Guid sourceId, CancellationToken ct = default);

    /// <summary>The email with this Message-ID header, angle brackets included or not.</summary>
    Task<OutboundMessage?> FindByMessageIdAsync(string messageIdHeader, CancellationToken ct = default);

    /// <summary>The email whose reply-capture address carries this token.</summary>
    Task<OutboundMessage?> FindByReplyTokenAsync(string token, CancellationToken ct = default);

    /// <summary>A workspace's latest webhook deliveries, newest first.</summary>
    Task<IReadOnlyList<OutboundMessage>> WebhookDeliveriesAsync(Guid organizationId, int take, CancellationToken ct = default);

    /// <summary>The first of these addresses on the workspace's suppression list, or null.</summary>
    Task<EmailSuppression?> FindSuppressionAsync(Guid organizationId, IReadOnlyCollection<string> addresses, CancellationToken ct = default);

    /// <summary>Puts an address on the workspace's suppression list. False when it was on it already.</summary>
    Task<bool> SuppressAsync(EmailSuppression suppression, CancellationToken ct = default);

    /// <summary>Whether a reply with this Message-ID has been taken in for the workspace already.</summary>
    Task<bool> InboundSeenAsync(Guid organizationId, string messageIdHeader, CancellationToken ct = default);

    /// <summary>
    /// Records a reply taken in. False when the same reply (by its Message-ID) was recorded
    /// by another post in the meantime.
    /// </summary>
    Task<bool> TryAddInboundAsync(InboundEmail inbound, CancellationToken ct = default);
}
