using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ACI.Application.Common;
using ACI.Application.Common.I18n;
using ACI.Application.Configuration;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Infrastructure.Services.Portal;

/// <summary>
/// Signing in to the member portal (emailed links, sessions) and staff's side of it (the
/// settings, invitations, who has used it, ending sessions). See <see cref="IMemberPortalSignIn"/>.
/// </summary>
/// <remarks>
/// <para>
/// A link's secret is never stored: only its SHA-256, which is what a link is looked up by.
/// Deleting the row is what uses it, so of two requests with one link the second deletes
/// nothing and is refused. Links nobody used are swept as new ones are made.
/// </para>
/// <para>
/// Someone signing in names no workspace yet: the portal's address (its key) and the link's
/// hash are looked up across workspaces (IgnoreQueryFilters, at each place), and everything
/// after acts for the workspace they name.
/// </para>
/// </remarks>
public sealed partial class MemberPortalAccess : IMemberPortalSignIn, IMemberPortalAdmin
{
    /// <summary>A link the member asks for on the sign-in page: they are waiting for it.</summary>
    public static readonly TimeSpan SignInLinkLifetime = TimeSpan.FromMinutes(15);

    /// <summary>An invitation staff sent: it may sit in the inbox for a while.</summary>
    public static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(3);

    /// <summary>A link handed to the club's own portal, which sends the member's browser to it at once.</summary>
    public static readonly TimeSpan HandoffLinkLifetime = TimeSpan.FromMinutes(2);

    /// <summary>How long a signed hand-over request stays fresh, either side of now.</summary>
    public static readonly TimeSpan HandoffFreshness = TimeSpan.FromMinutes(5);

    /// <summary>A session's last use is noted at most this often, so reading a page does not write each time.</summary>
    private static readonly TimeSpan SeenEvery = TimeSpan.FromMinutes(5);

    /// <summary>Old links and sessions are swept this many at a time, as new ones are made.</summary>
    private const int SweepBatch = 50;

    /// <summary>Invite-all queues this many emails per request; the next request carries on.</summary>
    internal const int InviteBatch = 200;

    /// <summary>Ended or run-out sessions are kept this long for the record, then swept.</summary>
    private static readonly TimeSpan KeepEndedSessions = TimeSpan.FromDays(90);

    private const string HandoffNoncePurpose = "portal-handoff";

    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IOutbox _outbox;
    private readonly IWorkspaceModules _modules;
    private readonly MemberPortalTokens _tokens;
    private readonly AuthAttemptLimiter _limiter;
    private readonly EmailSettings _email;
    private readonly ILogger<MemberPortalAccess> _logger;
    private readonly TimeProvider _clock;

    public MemberPortalAccess(
        AppDbContext db,
        ITenantContext tenant,
        IOutbox outbox,
        IWorkspaceModules modules,
        MemberPortalTokens tokens,
        AuthAttemptLimiter limiter,
        IOptions<EmailSettings> email,
        ILogger<MemberPortalAccess> logger,
        TimeProvider? clock = null)
    {
        _db = db;
        _tenant = tenant;
        _outbox = outbox;
        _modules = modules;
        _tokens = tokens;
        _limiter = limiter;
        _email = email.Value;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    // ── the club's sign-in page ─────────────────────────────────────────────────

    public async Task<Result<PortalClubDto>> ClubAsync(string portalKey, CancellationToken ct = default)
    {
        var settings = await SettingsByKeyAsync(portalKey, ct);
        if (settings == null) return DomainErrors.Portal.NotFound;
        var club = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == settings.OrganizationId, ct);
        if (club == null) return DomainErrors.Portal.NotFound;
        var open = await OpenRefusalAsync(settings, ct) == null;
        return new PortalClubDto(club.Name, open, club.Locale, AppLanguages.Normalise(club.Locale));
    }

    public async Task<Result> RequestLinkAsync(string portalKey, string? email, CancellationToken ct = default)
    {
        var address = (email ?? "").Trim().ToLowerInvariant();
        if (address.Length is 0 or > 256 || !ValidationHelper.IsValidEmail(address)) return DomainErrors.Portal.EmailInvalid;
        var settings = await SettingsByKeyAsync(portalKey, ct);
        if (settings == null) return DomainErrors.Portal.NotFound;
        if (await OpenRefusalAsync(settings, ct) is { } closed) return closed;

        // Counted whether or not the address is a member's, so the limit says nothing either.
        var limit = $"portal-link:{settings.OrganizationId:N}:{address}";
        if (_limiter.IsBlocked(limit, AuthAttemptLimiter.MaxResetEmails)) return DomainErrors.Portal.TooManyLinks;
        _limiter.Record(limit);

        using var acting = _tenant.ActFor(settings.OrganizationId, null);
        var members = await _db.ClubMembers
            .Where(m => m.OrganizationId == settings.OrganizationId && m.ErasedAtUtc == null && m.Email.ToLower() == address)
            .OrderBy(m => m.FirstName).ThenBy(m => m.LastName)
            .ToListAsync(ct);
        // Nobody of the club's at this address: the same answer, and nothing is sent.
        if (members.Count == 0) return Result.Success();

        var sent = await SendLinksAsync(settings, members, MemberPortalSignInWays.Link, null, ct);
        if (sent.IsFailure)
            _logger.LogWarning("Portal sign-in link for organization {OrganizationId} was not queued: {Error}", settings.OrganizationId, sent.Error.Description);
        return Result.Success();
    }

    public async Task<Result<PortalSessionStartedDto>> RedeemAsync(string portalKey, string? token, CancellationToken ct = default)
    {
        var secret = (token ?? "").Trim();
        if (secret.Length is 0 or > 200) return DomainErrors.Portal.LinkInvalid;
        var settings = await SettingsByKeyAsync(portalKey, ct);
        if (settings == null) return DomainErrors.Portal.NotFound;

        // Across workspaces: the link is found by its secret's hash alone, and must be this club's.
        var hash = Hash(secret);
        var link = await _db.MemberPortalLinks.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.TokenHash == hash, ct);
        if (link == null || link.OrganizationId != settings.OrganizationId) return DomainErrors.Portal.LinkInvalid;

        // Deleting it is what uses it: of two requests with the same link, the second deletes
        // nothing and is refused, so a link opens one session.
        _db.MemberPortalLinks.Remove(link);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            _db.Entry(link).State = EntityState.Detached;
            return DomainErrors.Portal.LinkInvalid;
        }
        var now = Now;
        if (link.ExpiresAtUtc <= now) return DomainErrors.Portal.LinkInvalid;
        if (await OpenRefusalAsync(settings, ct) is { } closed) return closed;

        using var acting = _tenant.ActFor(settings.OrganizationId, null);
        var member = await _db.ClubMembers.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == link.MemberId && m.OrganizationId == settings.OrganizationId && m.ErasedAtUtc == null, ct);
        if (member == null) return DomainErrors.Portal.LinkInvalid;

        var session = new MemberPortalSession
        {
            Id = Guid.NewGuid(),
            OrganizationId = settings.OrganizationId,
            MemberId = member.Id,
            Via = MemberPortalSignInWays.All.Contains(link.Purpose) ? link.Purpose : MemberPortalSignInWays.Link,
            CreatedAtUtc = now,
            ExpiresAtUtc = now + MemberPortalTokens.SessionLifetime,
            LastSeenAtUtc = now,
        };
        _db.MemberPortalSessions.Add(session);
        await SweepSessionsAsync(settings.OrganizationId, now, ct);
        await _db.SaveChangesAsync(ct);
        await NoteUseAsync(settings.OrganizationId, member.Id, account =>
        {
            account.FirstSignInAtUtc ??= now;
            account.LastSignInAtUtc = now;
            account.LastSeenAtUtc = now;
        }, ct);

        var club = await _db.Organizations.AsNoTracking().Where(o => o.Id == settings.OrganizationId).Select(o => o.Name).FirstOrDefaultAsync(ct);
        var jwt = _tokens.Issue(new PortalMember(settings.OrganizationId, member.Id, session.Id), session.ExpiresAtUtc);
        return new PortalSessionStartedDto(jwt, session.ExpiresAtUtc, member.Id, member.FirstName, club ?? "", AppLanguages.Normalise(member.PreferredLanguage));
    }

    // ── sessions ────────────────────────────────────────────────────────────────

    public async Task<PortalSessionCheck?> CheckSessionAsync(PortalMember who, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        var now = Now;
        var found = await (
                from s in _db.MemberPortalSessions
                join m in _db.ClubMembers on s.MemberId equals m.Id
                where s.Id == who.SessionId && s.OrganizationId == who.OrganizationId && s.MemberId == who.MemberId
                      && m.OrganizationId == who.OrganizationId
                select new { Session = s, m.FirstName, m.LastName, m.ErasedAtUtc })
            .FirstOrDefaultAsync(ct);
        if (found == null || found.ErasedAtUtc != null) return null;
        var session = found.Session;
        if (session.RevokedAtUtc != null || session.ExpiresAtUtc <= now) return null;

        if (now - session.LastSeenAtUtc >= SeenEvery)
        {
            try
            {
                session.LastSeenAtUtc = now;
                await _db.SaveChangesAsync(ct);
                await NoteUseAsync(who.OrganizationId, who.MemberId, account => account.LastSeenAtUtc = now, ct);
            }
            catch (DbUpdateException ex)
            {
                // A note of when it was last used; the request goes on without it.
                _logger.LogDebug(ex, "Portal session {SessionId}: its last use was not noted", session.Id);
            }
        }
        return new PortalSessionCheck($"{found.FirstName} {found.LastName}".Trim());
    }

    public async Task<bool> IsOpenAsync(Guid organizationId, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(organizationId, null);
        return await _db.MemberPortalSettings.AnyAsync(s => s.OrganizationId == organizationId && s.Enabled, ct);
    }

    public async Task SignOutAsync(PortalMember who, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        var session = await _db.MemberPortalSessions.FirstOrDefaultAsync(
            s => s.Id == who.SessionId && s.OrganizationId == who.OrganizationId && s.MemberId == who.MemberId && s.RevokedAtUtc == null, ct);
        if (session == null) return;
        session.RevokedAtUtc = Now;
        session.RevokedBy = MemberPortalRevocations.SignedOut;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> SignOutEverywhereAsync(PortalMember who, CancellationToken ct = default)
    {
        using var acting = _tenant.ActFor(who.OrganizationId, null);
        return await EndSessionsAsync(who.OrganizationId, who.MemberId, MemberPortalRevocations.Everywhere, null, ct);
    }

    /// <summary>
    /// Ends every open session of one member (or of the whole club, with no member), and takes
    /// back their sign-in links not yet used, which would open new ones.
    /// </summary>
    private async Task<int> EndSessionsAsync(Guid organizationId, Guid? memberId, string by, Guid? userId, CancellationToken ct)
    {
        var now = Now;
        var open = await _db.MemberPortalSessions
            .Where(s => s.OrganizationId == organizationId && (memberId == null || s.MemberId == memberId) && s.RevokedAtUtc == null && s.ExpiresAtUtc > now)
            .ToListAsync(ct);
        foreach (var session in open)
        {
            session.RevokedAtUtc = now;
            session.RevokedBy = by;
            session.RevokedByUserId = userId;
        }
        var links = await _db.MemberPortalLinks
            .Where(l => l.OrganizationId == organizationId && (memberId == null || l.MemberId == memberId))
            .ToListAsync(ct);
        _db.MemberPortalLinks.RemoveRange(links);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A link used at the same moment is gone already, which is what was asked; end the
            // sessions without it.
            foreach (var entry in _db.ChangeTracker.Entries<MemberPortalLink>().Where(e => e.State == EntityState.Deleted).ToList())
                entry.State = EntityState.Detached;
            await _db.SaveChangesAsync(ct);
        }
        return open.Count;
    }

    // ── pieces ──────────────────────────────────────────────────────────────────

    /// <summary>The settings a portal address names, in any workspace: someone signing in names none yet.</summary>
    private async Task<MemberPortalSettings?> SettingsByKeyAsync(string? portalKey, CancellationToken ct)
    {
        var key = (portalKey ?? "").Trim();
        if (key.Length is 0 or > 64) return null;
        return await _db.MemberPortalSettings.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(s => s.PortalKey == key, ct);
    }

    /// <summary>Why members cannot sign in to the club's portal now, or null: the portal, or the whole club, is off.</summary>
    private async Task<Error?> OpenRefusalAsync(MemberPortalSettings settings, CancellationToken ct)
    {
        if (!await _modules.IsOnAsync(settings.OrganizationId, WorkspaceModules.Club, ct)) return DomainErrors.Modules.Off(WorkspaceModules.Club);
        return settings.Enabled ? null : DomainErrors.Portal.Off;
    }

    /// <summary>
    /// Makes a link for each of these members (who share one address) and emails them in one
    /// message, from the club's sender, in the first one's language. A link the outbox will not
    /// send (a bounced or erased address) is taken back.
    /// </summary>
    private async Task<Result<OutboundMessage>> SendLinksAsync(MemberPortalSettings settings, IReadOnlyList<ClubMember> members, string purpose, Guid? userId, CancellationToken ct)
    {
        var now = Now;
        var lifetime = purpose == MemberPortalSignInWays.Invite ? InvitationLifetime : SignInLinkLifetime;
        await SweepLinksAsync(now, ct);
        var links = new List<(ClubMember Member, MemberPortalLink Link, string Url)>();
        foreach (var member in members)
        {
            var secret = NewSecret();
            var link = new MemberPortalLink
            {
                Id = Guid.NewGuid(),
                OrganizationId = settings.OrganizationId,
                MemberId = member.Id,
                Purpose = purpose,
                TokenHash = Hash(secret),
                CreatedAtUtc = now,
                ExpiresAtUtc = now + lifetime,
                CreatedByUserId = userId,
            };
            _db.MemberPortalLinks.Add(link);
            links.Add((member, link, SignInUrl(settings.PortalKey, secret)));
        }
        await _db.SaveChangesAsync(ct);

        var first = members[0];
        var language = AppLanguages.ForMember(first.PreferredLanguage);
        var club = await _db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == settings.OrganizationId, ct);
        var clubName = club?.Name ?? "";
        string L(string key, params (string, object?)[] args) => MemberTexts.T(language, key, args);

        var body = new StringBuilder();
        var greeting = members.Count == 1 ? Clip(first.FirstName.Trim(), 100) : string.Join(", ", members.Select(m => m.FirstName.Trim()));
        body.Append(L("greeting", ("name", greeting))).Append("\n\n");
        string subject;
        if (purpose == MemberPortalSignInWays.Invite)
        {
            subject = L("portal.inviteSubject", ("club", clubName));
            body.Append(L("portal.inviteIntro", ("club", clubName))).Append("\n\n");
            foreach (var (member, _, url) in links)
            {
                body.Append(members.Count == 1 ? L("portal.inviteOpen") : L("portal.inviteOpenFor", ("name", NameOf(member)))).Append('\n');
                body.Append(url).Append("\n\n");
            }
            var until = MemberTexts.Date(language, club?.Locale, Local(now + lifetime, club?.TimeZone));
            body.Append(L("portal.inviteValid", ("date", until), ("portal", PortalUrl(settings.PortalKey)))).Append("\n\n");
        }
        else
        {
            subject = L("portal.signInSubject", ("club", clubName));
            body.Append(L("portal.signInIntro", ("club", clubName))).Append("\n\n");
            foreach (var (member, _, url) in links)
            {
                if (members.Count > 1) body.Append(L("portal.signInAs", ("name", NameOf(member)))).Append('\n');
                body.Append(url).Append("\n\n");
            }
            body.Append(L("portal.signInValid", ("minutes", (int)lifetime.TotalMinutes))).Append("\n\n");
        }
        body.Append(clubName);

        var sender = await _db.OnboardingSettings.AsNoTracking().FirstOrDefaultAsync(s => s.OrganizationId == settings.OrganizationId, ct);
        var queued = await _outbox.QueueEmailAsync(new OutboundEmailRequest
        {
            OrganizationId = settings.OrganizationId,
            Source = OutboundSources.Portal,
            SourceId = first.Id,
            To = [first.Email.Trim()],
            RecipientName = NameOf(first),
            Subject = subject,
            Body = body.ToString(),
            CreatedByUserId = userId,
            ReplyToUserId = userId,
            MemberId = first.Id,
            // What the membership brings, not marketing: it goes whatever they said about news.
            Purpose = ConsentPurposes.Service,
            // From the club as Welcome & portal names it, as the welcome itself is.
            FromName = Blank(sender?.SenderName) ?? Blank(club?.Name),
            FromAddress = Blank(sender?.SenderEmail),
            ReplyTo = Blank(sender?.ReplyTo),
        }, ct);
        if (queued.IsFailure)
        {
            _db.MemberPortalLinks.RemoveRange(links.Select(l => l.Link));
            await _db.SaveChangesAsync(CancellationToken.None);
        }
        return queued;
    }

    /// <summary>Notes something about a member's use of the portal, making the record of it on first use.</summary>
    private async Task NoteUseAsync(Guid organizationId, Guid memberId, Action<MemberPortalAccount> note, CancellationToken ct)
    {
        for (var attempt = 0; ; attempt++)
        {
            var account = await _db.MemberPortalAccounts.FirstOrDefaultAsync(a => a.OrganizationId == organizationId && a.MemberId == memberId, ct);
            if (account == null)
            {
                account = new MemberPortalAccount { Id = Guid.NewGuid(), OrganizationId = organizationId, MemberId = memberId, CreatedAtUtc = Now };
                _db.MemberPortalAccounts.Add(account);
            }
            note(account);
            try
            {
                await _db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateException) when (attempt < 2)
            {
                // Another request made the record at the same moment (one per member): note it on that one.
                _db.Entry(account).State = EntityState.Detached;
            }
        }
    }

    private async Task SweepLinksAsync(DateTime now, CancellationToken ct)
    {
        // Across workspaces, like the links themselves: a link that ran out is nobody's business.
        var stale = await _db.MemberPortalLinks.IgnoreQueryFilters()
            .Where(l => l.ExpiresAtUtc < now)
            .OrderBy(l => l.ExpiresAtUtc)
            .Take(SweepBatch)
            .ToListAsync(ct);
        if (stale.Count == 0) return;
        _db.MemberPortalLinks.RemoveRange(stale);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Swept by another request first: gone either way.
            foreach (var entry in _db.ChangeTracker.Entries<MemberPortalLink>().Where(e => e.State == EntityState.Deleted).ToList())
                entry.State = EntityState.Detached;
        }
    }

    private async Task SweepSessionsAsync(Guid organizationId, DateTime now, CancellationToken ct)
    {
        var before = now - KeepEndedSessions;
        var old = await _db.MemberPortalSessions
            .Where(s => s.OrganizationId == organizationId && s.ExpiresAtUtc < before)
            .OrderBy(s => s.ExpiresAtUtc)
            .Take(SweepBatch)
            .ToListAsync(ct);
        _db.MemberPortalSessions.RemoveRange(old);
    }

    /// <summary>The portal's sign-in page.</summary>
    private string PortalUrl(string portalKey) => $"{FrontendBase()}/member-portal/{Uri.EscapeDataString(portalKey)}";

    /// <summary>
    /// A sign-in link. The secret rides in the fragment, which a browser never sends to a server,
    /// so neither the app's host nor a proxy on the way logs it; the page posts it to the API.
    /// </summary>
    private string SignInUrl(string portalKey, string secret, bool handoff = false) =>
        $"{PortalUrl(portalKey)}/sign-in{(handoff ? "?handoff=1" : "")}#token={Uri.EscapeDataString(secret)}";

    private string FrontendBase() => (_email.FrontendBaseUrl ?? "").Trim().TrimEnd('/');

    /// <summary>A link's secret: 256 random bits, base64url.</summary>
    internal static string NewSecret() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>A portal's address key: 128 random bits, base64url.</summary>
    internal static string NewPortalKey() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static string Hash(string secret) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))).ToLowerInvariant();

    private static DateTime Local(DateTime utc, string? timeZone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), RegionalSettings.ZoneOf(timeZone));

    private static string NameOf(ClubMember m) => $"{m.FirstName} {m.LastName}".Trim();

    private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string Clip(string s, int max) => s.Length <= max ? s : s[..max];
}
