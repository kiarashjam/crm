using System.Globalization;
using ACI.Application.Common.I18n;

namespace ACI.Application.Common.Pdf;

/// <summary>Everything an invoice for one charge says.</summary>
/// <param name="Number">The invoice's number, the charge's document number in the journal (C-1A2B3C4D).</param>
/// <param name="Amount">The charge's amount, VAT included.</param>
/// <param name="Paid">What has been paid on it so far.</param>
/// <param name="Voided">The charge was voided: nothing is owed on it.</param>
/// <param name="VatRate">The VAT rate in percent the amount includes, when known.</param>
/// <param name="GeneratedAtUtc">When it was made, printed on it; passed in so the same invoice gives the same bytes.</param>
/// <param name="PaymentSlip">The Swiss QR-bill for what is still owed, with the charge's reference; none prints no slip.</param>
/// <param name="IncurredBy">
/// Who in the household ran it up, when it is on the household primary's account because their
/// bills roll up; none for the account holder's own charge.
/// </param>
/// <param name="Language">The language the member reads ("en", "de", "fr"): the invoice's words, dates and QR-bill.</param>
public sealed record ChargeInvoicePdfRequest(
    string ClubName,
    string MemberName,
    string? MemberEmail,
    string? MemberTier,
    string Number,
    string Description,
    string Kind,
    DateTime PostedAtUtc,
    DateTime? DueAtUtc,
    decimal Amount,
    decimal Paid,
    bool Voided,
    decimal? VatRate,
    string Currency,
    string Locale,
    string TimeZone,
    DateTime GeneratedAtUtc,
    Payments.QrBill? PaymentSlip = null,
    string? IncurredBy = null,
    string Language = AppLanguages.English);

/// <summary>
/// Sets an invoice for one charge on an A4 page: what it was for, when it was posted and is
/// due, the VAT it includes, what has been paid, what is still owed, and the Swiss QR-bill
/// for that amount at the foot, whose reference names the charge so the bank's payment pays
/// it first.
/// </summary>
public static class ChargeInvoicePdf
{
    private static readonly PdfColor Ink = new(0.086, 0.106, 0.161);
    private static readonly PdfColor Muted = PdfColor.Grey(0.42);
    private static readonly PdfColor Hairline = PdfColor.Grey(0.855);
    private static readonly PdfColor Due = new(0.72, 0.11, 0.11);
    private static readonly PdfColor Settled = new(0.02, 0.51, 0.36);

    private const double PageW = PdfBuilder.A4WidthPt;
    private const double PageH = PdfBuilder.A4HeightPt;
    private const double Left = 50;
    private const double Right = PageW - 50;
    private const double DateRight = Right - 110;

    /// <summary>The amount still owed on a charge: nothing once voided or paid.</summary>
    public static decimal Owed(decimal amount, decimal paid, bool voided) => voided ? 0 : Math.Max(0, amount - paid);

    /// <summary>Renders the invoice. Tests pass <paramref name="compressStreams"/> false to read the text back.</summary>
    public static byte[] Render(ChargeInvoicePdfRequest r, bool compressStreams = true)
    {
        var culture = Culture(r.Locale);
        var zone = RegionalSettings.ZoneOf(r.TimeZone);
        var language = AppLanguages.Normalise(r.Language) ?? AppLanguages.English;
        string L(string key, params (string, object?)[] args) => MemberTexts.T(language, key, args);
        string Money(decimal amount) => $"{(amount < 0 ? "-" : "")}{r.Currency} {Math.Abs(amount).ToString("N2", culture)}";
        string Date(DateTime utc) => MemberTexts.Date(language, r.Locale,
            TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone));
        var owed = Owed(r.Amount, r.Paid, r.Voided);

        var pdf = new PdfBuilder
        {
            Title = $"{L("invoice.title")} {r.Number} – {r.MemberName}",
            Author = r.ClubName,
            Subject = L("invoice.title"),
            CreatedUtc = r.GeneratedAtUtc,
            CompressStreams = compressStreams,
        };
        var page = pdf.AddPage();

        // The letterhead: the club, the title and number, and whom it is for.
        page.Text(Left, 62, PdfFont.HelveticaBold, 11, StatementPdf.Fit(r.ClubName.ToUpperInvariant(), PdfFont.HelveticaBold, 11, 280), Muted, tracking: 1.2);
        page.Text(Left, 96, PdfFont.HelveticaBold, 24, L("invoice.title"), Ink);
        page.Text(Left, 116, PdfFont.Helvetica, 10.5, L("invoice.number", ("number", r.Number)), Ink);
        page.Text(Left, 131, PdfFont.Helvetica, 8.5, L("pdf.issued", ("date", Date(r.GeneratedAtUtc))), Muted);
        page.Text(Right, 96, PdfFont.HelveticaBold, 11, StatementPdf.Fit(r.MemberName, PdfFont.HelveticaBold, 11, 220), Ink, align: PdfAlign.Right);
        var y = 111.0;
        if (!string.IsNullOrWhiteSpace(r.MemberEmail))
        {
            page.Text(Right, y, PdfFont.Helvetica, 9, StatementPdf.Fit(r.MemberEmail, PdfFont.Helvetica, 9, 220), Muted, align: PdfAlign.Right);
            y += 13;
        }
        if (!string.IsNullOrWhiteSpace(r.MemberTier))
            page.Text(Right, y, PdfFont.Helvetica, 9, StatementPdf.Fit(L("pdf.tierMember", ("tier", r.MemberTier)), PdfFont.Helvetica, 9, 220), Muted, align: PdfAlign.Right);
        page.Rule(Left, 150, Right, 0.6, Hairline);

        // The one line it is for.
        y = 178;
        page.Text(Left, y, PdfFont.HelveticaBold, 7.5, L("pdf.colDescription").ToUpper(culture), Muted, tracking: 0.4);
        page.Text(DateRight, y, PdfFont.HelveticaBold, 7.5, L("invoice.colPosted").ToUpper(culture), Muted, tracking: 0.4, align: PdfAlign.Right);
        page.Text(Right, y, PdfFont.HelveticaBold, 7.5, L("invoice.colAmount").ToUpper(culture), Muted, tracking: 0.4, align: PdfAlign.Right);
        page.Rule(Left, y + 6, Right, 0.6, Hairline);
        y += 24;
        page.Text(Left, y, PdfFont.Helvetica, 10, StatementPdf.Fit(r.Description, PdfFont.Helvetica, 10, DateRight - 80 - Left), Ink);
        page.Text(DateRight, y, PdfFont.Helvetica, 9, Date(r.PostedAtUtc), Muted, align: PdfAlign.Right);
        page.Text(Right, y, PdfFont.Helvetica, 10, Money(r.Amount), Ink, align: PdfAlign.Right);
        // A household member's bill on the primary's account says whose it was, as the
        // statement's lines do; the invoice is addressed to the primary who pays it.
        var kind = MemberTexts.Kind(language, r.Kind);
        var detail = string.IsNullOrWhiteSpace(r.IncurredBy) ? kind : $"{L("line.incurredBy", ("name", r.IncurredBy.Trim()))} · {kind}";
        page.Text(Left, y + 12, PdfFont.HelveticaOblique, 7.5, StatementPdf.Fit(detail, PdfFont.HelveticaOblique, 7.5, DateRight - 80 - Left), Muted);
        y += 30;
        page.Rule(Left, y - 8, Right, 0.4, Hairline);

        if (r.VatRate is { } rate && rate > 0)
        {
            var included = Math.Round(r.Amount * rate / (100 + rate), 2, MidpointRounding.AwayFromZero);
            page.Text(Left, y + 6, PdfFont.Helvetica, 9, L("invoice.vatIncluded", ("rate", rate.ToString("0.0#", culture))), Muted);
            page.Text(Right, y + 6, PdfFont.Helvetica, 9, Money(included), Muted, align: PdfAlign.Right);
            y += 16;
        }
        if (r.Paid > 0 && !r.Voided)
        {
            page.Text(Left, y + 6, PdfFont.Helvetica, 9, L("invoice.paidSoFar"), Muted);
            page.Text(Right, y + 6, PdfFont.Helvetica, 9, Money(-r.Paid), Settled, align: PdfAlign.Right);
            y += 16;
        }
        y += 26;

        var (headline, colour) = r.Voided ? (L("invoice.cancelled"), Ink)
            : owed > 0 ? (L("pdf.amountDue", ("amount", Money(owed))), Due)
            : (L("invoice.paidInFull"), Settled);
        page.Text(Left, y, PdfFont.HelveticaBold, 14, headline, colour);
        y += 18;
        if (owed > 0 && r.DueAtUtc is { } due)
        {
            page.Text(Left, y, PdfFont.Helvetica, 9, L("invoice.payBy", ("date", Date(due))), Muted);
            y += 13;
        }
        // German and French run longer than the English these lines were laid out for: wrapped, not cut.
        if (r.PaymentSlip != null)
        {
            foreach (var line in StatementPdf.Lines(L("invoice.qrNote"), PdfFont.Helvetica, 9, Right - Left))
            {
                page.Text(Left, y, PdfFont.Helvetica, 9, line, Muted);
                y += 13;
            }
        }
        foreach (var line in StatementPdf.Lines(L("invoice.questions"), PdfFont.Helvetica, 9, Right - Left))
        {
            page.Text(Left, y + 6, PdfFont.Helvetica, 9, line, Muted);
            y += 13;
        }

        var footer = PageH - 50;
        if (r.PaymentSlip is { } slip)
        {
            QrBillPdf.Draw(page, slip, language);
            footer = PageH - QrBillPdf.HeightPt - 32;
        }
        page.Rule(Left, footer, Right, 0.4, Hairline);
        page.Text(Left, footer + 14, PdfFont.Helvetica, 7.5, StatementPdf.Fit(r.ClubName, PdfFont.Helvetica, 7.5, 300), Muted);
        page.Text(Right, footer + 14, PdfFont.Helvetica, 7.5, L("invoice.footer", ("number", r.Number)), Muted, align: PdfAlign.Right);
        return pdf.Build();
    }

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
