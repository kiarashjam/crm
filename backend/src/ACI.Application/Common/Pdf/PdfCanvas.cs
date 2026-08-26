using System.Globalization;
using System.Text;

namespace ACI.Application.Common.Pdf;

/// <summary>A greyscale or colour fill, 0-1 per channel.</summary>
/// <remarks>
/// Greys are given as three equal channels rather than PDF's <c>g</c> operator, so
/// there is one code path for colour and one fewer thing to get wrong.
/// </remarks>
public readonly record struct PdfColor(double R, double G, double B)
{
    public static PdfColor Grey(double v) => new(v, v, v);
    public static readonly PdfColor Black = Grey(0);
    public static readonly PdfColor White = Grey(1);
}

/// <summary>How a run of text sits against the x coordinate given.</summary>
public enum PdfAlign
{
    /// <summary>x is the left edge.</summary>
    Left,
    /// <summary>x is the right edge; the text is laid out backwards from it.</summary>
    Right,
    /// <summary>x is the centre.</summary>
    Center,
}

/// <summary>
/// One page's worth of drawing operators.
/// </summary>
/// <remarks>
/// <para>
/// Coordinates here are TOP-DOWN — y grows downwards from the top edge, the way a
/// page is read and the way a layout is written. PDF's own origin is the bottom
/// left, and the conversion happens once, here, at the moment each operator is
/// emitted. Doing it anywhere else means every piece of layout arithmetic has to
/// remember which way up it is, which is exactly the bug that puts a footer above
/// a heading.
/// </para>
/// <para>
/// This class knows nothing about contracts. It draws text, rules and boxes.
/// </para>
/// </remarks>
public sealed class PdfCanvas
{
    private readonly StringBuilder _ops = new();

    public PdfCanvas(double widthPt, double heightPt)
    {
        Width = widthPt;
        Height = heightPt;
    }

    /// <summary>Page width in points.</summary>
    public double Width { get; }

    /// <summary>Page height in points.</summary>
    public double Height { get; }

    /// <summary>True if nothing has been drawn — used to avoid emitting a blank final page.</summary>
    public bool IsEmpty => _ops.Length == 0;

    /// <summary>The accumulated content stream.</summary>
    public string Content => _ops.ToString();

    private static string N(double v)
    {
        // Three decimals is finer than any printer resolves, and rounding here keeps
        // the file small and byte-identical between runs for identical input — which
        // is what lets a test assert on generated output at all.
        var rounded = Math.Round(v, 3);
        if (rounded == 0) rounded = 0; // fold -0 into 0 so output is stable
        return rounded.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private double Flip(double topDownY) => Height - topDownY;

    /// <summary>
    /// Draws one line of text with its BASELINE at <paramref name="y"/>.
    /// </summary>
    /// <remarks>
    /// Baseline rather than top edge, because that is what PDF positions and what
    /// typography is measured from; a layout that thinks in top edges has to guess
    /// at ascender heights the standard fonts do not report here.
    /// </remarks>
    /// <returns>The width drawn, in points.</returns>
    public double Text(
        double x, double y, PdfFont font, double sizePt, string text,
        PdfColor? color = null, double tracking = 0, PdfAlign align = PdfAlign.Left,
        double wordSpacing = 0)
    {
        var encoded = WinAnsi.Encode(text);
        return Text(x, y, font, sizePt, encoded.Bytes, color, tracking, align, wordSpacing);
    }

    /// <summary>
    /// Draws already-encoded text, so a caller that wrapped by measuring draws
    /// exactly what it measured.
    /// </summary>
    /// <param name="wordSpacing">
    /// Extra points added to every space character — PDF's <c>Tw</c>. This is how a
    /// justified line is set: the wrapper works out how much slack the line has and
    /// distributes it across the spaces, rather than moving each word separately.
    /// </param>
    public double Text(
        double x, double y, PdfFont font, double sizePt, ReadOnlySpan<byte> encoded,
        PdfColor? color = null, double tracking = 0, PdfAlign align = PdfAlign.Left,
        double wordSpacing = 0)
    {
        if (encoded.Length == 0) return 0;

        var width = PdfFonts.Measure(font, encoded, sizePt, tracking);
        if (wordSpacing != 0)
        {
            var spaces = 0;
            foreach (var b in encoded) if (b == (byte)' ') spaces++;
            width += wordSpacing * spaces;
        }
        var left = align switch
        {
            PdfAlign.Right => x - width,
            PdfAlign.Center => x - width / 2,
            _ => x,
        };

        // Tracking is applied AFTER the last glyph too, so a tracked-out string is
        // one tracking-unit wider than its glyphs. Centring on the glyphs rather
        // than the advance keeps letter-spaced headings visually centred.
        if (align == PdfAlign.Center && tracking > 0) left += tracking / 2;

        var c = color ?? PdfColor.Black;
        _ops.Append("BT ");
        _ops.Append($"{N(c.R)} {N(c.G)} {N(c.B)} rg ");
        _ops.Append($"/{PdfFonts.ResourceName(font)} {N(sizePt)} Tf ");
        if (tracking != 0) _ops.Append($"{N(tracking)} Tc ");
        if (wordSpacing != 0) _ops.Append($"{N(wordSpacing)} Tw ");
        _ops.Append($"1 0 0 1 {N(left)} {N(Flip(y))} Tm ");
        _ops.Append(WinAnsi.Literal(encoded));
        _ops.Append(" Tj ");
        // Both are part of the graphics state, so they must be put back or they
        // leak into every later run on this page.
        if (tracking != 0) _ops.Append("0 Tc ");
        if (wordSpacing != 0) _ops.Append("0 Tw ");
        _ops.Append("ET\n");
        return width;
    }

    /// <summary>A horizontal rule.</summary>
    public void Rule(double x1, double y, double x2, double thickness, PdfColor? color = null)
        => Line(x1, y, x2, y, thickness, color);

    /// <summary>A straight line.</summary>
    public void Line(double x1, double y1, double x2, double y2, double thickness, PdfColor? color = null)
    {
        var c = color ?? PdfColor.Black;
        _ops.Append($"{N(c.R)} {N(c.G)} {N(c.B)} RG {N(thickness)} w ");
        _ops.Append($"{N(x1)} {N(Flip(y1))} m {N(x2)} {N(Flip(y2))} l S\n");
    }

    /// <summary>A filled rectangle, positioned by its top-left corner.</summary>
    public void FilledRect(double x, double y, double width, double height, PdfColor color)
    {
        _ops.Append($"{N(color.R)} {N(color.G)} {N(color.B)} rg ");
        // PDF's `re` takes the BOTTOM-left corner, so the flipped y is the bottom
        // edge of the box: top-down y plus its height.
        _ops.Append($"{N(x)} {N(Flip(y + height))} {N(width)} {N(height)} re f\n");
    }

    /// <summary>
    /// A filled rectangle with rounded corners, positioned by its top-left corner.
    /// </summary>
    /// <remarks>
    /// PDF has no rounded rectangle, so each corner is a cubic Bezier. The magic
    /// 0.5523 is the standard control-point ratio that makes a Bezier
    /// indistinguishable from a quarter circle; anything else gives corners that
    /// look subtly wrong without looking obviously wrong.
    /// </remarks>
    public void RoundedRect(
        double x, double y, double width, double height, double radius,
        PdfColor? fill = null, PdfColor? stroke = null, double strokeWidth = 0.6)
    {
        var r = Math.Min(radius, Math.Min(width, height) / 2);
        const double K = 0.5523;
        var k = r * K;

        // Work in PDF's own upward y for the curve maths: bottom edge, then round
        // anticlockwise. Mixing a flipped y into control points is how these end up
        // mirrored.
        var x0 = x;
        var x1 = x + width;
        var y0 = Flip(y + height); // bottom
        var y1 = Flip(y);          // top

        if (fill is { } f) _ops.Append($"{N(f.R)} {N(f.G)} {N(f.B)} rg ");
        if (stroke is { } s) _ops.Append($"{N(s.R)} {N(s.G)} {N(s.B)} RG {N(strokeWidth)} w ");

        _ops.Append($"{N(x0 + r)} {N(y0)} m ");
        _ops.Append($"{N(x1 - r)} {N(y0)} l ");
        _ops.Append($"{N(x1 - r + k)} {N(y0)} {N(x1)} {N(y0 + r - k)} {N(x1)} {N(y0 + r)} c ");
        _ops.Append($"{N(x1)} {N(y1 - r)} l ");
        _ops.Append($"{N(x1)} {N(y1 - r + k)} {N(x1 - r + k)} {N(y1)} {N(x1 - r)} {N(y1)} c ");
        _ops.Append($"{N(x0 + r)} {N(y1)} l ");
        _ops.Append($"{N(x0 + r - k)} {N(y1)} {N(x0)} {N(y1 - r + k)} {N(x0)} {N(y1 - r)} c ");
        _ops.Append($"{N(x0)} {N(y0 + r)} l ");
        _ops.Append($"{N(x0)} {N(y0 + r - k)} {N(x0 + r - k)} {N(y0)} {N(x0 + r)} {N(y0)} c ");

        _ops.Append(fill is not null && stroke is not null ? "B\n" : fill is not null ? "f\n" : "S\n");
    }

    /// <summary>A rectangle outline.</summary>
    public void StrokedRect(double x, double y, double width, double height, double thickness, PdfColor color)
    {
        _ops.Append($"{N(color.R)} {N(color.G)} {N(color.B)} RG {N(thickness)} w ");
        _ops.Append($"{N(x)} {N(Flip(y + height))} {N(width)} {N(height)} re S\n");
    }

    /// <summary>
    /// Text rotated about its own start point — the diagonal DRAFT stamp.
    /// </summary>
    /// <param name="degrees">Counter-clockwise, so 45 reads bottom-left to top-right.</param>
    public void RotatedText(
        double x, double y, double degrees, PdfFont font, double sizePt, string text,
        PdfColor color, double tracking = 0)
    {
        var encoded = WinAnsi.Encode(text);
        if (encoded.Bytes.Length == 0) return;

        var rad = degrees * Math.PI / 180.0;
        var cos = Math.Cos(rad);
        var sin = Math.Sin(rad);
        // Centre the run on the anchor point by stepping back along its own baseline.
        var half = PdfFonts.Measure(font, encoded.Bytes, sizePt, tracking) / 2;
        var ox = x - half * cos;
        var oy = Flip(y) - half * sin;

        _ops.Append("BT ");
        _ops.Append($"{N(color.R)} {N(color.G)} {N(color.B)} rg ");
        _ops.Append($"/{PdfFonts.ResourceName(font)} {N(sizePt)} Tf ");
        if (tracking != 0) _ops.Append($"{N(tracking)} Tc ");
        _ops.Append($"{N(cos)} {N(sin)} {N(-sin)} {N(cos)} {N(ox)} {N(oy)} Tm ");
        _ops.Append(WinAnsi.Literal(encoded.Bytes));
        _ops.Append(" Tj ");
        if (tracking != 0) _ops.Append("0 Tc ");
        _ops.Append("ET\n");
    }
}
