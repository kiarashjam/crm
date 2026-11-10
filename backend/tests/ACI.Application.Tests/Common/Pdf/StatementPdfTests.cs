using ACI.Application.Common.Pdf;

namespace ACI.Application.Tests.Common.Pdf;

/// <summary>
/// A member's statement as a PDF: what it says, that nothing runs off the page, and that a
/// long one carries on over pages that say where they are.
/// </summary>
/// <remarks>
/// The statement used to be the browser's print of a page that added up today's charges and
/// payments. The server's PDF is set from the ledger's statement, so these read the text back
/// out of the generated file rather than trusting the code that drew it.
/// </remarks>
public class StatementPdfTests
{
    private const double Left = 50;
    private const double Right = PdfBuilder.A4WidthPt - 50;

    private static StatementPdfRequest Request(IReadOnlyList<StatementPdfLine>? lines = null, decimal closing = 299m, string? name = null) => new(
        "Pavillon 46",
        name ?? "Jean Dupont",
        "jean@example.ch",
        "Gold",
        new DateTime(2026, 9, 30, 22, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 10, 31, 23, 0, 0, DateTimeKind.Utc),
        "CHF",
        "de-CH",
        "Europe/Zurich",
        0m,
        lines?.Sum(l => l.Charge) ?? 1299m,
        lines?.Sum(l => l.Payment) ?? 1000m,
        closing,
        lines ??
        [
            new(new DateTime(2026, 10, 1, 6, 0, 0, DateTimeKind.Utc), "Gold dues · 1 Oct 2026 – 31 Oct 2026", null, 299m, 0m, 299m, 8.1m),
            new(new DateTime(2026, 10, 3, 9, 0, 0, DateTimeKind.Utc), "Initiation fee, Gold", null, 1000m, 0m, 1299m, 8.1m),
            new(new DateTime(2026, 10, 9, 15, 0, 0, DateTimeKind.Utc), "Payment (Wire)", "Ref 2026-114", 0m, 1000m, 299m, null),
        ],
        new DateTime(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc));

    private static string TextOf(byte[] pdf) => string.Join("\n", PdfPageScanner.Scan(pdf).Select(r => r.Text));

    [Fact]
    public void ItSaysThePeriodInTheClubsTimeZone_EachEntry_TheAmountDue_AndTheVatIncluded()
    {
        var pdf = StatementPdf.Render(Request(), compressStreams: false);
        var text = TextOf(pdf);

        // The period's days are Zurich's, not UTC's (whose would be 30 Sep to 31 Oct). The
        // dash between them is WinAnsi's, which the scanner's Latin-1 reading drops.
        text.Should().Contain("Statement").And.MatchRegex(@"\n1 Oct 2026 .?\s?31 Oct 2026\n");
        text.Should().Contain("Jean Dupont").And.Contain("Gold member").And.Contain("Balance brought forward");
        text.Should().Contain("Gold dues · 1 Oct 2026").And.Contain("Payment (Wire)").And.Contain("Ref 2026-114");
        // de-CH writes a thousands separator: an apostrophe, straight or typographic by ICU
        // version, which the scanner may drop as it does the dash.
        text.Should().MatchRegex(@"CHF 1.?299\.00");
        text.Should().Contain("Amount due: CHF 299.00");
        text.Should().Contain("8.1% on CHF").And.Contain("VAT included");
        text.Should().Contain("Page 1 of 1");
    }

    [Fact]
    public void AMemberInCreditIsToldSo_AndOneWithNothingToPayToo()
    {
        TextOf(StatementPdf.Render(Request(closing: -50m), compressStreams: false)).Should().Contain("In credit: CHF 50.00");
        TextOf(StatementPdf.Render(Request(closing: 0m), compressStreams: false)).Should().Contain("Nothing to pay");
    }

    [Fact]
    public void ALongStatementCarriesOnOverPages_AndNoTextRunsPastTheMargins()
    {
        var lines = Enumerable.Range(1, 90).Select(i => new StatementPdfLine(
            new DateTime(2026, 10, 1, 6, 0, 0, DateTimeKind.Utc).AddHours(i * 7),
            $"Bar tab {i} with a description long enough that it has to be cut short somewhere before the amounts begin, number {i}",
            i % 3 == 0 ? $"Void · entered twice by the late shift, which is a long reason that also has to fit, item {i}" : null,
            12.5m, i % 3 == 0 ? 12.5m : 0m, i * 1.5m, null)).ToList();
        var pdf = StatementPdf.Render(Request(lines, name: new string('W', 120)), compressStreams: false);
        var runs = PdfPageScanner.Scan(pdf);

        runs.Max(r => r.Page).Should().BeGreaterThan(2);
        runs.Should().Contain(r => r.Page == 2 && r.Text == "Statement, continued", "a later page says it carries a statement on");
        runs.Should().Contain(r => r.Text == $"Page 3 of {runs.Max(x => x.Page)}");
        foreach (var run in runs)
        {
            run.X.Should().BeGreaterThanOrEqualTo(Left - 0.5, $"'{run.Text}' starts inside the left margin");
            (run.X + run.Width).Should().BeLessThanOrEqualTo(Right + 0.5, $"'{run.Text}' ends inside the right margin");
        }
    }
}
