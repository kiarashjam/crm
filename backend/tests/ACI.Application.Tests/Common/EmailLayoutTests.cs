using ACI.Application.Common;

namespace ACI.Application.Tests.Common;

/// <summary>
/// The one email design every message the system sends wears: the same words in the HTML
/// part and the plain-text part, everything from a record escaped, and a button that still
/// works when the button does not (its address written out under it).
/// </summary>
public class EmailLayoutTests
{
    private static EmailDesign Sample(EmailLook look) =>
        new EmailDesign(look, "Pavillon 46")
        {
            Preheader = "Reset your password",
            Eyebrow = "Password reset",
            Title = "Choose a new password",
            FooterNote = "You received this because someone asked to reset the password of this account.",
        }
            .Paragraph("Hi Anna,")
            .Paragraph("Someone asked to reset the password for your account. If it was you, choose a new one below.")
            .Button("Choose a new password", "https://crm.example.ch/reset-password?token=abc")
            .Note("This link works once and expires in 30 minutes.")
            .Facts([("Amount due", "CHF 1,200.00"), ("Due on", "31 December 2026")]);

    [Theory]
    [InlineData(EmailLook.Product)]
    [InlineData(EmailLook.Club)]
    [InlineData(EmailLook.Personal)]
    public void EveryPartIsInBothTheHtmlAndTheText(EmailLook look)
    {
        var (html, text) = EmailLayout.Render(Sample(look), "Reset your password");

        foreach (var words in new[] { "Hi Anna,", "choose a new one below", "Choose a new password", "https://crm.example.ch/reset-password?token=abc" })
        {
            html.Should().Contain(words.Replace("'", "&#39;"));
            text.Should().Contain(words);
        }
        if (look != EmailLook.Personal)
        {
            html.Should().Contain("expires in 30 minutes").And.Contain("CHF 1,200.00").And.Contain("Reset your password</title>");
            text.Should().Contain("Amount due: CHF 1,200.00").And.Contain("expires in 30 minutes");
        }
    }

    [Fact]
    public void WhatARecordSaysIsEscaped_NeverMarkup()
    {
        var design = new EmailDesign(EmailLook.Club, "<b>Club</b>") { Title = "<script>alert(1)</script>" }
            .Paragraph("Tom & Jerry <img src=x onerror=alert(1)>")
            .Button("Go\" onclick=\"x", "https://example.ch/?a=1&b=\"2\"")
            .Facts([("<i>Label</i>", "<u>Value</u>")]);

        var html = EmailLayout.Html(design, "<subject>");

        html.Should().NotContain("<script>").And.NotContain("<img src=x").And.NotContain("<b>Club</b>").And.NotContain("<i>Label</i>")
            .And.NotContain("onclick=\"x").And.NotContain("<subject>");
        html.Should().Contain("Tom &amp; Jerry &lt;img").And.Contain("&lt;script&gt;");
    }

    [Fact]
    public void WebAddressesInAParagraphBecomeLinks_AndLineBreaksStay()
    {
        var html = EmailLayout.Html(new EmailDesign(EmailLook.Product).Paragraph("See https://cadencee.ai/help.\nThanks"), "s");

        html.Should().Contain("<a href=\"https://cadencee.ai/help\"").And.Contain("help</a>.<br>Thanks");
    }

    [Fact]
    public void TheButtonsAddressIsWrittenOutUnderIt_UnlessAskedNotTo()
    {
        var shown = EmailLayout.Html(new EmailDesign(EmailLook.Product).Button("Open", "https://a.ch/x"), "s");
        var hidden = EmailLayout.Html(new EmailDesign(EmailLook.Product).Button("Open", "https://a.ch/x", showAddress: false), "s");

        shown.Should().Contain("copy this address into your browser");
        hidden.Should().NotContain("copy this address into your browser");
    }

    [Fact]
    public void TheClubsEmailCarriesTheClubsName_AndCadenceEsItsMark()
    {
        EmailLayout.Html(Sample(EmailLook.Club), "s").Should().Contain("Pavillon 46").And.NotContain("cadencee.ai");
        EmailLayout.Html(Sample(EmailLook.Product), "s").Should().Contain("cadencee.ai").And.Contain("&#10022;");
    }

    [Fact]
    public void TheHiddenPreviewLineIsThere_AndNotInTheText()
    {
        var (html, text) = EmailLayout.Render(Sample(EmailLook.Product), "s");
        html.Should().Contain("display:none").And.Contain("Reset your password");
        text.Should().NotContain("display:none");
    }

    private static EmailDesign WeekTable(EmailLook look = EmailLook.Product, string language = "en") =>
        new EmailDesign(look, "Pavillon 46") { Language = language }
            .Table(language == "fr" ? ["Semaine", "Semaine précédente"] : ["Week", "Week before"],
            [
                new EmailTableRow("New leads", ["12", "8"]),
                new EmailTableRow("pavillon46.ch", ["7", "5"], Indented: true),
                new EmailTableRow("Won in CHF", ["CHF 45,000.00", "CHF 12,000.00"]),
            ]);

    [Theory]
    [InlineData(EmailLook.Product)]
    [InlineData(EmailLook.Club)]
    public void ATableHasAHeaderRowAndEachFigureInBothParts(EmailLook look)
    {
        var (html, text) = EmailLayout.Render(WeekTable(look), "s");

        html.Should().Contain("<th scope=\"col\"").And.Contain(">Week<").And.Contain(">Week before<")
            .And.Contain(">New leads<").And.Contain(">CHF 45,000.00<").And.Contain("text-align:right;")
            .And.Contain("class=\"em-facts em-table\"");
        // The first column is the one that matters; the others are there to compare with.
        html.Should().MatchRegex("font-weight:600;color:#[0-9a-f]{6};\">12<");
        text.Should().Contain("- New leads: Week 12, Week before 8\n")
            .And.Contain("  - pavillon46.ch: Week 7, Week before 5\n")
            .And.Contain("- Won in CHF: Week CHF 45,000.00, Week before CHF 12,000.00\n");
    }

    [Fact]
    public void ATableIndentsAPartUnderItsRow_AndKeepsToAPhoneScreen()
    {
        var html = EmailLayout.Html(WeekTable(), "s");

        html.Should().Contain("em-indent").And.Contain("padding-left:34px;");
        // Dark mode recolours its lines; a phone tightens its edges, lets a figure break only at a space
        // and a label anywhere it must.
        html.Should().Contain(".em-table th{border-color").And.Contain(".em-table td,.em-table th{white-space:normal !important;word-break:normal !important;")
            .And.Contain("text-align:left;word-break:break-word;\">New leads<");
    }

    [Fact]
    public void ATableWithOneColumnReadsAsLabelAndFigure_AndBlankFiguresAreLeftOutOfTheText()
    {
        var design = new EmailDesign(EmailLook.Product)
            .Table(["Now"], [new EmailTableRow("Outreach", ["20"]), new EmailTableRow("Meeting", ["5"])])
            .Table(["Visitors", "Sign-ups"], [new EmailTableRow("Tue 24 Nov", ["3"])]);

        var text = EmailLayout.Text(design);

        text.Should().Contain("- Outreach: 20\n").And.Contain("- Meeting: 5\n").And.Contain("- Tue 24 Nov: Visitors 3\n");
    }

    [Fact]
    public void ATableInFrenchPutsANoBreakSpaceBeforeTheColon()
    {
        EmailLayout.Text(WeekTable(language: "fr")).Should().Contain("- New leads : Semaine 12, Semaine précédente 8");
    }

    [Fact]
    public void ATableEscapesWhatItHolds_AndMakesNoLinkOfIt()
    {
        var design = new EmailDesign(EmailLook.Product)
            .Table(["<b>Views</b>"], [new EmailTableRow("http://evil.example/<script>", ["<i>4</i>"])], labelColumn: "Page & source");

        var html = EmailLayout.Html(design, "s");

        html.Should().NotContain("<script>").And.NotContain("<b>Views</b>").And.NotContain("<i>4</i>")
            .And.NotContain("href=\"http://evil.example");
        html.Should().Contain("http://evil.example/&lt;script&gt;").And.Contain("&lt;b&gt;Views&lt;/b&gt;").And.Contain("Page &amp; source");
    }

    [Fact]
    public void ATableWithoutRowsOrColumnsIsLeftOut()
    {
        var design = new EmailDesign(EmailLook.Product)
            .Table(["Week"], [])
            .Table([], [new EmailTableRow("New leads", ["12"])])
            .Table(["Week"], [new EmailTableRow("  ", ["12"])]);

        EmailLayout.Html(design, "s").Should().NotContain("em-table\"");
        EmailLayout.Text(design).Trim().Should().BeEmpty();
    }

    [Fact]
    public void InFrench_TheTextPartSetsANoBreakSpaceBeforeEachColon()
    {
        var text = EmailLayout.Text(new EmailDesign(EmailLook.Club, "Pavillon 46") { Language = "fr" }
            .Facts([("Montant dû", "CHF 480.00"), ("Échéance\u00a0", "30 nov. 2026")])
            .Stats([("Membres", "212")])
            .Button("Payer en ligne", "https://crm.example.ch/pay/abc")
            .Links([("Ouvrir le portail", "https://crm.example.ch/portal")]));

        text.Should().Contain("Montant dû\u00a0: CHF 480.00").And.Contain("Échéance\u00a0: 30 nov. 2026").And.Contain("Membres\u00a0: 212")
            .And.Contain("Payer en ligne\u00a0:\nhttps://crm.example.ch/pay/abc").And.Contain("Ouvrir le portail\u00a0: https://crm.example.ch/portal");
        text.Should().NotContain("\u00a0\u00a0").And.NotContain("dû:");
        EmailLayout.Text(new EmailDesign(EmailLook.Club).Facts([("Amount due", "CHF 480.00")])).Should().Contain("Amount due: CHF 480.00");
    }

    [Fact]
    public void InTheDark_TheEyebrowLinksAndColouredNotesStayReadable()
    {
        foreach (var look in new[] { EmailLook.Product, EmailLook.Club })
        {
            var html = EmailLayout.Html(new EmailDesign(look, "Pavillon 46") { Eyebrow = "Statement" }
                .Paragraph("See https://cadencee.ai/help")
                .Button("Open", "https://a.ch/x")
                .Note("Signed.", EmailTone.Success)
                .Note("Overdue.", EmailTone.Warning), "s");
            var dark = html[html.IndexOf("prefers-color-scheme:dark", StringComparison.Ordinal)..html.IndexOf("@media screen", StringComparison.Ordinal)];

            dark.Should().Contain(".em-eyebrow,.em-link{color:").And.Contain(".em-ok{").And.Contain(".em-warn{");
            html.Should().Contain("class=\"em-link\"").And.Contain("class=\"em-note em-ok\"").And.Contain("class=\"em-note em-warn\"");
        }
    }

    [Fact]
    public void AWordTooLongForAPhone_WrapsInsteadOfWideningTheEmail()
    {
        var hash = new string('a', 64);
        var html = EmailLayout.Html(new EmailDesign(EmailLook.Club, "Pavillon 46").Preformatted("SHA-256: " + hash).Note(hash), "s");

        html.Split(hash).Length.Should().Be(3);
        html.Split("overflow-wrap:anywhere").Length.Should().Be(3, "the record's box and the note each wrap a long word");
    }

    /// <summary>
    /// Writes each look to EMAIL_PREVIEW_DIR, when it is set, to be opened in a browser or
    /// screenshotted: email design is checked by looking at it.
    /// </summary>
    [Fact]
    public void PreviewsAreWrittenWhenAskedFor()
    {
        var dir = Environment.GetEnvironmentVariable("EMAIL_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(dir)) return;
        Directory.CreateDirectory(dir);
        foreach (var look in new[] { EmailLook.Product, EmailLook.Club, EmailLook.Personal })
            File.WriteAllText(Path.Combine(dir, $"sample-{look.ToString().ToLowerInvariant()}.html"), EmailLayout.Html(Sample(look), "Reset your password"));
        var digest = new EmailDesign(EmailLook.Product) { Eyebrow = "Weekly recap · Pavillon 46", Title = "Your week, 16 – 22 November" }
            .Paragraph("Hi Miguel, here is how the week went.")
            .Stats([("New leads", "18"), ("Meetings booked", "7"), ("New members", "3")])
            .Heading("Leads")
            .Table(["Week", "Week before"],
            [
                new EmailTableRow("New leads", ["18", "12"]),
                new EmailTableRow("pavillon46.ch", ["11", "7"], Indented: true),
                new EmailTableRow("Through a referral", ["4", "1"]),
            ])
            .Heading("Pipeline now")
            .Facts([("Outreach", "42"), ("Meeting", "11"), ("Contract", "6"), ("Signature", "3"), ("Deposit", "2")])
            .Links([("Open the reports", "https://crm.example.ch/reports"), ("Notification settings", "https://crm.example.ch/settings")]);
        File.WriteAllText(Path.Combine(dir, "sample-digest.html"), EmailLayout.Html(digest, "Your week"));
    }
}
