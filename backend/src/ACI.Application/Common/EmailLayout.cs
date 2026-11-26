using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ACI.Application.Common;

/// <summary>
/// Which of the three designs an email wears.
/// </summary>
public enum EmailLook
{
    /// <summary>
    /// CadenceE's own: account, security, team and digest emails to the people who use the
    /// CRM. White card on a soft grey page, the CadenceE mark, an orange button.
    /// </summary>
    Product,

    /// <summary>
    /// A club's: what a workspace sends its members (statements, reminders, invitations,
    /// sign-in links). Warm paper, the club's name in letter-spaced capitals, serif titles, a
    /// deep green button: it reads as the club's, not as software's.
    /// </summary>
    Club,

    /// <summary>
    /// A person's: one-to-one emails a salesperson writes. No card and no banner, just
    /// clean type, so it reads like the email it is.
    /// </summary>
    Personal,
}

/// <summary>
/// An email described as parts (a title, paragraphs, a button, a note, a table of facts),
/// drawn by <see cref="EmailLayout"/> as both an HTML part and a plain-text part, so the
/// two always say the same thing.
/// </summary>
/// <remarks>
/// Composers build one of these instead of concatenating markup, which keeps every email
/// the system sends in one design, escaped in one place, and testable without a mail server.
/// </remarks>
public sealed class EmailDesign
{
    public EmailDesign(EmailLook look, string? brandName = null)
    {
        Look = look;
        BrandName = brandName;
    }

    public EmailLook Look { get; }

    /// <summary>For a club's email, the club's name in its header; ignored for CadenceE's own, which carry the CadenceE mark.</summary>
    public string? BrandName { get; }

    /// <summary>The line most inboxes show after the subject. Hidden in the email itself.</summary>
    public string? Preheader { get; set; }

    /// <summary>A small label above the title: "Password reset", "Weekly recap".</summary>
    public string? Eyebrow { get; set; }

    public string? Title { get; set; }

    /// <summary>The page's language, for screen readers and hyphenation (en, de, fr).</summary>
    public string Language { get; set; } = "en";

    /// <summary>Why the reader got it, in the footer: "You received this because…".</summary>
    public string? FooterNote { get; set; }

    internal List<Block> Blocks { get; } = new();

    /// <summary>A paragraph. Line breaks are kept and web addresses become links.</summary>
    public EmailDesign Paragraph(string? text, bool muted = false)
    {
        if (!string.IsNullOrWhiteSpace(text)) Blocks.Add(new Block(BlockKind.Paragraph, Text: text.Trim(), Muted: muted));
        return this;
    }

    /// <summary>Several paragraphs from text written with blank lines between them.</summary>
    public EmailDesign Paragraphs(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return this;
        foreach (var part in Regex.Split(text.Replace("\r\n", "\n").Trim(), @"\n[ \t]*\n+"))
            Paragraph(part);
        return this;
    }

    /// <summary>A heading inside the email, for a section of a digest.</summary>
    public EmailDesign Heading(string? text)
    {
        if (!string.IsNullOrWhiteSpace(text)) Blocks.Add(new Block(BlockKind.Heading, Text: text.Trim()));
        return this;
    }

    /// <summary>
    /// The email's action: a button, and under it the address written out for when the
    /// button does not work (a mail client that blocks it, a text-only reader).
    /// </summary>
    public EmailDesign Button(string label, string? url, bool showAddress = true)
    {
        if (!string.IsNullOrWhiteSpace(url)) Blocks.Add(new Block(BlockKind.Button, Text: label, Url: url.Trim(), Muted: !showAddress));
        return this;
    }

    /// <summary>A short note in a tinted box: how long a link works, what to do if this was not you.</summary>
    public EmailDesign Note(string? text, EmailTone tone = EmailTone.Neutral)
    {
        if (!string.IsNullOrWhiteSpace(text)) Blocks.Add(new Block(BlockKind.Note, Text: text.Trim(), Tone: tone));
        return this;
    }

    /// <summary>Label and value rows: an amount due, a date, a place.</summary>
    public EmailDesign Facts(IEnumerable<(string Label, string? Value)> rows)
    {
        var kept = rows.Where(r => !string.IsNullOrWhiteSpace(r.Value)).Select(r => (r.Label, r.Value!.Trim())).ToList();
        if (kept.Count > 0) Blocks.Add(new Block(BlockKind.Facts, Rows: kept));
        return this;
    }

    /// <summary>Figures side by side, large: the numbers a digest leads with.</summary>
    public EmailDesign Stats(IEnumerable<(string Label, string Value)> stats)
    {
        var kept = stats.Where(s => !string.IsNullOrWhiteSpace(s.Value)).ToList();
        if (kept.Count > 0) Blocks.Add(new Block(BlockKind.Stats, Rows: kept));
        return this;
    }

    /// <summary>
    /// A table of figures: a header row naming the columns, then a row per label with its
    /// figures right-aligned, the first column in bold and the others quieter beside it (this
    /// week beside the week before). A row can sit indented under the one above, as a part of it.
    /// </summary>
    /// <param name="columns">The figure columns' headings, left to right.</param>
    /// <param name="rows">Each row's label and its figures, one per column; missing ones are left blank.</param>
    /// <param name="labelColumn">A heading over the labels; none leaves that corner empty.</param>
    /// <remarks>
    /// Its cells are text, never links: a table often holds what visitors' browsers sent
    /// (a page, a source), which must not become something to click.
    /// </remarks>
    public EmailDesign Table(IEnumerable<string> columns, IEnumerable<EmailTableRow> rows, string? labelColumn = null)
    {
        var heads = columns.Select(c => (c ?? "").Trim()).ToList();
        var kept = rows.Where(r => !string.IsNullOrWhiteSpace(r.Label))
            .Select(r => new EmailTableRow(r.Label.Trim(),
                heads.Select((_, i) => i < r.Values.Count ? (r.Values[i] ?? "").Trim() : "").ToList(), r.Indented))
            .ToList();
        if (heads.Count > 0 && kept.Count > 0)
            Blocks.Add(new Block(BlockKind.Table, Text: string.IsNullOrWhiteSpace(labelColumn) ? null : labelColumn.Trim(), Items: heads, Table: kept));
        return this;
    }

    /// <summary>A bulleted list.</summary>
    public EmailDesign List(IEnumerable<string> items)
    {
        var kept = items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).ToList();
        if (kept.Count > 0) Blocks.Add(new Block(BlockKind.List, Items: kept));
        return this;
    }

    /// <summary>Links side by side, smaller than a button: "Open the reports · Notification settings".</summary>
    public EmailDesign Links(IEnumerable<(string Label, string Url)> links)
    {
        var kept = links.Where(l => !string.IsNullOrWhiteSpace(l.Url)).ToList();
        if (kept.Count > 0) Blocks.Add(new Block(BlockKind.Links, Rows: kept.Select(l => (l.Label, l.Url)).ToList()));
        return this;
    }

    /// <summary>A thin line between parts.</summary>
    public EmailDesign Divider()
    {
        Blocks.Add(new Block(BlockKind.Divider));
        return this;
    }

    /// <summary>Text set as it was written, in a box: the full text of a contract, say.</summary>
    public EmailDesign Preformatted(string? text)
    {
        if (!string.IsNullOrWhiteSpace(text)) Blocks.Add(new Block(BlockKind.Pre, Text: text.TrimEnd()));
        return this;
    }

    /// <summary>Closing lines: "Best regards," and a name.</summary>
    public EmailDesign SignOff(params string?[] lines)
    {
        var kept = lines.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l!.Trim()).ToList();
        if (kept.Count > 0) Blocks.Add(new Block(BlockKind.SignOff, Items: kept));
        return this;
    }

    internal enum BlockKind { Paragraph, Heading, Button, Note, Facts, Stats, List, Links, Divider, Pre, SignOff, Table }

    internal sealed record Block(
        BlockKind Kind,
        string? Text = null,
        string? Url = null,
        bool Muted = false,
        EmailTone Tone = EmailTone.Neutral,
        List<(string Label, string Value)>? Rows = null,
        List<string>? Items = null,
        List<EmailTableRow>? Table = null);
}

/// <summary>The colour of a note: plain, good news, or a warning.</summary>
public enum EmailTone { Neutral, Success, Warning }

/// <summary>One row of an <see cref="EmailDesign.Table"/>: its label and its figures, left to right.</summary>
/// <param name="Indented">Set under the row above, as a part of it (a lead source under "New leads").</param>
public sealed record EmailTableRow(string Label, IReadOnlyList<string> Values, bool Indented = false);

/// <summary>
/// Draws an <see cref="EmailDesign"/> as email HTML and as plain text.
/// </summary>
/// <remarks>
/// <para>
/// Email HTML is its own medium: Outlook on Windows lays it out with Word, Gmail drops
/// most of what is in a style block, and phones show it narrow. So: one centred column,
/// tables for layout, every style inline, a button made of a table cell (so Outlook draws
/// its colour), web-safe fonts with good fallbacks, and the only style block holding what is
/// a bonus where it works (dark mode, phone widths). Nothing depends on images: the
/// CadenceE mark is a coloured cell with a sparkle, so it shows even with images blocked.
/// </para>
/// <para>
/// Everything that comes from a record goes through <see cref="E"/>; nothing is inserted raw.
/// </para>
/// </remarks>
public static class EmailLayout
{
    private sealed record Palette(
        string Page, string Card, string Border, string Title, string Body, string Muted, string Accent, string AccentText,
        string Link, string Soft, string SoftBorder, string Rule, string HeadingFont, string BodyFont, int Radius);

    private const string Sans = "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Helvetica,Arial,sans-serif";
    private const string Serif = "'Cormorant Garamond',Georgia,'Times New Roman',Times,serif";
    private const string Mono = "ui-monospace,SFMono-Regular,Menlo,Consolas,monospace";

    private static readonly Palette ProductPalette = new(
        Page: "#f4f4f5", Card: "#ffffff", Border: "#e4e4e7", Title: "#18181b", Body: "#3f3f46", Muted: "#71717a",
        Accent: "#ea580c", AccentText: "#ffffff", Link: "#c2410c", Soft: "#fafafa", SoftBorder: "#ececef", Rule: "#ececef",
        HeadingFont: Sans, BodyFont: Sans, Radius: 16);

    private static readonly Palette ClubPalette = new(
        Page: "#ece7e0", Card: "#fbf9f6", Border: "#e3ddd4", Title: "#1f3a30", Body: "#3b4741", Muted: "#7a857f",
        Accent: "#2c5545", AccentText: "#ffffff", Link: "#2c5545", Soft: "#f3efe9", SoftBorder: "#e6e0d6", Rule: "#e6e0d6",
        HeadingFont: Serif, BodyFont: Sans, Radius: 4);

    /// <summary>The design as HTML and plain text, with the subject in the HTML's title.</summary>
    public static (string Html, string Text) Render(EmailDesign design, string subject) =>
        (Html(design, subject), Text(design));

    /// <summary>The design as one composed message.</summary>
    public static EmailContent Compose(EmailDesign design, string subject) =>
        new(subject, Text(design), Html(design, subject));

    // ── HTML ─────────────────────────────────────────────────────────────────

    public static string Html(EmailDesign d, string subject)
    {
        if (d.Look == EmailLook.Personal) return PersonalHtml(d, subject);
        var p = d.Look == EmailLook.Club ? ClubPalette : ProductPalette;
        var club = d.Look == EmailLook.Club;
        var b = new StringBuilder(8192);
        b.Append("<!DOCTYPE html><html lang=\"").Append(E(d.Language)).Append("\" xmlns=\"http://www.w3.org/1999/xhtml\"><head>")
            .Append("<meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">")
            .Append("<meta name=\"x-apple-disable-message-reformatting\"><meta name=\"color-scheme\" content=\"light dark\">")
            .Append("<meta name=\"supported-color-schemes\" content=\"light dark\"><title>").Append(E(subject)).Append("</title>")
            .Append(Style(club)).Append("</head>")
            .Append("<body class=\"em-page\" style=\"margin:0;padding:0;width:100%;background-color:").Append(p.Page)
            .Append(";-webkit-text-size-adjust:100%;-ms-text-size-adjust:100%;\">");

        // What the inbox shows after the subject; padded so the body does not run on after it.
        if (!string.IsNullOrWhiteSpace(d.Preheader))
            b.Append("<div style=\"display:none;font-size:1px;line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;mso-hide:all;\">")
                .Append(E(d.Preheader)).Append(string.Concat(Enumerable.Repeat("&#847;&zwnj;&nbsp;", 40))).Append("</div>");

        b.Append("<table role=\"presentation\" class=\"em-page\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"background-color:")
            .Append(p.Page).Append(";\"><tr><td align=\"center\" class=\"em-outer\" style=\"padding:32px 16px;\">")
            .Append("<table role=\"presentation\" class=\"em-wrap\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"width:100%;max-width:600px;\">");

        // Header: the CadenceE mark, or the club's name.
        b.Append("<tr><td align=\"").Append(club ? "center" : "left").Append("\" style=\"padding:0 8px 18px;\">")
            .Append(club ? ClubMark(d.BrandName, p) : ProductMark(p)).Append("</td></tr>");

        // The card.
        b.Append("<tr><td class=\"em-card\" style=\"background-color:").Append(p.Card).Append(";border:1px solid ").Append(p.Border)
            .Append(";border-radius:").Append(p.Radius).Append("px;").Append(club ? "border-top:3px solid " + p.Accent + ";" : "")
            .Append("\"><table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\">")
            .Append("<tr><td class=\"em-pad\" style=\"padding:40px 44px 36px;\">");

        if (!string.IsNullOrWhiteSpace(d.Eyebrow))
            b.Append("<p class=\"em-eyebrow\" style=\"margin:0 0 10px;font-family:").Append(Sans)
                .Append(";font-size:12px;line-height:16px;font-weight:600;letter-spacing:").Append(club ? "2.4px" : "0.6px")
                .Append(";text-transform:uppercase;color:").Append(club ? p.Accent : p.Link).Append(";\">").Append(E(d.Eyebrow)).Append("</p>");
        if (!string.IsNullOrWhiteSpace(d.Title))
            b.Append("<h1 class=\"em-title\" style=\"margin:0 0 20px;font-family:").Append(p.HeadingFont).Append(";font-size:")
                .Append(club ? "32px;line-height:38px;font-weight:600;" : "26px;line-height:33px;font-weight:700;letter-spacing:-0.4px;")
                .Append("color:").Append(p.Title).Append(";\">").Append(E(d.Title)).Append("</h1>");

        foreach (var block in d.Blocks) AppendBlock(b, block, p, d.Language);

        b.Append("</td></tr></table></td></tr>");

        // Footer, under the card.
        b.Append("<tr><td align=\"center\" style=\"padding:22px 24px 0;font-family:").Append(Sans)
            .Append(";font-size:12px;line-height:19px;color:").Append(p.Muted).Append(";\" class=\"em-muted\">");
        if (!string.IsNullOrWhiteSpace(d.FooterNote))
            b.Append("<p style=\"margin:0 0 8px;\">").Append(Inline(d.FooterNote, p)).Append("</p>");
        b.Append("<p style=\"margin:0;\">").Append(club
            ? E(string.IsNullOrWhiteSpace(d.BrandName) ? "" : d.BrandName.Trim())
            : "CadenceE &middot; <a href=\"https://www.cadencee.ai\" style=\"color:" + p.Muted + ";text-decoration:underline;\">cadencee.ai</a>")
            .Append("</p></td></tr>");

        b.Append("</table></td></tr></table></body></html>");
        return b.ToString();
    }

    private static string ProductMark(Palette p) =>
        "<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr>" +
        "<td width=\"30\" height=\"30\" align=\"center\" valign=\"middle\" bgcolor=\"#f97316\" style=\"width:30px;height:30px;border-radius:9px;" +
        "background-color:#f97316;background-image:linear-gradient(135deg,#ea580c,#fb923c);color:#ffffff;font-family:" + Sans +
        ";font-size:17px;line-height:30px;font-weight:700;mso-line-height-rule:exactly;\">&#10022;</td>" +
        "<td style=\"padding-left:10px;font-family:" + Sans + ";font-size:18px;line-height:30px;font-weight:700;letter-spacing:-0.3px;color:" +
        p.Title + ";\" class=\"em-title\">Cadence<span style=\"color:#ea580c;\">E</span></td></tr></table>";

    private static string ClubMark(string? name, Palette p) =>
        string.IsNullOrWhiteSpace(name)
            ? ""
            : "<p style=\"margin:0;font-family:" + Serif + ";font-size:15px;line-height:20px;font-weight:600;letter-spacing:4px;" +
              "text-transform:uppercase;color:" + p.Title + ";\" class=\"em-title\">" + E(name.Trim()) + "</p>";

    private static void AppendBlock(StringBuilder b, EmailDesign.Block block, Palette p, string language)
    {
        var body = $"font-family:{p.BodyFont};font-size:16px;line-height:26px;color:{p.Body};";
        switch (block.Kind)
        {
            case EmailDesign.BlockKind.Paragraph:
                b.Append("<p class=\"").Append(block.Muted ? "em-muted" : "em-body").Append("\" style=\"margin:0 0 16px;").Append(body)
                    .Append(block.Muted ? $"font-size:14px;line-height:22px;color:{p.Muted};" : "").Append("\">")
                    .Append(Inline(block.Text, p)).Append("</p>");
                break;
            case EmailDesign.BlockKind.Heading:
                b.Append("<h2 class=\"em-title\" style=\"margin:28px 0 10px;font-family:").Append(p.HeadingFont)
                    .Append(p.HeadingFont == Serif ? ";font-size:22px;line-height:28px;font-weight:600;" : ";font-size:17px;line-height:24px;font-weight:700;")
                    .Append("color:").Append(p.Title).Append(";\">").Append(E(block.Text)).Append("</h2>");
                break;
            case EmailDesign.BlockKind.Button:
                b.Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:28px 0 ")
                    .Append(block.Muted ? "28px" : "14px").Append(";\" class=\"em-btn-wrap\"><tr><td align=\"center\" bgcolor=\"").Append(p.Accent)
                    .Append("\" style=\"border-radius:").Append(p.Radius == 4 ? 3 : 10).Append("px;background-color:").Append(p.Accent).Append(";\">")
                    .Append("<a href=\"").Append(E(block.Url)).Append("\" target=\"_blank\" class=\"em-btn\" style=\"display:inline-block;padding:15px 30px;font-family:")
                    .Append(Sans).Append(";font-size:16px;line-height:20px;font-weight:600;color:").Append(p.AccentText)
                    .Append(";text-decoration:none;border-radius:").Append(p.Radius == 4 ? 3 : 10).Append("px;")
                    .Append(p.Radius == 4 ? "letter-spacing:0.4px;" : "").Append("\">").Append(E(block.Text)).Append("</a></td></tr></table>");
                if (!block.Muted)
                    b.Append("<p class=\"em-muted\" style=\"margin:0 0 24px;font-family:").Append(Sans).Append(";font-size:13px;line-height:20px;color:")
                        .Append(p.Muted).Append(";\">").Append(E(LinkHint(language))).Append("<br><a href=\"").Append(E(block.Url))
                        .Append("\" class=\"em-link\" style=\"color:").Append(p.Link).Append(";text-decoration:underline;word-break:break-all;\">").Append(E(block.Url)).Append("</a></p>");
                break;
            case EmailDesign.BlockKind.Note:
                var (bg, edge, ink) = block.Tone switch
                {
                    EmailTone.Success => ("#ecfdf5", "#10b981", "#065f46"),
                    EmailTone.Warning => ("#fff7ed", "#f59e0b", "#7c2d12"),
                    _ => (p.Soft, p.SoftBorder, p.Body),
                };
                b.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:4px 0 20px;\"><tr>")
                    .Append("<td class=\"em-note").Append(block.Tone switch { EmailTone.Success => " em-ok", EmailTone.Warning => " em-warn", _ => "" })
                    .Append("\" style=\"padding:14px 18px;background-color:").Append(bg).Append(";border:1px solid ").Append(edge == p.SoftBorder ? edge : bg)
                    .Append(";border-left:3px solid ").Append(edge).Append(";border-radius:").Append(p.Radius == 4 ? 2 : 10).Append("px;font-family:").Append(Sans)
                    .Append(";font-size:14px;line-height:22px;color:").Append(ink).Append(";word-break:break-word;overflow-wrap:anywhere;\">").Append(Inline(block.Text, p)).Append("</td></tr></table>");
                break;
            case EmailDesign.BlockKind.Facts:
                b.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" class=\"em-facts\" style=\"margin:4px 0 22px;border-collapse:separate;background-color:")
                    .Append(p.Soft).Append(";border:1px solid ").Append(p.SoftBorder).Append(";border-radius:").Append(p.Radius == 4 ? 2 : 12).Append("px;\">");
                for (var i = 0; i < block.Rows!.Count; i++)
                {
                    var (label, value) = block.Rows[i];
                    var line = i == 0 ? "" : $"border-top:1px solid {p.SoftBorder};";
                    b.Append("<tr><td class=\"em-muted\" style=\"padding:12px 18px;").Append(line).Append("font-family:").Append(Sans)
                        .Append(";font-size:14px;line-height:20px;color:").Append(p.Muted).Append(";width:40%;vertical-align:top;\">").Append(E(label)).Append("</td>")
                        .Append("<td class=\"em-title\" style=\"padding:12px 18px;").Append(line).Append("font-family:").Append(Sans)
                        .Append(";font-size:14px;line-height:20px;font-weight:600;color:").Append(p.Title).Append(";text-align:right;vertical-align:top;\">")
                        .Append(Inline(value, p)).Append("</td></tr>");
                }
                b.Append("</table>");
                break;
            case EmailDesign.BlockKind.Stats:
                b.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:4px 0 22px;\"><tr>");
                var stats = block.Rows!;
                var width = 100 / Math.Min(stats.Count, 3);
                for (var i = 0; i < stats.Count; i++)
                {
                    if (i > 0 && i % 3 == 0) b.Append("</tr><tr>");
                    b.Append("<td class=\"em-stat\" width=\"").Append(width).Append("%\" style=\"padding:").Append(i % 3 == 0 ? "0 6px 12px 0" : "0 0 12px 6px")
                        .Append(";vertical-align:top;\"><table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr>")
                        .Append("<td class=\"em-note\" style=\"padding:16px 16px 14px;background-color:").Append(p.Soft).Append(";border:1px solid ").Append(p.SoftBorder)
                        .Append(";border-radius:").Append(p.Radius == 4 ? 2 : 12).Append("px;\">")
                        .Append("<p class=\"em-title\" style=\"margin:0;font-family:").Append(p.HeadingFont).Append(";font-size:26px;line-height:30px;font-weight:700;color:")
                        .Append(p.Title).Append(";\">").Append(E(stats[i].Value)).Append("</p>")
                        .Append("<p class=\"em-muted\" style=\"margin:4px 0 0;font-family:").Append(Sans).Append(";font-size:13px;line-height:18px;color:")
                        .Append(p.Muted).Append(";\">").Append(E(stats[i].Label)).Append("</p></td></tr></table></td>");
                }
                b.Append("</tr></table>");
                break;
            case EmailDesign.BlockKind.List:
                b.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:0 0 18px;\">");
                foreach (var item in block.Items!)
                    b.Append("<tr><td width=\"18\" style=\"width:18px;vertical-align:top;font-family:").Append(Sans).Append(";font-size:16px;line-height:26px;color:")
                        .Append(p.Accent).Append(";\">&bull;</td><td class=\"em-body\" style=\"padding:0 0 6px;").Append(body).Append("\">")
                        .Append(Inline(item, p)).Append("</td></tr>");
                b.Append("</table>");
                break;
            case EmailDesign.BlockKind.Links:
                b.Append("<p style=\"margin:8px 0 18px;font-family:").Append(Sans).Append(";font-size:14px;line-height:22px;\">");
                for (var i = 0; i < block.Rows!.Count; i++)
                {
                    if (i > 0) b.Append("<span class=\"em-muted\" style=\"color:").Append(p.Muted).Append(";\">&nbsp;&nbsp;&middot;&nbsp;&nbsp;</span>");
                    b.Append("<a href=\"").Append(E(block.Rows[i].Value)).Append("\" class=\"em-link\" style=\"color:").Append(p.Link)
                        .Append(";font-weight:600;text-decoration:none;\">").Append(E(block.Rows[i].Label)).Append("&nbsp;&rarr;</a>");
                }
                b.Append("</p>");
                break;
            case EmailDesign.BlockKind.Divider:
                b.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:8px 0 24px;\"><tr>")
                    .Append("<td height=\"1\" style=\"height:1px;font-size:1px;line-height:1px;background-color:").Append(p.Rule).Append(";\">&nbsp;</td></tr></table>");
                break;
            case EmailDesign.BlockKind.Pre:
                b.Append("<div class=\"em-note\" style=\"margin:0 0 20px;padding:16px 18px;white-space:pre-wrap;word-wrap:break-word;word-break:break-word;overflow-wrap:anywhere;font-family:").Append(Mono)
                    .Append(";font-size:13px;line-height:21px;color:").Append(p.Body).Append(";background-color:").Append(p.Soft).Append(";border:1px solid ")
                    .Append(p.SoftBorder).Append(";border-radius:").Append(p.Radius == 4 ? 2 : 10).Append("px;\">").Append(E(block.Text)).Append("</div>");
                break;
            case EmailDesign.BlockKind.SignOff:
                b.Append("<p class=\"em-body\" style=\"margin:24px 0 0;").Append(body).Append("\">")
                    .Append(string.Join("<br>", block.Items!.Select(E))).Append("</p>");
                break;
            case EmailDesign.BlockKind.Table:
                AppendTable(b, block, p);
                break;
        }
    }

    /// <summary>
    /// A table of figures, styled as the facts are: a tinted box, a quiet header row in small
    /// capitals, the labels on the left and the figures right-aligned in even digits, the first
    /// column bold. A real table (header cells with their scope), so a screen reader reads each
    /// figure with its column.
    /// </summary>
    private static void AppendTable(StringBuilder b, EmailDesign.Block block, Palette p)
    {
        var heads = block.Items!;
        var rows = block.Table!;
        var radius = p.Radius == 4 ? 2 : 12;
        var head = $"padding:12px 18px 9px 10px;font-family:{Sans};font-size:11px;line-height:15px;font-weight:600;letter-spacing:0.6px;" +
                   $"text-transform:uppercase;color:{p.Muted};border-bottom:1px solid {p.SoftBorder};vertical-align:bottom;";
        b.Append("<table class=\"em-facts em-table\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin:4px 0 22px;")
            .Append("border-collapse:separate;border-spacing:0;background-color:").Append(p.Soft).Append(";border:1px solid ").Append(p.SoftBorder)
            .Append(";border-radius:").Append(radius).Append("px;\"><thead><tr>")
            .Append("<th scope=\"col\" align=\"left\" class=\"em-muted em-tl\" style=\"").Append(head).Append("padding-left:18px;text-align:left;\">")
            .Append(E(block.Text)).Append("</th>");
        for (var c = 0; c < heads.Count; c++)
            b.Append("<th scope=\"col\" align=\"right\" class=\"em-muted").Append(c == heads.Count - 1 ? " em-tr" : "").Append("\" style=\"").Append(head)
                .Append("text-align:right;\">").Append(E(heads[c])).Append("</th>");
        b.Append("</tr></thead><tbody>");
        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var line = r == 0 ? "" : $"border-top:1px solid {p.SoftBorder};";
            var cell = $"padding:11px 18px 11px 10px;{line}font-family:{Sans};vertical-align:top;";
            b.Append("<tr><td class=\"").Append(row.Indented ? "em-muted em-tl em-indent" : "em-body em-tl").Append("\" style=\"").Append(cell)
                .Append(row.Indented
                    ? $"padding-left:34px;font-size:13px;line-height:20px;color:{p.Muted};"
                    : $"padding-left:18px;font-size:14px;line-height:20px;color:{p.Body};")
                // A long label with no space in it (a page's path) wraps rather than widen the email.
                .Append("text-align:left;word-break:break-word;\">").Append(E(row.Label)).Append("</td>");
            for (var c = 0; c < heads.Count; c++)
            {
                // The first figure in bold; a part's (an indented row's) in plain type, under its whole.
                var (ink, weight) = c > 0 ? ("em-muted", $"color:{p.Muted};")
                    : row.Indented ? ("em-body", $"color:{p.Body};")
                    : ("em-title", $"font-weight:600;color:{p.Title};");
                b.Append("<td align=\"right\" class=\"").Append(ink).Append(c == heads.Count - 1 ? " em-tr" : "")
                    .Append("\" style=\"").Append(cell).Append(row.Indented ? "font-size:13px;" : "font-size:14px;")
                    .Append("line-height:20px;text-align:right;white-space:nowrap;font-variant-numeric:tabular-nums;")
                    .Append(weight).Append("\">").Append(E(row.Values[c])).Append("</td>");
            }
            b.Append("</tr>");
        }
        b.Append("</tbody></table>");
    }

    /// <summary>The words under a button, before the address written out, in the email's language.</summary>
    private static string LinkHint(string? language) => (language ?? "").Trim().ToLowerInvariant() switch
    {
        "de" => "Falls die Schaltfläche nicht funktioniert, kopieren Sie diese Adresse in Ihren Browser:",
        "fr" => "Si le bouton ne fonctionne pas, copiez cette adresse dans votre navigateur\u00a0:",
        _ => "If the button does not work, copy this address into your browser:",
    };

    /// <summary>
    /// A personal email: no card, no banner, the writer's words in clean type. Used for the
    /// emails a salesperson writes, which should look like the emails they are.
    /// </summary>
    private static string PersonalHtml(EmailDesign d, string subject)
    {
        var b = new StringBuilder();
        b.Append("<!DOCTYPE html><html lang=\"").Append(E(d.Language)).Append("\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>").Append(E(subject)).Append("</title></head>")
            .Append("<body style=\"margin:0;padding:0;\"><div style=\"max-width:640px;padding:8px 4px;font-family:").Append(Sans)
            .Append(";font-size:15px;line-height:24px;color:#1f2937;\">");
        foreach (var block in d.Blocks)
        {
            switch (block.Kind)
            {
                case EmailDesign.BlockKind.Button:
                    b.Append("<p style=\"margin:0 0 14px;\"><a href=\"").Append(E(block.Url)).Append("\" style=\"color:#1d4ed8;font-weight:600;\">")
                        .Append(E(block.Text)).Append("</a></p>");
                    break;
                case EmailDesign.BlockKind.SignOff:
                    b.Append("<p style=\"margin:18px 0 0;\">").Append(string.Join("<br>", block.Items!.Select(E))).Append("</p>");
                    break;
                case EmailDesign.BlockKind.Table:
                    // No boxes in a person's email: the table's lines, as its text part has them.
                    var lines = new StringBuilder();
                    TableText(lines, block, d.Language);
                    b.Append("<p style=\"margin:0 0 14px;\">").Append(E(lines.ToString().TrimEnd()).Replace("\n", "<br>")).Append("</p>");
                    break;
                default:
                    if (block.Text != null) b.Append("<p style=\"margin:0 0 14px;\">").Append(Inline(block.Text, PersonalLink)).Append("</p>");
                    break;
            }
        }
        b.Append("</div></body></html>");
        return b.ToString();
    }

    private static readonly Palette PersonalLink = ProductPalette with { Link = "#1d4ed8" };

    /// <summary>
    /// The only style block: what helps where it is read (dark mode in Apple Mail and Outlook
    /// for Mac, phone widths) and costs nothing where it is not.
    /// </summary>
    private static string Style(bool club)
    {
        var dark = club
            ? ".em-page{background-color:#141816 !important}.em-card{background-color:#1c211e !important;border-color:#2b332f !important}" +
              ".em-title{color:#eef2ef !important}.em-body{color:#cfd7d2 !important}.em-muted{color:#9aa69f !important}" +
              ".em-note{background-color:#232a26 !important;border-color:#313a35 !important;color:#cfd7d2 !important}.em-facts{background-color:#232a26 !important;border-color:#313a35 !important}.em-facts td,.em-table th{border-color:#313a35 !important}" +
              ".em-eyebrow,.em-link{color:#8cc7ad !important}"
            : ".em-page{background-color:#0b0b0e !important}.em-card{background-color:#18181b !important;border-color:#27272a !important}" +
              ".em-title{color:#fafafa !important}.em-body{color:#d4d4d8 !important}.em-muted{color:#a1a1aa !important}" +
              ".em-note{background-color:#202024 !important;border-color:#2e2e33 !important;color:#d4d4d8 !important}.em-facts{background-color:#202024 !important;border-color:#2e2e33 !important}.em-facts td,.em-table th{border-color:#2e2e33 !important}" +
              ".em-eyebrow,.em-link{color:#fb923c !important}";
        // A good-news or warning note keeps its colour in the dark, on a dark tint of it.
        dark += ".em-ok{background-color:#10251c !important;border-color:#10251c !important;border-left-color:#10b981 !important;color:#a7f3d0 !important}" +
                ".em-warn{background-color:#2a1d0e !important;border-color:#2a1d0e !important;border-left-color:#f59e0b !important;color:#fde2b0 !important}";
        return "<style>" +
               ":root{color-scheme:light dark;supported-color-schemes:light dark}" +
               "body{margin:0 !important;padding:0 !important}table,td{mso-table-lspace:0pt;mso-table-rspace:0pt}" +
               "a[x-apple-data-detectors]{color:inherit !important;text-decoration:none !important}" +
               "@media (prefers-color-scheme:dark){" + dark + "}" +
               "@media screen and (max-width:620px){.em-outer{padding:20px 10px !important}.em-pad{padding:30px 22px 26px !important}" +
               ".em-title{word-break:break-word}h1.em-title{font-size:24px !important;line-height:30px !important}" +
               ".em-stat{display:block !important;width:100% !important;padding:0 0 10px 0 !important}" +
               // A table keeps to the screen: tighter edges, and a long figure may break at a space.
               // A figure breaks only at a space ("CHF" / "14 400,00"), never inside its digits; a label anywhere it must.
               ".em-table td,.em-table th{white-space:normal !important;word-break:normal !important;padding-left:6px !important;padding-right:10px !important}" +
               ".em-table .em-tl{padding-left:12px !important;word-break:break-word !important}.em-table .em-tl.em-indent{padding-left:24px !important}" +
               ".em-table .em-tr{padding-right:12px !important}" +
               ".em-btn-wrap{width:100% !important}.em-btn{display:block !important;text-align:center !important}}" +
               "</style>";
    }

    // ── Plain text ───────────────────────────────────────────────────────────

    public static string Text(EmailDesign d)
    {
        var t = new StringBuilder();
        var colon = Colon(d.Language);
        if (!string.IsNullOrWhiteSpace(d.Title)) t.Append(d.Title.Trim()).Append("\n\n");
        foreach (var block in d.Blocks)
        {
            switch (block.Kind)
            {
                case EmailDesign.BlockKind.Paragraph:
                case EmailDesign.BlockKind.Note:
                    t.Append(block.Text).Append("\n\n");
                    break;
                case EmailDesign.BlockKind.Heading:
                    t.Append(block.Text).Append('\n').Append(new string('-', Math.Min(block.Text!.Length, 60))).Append('\n');
                    break;
                case EmailDesign.BlockKind.Button:
                    t.Append(block.Text!.TrimEnd()).Append(colon.TrimEnd()).Append('\n').Append(block.Url).Append("\n\n");
                    break;
                case EmailDesign.BlockKind.Facts:
                case EmailDesign.BlockKind.Stats:
                    foreach (var (label, value) in block.Rows!) t.Append(label.TrimEnd()).Append(colon).Append(value).Append('\n');
                    t.Append('\n');
                    break;
                case EmailDesign.BlockKind.List:
                    foreach (var item in block.Items!) t.Append("- ").Append(item).Append('\n');
                    t.Append('\n');
                    break;
                case EmailDesign.BlockKind.Links:
                    foreach (var (label, url) in block.Rows!) t.Append(label.TrimEnd()).Append(colon).Append(url).Append('\n');
                    t.Append('\n');
                    break;
                case EmailDesign.BlockKind.Divider:
                    t.Append("--\n\n");
                    break;
                case EmailDesign.BlockKind.Pre:
                    t.Append(block.Text).Append("\n\n");
                    break;
                case EmailDesign.BlockKind.SignOff:
                    t.Append(string.Join("\n", block.Items!)).Append("\n\n");
                    break;
                case EmailDesign.BlockKind.Table:
                    TableText(t, block, d.Language);
                    break;
            }
        }
        if (!string.IsNullOrWhiteSpace(d.FooterNote)) t.Append("--\n").Append(d.FooterNote.Trim()).Append('\n');
        return t.ToString().TrimEnd() + "\n";
    }

    /// <summary>
    /// A table as lines a plain-text reader can follow without columns lining up (most show
    /// the text part in a proportional font): "- New leads: Week 12, Week before 8", an
    /// indented row under its parent, and with one column just "- Outreach: 20".
    /// </summary>
    private static void TableText(StringBuilder t, EmailDesign.Block block, string? language)
    {
        var heads = block.Items!;
        var colon = Colon(language);
        foreach (var row in block.Table!)
        {
            var figures = heads.Count == 1
                ? row.Values[0]
                : string.Join(", ", heads.Select((head, i) => (head, value: row.Values[i]))
                    .Where(c => c.value.Length > 0)
                    .Select(c => c.head.Length == 0 ? c.value : $"{c.head} {c.value}"));
            t.Append(row.Indented ? "  - " : "- ").Append(OneLine(row.Label).TrimEnd()).Append(colon).Append(OneLine(figures)).Append('\n');
        }
        t.Append('\n');
    }

    /// <summary>
    /// The colon after a label in the plain-text part: French sets a no-break space before it
    /// ("Montant dû : CHF 480.00"); a label that already ends with one is trimmed first.
    /// </summary>
    private static string Colon(string? language) =>
        string.Equals((language ?? "").Trim(), "fr", StringComparison.OrdinalIgnoreCase) ? "\u00a0: " : ": ";

    /// <summary>Text on one line, so a label typed with a line break stays in its row.</summary>
    private static string OneLine(string text) =>
        string.Join(' ', text.Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Trim();

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static readonly Regex WebAddress = new(@"https?://[^\s<>""']+[^\s<>""'.,;:!?)\]]", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Escaped text with its line breaks kept and its web addresses as links.</summary>
    private static string Inline(string? text, Palette p)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var b = new StringBuilder();
        var last = 0;
        foreach (Match m in WebAddress.Matches(text))
        {
            b.Append(E(text[last..m.Index]));
            b.Append("<a href=\"").Append(E(m.Value)).Append("\" class=\"em-link\" style=\"color:").Append(p.Link).Append(";text-decoration:underline;word-break:break-all;\">")
                .Append(E(m.Value)).Append("</a>");
            last = m.Index + m.Length;
        }
        b.Append(E(text[last..]));
        return b.ToString().Replace("\r\n", "\n").Replace("\n", "<br>");
    }

    /// <summary>HTML-escapes a value, quotes included, since some land in attributes.</summary>
    public static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
}
