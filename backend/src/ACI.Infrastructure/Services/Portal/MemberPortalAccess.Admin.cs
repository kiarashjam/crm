using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services.Portal;

/// <summary>Staff's side of the member portal: its settings, invitations, who has used it, and ending sessions.</summary>
public sealed partial class MemberPortalAccess
{
    /// <summary>The longest cut-off a club may set for members cancelling themselves: thirty days.</summary>
    private const int MaxCancelHours = 720;

    public async Task<MemberPortalSettingsDto> GetSettingsAsync(Guid organizationId, CancellationToken ct = default)
    {
        var settings = await _db.MemberPortalSettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);
        return await ToDtoAsync(organizationId, settings, ct);
    }

    public async Task<Result<MemberPortalSettingsDto>> SaveSettingsAsync(Guid organizationId, Guid userId, SaveMemberPortalSettingsRequest request, Guid? expectedVersion, CancellationToken ct = default)
    {
        if (request.MemberCancelHours is int hours && (hours < 0 || hours > MaxCancelHours)) return DomainErrors.Portal.CancelHoursInvalid;

        var settings = await _db.MemberPortalSettings.FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);
        var version = expectedVersion ?? request.Version;
        if (settings != null && version is Guid wanted && wanted != Guid.Empty && wanted != settings.Version)
            return DomainErrors.Portal.SettingsChanged;
        if (settings == null)
        {
            settings = new MemberPortalSettings { Id = Guid.NewGuid(), OrganizationId = organizationId, PortalKey = NewPortalKey() };
            _db.MemberPortalSettings.Add(settings);
        }
        var wasOn = settings.Enabled;
        if (request.Enabled is bool enabled) settings.Enabled = enabled;
        if (request.MemberCancelHours is int cutOff) settings.MemberCancelHours = cutOff;
        settings.UpdatedAtUtc = Now;
        settings.UpdatedByUserId = userId;
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Changed, or made for the first time, by someone else at the same moment.
            return DomainErrors.Portal.SettingsChanged;
        }

        // A club that closes its portal leaves nobody signed in to it, and no link that would
        // open it again when it reopens: members sign in afresh then.
        if (wasOn && !settings.Enabled)
            await EndSessionsAsync(organizationId, null, MemberPortalRevocations.Staff, userId, ct);
        return await ToDtoAsync(organizationId, settings, ct);
    }

    public async Task<IReadOnlyList<MemberPortalUseDto>> UsageAsync(Guid organizationId, CancellationToken ct = default)
    {
        var members = await _db.ClubMembers.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.ErasedAtUtc == null)
            .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
            .ToListAsync(ct);
        var accounts = await _db.MemberPortalAccounts.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId)
            .ToDictionaryAsync(a => a.MemberId, ct);
        var now = Now;
        var open = await _db.MemberPortalSessions.AsNoTracking()
            .Where(s => s.OrganizationId == organizationId && s.RevokedAtUtc == null && s.ExpiresAtUtc > now)
            .GroupBy(s => s.MemberId)
            .Select(g => new { MemberId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.MemberId, g => g.Count, ct);
        var handedOver = (await _db.DealOnboardings.AsNoTracking()
                .Where(o => o.OrganizationId == organizationId && o.ClubMemberId != null)
                .Select(o => o.ClubMemberId!.Value)
                .ToListAsync(ct))
            .ToHashSet();
        return members.Select(m => ToUse(m, accounts.GetValueOrDefault(m.Id), open.GetValueOrDefault(m.Id), handedOver.Contains(m.Id))).ToList();
    }

    public async Task<Result<MemberPortalUseDto>> UseAsync(Guid organizationId, Guid memberId, CancellationToken ct = default)
    {
        var member = await _db.ClubMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == organizationId, ct);
        if (member == null) return DomainErrors.Portal.MemberNotFound;
        var account = await _db.MemberPortalAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.OrganizationId == organizationId && a.MemberId == memberId, ct);
        var now = Now;
        var open = await _db.MemberPortalSessions.CountAsync(
            s => s.OrganizationId == organizationId && s.MemberId == memberId && s.RevokedAtUtc == null && s.ExpiresAtUtc > now, ct);
        var handedOver = await _db.DealOnboardings.AsNoTracking().AnyAsync(o => o.OrganizationId == organizationId && o.ClubMemberId == memberId, ct);
        return ToUse(member, account, open, handedOver);
    }

    public async Task<Result<MemberPortalUseDto>> InviteAsync(Guid organizationId, Guid userId, Guid memberId, CancellationToken ct = default)
    {
        var settings = await _db.MemberPortalSettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);
        if (settings is not { Enabled: true }) return DomainErrors.Portal.NotOn;
        var member = await _db.ClubMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == organizationId, ct);
        if (member == null) return DomainErrors.Portal.MemberNotFound;
        if (member.ErasedAtUtc != null) return DomainErrors.Portal.MemberErased;
        if (!ValidationHelper.IsValidEmail(member.Email.Trim())) return DomainErrors.Portal.MemberNoEmail;

        var queued = await SendLinksAsync(settings, [member], MemberPortalSignInWays.Invite, userId, ct);
        if (queued.IsFailure) return queued.Error;
        await NoteInvitedAsync(organizationId, userId, member, queued.Value, ct);
        return await UseAsync(organizationId, memberId, ct);
    }

    public async Task<Result<PortalInviteAllDto>> InviteAllAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        var settings = await _db.MemberPortalSettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);
        if (settings is not { Enabled: true }) return DomainErrors.Portal.NotOn;

        // Everyone the club still has: former members (Cancelled) are left out, and anyone erased.
        var members = await _db.ClubMembers.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.ErasedAtUtc == null && m.Status != "Cancelled")
            .OrderBy(m => m.LastName).ThenBy(m => m.FirstName)
            .ToListAsync(ct);
        var invited = (await _db.MemberPortalAccounts.AsNoTracking()
                .Where(a => a.OrganizationId == organizationId && a.InvitedAtUtc != null)
                .Select(a => a.MemberId)
                .ToListAsync(ct))
            .ToHashSet();

        var alreadyInvited = members.Count(m => invited.Contains(m.Id));
        var waiting = members.Where(m => !invited.Contains(m.Id)).ToList();
        var noEmail = waiting.Count(m => !ValidationHelper.IsValidEmail(m.Email.Trim()));
        // One invitation per mailbox: a couple sharing an address gets one email with a link for each.
        var mailboxes = waiting.Where(m => ValidationHelper.IsValidEmail(m.Email.Trim()))
            .GroupBy(m => m.Email.Trim().ToLowerInvariant())
            .ToList();

        int sent = 0, refused = 0;
        foreach (var mailbox in mailboxes.Take(InviteBatch))
        {
            var people = mailbox.ToList();
            var queued = await SendLinksAsync(settings, people, MemberPortalSignInWays.Invite, userId, ct);
            if (queued.IsFailure)
            {
                // A bounced, complained or erased address: not invited, and counted so staff
                // know; trying again later sends nothing either until the address is cleared.
                refused += people.Count;
                continue;
            }
            foreach (var person in people) await NoteInvitedAsync(organizationId, userId, person, queued.Value, ct);
            sent += people.Count;
        }
        var remaining = mailboxes.Skip(InviteBatch).Sum(m => m.Count());
        return new PortalInviteAllDto(sent, alreadyInvited, noEmail, refused, remaining);
    }

    public async Task<Result<MemberPortalUseDto>> RevokeAsync(Guid organizationId, Guid userId, Guid memberId, CancellationToken ct = default)
    {
        var member = await _db.ClubMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == organizationId, ct);
        if (member == null) return DomainErrors.Portal.MemberNotFound;
        await EndSessionsAsync(organizationId, memberId, MemberPortalRevocations.Staff, userId, ct);
        return await UseAsync(organizationId, memberId, ct);
    }

    // ── pieces ──────────────────────────────────────────────────────────────────

    private async Task NoteInvitedAsync(Guid organizationId, Guid userId, ClubMember member, OutboundMessage message, CancellationToken ct)
    {
        var now = Now;
        await NoteUseAsync(organizationId, member.Id, account =>
        {
            account.InvitedAtUtc = now;
            account.InvitedByUserId = userId;
            account.InvitationsSent++;
        }, ct);
        // On the member's communications, like every email the club sends them; the preview
        // leaves the link out, so nobody can open the account from the club's side.
        _db.MemberMessages.Add(new MemberMessage
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            MemberId = member.Id,
            MemberName = NameOf(member),
            Email = member.Email.Trim(),
            Channel = CampaignChannels.Email,
            Category = "Welcome",
            Purpose = ConsentPurposes.Service,
            Subject = message.Subject ?? "",
            Preview = "Invitation to the member portal, with a sign-in link that works once.",
            Status = MemberMessageStatuses.Queued,
            OutboundMessageId = message.Id,
            CreatedByUserId = userId,
            CreatedAtUtc = now,
        });
        await _db.SaveChangesAsync(ct);
    }

    private async Task<MemberPortalSettingsDto> ToDtoAsync(Guid organizationId, MemberPortalSettings? settings, CancellationToken ct)
    {
        // Whether the workspace also hands new members to its own portal (Welcome & portal):
        // the settings say so, so the club knows its members would then have two places.
        var handoff = await _db.OnboardingSettings.AsNoTracking()
            .AnyAsync(s => s.OrganizationId == organizationId && s.WebhookUrl != null && s.WebhookUrl != "" && s.WebhookSecret != null && s.WebhookSecret != "", ct);
        return new MemberPortalSettingsDto(
            settings?.Enabled ?? false,
            settings == null ? null : PortalUrl(settings.PortalKey),
            settings?.MemberCancelHours ?? 24,
            handoff,
            settings?.UpdatedAtUtc,
            settings?.Version ?? Guid.Empty);
    }

    /// <summary>
    /// One member's use of the portal. <paramref name="handedOver"/>: a welcome hand-over named
    /// them (its ClubMemberId), so they may sign in to the club's own portal too. Not whether the
    /// member has a deal: a membership contract's countersignature links the member it admits to
    /// the contract's deal as well, and that hands them to no other portal.
    /// </summary>
    private static MemberPortalUseDto ToUse(ClubMember m, MemberPortalAccount? account, int openSessions, bool handedOver) => new()
    {
        MemberId = m.Id,
        Name = NameOf(m),
        Email = string.IsNullOrWhiteSpace(m.Email) ? null : m.Email.Trim(),
        Status = m.Status,
        InvitedAtUtc = account?.InvitedAtUtc,
        InvitationsSent = account?.InvitationsSent ?? 0,
        FirstSignInAtUtc = account?.FirstSignInAtUtc,
        LastSeenAtUtc = account?.LastSeenAtUtc,
        ActiveSessions = openSessions,
        CanInvite = m.ErasedAtUtc == null && ValidationHelper.IsValidEmail(m.Email.Trim()),
        FromHandoff = handedOver,
    };
}
