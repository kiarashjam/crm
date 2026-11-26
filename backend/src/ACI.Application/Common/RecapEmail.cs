using System.Globalization;
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
/// A recap's subject, plain-text part and HTML part, from its figures, in the reader's language,
/// in CadenceE's design (<see cref="EmailLayout"/>).
/// </summary>
/// <remarks>
/// It leads with the period's key figures, large, then a part per section, each a table of its
/// figures beside the period before's; the pipeline, members at risk and renewals coming up are
/// as they stand now, in a column of their own that says so. Both parts are drawn from the one
/// design, so they never say different things, and record text (a lead source, a referrer's
/// name, the workspace's name) goes through it, escaped.
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
        string[] compared = [T($"col.{frequency}"), T($"col.{frequency}Before")];
        string[] now = [T("col.now")];

        var design = new EmailDesign(EmailLook.Product)
            {
                Language = language,
                Eyebrow = $"{T($"title.{frequency}")} · {OneLine(context.WorkspaceName)}",
                Title = periodText,
                Preheader = intro,
                FooterNote = T("why", ("workspace", context.WorkspaceName)),
            }
            .Paragraph(greeting)
            .Paragraph(intro)
            .Stats(KeyFigures(figures, language, numbers));

        foreach (var section in sections)
        {
            design.Heading(section.Title);
            if (section.Note != null && section.NowOnly) design.Paragraph(section.Note, muted: true);
            // A figure as it stands now has no period before to stand beside: such rows go in a
            // table of their own whose one column says "Now".
            foreach (var run in Runs(section.Rows))
            {
                var snapshot = run[0].Before == null;
                design.Table(snapshot ? now : compared,
                    run.Select(r => new EmailTableRow(OneLine(r.Label), snapshot ? [r.Now] : [r.Now, r.Before!], r.Indented)));
            }
            if (section.Note != null && !section.NowOnly) design.Paragraph(section.Note, muted: true);
        }

        if (money) design.Paragraph(T("amounts"), muted: true);
        var links = Links(context, language);
        if (SettingsUrl(context) is { } settings) links.Add((T("link.settings"), settings));
        design.Links(links);

        var (html, text) = EmailLayout.Render(design, subject);
        return (subject, text, html);
    }

    /// <summary>The section's rows in runs: those beside the period before, and those as they stand now.</summary>
    private static IEnumerable<List<Row>> Runs(IReadOnlyList<Row> rows)
    {
        var run = new List<Row>();
        foreach (var row in rows)
        {
            if (run.Count > 0 && (run[0].Before == null) != (row.Before == null))
            {
                yield return run;
                run = new List<Row>();
            }
            run.Add(row);
        }
        if (run.Count > 0) yield return run;
    }

    /// <summary>
    /// The three figures the recap leads with: the sales funnel's first and last steps with the
    /// meetings between, or with Sales off the club's visits, events and new members.
    /// </summary>
    private static IEnumerable<(string Label, string Value)> KeyFigures(RecapFigures f, string language, CultureInfo numbers)
    {
        string T(string key) => RecapTexts.T(language, key);
        string N(int n) => n.ToString("N0", numbers);
        if (f.Sales is { } s)
            return [(T("leads.new"), N(s.NewLeads.Now)), (T("meetings.held"), N(s.MeetingsHeld.Now)), (T("conversions.won"), N(s.Won.Now))];
        if (f.Club is not { } club) return [];
        var figures = new List<(string, string)> { (T("club.visits"), N(club.Visits.Now)), (T("club.events"), N(club.Events.Now)) };
        figures.Add(f.NewMembers is { } joined ? (T("club.newMembers"), N(joined.Now)) : (T("club.atRisk"), N(club.AtRiskNow)));
        return figures;
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

        // With Sales off the club's part also holds the new members, who alone can make a recap
        // go: it is shown for them too, or the recap would arrive with nothing in it.
        var membersHere = f.Sales == null && f.NewMembers is { } n && (n.Now > 0 || n.Before > 0);
        if (f.Club is { } club && (club.HasActivity || membersHere))
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

    /// <summary>Record text on one line: a source or a name typed with a line break stays in its row.</summary>
    private static string OneLine(string? value) =>
        string.Join(' ', (value ?? "").Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
}
