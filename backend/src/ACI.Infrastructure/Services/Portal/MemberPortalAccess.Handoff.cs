using System.Globalization;
using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services.Portal;

/// <summary>
/// The club's own portal opening a member's club account here, for a member it received
/// through the welcome hand-over (DealOnboarding), without a second sign-in.
/// </summary>
/// <remarks>
/// <para>
/// The club's portal holds the hand-over's secret already: it checks every hand-over with it.
/// It signs a request back the same way (X-Cadence-Timestamp, and X-Cadence-Signature-256 =
/// HMAC-SHA256 of "{timestamp}.{body}"), and gets a sign-in link for that hand-over's member
/// that works once, for two minutes; it sends the member's browser there.
/// </para>
/// <list type="bullet">
/// <item><b>Tied to the hand-over and its member.</b> The hand-over names the member it added;
/// no other member can be reached through it.</item>
/// <item><b>Fresh and used once.</b> A request more than five minutes from now is refused, and
/// each signature is taken once (kept as a sign-in step, which runs out with it).</item>
/// <item><b>Refused once the secret is replaced.</b> It is checked against the secret the
/// workspace has now, as the hand-over's progress reports are.</item>
/// </list>
/// <para>The hand-over's own contract (its payload, signatures, callback and events) is unchanged.</para>
/// </remarks>
public sealed partial class MemberPortalAccess
{
    public async Task<Result<PortalHandoffLinkDto>> HandoffLinkAsync(Guid onboardingId, string? timestamp, string? signature, string body, CancellationToken ct = default)
    {
        // The same answer for a hand-over that does not exist and a signature that does not
        // match, so the address tells a stranger nothing.
        var refused = DomainErrors.Portal.HandoffRefused;
        if (!long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)) return refused;
        DateTimeOffset at;
        try
        {
            at = DateTimeOffset.FromUnixTimeSeconds(seconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return refused;
        }
        var now = Now;
        if ((at.UtcDateTime - now).Duration() > HandoffFreshness) return refused;

        // Across workspaces: the club's portal names the hand-over, and its signature proves it.
        var onboarding = await _db.DealOnboardings.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(o => o.Id == onboardingId, ct);
        if (onboarding == null) return refused;
        using var acting = _tenant.ActFor(onboarding.OrganizationId, onboarding.CreatedByUserId);
        var handoff = await _db.OnboardingSettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == onboarding.OrganizationId, ct);
        if (string.IsNullOrWhiteSpace(handoff?.WebhookSecret) || !WebhookSigning.IsSignature(handoff.WebhookSecret, seconds, body, signature))
            return refused;

        // Each signed request once: a copy of it replayed within its five minutes is refused.
        var nonce = Hash(signature!.Trim().ToLowerInvariant());
        if (await _db.AuthChallenges.AnyAsync(c => c.Purpose == HandoffNoncePurpose && c.KeyHash == nonce, ct)) return refused;
        // Those that ran out are of no use any more: swept a few at a time, as new ones come.
        var stale = await _db.AuthChallenges
            .Where(c => c.Purpose == HandoffNoncePurpose && c.ExpiresAtUtc < now)
            .OrderBy(c => c.ExpiresAtUtc)
            .Take(SweepBatch)
            .ToListAsync(ct);
        _db.AuthChallenges.RemoveRange(stale);
        _db.AuthChallenges.Add(new AuthChallenge
        {
            Id = Guid.NewGuid(),
            Purpose = HandoffNoncePurpose,
            KeyHash = nonce,
            Payload = onboardingId.ToString("N"),
            CreatedAtUtc = now,
            ExpiresAtUtc = now + HandoffFreshness + HandoffFreshness,
        });
        try
        {
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Swept by another request first: gone either way; take this one's on its own.
                foreach (var entry in _db.ChangeTracker.Entries<AuthChallenge>().Where(e => e.State == EntityState.Deleted).ToList())
                    entry.State = EntityState.Detached;
                await _db.SaveChangesAsync(ct);
            }
        }
        catch (DbUpdateException)
        {
            // The same request at the same moment: one is taken (one row per signature).
            return refused;
        }

        if (onboarding.ClubMemberId is not Guid memberId) return DomainErrors.Portal.HandoffNoMember;
        var settings = await _db.MemberPortalSettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == onboarding.OrganizationId, ct);
        if (settings == null) return DomainErrors.Portal.Off;
        if (await OpenRefusalAsync(settings, ct) is { } closed) return closed;
        var member = await _db.ClubMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == onboarding.OrganizationId && m.ErasedAtUtc == null, ct);
        if (member == null) return DomainErrors.Portal.HandoffNoMember;

        var secret = NewSecret();
        var expires = now + HandoffLinkLifetime;
        _db.MemberPortalLinks.Add(new MemberPortalLink
        {
            Id = Guid.NewGuid(),
            OrganizationId = onboarding.OrganizationId,
            MemberId = member.Id,
            Purpose = MemberPortalSignInWays.Handoff,
            TokenHash = Hash(secret),
            CreatedAtUtc = now,
            ExpiresAtUtc = expires,
        });
        await _db.SaveChangesAsync(ct);
        return new PortalHandoffLinkDto(SignInUrl(settings.PortalKey, secret, handoff: true), expires);
    }
}
