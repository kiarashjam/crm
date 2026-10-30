using System.Globalization;
using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ACI.Infrastructure.Services;

/// <summary>
/// Forecasts: open deals by the month they are expected to close in, and the saved
/// monthly forecasts a manager commits to.
/// </summary>
/// <remarks>
/// <para>
/// The saved forecast counted every open deal in whatever month it was saved, so a
/// deal expected in March next year was in October's forecast at full weight, and the
/// month could never be set against what closed in it. A month's forecast is now the
/// deals expected to close in that month, by each deal's expected close date. Deals
/// past their date and deals with none are counted beside it, not in it.
/// </para>
/// <para>
/// Close dates are stored as the UTC midnight of the chosen day (the browser sends
/// the date as typed), so their month is the day's own, read as it is stored. The
/// moment a deal was won is a real instant, and is put in the month it fell in on the
/// workspace's calendar, in its time zone: read in UTC, a deal won in Zurich in the
/// first hours of November counted in October. So is "this month", when nobody names
/// one, and "today", which close dates before are overdue.
/// </para>
/// </remarks>
public partial class ReportingService
{
    private const int MaxForecastMonths = 24;

    private static DateTime MonthStart(int year, int month) => new(year, month, 1, 0, 0, 0, DateTimeKind.Utc);

    private static bool IsMonth(int year, int month) => year is >= 2000 and <= 2100 && month is >= 1 and <= 12;

    private static string MonthKey(int year, int month) => $"{year:D4}-{month:D2}";

    public async Task<Result<ForecastDto>> GetForecastAsync(Guid userId, Guid? organizationId, ForecastFilter filter, CancellationToken ct = default)
    {
        var (fallback, zone) = await RegionOfAsync(organizationId, ct);
        var firstMonth = filter.FirstMonthUtc ?? RegionalSettings.TodayIn(zone, DateTime.UtcNow).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        if (filter.Months is < 1 or > MaxForecastMonths || !IsMonth(firstMonth.Year, firstMonth.Month))
            return DomainErrors.Reporting.InvalidMonth;
        var currency = NormaliseFilterCurrency(filter.Currency, out var currencyValid);
        if (!currencyValid) return DomainErrors.Reporting.InvalidCurrency;
        var scoping = await ResolveScopeAsync(userId, organizationId, filter.Scope, filter.TeamId, ct);
        if (scoping.IsFailure) return scoping.Error;
        var pipeline = await CheckPipelineAsync(organizationId, filter.PipelineId, ct);
        if (pipeline.IsFailure) return pipeline.Error;

        var first = MonthStart(firstMonth.Year, firstMonth.Month);
        var end = first.AddMonths(filter.Months);
        var facts =FactsOf(DealsInScope(userId, organizationId, scoping.Value, filter.PipelineId), fallback);
        if (currency != null) facts = facts.Where(r => r.Currency == currency);
        var open = facts.Where(r => r.IsWon == null);

        var expected = await open
            .Where(r => r.ExpectedCloseDateUtc >= first && r.ExpectedCloseDateUtc < end)
            .GroupBy(r => new { r.ExpectedCloseDateUtc!.Value.Year, r.ExpectedCloseDateUtc.Value.Month, r.Currency })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Currency,
                Count = g.Count(),
                Value = g.Sum(r => r.Amount ?? 0m),
                Weighted = g.Sum(r => r.Amount != null && r.Probability != null ? r.Amount.Value * r.Probability.Value / 100m : 0m),
                Covered = g.Count(r => r.Amount != null && r.Probability != null),
                Missing = g.Count(r => r.Probability == null),
                Unreadable = g.Count(r => r.Amount == null),
            })
            .ToListAsync(ct);
        // Wins are counted by the UTC hour they happened in, by the database, and each hour
        // is then put in the month it is in on the workspace's calendar. A month's edges
        // are the workspace's midnights, which fall on a UTC hour in any zone that is a
        // whole number of hours from UTC (Zurich, all of Europe and the Americas).
        var wonFrom = RegionalSettings.StartOfDayUtc(DateOnly.FromDateTime(first), zone);
        var wonTo = RegionalSettings.StartOfDayUtc(DateOnly.FromDateTime(end), zone);
        var wonHours = await facts
            .Where(r => r.IsWon == true && r.ClosedAtUtc >= wonFrom && r.ClosedAtUtc < wonTo)
            .GroupBy(r => new { r.ClosedAtUtc!.Value.Year, r.ClosedAtUtc.Value.Month, r.ClosedAtUtc.Value.Day, r.ClosedAtUtc.Value.Hour, r.Currency })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, g.Key.Currency, Count = g.Count(), Value = g.Sum(r => r.Amount ?? 0m) })
            .ToListAsync(ct);
        var won = wonHours
            .Select(h => new
            {
                Local = TimeZoneInfo.ConvertTimeFromUtc(new DateTime(h.Year, h.Month, h.Day, h.Hour, 0, 0, DateTimeKind.Utc), zone),
                h.Currency,
                h.Count,
                h.Value,
            })
            .GroupBy(h => new { h.Local.Year, h.Local.Month, h.Currency })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Currency, Count = g.Sum(h => h.Count), Value = g.Sum(h => h.Value) })
            .ToList();

        // One row for every month and every currency seen, empty ones included, so a
        // month with nothing expected reads as zero rather than as missing.
        var currencies = expected.Select(e => e.Currency).Concat(won.Select(w => w.Currency))
            .Distinct().OrderBy(c => c, StringComparer.Ordinal).ToList();
        var rows = new List<ForecastMonthRowDto>();
        for (var month = first; month < end; month = month.AddMonths(1))
        {
            foreach (var c in currencies)
            {
                var e = expected.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month && x.Currency == c);
                var w = won.FirstOrDefault(x => x.Year == month.Year && x.Month == month.Month && x.Currency == c);
                rows.Add(new ForecastMonthRowDto(
                    MonthKey(month.Year, month.Month), c,
                    e?.Count ?? 0, e?.Value ?? 0m, Math.Round(e?.Weighted ?? 0m, 2), e?.Covered ?? 0, e?.Missing ?? 0, e?.Unreadable ?? 0,
                    w?.Count ?? 0, w?.Value ?? 0m));
            }
        }

        // A close date is a day, so what is overdue is what is due before the workspace's today.
        var today = RegionalSettings.TodayIn(zone, DateTime.UtcNow).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        return new ForecastDto(
            MonthKey(first.Year, first.Month),
            filter.Months,
            ScopeName(scoping.Value.Scope),
            scoping.Value.TeamId,
            rows,
            (await FiguresAsync(open.Where(r => r.ExpectedCloseDateUtc < today), ct)).ByCurrency,
            (await FiguresAsync(open.Where(r => r.ExpectedCloseDateUtc == null), ct)).ByCurrency);
    }

    public async Task<Result<ForecastSnapshotSetDto>> SaveForecastSnapshotAsync(Guid userId, Guid? organizationId, int? year, int? month, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var (fallback, zone) = await RegionOfAsync(organizationId, ct);
        // "This month" is the workspace's. Read in UTC, a Zurich manager saving at 00:30 on
        // 1 November saved October's forecast again instead of November's.
        var today = RegionalSettings.TodayIn(zone, now);
        var y = year ?? today.Year;
        var m = month ?? today.Month;
        if (!IsMonth(y, m)) return DomainErrors.Reporting.InvalidMonth;
        var start = MonthStart(y, m);
        var end = start.AddMonths(1);
        // A month can be saved for until it ends on the workspace's calendar. Saving
        // afterwards would be writing down a forecast once the outcome was known.
        if (now >= RegionalSettings.StartOfDayUtc(DateOnly.FromDateTime(end), zone)) return DomainErrors.Reporting.MonthOver;
        if (start > now.AddMonths(12)) return DomainErrors.Reporting.MonthTooFar;

        var lines = await FactsOf(ScopedDeals(userId, organizationId).Where(d => d.IsWon == null), fallback)
            .GroupBy(r => new { r.AssigneeId, r.Currency })
            .Select(g => new
            {
                g.Key.AssigneeId,
                g.Key.Currency,
                Value = g.Sum(r => r.ExpectedCloseDateUtc >= start && r.ExpectedCloseDateUtc < end ? r.Amount ?? 0m : 0m),
                Weighted = g.Sum(r => r.ExpectedCloseDateUtc >= start && r.ExpectedCloseDateUtc < end && r.Amount != null && r.Probability != null
                    ? r.Amount.Value * r.Probability.Value / 100m
                    : 0m),
                Covered = g.Count(r => r.ExpectedCloseDateUtc >= start && r.ExpectedCloseDateUtc < end && r.Amount != null && r.Probability != null),
                Missing = g.Count(r => r.ExpectedCloseDateUtc >= start && r.ExpectedCloseDateUtc < end && r.Amount != null && r.Probability == null),
                Unreadable = g.Count(r => r.ExpectedCloseDateUtc >= start && r.ExpectedCloseDateUtc < end && r.Amount == null),
                OverdueCount = g.Count(r => r.ExpectedCloseDateUtc < start),
                OverdueValue = g.Sum(r => r.ExpectedCloseDateUtc < start ? r.Amount ?? 0m : 0m),
                NoCloseDate = g.Count(r => r.ExpectedCloseDateUtc == null),
            })
            .ToListAsync(ct);
        if (lines.Count == 0) return DomainErrors.Reporting.NothingToSave;

        var names = await NamesAsync(lines.Select(l => l.AssigneeId), ct);
        var snapshotId = Guid.NewGuid();
        // Added beside the month's earlier saves, never in place of them: the forecast
        // made on the 1st is what the month is judged against, whoever saves later.
        _db.ForecastSnapshots.AddRange(lines.Select(l => new ForecastSnapshot
        {
            Id = Guid.NewGuid(),
            SnapshotId = snapshotId,
            ByCloseDate = true,
            OrganizationId = organizationId,
            SavedByUserId = userId,
            MonthUtc = start,
            Currency = l.Currency,
            AssigneeId = l.AssigneeId,
            AssigneeName = l.AssigneeId is Guid id ? names.GetValueOrDefault(id, "Unknown") : "Unassigned",
            WeightedValue = Math.Round(l.Weighted, 2),
            OpenValue = l.Value,
            Covered = l.Covered,
            Missing = l.Missing,
            UnreadableCount = l.Unreadable,
            OverdueCount = l.OverdueCount,
            OverdueValue = l.OverdueValue,
            NoCloseDateCount = l.NoCloseDate,
            SavedAtUtc = now,
        }));
        await _db.SaveChangesAsync(ct);
        return await ListForecastSnapshotsAsync(userId, organizationId, y, m, snapshotId, ct);
    }

    public async Task<Result<ForecastSnapshotSetDto>> ListForecastSnapshotsAsync(
        Guid userId, Guid? organizationId, int? year, int? month, Guid? snapshotId = null, CancellationToken ct = default)
    {
        var (fallback, zone) = await RegionOfAsync(organizationId, ct);
        var today = RegionalSettings.TodayIn(zone, DateTime.UtcNow);
        var y = year ?? today.Year;
        var m = month ?? today.Month;
        if (!IsMonth(y, m)) return DomainErrors.Reporting.InvalidMonth;
        var start = MonthStart(y, m);
        var end = start.AddMonths(1);

        // A month has a few saves of a few lines each, so they are read whole.
        var lines = await ScopedSnapshots(userId, organizationId).AsNoTracking()
            .Where(s => s.MonthUtc == start)
            .ToListAsync(ct);
        var saves = lines
            .GroupBy(l => l.SnapshotId)
            .Select(g => new
            {
                Id = g.Key,
                SavedAt = g.Max(l => l.SavedAtUtc),
                By = g.First().SavedByUserId,
                ByCloseDate = g.All(l => l.ByCloseDate),
            })
            .OrderByDescending(s => s.SavedAt)
            .ToList();
        if (snapshotId is Guid wanted && saves.All(s => s.Id != wanted)) return DomainErrors.Reporting.SnapshotNotFound;
        var chosen = snapshotId ?? saves.FirstOrDefault()?.Id;

        var savers = await NamesAsync(saves.Select(s => (Guid?)s.By), ct);
        // What was won in the month, between the workspace's midnights that begin and end it.
        var wonFrom = RegionalSettings.StartOfDayUtc(DateOnly.FromDateTime(start), zone);
        var wonTo = RegionalSettings.StartOfDayUtc(DateOnly.FromDateTime(end), zone);
        var won = await FactsOf(ScopedDeals(userId, organizationId), fallback)
            .Where(r => r.IsWon == true && r.ClosedAtUtc >= wonFrom && r.ClosedAtUtc < wonTo)
            .GroupBy(r => new { r.AssigneeId, r.Currency })
            .Select(g => new { g.Key.AssigneeId, g.Key.Currency, Count = g.Count(), Value = g.Sum(r => r.Amount ?? 0m) })
            .ToListAsync(ct);
        var owners = await NamesAsync(won.Select(w => w.AssigneeId), ct);

        return new ForecastSnapshotSetDto(
            MonthKey(y, m),
            saves.Select(s => new ForecastSaveDto(s.Id, s.SavedAt, savers.GetValueOrDefault(s.By, "Unknown"), s.ByCloseDate)).ToList(),
            chosen,
            lines.Where(l => l.SnapshotId == chosen)
                .OrderBy(l => l.AssigneeName, StringComparer.Ordinal).ThenBy(l => l.Currency, StringComparer.Ordinal)
                .Select(ToDto)
                .ToList(),
            won.Select(w => new ForecastActualDto(
                    w.AssigneeId is Guid id ? owners.GetValueOrDefault(id, "Unknown") : "Unassigned",
                    w.Currency, w.Count, w.Value))
                .OrderBy(a => a.AssigneeName, StringComparer.Ordinal).ThenBy(a => a.Currency, StringComparer.Ordinal)
                .ToList());
    }

    /// <summary>
    /// An organization's saved forecast, or with none the caller's own. "OrganizationId
    /// IS NULL" alone was every personal user's, so saving one replaced the others'.
    /// </summary>
    private IQueryable<ForecastSnapshot> ScopedSnapshots(Guid userId, Guid? organizationId) =>
        organizationId == null
            ? _db.ForecastSnapshots.Where(s => s.OrganizationId == null && s.SavedByUserId == userId)
            : _db.ForecastSnapshots.Where(s => s.OrganizationId == organizationId);

    private static ForecastSnapshotDto ToDto(ForecastSnapshot row) =>
        new(row.Id, row.MonthUtc.ToString("yyyy-MM", CultureInfo.InvariantCulture), row.Currency, row.AssigneeName,
            row.WeightedValue, row.OpenValue, row.Covered, row.Missing, row.SavedAtUtc,
            row.SnapshotId, row.ByCloseDate, row.OverdueCount, row.OverdueValue, row.NoCloseDateCount, row.UnreadableCount);
}
