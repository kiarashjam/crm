using ACI.Domain.Entities;

namespace ACI.Application.Common.Pdf;

/// <summary>Everything the document needs to say about itself.</summary>
/// <param name="Title">The contract's title, used as the running head.</param>
/// <param name="Body">The contract text — the instrument itself.</param>
/// <param name="OrganizationName">Whose letterhead this is.</param>
/// <param name="CounterpartyName">The other party.</param>
/// <param name="Status">A <c>ContractStatuses</c> value; decides the stamp and the panel.</param>
/// <param name="Reference">A short human-quotable reference, e.g. the first block of the id.</param>
/// <param name="BodyHash">The hash frozen at send time, or null before it was sent.</param>
/// <param name="GeneratedAtUtc">
/// Stamped into the document and its metadata. Passed in rather than read from the
/// clock so the same contract renders to the same bytes.
/// </param>
public sealed record ContractPdfRequest(
    string Title,
    string Body,
    string OrganizationName,
    string CounterpartyName,
    string Status,
    string? Reference = null,
    string? BodyHash = null,
    DateTime? GeneratedAtUtc = null,
    string? ClientSignatureName = null,
    DateTime? ClientSignedAtUtc = null,
    string? ClientSignatureIp = null,
    string? CounterSignatureName = null,
    DateTime? CounterSignedAtUtc = null,
    string? CounterSignatureIp = null);

/// <summary>The rendered document.</summary>
/// <param name="Bytes">The PDF file.</param>
/// <param name="PageCount">How many pages it came to.</param>
/// <param name="UnrepresentableCharacters">
/// Characters the encoding could not carry, so the caller can log that this
/// particular document is not a faithful rendering. Empty for every Western
/// European name.
/// </param>
public sealed record ContractPdfResult(
    byte[] Bytes, int PageCount, IReadOnlyList<char> UnrepresentableCharacters);

/// <summary>
/// Sets a contract as a document.
/// </summary>
/// <remarks>
/// <para>
/// The contract body is plain text on purpose: the signing page and the emailed
/// copy show the exact characters the document hash covers, and a renderer between
/// the signer and the text is a place where what was agreed and what was read can
/// diverge. That decision stands. This does not replace it — it adds a typeset
/// COPY, of the same words, for the file somebody keeps and the sheet somebody
/// prints. The plain text remains the instrument; the PDF carries the hash of it
/// so the two can always be checked against each other.
/// </para>
/// <para>
/// The typesetting changes no word, number or mark. What it does change is
/// presentation: prose is re-wrapped and justified to the measure, a row of equals
/// signs under a heading becomes that heading's weight, and a line of dashes
/// becomes a rule. That is what setting text means.
/// </para>
/// <para>
/// Unsigned states are rendered too, stamped and with empty signature rules, so a
/// draft can be circulated and read on paper without any chance of being mistaken
/// for an executed agreement.
/// </para>
/// </remarks>
public static class ContractPdf
{
    /* ---------------------------------------------------------- the palette */

    private static readonly PdfColor Ink = new(0.086, 0.106, 0.161);
    private static readonly PdfColor Accent = new(0.215, 0.188, 0.639);
    private static readonly PdfColor Muted = PdfColor.Grey(0.42);
    private static readonly PdfColor Faint = PdfColor.Grey(0.58);
    private static readonly PdfColor Hairline = PdfColor.Grey(0.855);
    private static readonly PdfColor Panel = PdfColor.Grey(0.965);
    private static readonly PdfColor PanelEdge = PdfColor.Grey(0.9);
    private static readonly PdfColor WatermarkGrey = PdfColor.Grey(0.925);
    private static readonly PdfColor Executed = new(0.02, 0.51, 0.36);
    private static readonly PdfColor Awaiting = new(0.72, 0.42, 0.02);

    /* --------------------------------------------------------- the geometry */

    private const double PageW = PdfBuilder.A4WidthPt;
    private const double PageH = PdfBuilder.A4HeightPt;
    private const double MarginX = 62;
    private const double Measure = PageW - 2 * MarginX;

    /// <summary>Where the body starts on the first page — below the whole title block.</summary>
    private const double FirstPageTop = 214;

    /// <summary>Where the body starts on every later page — below the running head.</summary>
    private const double LaterPageTop = 98;

    /// <summary>The last baseline the body may occupy before the footer's air begins.</summary>
    private const double BodyBottom = PageH - 82;

    /// <summary>Degrees the status stamp is set at, counter-clockwise.</summary>
    private const double WatermarkAngle = 38;

    private const double BodySize = 10.4;
    private const double BodyLeading = 15.9;

    /* ------------------------------------------------------------ rendering */

    /// <summary>Renders the contract.</summary>
    /// <param name="compressStreams">
    /// Leave true. Tests set it false so the generated operators can be read back and
    /// every text run measured against the margin — the one check on a document that
    /// cannot be made by trusting the code that produced it.
    /// </param>
    public static ContractPdfResult Render(ContractPdfRequest request, bool compressStreams = true)
    {
        var generatedAt = request.GeneratedAtUtc ?? DateTime.UtcNow;
        var unmapped = new List<char>();

        // Everything drawn goes through here, so one document reports every
        // character it could not carry rather than each call site guessing.
        string Safe(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            foreach (var c in WinAnsi.Encode(text).Unmapped)
            {
                if (!unmapped.Contains(c)) unmapped.Add(c);
            }
            return text;
        }

        var blocks = ContractBlocks.Parse(request.Body);
        var stamp = StampFor(request.Status);

        // The body's own top-level heading becomes the document's title, verbatim.
        // Setting it AND the contract's title would print the same words twice, so
        // it is promoted rather than repeated; if the body has no heading, the
        // contract's title fills that role instead.
        var bodyTitle = blocks.Count > 0 && blocks[0].Kind == ContractBlockKind.Title
            ? blocks[0].Text
            : null;
        var docTitle = bodyTitle ?? (request.Title.Length > 0 ? request.Title : "Contract");
        IReadOnlyList<ContractBlock> remaining = bodyTitle is null ? blocks : blocks.Skip(1).ToList();

        var builder = new PdfBuilder
        {
            Title = Safe(request.Title.Length > 0 ? request.Title : docTitle),
            Author = Safe(request.OrganizationName),
            Subject = stamp.MetadataSubject,
            CreatedUtc = generatedAt,
            CompressStreams = compressStreams,
        };

        var flow = new Flow(builder, stamp);
        var runningHead = Safe(request.Title.Length > 0 ? request.Title : docTitle);
        flow.StartFirstPage(
            Safe(request.OrganizationName), Safe(docTitle), runningHead,
            Safe(SubtitleFor(request)), Safe(request.Reference), generatedAt);

        foreach (var block in remaining) DrawBlock(flow, block, Safe);

        DrawSignaturePanel(flow, request, Safe);
        DrawIntegrityPanel(flow, request, generatedAt, Safe);
        flow.DrawFooters(runningHead, request.BodyHash);

        return new ContractPdfResult(builder.Build(), builder.PageCount, unmapped);
    }

    private static string SubtitleFor(ContractPdfRequest r)
    {
        var org = r.OrganizationName.Trim();
        var who = r.CounterpartyName.Trim();
        if (org.Length > 0 && who.Length > 0) return $"Between {org} and {who}";
        if (org.Length > 0) return org;
        return who;
    }

    /* -------------------------------------------------------------- content */

    private static void DrawBlock(Flow flow, ContractBlock block, Func<string?, string> safe)
    {
        switch (block.Kind)
        {
            case ContractBlockKind.Title:
                // A second top-level heading inside the body — set it as a clause so
                // it is still distinct, rather than competing with the document title.
                flow.Space(22);
                flow.Heading(safe(block.Text), 12.5, tracking: 0.6);
                flow.Space(10);
                break;

            case ContractBlockKind.Clause:
                flow.Space(flow.AtPageTop ? 0 : 19);
                flow.ClauseHeading(safe(block.Text));
                flow.Space(9);
                break;

            case ContractBlockKind.Definition:
                flow.DefinitionRow(safe(block.Label), safe(block.Text));
                break;

            case ContractBlockKind.Rule:
                flow.Space(11);
                flow.Rule();
                flow.Space(12);
                break;

            default:
                flow.Paragraph(safe(block.Text));
                flow.Space(9.5);
                break;
        }
    }

    private static void DrawSignaturePanel(Flow flow, ContractPdfRequest r, Func<string?, string> safe)
    {
        const double panelHeight = 148;
        flow.Space(16);
        flow.EnsureRoom(panelHeight + 12);

        var top = flow.Y;
        var canvas = flow.Page;
        canvas.RoundedRect(MarginX, top, Measure, panelHeight, 5, Panel, PanelEdge);
        // A solid bar down the binding edge. Cheaper than a border and it is what
        // makes the block read as a panel rather than a box.
        canvas.FilledRect(MarginX, top + 5, 3, panelHeight - 10, Accent);

        canvas.Text(MarginX + 20, top + 22, PdfFont.HelveticaBold, 7.8, "SIGNATURES",
            Muted, tracking: 1.5);

        var colWidth = (Measure - 60) / 2;
        DrawSignature(
            canvas, MarginX + 20, top + 46, colWidth,
            safe(r.CounterpartyName.Length > 0 ? r.CounterpartyName : "The counterparty"),
            safe(r.ClientSignatureName), r.ClientSignedAtUtc, safe(r.ClientSignatureIp));
        DrawSignature(
            canvas, MarginX + 40 + colWidth, top + 46, colWidth,
            safe(r.OrganizationName.Length > 0 ? r.OrganizationName : "The organisation"),
            safe(r.CounterSignatureName), r.CounterSignedAtUtc, safe(r.CounterSignatureIp));

        flow.Advance(panelHeight);
    }

    private static void DrawSignature(
        PdfCanvas canvas, double x, double y, double width,
        string role, string? signedName, DateTime? at, string? ip)
    {
        canvas.Text(x, y, PdfFont.HelveticaBold, 7.2,
            FitTo(role.ToUpperInvariant(), PdfFont.HelveticaBold, 7.2, width, 1.1),
            Faint, tracking: 1.1);

        var signed = !string.IsNullOrWhiteSpace(signedName);
        if (signed)
        {
            // The typed name set large in italic serif. It is not a facsimile of a
            // handwritten mark and must not pretend to be one, but it is the mark
            // that was made, and setting it as body text buries the one thing on the
            // page a reader is looking for.
            canvas.Text(x, y + 30, PdfFont.TimesItalic, 17, FitTo(signedName!, PdfFont.TimesItalic, 17, width),
                Ink);
        }

        canvas.Rule(x, y + 38, x + width, 0.7, signed ? PdfColor.Grey(0.62) : PdfColor.Grey(0.78));

        if (signed)
        {
            canvas.Text(x, y + 51, PdfFont.Helvetica, 8,
                FitTo(signedName!, PdfFont.Helvetica, 8, width), Ink);
            canvas.Text(x, y + 63, PdfFont.Helvetica, 7.4,
                at is null ? "Date not recorded" : $"Signed {FormatUtc(at.Value)}", Muted);
            if (!string.IsNullOrWhiteSpace(ip))
            {
                canvas.Text(x, y + 74, PdfFont.Helvetica, 7.4,
                    FitTo($"From {ip}", PdfFont.Helvetica, 7.4, width), Faint);
            }
        }
        else
        {
            canvas.Text(x, y + 51, PdfFont.HelveticaOblique, 7.8, "Not yet signed", Faint);
            canvas.Text(x, y + 63, PdfFont.Helvetica, 7.4, "Name", Faint);
            canvas.Text(x, y + 74, PdfFont.Helvetica, 7.4, "Date", Faint);
        }
    }

    private static void DrawIntegrityPanel(
        Flow flow, ContractPdfRequest r, DateTime generatedAt, Func<string?, string> safe)
    {
        flow.Space(18);
        flow.EnsureRoom(96);

        var canvas = flow.Page;
        var top = flow.Y;

        canvas.Text(MarginX, top + 8, PdfFont.HelveticaBold, 7.8, "DOCUMENT INTEGRITY", Muted, tracking: 1.5);
        canvas.Rule(MarginX, top + 14, MarginX + Measure, 0.5, Hairline);

        var y = top + 30;
        if (!string.IsNullOrWhiteSpace(r.BodyHash))
        {
            canvas.Text(MarginX, y, PdfFont.Helvetica, 8,
                "SHA-256 of the text signed, recorded when the contract was sent:", Muted);
            y += 13;
            // Split so it fits the measure at a size that can actually be read back
            // to somebody over the telephone, which is what this is for.
            foreach (var chunk in Chunks(r.BodyHash!.Trim(), 44))
            {
                canvas.Text(MarginX, y, PdfFont.Courier, 8.4, chunk, Ink);
                y += 11.5;
            }
        }
        else
        {
            canvas.Text(MarginX, y, PdfFont.HelveticaOblique, 8,
                "No document hash: this contract has not been sent for signature yet.", Faint);
            y += 13;
        }

        y += 4;
        foreach (var line in Wrap(
            "This is a simple electronic signature: a typed name recorded with its timestamp, "
            + "origin and a hash of the exact text signed. It is not a qualified electronic "
            + "signature under ZertES or eIDAS.",
            PdfFont.HelveticaOblique, 7.6, Measure))
        {
            canvas.Text(MarginX, y, PdfFont.HelveticaOblique, 7.6, line, Faint);
            y += 10.4;
        }

        y += 3;
        canvas.Text(MarginX, y, PdfFont.Helvetica, 7.4,
            $"Rendered {FormatUtc(generatedAt)}"
            + (string.IsNullOrWhiteSpace(r.Reference) ? "" : $"  ·  Reference {safe(r.Reference)}"),
            Faint);

        flow.Advance(y - top + 6);
    }

    /* ------------------------------------------------------------ the stamp */

    private readonly record struct StatusStamp(
        string? Watermark, string PillText, PdfColor PillColor, string MetadataSubject);

    private static StatusStamp StampFor(string? status) => status switch
    {
        ContractStatuses.Countersigned => new StatusStamp(
            null, "EXECUTED", Executed, "Executed contract"),
        ContractStatuses.SignedByClient => new StatusStamp(
            "AWAITING COUNTERSIGNATURE", "AWAITING COUNTERSIGNATURE", Awaiting,
            "Contract awaiting countersignature"),
        ContractStatuses.Sent => new StatusStamp(
            "UNSIGNED", "AWAITING SIGNATURE", Awaiting, "Contract awaiting signature"),
        ContractStatuses.Declined => new StatusStamp(
            "DECLINED", "DECLINED", Muted, "Declined contract"),
        ContractStatuses.Voided => new StatusStamp(
            "VOID", "VOID", Muted, "Voided contract"),
        // Draft, and anything a later version of the state machine adds. A document
        // whose status this code does not recognise is stamped DRAFT rather than
        // left to look executed.
        _ => new StatusStamp("DRAFT", "DRAFT", Muted, "Draft contract"),
    };

    /* ------------------------------------------------------- the page flow */

    /// <summary>
    /// Where we are on the page, and what happens when we run off the bottom.
    /// </summary>
    /// <remarks>
    /// Every drawing call goes through this so that page breaks happen in one place.
    /// The alternative — each section checking whether it fits — is how a signature
    /// panel ends up split across two pages.
    /// </remarks>
    private sealed class Flow
    {
        private readonly PdfBuilder _builder;
        private readonly StatusStamp _stamp;
        private readonly List<PdfCanvas> _pages = new();
        private string _runningHead = "";
        private string _org = "";

        public Flow(PdfBuilder builder, StatusStamp stamp)
        {
            _builder = builder;
            _stamp = stamp;
        }

        public PdfCanvas Page { get; private set; } = null!;

        /// <summary>The next baseline, measured down from the top of the page.</summary>
        public double Y { get; private set; }

        /// <summary>True when nothing has been set on this page yet, so leading above is wasted.</summary>
        public bool AtPageTop { get; private set; }

        public void StartFirstPage(
            string org, string title, string runningHead, string subtitle,
            string? reference, DateTime generatedAt)
        {
            _org = org;
            _runningHead = runningHead.Length > 0 ? runningHead : title;
            Page = NewPage();

            // ── letterhead ────────────────────────────────────────────────────
            var pillWidth = DrawPill(Page, PageW - MarginX, 62, _stamp.PillText, _stamp.PillColor);
            Page.Text(MarginX, 62, PdfFont.HelveticaBold, 9,
                FitTo(org.ToUpperInvariant(), PdfFont.HelveticaBold, 9, Measure - pillWidth - 20, 2.0),
                Ink, tracking: 2.0);
            Page.Rule(MarginX, 76, PageW - MarginX, 0.9, Ink);

            // ── title block ───────────────────────────────────────────────────
            var y = 126.0;
            var titleLines = Wrap(title, PdfFont.HelveticaBold, 21, Measure);
            foreach (var line in titleLines)
            {
                Page.Text(MarginX, y, PdfFont.HelveticaBold, 21, line, Ink);
                y += 25;
            }

            Page.FilledRect(MarginX, y - 8, 38, 2.4, Accent);

            var meta = generatedAt.ToString("d MMMM yyyy");
            if (!string.IsNullOrWhiteSpace(reference)) meta += "   ·   Ref. " + reference;
            var metaWidth = PdfFonts.Measure(PdfFont.Helvetica, meta, 8.4);
            Page.Text(PageW - MarginX, y + 18, PdfFont.Helvetica, 8.4, meta, Faint, align: PdfAlign.Right);

            if (subtitle.Length > 0)
            {
                // Fitted against what the date and reference actually occupy, so a long
                // organisation name cannot run underneath them.
                Page.Text(MarginX, y + 18, PdfFont.Helvetica, 9.6,
                    FitTo(subtitle, PdfFont.Helvetica, 9.6, Measure - metaWidth - 18), Muted);
            }

            Y = FirstPageTop;
            // If the title ran to more than one line, the body has to start lower or
            // it collides with the subtitle.
            if (titleLines.Count > 1) Y += (titleLines.Count - 1) * 25;
            AtPageTop = true;
        }

        private PdfCanvas NewPage()
        {
            var page = _builder.AddPage(PageW, PageH);
            _pages.Add(page);

            // First, so everything else sits on top of it.
            if (_stamp.Watermark is { } word)
            {
                // Sized so the ROTATED run fits the sheet. The page diagonal is the
                // wrong bound: a run at 38 degrees is limited by width / cos(38),
                // which for A4 is far tighter than the diagonal, and "AWAITING
                // COUNTERSIGNATURE" set to the diagonal ran off both corners.
                const double inset = 26;
                var rad = WatermarkAngle * Math.PI / 180.0;
                var maxRun = Math.Min(
                    (PageW - inset * 2) / Math.Cos(rad),
                    (PageH - inset * 2) / Math.Sin(rad));
                var atUnitSize = PdfFonts.Measure(PdfFont.HelveticaBold, word, 1, 1.0 / 12);
                var size = Math.Min(78, atUnitSize > 0 ? maxRun / atUnitSize : 78);
                page.RotatedText(PageW / 2, PageH / 2, WatermarkAngle, PdfFont.HelveticaBold, size, word,
                    WatermarkGrey, tracking: size / 12);
            }
            return page;
        }

        private void Break()
        {
            Page = NewPage();
            var orgHead = FitTo(_org.ToUpperInvariant(), PdfFont.HelveticaBold, 7.4, Measure * 0.42, 1.5);
            var orgWidth = PdfFonts.Measure(PdfFont.HelveticaBold, orgHead, 7.4, 1.5);
            Page.Text(MarginX, 52, PdfFont.HelveticaBold, 7.4, orgHead, Faint, tracking: 1.5);
            Page.Text(PageW - MarginX, 52, PdfFont.Helvetica, 7.4,
                FitTo(_runningHead, PdfFont.Helvetica, 7.4, Measure - orgWidth - 18), Faint,
                align: PdfAlign.Right);
            Page.Rule(MarginX, 60, PageW - MarginX, 0.5, Hairline);
            Y = LaterPageTop;
            AtPageTop = true;
        }

        /// <summary>Leaves vertical space, unless we are at the top of a page where it would show as a gap.</summary>
        public void Space(double points)
        {
            if (AtPageTop) return;
            Y += points;
        }

        public void Advance(double points)
        {
            Y += points;
            AtPageTop = false;
        }

        /// <summary>Breaks the page if <paramref name="height"/> will not fit below the current line.</summary>
        public void EnsureRoom(double height)
        {
            if (Y + height > BodyBottom) Break();
        }

        public void Rule()
        {
            EnsureRoom(2);
            Page.Rule(MarginX, Y, PageW - MarginX, 0.5, Hairline);
            Advance(1);
        }

        public void Heading(string text, double size, double tracking)
        {
            foreach (var line in Wrap(text, PdfFont.HelveticaBold, size, Measure))
            {
                EnsureRoom(size * 1.4);
                Page.Text(MarginX, Y, PdfFont.HelveticaBold, size, line, Ink, tracking: tracking);
                Advance(size * 1.4);
            }
        }

        /// <summary>
        /// A numbered clause heading, with the number in the accent colour.
        /// </summary>
        /// <remarks>
        /// Kept on the same page as at least the first two lines of its clause. A
        /// heading alone at the foot of a page is the classic typesetting fault, and
        /// in a contract it also makes a clause look as though it has no content.
        /// </remarks>
        public void ClauseHeading(string text)
        {
            const double size = 9.6;
            EnsureRoom(size * 1.5 + BodyLeading * 2);

            var split = ContractBlocks.SplitClauseNumber(text);
            if (split is { } parts)
            {
                var width = Page.Text(MarginX, Y, PdfFont.HelveticaBold, size, parts.Number,
                    Accent, tracking: 0.85);
                Page.Text(MarginX + width + 5.5, Y, PdfFont.HelveticaBold, size, parts.Words,
                    Ink, tracking: 0.85);
                Advance(size * 1.5);
            }
            else
            {
                Heading(text, size, 0.85);
            }
        }

        /// <summary>A label and its value, as two aligned columns.</summary>
        public void DefinitionRow(string? label, string value)
        {
            const double labelWidth = 92;
            var lines = Wrap(value, PdfFont.TimesRoman, BodySize, Measure - labelWidth);
            if (lines.Count == 0) lines = new List<string> { "" };

            EnsureRoom(lines.Count * BodyLeading);
            if (!string.IsNullOrWhiteSpace(label))
            {
                Page.Text(MarginX, Y, PdfFont.HelveticaBold, 7.6,
                    FitTo(label!.ToUpperInvariant(), PdfFont.HelveticaBold, 7.6, labelWidth - 8, 1.05),
                    Faint, tracking: 1.05);
            }
            for (var i = 0; i < lines.Count; i++)
            {
                Page.Text(MarginX + labelWidth, Y, PdfFont.TimesRoman, BodySize, lines[i], Ink);
                Advance(BodyLeading);
                if (i < lines.Count - 1) EnsureRoom(BodyLeading);
            }
        }

        /// <summary>Justified running prose.</summary>
        public void Paragraph(string text)
        {
            var lines = Wrap(text, PdfFont.TimesRoman, BodySize, Measure);
            for (var i = 0; i < lines.Count; i++)
            {
                EnsureRoom(BodyLeading);
                var isLast = i == lines.Count - 1;
                Page.Text(MarginX, Y, PdfFont.TimesRoman, BodySize, lines[i], Ink,
                    wordSpacing: isLast ? 0 : Justify(lines[i], PdfFont.TimesRoman, BodySize, Measure));
                Advance(BodyLeading);
            }
        }

        /// <summary>
        /// Draws the footer on every page, once the total is known.
        /// </summary>
        /// <remarks>
        /// "Page 2 of 5" cannot be written while page 2 is being laid out, so footers
        /// are left until the end rather than laying the document out twice.
        /// </remarks>
        public void DrawFooters(string title, string? bodyHash)
        {
            var total = _pages.Count;
            for (var i = 0; i < total; i++)
            {
                var page = _pages[i];
                var y = PageH - 46;
                page.Rule(MarginX, y - 12, PageW - MarginX, 0.5, Hairline);
                // Fitted by measured width, not by counting characters. A 64-character
                // title is a different number of points in every string, and counting
                // characters ran the title straight into the centred page number.
                var label = $"Page {i + 1} of {total}";
                var labelHalf = PdfFonts.Measure(PdfFont.Helvetica, label, 7.2) / 2;
                page.Text(MarginX, y, PdfFont.Helvetica, 7.2,
                    FitTo(title, PdfFont.Helvetica, 7.2, PageW / 2 - labelHalf - MarginX - 14), Faint);
                page.Text(PageW / 2, y, PdfFont.Helvetica, 7.2, label, Faint, align: PdfAlign.Center);
                if (!string.IsNullOrWhiteSpace(bodyHash))
                {
                    // The first bytes of the hash on every page: enough that a loose
                    // sheet can be matched back to the document it came from.
                    page.Text(PageW - MarginX, y, PdfFont.Courier, 7.2,
                        bodyHash!.Trim().ToLowerInvariant()[..Math.Min(12, bodyHash.Trim().Length)],
                        Faint, align: PdfAlign.Right);
                }
            }
        }

        /// <returns>How wide the pill came out, so the letterhead can avoid it.</returns>
        private static double DrawPill(PdfCanvas canvas, double rightX, double baseline, string text, PdfColor color)
        {
            const double size = 7.2;
            const double padX = 7;
            var width = PdfFonts.Measure(PdfFont.HelveticaBold, text, size, 1.1) + padX * 2;
            var left = rightX - width;
            canvas.RoundedRect(left, baseline - 9.5, width, 14, 7, null, color, 0.9);
            canvas.Text(left + padX, baseline - 0.5, PdfFont.HelveticaBold, size, text, color, tracking: 1.1);
            return width;
        }
    }

    /* --------------------------------------------------------------- typesetting */

    /// <summary>
    /// Greedy word wrap to a measure, in points.
    /// </summary>
    /// <remarks>
    /// A word longer than the whole measure — a hash, a long URL, an email address
    /// with no break in it — is split rather than allowed to run off the page. That
    /// case is rare and always looks slightly wrong; running into the margin looks
    /// broken.
    /// </remarks>
    internal static List<string> Wrap(string text, PdfFont font, double size, double measure)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text)) return lines;

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = "";

        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (PdfFonts.Measure(font, candidate, size) <= measure)
            {
                current = candidate;
                continue;
            }

            if (current.Length > 0) { lines.Add(current); current = ""; }

            var rest = word;
            while (PdfFonts.Measure(font, rest, size) > measure && rest.Length > 1)
            {
                var take = rest.Length;
                while (take > 1 && PdfFonts.Measure(font, rest[..take], size) > measure) take--;
                lines.Add(rest[..take]);
                rest = rest[take..];
            }
            current = rest;
        }

        if (current.Length > 0) lines.Add(current);
        return lines;
    }

    /// <summary>
    /// The word spacing that justifies a line, or zero if it should stay ragged.
    /// </summary>
    /// <remarks>
    /// Capped at 1.6 times the font's natural space. Past that, justification stops
    /// being invisible and becomes rivers of white running down the page — and a
    /// short line with one space in it would otherwise be stretched into two words
    /// at opposite margins.
    /// </remarks>
    internal static double Justify(string line, PdfFont font, double size, double measure)
    {
        var spaces = line.Count(c => c == ' ');
        if (spaces == 0) return 0;

        var natural = PdfFonts.Measure(font, line, size);
        var slack = measure - natural;
        if (slack <= 0) return 0;

        var per = slack / spaces;
        var spaceWidth = PdfFonts.Measure(font, " ", size);
        return per > spaceWidth * 1.6 ? 0 : per;
    }

    /// <summary>Shrinks text until it fits a width, rather than letting it overrun.</summary>
    /// <param name="tracking">Must match what the caller will draw with, or a
    /// letter-spaced heading measures narrower here than it does on the page.</param>
    private static string FitTo(string text, PdfFont font, double size, double width, double tracking = 0)
    {
        if (text.Length == 0 || PdfFonts.Measure(font, text, size, tracking) <= width) return text;
        var take = text.Length;
        while (take > 1 && PdfFonts.Measure(font, text[..take] + "…", size, tracking) > width) take--;
        return text[..take].TrimEnd() + "…";
    }


    private static IEnumerable<string> Chunks(string value, int size)
    {
        for (var i = 0; i < value.Length; i += size)
        {
            yield return value.Substring(i, Math.Min(size, value.Length - i));
        }
    }

    private static string FormatUtc(DateTime at)
        => at.ToString("d MMMM yyyy 'at' HH:mm 'UTC'");
}
