using System.Text;
using ACI.Application.Common;
using ACI.Application.Common.Pdf;
using ACI.Application.DTOs;
using ACI.Application.Services;
using ACI.Domain.Entities;
using FluentAssertions;

namespace ACI.Application.Tests.Common.Pdf;

/// <summary>
/// The contract document, checked as a file and as a rendering.
/// </summary>
/// <remarks>
/// Two kinds of test here. The structural ones check the file is a file — a reader
/// given a bad cross-reference offset reports "damaged" and shows nothing, and no
/// assertion about content would ever catch that. The rest read the operators back
/// out of the generated document and measure them, because the defects this code
/// actually produced were a title that ran into the page number and a watermark
/// that ran off the sheet, and both looked fine in every test that did not measure.
/// </remarks>
public class ContractPdfTests
{
    private const double PageWidth = PdfBuilder.A4WidthPt;
    private const double PageHeight = PdfBuilder.A4HeightPt;

    /// <summary>Must match ContractPdf's own margin.</summary>
    private const double MarginX = 62;

    private static string FilledBody() =>
        ContractTemplate.Fill(ContractTemplate.DefaultTemplate, new Dictionary<string, string?>
        {
            ["org.name"] = "Club Nautique du Leman",
            ["lead.name"] = "Jean-Michel Dupont",
            ["lead.email"] = "jean-michel.dupont@example.ch",
            ["lead.phoneClause"] = "\nPhone:     +41 22 555 01 34",
            ["today"] = "26 August 2026",
            ["contract.startDate"] = "1 September 2026",
            ["contract.fee"] = "CHF 1'450.00 per year",
            ["contract.paymentTerms"] = "annually in advance",
            ["contract.term"] = "twelve months",
            ["contract.noticePeriod"] = "two months'",
            ["contract.jurisdiction"] = "the Canton of Geneva, Switzerland",
        }).Body;

    private static readonly DateTime At = new(2026, 8, 26, 14, 32, 0, DateTimeKind.Utc);

    private static ContractPdfRequest Request(
        string status = ContractStatuses.Countersigned,
        string? title = "Membership Agreement — Jean-Michel Dupont",
        string? body = null,
        string? hash = "674ac833bdeb2ac6eb0b3aecdcd5631f19353bab27b9579f2a250ae9c841e2c8",
        string counterparty = "Jean-Michel Dupont",
        string org = "Club Nautique du Leman")
        => new(
            title ?? "", body ?? FilledBody(), org, counterparty, status,
            Reference: "3F9A21C0", BodyHash: hash, GeneratedAtUtc: At,
            ClientSignatureName: status is ContractStatuses.SignedByClient or ContractStatuses.Countersigned
                ? counterparty : null,
            ClientSignedAtUtc: status is ContractStatuses.SignedByClient or ContractStatuses.Countersigned
                ? At.AddHours(-19) : null,
            ClientSignatureIp: "84.226.11.7",
            CounterSignatureName: status == ContractStatuses.Countersigned ? "Anaïs Berger" : null,
            CounterSignedAtUtc: status == ContractStatuses.Countersigned ? At : null,
            CounterSignatureIp: status == ContractStatuses.Countersigned ? "195.176.3.24" : null);

    private static string AsText(byte[] pdf) => Encoding.Latin1.GetString(pdf);

    /* ------------------------------------------------------------ structure */

    [Fact]
    public void IsAWellFormedFile()
    {
        var pdf = ContractPdf.Render(Request()).Bytes;
        var text = AsText(pdf);

        text.Should().StartWith("%PDF-1.4\n");
        text.Should().EndWith("%%EOF\n");
        text.Should().Contain("/Type /Catalog");
        text.Should().Contain("/Type /Pages");
        text.Should().Contain("trailer");
        // Every font declared WinAnsi, or apostrophes and accents come out wrong.
        System.Text.RegularExpressions.Regex.Matches(text, "/Encoding /WinAnsiEncoding")
            .Count.Should().Be(PdfFonts.All.Length);
    }

    [Fact]
    public void EveryCrossReferenceOffsetPointsAtItsObject()
    {
        // The single most consequential thing in the file. A reader that follows one
        // of these to the wrong byte reports the document as damaged and renders
        // nothing at all — there is no partial failure to notice.
        var pdf = ContractPdf.Render(Request()).Bytes;
        var text = AsText(pdf);

        var startxref = text.LastIndexOf("startxref", StringComparison.Ordinal);
        startxref.Should().BeGreaterThan(0);
        var xrefOffset = int.Parse(text[(startxref + 9)..].Trim().Split('\n')[0]);
        text[xrefOffset..].Should().StartWith("xref\n");

        var lines = text[xrefOffset..].Split('\n');
        var count = int.Parse(lines[1].Split(' ')[1]);
        // Entry 0 is the free-list head; the rest are real objects, numbered from 1.
        for (var obj = 1; obj < count; obj++)
        {
            var entry = lines[1 + obj + 1];
            entry.Should().HaveLength(19, $"xref entry {obj} must be exactly 20 bytes including the newline");
            var offset = int.Parse(entry[..10]);
            text[offset..].Should().StartWith($"{obj} 0 obj",
                $"object {obj}'s offset must land on its own header");
        }
    }

    [Fact]
    public void RendersTheSameBytesForTheSameContract()
    {
        // Without this nothing downstream can be cached, compared, or attached twice
        // to the same email and expected to match.
        var a = ContractPdf.Render(Request()).Bytes;
        var b = ContractPdf.Render(Request()).Bytes;
        a.Should().Equal(b);
    }

    [Fact]
    public void CompressesByDefaultAndIsSmallEnoughToEmail()
    {
        var compressed = ContractPdf.Render(Request()).Bytes;
        var plain = ContractPdf.Render(Request(), compressStreams: false).Bytes;

        AsText(compressed).Should().Contain("/Filter /FlateDecode");
        AsText(plain).Should().NotContain("/Filter");
        compressed.Length.Should().BeLessThan(plain.Length);
        // A whole executed contract in a few kilobytes is what carrying no font data
        // buys; anything near a megabyte would mean something is being embedded.
        compressed.Length.Should().BeLessThan(64 * 1024);
    }

    [Fact]
    public void NeverProducesAZeroPageDocument()
    {
        // Every reader refuses a PDF with no pages, and an empty contract is exactly
        // what a brand-new draft is.
        var result = ContractPdf.Render(Request(title: "", body: "", hash: null,
            counterparty: "", org: ""));
        result.PageCount.Should().Be(1);
        AsText(result.Bytes).Should().Contain("/Count 1");
    }

    /* -------------------------------------------------- measured rendering */

    [Fact]
    public void NoTextRunsPastEitherMargin()
    {
        // The assertion this whole scanner exists for. Two real defects were exactly
        // this: a footer title truncated by character count ran into the centred page
        // number, and a long status word set to the page diagonal ran off the corner.
        var right = PageWidth - MarginX;

        foreach (var status in new[]
        {
            ContractStatuses.Draft, ContractStatuses.Sent, ContractStatuses.SignedByClient,
            ContractStatuses.Countersigned, ContractStatuses.Declined, ContractStatuses.Voided,
        })
        {
            var pdf = ContractPdf.Render(Request(status), compressStreams: false).Bytes;
            var runs = PdfPageScanner.Scan(pdf);
            runs.Should().NotBeEmpty($"[{status}] the scanner must find the text");

            foreach (var run in runs)
            {
                (run.X + run.Width).Should().BeLessThanOrEqualTo(right + 0.5,
                    $"[{status}] p{run.Page} \"{run.Text}\" must not cross the right margin");
                run.X.Should().BeGreaterThanOrEqualTo(MarginX - 0.5,
                    $"[{status}] p{run.Page} \"{run.Text}\" must not cross the left margin");
                run.Y.Should().BeInRange(0, PageHeight,
                    $"[{status}] p{run.Page} \"{run.Text}\" must be on the page");
            }
        }
    }

    [Fact]
    public void NoTextRunsPastTheMarginWithTheLongestPossibleValues()
    {
        // The overrun cases are all "somebody typed something long". A title, an
        // organisation name and a counterparty name are all free text.
        var request = Request(
            title: new string('W', 300),
            org: new string('M', 200),
            counterparty: new string('N', 200),
            body: FilledBody() + "\n\n" + new string('x', 400) + "\n\nhttps://"
                  + new string('u', 300) + "/sign");

        var runs = PdfPageScanner.Scan(ContractPdf.Render(request, compressStreams: false).Bytes);
        var right = PageWidth - MarginX;

        runs.Should().AllSatisfy(run =>
            (run.X + run.Width).Should().BeLessThanOrEqualTo(right + 0.5,
                $"p{run.Page} \"{Shorten(run.Text)}\" must not cross the right margin"));
    }

    [Fact]
    public void SetsTheContractTextWithoutChangingItsWords()
    {
        // The PDF is a rendering, not a rewrite. Every word, amount and address in
        // the body must appear, and the re-wrapping must not drop or merge any.
        var runs = PdfPageScanner.Scan(ContractPdf.Render(Request(), compressStreams: false).Bytes);
        var rendered = string.Join(" ", runs.Select(r => r.Text));

        foreach (var fragment in new[]
        {
            "MEMBERSHIP AGREEMENT", "Club Nautique du Leman", "Jean-Michel Dupont",
            "jean-michel.dupont@example.ch", "+41 22 555 01 34", "CHF 1'450.00 per year",
            "1 September 2026", "twelve months", "two months'",
            "the Canton of Geneva, Switzerland",
            "1.", "MEMBERSHIP", "7.", "GOVERNING LAW",
        })
        {
            rendered.Should().Contain(fragment);
        }
    }

    [Fact]
    public void ShowsBothSignaturesAndTheHashOnAnExecutedContract()
    {
        var runs = PdfPageScanner.Scan(
            ContractPdf.Render(Request(ContractStatuses.Countersigned), compressStreams: false).Bytes);
        var rendered = string.Join("\n", runs.Select(r => r.Text));

        rendered.Should().Contain("SIGNATURES");
        rendered.Should().Contain("Jean-Michel Dupont");
        rendered.Should().Contain("Anaïs Berger", "an accented name must survive the encoding");
        rendered.Should().Contain("84.226.11.7");
        rendered.Should().Contain("Signed 25 August 2026 at 19:32 UTC");
        rendered.Should().Contain("DOCUMENT INTEGRITY");
        rendered.Should().Contain("674ac833bdeb", "the hash must be readable, not just stored");
        rendered.Should().Contain("not a", "the eIDAS/ZertES limitation must be stated");
        rendered.Should().NotContain("EXECUTED\nEXECUTED");
    }

    [Fact]
    public void ShowsAnUnsignedContractAsUnsignedRatherThanAsFinished()
    {
        var runs = PdfPageScanner.Scan(
            ContractPdf.Render(Request(ContractStatuses.Sent, hash: null), compressStreams: false).Bytes);
        var rendered = string.Join("\n", runs.Select(r => r.Text));

        // Blank signature rules with prompts, not an empty panel that reads as a
        // rendering fault, and certainly not the word EXECUTED anywhere.
        rendered.Should().Contain("Not yet signed");
        rendered.Should().Contain("AWAITING SIGNATURE");
        rendered.Should().NotContain("EXECUTED");
        rendered.Should().Contain("has not been sent for signature yet");
    }

    [Theory]
    [InlineData(ContractStatuses.Draft, "DRAFT", true)]
    [InlineData(ContractStatuses.Sent, "AWAITING SIGNATURE", true)]
    [InlineData(ContractStatuses.SignedByClient, "AWAITING COUNTERSIGNATURE", true)]
    [InlineData(ContractStatuses.Countersigned, "EXECUTED", false)]
    [InlineData(ContractStatuses.Declined, "DECLINED", true)]
    [InlineData(ContractStatuses.Voided, "VOID", true)]
    public void StampsEveryStateAndWatermarksEveryUnfinishedOne(
        string status, string pill, bool watermarked)
    {
        // The executed copy is the only one with no watermark, because it is the only
        // one that IS the finished document. Everything else must be impossible to
        // mistake for it on paper.
        var pdf = ContractPdf.Render(Request(status), compressStreams: false).Bytes;
        var text = AsText(pdf);

        text.Should().Contain(PdfLiteral(pill));

        // The pill and the watermark often carry the SAME word, so "the word is in
        // the file" proves nothing. The scanner follows only upright runs, so a word
        // that appears in the file more often than it appears upright is rotated.
        var upright = PdfPageScanner.Scan(pdf).Select(r => r.Text).ToList();
        var rotated = new[] { "DRAFT", "UNSIGNED", "AWAITING COUNTERSIGNATURE", "DECLINED", "VOID" }
            .Any(w => Occurrences(text, PdfLiteral(w)) > upright.Count(t => t == w));
        rotated.Should().Be(watermarked, $"{status} watermark");
    }

    [Fact]
    public void AnUnrecognisedStatusIsStampedDraftRatherThanLeftLookingExecuted()
    {
        // Whatever a later version of the state machine adds, the safe default is the
        // one that cannot be mistaken for a signed agreement.
        var text = AsText(ContractPdf.Render(Request("some_future_status"), compressStreams: false).Bytes);
        text.Should().Contain(PdfLiteral("DRAFT"));
        text.Should().NotContain(PdfLiteral("EXECUTED"));
    }

    /* --------------------------------------------------------- page layout */

    [Fact]
    public void PutsTheRunningHeadAndAPageCountOnEveryPage()
    {
        var longBody = FilledBody() + string.Concat(Enumerable.Range(0, 30).Select(n =>
            $"\n\n{n + 8}. EXTRA CLAUSE\n--------------\n" + new string('w', 60) + " text here."));

        var result = ContractPdf.Render(
            Request(body: longBody), compressStreams: false);
        result.PageCount.Should().BeGreaterThan(2);

        var runs = PdfPageScanner.Scan(result.Bytes);
        for (var page = 1; page <= result.PageCount; page++)
        {
            var onPage = runs.Where(r => r.Page == page).Select(r => r.Text).ToList();
            onPage.Should().Contain($"Page {page} of {result.PageCount}",
                $"page {page} needs its own number — a loose sheet has to be placeable");
            onPage.Should().Contain(t => t.Contains("Club Nautique") || t.Contains("CLUB NAUTIQUE"),
                $"page {page} needs the organisation on it");
        }
    }

    [Fact]
    public void KeepsTheSignaturePanelWholeRatherThanSplittingIt()
    {
        // A signature panel broken across a page boundary is the worst possible place
        // for a page break: the names end up on a different sheet from the rules they
        // sit on. Grown one line at a time, the panel must always stay together.
        for (var extra = 0; extra < 26; extra++)
        {
            var body = FilledBody() + string.Concat(
                Enumerable.Repeat("\n\nA further paragraph of terms.", extra));
            var result = ContractPdf.Render(Request(body: body), compressStreams: false);
            var runs = PdfPageScanner.Scan(result.Bytes);

            var pagesWithPanel = runs
                .Where(r => r.Text is "SIGNATURES" or "Not yet signed" || r.Text.StartsWith("Signed "))
                .Select(r => r.Page).Distinct().ToList();

            pagesWithPanel.Should().HaveCount(1,
                $"with {extra} extra paragraphs the signature panel must sit on one page");
        }
    }

    [Fact]
    public void ReportsCharactersItCouldNotCarryInsteadOfHidingThem()
    {
        var result = ContractPdf.Render(Request(counterparty: "张伟"));
        result.UnrepresentableCharacters.Should().Equal(new[] { '张', '伟' });

        // And a document with nothing exotic in it must not cry wolf, or the warning
        // means nothing.
        ContractPdf.Render(Request(counterparty: "Anaïs Berger — Zürich"))
            .UnrepresentableCharacters.Should().BeEmpty();
    }

    [Fact]
    public void CarriesTheContractTitleIntoTheFileMetadata()
    {
        // What a reader shows in its tab and what a mail client shows when the
        // attachment is previewed.
        var text = AsText(ContractPdf.Render(Request()).Bytes);
        text.Should().Contain("/Title (Membership Agreement \\227 Jean-Michel Dupont)");
        text.Should().Contain("/Author (Club Nautique du Leman)");
        text.Should().Contain("/Subject (Executed contract)");
        text.Should().Contain("/CreationDate (D:20260826143200Z)");
    }

    [Fact]
    public void DoesNotPrintTheDocumentTitleTwice()
    {
        // The body opens with its own "MEMBERSHIP AGREEMENT" heading, which is
        // promoted into the title block. Setting both would print it twice, one under
        // the other.
        var runs = PdfPageScanner.Scan(ContractPdf.Render(Request(), compressStreams: false).Bytes);
        runs.Count(r => r.Text == "MEMBERSHIP AGREEMENT").Should().Be(1);
    }

    [Fact]
    public void FallsBackToTheContractTitleWhenTheBodyHasNoHeading()
    {
        var runs = PdfPageScanner.Scan(ContractPdf.Render(
            Request(title: "Sponsorship Agreement", body: "Just some prose, no heading at all."),
            compressStreams: false).Bytes);

        runs.Select(r => r.Text).Should().Contain("Sponsorship Agreement");
        runs.Select(r => r.Text).Should().Contain("Just some prose, no heading at all.");
    }

    /* -------------------------------------------------------- typesetting */

    [Fact]
    public void JustifiedLinesDoNotOpenRiversOfWhite()
    {
        // Justification that stretches a space too far is worse than a ragged edge.
        // Every body line either sits close to the measure or is a paragraph's last.
        var runs = PdfPageScanner.Scan(ContractPdf.Render(Request(), compressStreams: false).Bytes)
            .Where(r => r.Font == PdfFont.TimesRoman && r.Text.Contains(' ') && r.Text.Length > 40)
            .ToList();

        runs.Should().NotBeEmpty();
        runs.Should().AllSatisfy(r =>
            r.Width.Should().BeLessThanOrEqualTo(PageWidth - 2 * MarginX + 0.5,
                $"\"{Shorten(r.Text)}\" is wider than the measure"));
    }
    /* --------------------------------------------- the file, as a download */

    [Theory]
    // Every one of these was a 500. char.IsLetterOrDigit is true of an accented
    // letter, so the filename kept characters that ASP.NET Core refuses in a
    // Content-Disposition header — and accented names are most of the names here.
    [InlineData("Membership Agreement \u2014 Ana\u00EFs Berger")]
    [InlineData("Mitgliedschaft \u2014 Z\u00FCrich")]
    [InlineData("Accord \u2014 Fran\u00E7ois Lema\u00EEtre")]
    [InlineData("\u5F20\u4F1F membership")]
    [InlineData("")]
    [InlineData("////")]
    public void TheDownloadFilenameIsAlwaysPlainAscii(string title)
    {
        var name = ContractService.DocumentFileName(new Contract
        {
            Id = Guid.Parse("3f9a21c0-1111-2222-3333-444444444444"),
            Title = title,
            Status = ContractStatuses.Countersigned,
        });

        name.Should().MatchRegex("^[A-Za-z0-9.-]+$",
            "a Content-Disposition filename must be ASCII or the header throws");
        name.Should().EndWith(".pdf");
        // And it must still identify WHICH contract: "contract.pdf" in a folder of
        // thirty is worth nothing.
        name.Should().Contain("3F9A21C0");
    }

    [Fact]
    public void TheFilenameKeepsTheLettersItCanRatherThanDroppingThem()
    {
        // Folding, not stripping. Dropping the accented characters would hand
        // somebody a file called "Ana-s-Berger".
        var name = ContractService.DocumentFileName(new Contract
        {
            Id = Guid.NewGuid(),
            Title = "Membership \u2014 Ana\u00EFs Z\u00FCrich",
            Status = ContractStatuses.Countersigned,
        });
        name.Should().Contain("Anais").And.Contain("Zurich");
    }

    [Fact]
    public void ATabInTheBodyIsLaidOutAsSpaceRatherThanAsAQuestionMark()
    {
        // A tab is whitespace, not an unrepresentable character. It was rendering
        // as "?" in the middle of a clause, in the copy both parties keep.
        var runs = PdfPageScanner.Scan(ContractPdf.Render(
            Request(body: "1. FEES\n-------\nThe fee is\tCHF 100 per\tyear."),
            compressStreams: false).Bytes);
        var rendered = string.Join(" ", runs.Select(r => r.Text));

        rendered.Should().NotContain("?");
        rendered.Should().Contain("CHF 100");
        ContractPdf.Render(Request(body: "a\tb")).UnrepresentableCharacters.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Membership-Agreement-Anais-Berger-3F9A21C0-signed.pdf")]
    [InlineData("Mitgliedschaft-Z\u00FCrich.pdf")]   // if a non-ASCII name ever slips through
    [InlineData("has\"quote;and-semicolon.pdf")]
    [InlineData("")]
    public void TheContentDispositionHeaderIsAlwaysAsciiAndUnbreakable(string fileName)
    {
        // Kestrel throws on a non-ASCII header value, so this is the last line of
        // defence for the 500. A quote or a semicolon would end the parameter early
        // and let the filename break out of the header.
        var header = new ContractDocument(fileName, "application/pdf", new byte[] { 1 })
            .InlineContentDisposition;

        header.Should().MatchRegex("^[\\u0020-\\u007E]+$", "a header value must be ASCII");
        header.Should().StartWith("inline; filename=\"");
        // Exactly one quoted parameter: three quotes would mean the value escaped.
        header.Split('"').Should().HaveCount(3);
        header.Should().Contain("filename*=UTF-8''", "the real name must still reach the browser");
    }

    [Fact]
    public void TheHeaderCarriesTheAccentedNameEvenThoughTheAsciiFallbackCannot()
    {
        var header = new ContractDocument("Mitgliedschaft-Z\u00FCrich.pdf", "application/pdf", Array.Empty<byte>())
            .InlineContentDisposition;
        // Percent-encoded UTF-8: 0xC3 0xBC is u-with-diaeresis.
        header.Should().Contain("Z%C3%BCrich");
    }


    private static string PdfLiteral(string text) => WinAnsi.Literal(WinAnsi.Encode(text).Bytes);

    private static int Occurrences(string haystack, string needle)
    {
        var n = 0;
        for (var at = haystack.IndexOf(needle, StringComparison.Ordinal); at >= 0;
             at = haystack.IndexOf(needle, at + 1, StringComparison.Ordinal)) n++;
        return n;
    }

    private static string Shorten(string text)
        => text.Length <= 48 ? text : text[..48] + "...";
}
