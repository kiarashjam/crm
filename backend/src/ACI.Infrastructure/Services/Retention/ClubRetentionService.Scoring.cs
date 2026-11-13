using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ACI.Infrastructure.Services.Retention;

// The at-risk score: the signs each member's records hold, read for a whole workspace at once
// (a handful of grouped queries, not a query per member), scored by RiskScoring, kept one row
// per active member, and the crossings into at risk handed to the save actions.
public sealed partial class ClubRetentionService
{
    /// <summary>
    /// The local time from which a workspace's daily scoring goes: after the billing run (from
    /// 03:00) has marked what is overdue and lapsed whom it lapses, so the score reads that day's books.
    /// </summary>
    public static readonly TimeSpan RunsFromLocalTime = TimeSpan.FromHours(5);

    /// <summary>A run still marked running this long after it began has stopped (its server went away).</summary>
    private static readonly TimeSpan StoppedAfter = TimeSpan.FromHours(2);

    /// <summary>The in-memory test database has no conditional update: crossings are claimed one at a time there.</summary>
    private static readonly SemaphoreSlim InMemoryClaims = new(1, 1);

    private sealed record MemberRow(
        Guid Id, string FirstName, string LastName, string Email, string Tier, string Status, DateTime JoinedAtUtc, DateTime? StartsOnUtc,
        Guid OwnerUserId, Guid? ContactId, string? PreferredLanguage)
    {
        public string Name => $"{FirstName} {LastName}".Trim();
    }

    /// <summary>What one scoring did, for the run's record.</summary>
    private sealed class Tally
    {
        public int Scored;
        public int BecameAtRisk;
        public int NoLongerAtRisk;
        public int Tasks;
        public int Emails;
        public List<string> Notes { get; } = new();
    }

    // ── reads ────────────────────────────────────────────────────────────────

    public async Task<Result<MemberRiskDto?>> MemberRiskAsync(Guid organizationId, Guid memberId, CancellationToken ct = default)
    {
        if (!await MemberExistsAsync(organizationId, memberId, ct)) return DomainErrors.Club.MemberNotFound;
        var row = await _db.ClubMemberRisks.AsNoTracking().FirstOrDefaultAsync(r => r.OrganizationId == organizationId && r.MemberId == memberId, ct);
        if (row == null) return Result.Success<MemberRiskDto?>(null);
        var settings = await RetentionRules.ReadAsync(_db, organizationId, ct);
        return Result.Success<MemberRiskDto?>(ToDto(row, settings, []));
    }

    public async Task<IReadOnlyList<AtRiskMemberDto>> AtRiskAsync(Guid organizationId, string? level, CancellationToken ct = default)
    {
        var wanted = level?.Trim().ToLowerInvariant() is RiskLevels.Watch ? RiskLevels.Watch : RiskLevels.AtRisk;
        var rows = await (from r in _db.ClubMemberRisks.AsNoTracking()
                          join m in _db.ClubMembers.AsNoTracking() on r.MemberId equals m.Id
                          where r.OrganizationId == organizationId && r.Level == wanted && m.ErasedAtUtc == null && m.Status == "Active"
                          orderby r.Score descending
                          select new { r, m.FirstName, m.LastName, m.Tier, m.Status, m.OwnerUserId })
            .Take(500).ToListAsync(ct);
        var owners = await OwnerNamesAsync(rows.Select(x => x.OwnerUserId), ct);
        return rows.Select(x => new AtRiskMemberDto(
                x.r.MemberId, $"{x.FirstName} {x.LastName}".Trim(), x.Tier, x.Status, x.r.Score, x.r.Level, x.r.ComputedAtUtc, x.r.AtRiskSinceUtc,
                x.OwnerUserId, owners.GetValueOrDefault(x.OwnerUserId),
                Factors(x.r.FactorsJson).Where(f => f.Points > 0).OrderByDescending(f => f.Points).Take(3).ToList()))
            .ToList();
    }

    public async Task<IReadOnlyList<ScoringRunDto>> RunsAsync(Guid organizationId, int take = 10, CancellationToken ct = default)
    {
        var rows = await _db.ClubScoringRuns.AsNoTracking()
            .Where(r => r.OrganizationId == organizationId)
            .OrderByDescending(r => r.StartedAtUtc).Take(Math.Clamp(take, 1, 50)).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    // ── working scores out ───────────────────────────────────────────────────

    public async Task<Result<MemberRiskDto>> RecomputeAsync(Guid organizationId, Guid memberId, Guid userId, CancellationToken ct = default)
    {
        var member = await _db.ClubMembers.AsNoTracking().FirstOrDefaultAsync(m => m.Id == memberId && m.OrganizationId == organizationId && m.ErasedAtUtc == null, ct);
        if (member == null) return DomainErrors.Club.MemberNotFound;
        if (member.Status != "Active") return DomainErrors.Retention.NotActive;
        var tally = new Tally();
        await ScoreAsync(organizationId, memberId, userId, DateTime.UtcNow, tally, ct);
        var row = await _db.ClubMemberRisks.AsNoTracking().FirstAsync(r => r.OrganizationId == organizationId && r.MemberId == memberId, ct);
        var settings = await RetentionRules.ReadAsync(_db, organizationId, ct);
        return ToDto(row, settings, tally.Notes);
    }

    public async Task<Result<ScoringRunDto>> RunNowAsync(Guid organizationId, Guid userId, CancellationToken ct = default)
    {
        // One at a time: a run still going (started in the last two hours and not finished) is waited for.
        var since = DateTime.UtcNow - StoppedAfter;
        if (await _db.ClubScoringRuns.AnyAsync(r => r.OrganizationId == organizationId && r.Status == "running" && r.StartedAtUtc > since, ct))
            return DomainErrors.Retention.ScoringRunning;
        var run = new ClubScoringRun
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            Trigger = ScoringRunTriggers.Manual,
            StartedAtUtc = DateTime.UtcNow,
            StartedByUserId = userId,
        };
        _db.ClubScoringRuns.Add(run);
        await _db.SaveChangesAsync(ct);
        await ExecuteAsync(organizationId, run, userId, ct);
        return ToDto(run);
    }

    public async Task<int> RunScheduledAsync(DateTime utcNow, CancellationToken ct = default)
    {
        // Across every workspace, as only this job reads: which have active members, and their
        // club on. Each is then scored acting for that workspace alone.
        var workspaces = await _db.ClubMembers.IgnoreQueryFilters().AsNoTracking()
            .Where(m => m.OrganizationId != null && m.Status == "Active" && m.ErasedAtUtc == null)
            .Select(m => m.OrganizationId!.Value).Distinct().ToListAsync(ct);
        var clubOff = (await _db.Organizations.AsNoTracking().Where(o => o.ClubModuleOff).Select(o => o.Id).ToListAsync(ct)).ToHashSet();

        var ran = 0;
        foreach (var organizationId in workspaces.Where(w => !clubOff.Contains(w)))
        {
            ct.ThrowIfCancellationRequested();
            var (_, _, _, _, owner, timeZone) = await RetentionRules.WorkspaceAsync(_db, organizationId, ct);
            var zone = RegionalSettings.ZoneOf(timeZone);
            var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone);
            if (local.TimeOfDay < RunsFromLocalTime) continue;
            var day = DateOnly.FromDateTime(local);
            // Across workspaces, by this one's id: whether another server has taken its day.
            if (await _db.ClubScoringRuns.IgnoreQueryFilters().AnyAsync(r => r.OrganizationId == organizationId && r.ScheduledFor == day, ct)) continue;

            using var acting = _db.ActFor(organizationId, owner == Guid.Empty ? null : owner);
            var run = new ClubScoringRun
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                Trigger = ScoringRunTriggers.Schedule,
                ScheduledFor = day,
                StartedAtUtc = utcNow,
            };
            _db.ClubScoringRuns.Add(run);
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (IsUniqueViolation(ex))
            {
                // Another server took the day between the look and the claim.
                _db.Entry(run).State = EntityState.Detached;
                continue;
            }
            ran++;
            // The system acts as the workspace's owner, as the renewal scan and Stripe's reports do.
            await ExecuteAsync(organizationId, run, owner, ct);
            // Nothing one workspace's run tracked is carried into the next one's.
            _db.ChangeTracker.Clear();
        }
        return ran;
    }

    /// <summary>Scores the workspace and notes its coming renewals; the run row is written once, at the end.</summary>
    private async Task ExecuteAsync(Guid organizationId, ClubScoringRun run, Guid actorUserId, CancellationToken ct)
    {
        var tally = new Tally();
        try
        {
            var now = DateTime.UtcNow;
            await ScoreAsync(organizationId, null, actorUserId, now, tally, ct);
            run.RenewalsTracked = await _renewals.TrackUpcomingAsync(organizationId, now, ct);
            run.Status = "succeeded";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "The scoring run {RunId} stopped part way", run.Id);
            foreach (var entry in _db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList())
                if (entry.Entity != run) entry.State = EntityState.Detached;
            run.Status = "failed";
            run.Failure = Clip($"It stopped part way: {ex.Message}", 2000);
        }
        run.FinishedAtUtc = DateTime.UtcNow;
        run.MembersScored = tally.Scored;
        run.BecameAtRisk = tally.BecameAtRisk;
        run.NoLongerAtRisk = tally.NoLongerAtRisk;
        run.TasksCreated = tally.Tasks;
        run.EmailsQueued = tally.Emails;
        if (_db.Entry(run).State == EntityState.Detached) _db.ClubScoringRuns.Attach(run).State = EntityState.Modified;
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Works out the scores of the workspace's active members (or one of them), keeps them, and
    /// runs the save actions for each member who has just crossed into at risk.
    /// </summary>
    private async Task ScoreAsync(Guid organizationId, Guid? onlyMemberId, Guid actorUserId, DateTime now, Tally tally, CancellationToken ct,
        bool retried = false)
    {
        var settings = await RetentionRules.ReadAsync(_db, organizationId, ct);
        var weights = RetentionRules.Weights(settings);
        var workspace = await RetentionRules.WorkspaceAsync(_db, organizationId, ct);
        string Money(decimal amount, string currency) => RetentionRules.Money(amount, currency, workspace.Culture);

        var members = await _db.ClubMembers.AsNoTracking()
            .Where(m => m.OrganizationId == organizationId && m.Status == "Active" && m.ErasedAtUtc == null
                && (onlyMemberId == null || m.Id == onlyMemberId))
            .Select(m => new MemberRow(m.Id, m.FirstName, m.LastName, m.Email, m.Tier, m.Status, m.JoinedAtUtc, m.StartsOnUtc, m.OwnerUserId, m.ContactId,
                m.PreferredLanguage))
            .ToListAsync(ct);
        var signals = await SignalsAsync(organizationId, onlyMemberId, members, now, workspace.Currency, ct);

        var existing = await _db.ClubMemberRisks
            .Where(r => r.OrganizationId == organizationId && (onlyMemberId == null || r.MemberId == onlyMemberId))
            .ToListAsync(ct);
        var byMember = existing.GroupBy(r => r.MemberId).ToDictionary(g => g.Key, g => g.First());
        var crossed = new List<(MemberRow Member, ClubMemberRisk Row, RiskResult Result)>();
        foreach (var member in members)
        {
            var result = RiskScoring.Score(signals[member.Id], weights, Money, byMember.GetValueOrDefault(member.Id)?.AtRiskSinceUtc != null);
            if (!byMember.TryGetValue(member.Id, out var row))
            {
                row = new ClubMemberRisk { Id = Guid.NewGuid(), OrganizationId = organizationId, MemberId = member.Id };
                _db.ClubMemberRisks.Add(row);
                // A member's first score is the baseline: at risk already, they are recorded so,
                // and nothing is set off. Switching scoring on must not email every member at once.
                if (result.Level == RiskLevels.AtRisk) row.AtRiskSinceUtc = now;
            }
            else
            {
                row.PreviousScore = row.Score;
                if (result.Level == RiskLevels.AtRisk && row.AtRiskSinceUtc == null) crossed.Add((member, row, result));
                else if (result.Level != RiskLevels.AtRisk && row.AtRiskSinceUtc != null)
                {
                    row.AtRiskSinceUtc = null;
                    tally.NoLongerAtRisk++;
                }
            }
            row.Score = result.Score;
            row.Level = result.Level;
            row.FactorsJson = JsonSerializer.Serialize(result.Factors, Json);
            row.ComputedAtUtc = now;
            tally.Scored++;
        }
        // A member who is no longer active (lapsed, left, erased, deleted) has no score: one who
        // comes back starts again from a new baseline.
        if (onlyMemberId == null)
        {
            var active = members.Select(m => m.Id).ToHashSet();
            _db.ClubMemberRisks.RemoveRange(existing.Where(r => !active.Contains(r.MemberId)));
        }
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (!retried && IsUniqueViolation(ex))
        {
            // A recompute and the daily run gave a new member their first score at the same
            // moment (one row per member): let go of this one and score again over the other's.
            foreach (var entry in _db.ChangeTracker.Entries<ClubMemberRisk>().ToList()) entry.State = EntityState.Detached;
            tally.Scored = 0;
            tally.NoLongerAtRisk = 0;
            await ScoreAsync(organizationId, onlyMemberId, actorUserId, now, tally, ct, retried: true);
            return;
        }

        foreach (var (member, row, result) in crossed)
        {
            // Claimed with one conditional update, so a recompute and the daily run at the same
            // moment set the actions off once.
            if (!await ClaimCrossingAsync(row.Id, now, ct)) continue;
            row.AtRiskSinceUtc = now;
            tally.BecameAtRisk++;
            if (row.SaveActionsAtUtc is DateTime last && now - last < TimeSpan.FromDays(settings.SaveCooldownDays))
            {
                tally.Notes.Add($"{member.Name} is at risk again; the save actions ran on {last:yyyy-MM-dd}, within the club's {settings.SaveCooldownDays} days, so they did not run again.");
                await _db.SaveChangesAsync(ct);
                continue;
            }
            await SaveActionsAsync(organizationId, member, row, result, settings, workspace.Name, workspace.OwnerUserId, actorUserId, now, tally, ct);
        }
    }

    /// <summary>Marks the member at risk if nobody has since; answers whether this call did.</summary>
    private async Task<bool> ClaimCrossingAsync(Guid riskId, DateTime now, CancellationToken ct)
    {
        if (_db.Database.IsRelational())
        {
            var moved = await _db.ClubMemberRisks
                .Where(r => r.Id == riskId && r.AtRiskSinceUtc == null)
                .ExecuteUpdateAsync(set => set.SetProperty(r => r.AtRiskSinceUtc, now), ct);
            return moved == 1;
        }
        await InMemoryClaims.WaitAsync(ct);
        try
        {
            var fresh = await _db.ClubMemberRisks.AsNoTracking().FirstOrDefaultAsync(r => r.Id == riskId, ct);
            if (fresh == null || fresh.AtRiskSinceUtc != null) return false;
            var tracked = _db.ClubMemberRisks.Local.FirstOrDefault(r => r.Id == riskId) ?? await _db.ClubMemberRisks.FirstAsync(r => r.Id == riskId, ct);
            tracked.AtRiskSinceUtc = now;
            await _db.SaveChangesAsync(ct);
            return true;
        }
        finally
        {
            InMemoryClaims.Release();
        }
    }

    // ── the signs, for a whole workspace at once ─────────────────────────────

    private async Task<Dictionary<Guid, MemberSignals>> SignalsAsync(Guid organizationId, Guid? onlyMemberId, IReadOnlyList<MemberRow> members,
        DateTime now, string currency, CancellationToken ct)
    {
        var recentFrom = now.AddDays(-RiskScoring.RecentDays);
        var baselineFrom = recentFrom.AddDays(-RiskScoring.BaselineDays);
        var eventRecentFrom = now.AddDays(-RiskScoring.EventRecentDays);
        var eventFrom = eventRecentFrom.AddDays(-RiskScoring.EventRecentDays);
        var concernsFrom = now.AddDays(-365);

        var visits = (await _db.ClubVisits.AsNoTracking()
                .Where(v => v.OrganizationId == organizationId && v.VisitedAtUtc >= baselineFrom && v.VisitedAtUtc <= now
                    && (onlyMemberId == null || v.MemberId == onlyMemberId))
                .GroupBy(v => v.MemberId)
                .Select(g => new
                {
                    MemberId = g.Key,
                    Recent = g.Sum(v => v.VisitedAtUtc >= recentFrom ? 1 : 0),
                    Before = g.Sum(v => v.VisitedAtUtc < recentFrom ? 1 : 0),
                })
                .ToListAsync(ct))
            .ToDictionary(x => x.MemberId);

        // What a member spent: charges and their voids, other than dues (which run whatever the
        // member does), by whoever ran them up (a spouse whose bills roll up spent them, not the
        // primary). In the workspace's currency; another currency is not added to it.
        var spend = (await _db.ClubCharges.AsNoTracking()
                .Where(c => c.OrganizationId == organizationId && c.PostedAtUtc >= baselineFrom && c.PostedAtUtc <= now
                    && (c.EntryType == ClubEntryTypes.Charge || c.EntryType == ClubEntryTypes.Void) && c.Kind != "Dues" && c.Currency == currency
                    && (onlyMemberId == null || (c.IncurredByMemberId ?? c.MemberId) == onlyMemberId))
                .Select(c => new { MemberId = c.IncurredByMemberId ?? c.MemberId, c.Amount, c.PostedAtUtc })
                .GroupBy(x => x.MemberId)
                .Select(g => new
                {
                    MemberId = g.Key,
                    Recent = g.Sum(x => x.PostedAtUtc >= recentFrom ? x.Amount : 0m),
                    Before = g.Sum(x => x.PostedAtUtc < recentFrom ? x.Amount : 0m),
                })
                .ToListAsync(ct))
            .ToDictionary(x => x.MemberId);

        var overdue = (await ClubOverdue.ListAsync(_db, organizationId, now, onlyMemberId is Guid one ? [one] : null, ct))
            .Where(c => string.Equals(c.Currency, currency, StringComparison.OrdinalIgnoreCase))
            .GroupBy(c => c.MemberId)
            .ToDictionary(g => g.Key, g => (Owed: g.Sum(c => c.Owed), OldestDays: (int)Math.Floor((now - g.Min(c => c.DueAtUtc)).TotalDays)));

        var reminders = (await _db.ClubPaymentReminders.AsNoTracking()
                .Where(r => r.OrganizationId == organizationId && r.CreatedAtUtc >= recentFrom && r.Status == ReminderStatuses.Queued
                    && (onlyMemberId == null || r.MemberId == onlyMemberId))
                .GroupBy(r => r.MemberId).Select(g => new { MemberId = g.Key, Count = g.Count() })
                .ToListAsync(ct))
            .ToDictionary(x => x.MemberId, x => x.Count);

        var places = await (from r in _db.ClubEventRegistrations.AsNoTracking()
                            join e in _db.ClubEvents.AsNoTracking() on r.EventId equals e.Id
                            where r.OrganizationId == organizationId && r.MemberId != null && e.StartAtUtc >= eventFrom && e.StartAtUtc <= now
                                && (r.Status == RegistrationStatuses.CheckedIn || r.Status == RegistrationStatuses.NoShow)
                                && (onlyMemberId == null || r.MemberId == onlyMemberId)
                            select new { MemberId = r.MemberId!.Value, r.Status, e.StartAtUtc })
            .ToListAsync(ct);
        var events = places.GroupBy(p => p.MemberId).ToDictionary(g => g.Key, g => (
            Recent: g.Count(p => p.Status == RegistrationStatuses.CheckedIn && p.StartAtUtc >= eventRecentFrom),
            Before: g.Count(p => p.Status == RegistrationStatuses.CheckedIn && p.StartAtUtc < eventRecentFrom),
            NoShows: g.Count(p => p.Status == RegistrationStatuses.NoShow && p.StartAtUtc >= eventRecentFrom)));

        var concernRows = await _db.ClubMemberConcerns.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId && (onlyMemberId == null || c.MemberId == onlyMemberId)
                && ((c.ResolvedAtUtc == null && c.RecordedAtUtc >= concernsFrom) || (c.ResolvedAtUtc != null && c.ResolvedAtUtc >= recentFrom)))
            .Select(c => new { c.MemberId, c.Kind, c.ResolvedAtUtc })
            .ToListAsync(ct);
        var concerns = concernRows.GroupBy(c => c.MemberId).ToDictionary(g => g.Key, g => (
            Complaints: g.Count(c => c.ResolvedAtUtc == null && c.Kind == ConcernKinds.Complaint),
            Concerns: g.Count(c => c.ResolvedAtUtc == null && c.Kind == ConcernKinds.Concern),
            Resolved: g.Count(c => c.ResolvedAtUtc != null)));

        var result = new Dictionary<Guid, MemberSignals>();
        foreach (var m in members)
        {
            var v = visits.GetValueOrDefault(m.Id);
            var s = spend.GetValueOrDefault(m.Id);
            var o = overdue.GetValueOrDefault(m.Id);
            var e = events.GetValueOrDefault(m.Id);
            var c = concerns.GetValueOrDefault(m.Id);
            result[m.Id] = new MemberSignals(
                now, RetentionRules.MemberSince(m.JoinedAtUtc, m.StartsOnUtc, now),
                v?.Recent ?? 0, v?.Before ?? 0,
                s?.Recent ?? 0m, s?.Before ?? 0m, currency,
                o.Owed, o.OldestDays, reminders.GetValueOrDefault(m.Id),
                e.Recent, e.Before, e.NoShows,
                c.Complaints, c.Concerns, c.Resolved);
        }
        return result;
    }

    // ── answers ──────────────────────────────────────────────────────────────

    private static IReadOnlyList<RiskFactor> Factors(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<RiskFactor>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static MemberRiskDto ToDto(ClubMemberRisk r, ClubRetentionSettings settings, IReadOnlyList<string> notes) => new()
    {
        MemberId = r.MemberId,
        Score = r.Score,
        Level = r.Level,
        Factors = Factors(r.FactorsJson),
        ComputedAtUtc = r.ComputedAtUtc,
        PreviousScore = r.PreviousScore,
        AtRiskSinceUtc = r.AtRiskSinceUtc,
        SaveActionsAtUtc = r.SaveActionsAtUtc,
        SaveTaskId = r.SaveTaskId,
        AtRiskThreshold = settings.AtRiskThreshold,
        WatchThreshold = settings.WatchThreshold,
        Notes = notes,
    };

    private static ScoringRunDto ToDto(ClubScoringRun r) => new(
        r.Id, r.Trigger, r.Status, r.StartedAtUtc, r.FinishedAtUtc, r.MembersScored, r.BecameAtRisk, r.NoLongerAtRisk,
        r.TasksCreated, r.EmailsQueued, r.RenewalsTracked, r.Failure);

    private async Task<Dictionary<Guid, string>> OwnerNamesAsync(IEnumerable<Guid> userIds, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToList();
        return await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Name, ct);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    private static string Clip(string value, int max) => value.Length <= max ? value : value[..max];
}
