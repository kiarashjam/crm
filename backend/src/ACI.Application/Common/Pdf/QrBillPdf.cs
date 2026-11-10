using System.Globalization;
using System.Text;
using ACI.Application.Common.Payments;

namespace ACI.Application.Common.Pdf;

/// <summary>
/// Draws the payment part of a Swiss QR-bill (with its receipt) across the bottom 105 mm of an
/// A4 page, as the SIX Style Guide for the QR-bill lays it out: the receipt 62 mm wide on the
/// left, the payment part 148 mm wide with the 46 mm Swiss QR Code and its Swiss cross, the
/// headings and values in Helvetica at the sizes given there, and the lines to cut along with
/// "Separate before paying in" above them, as a slip printed from a PDF must have.
/// </summary>
/// <remarks>
/// The labels are English, as the statement above it is; the guidelines allow English beside
/// German, French and Italian. A bill that does not validate is never drawn (see
/// <see cref="SwissQrBill.Validate"/>): the caller leaves the slip off instead.
/// </remarks>
public static class QrBillPdf
{
    /// <summary>The slip's height: it fills the page's bottom 105 mm.</summary>
    public static readonly double HeightPt = Mm(105);

    private static readonly double ReceiptWidth = Mm(62);
    private static readonly double Margin = Mm(5);

    /// <summary>Draws the slip on <paramref name="page"/>, whose bottom 105 mm must be free.</summary>
    /// <exception cref="ArgumentException">The bill does not validate.</exception>
    public static void Draw(PdfCanvas page, QrBill bill)
    {
        var problems = SwissQrBill.Validate(bill);
        if (problems.Count > 0) throw new ArgumentException(string.Join(" ", problems), nameof(bill));

        var top = page.Height - HeightPt;
        var qr = QrCode.EncodeBytes(Encoding.UTF8.GetBytes(SwissQrBill.Payload(bill)), maxVersion: 25);

        // Where to cut: above the slip, and between the receipt and the payment part.
        page.Rule(0, top, page.Width, 0.5, PdfColor.Black);
        page.Line(ReceiptWidth, top, ReceiptWidth, page.Height, 0.5, PdfColor.Black);
        page.Text(page.Width / 2, top - 4, PdfFont.Helvetica, 7, "Separate before paying in", PdfColor.Black, align: PdfAlign.Center);

        DrawReceipt(page, bill, top);
        DrawPaymentPart(page, bill, qr, top);
    }

    private static void DrawReceipt(PdfCanvas page, QrBill bill, double top)
    {
        var x = Margin;
        var width = ReceiptWidth - 2 * Margin;
        page.Text(x, top + Margin + 8, PdfFont.HelveticaBold, 11, "Receipt", PdfColor.Black);

        var y = top + Mm(12) + 6;
        y = Section(page, x, y, width, "Account / Payable to", CreditorLines(bill), 6, 8, 9);
        if (bill.ReferenceType != SwissQrBill.Non)
            y = Section(page, x, y, width, "Reference", [PaymentReferences.Format(bill.Reference)], 6, 8, 9);
        if (bill.Debtor is { } debtor)
            Section(page, x, y, width, "Payable by", AddressLines(debtor), 6, 8, 9);
        else
        {
            page.Text(x, y, PdfFont.HelveticaBold, 6, "Payable by (name/address)", PdfColor.Black);
            CornerMarks(page, x, y + 3, Mm(52), Mm(20));
        }

        var amountTop = top + Mm(68);
        page.Text(x, amountTop + 6, PdfFont.HelveticaBold, 6, "Currency", PdfColor.Black);
        page.Text(x, amountTop + 6 + 11, PdfFont.Helvetica, 8, bill.Currency, PdfColor.Black);
        var amountX = x + Mm(12);
        page.Text(amountX, amountTop + 6, PdfFont.HelveticaBold, 6, "Amount", PdfColor.Black);
        if (bill.Amount is { } amount) page.Text(amountX, amountTop + 6 + 11, PdfFont.Helvetica, 8, FormatAmount(amount), PdfColor.Black);
        else CornerMarks(page, amountX + Mm(8), amountTop + 2, Mm(30), Mm(10));

        page.Text(ReceiptWidth - Margin, top + Mm(82) + 6, PdfFont.HelveticaBold, 6, "Acceptance point", PdfColor.Black, align: PdfAlign.Right);
    }

    private static void DrawPaymentPart(PdfCanvas page, QrBill bill, QrCode qr, double top)
    {
        var left = ReceiptWidth + Margin;
        page.Text(left, top + Margin + 8, PdfFont.HelveticaBold, 11, "Payment part", PdfColor.Black);

        // The Swiss QR Code: 46 mm square, 5 mm below the title section.
        var qrTop = top + Mm(17);
        var codeSize = Mm(46);
        var module = codeSize / qr.Size;
        for (var row = 0; row < qr.Size; row++)
        {
            var column = 0;
            while (column < qr.Size)
            {
                if (!qr.IsDark(row, column))
                {
                    column++;
                    continue;
                }
                var start = column;
                while (column < qr.Size && qr.IsDark(row, column)) column++;
                // A hair taller than a module, so no viewer shows a seam between rows.
                page.FilledRect(left + start * module, qrTop + row * module, (column - start) * module, module + 0.05, PdfColor.Black);
            }
        }
        SwissCross(page, left + codeSize / 2, qrTop + codeSize / 2);

        var amountTop = top + Mm(68);
        page.Text(left, amountTop + 8, PdfFont.HelveticaBold, 8, "Currency", PdfColor.Black);
        page.Text(left, amountTop + 8 + 13, PdfFont.Helvetica, 10, bill.Currency, PdfColor.Black);
        var amountX = left + Mm(14);
        page.Text(amountX, amountTop + 8, PdfFont.HelveticaBold, 8, "Amount", PdfColor.Black);
        if (bill.Amount is { } amount) page.Text(amountX, amountTop + 8 + 13, PdfFont.Helvetica, 10, FormatAmount(amount), PdfColor.Black);
        else CornerMarks(page, amountX, amountTop + 12, Mm(40), Mm(15));

        // The information section, right of the code.
        var x = ReceiptWidth + Mm(56);
        var width = page.Width - x - Margin;
        var y = top + Margin + 8;
        y = Section(page, x, y, width, "Account / Payable to", CreditorLines(bill), 8, 10, 11);
        if (bill.ReferenceType != SwissQrBill.Non)
            y = Section(page, x, y, width, "Reference", [PaymentReferences.Format(bill.Reference)], 8, 10, 11);
        var additional = new[] { SwissQrBill.Clean(bill.Message), SwissQrBill.Clean(bill.BillInformation) }.Where(t => t.Length > 0).ToList();
        if (additional.Count > 0)
            y = Section(page, x, y, width, "Additional information", additional, 8, 10, 11);
        if (bill.Debtor is { } debtor)
            Section(page, x, y, width, "Payable by", AddressLines(debtor), 8, 10, 11);
        else
        {
            page.Text(x, y, PdfFont.HelveticaBold, 8, "Payable by (name/address)", PdfColor.Black);
            CornerMarks(page, x, y + 4, Mm(65), Mm(25));
        }
    }

    /// <summary>A heading and its lines, each wrapped to the width. Returns where the next section starts.</summary>
    private static double Section(PdfCanvas page, double x, double y, double width, string heading, IEnumerable<string> lines,
        double headingSize, double valueSize, double leading)
    {
        page.Text(x, y, PdfFont.HelveticaBold, headingSize, heading, PdfColor.Black);
        y += leading;
        foreach (var line in lines)
            foreach (var part in Wrap(line, valueSize, width))
            {
                page.Text(x, y, PdfFont.Helvetica, valueSize, part, PdfColor.Black);
                y += leading;
            }
        return y + leading * 0.6;
    }

    private static IEnumerable<string> CreditorLines(QrBill bill) =>
        new[] { Iban.Format(bill.Account) }.Concat(AddressLines(bill.Creditor));

    /// <summary>The address as printed: the name, the street and number, the postal code and town (the country first unless Swiss).</summary>
    private static IEnumerable<string> AddressLines(QrBillAddress address)
    {
        yield return SwissQrBill.Clean(address.Name, 70);
        var street = $"{SwissQrBill.Clean(address.Street, 70)} {SwissQrBill.Clean(address.BuildingNumber, 16)}".Trim();
        if (street.Length > 0) yield return street;
        var country = SwissQrBill.Clean(address.Country, 2).ToUpperInvariant();
        var town = $"{SwissQrBill.Clean(address.PostalCode, 16)} {SwissQrBill.Clean(address.Town, 35)}".Trim();
        yield return country is "CH" or "LI" or "" ? town : $"{country}-{town}";
    }

    /// <summary>The words of <paramref name="text"/> in lines no wider than <paramref name="width"/>.</summary>
    private static IEnumerable<string> Wrap(string text, double size, double width)
    {
        var line = new StringBuilder();
        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = line.Length == 0 ? word : $"{line} {word}";
            if (line.Length > 0 && PdfFonts.Measure(PdfFont.Helvetica, candidate, size) > width)
            {
                yield return line.ToString();
                line.Clear().Append(word);
            }
            else
            {
                line.Clear().Append(candidate);
            }
        }
        if (line.Length > 0) yield return StatementPdf.Fit(line.ToString(), PdfFont.Helvetica, size, width);
    }

    /// <summary>The amount as the guidelines print it: a space between thousands and a full stop before the cents.</summary>
    public static string FormatAmount(decimal amount)
    {
        var text = amount.ToString("#,##0.00", CultureInfo.InvariantCulture);
        return text.Replace(',', ' ');
    }

    /// <summary>The corners of a box the payer writes in, as the guidelines draw it: 3 mm marks, no full outline.</summary>
    private static void CornerMarks(PdfCanvas page, double x, double y, double width, double height)
    {
        var mark = Mm(3);
        const double weight = 0.75;
        page.Line(x, y, x + mark, y, weight);
        page.Line(x, y, x, y + mark, weight);
        page.Line(x + width - mark, y, x + width, y, weight);
        page.Line(x + width, y, x + width, y + mark, weight);
        page.Line(x, y + height, x + mark, y + height, weight);
        page.Line(x, y + height - mark, x, y + height, weight);
        page.Line(x + width - mark, y + height, x + width, y + height, weight);
        page.Line(x + width, y + height - mark, x + width, y + height, weight);
    }

    /// <summary>
    /// The Swiss cross at the code's centre: 7 mm with its white margin, a black square and the
    /// white cross in the flag's proportions (arms 6 parts wide and 20 across, in 32).
    /// </summary>
    private static void SwissCross(PdfCanvas page, double centreX, double centreY)
    {
        var outer = Mm(7);
        var square = Mm(6);
        page.FilledRect(centreX - outer / 2, centreY - outer / 2, outer, outer, PdfColor.White);
        page.FilledRect(centreX - square / 2, centreY - square / 2, square, square, PdfColor.Black);
        var unit = square / 32;
        var armWidth = 6 * unit;
        var armLength = 20 * unit;
        page.FilledRect(centreX - armWidth / 2, centreY - armLength / 2, armWidth, armLength, PdfColor.White);
        page.FilledRect(centreX - armLength / 2, centreY - armWidth / 2, armLength, armWidth, PdfColor.White);
    }

    private static double Mm(double millimetres) => millimetres * 72.0 / 25.4;
}
