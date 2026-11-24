using System.Globalization;
using System.Text;
using ACI.Application.Common.I18n;
using ACI.Application.Services;
using ACI.Domain.Entities;

namespace ACI.Application.Common;

/// <summary>Who a recap is for and where its links go.</summary>
/// <param name="Language">The reader's language: en, de or fr.</param>
/// <param name="WorkspaceLocale">The workspace's locale, whose region dates follow and whose digits numbers and amounts are written in.</param>
/// <param name="AppUrl">The app's public address (Email:FrontendBaseUrl); empty leaves the links out.</param>
/// <param name="SalesOn">Whether the workspace has the Sales module on: the dashboard and reports are its pages.</param>
public sealed record RecapEmailContext(string Language, string? RecipientName, string WorkspaceName, string? WorkspaceLocale, string AppUrl, bool SalesOn);

/// <summary>
/// A recap's subject, plain-text part and HTML part, from its figures, in the reader's language.
/// </summary>
/// <remarks>
/// Both parts are drawn from one list of sections, so they never say different things. Each
/// figure stands beside the period before's; the pipeline, members at risk and renewals coming up
/// are as they stand now, and say so. Record text (a lead source, a referrer's name, the
/// workspace's name) is HTML-encoded in the HTML part.
/// </remarks>
public static class RecapEmail
{
    /// <summary>The settings page where a person switches their recaps on and off.</summary>
    public const string SettingsPath = "/settings?tab=notifications";

    private sealed record Row(string Label, string Now, string? Before, bool Indented = false);

    private sealed record Section(string Title, string? Note, IReadOnlyList<Row> Rows, bool NowOnly = false);

    public static (string Subject, string Text, string Html) Compose(RecapFigures figures, RecapEmailContext context)
    {
        var language = AppLanguages.Normalise(context.Language) ?? AppLanguages.English;
        var dates = MemberTexts.Culture(language, context.WorkspaceLocale);
        var numbers = ContractDealFields.CultureOf(context.WorkspaceLocale);
        string T(string key, params (string, object?)[] args) => RecapTexts.T(language, key, args);

        var period = figures.Period;
        var frequency = period.Frequency;
        var periodText = PeriodText(language, period, dates);
        var subject = T($"subject.{frequency}", ("workspace", OneLine(context.WorkspaceName)), ("period", periodText));
        var intro = frequency switch
        {
            RecapFrequencies.Weekly => T("intro.weekly", ("workspace", context.WorkspaceName),
                ("first", Day(language, period.FirstDay, dates, "date.rangeDay")), ("last", Day(language, period.LastDay, dates, "date.rangeDay"))),
            RecapFrequencies.Monthly => T("intro.monthly", ("workspace", context.WorkspaceName), ("period", periodText)),
            _ => T("intro.daily", ("workspace", context.WorkspaceName), ("period", periodText)),
        };
        var greeting = string.IsNullOrWhiteSpace(context.RecipientName)
            ? T("greetingNoName")
            : T("greeting", ("name", context.RecipientName.Trim()));

        var sections = Sections(figures, language, numbers);
        var money = figures.Sales is { } s && (s.DepositAmounts.Count > 0 || s.WonValue.Count > 0);
        var links = Links(context, language);
        var current = T($"col.{frequency}");
        var before = T($"col.{frequency}Before");

        return (subject, Text(greeting, intro, sections, money, links, context, language),
            Html(subject, T($"title.{frequency}"), periodText, greeting, intro, sections, current, before, money, links, context, language));
    }

    /// <summary>The period as the reader says it: "Monday 23 November 2026", "16 – 22 November 2026", "October 2026".</summary>
    public static string PeriodText(string language, RecapPeriod period, CultureInfo dates) => period.Frequency switch
    {
        RecapFrequencies.Weekly => RecapTexts.T(language, "range",
            ("first", Day(language, period.FirstDay, dates, "date.rangeDay")), ("last", Day(language, period.LastDay, dates, "date.rangeDay"))),
        RecapFrequencies.Monthly => period.FirstDay.ToString(RecapTexts.T(language, "date.month"), dates),
        _ => Day(language, period.FirstDay, dates, "date.day"),
    };

    private static string Day(string language, DateOnly day, CultureInfo dates, string pattern) =>
        day.ToString(RecapTexts.T(language, pattern), dates);

    private static List<Section> Sections(RecapFigures f, string language, CultureInfo numbers)
    {
        string T(string key, params (string, object?)[] args) => RecapTexts.T(language, key, args);
        string N(int n) => n.ToString("N0", numbers);
        Row Count(string label, RecapCount c, bool indented = false) => new(label, N(c.Now), N(c.Before), indented);
        Row Money(string label, RecapAmount a) =>
            new(label, ContractDealFields.Money(a.Now, a.Currency, numbers), ContractDealFields.Money(a.Before, a.Currency, numbers));

        var sections = new List<Section>();
        if (f.Sales is { } s)
        {
            var leads = new List<Row> { Count(T("leads.new"), s.NewLeads) };
            leads.AddRange(s.Sources.Select(source =>
                Count(string.IsNullOrWhiteSpace(source.Name) ? T("leads.noSource") : source.Name, new RecapCount(source.Now, source.Before), indented: true)));
            if (s.OtherSources.Now > 0 || s.OtherSources.Before > 0) leads.Add(Count(T("leads.otherSources"), s.OtherSources, indented: true));
            leads.Add(Count(T("leads.referred"), s.Referred));
            leads.AddRange(s.Referrers.Select(r => Count(r.Name, new RecapCount(r.Now, r.Before), indented: true)));
            leads.Add(Count(T("leads.lost"), s.Lost));
            sections.Add(new Section(T("section.leads"), null, leads));

            sections.Add(new Section(T("section.meetings"), null,
            [
                Count(T("meetings.invited"), s.Invitations),
                Count(T("meetings.booked"), s.MeetingsBooked),
                Count(T("meetings.held"), s.MeetingsHeld),
                Count(T("meetings.cancelled"), s.MeetingsCancelled),
            ]));

            var contracts = new List<Row> { Count(T("contracts.sent"), s.ContractsSent), Count(T("contracts.signed"), s.ContractsSigned), Count(T("contracts.deposits"), s.Deposits) };
            contracts.AddRange(s.DepositAmounts.Select(a => Money(T("contracts.depositAmount", ("currency", a.Currency)), a)));
            sections.Add(new Section(T("section.contracts"), null, contracts));

            var conversions = new List<Row> { Count(T("conversions.converted"), s.Converted), Count(T("conversions.won"), s.Won) };
            conversions.AddRange(s.WonValue.Select(a => Money(T("conversions.wonValue", ("currency", a.Currency)), a)));
            if (f.NewMembers is { } joined) conversions.Add(Count(T("conversions.members"), joined));
            sections.Add(new Section(T("section.conversions"),
                s.WonWithoutValue > 0 ? T("conversions.withoutValue", ("count", N(s.WonWithoutValue))) : null, conversions));

            sections.Add(new Section(T("section.pipeline"), T("pipeline.intro"),
                s.PipelineNow.Select((count, i) => new Row(T($"pipeline.{i + 1}"), N(count), null)).ToList(), NowOnly: true));
        }

        if (f.Club is { HasActivity: true } club)
        {
            var rows = new List<Row>
            {
                Count(T("club.visits"), club.Visits),
                Count(T("club.events"), club.Events),
                Count(T("club.attendance"), club.Attendance, indented: true),
            };
            // With Sales off there is no conversions part to hold them.
            if (f.Sales == null && f.NewMembers is { } joined) rows.Add(Count(T("club.newMembers"), joined));
            rows.Add(new Row(T("club.atRisk"), N(club.AtRiskNow), null));
            rows.Add(new Row(T("club.renewals"), N(club.RenewalsDue), null));
            sections.Add(new Section(T("section.club"), null, rows));
        }
        return sections;
    }

    private static List<(string Label, string Url)> Links(RecapEmailContext context, string language)
    {
        var app = (context.AppUrl ?? "").Trim().TrimEnd('/');
        if (app.Length == 0) return [];
        return context.SalesOn
            ? [(RecapTexts.T(language, "link.dashboard"), $"{app}/dashboard"), (RecapTexts.T(language, "link.reports"), $"{app}/reports")]
            : [(RecapTexts.T(language, "link.app"), $"{app}/")];
    }

    private static string? SettingsUrl(RecapEmailContext context)
    {
        var app = (context.AppUrl ?? "").Trim().TrimEnd('/');
        return app.Length == 0 ? null : app + SettingsPath;
    }

    private static string Text(string greeting, string intro, List<Section> sections, bool money,
        List<(string Label, string Url)> links, RecapEmailContext context, string language)
    {
        string Line(string label, string value) => RecapTexts.T(language, "text.line", ("label", label), ("value", value));
        var b = new StringBuilder();
        b.AppendLine(greeting).AppendLine().AppendLine(intro);
        foreach (var section in sections)
        {
            b.AppendLine().AppendLine(section.Title).AppendLine(new string('-', Math.Min(section.Title.Length, 60)));
            if (section.Note != null && section.NowOnly) b.AppendLine(section.Note);
            foreach (var row in section.Rows)
            {
                var value = row.Before == null ? row.Now : RecapTexts.T(language, "text.beside", ("now", row.Now), ("before", row.Before));
                b.Append(row.Indented ? "  - " : "- ").AppendLine(Line(OneLine(row.Label), value));
            }
            if (section.Note != null && !section.NowOnly) b.AppendLine(section.Note);
        }
        if (money) b.AppendLine().AppendLine(RecapTexts.T(language, "amounts"));
        if (links.Count > 0)
        {
            b.AppendLine();
            foreach (var (label, url) in links) b.AppendLine(Line(label, url));
        }
        b.AppendLine().AppendLine(RecapTexts.T(language, "why", ("workspace", context.WorkspaceName)));
        if (SettingsUrl(context) is { } settings) b.AppendLine(Line(RecapTexts.T(language, "link.settings"), settings));
        return b.ToString().TrimEnd() + "\n";
    }

    private static string Html(string subject, string title, string periodText, string greeting, string intro, List<Section> sections,
        string current, string before, bool money, List<(string Label, string Url)> links, RecapEmailContext context, string language)
    {
        // Only what HTML reads as markup: the page is UTF-8, so the reader's letters stay as written.
        static string E(string? text) => (text ?? "")
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
        const string Cell = "padding:7px 0;border-top:1px solid #eef0f3;";
        var b = new StringBuilder();
        b.Append("<!DOCTYPE html><html lang=\"").Append(E(language)).Append("\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>").Append(E(subject)).Append("</title></head>")
            .Append("<body style=\"margin:0;padding:0;background:#f4f5f7;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Helvetica,Arial,sans-serif;color:#1f2937;\">")
            .Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:#f4f5f7;\"><tr><td align=\"center\" style=\"padding:24px 12px;\">")
            .Append("<table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" style=\"width:100%;max-width:600px;background:#ffffff;border:1px solid #e5e7eb;border-radius:12px;\">")
            .Append("<tr><td style=\"padding:24px 28px 8px;\">")
            .Append("<p style=\"margin:0;font-size:13px;color:#6b7280;\">").Append(E(context.WorkspaceName)).Append("</p>")
            .Append("<h1 style=\"margin:4px 0 0;font-size:21px;line-height:1.3;color:#111827;\">").Append(E(title)).Append("</h1>")
            .Append("<p style=\"margin:2px 0 0;font-size:14px;color:#374151;\">").Append(E(periodText)).Append("</p>")
            .Append("<p style=\"margin:16px 0 0;font-size:14px;line-height:1.5;\">").Append(E(greeting)).Append("</p>")
            .Append("<p style=\"margin:6px 0 0;font-size:14px;line-height:1.5;\">").Append(E(intro)).Append("</p>")
            .Append("</td></tr>");

        foreach (var section in sections)
        {
            b.Append("<tr><td style=\"padding:14px 28px 0;\">")
                .Append("<h2 style=\"margin:0 0 4px;font-size:15px;color:#111827;\">").Append(E(section.Title)).Append("</h2>");
            if (section.Note != null && section.NowOnly)
                b.Append("<p style=\"margin:0 0 4px;font-size:13px;color:#6b7280;\">").Append(E(section.Note)).Append("</p>");
            b.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border-collapse:collapse;font-size:14px;\">")
                .Append("<tr><th align=\"left\" style=\"padding:4px 0;font-size:12px;font-weight:600;color:#6b7280;\"></th>");
            if (section.NowOnly)
            {
                b.Append("<th align=\"right\" style=\"padding:4px 0;font-size:12px;font-weight:600;color:#6b7280;\">").Append(E(RecapTexts.T(language, "col.now"))).Append("</th>");
            }
            else
            {
                b.Append("<th align=\"right\" style=\"padding:4px 0 4px 12px;font-size:12px;font-weight:600;color:#6b7280;white-space:nowrap;\">").Append(E(current)).Append("</th>")
                    .Append("<th align=\"right\" style=\"padding:4px 0 4px 12px;font-size:12px;font-weight:600;color:#9ca3af;white-space:nowrap;\">").Append(E(before)).Append("</th>");
            }
            b.Append("</tr>");
            foreach (var row in section.Rows)
            {
                var label = row.Indented
                    ? $"<td style=\"{Cell}padding-left:14px;color:#4b5563;\">{E(row.Label)}</td>"
                    : $"<td style=\"{Cell}\">{E(row.Label)}</td>";
                b.Append("<tr>").Append(label)
                    .Append("<td align=\"right\" style=\"").Append(Cell).Append("padding-left:12px;font-weight:600;white-space:nowrap;\">").Append(E(row.Now)).Append("</td>");
                if (!section.NowOnly)
                    b.Append("<td align=\"right\" style=\"").Append(Cell).Append("padding-left:12px;color:#9ca3af;white-space:nowrap;\">").Append(E(row.Before ?? "")).Append("</td>");
                b.Append("</tr>");
            }
            b.Append("</table>");
            if (section.Note != null && !section.NowOnly)
                b.Append("<p style=\"margin:6px 0 0;font-size:12px;color:#6b7280;\">").Append(E(section.Note)).Append("</p>");
            b.Append("</td></tr>");
        }

        b.Append("<tr><td style=\"padding:20px 28px 24px;font-size:13px;line-height:1.5;color:#6b7280;\">");
        if (money) b.Append("<p style=\"margin:0 0 10px;\">").Append(E(RecapTexts.T(language, "amounts"))).Append("</p>");
        if (links.Count > 0)
        {
            b.Append("<p style=\"margin:0 0 14px;\">");
            for (var i = 0; i < links.Count; i++)
            {
                if (i > 0) b.Append(" &nbsp;·&nbsp; ");
                b.Append("<a href=\"").Append(E(links[i].Url)).Append("\" style=\"color:#c2410c;font-weight:600;text-decoration:none;\">").Append(E(links[i].Label)).Append("</a>");
            }
            b.Append("</p>");
        }
        b.Append("<p style=\"margin:0;\">").Append(E(RecapTexts.T(language, "why", ("workspace", context.WorkspaceName))));
        if (SettingsUrl(context) is { } settings)
            b.Append(" <a href=\"").Append(E(settings)).Append("\" style=\"color:#c2410c;\">").Append(E(RecapTexts.T(language, "link.settings"))).Append("</a>");
        b.Append("</p></td></tr></table></td></tr></table></body></html>");
        return b.ToString();
    }

    /// <summary>Record text on one line: a source or a name typed with a line break stays in its row.</summary>
    private static string OneLine(string? value) =>
        string.Join(' ', (value ?? "").Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
}
