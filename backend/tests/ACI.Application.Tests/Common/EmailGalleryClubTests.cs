using System.Globalization;
using ACI.Application.Common;
using ACI.Application.Common.I18n;

namespace ACI.Application.Tests.Common;

/// <summary>
/// Every email a club sends its members, written to EMAIL_PREVIEW_DIR when it is set (and
/// nothing otherwise), one file each, with realistic sample data: email design is checked by
/// looking at it. Marketing emails carry the unsubscribe footer the outbox adds.
/// </summary>
public class EmailGalleryClubTests
{
    private const string Club = "Pavillon 46";
    private const string App = "https://crm.pavillon46.ch";
    private static readonly CultureInfo Swiss = CultureInfo.GetCultureInfo("de-CH");

    private static string Chf(decimal amount) => "CHF " + amount.ToString("N2", Swiss);
    private static string Day(string language, int year, int month, int day) => MemberTexts.Date(language, "de-CH", new DateTime(year, month, day));

    private static string When(string language) =>
        new DateTime(2026, 12, 11, 19, 30, 0).ToString(MemberTexts.T(language, "event.when"),
            language == AppLanguages.English ? CultureInfo.GetCultureInfo("en-GB") : MemberTexts.Culture(language, "de-CH"));

    public static ClubEmail Statement(string language) => ClubEmails.Statement(new StatementDetails(
        Club, language, language switch { "de" => "Lukas", "fr" => "Camille", _ => "Anna" },
        MemberTexts.Period(language, "de-CH", new DateTime(2026, 10, 1), new DateTime(2026, 11, 1)),
        Chf(120m), Chf(1480m), Chf(120m), 1480m, Chf(1480m),
        DueDate: Day(language, 2026, 11, 30), QrBill: true,
        PayUrl: $"{App}/pay/CfDJ8Nq2xK4hT0aWv9mYbQ3rLs7Pz1uE", PayUntil: Day(language, 2027, 1, 25)));

    public static ClubEmail Invitation(string language)
    {
        var reply = $"{App}/invitation/kq3ZtW8yN2vRb5HcXe7mPa";
        var values = new InvitationTemplate.Values(
            language switch { "de" => "Lukas", "fr" => "Camille", _ => "Anna" }, "", "Soirée Chasselas & Raclette", When(language),
            "La Cave, Pavillon 46", Chf(85m), reply, Club);
        return ClubEmails.Invitation(new InvitationDetails(
            Club, language, false,
            InvitationTemplate.Fill(MemberTexts.T(language, "invite.subject"), values),
            InvitationTemplate.Fill(MemberTexts.T(language, "invite.body"), values),
            values.EventName, values.EventDate, values.EventPlace, values.MemberPrice, reply));
    }

    private static IEnumerable<(string Name, string Html)> Gallery()
    {
        foreach (var language in AppLanguages.All)
            yield return (language == "en" ? "statement" : $"statement-{language}", Statement(language).Html);

        yield return ("payment-reminder", ClubEmails.Reminder(new ReminderDetails(
            Club, "en", "Anna", 2, 3,
            [new ReminderCharge("Membership dues, Q4 2026", Chf(950m), Day("en", 2026, 10, 15)), new ReminderCharge("Wine dinner, 2 guests", Chf(180m), Day("en", 2026, 10, 31))],
            Chf(1130m), 42, LapseAfterDays: 60, PayUrl: $"{App}/pay/CfDJ8Kd9sLm2Qw", PayUntil: Day("en", 2027, 1, 25))).Html);

        yield return ("pay-link", ClubEmails.PayLink(new PayLinkDetails(
            Club, "en", "Anna", false, false, Chf(1480m), "card or TWINT", $"{App}/pay/CfDJ8Hy7wQe4Rt", Day("en", 2027, 1, 25))).Html);
        yield return ("pay-link-card", ClubEmails.PayLink(new PayLinkDetails(
            Club, "fr", "Camille", true, true, Chf(0), "carte", $"{App}/pay/CfDJ8Pl0oKi9Uj", Day("fr", 2026, 12, 10))).Html);

        yield return ("event-seat-freed", ClubEmails.SeatFreed(new EventNoticeDetails(
            Club, "en", "Anna", "Soirée Chasselas & Raclette", When("en"), "La Cave, Pavillon 46", Chf(85m))).Html);
        yield return ("event-cancelled", ClubEmails.EventCancelled(new EventNoticeDetails(
            Club, "de", "Lukas", "Weindegustation Lavaux", When("de"), "Salon Bleu", Chf(65m), TicketTakenOff: true)).Html);

        yield return ("welcome", ClubEmails.Welcome(new WelcomeDetails(Club, "en", "Anna", "Founding Member")).Html);

        yield return ("portal-sign-in", ClubEmails.PortalLinks(new PortalLinkDetails(
            Club, "en", "Anna", false, [new PortalLink("Anna Keller", $"{App}/portal/p46/sign-in?token=Zq8vN3mX5tLw2Rb7")], null, 15, $"{App}/portal/p46")).Html);
        yield return ("portal-invitation", ClubEmails.PortalLinks(new PortalLinkDetails(
            Club, "de", "Lukas, Sophie", true,
            [new PortalLink("Lukas Brunner", $"{App}/portal/p46/sign-in?token=Hx4kP9sQ2wE7"), new PortalLink("Sophie Brunner", $"{App}/portal/p46/sign-in?token=Mv3nB8cX1zL6")],
            Day("de", 2026, 11, 29), 0, $"{App}/portal/p46")).Html);

        foreach (var language in AppLanguages.All)
            yield return (language == "en" ? "invitation" : $"invitation-{language}", Marketing(Invitation(language).Html, language, "anna.keller@bluewin.ch"));

        var reminderValues = new InvitationTemplate.Values("Anna", "Anna Keller", "Soirée Chasselas & Raclette", When("en"), "La Cave, Pavillon 46", Chf(85m),
            $"{App}/invitation/Rt5yU8iO2pLk", Club);
        yield return ("invitation-reminder", Marketing(ClubEmails.Invitation(new InvitationDetails(
            Club, "en", true, InvitationTemplate.Fill(MemberTexts.T("en", "invite.reminderSubject"), reminderValues),
            InvitationTemplate.Fill(MemberTexts.T("en", "invite.reminderBody"), reminderValues),
            reminderValues.EventName, reminderValues.EventDate, reminderValues.EventPlace, reminderValues.MemberPrice, reminderValues.ReplyLink)).Html, "en", "anna.keller@bluewin.ch"));

        var test = ClubEmails.Invitation(new InvitationDetails(
            Club, "fr", false, "[Test] Invitation : Soirée Chasselas & Raclette",
            InvitationTemplate.Fill(MemberTexts.T("fr", "invite.body"), new InvitationTemplate.Values("Camille", "Camille Favre", "Soirée Chasselas & Raclette",
                When("fr"), "La Cave, Pavillon 46", Chf(85m), $"{App}/invitation/sample", Club)),
            "Soirée Chasselas & Raclette", When("fr"), "La Cave, Pavillon 46", Chf(85m), $"{App}/invitation/sample",
            "This is a test of the invitation, as Camille Favre will receive it (French). Its reply link is a sample: it answers for nobody. Members' copies end with the link that stops news and invitations."));
        yield return ("invitation-test", test.Html);

        yield return ("retention", Marketing(EmailLayout.Html(ClubEmails.Letter(Club, "en", "We would love to see you at Pavillon 46 again",
            "Dear Anna,\n\n" + MemberTexts.T("en", "retention.body", ("club", Club)) + "\n\n" + MemberTexts.T("en", "retention.closing") + "\n\nPavillon 46", title: true),
            "We would love to see you at Pavillon 46 again"), "en", "anna.keller@bluewin.ch"));

        const string gala = "The Winter Gala, 19 December";
        const string galaBody = "Dear Anna,\n\nOn Saturday 19 December the whole house opens for our Winter Gala: champagne on the terrace from 18:30, a five-course dinner by our chef Marc Duval, and the Lausanne Jazz Quartet until late.\n\nTables of eight can be reserved at the front desk or by replying to this email. Black tie.\n\nWe look forward to celebrating the season with you.\n\nThe Pavillon 46 team";
        yield return ("campaign", Marketing(EmailLayout.Html(ClubEmails.Letter(Club, "en", gala, galaBody, title: true), gala), "en", "anna.keller@bluewin.ch"));
        yield return ("campaign-test", EmailLayout.Html(ClubEmails.Letter(Club, "en", gala, galaBody, title: true, "Test copy",
            "This is a test of the campaign \"Winter Gala\", sent to you alone. Members have not received it. Their copies end with a link to unsubscribe from marketing."), "[Test] " + gala));

        yield return ("member-message", EmailLayout.Html(ClubEmails.Letter(Club, "de", "Ihre Garderobe",
            "Guten Tag Herr Brunner,\n\nIhr Mantel ist nach dem Abendessen vom Freitag bei uns an der Garderobe geblieben. Wir bewahren ihn gerne für Sie auf, bis Sie das nächste Mal vorbeikommen.\n\nFreundliche Grüsse\nClaire Morel, Empfang", title: false),
            "Ihre Garderobe"));
    }

    /// <summary>A marketing email as the outbox sends it: with the link that withdraws consent at its foot.</summary>
    private static string Marketing(string html, string language, string address) =>
        PlainTextEmail.WithFooter(html, null, Club, [new UnsubscribeLink(address, $"{App}/unsubscribe/m.c2FtcGxlLXRva2Vu")],
            PlainTextEmail.MarketingQuestion(Club, language), language);

    [Fact]
    public void EveryEmailOfTheGalleryWearsTheClubsLook_UnderItsName()
    {
        var all = Gallery().ToList();
        all.Select(e => e.Name).Should().OnlyHaveUniqueItems();
        all.Should().OnlyContain(e => e.Html.Contains("Pavillon 46") && e.Html.Contains("#2c5545"));
    }

    [Fact]
    public void PreviewsAreWrittenWhenAskedFor()
    {
        var dir = Environment.GetEnvironmentVariable("EMAIL_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(dir)) return;
        Directory.CreateDirectory(dir);
        foreach (var (name, html) in Gallery()) File.WriteAllText(Path.Combine(dir, $"club-{name}.html"), html);
    }
}
