using ACI.Application.Common.Payments;
using ACI.Application.Common.Pdf;

namespace ACI.Application.Tests.Common.Pdf;

/// <summary>
/// A statement and an invoice in the member's language: German and French headings, notes,
/// month names and QR-bill, with the amounts still in the workspace's number format, and
/// nothing running off the page however much longer the words are than the English.
/// </summary>
/// <remarks>
/// The text is read back out of the file in Latin-1, which WinAnsi agrees with for every
/// letter used here (ä, ü, é, à, û…); a typographic apostrophe and a dash are not checked.
/// </remarks>
public class StatementPdfLanguageTests
{
    private const double PageW = PdfBuilder.A4WidthPt;

    private static readonly QrBill Bill = new(
        "CH4431999123000889012",
        new QrBillAddress("Pavillon 46", "Rue du Lac", "1268", "2501", "Biel", "CH"),
        299m,
        "CHF",
        null,
        SwissQrBill.Qrr,
        "210000000003139471430009017",
        "Kontoauszug Oktober 2026");

    private static StatementPdfRequest Request(string language, int lineCount = 3, decimal closing = 299m, QrBill? slip = null) => new(
        "Pavillon 46", "Jean Dupont", "jean@example.ch", "Gold",
        new DateTime(2026, 9, 30, 22, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 31, 23, 0, 0, DateTimeKind.Utc),
        "CHF", "de-CH", "Europe/Zurich",
        0m, 1299m, 1000m, closing,
        Enumerable.Range(1, lineCount).Select(i => new StatementPdfLine(
            new DateTime(2026, 10, 1, 6, 0, 0, DateTimeKind.Utc).AddHours(i), $"Bar {i}", null, 1m, 0m, i, 8.1m)).ToList(),
        new DateTime(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc),
        slip,
        Language: language);

    private static string TextOf(byte[] pdf) => string.Join("\n", PdfPageScanner.Scan(pdf).Select(r => r.Text));

    private static void NothingRunsOffThePage(byte[] pdf)
    {
        foreach (var run in PdfPageScanner.Scan(pdf))
        {
            run.X.Should().BeGreaterThanOrEqualTo(0, $"'{run.Text}' starts on the page");
            (run.X + run.Width).Should().BeLessThanOrEqualTo(PageW + 0.5, $"'{run.Text}' stays on the page");
        }
    }

    [Fact]
    public void AGermanStatement_SaysEverythingInSwissGerman_WithTheQrBillInTheGuidelinesWords()
    {
        var pdf = StatementPdf.Render(Request("de", slip: Bill), compressStreams: false);
        var text = TextOf(pdf);

        text.Should().Contain("Kontoauszug").And.Contain("Ausgestellt am").And.Contain("Mitglied Gold");
        text.Should().Contain("SALDOVORTRAG").And.Contain("BELASTUNGEN").And.Contain("SCHLUSSSALDO");
        text.Should().Contain("DATUM").And.Contain("BESCHREIBUNG").And.Contain("OFFEN");
        text.Should().Contain("Saldovortrag").And.Contain("Total der Periode");
        text.Should().MatchRegex(@"1\. Okt\.? 2026", "the dates' month names are German");
        text.Should().Contain("Offener Betrag: CHF 299.00", "the amount keeps the workspace's number format");
        text.Should().Contain("In den obigen Belastungen enthaltene MWST").And.MatchRegex(@"8\.1 % auf CHF");
        text.Should().Contain("Bezahlen Sie mit der QR-Rechnung");
        text.Should().Contain("Seite 1 von 1");

        text.Should().Contain("Empfangsschein").And.Contain("Zahlteil").And.Contain("Konto / Zahlbar an")
            .And.Contain("Referenz").And.Contain("Zusätzliche Informationen").And.Contain("Zahlbar durch (Name/Adresse)")
            .And.Contain("Währung").And.Contain("Betrag").And.Contain("Annahmestelle").And.Contain("Vor der Einzahlung abzutrennen");
        text.Should().NotContain("Payment part").And.NotContain("Receipt").And.NotContain("Statement");
        NothingRunsOffThePage(pdf);
    }

    [Fact]
    public void AFrenchStatement_SaysEverythingInFrench_WithTheQrBillInTheGuidelinesWords()
    {
        var pdf = StatementPdf.Render(Request("fr", slip: Bill with { Message = "Relevé octobre 2026" }), compressStreams: false);
        var text = TextOf(pdf);

        text.Should().Contain("Relevé de compte").And.Contain("Membre Gold");
        text.Should().Contain("SOLDE REPORTÉ").And.Contain("DÉBITS").And.Contain("SOLDE FINAL");
        text.Should().Contain("Totaux de la période");
        text.Should().MatchRegex(@"1 oct\.? 2026", "the dates' month names are French");
        // The no-break space before the colon is printed as a space (WinAnsi has no NBSP glyph of its own).
        text.Should().MatchRegex(@"Montant d. : CHF 299\.00");
        text.Should().Contain("Payez avec la QR-facture");
        text.Should().Contain("Page 1 sur 1");

        text.Should().Contain("Récépissé").And.Contain("Section paiement").And.Contain("Compte / Payable à")
            .And.Contain("Référence").And.Contain("Informations supplémentaires").And.Contain("Payable par (nom/adresse)")
            .And.Contain("Monnaie").And.Contain("Montant").And.Contain("Point de dépôt").And.Contain("A détacher avant le versement");
        text.Should().NotContain("Payment part").And.NotContain("Receipt");
        NothingRunsOffThePage(pdf);
    }

    [Fact]
    public void ALongStatementInGermanOrFrench_CarriesOnOverPagesThatSayWhereTheyAre_AndNothingRunsOff()
    {
        var german = StatementPdf.Render(Request("de", lineCount: 60, slip: Bill), compressStreams: false);
        TextOf(german).Should().Contain("Kontoauszug, Fortsetzung").And.MatchRegex(@"Seite 1 von [2-9]");
        NothingRunsOffThePage(german);

        var french = StatementPdf.Render(Request("fr", lineCount: 60, closing: -50m), compressStreams: false);
        TextOf(french).Should().Contain("Relevé de compte, suite").And.MatchRegex(@"Page 1 sur [2-9]")
            .And.MatchRegex(@"Solde en votre faveur : CHF 50\.00");
        NothingRunsOffThePage(french);
    }

    [Fact]
    public void AnUnknownLanguageReadsEnglish_AsAStatementAlwaysDid()
    {
        var text = TextOf(StatementPdf.Render(Request("it", slip: Bill), compressStreams: false));

        text.Should().Contain("Statement").And.Contain("Amount due: CHF 299.00").And.Contain("1 Oct 2026").And.Contain("Payment part");
    }

    [Fact]
    public void AnInvoiceInFrench_NamesItsKindAndItsQrBillInFrench()
    {
        var pdf = ChargeInvoicePdf.Render(new ChargeInvoicePdfRequest(
            "Pavillon 46", "Jean Dupont", "jean@example.ch", "Gold",
            "2026-0042", "Cotisation Gold octobre", "Dues",
            new DateTime(2026, 10, 1, 6, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 31, 6, 0, 0, DateTimeKind.Utc),
            299m, 0m, false, 8.1m, "CHF", "fr-CH", "Europe/Zurich",
            new DateTime(2026, 10, 1, 7, 0, 0, DateTimeKind.Utc),
            Bill with { Message = "Facture 2026-0042" },
            Language: "fr"), compressStreams: false);
        var text = TextOf(pdf);

        text.Should().Contain("Facture").And.Contain("2026-0042").And.Contain("Cotisation");
        text.Should().Contain("Section paiement").And.Contain("Récépissé").And.Contain("Payez avec la QR-facture");
        text.Should().NotContain("Invoice").And.NotContain("Payment part");
        NothingRunsOffThePage(pdf);
    }
}
