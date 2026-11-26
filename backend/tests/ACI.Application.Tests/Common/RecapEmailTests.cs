using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;

namespace ACI.Application.Tests.Common;

/// <summary>
/// A recap's subject, plain text and HTML, from its figures, in the reader's language and
/// CadenceE's design: the key figures first, then each figure beside the period before's, the
/// snapshots in a "Now" column of their own, amounts per currency in the workspace's digits, and
/// record text escaped in the HTML.
/// </summary>
public class RecapEmailTests
{
    private static readonly TimeZoneInfo Zurich = RegionalSettings.ZoneOf("Europe/Zurich");
    private const string Locale = "fr-CH";

    private static RecapFigures Figures(bool salesOn = true, bool clubActive = true)
    {
        var week = RecapPeriods.For(RecapFrequencies.Weekly, new DateOnly(2026, 11, 16), Zurich);
        var sales = new RecapSalesFigures(
            NewLeads: new(12, 8),
            Sources: [new("pavillon46.ch", 7, 5), new("", 2, 0)],
            OtherSources: new(3, 3),
            Referred: new(3, 1),
            Referrers: [new("Anna <Muster>", 2, 0)],
            Lost: new(1, 2),
            Invitations: new(40, 0),
            MeetingsBooked: new(5, 3),
            MeetingsHeld: new(4, 2),
            MeetingsCancelled: new(1, 0),
            ContractsSent: new(3, 1),
            ContractsSigned: new(2, 1),
            Deposits: new(2, 0),
            DepositAmounts: [new("CHF", 10000m, 0m), new("EUR", 2500m, 0m)],
            Converted: new(1, 0),
            Won: new(2, 1),
            WonValue: [new("CHF", 45000m, 12000m)],
            WonWithoutValue: 1,
            PipelineNow: [20, 5, 3, 2, 1]);
        var club = clubActive ? new RecapClubFigures(new(120, 100), new(2, 1), new(48, 30), 3, 4) : new RecapClubFigures(RecapCount.Zero, RecapCount.Zero, RecapCount.Zero, 0, 0);
        return new RecapFigures(week, RecapPeriods.Previous(week, Zurich), salesOn ? sales : null, new RecapCount(1, 0), club);
    }

    private static RecapEmailContext Context(string language, bool salesOn = true) =>
        new(language, "Miguel van Ackere", "Pavillon 46", Locale, "https://app.cadencee.ai/", salesOn);

    private static string Chf(decimal amount) => ContractDealFields.Money(amount, "CHF", ContractDealFields.CultureOf(Locale));

    [Fact]
    public void InEnglish_ItLeadsWithTheKeyFigures_ThenEachFigureStandsBesideTheWeekBefore()
    {
        var (subject, text, html) = RecapEmail.Compose(Figures(), Context("en"));

        subject.Should().Be("Pavillon 46 · Weekly recap · 16 November 2026 – 22 November 2026");
        text.Should().StartWith("16 November 2026 – 22 November 2026\n\nHi Miguel van Ackere,")
            .And.Contain("What happened at Pavillon 46 from 16 November 2026 to 22 November 2026, each figure beside the week before.")
            .And.Contain("New leads: 12\nMeetings held: 4\nDeals won: 2\n")
            .And.Contain("Leads\n-----\n- New leads: Week 12, Week before 8\n")
            .And.Contain("  - pavillon46.ch: Week 7, Week before 5")
            .And.Contain("  - No source: Week 2, Week before 0")
            .And.Contain("  - Other sources: Week 3, Week before 3")
            .And.Contain("- Through a referral: Week 3, Week before 1")
            .And.Contain("  - Anna <Muster>: Week 2, Week before 0")
            .And.Contain("- Marked lost or not interested: Week 1, Week before 2")
            .And.Contain("- Invitations to book a meeting sent: Week 40, Week before 0")
            .And.Contain("- Meetings booked: Week 5, Week before 3")
            .And.Contain("- Meetings held: Week 4, Week before 2")
            .And.Contain("- Meetings cancelled: Week 1, Week before 0")
            .And.Contain("- Contracts sent: Week 3, Week before 1")
            .And.Contain("- Contracts signed: Week 2, Week before 1")
            .And.Contain("- Deposits paid: Week 2, Week before 0")
            .And.Contain($"- Deposits received in CHF: Week {Chf(10000m)}, Week before {Chf(0m)}")
            .And.Contain("- Deposits received in EUR: ")
            .And.Contain("- Leads converted: Week 1, Week before 0")
            .And.Contain("- Deals won: Week 2, Week before 1")
            .And.Contain($"- Won in CHF: Week {Chf(45000m)}, Week before {Chf(12000m)}")
            .And.Contain("Won deals whose value could not be read: 1. They are counted, not added in.")
            .And.Contain("- New club members: Week 1, Week before 0")
            .And.Contain("Pipeline now").And.Contain("Open leads at each phase, as they stand now.")
            .And.Contain("- Outreach: 20").And.Contain("- Meeting: 5").And.Contain("- Contract: 3").And.Contain("- Signature: 2").And.Contain("- Deposit: 1")
            .And.Contain("- Visits: Week 120, Week before 100")
            .And.Contain("- Events held: Week 2, Week before 1")
            .And.Contain("  - Attendance at them: Week 48, Week before 30")
            .And.Contain("- Members at risk now: 3")
            .And.Contain("- Renewals due in the next 30 days: 4")
            .And.Contain("Amounts are per currency and never converted.")
            .And.Contain("Open the dashboard: https://app.cadencee.ai/dashboard")
            .And.Contain("Open the reports: https://app.cadencee.ai/reports")
            .And.Contain("Your notification settings: https://app.cadencee.ai/settings?tab=notifications")
            .And.Contain("--\nYou receive this recap because it is switched on for you in Pavillon 46.");

        html.Should().Contain("<html lang=\"en\"")
            .And.Contain(">" + EmailLayout.E("Weekly recap · Pavillon 46") + "<").And.Contain("16 November 2026 – 22 November 2026</h1>")
            .And.Contain("class=\"em-stat\"")
            .And.Contain("Anna &lt;Muster&gt;").And.NotContain("Anna <Muster>")
            .And.Contain(">Week<").And.Contain(">Week before<").And.Contain(">Now<")
            .And.Contain("href=\"https://app.cadencee.ai/settings?tab=notifications\"")
            .And.Contain("href=\"https://app.cadencee.ai/dashboard\"")
            .And.Contain("&#10022;");
    }

    [Fact]
    public void FiguresAsTheyStandNow_SitInANowColumn_NeverBesideAPeriodBefore()
    {
        var (_, text, html) = RecapEmail.Compose(Figures(), Context("en"));

        // The club's part: its counts beside the week before, then its snapshots under "Now".
        text.Should().NotContain("Members at risk now: Week").And.NotContain("Outreach: Week");
        var club = html[html.IndexOf(">The club<", StringComparison.Ordinal)..];
        club.IndexOf(">Week before<", StringComparison.Ordinal).Should().BeLessThan(club.IndexOf(">Now<", StringComparison.Ordinal));
        club.IndexOf(">Now<", StringComparison.Ordinal).Should().BeLessThan(club.IndexOf(">Members at risk now<", StringComparison.Ordinal));
    }

    [Fact]
    public void InFrenchAndGerman_TheWordsAndTheDatesAreTheReaders()
    {
        var (frSubject, frText, frHtml) = RecapEmail.Compose(Figures(), Context("fr"));
        frSubject.Should().Be("Pavillon 46 · Récapitulatif de la semaine · 16 novembre 2026 – 22 novembre 2026");
        frText.Should().StartWith("16 novembre 2026 – 22 novembre 2026\n\nBonjour Miguel van Ackere,")
            .And.Contain("du 16 novembre 2026 au 22 novembre 2026")
            .And.Contain("- Nouveaux prospects : Semaine 12, Semaine précédente 8")
            .And.Contain("- Prise de contact : 20")
            .And.Contain("https://app.cadencee.ai/dashboard");
        frHtml.Should().Contain("<html lang=\"fr\"").And.Contain(EmailLayout.E("Semaine précédente"))
            .And.Contain(EmailLayout.E("Récapitulatif de la semaine · Pavillon 46"))
            // The button's hint under nothing: a recap has links, not a button; the page is French.
            .And.NotContain("copy this address");

        var (deSubject, deText, _) = RecapEmail.Compose(Figures(), Context("de"));
        deSubject.Should().Be("Pavillon 46 · Wochenrückblick · 16. November 2026 – 22. November 2026");
        deText.Should().Contain("Guten Tag Miguel van Ackere,")
            .And.Contain("- Neue Leads: Woche 12, Vorwoche 8")
            .And.Contain("- Gefährdete Mitglieder jetzt: 3");
    }

    [Fact]
    public void ADailyAndAMonthlyRecapNameTheirPeriod()
    {
        var day = RecapPeriods.For(RecapFrequencies.Daily, new DateOnly(2026, 11, 23), Zurich);
        var month = RecapPeriods.For(RecapFrequencies.Monthly, new DateOnly(2026, 10, 1), Zurich);
        var figures = Figures();

        var (daySubject, dayText, _) = RecapEmail.Compose(figures with { Period = day, Previous = RecapPeriods.Previous(day, Zurich) }, Context("en"));
        daySubject.Should().Be("Pavillon 46 · Daily recap · Monday 23 November 2026");
        dayText.Should().Contain("on Monday 23 November 2026, each figure beside the day before.").And.Contain("- New leads: Day 12, Day before 8");

        var (monthSubject, monthText, monthHtml) = RecapEmail.Compose(figures with { Period = month, Previous = RecapPeriods.Previous(month, Zurich) }, Context("fr"));
        monthSubject.Should().Be("Pavillon 46 · Récapitulatif du mois · octobre 2026");
        monthText.Should().Contain("en octobre 2026, chaque chiffre à côté de celui du mois précédent.");
        monthHtml.Should().Contain(">" + EmailLayout.E("Mois précédent") + "<");
    }

    [Fact]
    public void WithSalesOff_TheSalesPartsAreLeftOutAndNewMembersGoWithTheClub()
    {
        var (_, text, html) = RecapEmail.Compose(Figures(salesOn: false), Context("en", salesOn: false));

        text.Should().NotContain("New leads").And.NotContain("Pipeline now").And.NotContain("Deals won").And.NotContain("Open the dashboard");
        text.Should().Contain("The club").And.Contain("- New members: Week 1, Week before 0").And.Contain("Open CadenceE: https://app.cadencee.ai/");
        // It leads with the club's figures.
        text.Should().Contain("Visits: 120\nEvents held: 2\nNew members: 1\n");
        html.Should().NotContain("/dashboard");
    }

    [Fact]
    public void WithSalesOff_NewMembersAloneStillShowTheClubPart()
    {
        // A club before it opens: no visits or events yet, and a member admitted. That member is
        // what makes the recap go (RecapFigures.HasNews), so the recap must say it, not arrive empty.
        var figures = Figures(salesOn: false, clubActive: false);
        figures.HasNews.Should().BeTrue();

        var (_, text, _) = RecapEmail.Compose(figures, Context("en", salesOn: false));

        text.Should().Contain("The club").And.Contain("- New members: Week 1, Week before 0");
    }

    [Fact]
    public void AClubWithoutActivityHasNoPartInTheRecap()
    {
        var (_, text, _) = RecapEmail.Compose(Figures(clubActive: false), Context("en"));
        text.Should().NotContain("The club").And.NotContain("Visits");
    }

    [Fact]
    public void WithoutTheAppsAddressTheLinksAreLeftOut()
    {
        var (_, text, html) = RecapEmail.Compose(Figures(), Context("en") with { AppUrl = "" });
        text.Should().NotContain("http").And.Contain("You receive this recap because it is switched on for you in Pavillon 46.");
        // Only CadenceE's own address, in the design's footer, is left to link to.
        html.Should().NotContain("app.cadencee.ai").And.NotContain("/settings").And.NotContain("/reports");
    }

    [Fact]
    public void AWorkspaceNameWithMarkupIsEscaped()
    {
        var (_, _, html) = RecapEmail.Compose(Figures(), Context("en") with { WorkspaceName = "<b>Club</b> & Co" });

        html.Should().NotContain("<b>Club</b>").And.Contain("&lt;b&gt;Club&lt;/b&gt; &amp; Co");
    }
}
