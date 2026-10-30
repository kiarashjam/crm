using System.Globalization;
using System.Text;
using ACI.Application.Configuration;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Infrastructure.Services;

/// <summary>
/// The weekly report digest: once a week, an email with last week's figures for each
/// workspace a person owns or manages, for those who turned it on in their settings.
/// </summary>
/// <remarks>
/// <para>
/// It goes out from 07:00 on Monday in the person's own time zone (their Timezone
/// setting), covering Monday to Sunday just ended. A week that was missed while the
/// app was down goes out at the next pass of the same week, never twice: a send is
/// claimed first by moving WeeklyDigestSentAtUtc, in one statement on SQL Server, so
/// two instances cannot both take it. A failed send gives the claim back for the
/// next pass to retry.
/// </para>
/// <para>
/// The figures come from <see cref="IReportingService"/>, read as the recipient, so a
/// digest says what the Reports page says to them and leaves out what it leaves out.
/// </para>
/// </remarks>
public sealed class WeeklyDigestService
{
    private readonly AppDbContext _db;
    private readonly IReportingService _reporting;
    private readonly IEmailSender _email;
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<WeeklyDigestService> _logger;

    public WeeklyDigestService(
        AppDbContext db, IReportingService reporting, IEmailSender email, IOptions<EmailSettings> emailSettings, ILogger<WeeklyDigestService> logger)
    {
        _db = db;
        _reporting = reporting;
        _email = email;
        _emailSettings = emailSettings.Value;
        _logger = logger;
    }

    /// <summary>The week a digest due at <paramref name="nowUtc"/> covers, and when it fell due.</summary>
    /// <returns>
    /// The latest Monday 07:00 at or before now in <paramref name="zone"/> (as UTC), and the
    /// Monday-to-Monday week before it, as UTC instants of local midnights.
    /// </returns>
    public static (DateTime DueUtc, DateTime WeekStartUtc, DateTime WeekEndUtc, DateOnly FirstDay, DateOnly LastDay) WeekFor(DateTime nowUtc, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), zone);
        var monday = local.Date.AddDays(-(((int)local.DayOfWeek + 6) % 7));
        if (local < monday.AddHours(7)) monday = monday.AddDays(-7);
        // A zone whose clocks jump at midnight skips that midnight; the moment they
        // jump to is when that day starts there.
        DateTime Utc(DateTime localTime)
        {
            var at = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
            while (zone.IsInvalidTime(at)) at = at.AddMinutes(30);
            return TimeZoneInfo.ConvertTimeToUtc(at, zone);
        }
        return (Utc(monday.AddHours(7)), Utc(monday.AddDays(-7)), Utc(monday),
            DateOnly.FromDateTime(monday.AddDays(-7)), DateOnly.FromDateTime(monday.AddDays(-1)));
    }

    /// <summary>Sends every digest that is due. Returns how many went.</summary>
    public async Task<int> SendDueAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        // Without a mail server nothing can be delivered; the digests stay due and go
        // once one is configured.
        if (string.IsNullOrWhiteSpace(_emailSettings.SmtpHost)) return 0;

        var subscribers = await _db.UserSettings.AsNoTracking()
            .Where(s => s.WeeklyDigestEnabled)
            .Select(s => new { s.UserId, s.Timezone, s.WeeklyDigestSentAtUtc })
            .ToListAsync(ct);

        var sent = 0;
        foreach (var subscriber in subscribers)
        {
            // One person's digest failing, however, never holds up anyone else's.
            try
            {
                var zone = ZoneOf(subscriber.Timezone);
                var week = WeekFor(nowUtc, zone);
                if (subscriber.WeeklyDigestSentAtUtc >= week.DueUtc) continue;
                if (await SendOneAsync(subscriber.UserId, subscriber.WeeklyDigestSentAtUtc, zone, week, nowUtc, ct)) sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "The weekly digest for user {UserId} could not be prepared", subscriber.UserId);
            }
        }
        return sent;
    }

    private async Task<bool> SendOneAsync(
        Guid userId, DateTime? previous, TimeZoneInfo zone,
        (DateTime DueUtc, DateTime WeekStartUtc, DateTime WeekEndUtc, DateOnly FirstDay, DateOnly LastDay) week,
        DateTime nowUtc, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.Email, u.Name }).FirstOrDefaultAsync(ct);
        if (user == null || string.IsNullOrWhiteSpace(user.Email)) return false;

        // A digest is for whoever answers for a workspace's numbers. A salesperson who
        // turns it on and manages nothing gets none, rather than a report of their own
        // pipeline dressed up as the team's.
        var workspaces = await _db.OrganizationMembers.AsNoTracking()
            .Where(m => m.UserId == userId && (m.Role == OrgMemberRole.Owner || m.Role == OrgMemberRole.Manager))
            .Join(_db.Organizations.AsNoTracking(), m => m.OrganizationId, o => o.Id, (m, o) => new { o.Id, o.Name })
            .OrderBy(o => o.Name)
            .ToListAsync(ct);
        if (workspaces.Count == 0) return false;

        if (!await ClaimAsync(userId, previous, week.DueUtc, nowUtc, ct)) return false;

        var delivered = false;
        try
        {
            // The forecast is for the month the digest arrives in: the Monday after the week.
            var arrives = week.LastDay.AddDays(1);
            var month = new DateTime(arrives.Year, arrives.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var sections = new List<DigestSection>();
            foreach (var workspace in workspaces)
            {
                var summary = await _reporting.GetSummaryAsync(userId, workspace.Id, new ReportFilter
                {
                    FromUtc = week.WeekStartUtc,
                    ToUtc = week.WeekEndUtc,
                    Scope = ReportScope.Organization,
                    TimeZone = zone.Id,
                }, ct);
                var forecast = await _reporting.GetForecastAsync(userId, workspace.Id, new ForecastFilter
                {
                    FirstMonthUtc = month,
                    Months = 1,
                    Scope = ReportScope.Organization,
                }, ct);
                if (summary.IsFailure || forecast.IsFailure) continue;
                sections.Add(new DigestSection(workspace.Name, summary.Value, forecast.Value));
            }

            var baseUrl = (_emailSettings.FrontendBaseUrl ?? string.Empty).Trim().TrimEnd('/');
            var (subject, body) = Compose(user.Name, week.FirstDay, week.LastDay, sections, $"{baseUrl}/reports");
            delivered = sections.Count > 0 && await _email.SendMessageAsync(user.Email, user.Name, subject, body, ct);
        }
        finally
        {
            if (!delivered)
            {
                // Give the week back, so the next pass tries again, also when working
                // out the figures threw or the app is stopping.
                await ReleaseAsync(userId, previous, nowUtc, CancellationToken.None);
                _logger.LogWarning("The weekly digest for user {UserId} was not delivered; it will be tried again", userId);
            }
        }
        return delivered;
    }

    /// <summary>Takes this week's digest for sending, unless someone (another instance) already has.</summary>
    private async Task<bool> ClaimAsync(Guid userId, DateTime? previous, DateTime dueUtc, DateTime nowUtc, CancellationToken ct)
    {
        if (_db.Database.IsRelational())
        {
            var claimed = await _db.UserSettings
                .Where(s => s.UserId == userId && (s.WeeklyDigestSentAtUtc == null || s.WeeklyDigestSentAtUtc < dueUtc))
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.WeeklyDigestSentAtUtc, (DateTime?)nowUtc), ct);
            return claimed == 1;
        }
        // The in-memory test host has no UPDATE statements; one process, so no race.
        var settings = await _db.UserSettings.SingleAsync(s => s.UserId == userId, ct);
        if (settings.WeeklyDigestSentAtUtc != previous) return false;
        settings.WeeklyDigestSentAtUtc = nowUtc;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private async Task ReleaseAsync(Guid userId, DateTime? previous, DateTime claimedAtUtc, CancellationToken ct)
    {
        if (_db.Database.IsRelational())
        {
            await _db.UserSettings
                .Where(s => s.UserId == userId && s.WeeklyDigestSentAtUtc == claimedAtUtc)
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.WeeklyDigestSentAtUtc, previous), ct);
            return;
        }
        var settings = await _db.UserSettings.SingleAsync(s => s.UserId == userId, ct);
        if (settings.WeeklyDigestSentAtUtc != claimedAtUtc) return;
        settings.WeeklyDigestSentAtUtc = previous;
        await _db.SaveChangesAsync(ct);
    }

    private static TimeZoneInfo ZoneOf(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id.Trim());
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }
        return TimeZoneInfo.Utc;
    }

    /// <summary>One workspace's figures in a digest.</summary>
    public sealed record DigestSection(string WorkspaceName, ReportSummaryDto Summary, ForecastDto Forecast);

    /// <summary>The digest's subject and plain-text body.</summary>
    public static (string Subject, string Body) Compose(
        string recipientName, DateOnly firstDay, DateOnly lastDay, IReadOnlyList<DigestSection> sections, string reportsUrl)
    {
        var inv = CultureInfo.InvariantCulture;
        var week = $"{firstDay.ToString("d MMM", inv)} – {lastDay.ToString("d MMM yyyy", inv)}";
        var body = new StringBuilder();
        body.Append("Hi").Append(string.IsNullOrWhiteSpace(recipientName) ? "" : " " + recipientName.Trim()).AppendLine(",");
        body.AppendLine();
        body.AppendLine($"Here is last week ({week}) for {(sections.Count == 1 ? sections[0].WorkspaceName : "your workspaces")}.");

        foreach (var section in sections)
        {
            var s = section.Summary;
            var forecastMonth = section.Forecast.FirstMonth;
            var monthName = DateTime.TryParseExact(forecastMonth, "yyyy-MM", inv, DateTimeStyles.None, out var m) ? m.ToString("MMMM", inv) : forecastMonth;
            var expected = section.Forecast.Rows.Where(r => r.Count > 0).ToList();
            var wonThisMonth = section.Forecast.Rows.Where(r => r.WonCount > 0).ToList();
            var weighted = s.Weighted.Where(w => w.Covered > 0).ToList();
            var missing = s.Weighted.Sum(w => w.Missing);

            body.AppendLine();
            body.AppendLine(section.WorkspaceName);
            body.AppendLine(new string('-', Math.Min(section.WorkspaceName.Length, 60)));
            body.AppendLine($"Won: {Deals(s.Won.Count)}{Money(s.Won.ByCurrency)}");
            body.AppendLine($"Lost: {Deals(s.Lost.Count)}");
            body.AppendLine($"New deals: {Deals(s.Created.Count)}{Money(s.Created.ByCurrency)}");
            body.AppendLine($"Open pipeline now: {Deals(s.Open.Count)}{Money(s.Open.ByCurrency)}");
            if (weighted.Count > 0)
            {
                body.AppendLine("  weighted: " + string.Join(" · ", weighted.Select(w => Amount(w.Currency, w.Value)))
                    + (missing > 0 ? $" ({missing} without a probability, left out)" : ""));
            }
            body.AppendLine($"Expected to close in {monthName}: {Deals(expected.Sum(r => r.Count))}"
                + (expected.Count == 0 ? "" : ", " + string.Join(" · ", expected.Select(r => Amount(r.Currency, r.Value)))
                    + " (weighted " + string.Join(" · ", expected.Select(r => Amount(r.Currency, r.Weighted))) + ")"));
            body.AppendLine($"Won so far in {monthName}: {Deals(wonThisMonth.Sum(r => r.WonCount))}"
                + (wonThisMonth.Count == 0 ? "" : ", " + string.Join(" · ", wonThisMonth.Select(r => Amount(r.Currency, r.WonValue)))));
            var overdue = section.Forecast.Overdue;
            if (overdue.Sum(r => r.Count) > 0)
                body.AppendLine($"Past their close date and still open: {Deals(overdue.Sum(r => r.Count))}{Money(overdue)}");
            body.AppendLine($"New leads: {s.Leads.Created} · converted: {s.Leads.Converted} · active leads now: {s.Leads.Active}");
            body.AppendLine($"Activities logged: {s.Activities.Total}");
        }

        body.AppendLine();
        body.AppendLine("Amounts are per currency and never converted. Deals whose value could not be read are counted but not added in.");
        body.AppendLine();
        body.AppendLine($"Open the reports: {reportsUrl}");
        body.AppendLine();
        body.AppendLine("You receive this because the weekly report digest is on in your notification settings. Turn it off there to stop it.");

        return ($"Your weekly report, {week}", body.ToString());
    }

    private static string Deals(int count) => count == 1 ? "1 deal" : $"{count.ToString(CultureInfo.InvariantCulture)} deals";

    private static string Amount(string currency, decimal value) =>
        $"{currency} {value.ToString("N0", CultureInfo.InvariantCulture)}";

    private static string Money(IReadOnlyList<MoneyRowDto> rows)
    {
        var readable = rows.Where(r => r.Count > r.Unreadable).ToList();
        var unreadable = rows.Sum(r => r.Unreadable);
        var text = readable.Count == 0 ? "" : ", " + string.Join(" · ", readable.Select(r => Amount(r.Currency, r.Value)));
        return unreadable > 0 ? $"{text} ({unreadable} with no readable value)" : text;
    }
}
