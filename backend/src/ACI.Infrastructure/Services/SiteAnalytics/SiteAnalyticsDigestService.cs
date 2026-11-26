using System.Globalization;
using System.Net;
using System.Text;
using ACI.Application.Configuration;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ACI.Infrastructure.Services.SiteAnalytics;

/// <inheritdoc cref="ISiteAnalyticsDigest"/>
/// <remarks>
/// <para>
/// Due from 07:30 Europe/Zurich, about the Zurich day before: its visitors, sessions, page views
/// and sign-ups, each against the day before it and the same weekday a week earlier; the seven
/// days to it; and its top pages and sources. It goes on a quiet day too: a new site's zero is
/// worth knowing, and the trend is the point.
/// </para>
/// <para>
/// It counts the website's visitors (<see cref="SiteAudience.Visitors"/>): what people did
/// signed out, and their sign-ups, never the app's signed-in use, the owner's own included.
/// </para>
/// <para>
/// Its pages and sources are what visitors' browsers sent, so the email shows them defanged: a
/// source is a classified kind (google, linkedin, email…) or a valid host, anything else
/// "other"; and every dot in a page or a source is written "[.]", so no mail client turns
/// "example.com" into a link (<see cref="Defang"/>).
/// </para>
/// <para>
/// One email per platform admin (Platform:AdminEmails), through the outbox as a platform email
/// (no workspace). The day's <see cref="SiteAnalyticsDigest"/> row is the claim: inserted once,
/// keyed by the day, so a second server or a restart never sends the day again. The emails are
/// queued after it is saved: a failure there costs that day's email, never a second one. With no
/// admin configured nothing is claimed, so one added later that morning still gets the day's.
/// </para>
/// </remarks>
public sealed class SiteAnalyticsDigestService : ISiteAnalyticsDigest
{
    /// <summary>When the day's email falls due, Zurich time.</summary>
    public static readonly TimeSpan DueAt = new(7, 30, 0);

    /// <summary>Rows in the email's top lists.</summary>
    public const int TopRows = 5;

    /// <summary>What a source that is neither a classified kind nor a host (a utm_source) is shown as.</summary>
    public const string OtherSource = "other";

    private readonly AppDbContext _db;
    private readonly ISiteAnalytics _analytics;
    private readonly IPlatformAdmins _admins;
    private readonly IOutbox _outbox;
    private readonly EmailSettings _email;
    private readonly ILogger<SiteAnalyticsDigestService> _logger;

    public SiteAnalyticsDigestService(AppDbContext db, ISiteAnalytics analytics, IPlatformAdmins admins, IOutbox outbox,
        IOptions<EmailSettings> email, ILogger<SiteAnalyticsDigestService> logger)
    {
        _db = db;
        _analytics = analytics;
        _admins = admins;
        _outbox = outbox;
        _email = email.Value;
        _logger = logger;
    }

    /// <summary>The day an email due at <paramref name="nowUtc"/> reports on (yesterday in Zurich); null before 07:30.</summary>
    public static DateOnly? DayFor(DateTime nowUtc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), SiteAnalyticsRules.Zone);
        if (local.TimeOfDay < DueAt) return null;
        return DateOnly.FromDateTime(local).AddDays(-1);
    }

    public async Task<int> SendDueAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        if (DayFor(nowUtc) is not { } day) return 0;
        if (await _db.SiteAnalyticsDigests.AsNoTracking().AnyAsync(d => d.Day == day, ct)) return 0;
        var recipients = _admins.Emails;
        if (recipients.Count == 0) return 0;

        var digest = await ReadAsync(day, ct);
        var link = $"{(_email.FrontendBaseUrl ?? "").Trim().TrimEnd('/')}{SiteAnalyticsRules.DashboardPath}";
        var (subject, text, html) = Compose(digest, link);

        if (!await ClaimAsync(day, recipients.Count, nowUtc, ct)) return 0;
        var queued = 0;
        foreach (var to in recipients)
        {
            try
            {
                var result = await _outbox.QueuePlatformEmailAsync(new PlatformEmailRequest
                {
                    Source = OutboundSources.SiteAnalyticsDigest,
                    To = to,
                    Subject = subject,
                    Body = text,
                    Html = html,
                }, ct);
                if (result.IsSuccess) queued++;
                else _logger.LogWarning("The site analytics email for {Day} was not queued for one admin: {Error}", day, result.Error.Description);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The day is claimed: never a second email by trying the day again.
                _logger.LogWarning(ex, "The site analytics email for {Day} could not be queued for one admin", day);
            }
        }
        return queued;
    }

    /// <summary>Takes the day by saving its row; false when it was taken already.</summary>
    private async Task<bool> ClaimAsync(DateOnly day, int recipients, DateTime nowUtc, CancellationToken ct)
    {
        var row = new SiteAnalyticsDigest { Day = day, SentAtUtc = nowUtc, Recipients = recipients };
        _db.SiteAnalyticsDigests.Add(row);
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex) when (ex is DbUpdateException or InvalidOperationException or ArgumentException)
        {
            // Another server took it first (SQL Server: a duplicate key; in memory: an argument error).
            _db.Entry(row).State = EntityState.Detached;
            _logger.LogInformation("The site analytics email for {Day} was taken by another server", day);
            return false;
        }
    }

    /// <summary>What one day's email says.</summary>
    /// <param name="Week">The seven days up to <paramref name="Day"/>, oldest first.</param>
    /// <param name="TopSources">The day's sources, most sessions first: the email shows the first <see cref="TopRows"/> of <see cref="ShownSources"/>.</param>
    public sealed record Digest(
        DateOnly Day,
        SiteDailyDto Yesterday,
        SiteDailyDto DayBefore,
        SiteDailyDto WeekBefore,
        IReadOnlyList<SiteDailyDto> Week,
        IReadOnlyList<SitePageDto> TopPages,
        IReadOnlyList<SiteSourceDto> TopSources,
        int BotHits);

    private async Task<Digest> ReadAsync(DateOnly day, CancellationToken ct)
    {
        // The day itself, for its pages and sources, then the eight days before it for the
        // comparisons: the website's visitors, not the app's signed-in users.
        var summary = await _analytics.SummaryAsync(day, day, SiteAudience.Visitors, ct);
        var weeks = await _analytics.SummaryAsync(day.AddDays(-7), day, SiteAudience.Visitors, ct);
        var daily = weeks.Daily;
        return new Digest(
            day,
            daily[^1],
            daily[^2],
            daily[0],
            daily.Skip(daily.Count - 7).ToList(),
            summary.TopPages.Take(TopRows).ToList(),
            summary.Sources,
            summary.Totals.BotHits);
    }

    /// <summary>
    /// The sources as the email names them: a classified kind or a valid host as it is, any other
    /// (a utm_source, which is whatever a link said) as <see cref="OtherSource"/>, those summed.
    /// </summary>
    public static IReadOnlyList<SiteSourceDto> ShownSources(IEnumerable<SiteSourceDto> sources) =>
        sources
            .GroupBy(s => SiteAnalyticsRules.SourceKinds.Contains(s.Source) || SiteAnalyticsRules.IsHostName(s.Source) ? s.Source : OtherSource)
            .Select(g => new SiteSourceDto(g.Key, g.Sum(s => s.Sessions), g.Sum(s => s.Visitors)))
            .OrderByDescending(s => s.Sessions).ThenBy(s => s.Source, StringComparer.Ordinal)
            .Take(TopRows)
            .ToList();

    /// <summary>Visitor-sent text with every dot written "[.]" ("example[.]com"), so a mail client makes no link of it.</summary>
    public static string Defang(string text) => text.Replace(".", "[.]", StringComparison.Ordinal);

    /// <summary>The subject, the plain text and the HTML of the email: counts and pages, nothing about anyone.</summary>
    public static (string Subject, string Text, string Html) Compose(Digest d, string link)
    {
        d = d with { TopPages = d.TopPages.Take(TopRows).ToList(), TopSources = ShownSources(d.TopSources) };
        var y = d.Yesterday;
        var dayName = d.Day.ToString("dddd d MMMM yyyy", CultureInfo.InvariantCulture);
        var weekday = d.Day.ToString("dddd", CultureInfo.InvariantCulture);
        var subject = $"cadencee.ai yesterday: {Visitors(y.Visitors)}, {Count(y.PageViews, "page view", "page views")}" +
            (y.Signups > 0 ? $", {Count(y.Signups, "sign-up", "sign-ups")}" : "");

        var metrics = new (string Label, Func<SiteDailyDto, int> Of)[]
        {
            ("Visitors", r => r.Visitors),
            ("Sessions", r => r.Sessions),
            ("Page views", r => r.PageViews),
            ("Sign-ups", r => r.Signups),
        };

        var text = new StringBuilder();
        text.AppendLine($"cadencee.ai on {dayName} (Europe/Zurich)");
        text.AppendLine();
        foreach (var (label, of) in metrics)
        {
            text.AppendLine($"{label}: {N(of(y))}   ({Change(of(y), of(d.DayBefore))} vs the day before, {Change(of(y), of(d.WeekBefore))} vs last {weekday})");
        }
        text.AppendLine();
        text.AppendLine($"Last 7 days (visitors): {Sparkline(d.Week.Select(w => w.Visitors).ToList())}");
        foreach (var w in d.Week)
        {
            var date = SiteAnalyticsRules.ParseDay(w.Date)!.Value;
            text.AppendLine($"  {date.ToString("ddd d MMM", CultureInfo.InvariantCulture),-11} {N(w.Visitors),5} visitors {N(w.PageViews),6} page views {N(w.Signups),4} sign-ups");
        }
        text.AppendLine();
        text.AppendLine("Top pages");
        if (d.TopPages.Count == 0) text.AppendLine("  (none)");
        for (var i = 0; i < d.TopPages.Count; i++)
            text.AppendLine($"  {i + 1}. {Defang(d.TopPages[i].Path)}  {Count(d.TopPages[i].Views, "view", "views")}, {Visitors(d.TopPages[i].Visitors)}");
        text.AppendLine();
        text.AppendLine("Top sources");
        if (d.TopSources.Count == 0) text.AppendLine("  (none)");
        for (var i = 0; i < d.TopSources.Count; i++)
            text.AppendLine($"  {i + 1}. {Defang(d.TopSources[i].Source)}  {Count(d.TopSources[i].Sessions, "session", "sessions")}, {Visitors(d.TopSources[i].Visitors)}");
        if (d.BotHits > 0)
        {
            text.AppendLine();
            text.AppendLine($"Robots: {Count(d.BotHits, "hit", "hits")} counted and left out of the figures above.");
        }
        text.AppendLine();
        text.AppendLine($"Open the dashboard: {link}");
        text.AppendLine();
        text.AppendLine("Website visitors only: what is done signed in to the app is left out, sign-ups excepted.");
        text.AppendLine("Counted without cookies. A visitor is counted once a day and cannot be followed from one day to the next.");
        text.AppendLine("You receive this because your address is a platform admin of CadenceE (Platform:AdminEmails).");

        return (subject, text.ToString(), Html(d, dayName, weekday, metrics, link));
    }

    private static string Html(Digest d, string dayName, string weekday, (string Label, Func<SiteDailyDto, int> Of)[] metrics, string link)
    {
        static string E(string s) => WebUtility.HtmlEncode(s);
        const string cell = "padding:6px 10px;border-bottom:1px solid #e5e7eb;";
        var y = d.Yesterday;
        var html = new StringBuilder();
        html.Append("<!doctype html><html><body style=\"margin:0;padding:24px;background:#f8fafc;\">");
        html.Append("<div style=\"max-width:600px;margin:0 auto;font-family:-apple-system,Segoe UI,Roboto,Arial,sans-serif;font-size:14px;line-height:1.5;color:#0f172a;\">");
        html.Append($"<h1 style=\"font-size:20px;margin:0 0 4px;\">cadencee.ai yesterday</h1><p style=\"margin:0 0 16px;color:#475569;\">{E(dayName)} (Europe/Zurich)</p>");

        html.Append("<table role=\"presentation\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:100%;border-collapse:collapse;background:#fff;border:1px solid #e5e7eb;\">");
        html.Append($"<tr style=\"background:#f1f5f9;text-align:left;\"><th style=\"{cell}\"></th><th style=\"{cell}text-align:right;\">Yesterday</th><th style=\"{cell}text-align:right;\">vs the day before</th><th style=\"{cell}text-align:right;\">vs last {E(weekday)}</th></tr>");
        foreach (var (label, of) in metrics)
        {
            html.Append($"<tr><td style=\"{cell}\">{E(label)}</td><td style=\"{cell}text-align:right;font-weight:600;\">{E(N(of(y)))}</td>");
            html.Append($"<td style=\"{cell}text-align:right;\">{E(Change(of(y), of(d.DayBefore)))}</td><td style=\"{cell}text-align:right;\">{E(Change(of(y), of(d.WeekBefore)))}</td></tr>");
        }
        html.Append("</table>");

        html.Append($"<h2 style=\"font-size:16px;margin:24px 0 8px;\">Last 7 days <span style=\"font-family:monospace;letter-spacing:2px;\">{E(Sparkline(d.Week.Select(w => w.Visitors).ToList()))}</span></h2>");
        html.Append("<table role=\"presentation\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:100%;border-collapse:collapse;background:#fff;border:1px solid #e5e7eb;\">");
        html.Append($"<tr style=\"background:#f1f5f9;text-align:left;\"><th style=\"{cell}\">Day</th><th style=\"{cell}text-align:right;\">Visitors</th><th style=\"{cell}text-align:right;\">Page views</th><th style=\"{cell}text-align:right;\">Sign-ups</th></tr>");
        foreach (var w in d.Week)
        {
            var date = SiteAnalyticsRules.ParseDay(w.Date)!.Value;
            html.Append($"<tr><td style=\"{cell}\">{E(date.ToString("ddd d MMM", CultureInfo.InvariantCulture))}</td><td style=\"{cell}text-align:right;\">{E(N(w.Visitors))}</td>");
            html.Append($"<td style=\"{cell}text-align:right;\">{E(N(w.PageViews))}</td><td style=\"{cell}text-align:right;\">{E(N(w.Signups))}</td></tr>");
        }
        html.Append("</table>");

        void List(string title, IReadOnlyList<(string Name, string Figures)> rows)
        {
            html.Append($"<h2 style=\"font-size:16px;margin:24px 0 8px;\">{E(title)}</h2>");
            if (rows.Count == 0)
            {
                html.Append("<p style=\"margin:0;color:#64748b;\">None yesterday.</p>");
                return;
            }
            html.Append("<table role=\"presentation\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:100%;border-collapse:collapse;background:#fff;border:1px solid #e5e7eb;\">");
            foreach (var (name, figures) in rows)
                html.Append($"<tr><td style=\"{cell}word-break:break-all;\">{E(name)}</td><td style=\"{cell}text-align:right;white-space:nowrap;\">{E(figures)}</td></tr>");
            html.Append("</table>");
        }
        List("Top pages", d.TopPages.Select(p => (Defang(p.Path), $"{Count(p.Views, "view", "views")}, {Visitors(p.Visitors)}")).ToList());
        List("Top sources", d.TopSources.Select(s => (Defang(s.Source), $"{Count(s.Sessions, "session", "sessions")}, {Visitors(s.Visitors)}")).ToList());
        if (d.BotHits > 0)
            html.Append($"<p style=\"margin:16px 0 0;color:#64748b;\">Robots: {E(Count(d.BotHits, "hit", "hits"))} counted and left out of the figures above.</p>");

        html.Append($"<p style=\"margin:24px 0;\"><a href=\"{E(link)}\" style=\"display:inline-block;background:#ea580c;color:#fff;text-decoration:none;padding:10px 16px;border-radius:8px;font-weight:600;\">Open the dashboard</a></p>");
        html.Append("<p style=\"margin:0;color:#64748b;font-size:12px;\">Website visitors only: what is done signed in to the app is left out, sign-ups excepted. Counted without cookies. A visitor is counted once a day and cannot be followed from one day to the next. ");
        html.Append("You receive this because your address is a platform admin of CadenceE (Platform:AdminEmails).</p>");
        html.Append("</div></body></html>");
        return html.ToString();
    }

    /// <summary>"+4 (+50%)", "-2 (-20%)", "+3 (new)", "no change".</summary>
    public static string Change(int now, int before)
    {
        var delta = now - before;
        if (delta == 0) return "no change";
        var sign = delta > 0 ? "+" : "-";
        var amount = $"{sign}{N(Math.Abs(delta))}";
        if (before == 0) return $"{amount} (new)";
        var percent = Math.Round(Math.Abs(delta) * 100.0 / before, MidpointRounding.AwayFromZero);
        return $"{amount} ({sign}{percent.ToString("0", CultureInfo.InvariantCulture)}%)";
    }

    /// <summary>One bar per day, ▁ to █, scaled to the week's highest; a quiet week is all ▁.</summary>
    public static string Sparkline(IReadOnlyList<int> values)
    {
        const string bars = "▁▂▃▄▅▆▇█";
        var max = values.Count == 0 ? 0 : values.Max();
        if (max <= 0) return new string(bars[0], values.Count);
        return string.Concat(values.Select(v => bars[(int)Math.Round(Math.Max(0, v) * (bars.Length - 1.0) / max)]));
    }

    private static string N(int n) => n.ToString("N0", CultureInfo.InvariantCulture);

    private static string Count(int n, string one, string many) => $"{N(n)} {(n == 1 ? one : many)}";

    private static string Visitors(int n) => Count(n, "visitor", "visitors");
}
