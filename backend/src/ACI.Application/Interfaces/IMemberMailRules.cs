namespace ACI.Application.Interfaces;

/// <summary>
/// What the outbox asks before it emails a club member, at the moment the email is queued
/// and again when it goes: does the member still exist, and may marketing go to them.
/// </summary>
public interface IMemberMailRules
{
    /// <summary>Whether the member is still one of the workspace's.</summary>
    Task<bool> MemberExistsAsync(Guid organizationId, Guid memberId, CancellationToken ct = default);

    /// <summary>
    /// Why news and offers may not go to the member on <paramref name="channel"/>, in words
    /// fit to show the sender, or null when they agreed and have not withdrawn it.
    /// </summary>
    Task<MailRefusal?> MarketingRefusalAsync(Guid organizationId, Guid memberId, string channel, CancellationToken ct = default);

    /// <summary>The language staff set for the member (en, de, fr), or null when none is set.</summary>
    Task<string?> PreferredLanguageAsync(Guid organizationId, Guid memberId, CancellationToken ct = default);
}

/// <param name="Code">Consent.Missing or Consent.Withdrawn.</param>
/// <param name="Reason">A sentence for the person who sent it.</param>
public sealed record MailRefusal(string Code, string Reason);
