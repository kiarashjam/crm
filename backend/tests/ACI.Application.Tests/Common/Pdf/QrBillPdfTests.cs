using ACI.Application.Common.Payments;
using ACI.Application.Common.Pdf;

namespace ACI.Application.Tests.Common.Pdf;

/// <summary>
/// The QR-bill's payment part and receipt at the foot of a statement: where the guidelines put
/// them, what they say, and that a statement too long for it gives the slip a page of its own.
/// </summary>
public class QrBillPdfTests
{
    private const double PageW = PdfBuilder.A4WidthPt;
    private const double Mm = 72.0 / 25.4;

    private static readonly QrBill Bill = new(
        "CH4431999123000889012",
        new QrBillAddress("Pavillon 46", "Rue du Lac", "1268", "2501", "Biel", "CH"),
        1949.75m,
        "CHF",
        null,
        SwissQrBill.Qrr,
        "210000000003139471430009017",
        "Statement October 2026");

    private static StatementPdfRequest Request(QrBill? slip, int lineCount = 3) => new(
        "Pavillon 46", "Jean Dupont", "jean@example.ch", "Gold",
        new DateTime(2026, 9, 30, 22, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 31, 23, 0, 0, DateTimeKind.Utc),
        "CHF", "de-CH", "Europe/Zurich",
        0m, 1949.75m, 0m, 1949.75m,
        Enumerable.Range(1, lineCount).Select(i => new StatementPdfLine(
            new DateTime(2026, 10, 1, 6, 0, 0, DateTimeKind.Utc).AddHours(i), $"Bar tab {i}", null, 1m, 0m, i, null)).ToList(),
        new DateTime(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc),
        slip);

    [Fact]
    public void TheSlipSitsInTheBottom105MillimetresOfTheLastPage_WithItsHeadingsWhereTheGuidelinesPutThem()
    {
        var runs = PdfPageScanner.Scan(StatementPdf.Render(Request(Bill), compressStreams: false));
        var last = runs.Max(r => r.Page);

        last.Should().Be(1, "a short statement has room for the slip on its own page");
        var receipt = runs.Single(r => r.Text == "Receipt");
        var paymentPart = runs.Single(r => r.Text == "Payment part");
        receipt.X.Should().BeApproximately(5 * Mm, 0.5, "the receipt's title starts 5 mm in");
        paymentPart.X.Should().BeApproximately(67 * Mm, 0.5, "the payment part starts at 62 mm, its title 5 mm in");
        paymentPart.Y.Should().BeGreaterThan(95 * Mm).And.BeLessThan(105 * Mm, "the title is in the slip's top 10 mm");

        var text = string.Join("\n", runs.Select(r => r.Text));
        text.Should().Contain("Account / Payable to").And.Contain("CH44 3199 9123 0008 8901 2").And.Contain("Pavillon 46")
            .And.Contain("Rue du Lac 1268").And.Contain("2501 Biel");
        text.Should().Contain("Reference").And.Contain("21 00000 00003 13947 14300 09017");
        text.Should().Contain("Additional information").And.Contain("Statement October 2026");
        text.Should().Contain("Payable by (name/address)", "with no debtor the payer writes their own in");
        text.Should().Contain("1 949.75", "amounts are printed with a space between thousands");
        text.Should().Contain("Acceptance point").And.Contain("Separate before paying in");
        text.Should().Contain("Pay with the QR-bill at the foot of the statement");
        runs.Single(r => r.Text == "Page 1 of 1").Y.Should().BeGreaterThan(105 * Mm, "the footer moves above the slip");
        foreach (var run in runs)
        {
            run.X.Should().BeGreaterThanOrEqualTo(0);
            (run.X + run.Width).Should().BeLessThanOrEqualTo(PageW + 0.5, $"'{run.Text}' stays on the page");
        }
    }

    [Fact]
    public void AStatementTooLongForTheSlipGivesItAPageOfItsOwn()
    {
        var runs = PdfPageScanner.Scan(StatementPdf.Render(Request(Bill, lineCount: 50), compressStreams: false));
        var last = runs.Max(r => r.Page);

        runs.Should().Contain(r => r.Page == last && r.Text == "Payment slip");
        runs.Should().Contain(r => r.Page == last && r.Text == "Payment part");
        runs.Should().NotContain(r => r.Page < last && r.Text == "Payment part");
    }

    [Fact]
    public void NoSlipIsPrintedWithoutOne_AndABillThatDoesNotValidateIsNeverDrawn()
    {
        var text = string.Join("\n", PdfPageScanner.Scan(StatementPdf.Render(Request(null), compressStreams: false)).Select(r => r.Text));
        text.Should().NotContain("Payment part").And.NotContain("Pay with the QR-bill");

        var page = new PdfBuilder().AddPage();
        var act = () => QrBillPdf.Draw(page, Bill with { ReferenceType = SwissQrBill.Scor });
        act.Should().Throw<ArgumentException>("a QR-IBAN takes a QR reference, and a slip the bank refuses is worse than none");
    }

    [Fact]
    public void AmountsArePrintedAsTheGuidelinesWriteThem()
    {
        QrBillPdf.FormatAmount(1949.75m).Should().Be("1 949.75");
        QrBillPdf.FormatAmount(5m).Should().Be("5.00");
        QrBillPdf.FormatAmount(1234567.5m).Should().Be("1 234 567.50");
    }
}
