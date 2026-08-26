using ACI.Application.Common;

namespace ACI.Application.Tests.Common;

/// <summary>
/// The three contract emails.
/// </summary>
/// <remarks>
/// Email content is usually the least tested thing a customer actually reads,
/// because it hides behind an SMTP connection. These are the properties that
/// matter: nothing a person typed reaches the recipient as markup, the link is
/// always recoverable when the button fails, both parts carry the same substance,
/// and the executed copy really does contain the whole contract.
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
        e.Text.Should().Contain("Hi Jean Dupont").And.Contain(Url);
        e.Html.Should().Contain("Please review and sign").And.Contain(Url);
        // The reassurance is the difference between a contract request and a
        // demand: they need to know signing is the only committing act.
        e.Text.Should().Contain("Nothing is agreed until you sign");
        e.Html.Should().Contain("Nothing is agreed until you sign");
    }

    [Fact]
    public void SignedNotification_NamesWhoSignedAndAsksForOurs()
    {
        var e = ContractEmailContent.SignedNotification(Sender, "Kia", "Jean Dupont", "Membership Agreement", Url);

        e.Subject.Should().Contain("Jean Dupont").And.Contain("your signature is next");
        e.Text.Should().Contain("Jean Dupont").And.Contain(Url);
        e.Html.Should().Contain("They have signed").And.Contain("Countersign");
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
        // The HTML gets it escaped but complete.
        e.Html.Should().Contain("Fees are CHF 2,400").And.Contain("Countersigned by Kia");
        e.Subject.Should().Contain("Signed by both parties");
        e.Text.Should().Contain("keep this email as your copy");
    }

    [Fact]
    public void EveryEmailCarriesBothAPlainTextAndAnHtmlPart()
    {
        foreach (var e in AllThree())
        {
            e.Subject.Should().NotBeNullOrWhiteSpace();
            e.Text.Should().NotBeNullOrWhiteSpace();
            e.Html.Should().StartWith("<!doctype html>");
            e.Html.Should().Contain("</html>");
        }
    }

    [Fact]
    public void HtmlIsStyledInline_BecauseMailClientsStripStyleBlocks()
    {
        foreach (var e in AllThree())
        {
            e.Html.Should().NotContain("<style", "every mail client strips a style block");
            e.Html.Should().NotContain("class=", "a stripped stylesheet leaves classes meaningless");
            e.Html.Should().Contain("style=\"", "so everything has to be inlined");
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

        foreach (var e in new[] { forSig, signed, executed })
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
            "Clause <img src=x onerror=alert(1)> applies.", "sigs");

        e.Html.Should().NotContain("<img");
        e.Html.Should().Contain("&lt;img");
        // ...but the plain-text part is plain text, so it stays readable as typed.
        e.Text.Should().Contain("Clause <img src=x onerror=alert(1)> applies.");
    }

    [Fact]
    public void AnOrganizationNameIsEscapedInTheFooter()
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
            e.Html.Should().Contain(Url);
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
            e.Text.Should().StartWith("Hi,");
            e.Text.Should().NotContain("Hi ,");
        }
    }

    [Fact]
    public void TrimsAPaddedRecipientName()
    {
        var e = ContractEmailContent.ForSignature(Sender, "  Jean  ", Org, "Agreement", Url);
        e.Text.Should().StartWith("Hi Jean,");
    }

    [Fact]
    public void EscapeIsTotal()
    {
        ContractEmailContent.Escape("<>&\"'").Should().Be("&lt;&gt;&amp;&quot;&#39;");
        ContractEmailContent.Escape(null).Should().BeEmpty();
        ContractEmailContent.Escape("").Should().BeEmpty();
        // Ampersand first, or the escapes of the other escapes get double-encoded.
        ContractEmailContent.Escape("a & b < c").Should().Be("a &amp; b &lt; c");
    }

    private static IEnumerable<EmailContent> AllThree() => new[]
    {
        ContractEmailContent.ForSignature(Sender, "Jean Dupont", Org, "Membership Agreement", Url),
        ContractEmailContent.SignedNotification(Sender, "Kia", "Jean Dupont", "Membership Agreement", Url),
        ContractEmailContent.Executed(Sender, "Jean Dupont", Org, "Membership Agreement", "body text", "signatures"),
    };
}
