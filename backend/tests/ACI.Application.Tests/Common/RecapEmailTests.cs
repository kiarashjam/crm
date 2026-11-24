using ACI.Application.Common;
using ACI.Application.Services;
using ACI.Domain.Entities;

namespace ACI.Application.Tests.Common;

/// <summary>
/// A recap's subject, plain text and HTML, from its figures, in the reader's language: each
/// figure beside the period before's, the snapshots said to be as they stand, amounts per
/// currency in the workspace's digits, and record text encoded in the HTML.
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
    public void InEnglish_EachFigureStandsBesideTheWeekBefore()
    {
        var (subject, text, html) = RecapEmail.Compose(Figures(), Context("en"));

        subject.Should().Be("Pavillon 46 · Weekly recap · 16 November 2026 – 22 November 2026");
        text.Should().StartWith("Hi Miguel van Ackere,")
            .And.Contain("What happened at Pavillon 46 from 16 November 2026 to 22 November 2026, each figure beside the week before.")
            .And.Contain("- New leads: 12 (previously 8)")
            .And.Contain("  - pavillon46.ch: 7 (previously 5)")
            .And.Contain("  - No source: 2 (previously 0)")
            .And.Contain("  - Other sources: 3 (previously 3)")
            .And.Contain("- Through a referral: 3 (previously 1)")
            .And.Contain("  - Anna <Muster>: 2 (previously 0)")
            .And.Contain("- Marked lost or not interested: 1 (previously 2)")
            .And.Contain("- Invitations to book a meeting sent: 40 (previously 0)")
            .And.Contain("- Meetings booked: 5 (previously 3)")
            .And.Contain("- Meetings held: 4 (previously 2)")
            .And.Contain("- Meetings cancelled: 1 (previously 0)")
            .And.Contain("- Contracts sent: 3 (previously 1)")
            .And.Contain("- Contracts signed: 2 (previously 1)")
            .And.Contain("- Deposits paid: 2 (previously 0)")
            .And.Contain($"- Deposits received in CHF: {Chf(10000m)} (previously {Chf(0m)})")
            .And.Contain("- Deposits received in EUR: ")
            .And.Contain("- Leads converted: 1 (previously 0)")
            .And.Contain("- Deals won: 2 (previously 1)")
            .And.Contain($"- Won in CHF: {Chf(45000m)} (previously {Chf(12000m)})")
            .And.Contain("Won deals whose value could not be read: 1. They are counted, not added in.")
            .And.Contain("- New club members: 1 (previously 0)")
            .And.Contain("Pipeline now").And.Contain("Open leads at each phase, as they stand now.")
            .And.Contain("- Outreach: 20").And.Contain("- Meeting: 5").And.Contain("- Contract: 3").And.Contain("- Signature: 2").And.Contain("- Deposit: 1")
            .And.Contain("- Visits: 120 (previously 100)")
            .And.Contain("- Events held: 2 (previously 1)")
            .And.Contain("  - Attendance at them: 48 (previously 30)")
            .And.Contain("- Members at risk now: 3")
            .And.Contain("- Renewals due in the next 30 days: 4")
            .And.Contain("Amounts are per currency and never converted.")
            .And.Contain("Open the dashboard: https://app.cadencee.ai/dashboard")
            .And.Contain("Open the reports: https://app.cadencee.ai/reports")
            .And.Contain("Your notification settings: https://app.cadencee.ai/settings?tab=notifications");

        html.Should().Contain("<html lang=\"en\">")
            .And.Contain("Anna &lt;Muster&gt;").And.NotContain("Anna <Muster>")
            .And.Contain(">Week<").And.Contain(">Week before<")
            .And.Contain("href=\"https://app.cadencee.ai/settings?tab=notifications\"")
            .And.Contain("href=\"https://app.cadencee.ai/dashboard\"");
    }

    [Fact]
    public void InFrenchAndGerman_TheWordsAndTheDatesAreTheReaders()
    {
        var (frSubject, frText, frHtml) = RecapEmail.Compose(Figures(), Context("fr"));
        frSubject.Should().Be("Pavillon 46 · Récapitulatif de la semaine · 16 novembre 2026 – 22 novembre 2026");
        frText.Should().StartWith("Bonjour Miguel van Ackere,")
            .And.Contain("du 16 novembre 2026 au 22 novembre 2026")
            .And.Contain("- Nouveaux prospects : 12 (auparavant 8)")
            .And.Contain("- Prise de contact : 20")
            .And.Contain("Ouvrir le tableau de bord : https://app.cadencee.ai/dashboard");
        frHtml.Should().Contain("<html lang=\"fr\">").And.Contain("Semaine précédente");

        var (deSubject, deText, _) = RecapEmail.Compose(Figures(), Context("de"));
        deSubject.Should().Be("Pavillon 46 · Wochenrückblick · 16. November 2026 – 22. November 2026");
        deText.Should().StartWith("Guten Tag Miguel van Ackere,")
            .And.Contain("- Neue Leads: 12 (zuvor 8)")
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
        dayText.Should().Contain("on Monday 23 November 2026, each figure beside the day before.");

        var (monthSubject, monthText, monthHtml) = RecapEmail.Compose(figures with { Period = month, Previous = RecapPeriods.Previous(month, Zurich) }, Context("fr"));
        monthSubject.Should().Be("Pavillon 46 · Récapitulatif du mois · octobre 2026");
        monthText.Should().Contain("en octobre 2026, chaque chiffre à côté de celui du mois précédent.");
        monthHtml.Should().Contain(">Mois précédent<");
    }

    [Fact]
    public void WithSalesOff_TheSalesPartsAreLeftOutAndNewMembersGoWithTheClub()
    {
        var (_, text, html) = RecapEmail.Compose(Figures(salesOn: false), Context("en", salesOn: false));

        text.Should().NotContain("New leads").And.NotContain("Pipeline now").And.NotContain("Deals won").And.NotContain("Open the dashboard");
        text.Should().Contain("The club").And.Contain("- New members: 1 (previously 0)").And.Contain("Open CadenceE: https://app.cadencee.ai/");
        html.Should().NotContain("/dashboard");
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
        html.Should().NotContain("href=");
    }
}
