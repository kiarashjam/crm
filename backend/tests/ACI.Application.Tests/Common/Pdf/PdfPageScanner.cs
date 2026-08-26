using System.Text;
using ACI.Application.Common.Pdf;

namespace ACI.Application.Tests.Common.Pdf;

/// <summary>One run of text found in a generated PDF, with where it was drawn.</summary>
/// <param name="Page">1-based page number.</param>
/// <param name="X">Left edge, in points from the left of the page.</param>
/// <param name="Y">Baseline, in points from the BOTTOM — PDF's own direction.</param>
/// <param name="Width">Measured width, including tracking and word spacing.</param>
public readonly record struct DrawnText(
    int Page, double X, double Y, double Width, string Text, PdfFont Font, double Size);

/// <summary>
/// Reads text runs back out of a generated PDF.
/// </summary>
/// <remarks>
/// <para>
/// This exists for one assertion that cannot be made any other way: that no text
/// overruns the margin. Checking the wrapper only proves the wrapper agrees with
/// itself — every overrun this codebase has actually produced came from a caller
/// that measured one thing and drew another, or truncated by counting characters
/// instead of points.
/// </para>
/// <para>
/// It is a deliberately small parser. It understands the operators this writer
/// emits and nothing else, and it ignores rotated runs, because a matrix that is
/// not the identity translation is the watermark and is meant to leave the text
/// area.
/// </para>
/// </remarks>
public static class PdfPageScanner
{
    public static List<DrawnText> Scan(byte[] pdf)
    {
        var runs = new List<DrawnText>();
        var page = 0;

        foreach (var content in ContentStreams(pdf))
        {
            page++;
            var font = PdfFont.Helvetica;
            var size = 0.0;
            var tracking = 0.0;
            var wordSpacing = 0.0;
            double? x = null, y = null;

            foreach (var (op, args) in Operators(content))
            {
                switch (op)
                {
                    case "Tf" when args.Count == 2:
                        font = FontFromResource(args[0]);
                        size = Number(args[1]);
                        break;
                    case "Tc" when args.Count == 1:
                        tracking = Number(args[0]);
                        break;
                    case "Tw" when args.Count == 1:
                        wordSpacing = Number(args[0]);
                        break;
                    case "Tm" when args.Count == 6:
                        // Only an upright run is positioned for the text area. A
                        // rotated one is the status stamp.
                        if (args[0] == "1" && args[1] == "0" && args[2] == "0" && args[3] == "1")
                        {
                            x = Number(args[4]);
                            y = Number(args[5]);
                        }
                        else { x = null; y = null; }
                        break;
                    case "Tj" when args.Count == 1 && x is not null && y is not null:
                    {
                        var bytes = DecodeLiteral(args[0]);
                        var width = PdfFonts.Measure(font, bytes, size, tracking)
                                    + wordSpacing * bytes.Count(b => b == (byte)' ');
                        runs.Add(new DrawnText(
                            page, x.Value, y.Value, width,
                            Encoding.Latin1.GetString(bytes), font, size));
                        break;
                    }
                }
            }
        }

        return runs;
    }

    /// <summary>Every page's content stream, in page order, uncompressed only.</summary>
    private static IEnumerable<string> ContentStreams(byte[] pdf)
    {
        var text = Encoding.Latin1.GetString(pdf);
        var at = 0;
        while (true)
        {
            var start = text.IndexOf("stream\n", at, StringComparison.Ordinal);
            if (start < 0) break;
            var from = start + "stream\n".Length;
            var end = text.IndexOf("\nendstream", from, StringComparison.Ordinal);
            if (end < 0) break;
            yield return text[from..end];
            at = end;
        }
    }

    /// <summary>Splits a content stream into operators and their preceding arguments.</summary>
    private static IEnumerable<(string Op, List<string> Args)> Operators(string content)
    {
        var args = new List<string>();
        var i = 0;
        while (i < content.Length)
        {
            var c = content[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }

            if (c == '(')
            {
                // Balanced, and a backslash escapes whatever follows it — including a
                // parenthesis, which is the whole reason the writer escapes them.
                var start = i++;
                var depth = 1;
                while (i < content.Length && depth > 0)
                {
                    if (content[i] == '\\') { i += 2; continue; }
                    if (content[i] == '(') depth++;
                    else if (content[i] == ')') depth--;
                    i++;
                }
                args.Add(content[start..i]);
                continue;
            }

            var tokenStart = i;
            while (i < content.Length && !char.IsWhiteSpace(content[i]) && content[i] != '(') i++;
            var token = content[tokenStart..i];

            if (token.Length == 0) { i++; continue; }

            // A token that could be an operator is any bare word that is not a number
            // and not a name. Numbers and /Names are arguments.
            if (token[0] == '/' || IsNumber(token))
            {
                args.Add(token);
                continue;
            }

            yield return (token, new List<string>(args));
            args.Clear();
        }
    }

    private static bool IsNumber(string token)
        => double.TryParse(token, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out _);

    private static double Number(string token)
        => double.Parse(token, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture);

    private static PdfFont FontFromResource(string name)
    {
        var bare = name.TrimStart('/');
        foreach (var font in PdfFonts.All)
        {
            if (PdfFonts.ResourceName(font) == bare) return font;
        }
        throw new InvalidOperationException($"unknown font resource {name}");
    }

    /// <summary>Turns a PDF string literal back into the bytes it stood for.</summary>
    private static byte[] DecodeLiteral(string literal)
    {
        var inner = literal.StartsWith('(') && literal.EndsWith(')')
            ? literal[1..^1]
            : literal;

        var bytes = new List<byte>(inner.Length);
        for (var i = 0; i < inner.Length; i++)
        {
            if (inner[i] != '\\') { bytes.Add((byte)inner[i]); continue; }

            i++;
            if (i >= inner.Length) break;
            var next = inner[i];
            if (next is >= '0' and <= '7')
            {
                var digits = next.ToString();
                while (digits.Length < 3 && i + 1 < inner.Length && inner[i + 1] is >= '0' and <= '7')
                {
                    digits += inner[++i];
                }
                bytes.Add(Convert.ToByte(digits, 8));
            }
            else
            {
                bytes.Add((byte)next);
            }
        }
        return bytes.ToArray();
    }
}
