using System.Text.RegularExpressions;
using ACI.Application.Common;

namespace ACI.Application.Tests.Common;

/// <summary>
/// The contract emails.
/// </summary>
/// <remarks>
/// Email content is usually the least tested thing a customer actually reads,
/// because it hides behind an SMTP connection. These are the properties that
/// matter: nothing a person typed reaches the recipient as markup, the link is
/// always recoverable when the button fails, both parts carry the same substance,
/// the executed copy really does contain the whole contract, and what goes to the
/// counterparty reads as the workspace's, not as software's.
/// </remarks>
public class ContractEmailContentTests
{
    private const string Sender = "Cadence";
    private const string Org = "Pavillon 46";
    private const string Url = "https://crm.example/sign/abc123";

    [Fact]
    public void ForSignature_SaysWhoItIsFromAndWhatToDo()
    {
        var e = ContractEmailContent.ForSignature(Sender, "Jean Dupont", Org, "Membership Agreement", Url);

        e.Subject.Should().Contain(Org).And.Contain("Membership Agreement");
        e.Text.Should().Contain("Hi Jean Dupont").And.Contain(Url).And.Contain("Contract: Membership Agreement");
        e.Html.Should().Contain("Please review and sign").And.Contain(Url).And.Contain(">Read and sign the contract<");
        // The reassurance is the difference between a contract request and a
        // demand: they need to know signing is the only committing act.
        e.Text.Should().Contain("Nothing is agreed until you sign");
        e.Html.Should().Contain("Nothing is agreed until you sign");
        // How long the link works is what ContractService gives it.
        e.Text.Should().Contain($"The link works for {ContractSigningToken.Lifetime.TotalDays:0} days.");
        e.Html.Should().Contain("The link works for 30 days.");
    }

    [Fact]
    public void ForSignature_SaysTheUnsignedCopyIsAttached_WhenItIs()
    {
        var with = ContractEmailContent.ForSignature(Sender, "Jean", Org, "Agreement", Url, hasAttachment: true);
        var without = ContractEmailContent.ForSignature(Sender, "Jean", Org, "Agreement", Url);

        foreach (var part in new[] { with.Text, with.Html })
            part.Should().Contain("A copy is attached to this email").And.Contain("marked unsigned until both parties have signed")
                .And.Contain("An unsigned copy (PDF)");
        without.Text.Should().NotContain("attached");
        without.Html.Should().NotContain("attached");
    }

    [Fact]
    public void WhatGoesToTheCounterparty_IsTheWorkspaces_AndWhatGoesToTheTeamIsCadenceEs()
    {
        foreach (var e in new[]
        {
            ContractEmailContent.ForSignature(Sender, "Jean Dupont", Org, "Membership Agreement", Url),
            ContractEmailContent.Executed(Sender, "Jean Dupont", Org, "Membership Agreement", "body", "signatures"),
            ContractEmailContent.Reminder(Sender, "Jean Dupont", Org, "Membership Agreement"),
        })
        {
            // The club's name in letter-spaced capitals, its deep green, no CadenceE mark.
            e.Html.Should().Contain("letter-spacing:4px;text-transform:uppercase").And.Contain(">Pavillon 46</p>")
                .And.Contain("#2c5545").And.NotContain("&#10022;");
            e.Text.Should().Contain("Kind regards,\nPavillon 46");
            e.Html.Should().Contain("Sent by Cadence on behalf of Pavillon 46.");
        }

        var signed = ContractEmailContent.SignedNotification(Sender, "Kia", "Jean Dupont", "Membership Agreement", Url);
        signed.Html.Should().Contain("&#10022;").And.Contain("cadencee.ai").And.Contain("#ea580c");
    }

    [Fact]
    public void SignedNotification_NamesWhoSignedAndAsksForOurs()
    {
        var e = ContractEmailContent.SignedNotification(Sender, "Kia", "Jean Dupont", "Membership Agreement", Url);

        e.Subject.Should().Contain("Jean Dupont").And.Contain("your signature is next");
        e.Text.Should().Contain("Jean Dupont has signed “Membership Agreement”.").And.Contain(Url);
        e.Html.Should().Contain("Jean Dupont has signed").And.Contain("Countersign");
        // The good news in the success colours.
        e.Html.Should().Contain("#ecfdf5");

        ContractEmailContent.SignedNotification(Sender, "Kia", " ", "Agreement", Url).Html.Should().Contain("They have signed");
    }

    [Fact]
    public void Executed_CarriesTheWholeContractAndBothSignatures()
    {
        const string body = "# Membership Agreement\n\n1. The Member agrees...\n2. Fees are CHF 2,400.";
        const string signatures = "Signed by Jean Dupont on 2026-08-26\nCountersigned by Kia on 2026-08-26";

        var e = ContractEmailContent.Executed(Sender, "Jean Dupont", Org, "Membership Agreement", body, signatures);

        // The text part gets the document verbatim — a text-only client must not
        // receive a "please enable HTML" stub in place of a contract.
        e.Text.Should().Contain(body).And.Contain(signatures);
        // The HTML gets it escaped but complete, the text as it was set out.
        e.Html.Should().Contain("Fees are CHF 2,400").And.Contain("Countersigned by Kia").And.Contain("white-space:pre-wrap");
        e.Subject.Should().Contain("Signed by both parties");
        e.Text.Should().Contain("keep this email as your copy");
    }

    [Fact]
    public void Executed_PutsTheSignatureRecordInItsOwnBox_UnderASuccessNote_AndSaysWhenThePdfIsAttached()
    {
        const string hash = "4f1d9c0e8b7a6e2f3c5d1a9b8e7f6c5d4b3a2918f7e6d5c4b3a29180f7e6d5c4";
        const string signatures = "SIGNATURE RECORD\n\nCounterparty : Jean Dupont\nSigned       : 2026-11-24 18:42:07 UTC\nDocument hash: " + hash;

        var e = ContractEmailContent.Executed(Sender, "Jean Dupont", Org, "Membership Agreement", "body", signatures, hasAttachment: true);

        // A note in the success colours says what the record is; the record follows, every line
        // set out as written (its columns line up), in a box whose long hash wraps on a phone.
        e.Html.Should().MatchRegex("#ecfdf5[^>]*>Signed by both parties. The signature record below is the evidence");
        e.Html.Should().MatchRegex("white-space:pre-wrap;word-wrap:break-word;[^>]*>SIGNATURE RECORD\n\nCounterparty : Jean Dupont\nSigned       : 2026-11-24");
        e.Html.IndexOf("#ecfdf5", StringComparison.Ordinal).Should().BeLessThan(e.Html.IndexOf("SIGNATURE RECORD", StringComparison.Ordinal));
        e.Text.Should().Contain(signatures);
        foreach (var part in new[] { e.Text, e.Html })
            part.Should().Contain("The signed contract is attached as a PDF for your records").And.Contain("a complete copy on its own");
    }

    [Fact]
    public void Reminder_PointsToTheOriginalEmail_WithWhenItWasSentAndHowLongItsLinkWorks()
    {
        var e = ContractEmailContent.Reminder(Sender, "Jean Dupont", Org, "Membership Agreement",
            new DateTime(2026, 11, 23, 9, 0, 0, DateTimeKind.Utc), new DateTime(2026, 12, 23, 9, 0, 0, DateTimeKind.Utc));

        e.Subject.Should().Be("Reminder: Membership Agreement");
        foreach (var part in new[] { e.Text, e.Html })
            part.Should().Contain("A contract is waiting for your signature. Please use the link from the original email")
                .And.Contain("Membership Agreement").And.Contain("23 November 2026").And.Contain("23 December 2026")
                .And.Contain("Nothing is agreed until you sign.");
        e.Text.Should().Contain("Sent on: 23 November 2026").And.Contain("Link works until: 23 December 2026");

        var bare = ContractEmailContent.Reminder(Sender, "", "", "Agreement");
        bare.Text.Should().NotContain("Sent on").And.NotContain("Link works until").And.Contain("Hi,");
    }

    [Fact]
    public void EveryEmailCarriesBothAPlainTextAndAnHtmlPart()
    {
        foreach (var e in All())
        {
            e.Subject.Should().NotBeNullOrWhiteSpace();
            e.Text.Should().NotBeNullOrWhiteSpace();
            e.Html.Should().StartWithEquivalentOf("<!doctype html>");
            e.Html.Should().Contain("</html>");
        }
    }

    [Fact]
    public void HtmlIsStyledInline_SoAStrippedStyleBlockLosesOnlyTheExtras()
    {
        // Gmail and others drop a style block: everything that makes the email look as it
        // does is on the element itself, and the block holds only dark mode and phone widths.
        foreach (var e in All())
        {
            var style = Regex.Match(e.Html, "<style>(.*?)</style>", RegexOptions.Singleline).Groups[1].Value;
            style.Should().Contain("@media (prefers-color-scheme:dark)").And.Contain("@media screen and (max-width:620px)");
            var body = Regex.Replace(e.Html, "<head>.*?</head>", "", RegexOptions.Singleline);
            Regex.Matches(body, "<(p|h1|h2|a|td|th)\\b[^>]*>").Select(m => m.Value)
                .Should().OnlyContain(tag => tag.Contains("style=\""), "every block styles itself");
        }
    }

    [Fact]
    public void ADangerousTitleIsDeliveredAsTEXT_NotAsMarkup()
    {
        // A contract title is typed by a person. Unescaped, this would arrive in
        // the counterparty's mail client as markup.
        const string nasty = "Deal <script>alert('x')</script> & \"quoted\"";

        var forSig = ContractEmailContent.ForSignature(Sender, "Jean", Org, nasty, Url);
        var signed = ContractEmailContent.SignedNotification(Sender, "Kia", "Jean", nasty, Url);
        var executed = ContractEmailContent.Executed(Sender, "Jean", Org, nasty, "body", "sigs");
        var reminder = ContractEmailContent.Reminder(Sender, "Jean", Org, nasty);

        foreach (var e in new[] { forSig, signed, executed, reminder })
        {
            e.Html.Should().NotContain("<script>");
            e.Html.Should().Contain("&lt;script&gt;");
            e.Html.Should().Contain("&amp;");
        }
    }

    [Fact]
    public void ADangerousContractBodyIsEscapedToo()
    {
        var e = ContractEmailContent.Executed(
            Sender, "Jean", Org, "Agreement",
            "Clause <img src=x onerror=alert(1)> applies.", "sigs <b>bold</b>");

        e.Html.Should().NotContain("<img").And.NotContain("<b>bold");
        e.Html.Should().Contain("&lt;img").And.Contain("&lt;b&gt;bold");
        // ...but the plain-text part is plain text, so it stays readable as typed.
        e.Text.Should().Contain("Clause <img src=x onerror=alert(1)> applies.");
    }

    [Fact]
    public void AnOrganizationNameIsEscapedInTheHeaderAndFooter()
    {
        var e = ContractEmailContent.ForSignature(Sender, "Jean", "Ben & Jerry's <Club>", "Agreement", Url);
        e.Html.Should().NotContain("<Club>");
        e.Html.Should().Contain("&amp;").And.Contain("&lt;Club&gt;");
    }

    [Fact]
    public void EveryButtonIsPairedWithThePlainUrl()
    {
        // Plenty of clients strip or fail to render the anchor. A button that does
        // nothing, with no way to recover, is how a contract goes unsigned.
        foreach (var e in new[]
        {
            ContractEmailContent.ForSignature(Sender, "Jean", Org, "Agreement", Url),
            ContractEmailContent.SignedNotification(Sender, "Kia", "Jean", "Agreement", Url),
        })
        {
            e.Html.Should().Contain("If the button does not work");
            e.Html.Should().Contain($">{Url}</a>");
            e.Text.Should().Contain(Url, "the text part has no button at all");
        }
    }

    [Fact]
    public void CopesWithNoRecipientName()
    {
        // Leads often have an email and nothing else. "Hi ," reads as broken.
        foreach (var name in new[] { "", "   ", (string?)null })
        {
            var e = ContractEmailContent.ForSignature(Sender, name!, Org, "Agreement", Url);
            e.Text.Should().Contain("\n\nHi,\n\n");
            e.Text.Should().NotContain("Hi ,");
        }
    }

    [Fact]
    public void TrimsAPaddedRecipientName()
    {
        var e = ContractEmailContent.ForSignature(Sender, "  Jean  ", Org, "Agreement", Url);
        e.Text.Should().Contain("\n\nHi Jean,\n\n");
    }

    [Fact]
    public void TheEscapingTheyGoThroughIsTotal()
    {
        // Every value from a record goes through EmailLayout.E, quotes included, since some land in attributes.
        EmailLayout.E("<>&\"'").Should().Be("&lt;&gt;&amp;&quot;&#39;");
        EmailLayout.E(null).Should().BeEmpty();
        EmailLayout.E("").Should().BeEmpty();
        // Ampersand first, or the escapes of the other escapes get double-encoded.
        EmailLayout.E("a & b < c").Should().Be("a &amp; b &lt; c");
    }

    private static IEnumerable<EmailContent> All() => new[]
    {
        ContractEmailContent.ForSignature(Sender, "Jean Dupont", Org, "Membership Agreement", Url),
        ContractEmailContent.SignedNotification(Sender, "Kia", "Jean Dupont", "Membership Agreement", Url),
        ContractEmailContent.Executed(Sender, "Jean Dupont", Org, "Membership Agreement", "body text", "signatures"),
        ContractEmailContent.Reminder(Sender, "Jean Dupont", Org, "Membership Agreement"),
    };
}
