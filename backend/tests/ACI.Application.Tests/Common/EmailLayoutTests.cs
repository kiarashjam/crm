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
            .Heading("Pipeline now")
            .Facts([("Outreach", "42"), ("Meeting", "11"), ("Contract", "6"), ("Signature", "3"), ("Deposit", "2")])
            .Links([("Open the reports", "https://crm.example.ch/reports"), ("Notification settings", "https://crm.example.ch/settings")]);
        File.WriteAllText(Path.Combine(dir, "sample-digest.html"), EmailLayout.Html(digest, "Your week"));
    }
}
