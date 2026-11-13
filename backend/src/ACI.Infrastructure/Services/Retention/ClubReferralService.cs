using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services.Retention;

/// <summary>Referrals, sponsors, the referral bonus and the report (see <see cref="IClubReferrals"/>).</summary>
/// <remarks>
/// A referrer used to be a name typed into the application, so the bonus the settings page
/// offered was never paid and nobody could tell which members bring others in. The referrer is
/// now a member, carried onto the new member at approval; proposers and seconders are members
/// held to the club's rule; and the bonus is paid once, by a row per member brought in that the
/// database will not take twice.
/// </remarks>
public sealed class ClubReferralService : IClubReferrals
{
    /// <summary>The longest report period: three years, as Club Reports allows.</summary>
    private static readonly TimeSpan LongestPeriod = TimeSpan.FromDays(3 * 366);

    /// <summary>
    /// How long a request holds a bonus it is giving: far longer than a credit takes, short enough
    /// that a hold left by a request that stopped part way does not keep the bonus from being given.
    /// </summary>
    private static readonly TimeSpan HoldFor = TimeSpan.FromMinutes(2);

    /// <summary>Where the in-memory provider (tests) takes the hold: it cannot run a conditional update.</summary>
    private static readonly SemaphoreSlim InMemoryHolds = new(1, 1);

    private readonly AppDbContext _db;
    private readonly IClubLedger _ledger;
    private readonly IPermissionService _permissions;
    private readonly ILogger<ClubReferralService> _logger;

    public ClubReferralService(AppDbContext db, IClubLedger ledger, IPermissionService permissions, ILogger<ClubReferralService> logger)
    {
        _db = db;
        _ledger = ledger;
        _permissions = permissions;
        _logger = logger;
    }

    private sealed record Person(Guid Id, string FirstName, string LastName, string Status, string Tier, DateTime JoinedAtUtc, DateTime? StartsOnUtc,
        Guid? ReferredByMemberId, DateTime? ErasedAtUtc)
    {
        public string Name => $"{FirstName} {LastName}".Trim();
        public ReferralPersonDto Dto => new(Id, Name, Status, Tier, JoinedAtUtc);
    }

    private IQueryable<ClubMember> Members(Guid organizationId) =>
        _db.ClubMembers.AsNoTracking().Where(m => m.OrganizationId == organizationId);

    // Applied last: SQL Server cannot filter or sort on what a constructor made (the in-memory
    // provider can), so every Where and OrderBy is on the member before this.
    private static readonly System.Linq.Expressions.Expression<Func<ClubMember, Person>> AsPerson =
        m => new Person(m.Id, m.FirstName, m.LastName, m.Status, m.Tier, m.JoinedAtUtc, m.StartsOnUtc, m.ReferredByMemberId, m.ErasedAtUtc);

    // ── good standing ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SponsorStandingDto>> StandingAsync(Guid organizationId, IReadOnlyCollection<Guid> memberIds, CancellationToken ct = default)
    {
        var settings = await RetentionRules.ReadAsync(_db, organizationId, ct);
        return await StandingAsync(organizationId, memberIds, settings, ct);
    }

    private async Task<IReadOnlyList<SponsorStandingDto>> StandingAsync(Guid organizationId, IReadOnlyCollection<Guid> memberIds,
        ClubRetentionSettings settings, CancellationToken ct)
    {
        var ids = memberIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        var people = await Members(organizationId).Where(m => ids.Contains(m.Id)).Select(AsPerson).ToListAsync(ct);
        var now = DateTime.UtcNow;
        var overdue = settings.SponsorNoOverdue
            ? (await ClubOverdue.ListAsync(_db, organizationId, now, ids, ct)).GroupBy(c => c.MemberId)
                .ToDictionary(g => g.Key, g => g.GroupBy(c => c.Currency).Select(c => (Currency: c.Key, Owed: c.Sum(x => x.Owed))).ToList())
            : [];
        var (_, culture, _, _, _, _) = await RetentionRules.WorkspaceAsync(_db, organizationId, ct);
        return people.Select(p =>
        {
            var reasons = new List<string>();
            if (p.ErasedAtUtc != null) reasons.Add("their personal data was erased");
            else if (p.Status != "Active") reasons.Add($"they are {p.Status.ToLowerInvariant()}, not an active member");
            var months = (int)Math.Floor((now - RetentionRules.MemberSince(p.JoinedAtUtc, p.StartsOnUtc, now)).TotalDays / 30.44);
            if (months < settings.SponsorMinTenureMonths)
                reasons.Add($"they have been a member for {Math.Max(0, months)} {(months == 1 ? "month" : "months")} and the club asks for {settings.SponsorMinTenureMonths}");
            if (overdue.TryGetValue(p.Id, out var owed))
                reasons.Add($"they have {string.Join(" and ", owed.Select(o => RetentionRules.Money(o.Owed, o.Currency, culture)))} overdue");
            return new SponsorStandingDto(p.Id, p.ErasedAtUtc != null ? "An erased member" : p.Name, reasons.Count == 0, reasons);
        }).ToList();
    }

    // ── applications ─────────────────────────────────────────────────────────

    public async Task<Result> CheckApplicationAsync(Guid organizationId, Guid? applicantMemberId, Guid? referrerId, Guid? proposerId, Guid? seconderId,
        bool referrerChanged, bool proposerChanged, bool seconderChanged, CancellationToken ct = default)
    {
        var named = new[] { referrerId, proposerId, seconderId }.OfType<Guid>().Distinct().ToList();
        var known = named.Count == 0 ? new HashSet<Guid>()
            : (await Members(organizationId).Where(m => named.Contains(m.Id) && m.ErasedAtUtc == null).Select(m => m.Id).ToListAsync(ct)).ToHashSet();
        if (referrerId is Guid r && !known.Contains(r)) return DomainErrors.Referral.ReferrerNotFound;
        if (proposerId is Guid p && !known.Contains(p)) return DomainErrors.Referral.ProposerNotFound;
        if (seconderId is Guid s && !known.Contains(s)) return DomainErrors.Referral.SeconderNotFound;
        if (applicantMemberId is Guid self && (referrerId == self || proposerId == self || seconderId == self)) return DomainErrors.Referral.SelfSponsor;
        if (proposerId != null && proposerId == seconderId) return DomainErrors.Referral.SameSponsor;

        var settings = await RetentionRules.ReadAsync(_db, organizationId, ct);
        if (settings.SponsorRule == SponsorRules.Off) return Result.Success();
        // Only a sponsor named now is held to the rule here: someone who was in good standing when
        // they were named and has since lapsed does not block editing the rest of the application.
        // The approval checks them all again.
        var check = new List<(Guid Id, string Role)>();
        if (proposerChanged && proposerId is Guid proposer) check.Add((proposer, "propose"));
        if (seconderChanged && seconderId is Guid seconder) check.Add((seconder, "second"));
        return await HeldToTheRuleAsync(organizationId, check, settings, ct);
    }

    public async Task<Result> CheckApprovalAsync(Guid organizationId, ClubApplication application, CancellationToken ct = default)
    {
        var settings = await RetentionRules.ReadAsync(_db, organizationId, ct);
        if (settings.SponsorRule == SponsorRules.Off) return Result.Success();
        if (settings.SponsorRule == SponsorRules.Required && (application.ProposerMemberId == null || application.SeconderMemberId == null))
            return DomainErrors.Referral.SponsorsRequired;
        var check = new List<(Guid Id, string Role)>();
        if (application.ProposerMemberId is Guid proposer) check.Add((proposer, "propose"));
        if (application.SeconderMemberId is Guid seconder) check.Add((seconder, "second"));
        return await HeldToTheRuleAsync(organizationId, check, settings, ct);
    }

    private async Task<Result> HeldToTheRuleAsync(Guid organizationId, IReadOnlyList<(Guid Id, string Role)> sponsors, ClubRetentionSettings settings, CancellationToken ct)
    {
        if (sponsors.Count == 0) return Result.Success();
        var standing = (await StandingAsync(organizationId, sponsors.Select(s => s.Id).ToList(), settings, ct)).ToDictionary(s => s.MemberId);
        foreach (var (id, role) in sponsors)
        {
            if (!standing.TryGetValue(id, out var s)) return role == "propose" ? DomainErrors.Referral.ProposerNotFound : DomainErrors.Referral.SeconderNotFound;
            if (!s.InGoodStanding) return DomainErrors.Referral.NotInGoodStanding(role, s.Name, string.Join("; ", s.Reasons));
        }
        return Result.Success();
    }

    // ── the bonus ────────────────────────────────────────────────────────────

    public async Task<string?> RewardReferrerAsync(Guid organizationId, Guid applicationId, Guid userId, bool mayHandleMoney, CancellationToken ct = default)
    {
        var app = await _db.ClubApplications.AsNoTracking()
            .Where(a => a.Id == applicationId && a.OrganizationId == organizationId)
            .Select(a => new { a.MemberId, a.ReferredByMemberId }).FirstOrDefaultAsync(ct);
        if (app?.MemberId is not Guid memberId || app.ReferredByMemberId is not Guid referrerId) return null;
        var award = await AwardCoreAsync(organizationId, memberId, referrerId, applicationId, userId, mayHandleMoney, requireActiveReferrer: true, ct);
        return award.Sentence;
    }

    public async Task<Result<ReferralBonusDto>> AwardAsync(Guid organizationId, Guid referredMemberId, Guid userId, CancellationToken ct = default)
    {
        var member = await Members(organizationId).Where(m => m.Id == referredMemberId && m.ErasedAtUtc == null).Select(AsPerson).FirstOrDefaultAsync(ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;
        if (member.ReferredByMemberId is not Guid referrerId) return DomainErrors.Referral.NoReferrer;
        var settings = await RetentionRules.ReadAsync(_db, organizationId, ct);
        if (settings.ReferralBonus == ReferralBonusKinds.None) return DomainErrors.Referral.NoBonus;
        var held = await _permissions.PermissionsAsync(userId, organizationId, ct);
        var mayHandleMoney = held.Contains(MemberPermissions.ManageMoney);
        if (settings.ReferralBonus == ReferralBonusKinds.Credit && !mayHandleMoney) return DomainErrors.Referral.CreditNeedsMoney;
        if (await _db.ClubReferralBonuses.AnyAsync(b => b.OrganizationId == organizationId && b.ReferredMemberId == referredMemberId
                && b.Status == ReferralBonusStatuses.Awarded, ct))
            return DomainErrors.Referral.BonusGiven;
        var applicationId = await _db.ClubApplications.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId && a.MemberId == referredMemberId && a.Status == ApplicationStatuses.Approved)
            .OrderByDescending(a => a.ApprovedAtUtc).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(ct);
        // Given by a person, who decided: the referrer need not still be active, only in the club's records.
        var (row, sentence, another) = await AwardCoreAsync(organizationId, referredMemberId, referrerId, applicationId, userId, mayHandleMoney,
            requireActiveReferrer: false, ct);
        // Someone gave it at the same moment: theirs stands, given or on its way to the ledger.
        if (another) return row?.Status == ReferralBonusStatuses.Awarded ? DomainErrors.Referral.BonusGiven : DomainErrors.Referral.BonusBeingGiven;
        if (row == null) return new Error("ReferralBonus.NotGiven", sentence ?? "The referral bonus could not be given.");
        if (row.Status != ReferralBonusStatuses.Awarded)
            return new Error("ReferralBonus.NotGiven", row.Reason ?? sentence ?? "The referral bonus could not be given.");
        return (await BonusDtosAsync(organizationId, [row], ct))[0];
    }

    /// <summary>
    /// Gives the referrer their bonus for one member, once. The bonus row is the claim: a new one
    /// by the unique index (one per member brought in), one already there (skipped, or a credit
    /// left pending) by a hold taken with one conditional update, so two requests at the same
    /// moment never both add points or both finish a credit. The points entry is saved with the
    /// row; a credit goes through the ledger after it, with a key of its own, so a retry finds
    /// what was given and gives nothing again.
    /// </summary>
    private sealed record Award(ClubReferralBonus? Row, string? Sentence, bool GivenByAnother = false);

    private async Task<Award> AwardCoreAsync(Guid organizationId, Guid referredId, Guid referrerId, Guid? applicationId,
        Guid userId, bool mayHandleMoney, bool requireActiveReferrer, CancellationToken ct)
    {
        var settings = await RetentionRules.ReadAsync(_db, organizationId, ct);
        if (settings.ReferralBonus == ReferralBonusKinds.None || referrerId == referredId) return new(null, null);
        var row = await _db.ClubReferralBonuses.FirstOrDefaultAsync(b => b.OrganizationId == organizationId && b.ReferredMemberId == referredId, ct);
        // Given already, by another request (or an approval) that finished after the caller
        // looked: theirs stands, and this call gave nothing.
        if (row?.Status == ReferralBonusStatuses.Awarded) return new(row, null, GivenByAnother: true);

        var people = await Members(organizationId).Where(m => m.Id == referrerId || m.Id == referredId).Select(AsPerson).ToListAsync(ct);
        var referrer = people.FirstOrDefault(p => p.Id == referrerId);
        var referred = people.FirstOrDefault(p => p.Id == referredId);
        if (referred == null) return new(null, null);
        var (currency, culture, _, _, _, _) = await RetentionRules.WorkspaceAsync(_db, organizationId, ct);
        var now = DateTime.UtcNow;
        if (row == null)
        {
            row = new ClubReferralBonus
            {
                Id = Guid.NewGuid(), OrganizationId = organizationId, ReferrerMemberId = referrerId, ReferredMemberId = referredId,
                ApplicationId = applicationId, CreatedAtUtc = now, Currency = currency, ClaimedUntilUtc = now.Add(HoldFor),
            };
            _db.ClubReferralBonuses.Add(row);
        }
        else if (!await HoldAsync(row, now, ct))
        {
            // Someone is giving it at this moment, or has just given it: theirs stands.
            var theirs = await _db.ClubReferralBonuses.AsNoTracking().FirstOrDefaultAsync(b => b.Id == row.Id, ct);
            return new(theirs, null, GivenByAnother: true);
        }
        row.Kind = settings.ReferralBonus;
        row.ReferrerMemberId = referrerId;
        row.ApplicationId ??= applicationId;
        row.Points = settings.ReferralBonus == ReferralBonusKinds.Points ? settings.ReferralBonusPoints : 0;
        row.Amount = settings.ReferralBonus == ReferralBonusKinds.Credit ? settings.ReferralBonusAmount : 0;
        row.Currency = currency;

        string? Skip(string why)
        {
            row.Status = ReferralBonusStatuses.Skipped;
            row.Reason = why;
            return $"No referral bonus was given: {why}.";
        }
        string? sentence;
        if (referrer == null || referrer.ErasedAtUtc != null) sentence = Skip("the referrer is no longer in the club's records");
        else if (requireActiveReferrer && referrer.Status != "Active") sentence = Skip($"{referrer.Name} is {referrer.Status.ToLowerInvariant()}, not an active member; someone can still give it from the member's page");
        else if (settings.ReferralBonus == ReferralBonusKinds.Points)
        {
            var entry = new LoyaltyLedgerEntry
            {
                Id = Guid.NewGuid(), OrganizationId = organizationId, MemberId = referrer.Id, MemberName = referrer.Name, Kind = "Earned",
                Reason = "Referral", Points = settings.ReferralBonusPoints, Note = Clip($"For introducing {referred.Name}", 500), CreatedAtUtc = now,
            };
            _db.LoyaltyLedger.Add(entry);
            row.LoyaltyEntryId = entry.Id;
            row.Status = ReferralBonusStatuses.Awarded;
            row.Reason = null;
            row.AwardedAtUtc = now;
            row.AwardedByUserId = userId;
            sentence = $"{referrer.Name} was given {settings.ReferralBonusPoints} points for introducing {referred.Name}.";
        }
        else if (!mayHandleMoney)
        {
            row.Status = ReferralBonusStatuses.Pending;
            row.Reason = "A credit puts money on an account: it waits for someone who may take payments and charge accounts.";
            sentence = $"A referral credit of {RetentionRules.Money(settings.ReferralBonusAmount, currency, culture)} for {referrer.Name} waits for someone who can charge accounts.";
        }
        else
        {
            // The row first, held; the credit after, under its own key.
            row.Status = ReferralBonusStatuses.Pending;
            row.Reason = null;
            sentence = null;
        }
        // Done in this step unless a credit is still to be posted: the hold is let go with it.
        var creditToPost = row.Kind == ReferralBonusKinds.Credit && row.Status == ReferralBonusStatuses.Pending && mayHandleMoney && referrer != null;
        if (!creditToPost) row.ClaimedUntilUtc = null;
        if (!await SaveClaimAsync(row, ct))
        {
            // Another approval or press gave it at the same moment: theirs stands.
            var theirs = await _db.ClubReferralBonuses.AsNoTracking().FirstOrDefaultAsync(b => b.OrganizationId == organizationId && b.ReferredMemberId == referredId, ct);
            return new(theirs, null, GivenByAnother: true);
        }
        if (!creditToPost) return new(row, sentence);

        var credited = await _ledger.CreditAsync(organizationId, referrer!.Id, settings.ReferralBonusAmount, $"Referral bonus for introducing {referred.Name}",
            $"referral:{referredId}", ct);
        if (credited.IsSuccess)
        {
            row.PaymentId = credited.Value.Id;
            row.Status = ReferralBonusStatuses.Awarded;
            row.Reason = null;
            row.AwardedAtUtc = now;
            row.AwardedByUserId = userId;
            sentence = $"{referrer.Name} was given a credit of {RetentionRules.Money(settings.ReferralBonusAmount, currency, culture)} for introducing {referred.Name}.";
        }
        else
        {
            row.Reason = Clip($"The credit could not be posted: {credited.Error.Description}", 500);
            sentence = $"The referral credit for {referrer.Name} could not be posted yet: {credited.Error.Description}";
        }
        row.ClaimedUntilUtc = null;
        // The ledger may have let go of what this context tracked when its own save lost a race.
        if (_db.Entry(row).State == EntityState.Detached) _db.ClubReferralBonuses.Attach(row).State = EntityState.Modified;
        await _db.SaveChangesAsync(ct);
        return new(row, sentence);
    }

    /// <summary>
    /// Takes the hold on a bonus row already there, unless it was given or another request holds
    /// it: one conditional update, so of two requests at the same moment one gets it.
    /// </summary>
    private async Task<bool> HoldAsync(ClubReferralBonus row, DateTime now, CancellationToken ct)
    {
        var until = now.Add(HoldFor);
        if (_db.Database.IsRelational())
        {
            var held = await _db.ClubReferralBonuses
                .Where(b => b.Id == row.Id && b.Status != ReferralBonusStatuses.Awarded && (b.ClaimedUntilUtc == null || b.ClaimedUntilUtc < now))
                .ExecuteUpdateAsync(set => set.SetProperty(b => b.ClaimedUntilUtc, until), ct);
            if (held != 1) return false;
            // What the database now holds; the save that lets the hold go writes it again.
            _db.Entry(row).Property(b => b.ClaimedUntilUtc).CurrentValue = until;
            _db.Entry(row).Property(b => b.ClaimedUntilUtc).OriginalValue = until;
            return true;
        }
        await InMemoryHolds.WaitAsync(ct);
        try
        {
            var fresh = await _db.ClubReferralBonuses.AsNoTracking().FirstOrDefaultAsync(b => b.Id == row.Id, ct);
            if (fresh == null || fresh.Status == ReferralBonusStatuses.Awarded || fresh.ClaimedUntilUtc >= now) return false;
            row.ClaimedUntilUtc = until;
            await _db.SaveChangesAsync(ct);
            return true;
        }
        finally
        {
            InMemoryHolds.Release();
        }
    }

    /// <summary>Saves the bonus row; false when another request saved the member's first (the unique index on SQL Server).</summary>
    private async Task<bool> SaveClaimAsync(ClubReferralBonus row, CancellationToken ct)
    {
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            foreach (var entry in _db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified).ToList())
                entry.State = EntityState.Detached;
            return false;
        }
    }

    // ── a member's page ──────────────────────────────────────────────────────

    public async Task<Result<MemberReferralsDto>> MemberAsync(Guid organizationId, Guid memberId, CancellationToken ct = default)
    {
        var member = await Members(organizationId).Where(m => m.Id == memberId && m.ErasedAtUtc == null).Select(AsPerson).FirstOrDefaultAsync(ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;
        var settings = await RetentionRules.ReadAsync(_db, organizationId, ct);
        var application = await _db.ClubApplications.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId && a.MemberId == memberId && a.Status == ApplicationStatuses.Approved)
            .OrderByDescending(a => a.ApprovedAtUtc)
            .Select(a => new { a.Id, a.ProposerMemberId, a.SeconderMemberId }).FirstOrDefaultAsync(ct);
        var referred = await Members(organizationId).Where(m => m.ReferredByMemberId == memberId).OrderByDescending(m => m.JoinedAtUtc).Select(AsPerson)
            .ToListAsync(ct);
        var sponsored = await _db.ClubApplications.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId && (a.ProposerMemberId == memberId || a.SeconderMemberId == memberId))
            .OrderByDescending(a => a.SubmittedAtUtc)
            .Select(a => new { a.Id, a.FirstName, a.LastName, a.Status, a.SubmittedAtUtc, a.MemberId, a.ProposerMemberId })
            .ToListAsync(ct);
        var bonuses = await _db.ClubReferralBonuses.AsNoTracking()
            .Where(b => b.OrganizationId == organizationId && (b.ReferrerMemberId == memberId || b.ReferredMemberId == memberId))
            .OrderByDescending(b => b.CreatedAtUtc).ToListAsync(ct);
        var bonusDtos = await BonusDtosAsync(organizationId, bonuses, ct);
        var named = new[] { member.ReferredByMemberId, application?.ProposerMemberId, application?.SeconderMemberId }.OfType<Guid>().Distinct().ToList();
        var people = (await Members(organizationId).Where(m => named.Contains(m.Id)).Select(AsPerson).ToListAsync(ct)).ToDictionary(p => p.Id);
        // An erased member shows under the pseudonym erasure gave them, as everywhere else.
        ReferralPersonDto? Person(Guid? id) => id is Guid known && people.TryGetValue(known, out var p) ? p.Dto : null;
        var forThem = bonusDtos.FirstOrDefault(b => b.ReferredMemberId == memberId);
        return new MemberReferralsDto
        {
            MemberId = memberId,
            ReferredBy = Person(member.ReferredByMemberId),
            ApplicationId = application?.Id,
            ProposedBy = Person(application?.ProposerMemberId),
            SecondedBy = Person(application?.SeconderMemberId),
            Referred = referred.Select(p => p.Dto).ToList(),
            Proposed = sponsored.Where(a => a.ProposerMemberId == memberId)
                .Select(a => new SponsoredApplicationDto(a.Id, $"{a.FirstName} {a.LastName}".Trim(), a.Status, a.SubmittedAtUtc, a.MemberId)).ToList(),
            Seconded = sponsored.Where(a => a.ProposerMemberId != memberId)
                .Select(a => new SponsoredApplicationDto(a.Id, $"{a.FirstName} {a.LastName}".Trim(), a.Status, a.SubmittedAtUtc, a.MemberId)).ToList(),
            BonusesEarned = bonusDtos.Where(b => b.ReferrerMemberId == memberId).ToList(),
            BonusForThem = forThem,
            BonusAvailable = member.ReferredByMemberId != null && settings.ReferralBonus != ReferralBonusKinds.None
                && forThem?.Status != ReferralBonusStatuses.Awarded,
            Standing = (await StandingAsync(organizationId, [memberId], settings, ct)).FirstOrDefault(),
        };
    }

    public async Task<Result<MemberReferralsDto>> SetReferrerAsync(Guid organizationId, Guid memberId, Guid userId, Guid? referrerId, CancellationToken ct = default)
    {
        var member = await _db.ClubMembers.FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == organizationId && m.ErasedAtUtc == null, ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;
        if (referrerId == memberId) return DomainErrors.Referral.SelfReferral;
        if (referrerId is Guid id && !await _db.ClubMembers.AnyAsync(m => m.Id == id && m.OrganizationId == organizationId && m.ErasedAtUtc == null, ct))
            return DomainErrors.Referral.ReferrerNotFound;
        member.ReferredByMemberId = referrerId;
        await _db.SaveChangesAsync(ct);
        return await MemberAsync(organizationId, memberId, ct);
    }

    private async Task<IReadOnlyList<ReferralBonusDto>> BonusDtosAsync(Guid organizationId, IReadOnlyList<ClubReferralBonus> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];
        var ids = rows.SelectMany(b => new[] { b.ReferrerMemberId, b.ReferredMemberId }).Distinct().ToList();
        var names = await _db.ClubMembers.AsNoTracking().Where(m => m.OrganizationId == organizationId && ids.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => $"{m.FirstName} {m.LastName}".Trim(), ct);
        return rows.Select(b => new ReferralBonusDto(
                b.Id, b.ReferrerMemberId, names.GetValueOrDefault(b.ReferrerMemberId, "A former member"), b.ReferredMemberId,
                names.GetValueOrDefault(b.ReferredMemberId, "A former member"), b.ApplicationId, b.Kind, b.Status, b.Points, b.Amount, b.Currency,
                b.Reason, b.CreatedAtUtc, b.AwardedAtUtc))
            .ToList();
    }

    // ── the report ───────────────────────────────────────────────────────────

    public async Task<Result<ReferralReportDto>> ReportAsync(Guid organizationId, ReferralReportQuery query, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var to = query.ToUtc ?? now;
        var from = query.FromUtc ?? to.AddMonths(-12);
        if (to <= from || to - from > LongestPeriod) return new Error("ReferralReport.PeriodInvalid", "The period ends after it starts and is at most three years long.");
        var (currency, _, _, _, _, _) = await RetentionRules.WorkspaceAsync(_db, organizationId, ct);

        var people = await Members(organizationId).Select(AsPerson).ToListAsync(ct);
        var byId = people.ToDictionary(p => p.Id);
        var referredBy = people.Where(p => p.ReferredByMemberId != null).ToLookup(p => p.ReferredByMemberId!.Value);
        var referredIds = people.Where(p => p.ReferredByMemberId != null).Select(p => p.Id).ToHashSet();

        // What the members brought in were charged in the period, by whoever ran it up (a spouse
        // whose bills go on the primary's account brought their own spending in): dues apart from
        // everything else, voids taken off, per currency. Opening balances and corrections are not
        // sales, and voiding one takes back none (as on Club Reports).
        var charged = (await _db.ClubCharges.AsNoTracking()
                .Where(c => c.OrganizationId == organizationId && c.PostedAtUtc >= from && c.PostedAtUtc < to
                    && (c.EntryType == ClubEntryTypes.Charge
                        || (c.EntryType == ClubEntryTypes.Void
                            && _db.ClubCharges.Any(o => o.Id == c.ReversesChargeId && o.EntryType == ClubEntryTypes.Charge))))
                .Select(c => new { MemberId = c.IncurredByMemberId ?? c.MemberId, Dues = c.Kind == "Dues", c.Currency, c.Amount })
                .GroupBy(x => new { x.MemberId, x.Dues, x.Currency })
                .Select(g => new { g.Key.MemberId, g.Key.Dues, g.Key.Currency, Amount = g.Sum(x => x.Amount) })
                .ToListAsync(ct))
            .Where(x => referredIds.Contains(x.MemberId))
            .ToLookup(x => x.MemberId);

        var approved = await _db.ClubApplications.AsNoTracking()
            .Where(a => a.OrganizationId == organizationId && a.Status == ApplicationStatuses.Approved && a.ApprovedAtUtc >= from && a.ApprovedAtUtc < to
                && (a.ProposerMemberId != null || a.SeconderMemberId != null))
            .Select(a => new { a.ProposerMemberId, a.SeconderMemberId })
            .ToListAsync(ct);
        var proposed = approved.Where(a => a.ProposerMemberId != null).GroupBy(a => a.ProposerMemberId!.Value).ToDictionary(g => g.Key, g => g.Count());
        var seconded = approved.Where(a => a.SeconderMemberId != null).GroupBy(a => a.SeconderMemberId!.Value).ToDictionary(g => g.Key, g => g.Count());

        var bonuses = (await _db.ClubReferralBonuses.AsNoTracking()
                .Where(b => b.OrganizationId == organizationId && b.Status == ReferralBonusStatuses.Awarded && b.AwardedAtUtc >= from && b.AwardedAtUtc < to)
                .Select(b => new { b.ReferrerMemberId, b.Points, b.Amount, b.Currency, b.Kind })
                .ToListAsync(ct))
            .ToLookup(b => b.ReferrerMemberId);

        static IReadOnlyList<ClubCurrencyAmountDto> Sum(IEnumerable<(string Currency, decimal Amount)> amounts, string first) =>
            amounts.GroupBy(a => a.Currency).Select(g => new ClubCurrencyAmountDto(g.Key, g.Sum(a => a.Amount)))
                .Where(a => a.Amount != 0).OrderBy(a => a.Currency == first ? 0 : 1).ThenBy(a => a.Currency).ToList();

        var referrerIds = referredBy.Select(g => g.Key).Concat(proposed.Keys).Concat(seconded.Keys).Distinct().ToList();
        var rows = new List<ReferrerRowDto>();
        foreach (var id in referrerIds)
        {
            if (!byId.TryGetValue(id, out var referrer)) continue;
            var theirs = referredBy[id].ToList();
            var members = theirs.Select(m =>
                {
                    var amounts = charged[m.Id].ToList();
                    return new ReferredMemberRowDto(m.Id, m.Name, m.Status, m.Tier, m.JoinedAtUtc,
                        Sum(amounts.Where(a => a.Dues).Select(a => (a.Currency, a.Amount)), currency),
                        Sum(amounts.Where(a => !a.Dues).Select(a => (a.Currency, a.Amount)), currency));
                })
                .OrderByDescending(m => m.JoinedAtUtc).ToList();
            var given = bonuses[id].ToList();
            rows.Add(new ReferrerRowDto
            {
                MemberId = id,
                Name = referrer.Name,
                Status = referrer.Status,
                Tier = referrer.Tier,
                ReferredAllTime = theirs.Count,
                ReferredInPeriod = theirs.Count(m => m.JoinedAtUtc >= from && m.JoinedAtUtc < to),
                Proposed = proposed.GetValueOrDefault(id),
                Seconded = seconded.GetValueOrDefault(id),
                Dues = Sum(members.SelectMany(m => m.Dues).Select(a => (a.Currency, a.Amount)), currency),
                Spend = Sum(members.SelectMany(m => m.Spend).Select(a => (a.Currency, a.Amount)), currency),
                Revenue = Sum(members.SelectMany(m => m.Dues.Concat(m.Spend)).Select(a => (a.Currency, a.Amount)), currency),
                BonusPoints = given.Sum(b => b.Points),
                BonusCredit = Sum(given.Where(b => b.Kind == ReferralBonusKinds.Credit).Select(b => (b.Currency, b.Amount)), currency),
                Members = members,
            });
        }
        rows = rows
            .OrderByDescending(r => r.ReferredInPeriod)
            .ThenByDescending(r => r.Revenue.Where(a => a.Currency == currency).Sum(a => a.Amount))
            .ThenByDescending(r => r.ReferredAllTime)
            .ThenBy(r => r.Name)
            .ToList();
        var totals = new ReferralTotalsDto(
            rows.Count(r => r.ReferredAllTime > 0),
            rows.Sum(r => r.ReferredAllTime),
            rows.Sum(r => r.ReferredInPeriod),
            people.Count(p => p.JoinedAtUtc >= from && p.JoinedAtUtc < to),
            Sum(rows.SelectMany(r => r.Dues).Select(a => (a.Currency, a.Amount)), currency),
            Sum(rows.SelectMany(r => r.Spend).Select(a => (a.Currency, a.Amount)), currency),
            Sum(rows.SelectMany(r => r.Revenue).Select(a => (a.Currency, a.Amount)), currency),
            rows.Sum(r => r.BonusPoints),
            Sum(rows.SelectMany(r => r.BonusCredit).Select(a => (a.Currency, a.Amount)), currency));
        return new ReferralReportDto(from, to, currency, rows, totals);
    }

    private static string Clip(string value, int max) => value.Length <= max ? value : value[..max];
}
