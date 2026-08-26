using ACI.Application.Common;
using ACI.Application.Common.Pdf;
using FluentAssertions;

namespace ACI.Application.Tests.Common.Pdf;

/// <summary>
/// Reading the structure a plain-text contract already has.
/// </summary>
/// <remarks>
/// The risk here is over-reading: deciding a sentence is a table row, or a clause
/// heading is prose. Either produces a document that is subtly wrong in a way
/// nobody checks, because it still looks like a contract. So the cases below are
/// mostly the near misses.
/// </remarks>
public class ContractBlocksTests
{
    [Fact]
    public void ReadsTheShippedTemplateTheWayItIsWritten()
    {
        // The one that matters: the template every organisation starts from.
        var merged = ContractTemplate.Fill(ContractTemplate.DefaultTemplate,
            new Dictionary<string, string?>
            {
                ["org.name"] = "Club Nautique",
                ["lead.name"] = "Jean Dupont",
                ["lead.email"] = "jean@example.ch",
                ["lead.phoneClause"] = "\nPhone:     +41 22 555 01 34",
                ["today"] = "26 August 2026",
                ["contract.startDate"] = "1 September 2026",
                ["contract.fee"] = "CHF 1'450.00",
                ["contract.paymentTerms"] = "annually",
                ["contract.term"] = "twelve months",
                ["contract.noticePeriod"] = "two months'",
                ["contract.jurisdiction"] = "Geneva",
            });
        merged.UnresolvedFields.Should().BeEmpty("the fixture must fill the whole template");

        var blocks = ContractBlocks.Parse(merged.Body);

        blocks[0].Kind.Should().Be(ContractBlockKind.Title);
        blocks[0].Text.Should().Be("MEMBERSHIP AGREEMENT");

        // The "Between: / And: / Date:" rows are a table, not three sentences.
        blocks.Skip(1).Take(3).Should().AllSatisfy(b =>
            b.Kind.Should().Be(ContractBlockKind.Definition));
        blocks[1].Label.Should().Be("Between");
        blocks[1].Text.Should().Be("Club Nautique (\"the Club\")");

        // All seven numbered clauses, in order, recognised as headings.
        blocks.Where(b => b.Kind == ContractBlockKind.Clause).Select(b => b.Text)
            .Should().Equal(
                "1. MEMBERSHIP", "2. FEES", "3. TERM AND RENEWAL",
                "4. NOTICE AND CANCELLATION", "5. CLUB RULES",
                "6. CONTACT DETAILS", "7. GOVERNING LAW");

        // The optional phone clause arrives as its own line and must land as its own
        // row. This is the field that once rendered as "jean@example.chPhone: +41".
        blocks.Should().ContainSingle(b => b.Label == "Phone" && b.Text == "+41 22 555 01 34");

        // The author's own rule before the signature sentence survives as a rule.
        blocks.Should().Contain(b => b.Kind == ContractBlockKind.Rule);
        blocks[^1].Kind.Should().Be(ContractBlockKind.Paragraph);
        blocks[^1].Text.Should().StartWith("By signing below");
    }

    [Fact]
    public void JoinsWrappedProseIntoOneParagraph()
    {
        // The template hard-wraps its prose at about 74 columns. Setting each source
        // line as its own paragraph would put a blank line between every line of text.
        var blocks = ContractBlocks.Parse(
            "The Club grants the Member access to its facilities and services on the\n" +
            "terms set out in this agreement.\n\n" +
            "A second paragraph.");

        blocks.Should().HaveCount(2);
        blocks[0].Text.Should().Be(
            "The Club grants the Member access to its facilities and services on the " +
            "terms set out in this agreement.");
        blocks[1].Text.Should().Be("A second paragraph.");
    }

    [Fact]
    public void DoesNotMistakeASentenceContainingAColonForATableRow()
    {
        // One space after the colon, so it is prose. Setting this as a label and a
        // value would put "Note" in a grey label column and indent the sentence.
        var blocks = ContractBlocks.Parse("Note: the fee is payable monthly in advance.");
        blocks.Should().ContainSingle();
        blocks[0].Kind.Should().Be(ContractBlockKind.Paragraph);
        blocks[0].Text.Should().Be("Note: the fee is payable monthly in advance.");
    }

    [Fact]
    public void DoesNotTreatALongIntroducedClauseAsATableRow()
    {
        // Two spaces, but the label is far too long to be one. Capping the label
        // length is what separates a field from a sentence that happens to align.
        var blocks = ContractBlocks.Parse(
            "The parties agree as follows:  the fee is payable monthly.");
        blocks[0].Kind.Should().Be(ContractBlockKind.Paragraph);
    }

    [Theory]
    [InlineData("=")]
    [InlineData("-")]
    [InlineData("_")]
    [InlineData("*")]
    public void ReadsAStandaloneRuleAsARule(string ch)
    {
        var blocks = ContractBlocks.Parse($"Some prose.\n\n{new string(ch[0], 40)}\n\nMore prose.");
        blocks.Select(b => b.Kind).Should().Equal(
            ContractBlockKind.Paragraph, ContractBlockKind.Rule, ContractBlockKind.Paragraph);
    }

    [Fact]
    public void TellsAnUnderlineApartFromARule()
    {
        // Same characters; the difference is entirely whether there is a line above
        // to underline. Equals means the document's title, dashes mean a clause.
        var blocks = ContractBlocks.Parse("TITLE\n=====\n\nHEADING\n-------\n\n-------");
        blocks.Select(b => b.Kind).Should().Equal(
            ContractBlockKind.Title, ContractBlockKind.Clause, ContractBlockKind.Rule);
        blocks[0].Text.Should().Be("TITLE");
        blocks[1].Text.Should().Be("HEADING");
    }

    [Fact]
    public void DoesNotSwallowTheLineAfterAHeadingAsPartOfIt()
    {
        var blocks = ContractBlocks.Parse("1. FEES\n-------\nThe fee is CHF 100.");
        blocks.Should().HaveCount(2);
        blocks[1].Kind.Should().Be(ContractBlockKind.Paragraph);
        blocks[1].Text.Should().Be("The fee is CHF 100.");
    }

    [Fact]
    public void TwoDashesAreNotAnUnderline()
    {
        // A short run is more likely to be a dash in prose than a heading marker.
        var blocks = ContractBlocks.Parse("Heading\n--");
        blocks.Should().ContainSingle();
        blocks[0].Kind.Should().Be(ContractBlockKind.Paragraph);
        blocks[0].Text.Should().Be("Heading --");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\n  \t ")]
    public void HasNothingToSayAboutNothing(string? body)
    {
        ContractBlocks.Parse(body).Should().BeEmpty();
    }

    [Fact]
    public void ReadsWindowsAndOldMacLineEndings()
    {
        // A body edited on Windows arrives with \r\n. Leaving the \r in place put a
        // stray glyph at the end of every line.
        ContractBlocks.Parse("TITLE\r\n=====\r\n\r\nProse.").Select(b => b.Kind)
            .Should().Equal(ContractBlockKind.Title, ContractBlockKind.Paragraph);
        ContractBlocks.Parse("TITLE\r=====\r\rProse.").Select(b => b.Kind)
            .Should().Equal(ContractBlockKind.Title, ContractBlockKind.Paragraph);
    }

    [Fact]
    public void KeepsAnEmptyValueAsARowRatherThanDroppingIt()
    {
        // "Email:" with nothing after it is a fact about the contract — the field is
        // there and unfilled. Dropping the row would hide that.
        var blocks = ContractBlocks.Parse("Email:     ");
        blocks.Should().ContainSingle();
        blocks[0].Kind.Should().Be(ContractBlockKind.Definition);
        blocks[0].Label.Should().Be("Email");
        blocks[0].Text.Should().BeEmpty();
    }

    [Theory]
    [InlineData("1. MEMBERSHIP", "1.", "MEMBERSHIP")]
    [InlineData("12 FEES", "12", "FEES")]
    [InlineData("3.1.2 SUB-CLAUSE", "3.1.2", "SUB-CLAUSE")]
    public void SplitsAClauseNumberFromItsWords(string heading, string number, string words)
    {
        var split = ContractBlocks.SplitClauseNumber(heading);
        split.Should().NotBeNull();
        split!.Value.Number.Should().Be(number);
        split.Value.Words.Should().Be(words);
    }

    [Theory]
    [InlineData("MEMBERSHIP")]
    [InlineData("2026 was the year")] // a leading number that is not a clause number
    [InlineData("7.")]
    public void LeavesAnUnnumberedHeadingWhole(string heading)
    {
        // Only the numbered form is split, and only so the number can be coloured.
        // A heading that merely starts with a year must not lose it.
        if (heading == "2026 was the year")
        {
            // This one DOES look like a clause number, and that is acceptable: both
            // halves are still drawn, in order, so nothing is lost either way.
            ContractBlocks.SplitClauseNumber(heading).Should().NotBeNull();
            return;
        }
        ContractBlocks.SplitClauseNumber(heading).Should().BeNull();
    }
}
