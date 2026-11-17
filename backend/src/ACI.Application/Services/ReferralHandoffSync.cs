using System.Text.Json;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Services;

/// <summary>
/// Tells the workspace's portal who referred someone it already welcomed, when that is recorded
/// here after the handoff: on the lead they came from ("Referred by") or on their club member.
/// The portal credits the referrer, as it does for a referrer named in the handoff itself.
/// </summary>
public interface IReferralHandoffSync
{
    Task LeadReferrerChangedAsync(Guid organizationId, Guid leadId, CancellationToken ct = default);
    Task MemberReferrerChangedAsync(Guid organizationId, Guid clubMemberId, CancellationToken ct = default);
}

/// <remarks>
/// A signed <c>member.referrer</c> event to the portal's handoff address, through the outbox, which
/// retries it as it retries a handoff. Nothing goes for someone not handed over yet (the handoff
/// will carry the referrer), nor when a referrer is taken off: the portal keeps credit given.
/// </remarks>
public sealed class ReferralHandoffSync : IReferralHandoffSync
{
    public const string ReferrerEvent = "member.referrer";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IOnboardingRepository _onboarding;
    private readonly IOutboxRepository _outbox;
    private readonly IOutboxSignal _signal;
    private readonly ILogger<ReferralHandoffSync> _logger;

    public ReferralHandoffSync(IOnboardingRepository onboarding, IOutboxRepository outbox, IOutboxSignal signal, ILogger<ReferralHandoffSync> logger)
    {
        _onboarding = onboarding;
        _outbox = outbox;
        _signal = signal;
        _logger = logger;
    }

    public Task LeadReferrerChangedAsync(Guid organizationId, Guid leadId, CancellationToken ct = default) =>
        SyncAsync(organizationId, leadId, null, ct);

    public Task MemberReferrerChangedAsync(Guid organizationId, Guid clubMemberId, CancellationToken ct = default) =>
        SyncAsync(organizationId, null, clubMemberId, ct);

    private async Task SyncAsync(Guid organizationId, Guid? leadId, Guid? clubMemberId, CancellationToken ct)
    {
        try
        {
            var handoff = await _onboarding.LatestHandoffForAsync(organizationId, leadId, clubMemberId, ct);
            if (handoff == null) return;
            var settings = await _onboarding.GetSettingsAsync(organizationId, ct);
            if (string.IsNullOrWhiteSpace(settings?.WebhookUrl) || string.IsNullOrWhiteSpace(settings.WebhookSecret)) return;
            var referrer = await _onboarding.ReferrerOfAsync(organizationId, handoff.DealId, handoff.LeadId ?? leadId, handoff.ClubMemberId ?? clubMemberId, ct);
            if (referrer is not { } by) return;

            var payload = new
            {
                version = 1,
                onboardingId = handoff.Id,
                contact = new { name = handoff.Name, email = handoff.Email },
                referrer = new { name = by.Name, email = by.Email },
            };
            var now = DateTime.UtcNow;
            await _outbox.AddAsync(new OutboundMessage
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Channel = OutboundChannels.Webhook,
                // To the portal's handoff address, signed with its secret, as a handoff is.
                Source = OutboundSources.Onboarding,
                SourceId = handoff.Id,
                Status = OutboundStatuses.Queued,
                NextAttemptAtUtc = now,
                CreatedAtUtc = now,
                EventName = ReferrerEvent,
                PayloadJson = JsonSerializer.Serialize(payload, Json),
                DealId = handoff.DealId,
                ContactId = handoff.ContactId,
            }, ct);
            _signal.Notify();
            _logger.LogInformation("Referrer of handoff {OnboardingId} queued for the portal", handoff.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The referrer is saved here whatever happens; the portal hears of it with the next handoff or resend.
            _logger.LogError(ex, "The referrer could not be queued for the portal (lead {LeadId}, member {MemberId})", leadId, clubMemberId);
        }
    }
}
