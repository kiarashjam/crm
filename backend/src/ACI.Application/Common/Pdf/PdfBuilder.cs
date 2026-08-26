using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace ACI.Application.Common.Pdf;

/// <summary>
/// Assembles pages into a PDF file.
/// </summary>
/// <remarks>
/// <para>
/// A PDF is a sequence of numbered objects, a cross-reference table giving the byte
/// offset of each, and a trailer pointing at the table. That is nearly all of it,
/// and writing it directly is a few hundred lines — against a dependency that would
/// need restoring in CI, a licence to honour, and in one popular case a native
/// library that does not exist on the deployment target.
/// </para>
/// <para>
/// The cross-reference offsets are the part that must be exact: a reader that finds
/// the wrong byte there reports the file as damaged and shows nothing. So the file
/// is built in one pass over a byte buffer, and every offset is the buffer's length
/// at the moment the object starts, never a computed guess.
/// </para>
/// </remarks>
public sealed class PdfBuilder
{
    /// <summary>A4 in points. The document standard everywhere this is used.</summary>
    public const double A4WidthPt = 595.276;

    /// <summary>A4 in points.</summary>
    public const double A4HeightPt = 841.890;

    private readonly List<PdfCanvas> _pages = new();

    /// <summary>Shown as the document title in a reader's title bar and tab.</summary>
    public string Title { get; set; } = "";

    /// <summary>Who produced it.</summary>
    public string Author { get; set; } = "";

    /// <summary>What kind of document a reader is looking at, for its properties panel.</summary>
    public string Subject { get; set; } = "";

    /// <summary>
    /// The creation timestamp, or null to omit it.
    /// </summary>
    /// <remarks>
    /// Supplied by the caller rather than read from the clock, so the same contract
    /// generates the same bytes twice. A document whose bytes change every second
    /// cannot be compared, cached, or asserted on.
    /// </remarks>
    public DateTime? CreatedUtc { get; set; }

    /// <summary>
    /// Whether page content is deflated. True everywhere except in tests.
    /// </summary>
    /// <remarks>
    /// A compressed stream cannot be read back, and the one check worth making on a
    /// generated document is that no text runs past the margin — which needs the
    /// operators. So tests turn this off and measure every run in the real output,
    /// rather than trusting the wrapper that produced it.
    /// </remarks>
    public bool CompressStreams { get; set; } = true;

    /// <summary>Starts a new page and returns its canvas.</summary>
    public PdfCanvas AddPage(double widthPt = A4WidthPt, double heightPt = A4HeightPt)
    {
        var page = new PdfCanvas(widthPt, heightPt);
        _pages.Add(page);
        return page;
    }

    /// <summary>How many pages have been started.</summary>
    public int PageCount => _pages.Count;

    /// <summary>The finished file.</summary>
    public byte[] Build()
    {
        // Never emit a zero-page PDF: it is malformed, and every reader refuses it.
        if (_pages.Count == 0) AddPage();

        var body = new List<byte[]>();          // object bodies, 1-indexed by position
        var offsets = new List<int>();
        var buffer = new MemoryStream();

        // Object numbers are laid out up front so /Kids and /Contents can refer
        // forwards. 1 = catalog, 2 = page tree, then one page object and one content
        // stream per page, then the fonts, then the info dictionary.
        var pageObj = new int[_pages.Count];
        var contentObj = new int[_pages.Count];
        var next = 3;
        for (var i = 0; i < _pages.Count; i++) { pageObj[i] = next++; contentObj[i] = next++; }
        var fontObj = new int[PdfFonts.All.Length];
        for (var i = 0; i < PdfFonts.All.Length; i++) fontObj[i] = next++;
        var infoObj = next++;
        var objectCount = next - 1;

        var fontResources = new StringBuilder();
        for (var i = 0; i < PdfFonts.All.Length; i++)
        {
            fontResources.Append($"/{PdfFonts.ResourceName(PdfFonts.All[i])} {fontObj[i]} 0 R ");
        }

        body.Add(Ascii("<< /Type /Catalog /Pages 2 0 R >>"));

        var kids = string.Join(" ", pageObj.Select(n => $"{n} 0 R"));
        body.Add(Ascii($"<< /Type /Pages /Count {_pages.Count} /Kids [{kids}] >>"));

        for (var i = 0; i < _pages.Count; i++)
        {
            var p = _pages[i];
            body.Add(Ascii(
                "<< /Type /Page /Parent 2 0 R " +
                $"/MediaBox [0 0 {Num(p.Width)} {Num(p.Height)}] " +
                $"/Resources << /Font << {fontResources.ToString().TrimEnd()} >> >> " +
                $"/Contents {contentObj[i]} 0 R >>"));

            body.Add(ContentStream(p.Content, CompressStreams));
        }

        foreach (var font in PdfFonts.All)
        {
            body.Add(Ascii(
                "<< /Type /Font /Subtype /Type1 " +
                $"/BaseFont /{PdfFonts.BaseFontName(font)} " +
                // Without this the reader falls back to StandardEncoding, in which
                // 0x27 is a right quote and the accented characters are absent —
                // so "jean.o'neill@example.ch" and "Zurich" would both come out wrong.
                "/Encoding /WinAnsiEncoding >>"));
        }

        body.Add(Ascii(InfoDictionary()));

        // ---- serialise ------------------------------------------------------
        Write(buffer, "%PDF-1.4\n");
        // A comment of high bytes, immediately after the header, is the conventional
        // marker that tells tools this file is binary and must not be newline-translated.
        buffer.Write(new byte[] { (byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n' });

        for (var i = 0; i < body.Count; i++)
        {
            offsets.Add((int)buffer.Length);
            Write(buffer, $"{i + 1} 0 obj\n");
            buffer.Write(body[i]);
            Write(buffer, "\nendobj\n");
        }

        var xref = (int)buffer.Length;
        Write(buffer, $"xref\n0 {objectCount + 1}\n");
        // The head of the free list. Exactly 20 bytes per entry, including this one,
        // or the table cannot be indexed.
        Write(buffer, "0000000000 65535 f \n");
        foreach (var off in offsets) Write(buffer, $"{off.ToString("D10", CultureInfo.InvariantCulture)} 00000 n \n");

        Write(buffer, "trailer\n");
        Write(buffer, $"<< /Size {objectCount + 1} /Root 1 0 R /Info {infoObj} 0 R >>\n");
        Write(buffer, $"startxref\n{xref}\n%%EOF\n");

        return buffer.ToArray();
    }

    private string InfoDictionary()
    {
        var sb = new StringBuilder("<< ");
        if (Title.Length > 0) sb.Append($"/Title {TextString(Title)} ");
        if (Author.Length > 0) sb.Append($"/Author {TextString(Author)} ");
        if (Subject.Length > 0) sb.Append($"/Subject {TextString(Subject)} ");
        sb.Append("/Producer (Cadence CRM) ");
        if (CreatedUtc is { } at)
        {
            // PDF's own date syntax. The trailing Z is what says UTC rather than
            // "the timezone of whichever machine happened to render this".
            sb.Append($"/CreationDate (D:{at:yyyyMMddHHmmss}Z) ");
        }
        sb.Append(">>");
        return sb.ToString();
    }

    /// <summary>A PDF text string, WinAnsi-encoded and escaped.</summary>
    private static string TextString(string value) => WinAnsi.Literal(WinAnsi.Encode(value).Bytes);

    /// <summary>
    /// A content stream, deflated.
    /// </summary>
    /// <remarks>
    /// <c>ZLibStream</c> produces the zlib wrapper PDF's <c>/FlateDecode</c> expects;
    /// <c>DeflateStream</c> produces a raw deflate block without it, and a reader
    /// given that reports a damaged file. They differ by two header bytes and a
    /// checksum, which is an easy hour to lose.
    /// </remarks>
    private static byte[] ContentStream(string content, bool compress)
    {
        var raw = Encoding.ASCII.GetBytes(content);
        if (!compress)
        {
            return Concat(
                Ascii($"<< /Length {raw.Length} >>\nstream\n"), raw, Ascii("\nendstream"));
        }

        byte[] compressed;
        using (var output = new MemoryStream())
        {
            using (var z = new ZLibStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                z.Write(raw);
            }
            compressed = output.ToArray();
        }

        return Concat(
            Ascii($"<< /Length {compressed.Length} /Filter /FlateDecode >>\nstream\n"),
            compressed,
            Ascii("\nendstream"));
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var all = new byte[parts.Sum(p => p.Length)];
        var at = 0;
        foreach (var part in parts) { part.CopyTo(all, at); at += part.Length; }
        return all;
    }

    private static byte[] Ascii(string s) => Encoding.ASCII.GetBytes(s);

    private static void Write(Stream s, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        s.Write(bytes, 0, bytes.Length);
    }

    private static string Num(double v) => Math.Round(v, 3).ToString("0.###", CultureInfo.InvariantCulture);
}
