using System.Globalization;

namespace ACI.Application.Common.Pdf;

/// <summary>One entry on a statement, as the member reads it.</summary>
/// <param name="AtUtc">When it counts on the account.</param>
/// <param name="Description">What it was: "Gold dues · 1 Nov 2026 – 30 Nov 2026", "Payment (Wire)".</param>
/// <param name="Detail">A second, smaller line: "Void · posted twice", a reference; none for most.</param>
/// <param name="Charge">What it added to what is owed (never negative).</param>
/// <param name="Payment">What it took off what is owed (never negative).</param>
/// <param name="OwedAfter">What was owed after it: negative when the member was in credit.</param>
/// <param name="VatRate">The VAT rate in percent included in it, when known.</param>
public sealed record StatementPdfLine(
    DateTime AtUtc, string Description, string? Detail, decimal Charge, decimal Payment, decimal OwedAfter, decimal? VatRate);

/// <summary>One person on a household statement: what they ran up on the account in the period.</summary>
/// <param name="Role">Their place in the household: Primary, Spouse, Child and so on.</param>
/// <param name="Charged">What they ran up on this account in the period, voids taken off.</param>
/// <param name="OwnAccountOwed">What an account of their own owed at the end of the period, when it owes or holds anything; none otherwise.</param>
public sealed record StatementPdfHouseholdLine(string Name, string Role, decimal Charged, decimal? OwnAccountOwed);

/// <summary>Everything a statement says.</summary>
/// <param name="ClubName">Whose letterhead it is.</param>
/// <param name="FromUtc">The period's first moment.</param>
/// <param name="ToUtc">The moment after the period's last, as the ledger reads periods.</param>
/// <param name="Locale">How amounts are written (the workspace's number format), e.g. de-CH.</param>
/// <param name="TimeZone">The workspace's time zone: the days the dates on the statement fall on.</param>
/// <param name="OpeningOwed">Owed when the period began: negative when in credit.</param>
/// <param name="ClosingOwed">Owed when it ended: negative when in credit.</param>
/// <param name="GeneratedAtUtc">When it was made, printed on it; passed in so the same statement gives the same bytes.</param>
/// <param name="Title">The heading: "Statement", or "Household statement" for a household's primary.</param>
/// <param name="Household">On a household statement: each person and what they ran up in the period.</param>
/// <param name="HouseholdName">On a household statement: the household's name, under the primary's.</param>
public sealed record StatementPdfRequest(
    string ClubName,
    string MemberName,
    string? MemberEmail,
    string? MemberTier,
    DateTime FromUtc,
    DateTime ToUtc,
    string Currency,
    string Locale,
    string TimeZone,
    decimal OpeningOwed,
    decimal ChargesTotal,
    decimal PaymentsTotal,
    decimal ClosingOwed,
    IReadOnlyList<StatementPdfLine> Lines,
    DateTime GeneratedAtUtc,
    string Title = "Statement",
    IReadOnlyList<StatementPdfHouseholdLine>? Household = null,
    string? HouseholdName = null);

/// <summary>
/// Sets a member's statement as an A4 document: the period, the balance brought forward,
/// each entry with what was owed after it, the totals, the amount due and the VAT the
/// charges included.
/// </summary>
/// <remarks>
/// Built from the ledger's statement, which is worked out from the entries alone, so the
/// PDF says what the statement page says and what the account adds up to. It used to be
/// the browser's print of a page that added up today's charges and payments, which counted
/// a charge paid in a later month twice. Amounts are written in the club's number format;
/// dates in its time zone.
/// </remarks>
public static class StatementPdf
{
    private static readonly PdfColor Ink = new(0.086, 0.106, 0.161);
    private static readonly PdfColor Muted = PdfColor.Grey(0.42);
    private static readonly PdfColor Hairline = PdfColor.Grey(0.855);
    private static readonly PdfColor Panel = PdfColor.Grey(0.965);
    private static readonly PdfColor Due = new(0.72, 0.11, 0.11);
    private static readonly PdfColor Paid = new(0.02, 0.51, 0.36);

    private const double PageW = PdfBuilder.A4WidthPt;
    private const double PageH = PdfBuilder.A4HeightPt;
    private const double Left = 50;
    private const double Right = PageW - 50;
    private const double BodyBottom = PageH - 70;

    // The table's columns: the date and the description from the left, the amounts set
    // flush right against their column's edge.
    private const double DateX = Left;
    private const double DescriptionX = Left + 72;
    private const double ChargeRight = Right - 150;
    private const double PaymentRight = Right - 75;
    private const double OwedRight = Right;
    private const double DescriptionWidth = ChargeRight - 70 - DescriptionX;

    /// <summary>Renders the statement. Tests pass <paramref name="compressStreams"/> false to read the text back.</summary>
    public static byte[] Render(StatementPdfRequest r, bool compressStreams = true)
    {
        var culture = Culture(r.Locale);
        var zone = RegionalSettings.ZoneOf(r.TimeZone);
        string Money(decimal amount) => $"{(amount < 0 ? "-" : "")}{r.Currency} {Math.Abs(amount).ToString("N2", culture)}";
        string Date(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone)
            .ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        // The last day of the period is the day before the moment it ends.
        var period = $"{Date(r.FromUtc)} – {Date(r.ToUtc.AddTicks(-1))}";

        var pdf = new PdfBuilder
        {
            Title = $"{r.Title} {period} – {r.MemberName}",
            Author = r.ClubName,
            Subject = "Account statement",
            CreatedUtc = r.GeneratedAtUtc,
            CompressStreams = compressStreams,
        };

        var page = pdf.AddPage();
        var y = Letterhead(page, r, period, Date(r.GeneratedAtUtc));

        // The four figures a member looks for first.
        var boxes = new (string Label, decimal Value, PdfColor Colour)[]
        {
            ("Brought forward", r.OpeningOwed, Ink),
            ("Charges", r.ChargesTotal, Ink),
            ("Payments and credits", r.PaymentsTotal, Paid),
            ("Closing balance", r.ClosingOwed, r.ClosingOwed > 0 ? Due : Ink),
        };
        var boxWidth = (Right - Left - 3 * 10) / 4;
        for (var i = 0; i < boxes.Length; i++)
        {
            var x = Left + i * (boxWidth + 10);
            page.RoundedRect(x, y, boxWidth, 48, 6, fill: Panel);
            page.Text(x + 10, y + 17, PdfFont.Helvetica, 7.5, boxes[i].Label.ToUpperInvariant(), Muted, tracking: 0.4);
            page.Text(x + 10, y + 36, PdfFont.HelveticaBold, 12, Fit(Money(boxes[i].Value), PdfFont.HelveticaBold, 12, boxWidth - 20), boxes[i].Colour);
        }
        y += 72;

        y = TableHeader(page, y);
        y = Row(page, y, Date(r.FromUtc), "Balance brought forward", null, null, null, Money(r.OpeningOwed));
        foreach (var line in r.Lines)
        {
            var height = line.Detail == null ? 18 : 28;
            if (y + height > BodyBottom)
            {
                page = pdf.AddPage();
                // Whose statement a loose page belongs to, the name cut to fit rather than the words.
                page.Text(Left, 60, PdfFont.HelveticaBold, 10, "Statement, continued", Ink);
                page.Text(Right, 60, PdfFont.Helvetica, 9, Fit($"{r.MemberName} · {period}", PdfFont.Helvetica, 9, 300), Muted, align: PdfAlign.Right);
                y = TableHeader(page, 84);
            }
            y = Row(page, y, Date(line.AtUtc), line.Description, line.Detail,
                line.Charge > 0 ? Money(line.Charge) : null,
                line.Payment > 0 ? Money(-line.Payment) : null,
                Money(line.OwedAfter));
        }
        if (r.Lines.Count == 0)
            y = Row(page, y, "", "Nothing was charged or paid in this period.", null, null, null, null);

        if (y + 120 > BodyBottom)
        {
            page = pdf.AddPage();
            y = 70;
        }
        page.Rule(Left, y - 6, Right, 0.6, Hairline);
        page.Text(DescriptionX, y + 8, PdfFont.HelveticaBold, 9, "Totals for the period", Ink);
        page.Text(ChargeRight, y + 8, PdfFont.HelveticaBold, 9, Money(r.ChargesTotal), Ink, align: PdfAlign.Right);
        page.Text(PaymentRight, y + 8, PdfFont.HelveticaBold, 9, Money(-r.PaymentsTotal), Paid, align: PdfAlign.Right);
        y += 34;

        // What the member is to do about it.
        var (headline, colour) = r.ClosingOwed > 0
            ? ($"Amount due: {Money(r.ClosingOwed)}", Due)
            : r.ClosingOwed < 0
                ? ($"In credit: {Money(-r.ClosingOwed)}", Paid)
                : ("Nothing to pay", Ink);
        page.Text(Left, y, PdfFont.HelveticaBold, 14, headline, colour);
        y += 18;
        page.Text(Left, y, PdfFont.Helvetica, 9,
            r.ClosingOwed > 0 ? "This is what the account owed when the period ended. Payments made since are on the next statement."
            : "Questions about this statement? Reply to the email it came with, or ask the membership office.", Muted);
        y += 26;

        // The VAT the charges included, by rate, for those whose rate is known.
        var vat = r.Lines.Where(l => l.VatRate is > 0)
            .GroupBy(l => l.VatRate!.Value)
            .Select(g => (Rate: g.Key, Gross: g.Sum(l => l.Charge - l.Payment)))
            .Where(v => v.Gross != 0)
            .OrderBy(v => v.Rate)
            .ToList();
        if (vat.Count > 0)
        {
            page.Text(Left, y, PdfFont.HelveticaBold, 9, "VAT included in the charges above", Ink);
            y += 14;
            foreach (var (rate, gross) in vat)
            {
                var included = Math.Round(gross * rate / (100 + rate), 2, MidpointRounding.AwayFromZero);
                page.Text(Left, y, PdfFont.Helvetica, 9,
                    $"{rate.ToString("0.0#", CultureInfo.InvariantCulture)}% on {Money(gross)}: {Money(included)}", Muted);
                y += 13;
            }
        }

        // On a household statement: who ran up what, and what anyone billed on their own owes.
        if (r.Household is { Count: > 0 } household)
        {
            y += 12;
            if (y + 40 + household.Count * 15 > BodyBottom)
            {
                page = pdf.AddPage();
                y = 70;
            }
            page.Text(Left, y, PdfFont.HelveticaBold, 9, "Who ran up what in this period", Ink);
            y += 16;
            foreach (var person in household)
            {
                if (y + 15 > BodyBottom)
                {
                    page = pdf.AddPage();
                    y = 70;
                }
                page.Text(Left, y, PdfFont.Helvetica, 9, Fit($"{person.Name} · {person.Role}", PdfFont.Helvetica, 9, 250), Ink);
                page.Text(ChargeRight, y, PdfFont.Helvetica, 9, Money(person.Charged), Ink, align: PdfAlign.Right);
                if (person.OwnAccountOwed is { } own)
                    page.Text(OwedRight, y, PdfFont.Helvetica, 8, own > 0 ? $"Own account owes {Money(own)}" : $"Own account in credit {Money(-own)}",
                        own > 0 ? Due : Muted, align: PdfAlign.Right);
                y += 15;
            }
        }

        // Page numbers last, now that the number of pages is known.
        var pages = pdf.PageCount;
        for (var i = 0; i < pages; i++)
        {
            var p = PageAt(pdf, i);
            p.Rule(Left, PageH - 50, Right, 0.4, Hairline);
            p.Text(Left, PageH - 36, PdfFont.Helvetica, 7.5, Fit(r.ClubName, PdfFont.Helvetica, 7.5, 300), Muted);
            p.Text(Right, PageH - 36, PdfFont.Helvetica, 7.5, $"Page {i + 1} of {pages}", Muted, align: PdfAlign.Right);
        }
        return pdf.Build();
    }

    /// <summary>The club's name, the title, the period and who it is for. Returns where the body starts.</summary>
    private static double Letterhead(PdfCanvas page, StatementPdfRequest r, string period, string issued)
    {
        page.Text(Left, 62, PdfFont.HelveticaBold, 11, Fit(r.ClubName.ToUpperInvariant(), PdfFont.HelveticaBold, 11, 280), Muted, tracking: 1.2);
        page.Text(Left, 96, PdfFont.HelveticaBold, 24, r.Title, Ink);
        page.Text(Left, 116, PdfFont.Helvetica, 10.5, period, Ink);
        page.Text(Left, 131, PdfFont.Helvetica, 8.5, $"Issued {issued}", Muted);

        var x = Right;
        page.Text(x, 96, PdfFont.HelveticaBold, 11, Fit(r.MemberName, PdfFont.HelveticaBold, 11, 220), Ink, align: PdfAlign.Right);
        var y = 111.0;
        if (!string.IsNullOrWhiteSpace(r.MemberEmail))
        {
            page.Text(x, y, PdfFont.Helvetica, 9, Fit(r.MemberEmail, PdfFont.Helvetica, 9, 220), Muted, align: PdfAlign.Right);
            y += 13;
        }
        if (!string.IsNullOrWhiteSpace(r.MemberTier))
        {
            page.Text(x, y, PdfFont.Helvetica, 9, Fit($"{r.MemberTier} member", PdfFont.Helvetica, 9, 220), Muted, align: PdfAlign.Right);
            y += 13;
        }
        if (!string.IsNullOrWhiteSpace(r.HouseholdName))
            page.Text(x, y, PdfFont.Helvetica, 9, Fit($"For the {r.HouseholdName} household", PdfFont.Helvetica, 9, 220), Muted, align: PdfAlign.Right);

        page.Rule(Left, 150, Right, 0.6, Hairline);
        return 168;
    }

    private static double TableHeader(PdfCanvas page, double y)
    {
        page.Text(DateX, y + 10, PdfFont.HelveticaBold, 7.5, "DATE", Muted, tracking: 0.4);
        page.Text(DescriptionX, y + 10, PdfFont.HelveticaBold, 7.5, "DESCRIPTION", Muted, tracking: 0.4);
        page.Text(ChargeRight, y + 10, PdfFont.HelveticaBold, 7.5, "CHARGES", Muted, tracking: 0.4, align: PdfAlign.Right);
        page.Text(PaymentRight, y + 10, PdfFont.HelveticaBold, 7.5, "PAYMENTS", Muted, tracking: 0.4, align: PdfAlign.Right);
        page.Text(OwedRight, y + 10, PdfFont.HelveticaBold, 7.5, "OWED", Muted, tracking: 0.4, align: PdfAlign.Right);
        page.Rule(Left, y + 16, Right, 0.6, Hairline);
        return y + 32;
    }

    private static double Row(PdfCanvas page, double y, string date, string description, string? detail, string? charge, string? payment, string? owed)
    {
        page.Text(DateX, y, PdfFont.Helvetica, 9, date, Muted);
        page.Text(DescriptionX, y, PdfFont.Helvetica, 9, Fit(description, PdfFont.Helvetica, 9, DescriptionWidth), Ink);
        if (charge != null) page.Text(ChargeRight, y, PdfFont.Helvetica, 9, charge, Ink, align: PdfAlign.Right);
        if (payment != null) page.Text(PaymentRight, y, PdfFont.Helvetica, 9, payment, Paid, align: PdfAlign.Right);
        if (owed != null) page.Text(OwedRight, y, PdfFont.Helvetica, 9, owed, Ink, align: PdfAlign.Right);
        if (detail != null)
        {
            page.Text(DescriptionX, y + 11, PdfFont.HelveticaOblique, 7.5, Fit(detail, PdfFont.HelveticaOblique, 7.5, DescriptionWidth), Muted);
            return y + 28;
        }
        return y + 18;
    }

    /// <summary>The text, cut with an ellipsis where it would run past <paramref name="width"/> points.</summary>
    public static string Fit(string text, PdfFont font, double size, double width)
    {
        if (PdfFonts.Measure(font, text, size) <= width) return text;
        var cut = text;
        while (cut.Length > 0 && PdfFonts.Measure(font, cut + "…", size) > width) cut = cut[..^1];
        return cut.TrimEnd() + "…";
    }

    private static PdfCanvas PageAt(PdfBuilder pdf, int index) => pdf.Page(index);

    private static CultureInfo Culture(string locale)
    {
        try
        {
            return CultureInfo.GetCultureInfo(string.IsNullOrWhiteSpace(locale) ? RegionalSettings.DefaultLocale : locale);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo(RegionalSettings.DefaultLocale);
        }
    }

}
