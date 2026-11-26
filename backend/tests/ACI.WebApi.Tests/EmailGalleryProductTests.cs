using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Infrastructure.Services;
using ACI.Infrastructure.Services.Assistant;
using ACI.Infrastructure.Services.Monitoring;
using ACI.Infrastructure.Services.SiteAnalytics;

namespace ACI.WebApi.Tests;

/// <summary>
/// Every email of CadenceE's own and the contract emails, with realistic sample data, written to
/// EMAIL_PREVIEW_DIR when it is set (and nothing otherwise), one file per email named
/// &lt;look&gt;-&lt;email&gt;.html (and its text part as .txt): email design is checked by looking
/// at it, in a browser or a screenshot, on a desktop, a phone and in dark mode.
/// </summary>
public class EmailGalleryProductTests
{
    private const string App = "https://app.cadencee.ai";

    /// <summary>The emails, by the file each is written to.</summary>
    public static IEnumerable<(string File, EmailContent Email)> Gallery()
    {
        // ── Account and security ─────────────────────────────────────────────
        yield return ("product-password-reset", AccountEmailContent.PasswordReset(
            "anna.muller@pavillon46.ch", "Anna Müller", $"{App}/reset-password?token=Qm9uam91ciBBbm5hIQ"));
        yield return ("product-email-verification", AccountEmailContent.EmailVerification(
            "luc.favre@pavillon46.ch", "Luc Favre", $"{App}/verify-email?token=VmVyaWZ5IEx1Yw"));
        yield return ("product-workspace-invitation", AccountEmailContent.WorkspaceInvitation(
            "claire.rochat@bluewin.ch", "Pavillon 46", "Miguel van Ackere", $"{App}/invites/accept?token=SW52aXRlIENsYWlyZQ", "read_only"));
        yield return ("product-task-reminder", AccountEmailContent.TaskReminder(
            "Anna Müller", "Call Ada Rossi about her Founding membership renewal",
            new DateTime(2026, 11, 27, 13, 30, 0, DateTimeKind.Utc), $"{App}/tasks/3f1c2a9e"));

        // ── Contracts: the counterparty's in the club's look, the team's in CadenceE's ──
        const string title = "Founding Membership Agreement 2027";
        yield return ("club-contract-for-signature", ContractEmailContent.ForSignature(
            "CadenceE", "Jean-Marc Dubois", "Pavillon 46", title, $"{App}/sign/c2lnbiBKZWFuLU1hcmM", hasAttachment: true));
        yield return ("club-contract-reminder", ContractEmailContent.Reminder(
            "CadenceE", "Jean-Marc Dubois", "Pavillon 46", title,
            new DateTime(2026, 11, 23, 9, 12, 0, DateTimeKind.Utc), new DateTime(2026, 12, 23, 9, 12, 0, DateTimeKind.Utc)));
        yield return ("product-contract-signed", ContractEmailContent.SignedNotification(
            "CadenceE", "Miguel van Ackere", "Jean-Marc Dubois", title, $"{App}/leads/8b0f4e21"));
        yield return ("club-contract-executed", ContractEmailContent.Executed(
            "CadenceE", "Jean-Marc Dubois", "Pavillon 46", title, ContractBody, SignatureRecord, hasAttachment: true));

        // ── Staff digests ────────────────────────────────────────────────────
        yield return ("product-weekly-report", WeeklyDigestService.Compose(
            "Miguel van Ackere", new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 22),
            [DigestSection("Pavillon 46")], $"{App}/reports"));
        yield return ("product-weekly-report-two-workspaces", WeeklyDigestService.Compose(
            "Miguel van Ackere", new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 22),
            [DigestSection("Pavillon 46"), DigestSection("Cercle du Lac", quiet: true)], $"{App}/reports"));
        yield return ("product-morning-brief", MorningBriefService.ComposeEmail(MorningBrief(),
            "Start with Ada Rossi: her renewal call is overdue and her membership renews on Monday. Then look at Fondation Léman, quiet for 12 days.",
            "Your morning brief for Thu 26 Nov", $"{App}/brief"));
        yield return ("product-recap-weekly", Recap(RecapFrequencies.Weekly, new DateOnly(2026, 11, 16), "en"));
        yield return ("product-recap-daily-fr", Recap(RecapFrequencies.Daily, new DateOnly(2026, 11, 25), "fr"));
        yield return ("product-recap-monthly-de", Recap(RecapFrequencies.Monthly, new DateOnly(2026, 10, 1), "de"));
        yield return ("product-recap-club-only", Recap(RecapFrequencies.Weekly, new DateOnly(2026, 11, 16), "en", salesOn: false));
        yield return ("product-health-digest", HealthDigestService.Compose(
            "Anna Müller", "Pavillon 46", new HealthDigestService.DigestCounts(FailedRuns: 1, Bounces: 3, SpamReports: 0, NewSuppressions: 2, Handoffs: 1, FailedEmails: 2),
            $"{App}/settings?tab=health"));
        var (analyticsSubject, analyticsText, analyticsHtml) = SiteAnalyticsDigestService.Compose(SiteAnalytics(), $"{App}/admin/analytics");
        yield return ("product-site-analytics", new EmailContent(analyticsSubject, analyticsText, analyticsHtml));
    }

    [Fact]
    public void EveryEmailIsWrittenForLookingAt_WhenAskedFor()
    {
        var all = Gallery().ToList();
        all.Select(g => g.File).Should().OnlyHaveUniqueItems();
        foreach (var (file, email) in all)
        {
            file.Should().MatchRegex("^(product|club)-[a-z0-9-]+$");
            email.Html.Should().StartWith("<!DOCTYPE html>", file);
            email.Html.Should().Contain(file.StartsWith("club-") ? "letter-spacing:4px;text-transform:uppercase" : "&#10022;", $"{file} wears its look");
            email.Text.Should().NotBeNullOrWhiteSpace(file);
        }

        var dir = Environment.GetEnvironmentVariable("EMAIL_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(dir)) return;
        Directory.CreateDirectory(dir);
        foreach (var (file, email) in all)
        {
            File.WriteAllText(Path.Combine(dir, file + ".html"), email.Html);
            File.WriteAllText(Path.Combine(dir, file + ".txt"), $"Subject: {email.Subject}\n\n{email.Text}");
        }
    }

    [Fact]
    public void TheWeeklyReport_EscapesAWorkspacesName_AndKeepsItsFiguresInBothParts()
    {
        var email = WeeklyDigestService.Compose("Miguel", new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 22),
            [DigestSection("<b>Pavillon</b> & 46")], $"{App}/reports");

        email.Html.Should().NotContain("<b>Pavillon</b>").And.Contain("&lt;b&gt;Pavillon&lt;/b&gt; &amp; 46")
            .And.Contain($"href=\"{App}/reports\"");
        foreach (var words in new[] { "3 deals, CHF 64,500", "Expected to close in November", "Activities logged" })
        {
            email.Text.Should().Contain(words);
            email.Html.Should().Contain(words);
        }
    }

    [Fact]
    public void TheMorningBrief_EscapesWhatTasksAndDealsSay_AndLinksToTheBrief()
    {
        var brief = MorningBrief() with
        {
            Sections = [new MorningBriefSection("tasks", "Your tasks due", 1, [new MorningBriefLine("Today: <script>alert(1)</script> & call", "/tasks/1")])],
        };

        var email = MorningBriefService.ComposeEmail(brief, null, "Your morning brief for Thu 26 Nov", $"{App}/brief");

        email.Html.Should().NotContain("<script>").And.Contain("&lt;script&gt;alert(1)&lt;/script&gt; &amp; call")
            .And.Contain($"href=\"{App}/brief\"");
        email.Text.Should().Contain("- Today: <script>alert(1)</script> & call").And.Contain($"Open your brief:\n{App}/brief");
    }

    [Fact]
    public void TheHealthDigest_ListsOnlyWhatHappened_InBothParts()
    {
        var email = HealthDigestService.Compose("Anna", "Pavillon <46>", new HealthDigestService.DigestCounts(0, 2, 0, 0, 0, 1), $"{App}/settings?tab=health");

        email.Subject.Should().Be("System health: 3 things to look at in Pavillon <46>");
        email.Text.Should().Contain("Emails that could not be sent: 1").And.Contain("Emails that bounced: 2")
            .And.NotContain("Spam reports").And.NotContain("Automation runs").And.Contain($"{App}/settings?tab=health");
        email.Html.Should().Contain("3 things to look at").And.Contain("Pavillon &lt;46&gt;").And.NotContain("Pavillon <46>")
            .And.Contain($"href=\"{App}/settings?tab=health\"");
    }

    // ── Sample data ──────────────────────────────────────────────────────────

    private static WeeklyDigestService.DigestSection DigestSection(string workspace, bool quiet = false)
    {
        static DealFiguresDto Deals(int count, params (string Currency, decimal Value)[] money) =>
            new(count, money.Select(m => new MoneyRowDto(m.Currency, count, m.Value, 0)).ToList());
        var period = new ReportPeriodDto(new DateTime(2026, 11, 15, 23, 0, 0, DateTimeKind.Utc), new DateTime(2026, 11, 22, 23, 0, 0, DateTimeKind.Utc),
            "organization", null, null, null, null, "Europe/Zurich", "day");
        var summary = new ReportSummaryDto(period, ["CHF", "EUR"], "CHF",
            Open: quiet ? Deals(4, ("CHF", 38000m)) : Deals(17, ("CHF", 412000m), ("EUR", 20000m)),
            Weighted: quiet ? [] : [new WeightedRowDto("CHF", 186500m, 15, 2), new WeightedRowDto("EUR", 10000m, 1, 0)],
            ClosingInPeriod: Deals(2, ("CHF", 24000m)),
            Created: quiet ? Deals(0) : Deals(3, ("CHF", 64500m)),
            Won: quiet ? Deals(0) : Deals(2, ("CHF", 45000m)),
            Lost: Deals(quiet ? 0 : 1),
            WinRate: 0.4, AverageSalesCycleDays: 38, ByStage: [], ByOwner: [],
            Leads: new LeadFiguresDto(Active: quiet ? 9 : 64, Created: quiet ? 1 : 18, CreatedConverted: 2, Converted: quiet ? 0 : 4, ConvertedToDeal: 3,
                Scored: 40, AverageScore: 61, ByStatus: [], BySource: [], CreatedSeries: []),
            Activities: new ActivityFiguresDto(quiet ? 6 : 57, [], []));
        var forecast = new ForecastDto("2026-11", 1, "organization", null,
            quiet ? [] : [new ForecastMonthRowDto("2026-11", "CHF", 3, 96000m, 52800m, 3, 0, 0, 2, 45000m)],
            quiet ? [] : [new MoneyRowDto("CHF", 2, 18000m, 0)],
            []);
        return new WeeklyDigestService.DigestSection(workspace, summary, forecast);
    }

    private static MorningBriefDto MorningBrief() => new("2026-11-26", "Europe/Zurich", "Pavillon 46",
        "2 tasks due, 1 deal gone quiet, 3 bookings today and 1 renewal coming up.",
        [
            new MorningBriefSection("tasks", "Your tasks due", 2,
            [
                new MorningBriefLine("Overdue since Tue 24 Nov: Call Ada Rossi about her renewal", "/tasks/1"),
                new MorningBriefLine("Today at 14:00: Send Fondation Léman the revised proposal", "/tasks/2"),
            ]),
            new MorningBriefSection("stale_deals", "Your deals gone quiet", 1,
                [new MorningBriefLine("Fondation Léman (Proposal): nothing done for 12 days", "/deals/3")]),
            new MorningBriefSection("arrivals", "Today's bookings", 3,
            [
                new MorningBriefLine("12:30 Salon Rhodanie: Claire Rochat, 4 people", "/members/4"),
                new MorningBriefLine("13:00 Terrasse: Luc Favre, 2 people", "/members/5"),
                new MorningBriefLine("19:30 Salle des Cartes: Jean-Marc Dubois, 6 people", "/members/6"),
            ]),
            new MorningBriefSection("renewals", "Renewals in the next 14 days", 1,
                [new MorningBriefLine("Ada Rossi (Founding) renews on Mon 30 Nov", "/members/7")]),
        ], 7);

    private static EmailContent Recap(string frequency, DateOnly first, string language, bool salesOn = true)
    {
        var zone = RegionalSettings.ZoneOf("Europe/Zurich");
        var period = RecapPeriods.For(frequency, first, zone);
        var sales = new RecapSalesFigures(
            NewLeads: new(18, 12),
            Sources: [new("pavillon46.ch", 11, 7), new("Instagram", 4, 3), new("", 1, 0)],
            OtherSources: new(2, 2),
            Referred: new(4, 1),
            Referrers: [new("Claire Rochat", 2, 0), new("Luc Favre", 1, 1)],
            Lost: new(2, 3),
            Invitations: new(36, 28),
            MeetingsBooked: new(9, 6),
            MeetingsHeld: new(7, 5),
            MeetingsCancelled: new(1, 2),
            ContractsSent: new(4, 2),
            ContractsSigned: new(3, 1),
            Deposits: new(2, 1),
            DepositAmounts: [new("CHF", 2400m, 1200m)],
            Converted: new(3, 1),
            Won: new(3, 1),
            WonValue: [new("CHF", 14400m, 4800m)],
            WonWithoutValue: 0,
            PipelineNow: [42, 11, 6, 3, 2]);
        var figures = new RecapFigures(period, RecapPeriods.Previous(period, zone), salesOn ? sales : null, new RecapCount(3, 1),
            new RecapClubFigures(new(214, 187), new(3, 2), new(96, 61), 4, 7));
        var (subject, text, html) = RecapEmail.Compose(figures,
            new RecapEmailContext(language, "Miguel van Ackere", "Pavillon 46", "fr-CH", App, salesOn));
        return new EmailContent(subject, text, html);
    }

    private static SiteAnalyticsDigestService.Digest SiteAnalytics()
    {
        var week = new[]
        {
            new SiteDailyDto("2026-11-18", 41, 47, 118, 0),
            new SiteDailyDto("2026-11-19", 38, 44, 97, 1),
            new SiteDailyDto("2026-11-20", 52, 61, 160, 0),
            new SiteDailyDto("2026-11-21", 29, 31, 66, 0),
            new SiteDailyDto("2026-11-22", 24, 26, 58, 0),
            new SiteDailyDto("2026-11-23", 47, 55, 139, 2),
            new SiteDailyDto("2026-11-24", 63, 74, 201, 3),
        };
        return new SiteAnalyticsDigestService.Digest(
            new DateOnly(2026, 11, 24), week[^1], week[^2], new SiteDailyDto("2026-11-17", 44, 50, 121, 1), week,
            [new SitePageDto("/", 88, 51), new SitePageDto("/pricing", 41, 29), new SitePageDto("/guides/crm-for-private-members-clubs", 27, 22),
             new SitePageDto("/de/preise", 19, 14), new SitePageDto("/signup", 12, 9)],
            [new SiteSourceDto("google", 31, 27), new SiteSourceDto("direct", 22, 19), new SiteSourceDto("linkedin", 9, 8),
             new SiteSourceDto("pavillon46.ch", 6, 5), new SiteSourceDto("spring newsletter", 4, 4)],
            BotHits: 312);
    }

    private const string ContractBody =
        "FOUNDING MEMBERSHIP AGREEMENT 2027\n\n" +
        "Between Pavillon 46 SA, Avenue de Rhodanie 46, 1007 Lausanne (\"the Club\")\n" +
        "and Jean-Marc Dubois, Chemin des Fleurettes 12, 1007 Lausanne (\"the Member\").\n\n" +
        "1. Membership. The Club admits the Member as a Founding Member from 1 January 2027 to 31 December 2027.\n\n" +
        "2. Fees. The annual fee is CHF 4,800.00, payable in advance by 15 January 2027. A deposit of CHF 1,200.00 " +
        "is due on signature and is credited against the annual fee.\n\n" +
        "3. Renewal. The membership renews for a further year unless either party gives notice in writing " +
        "by 30 September 2027.\n\n" +
        "4. Governing law. This agreement is governed by Swiss law; the courts of Lausanne have jurisdiction.";

    private const string SignatureRecord =
        "SIGNATURE RECORD\n\n" +
        "Counterparty : Jean-Marc Dubois\n" +
        "Signed       : 2026-11-24 18:42:07 UTC\n" +
        "From         : 85.218.14.32\n\n" +
        "Pavillon 46  : Miguel van Ackere\n" +
        "Signed       : 2026-11-25 08:15:51 UTC\n\n" +
        "Document hash: 4f1d9c0e8b7a6e2f3c5d1a9b8e7f6c5d4b3a2918f7e6d5c4b3a29180f7e6d5c4\n\n" +
        "This is a simple electronic signature: a typed name recorded with its\n" +
        "timestamp, origin and a hash of the exact text signed. It is not a\n" +
        "qualified electronic signature under ZertES or eIDAS.";
}
