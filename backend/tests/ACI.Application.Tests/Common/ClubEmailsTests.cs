using System.Net;
using ACI.Application.Common;
using ACI.Application.Common.I18n;

namespace ACI.Application.Tests.Common;

/// <summary>
/// The emails a club sends its members, in its look: each says what it said before, in the
/// member's language, with its button's address and its key facts in both the HTML and the
/// plain-text part, and what comes from a record escaped.
/// </summary>
public class ClubEmailsTests
{
    private const string Club = "Pavillon 46";

    /// <summary>Sentences the email says, in both parts.</summary>
    private static void Says(ClubEmail email, params string[] words)
    {
        foreach (var w in words)
        {
            email.Text.Should().Contain(w);
            email.Html.Should().Contain(WebUtility.HtmlEncode(w));
        }
    }

    /// <summary>A label and its value: "Label: value" in the text, side by side in the HTML.</summary>
    private static void HasFact(ClubEmail email, string label, string value)
    {
        email.Text.Should().Contain($"{label}: {value}");
        email.Html.Should().Contain(WebUtility.HtmlEncode(label)).And.Contain(WebUtility.HtmlEncode(value));
    }

    /// <summary>The button: its words and address in the text, a link to the address in the HTML.</summary>
    private static void HasButton(ClubEmail email, string label, string url)
    {
        // French sets a no-break space before the colon.
        email.Text.Should().ContainAny($"{label}:\n{url}", $"{label}\u00a0:\n{url}");
        email.Html.Should().Contain($"href=\"{WebUtility.HtmlEncode(url)}\"").And.Contain(WebUtility.HtmlEncode(label));
    }

    private static StatementDetails Statement(string language = "en", decimal owed = 1480m) => new(
        Club, language, "Anna", "October 2026", "CHF 120.00", "CHF 1’480.00", "CHF 120.00", owed, "CHF 1’480.00",
        DueDate: "30 Nov 2026", QrBill: true, PayUrl: "https://crm.example.ch/pay/abc", PayUntil: "25 Jan 2027");

    [Fact]
    public void TheStatement_HasItsFiguresDueDatePayButtonAndQrBill_InBothParts_InTheClubsLook()
    {
        var email = ClubEmails.Statement(Statement());

        email.Subject.Should().Be("Your statement for October 2026");
        email.Design.Look.Should().Be(EmailLook.Club);
        email.Design.BrandName.Should().Be(Club);
        email.Design.Eyebrow.Should().Be("Account statement");
        email.Design.Title.Should().Be("October 2026");
        Says(email, "Dear Anna,", "Your statement for October 2026 is attached.", "The link works until 25 Jan 2027.",
            "You can also pay with the QR-bill", "If you have a question about it, reply to this email.", "Kind regards,");
        HasFact(email, "Amount due", "CHF 1’480.00");
        HasFact(email, "Due date", "30 Nov 2026");
        HasFact(email, "Brought forward", "CHF 120.00");
        HasFact(email, "Charges", "CHF 1’480.00");
        HasFact(email, "Payments and credits", "CHF 120.00");
        HasButton(email, "Pay online", "https://crm.example.ch/pay/abc");
    }

    [Fact]
    public void AStatementInCredit_AsksForNothing_NoButtonNoDueDateNoQrBill()
    {
        var email = ClubEmails.Statement(Statement(owed: -50m) with { Closing = "CHF 50.00" });

        HasFact(email, "In credit", "CHF 50.00");
        email.Text.Should().NotContain("Due date").And.NotContain("/pay/").And.NotContain("QR-bill");
    }

    [Fact]
    public void AStatementWithoutAPayLink_PointsToTheQrBillAlone()
    {
        var email = ClubEmails.Statement(Statement() with { PayUrl = null });

        email.Text.Should().NotContain("Pay online").And.Contain(MemberTexts.T("en", "pdf.qrNote"));
    }

    [Fact]
    public void TheStatementInFrench_IsInFrench_WithANoBreakSpaceBeforeEachColonOfItsFacts()
    {
        var email = ClubEmails.Statement(Statement("fr"));

        email.Subject.Should().Be("Votre relevé de compte pour October 2026");
        email.Text.Should().StartWith("October 2026\n\nBonjour Anna,");
        HasFact(email, "Montant dû ", "CHF 1’480.00");
        HasFact(email, "Échéance ", "30 Nov 2026");
        HasButton(email, "Payer en ligne", "https://crm.example.ch/pay/abc");
        email.Html.Should().Contain("lang=\"fr\"").And.Contain("Si le bouton ne fonctionne pas");
    }

    [Fact]
    public void TheReminder_ListsEachChargeTheTotalAndDaysOverdue_WithTheLapseWarningAndPayButton_Escaped()
    {
        var email = ClubEmails.Reminder(new ReminderDetails(Club, "de", "Max", 1, 3,
            [new ReminderCharge("Bar <b>tab</b>", "CHF 80.00", "15. Okt. 2026"), new ReminderCharge("Dues", "CHF 950.00", "31. Okt. 2026")],
            "CHF 1’030.00", 42, LapseAfterDays: 60, PayUrl: "https://crm.example.ch/pay/r1"));

        email.Subject.Should().Be("Zahlungserinnerung");
        email.Design.Title.Should().Be("Eine freundliche Erinnerung");
        Says(email, "Guten Tag Max,", "Wir erinnern Sie daran", "60 Tage nach Fälligkeit", "Freundliche Grüsse");
        HasFact(email, "Bar <b>tab</b> · fällig am 15. Okt. 2026", "CHF 80.00");
        HasFact(email, "Dues · fällig am 31. Okt. 2026", "CHF 950.00");
        HasFact(email, "Total überfällig", "CHF 1’030.00");
        HasFact(email, "Tage überfällig", "42");
        HasButton(email, "Online bezahlen", "https://crm.example.ch/pay/r1");
        email.Html.Should().NotContain("<b>tab</b>");
    }

    [Theory]
    [InlineData(1, 3, "Payment reminder", "A gentle reminder")]
    [InlineData(2, 3, "Second payment reminder", "Your payment is still outstanding")]
    [InlineData(3, 3, "Final payment reminder", "Please settle your account")]
    public void TheReminderIsFirmerAtEachStep(int step, int steps, string subject, string title)
    {
        var email = ClubEmails.Reminder(new ReminderDetails(Club, "en", "Anna", step, steps, [new ReminderCharge("Dues", "CHF 950.00", "1 Oct 2026")], "CHF 950.00", 10));

        email.Subject.Should().Be(subject);
        email.Design.Eyebrow.Should().Be(subject);
        email.Design.Title.Should().Be(title);
        email.Text.Should().NotContain("/pay/", "no pay link, no button").And.Contain("If you have paid in the last few days");
    }

    [Fact]
    public void ThePayLink_IsAButtonWithTheAmount_AndSaysUntilWhenItWorks()
    {
        var pay = ClubEmails.PayLink(new PayLinkDetails(Club, "en", "Anna", false, false, "CHF 480.00", "card or TWINT", "https://crm.example.ch/pay/p", "25 January 2027"));
        pay.Subject.Should().Be("Pay your account with Pavillon 46");
        Says(pay, "You can pay CHF 480.00 online, by card or TWINT, on Stripe's secure page.", "The link works until 25 January 2027.");
        HasButton(pay, "Pay CHF 480.00", "https://crm.example.ch/pay/p");

        var card = ClubEmails.PayLink(new PayLinkDetails(Club, "en", "Anna", true, true, "", "", "https://crm.example.ch/pay/s", "10 December 2026"));
        card.Subject.Should().Be("Save a card with Pavillon 46");
        Says(card, "membership dues", "the club never sees its number", "The link works until 10 December 2026.");
        HasButton(card, "Save a card", "https://crm.example.ch/pay/s");
    }

    [Fact]
    public void TheEventEmails_SetOutWhenAndWhere_SayWhereTheTicketIs_AndEscapeTheEventsName()
    {
        var freed = ClubEmails.SeatFreed(new EventNoticeDetails(Club, "en", "Anna", "Wine & <i>cheese</i>", "Friday 11 December 2026 at 19:30", "La Cave", "CHF 35.00"));
        freed.Subject.Should().Be("A place for you at Wine & <i>cheese</i>");
        Says(freed, "Dear Anna,", "a place has come free at Wine & <i>cheese</i>, and it is yours.", "The ticket, CHF 35.00, is on your house account.",
            "If you can no longer come");
        HasFact(freed, "When", "Friday 11 December 2026 at 19:30");
        HasFact(freed, "Where", "La Cave");
        freed.Html.Should().NotContain("<i>cheese</i>");

        var cancelled = ClubEmails.EventCancelled(new EventNoticeDetails(Club, "fr", null, "Soirée", "vendredi 11 décembre 2026 à 19:30", null, "CHF 35.00",
            TicketTakenOff: true, Member: false));
        Says(cancelled, "Soirée n’aura pas lieu", "Le billet (CHF 35.00) a été retiré", "car vous êtes inscrit(e) à Soirée.");
        HasFact(cancelled, "Quand ", "vendredi 11 décembre 2026 à 19:30");
        cancelled.Text.Should().NotContain("Bonjour", "a guest with no name is not greeted").And.NotContain("Où", "no place, no row");
    }

    [Fact]
    public void TheWelcome_NamesTheMembership()
    {
        var email = ClubEmails.Welcome(new WelcomeDetails(Club, "de", "Lena", "Gold"));
        email.Subject.Should().Be("Willkommen bei Pavillon 46");
        email.Design.Title.Should().Be("Willkommen bei Pavillon 46");
        Says(email, "Guten Tag Lena,", "Mitglied von Pavillon 46 (Gold)", "Bei Fragen antworten Sie einfach auf diese E-Mail.");
    }

    [Fact]
    public void ThePortalSignIn_IsASecurityEmail_TheLinkAsAButton_ItsExpiry_AndIfThisWasNotYou()
    {
        var one = ClubEmails.PortalLinks(new PortalLinkDetails(Club, "en", "Anna", false, [new PortalLink("Anna Keller", "https://crm.example.ch/p#token=a")],
            null, 15, "https://crm.example.ch/p"));
        one.Subject.Should().Be("Your sign-in link for Pavillon 46");
        Says(one, "Dear Anna,", "Each link works once, for 15 minutes.", "If you did not ask for it, you can ignore this email");
        HasButton(one, "Sign in", "https://crm.example.ch/p#token=a");

        var two = ClubEmails.PortalLinks(new PortalLinkDetails(Club, "en", "Lukas, Sophie", true,
            [new PortalLink("Lukas Brunner", "https://crm.example.ch/p#token=l"), new PortalLink("Sophie Brunner", "https://crm.example.ch/p#token=s")],
            "29 Nov 2026", 0, "https://crm.example.ch/p"));
        two.Subject.Should().Be("Pavillon 46: your member account");
        Says(two, "Dear Lukas, Sophie,", "Each link works once, until 29 Nov 2026.");
        HasButton(two, "Open the account of Lukas Brunner", "https://crm.example.ch/p#token=l");
        HasButton(two, "Open the account of Sophie Brunner", "https://crm.example.ch/p#token=s");
        two.Text.IndexOf("token=l", StringComparison.Ordinal).Should().BeLessThan(two.Text.IndexOf("token=s", StringComparison.Ordinal));
    }

    private static InvitationDetails Invitation(string letter, string? test = null) => new(
        Club, "en", false, "You are invited: Gala", letter, "Gala", "Friday 11 December 2026 at 19:30", "La Cave", "CHF 85.00",
        "https://crm.example.ch/invitation/tok", test);

    [Fact]
    public void TheInvitation_TurnsTheLettersDetailsIntoFacts_AndItsReplyLinkIntoTheButton_EachOnce()
    {
        var values = new InvitationTemplate.Values("Anna", "Anna Keller", "Gala", "Friday 11 December 2026 at 19:30", "La Cave", "CHF 85.00",
            "https://crm.example.ch/invitation/tok", Club);
        var email = ClubEmails.Invitation(Invitation(InvitationTemplate.Fill(MemberTexts.T("en", "invite.body"), values)));

        email.Design.Eyebrow.Should().Be("Invitation");
        email.Design.Title.Should().Be("Gala");
        Says(email, "Dear Anna,", "We would be delighted to welcome you to Gala.", "Will you come? Answer in one click", "We look forward to seeing you.");
        HasFact(email, "When", "Friday 11 December 2026 at 19:30");
        HasFact(email, "Where", "La Cave");
        HasFact(email, "Price for members", "CHF 85.00");
        HasButton(email, "Reply to the invitation", "https://crm.example.ch/invitation/tok");
        email.Text.Split("Price for members").Length.Should().Be(2, "the details are set out once");
        email.Text.Split("https://crm.example.ch/invitation/tok").Length.Should().Be(2, "the link is the button, not a line of its own as well");
    }

    [Fact]
    public void ALetterWithoutTheDetails_GetsThemAsFacts_AndOneWithTheLinkInASentence_KeepsItThere()
    {
        var email = ClubEmails.Invitation(Invitation("Dear Anna,\n\nJoin us: answer at https://crm.example.ch/invitation/tok please.\n\n<b>Pavillon</b>"));

        HasFact(email, "When", "Friday 11 December 2026 at 19:30");
        HasFact(email, "Price for members", "CHF 85.00");
        HasButton(email, "Reply to the invitation", "https://crm.example.ch/invitation/tok");
        email.Text.Should().Contain("Join us: answer at https://crm.example.ch/invitation/tok please.");
        Says(email, "<b>Pavillon</b>");
        email.Html.Should().NotContain("<b>Pavillon</b>");
    }

    [Fact]
    public void TheInvitationsReminderAndTestCopy_SayWhatTheyAre()
    {
        var reminder = ClubEmails.Invitation(Invitation("Dear Anna,\n\nhttps://crm.example.ch/invitation/tok") with { Reminder = true });
        reminder.Design.Eyebrow.Should().Be("Reminder");

        var test = ClubEmails.Invitation(Invitation("Dear Camille,\n\nhttps://crm.example.ch/invitation/tok", "This is a test of the invitation."));
        test.Text.IndexOf("This is a test", StringComparison.Ordinal).Should().BeLessThan(test.Text.IndexOf("Dear Camille", StringComparison.Ordinal));
        Says(test, "This is a test of the invitation.");
    }

    [Fact]
    public void ALetter_IsTheTextAsParagraphs_UnderTheSubjectWhenItReadsAsATitle()
    {
        var titled = ClubEmails.Letter(Club, "en", "The Winter Gala", "Dear Anna,\n\nJoin us <b>soon</b>.", title: true);
        titled.Title.Should().Be("The Winter Gala");
        var (html, text) = EmailLayout.Render(titled, "The Winter Gala");
        text.Should().Be("The Winter Gala\n\nDear Anna,\n\nJoin us <b>soon</b>.\n\n--\nYou are receiving this email as a member of Pavillon 46.\n");
        html.Should().Contain("Join us &lt;b&gt;soon&lt;/b&gt;.").And.NotContain("<b>soon</b>").And.Contain("Pavillon 46");

        ClubEmails.Letter(Club, "en", "Re: your visit", "Thanks", title: true).Title.Should().BeNull();
        ClubEmails.Letter(Club, "en", "The gala", "The gala\n\nIs on Friday.", title: true).Title.Should().BeNull("the text opens with it already");
        ClubEmails.Letter(Club, "de", "Ihre Garderobe", "Guten Tag", title: false).Title.Should().BeNull("a message to one member has none");
        ClubEmails.Letter(Club, "en", "Gala", "Dear Anna,\n\nThe gala is on Friday.", title: false).Preheader.Should().Be("The gala is on Friday.");
        ClubEmails.Letter(Club, "en", "Gala", "Hi", title: false).Eyebrow.Should().BeNull("light: no eyebrow");
    }

    [Theory]
    [InlineData("The Winter Gala, 19 December", true)]
    [InlineData("Your statement", true)]
    [InlineData("Re: dinner", false)]
    [InlineData("FWD: dinner", false)]
    [InlineData("[Test] Gala", false)]
    [InlineData("LAST CHANCE TO BOOK", false)]
    [InlineData("Book now!!", false)]
    [InlineData("See https://example.ch", false)]
    [InlineData("A subject that goes on and on well past what any heading should ever be asked to hold", false)]
    [InlineData("", false)]
    public void ASubjectReadsAsATitle_WhenItIsAShortCalmLine(string subject, bool reads)
    {
        ClubEmails.ReadsAsTitle(subject).Should().Be(reads);
    }

    [Fact]
    public void EveryClubEmailCarriesTheClubsName_Escaped_AndSaysWhyTheMemberGotIt()
    {
        var email = ClubEmails.Welcome(new WelcomeDetails("<Club & Co>", "en", "Anna", "Gold"));
        email.Html.Should().Contain("&lt;Club &amp; Co&gt;").And.NotContain("<Club & Co>");
        email.Text.Should().Contain("Kind regards,\n<Club & Co>").And.Contain("You are receiving this email as a member of <Club & Co>.");
    }
}
