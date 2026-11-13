using System.Collections.Concurrent;
using ACI.Application.Common;
using ACI.Application.Configuration;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Infrastructure.Services.Assistant;

/// <inheritdoc cref="IAiBudget"/>
/// <remarks>
/// <para>
/// A call is taken by one conditional UPDATE on the month's row ("Calls = Calls + 1 where
/// Calls is below the limit"), so two servers, or two people at the same moment, cannot both
/// take the last one. The month's row is made by the first call; two first calls racing to make
/// it meet the unique index, and the loser takes its call from the winner's row.
/// </para>
/// <para>
/// It reads its row past the workspace filters, naming it by organization (or, outside any, by
/// person) and month: it runs in requests and in the morning brief job, which acts for no
/// workspace, and the row it names is the only one it touches.
/// </para>
/// </remarks>
public sealed class AiBudget : IAiBudget
{
    private readonly AppDbContext _db;
    private readonly AssistantOptions _options;
    private readonly AiWorkspaceThrottle _throttle;
    private readonly ILogger<AiBudget> _logger;

    public AiBudget(AppDbContext db, IOptions<AssistantOptions> options, AiWorkspaceThrottle throttle, ILogger<AiBudget> logger)
    {
        _db = db;
        _options = options.Value;
        _throttle = throttle;
        _logger = logger;
    }

    public async Task<AiAllowance> AllowanceAsync(Guid? organizationId, Guid userId, CancellationToken ct = default)
    {
        var (month, resets) = MonthOf(DateTime.UtcNow);
        var limit = await LimitAsync(organizationId, ct);
        var row = await Rows(organizationId, userId, month).AsNoTracking().FirstOrDefaultAsync(ct);
        return Allowance(row, limit, month, resets);
    }

    public async Task<Result<AiAllowance>> TakeAsync(Guid? organizationId, Guid userId, bool personWaiting, CancellationToken ct = default)
    {
        var (month, resets) = MonthOf(DateTime.UtcNow);
        var limit = await LimitAsync(organizationId, ct);
        if (limit <= 0) return DomainErrors.Ai.MonthlyLimit(limit, resets, organizationId == null);

        // A team asking at once is slowed before anything is counted, so the month's allowance
        // is not spent by a burst the workspace's throttle refuses anyway.
        if (personWaiting && !_throttle.TryTake(ThrottleKey(organizationId, userId)))
            return DomainErrors.Ai.Busy;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (await TryIncrementAsync(organizationId, userId, month, limit, ct))
                return await AllowanceAsync(organizationId, userId, ct);

            var exists = await Rows(organizationId, userId, month).AnyAsync(ct);
            if (exists)
            {
                await CountRefusalAsync(organizationId, userId, month, ct);
                return DomainErrors.Ai.MonthlyLimit(limit, resets, organizationId == null);
            }
            if (await TryCreateAsync(organizationId, userId, month, ct))
                return await AllowanceAsync(organizationId, userId, ct);
            // Another call made the month's row first: take this one from it.
        }
        _logger.LogWarning("An AI call could not be counted for workspace {OrganizationId}; it was refused", organizationId);
        return DomainErrors.Ai.Busy;
    }

    public async Task RecordTokensAsync(Guid? organizationId, Guid userId, int inputTokens, int outputTokens, CancellationToken ct = default)
    {
        if (inputTokens <= 0 && outputTokens <= 0) return;
        var (month, _) = MonthOf(DateTime.UtcNow);
        try
        {
            if (_db.Database.IsRelational())
            {
                await Rows(organizationId, userId, month).ExecuteUpdateAsync(set => set
                    .SetProperty(u => u.InputTokens, u => u.InputTokens + inputTokens)
                    .SetProperty(u => u.OutputTokens, u => u.OutputTokens + outputTokens), ct);
                return;
            }
            // The in-memory test host has no UPDATE statements; one process, so no race.
            var row = await Rows(organizationId, userId, month).FirstOrDefaultAsync(ct);
            if (row == null) return;
            row.InputTokens += inputTokens;
            row.OutputTokens += outputTokens;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The call it follows has been made and answered: a count that could not be written
            // is logged, never turned into a failed answer.
            _logger.LogWarning(ex, "The tokens of an AI call could not be added up for workspace {OrganizationId}", organizationId);
        }
    }

    /// <summary>The limit that applies: the workspace's own, else the server's default; outside any organization, the personal one.</summary>
    private async Task<int> LimitAsync(Guid? organizationId, CancellationToken ct)
    {
        if (organizationId is not Guid org) return Math.Max(0, _options.PersonalMonthlyLimit);
        // Read past the filters by its organization: the morning brief job acts for no workspace.
        var own = await _db.AssistantSettings.AsNoTracking().IgnoreQueryFilters()
            .Where(s => s.OrganizationId == org).Select(s => s.MonthlyLimit).FirstOrDefaultAsync(ct);
        return EffectiveLimit(own, _options);
    }

    /// <summary>A workspace's own limit when it set one, else the server's default, never above the server's ceiling.</summary>
    public static int EffectiveLimit(int? own, AssistantOptions options)
    {
        var ceiling = Math.Max(0, options.MaxMonthlyLimit);
        return Math.Clamp(own ?? options.DefaultMonthlyLimit, 0, ceiling);
    }

    private IQueryable<AiUsage> Rows(Guid? organizationId, Guid userId, DateOnly month) =>
        organizationId is Guid org
            ? _db.AiUsage.IgnoreQueryFilters().Where(u => u.OrganizationId == org && u.UserId == null && u.Month == month)
            : _db.AiUsage.IgnoreQueryFilters().Where(u => u.OrganizationId == null && u.UserId == userId && u.Month == month);

    private async Task<bool> TryIncrementAsync(Guid? organizationId, Guid userId, DateOnly month, int limit, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (_db.Database.IsRelational())
        {
            var taken = await Rows(organizationId, userId, month).Where(u => u.Calls < limit)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(u => u.Calls, u => u.Calls + 1)
                    .SetProperty(u => u.UpdatedAtUtc, now), ct);
            return taken == 1;
        }
        var row = await Rows(organizationId, userId, month).FirstOrDefaultAsync(ct);
        if (row == null || row.Calls >= limit) return false;
        row.Calls++;
        row.UpdatedAtUtc = now;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<bool> TryCreateAsync(Guid? organizationId, Guid userId, DateOnly month, CancellationToken ct)
    {
        var row = new AiUsage
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = organizationId == null ? userId : null,
            Month = month,
            Calls = 1,
            UpdatedAtUtc = DateTime.UtcNow,
        };
        _db.AiUsage.Add(row);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            _db.Entry(row).State = EntityState.Detached;
            return false;
        }
    }

    private async Task CountRefusalAsync(Guid? organizationId, Guid userId, DateOnly month, CancellationToken ct)
    {
        try
        {
            if (_db.Database.IsRelational())
            {
                await Rows(organizationId, userId, month).ExecuteUpdateAsync(set => set.SetProperty(u => u.Refused, u => u.Refused + 1), ct);
                return;
            }
            var row = await Rows(organizationId, userId, month).FirstOrDefaultAsync(ct);
            if (row == null) return;
            row.Refused++;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "A refused AI call could not be counted for workspace {OrganizationId}", organizationId);
        }
    }

    private static AiAllowance Allowance(AiUsage? row, int limit, DateOnly month, DateOnly resets) =>
        new(row?.Calls ?? 0, limit, month, resets, row?.Refused ?? 0, row?.InputTokens ?? 0, row?.OutputTokens ?? 0);

    /// <summary>The calendar month (UTC) of <paramref name="nowUtc"/>, and the day the next one starts.</summary>
    public static (DateOnly Month, DateOnly ResetsOn) MonthOf(DateTime nowUtc)
    {
        var month = new DateOnly(nowUtc.Year, nowUtc.Month, 1);
        return (month, month.AddMonths(1));
    }

    private static string ThrottleKey(Guid? organizationId, Guid userId) =>
        organizationId is Guid org ? $"org:{org}" : $"user:{userId}";
}

/// <summary>
/// A short-term allowance per workspace (Assistant:WorkspaceBurst calls at once, then
/// Assistant:WorkspacePerMinute a minute), so a whole team, or one compromised account in it,
/// cannot spend the month in a few minutes. Kept in memory per server, like the other rate limits.
/// </summary>
/// <remarks>
/// Checked in the service rather than as an ASP.NET rate-limit policy: the workspace is known for
/// certain only after the membership check, which runs after the rate limiter, and a policy keyed
/// on the header would have let anyone pick the bucket by naming another workspace.
/// </remarks>
public sealed class AiWorkspaceThrottle
{
    private readonly ConcurrentDictionary<string, Bucket> _buckets = new();
    private readonly AssistantOptions _options;
    private readonly TimeProvider _clock;

    public AiWorkspaceThrottle(IOptions<AssistantOptions> options, TimeProvider? clock = null)
    {
        _options = options.Value;
        _clock = clock ?? TimeProvider.System;
    }

    public bool TryTake(string key)
    {
        var burst = Math.Max(1, _options.WorkspaceBurst);
        var perMinute = Math.Max(0, _options.WorkspacePerMinute);
        var now = _clock.GetUtcNow();
        var bucket = _buckets.GetOrAdd(key, _ => new Bucket { Tokens = burst, Last = now });
        lock (bucket)
        {
            var minutes = (now - bucket.Last).TotalMinutes;
            if (minutes > 0) bucket.Tokens = Math.Min(burst, bucket.Tokens + minutes * perMinute);
            bucket.Last = now;
            if (bucket.Tokens < 1) return false;
            bucket.Tokens -= 1;
            return true;
        }
    }

    private sealed class Bucket
    {
        public double Tokens;
        public DateTimeOffset Last;
    }
}
