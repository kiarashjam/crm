namespace ACI.Application.Common.Pdf;

/// <summary>
/// One of the fourteen fonts every PDF reader is required to have built in.
/// </summary>
/// <remarks>
/// Using only these is a deliberate constraint. An embedded font would mean
/// shipping font binaries in the repository, a licence to honour for each one, and
/// a font-loading path that can fail on a machine that is not this one. The
/// standard fourteen need none of that: the file carries no font data, renders
/// identically in every reader, and stays a few kilobytes.
///
/// Helvetica is also simply the right typeface for the job here — the
/// organisations using this are Swiss, and their membership agreements are set in
/// the national sans by long habit.
/// </remarks>
public enum PdfFont
{
    /// <summary>Body sans.</summary>
    Helvetica,
    /// <summary>Headings, labels, the letterhead.</summary>
    HelveticaBold,
    /// <summary>Captions and asides.</summary>
    HelveticaOblique,
    /// <summary>Body serif, for running prose.</summary>
    TimesRoman,
    /// <summary>Serif emphasis.</summary>
    TimesBold,
    /// <summary>Signatures — a typed name in italic serif reads as a mark.</summary>
    TimesItalic,
    /// <summary>Hashes and anything that must not be mistaken for prose.</summary>
    Courier,
}

/// <summary>
/// Widths for the standard fourteen, and the WinAnsi encoding that addresses them.
/// </summary>
/// <remarks>
/// <para>
/// The width tables are the published Adobe Font Metrics for the core fonts,
/// transcribed for WinAnsi codes 32-255 in units of 1/1000 em. They are FACTS about
/// the fonts, not a choice, and they are what makes it possible to break a line or
/// centre a heading without a rendering engine: the width of a string is the sum of
/// its glyph widths.
/// </para>
/// <para>
/// Get these wrong and nothing throws — text simply runs past the margin. So
/// <c>PdfFontMetricsTests</c> checks a sample against the published values and
/// checks that every WinAnsi code has a non-zero width in every font.
/// </para>
/// </remarks>
public static class PdfFonts
{
    /// <summary>The first WinAnsi code the tables cover. Below this is control space.</summary>
    private const int FirstCode = 32;

    /// <summary>Helvetica, WinAnsi codes 32-255.</summary>
    private static readonly short[] Helvetica =
    {
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
        1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
        333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
        556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584, 0,
        556, 0, 222, 556, 333, 1000, 556, 556, 333, 1000, 667, 333, 1000, 0, 611, 0,
        0, 222, 222, 333, 333, 350, 556, 1000, 333, 1000, 500, 333, 944, 0, 500, 667,
        278, 333, 556, 556, 556, 556, 260, 556, 333, 737, 370, 556, 584, 333, 737, 333,
        400, 584, 333, 333, 333, 556, 537, 278, 333, 333, 365, 556, 834, 834, 834, 611,
        667, 667, 667, 667, 667, 667, 1000, 722, 667, 667, 667, 667, 278, 278, 278, 278,
        722, 722, 778, 778, 778, 778, 778, 584, 778, 722, 722, 722, 722, 667, 667, 611,
        556, 556, 556, 556, 556, 556, 889, 500, 556, 556, 556, 556, 278, 278, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 584, 611, 556, 556, 556, 556, 500, 556, 500,
    };

    /// <summary>Helvetica-Bold, WinAnsi codes 32-255.</summary>
    private static readonly short[] HelveticaBold =
    {
        278, 333, 474, 556, 556, 889, 722, 238, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 333, 333, 584, 584, 584, 611,
        975, 722, 722, 722, 722, 667, 611, 778, 722, 278, 556, 722, 611, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 333, 278, 333, 584, 556,
        333, 556, 611, 556, 611, 556, 333, 611, 611, 278, 278, 556, 278, 889, 611, 611,
        611, 611, 389, 556, 333, 611, 556, 778, 556, 556, 500, 389, 280, 389, 584, 0,
        556, 0, 278, 556, 500, 1000, 556, 556, 333, 1000, 667, 333, 1000, 0, 611, 0,
        0, 278, 278, 500, 500, 350, 556, 1000, 333, 1000, 556, 333, 944, 0, 500, 667,
        278, 333, 556, 556, 556, 556, 280, 556, 333, 737, 370, 556, 584, 333, 737, 333,
        400, 584, 333, 333, 333, 611, 556, 278, 333, 333, 365, 556, 834, 834, 834, 611,
        722, 722, 722, 722, 722, 722, 1000, 722, 667, 667, 667, 667, 278, 278, 278, 278,
        722, 722, 778, 778, 778, 778, 778, 584, 778, 722, 722, 722, 722, 667, 667, 611,
        556, 556, 556, 556, 556, 556, 889, 556, 556, 556, 556, 556, 278, 278, 278, 278,
        611, 611, 611, 611, 611, 611, 611, 584, 611, 611, 611, 611, 611, 556, 611, 556,
    };

    /// <summary>Helvetica-Oblique, WinAnsi codes 32-255.</summary>
    private static readonly short[] HelveticaOblique =
    {
        278, 278, 355, 556, 556, 889, 667, 191, 333, 333, 389, 584, 278, 333, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 556, 556, 556, 278, 278, 584, 584, 584, 556,
        1015, 667, 667, 722, 722, 667, 611, 778, 722, 278, 500, 667, 556, 833, 722, 778,
        667, 778, 722, 667, 611, 722, 667, 944, 667, 667, 611, 278, 278, 278, 469, 556,
        333, 556, 556, 500, 556, 556, 278, 556, 556, 222, 222, 500, 222, 833, 556, 556,
        556, 556, 333, 500, 278, 556, 500, 722, 500, 500, 500, 334, 260, 334, 584, 0,
        556, 0, 222, 556, 333, 1000, 556, 556, 333, 1000, 667, 333, 1000, 0, 611, 0,
        0, 222, 222, 333, 333, 350, 556, 1000, 333, 1000, 500, 333, 944, 0, 500, 667,
        278, 333, 556, 556, 556, 556, 260, 556, 333, 737, 370, 556, 584, 333, 737, 333,
        400, 584, 333, 333, 333, 556, 537, 278, 333, 333, 365, 556, 834, 834, 834, 611,
        667, 667, 667, 667, 667, 667, 1000, 722, 667, 667, 667, 667, 278, 278, 278, 278,
        722, 722, 778, 778, 778, 778, 778, 584, 778, 722, 722, 722, 722, 667, 667, 611,
        556, 556, 556, 556, 556, 556, 889, 500, 556, 556, 556, 556, 278, 278, 278, 278,
        556, 556, 556, 556, 556, 556, 556, 584, 611, 556, 556, 556, 556, 500, 556, 500,
    };

    /// <summary>Times-Roman, WinAnsi codes 32-255.</summary>
    private static readonly short[] TimesRoman =
    {
        250, 333, 408, 500, 500, 833, 778, 180, 333, 333, 500, 564, 250, 333, 250, 278,
        500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 278, 278, 564, 564, 564, 444,
        921, 722, 667, 667, 722, 611, 556, 722, 722, 333, 389, 722, 611, 889, 722, 722,
        556, 722, 667, 556, 611, 722, 722, 944, 722, 722, 611, 333, 278, 333, 469, 500,
        333, 444, 500, 444, 500, 444, 333, 500, 500, 278, 278, 500, 278, 778, 500, 500,
        500, 500, 333, 389, 278, 500, 500, 722, 500, 500, 444, 480, 200, 480, 541, 0,
        500, 0, 333, 500, 444, 1000, 500, 500, 333, 1000, 556, 333, 889, 0, 611, 0,
        0, 333, 333, 444, 444, 350, 500, 1000, 333, 980, 389, 333, 722, 0, 444, 722,
        250, 333, 500, 500, 500, 500, 200, 500, 333, 760, 276, 500, 564, 333, 760, 333,
        400, 564, 300, 300, 333, 500, 453, 250, 333, 300, 310, 500, 750, 750, 750, 444,
        722, 722, 722, 722, 722, 722, 889, 667, 611, 611, 611, 611, 333, 333, 333, 333,
        722, 722, 722, 722, 722, 722, 722, 564, 722, 722, 722, 722, 722, 722, 556, 500,
        444, 444, 444, 444, 444, 444, 667, 444, 444, 444, 444, 444, 278, 278, 278, 278,
        500, 500, 500, 500, 500, 500, 500, 564, 500, 500, 500, 500, 500, 500, 500, 500,
    };

    /// <summary>Times-Bold, WinAnsi codes 32-255.</summary>
    private static readonly short[] TimesBold =
    {
        250, 333, 555, 500, 500, 1000, 833, 278, 333, 333, 500, 570, 250, 333, 250, 278,
        500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 333, 333, 570, 570, 570, 500,
        930, 722, 667, 722, 722, 667, 611, 778, 778, 389, 500, 778, 667, 944, 722, 778,
        611, 778, 722, 556, 667, 722, 722, 1000, 722, 722, 667, 333, 278, 333, 581, 500,
        333, 500, 556, 444, 556, 444, 333, 500, 556, 278, 333, 556, 278, 833, 556, 500,
        556, 556, 444, 389, 333, 556, 500, 722, 500, 500, 444, 394, 220, 394, 520, 0,
        500, 0, 333, 500, 500, 1000, 500, 500, 333, 1000, 556, 333, 1000, 0, 667, 0,
        0, 333, 333, 500, 500, 350, 500, 1000, 333, 1000, 389, 333, 722, 0, 444, 722,
        250, 333, 500, 500, 500, 500, 220, 500, 333, 747, 300, 500, 570, 333, 747, 333,
        400, 570, 300, 300, 333, 556, 540, 250, 333, 300, 330, 500, 750, 750, 750, 500,
        722, 722, 722, 722, 722, 722, 1000, 722, 667, 667, 667, 667, 389, 389, 389, 389,
        722, 722, 778, 778, 778, 778, 778, 570, 778, 722, 722, 722, 722, 722, 611, 556,
        500, 500, 500, 500, 500, 500, 722, 444, 444, 444, 444, 444, 278, 278, 278, 278,
        500, 556, 500, 500, 500, 500, 500, 570, 500, 556, 556, 556, 556, 500, 556, 500,
    };

    /// <summary>Times-Italic, WinAnsi codes 32-255.</summary>
    private static readonly short[] TimesItalic =
    {
        250, 333, 420, 500, 500, 833, 778, 214, 333, 333, 500, 675, 250, 333, 250, 278,
        500, 500, 500, 500, 500, 500, 500, 500, 500, 500, 333, 333, 675, 675, 675, 500,
        920, 611, 611, 667, 722, 611, 611, 722, 722, 333, 444, 667, 556, 833, 667, 722,
        611, 722, 611, 500, 556, 722, 611, 833, 611, 556, 556, 389, 278, 389, 422, 500,
        333, 500, 500, 444, 500, 444, 278, 500, 500, 278, 278, 444, 278, 722, 500, 500,
        500, 500, 389, 389, 278, 500, 444, 667, 444, 444, 389, 400, 275, 400, 541, 0,
        500, 0, 333, 500, 556, 889, 500, 500, 333, 1000, 500, 333, 944, 0, 556, 0,
        0, 333, 333, 556, 556, 350, 500, 889, 333, 980, 389, 333, 667, 0, 389, 556,
        250, 389, 500, 500, 500, 500, 275, 500, 333, 760, 276, 500, 675, 333, 760, 333,
        400, 675, 300, 300, 333, 500, 523, 250, 333, 300, 310, 500, 750, 750, 750, 500,
        611, 611, 611, 611, 611, 611, 889, 667, 611, 611, 611, 611, 333, 333, 333, 333,
        722, 667, 722, 722, 722, 722, 722, 675, 722, 722, 722, 722, 722, 556, 611, 500,
        500, 500, 500, 500, 500, 500, 667, 444, 444, 444, 444, 444, 278, 278, 278, 278,
        500, 500, 500, 500, 500, 500, 500, 675, 500, 500, 500, 500, 500, 444, 500, 444,
    };

    /// <summary>Courier, WinAnsi codes 32-255.</summary>
    private static readonly short[] Courier =
    {
        600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600,
        600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600,
        600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600,
        600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600,
        600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600,
        600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 0,
        600, 0, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 0, 600, 0,
        0, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 0, 600, 600,
        600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600,
        600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600,
        600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600,
        600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600,
        600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600,
        600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600, 600,
    };

    private static short[] TableFor(PdfFont font) => font switch
    {
        PdfFont.Helvetica => Helvetica,
        PdfFont.HelveticaBold => HelveticaBold,
        PdfFont.HelveticaOblique => HelveticaOblique,
        PdfFont.TimesRoman => TimesRoman,
        PdfFont.TimesBold => TimesBold,
        PdfFont.TimesItalic => TimesItalic,
        PdfFont.Courier => Courier,
        _ => Helvetica,
    };

    /// <summary>The /BaseFont name that goes in the PDF font dictionary.</summary>
    public static string BaseFontName(PdfFont font) => font switch
    {
        PdfFont.Helvetica => "Helvetica",
        PdfFont.HelveticaBold => "Helvetica-Bold",
        PdfFont.HelveticaOblique => "Helvetica-Oblique",
        PdfFont.TimesRoman => "Times-Roman",
        PdfFont.TimesBold => "Times-Bold",
        PdfFont.TimesItalic => "Times-Italic",
        PdfFont.Courier => "Courier",
        _ => "Helvetica",
    };

    /// <summary>Every font this writer can name, in resource order.</summary>
    public static readonly PdfFont[] All =
    {
        PdfFont.Helvetica, PdfFont.HelveticaBold, PdfFont.HelveticaOblique,
        PdfFont.TimesRoman, PdfFont.TimesBold, PdfFont.TimesItalic, PdfFont.Courier,
    };

    /// <summary>The /F1 .. /F7 name this font is referred to by inside a content stream.</summary>
    public static string ResourceName(PdfFont font) => "F" + (Array.IndexOf(All, font) + 1).ToString();

    /// <summary>Width of one already-encoded WinAnsi byte, in 1/1000 em.</summary>
    private static int WidthOf(PdfFont font, byte code)
    {
        if (code < FirstCode) return 0;
        var table = TableFor(font);
        var i = code - FirstCode;
        return i < table.Length ? table[i] : 0;
    }

    /// <summary>
    /// Width of encoded text at a point size, in points.
    /// </summary>
    /// <param name="tracking">
    /// Extra space after every glyph, in points — the PDF <c>Tc</c> operator. Used
    /// for the letter-spaced capitals in the letterhead, and it must be included
    /// here or a tracked-out string measures narrower than it draws and overruns.
    /// </param>
    public static double Measure(PdfFont font, ReadOnlySpan<byte> encoded, double sizePt, double tracking = 0)
    {
        var units = 0;
        foreach (var b in encoded) units += WidthOf(font, b);
        return units * sizePt / 1000.0 + tracking * encoded.Length;
    }

    /// <summary>Width of a string, encoding it first so the measurement matches what is drawn.</summary>
    public static double Measure(PdfFont font, string text, double sizePt, double tracking = 0)
        => Measure(font, WinAnsi.Encode(text).Bytes, sizePt, tracking);
}
