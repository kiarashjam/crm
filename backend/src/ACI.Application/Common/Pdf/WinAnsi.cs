using System.Text;

namespace ACI.Application.Common.Pdf;

/// <summary>
/// Text encoded into the single-byte encoding the standard fonts are addressed by.
/// </summary>
/// <param name="Bytes">The WinAnsi bytes, ready to be measured and written.</param>
/// <param name="Unmapped">
/// Characters WinAnsi cannot represent, in the order they appeared, without
/// duplicates. Empty for every Western European name; non-empty for, say, a
/// Cyrillic or Chinese one.
/// </param>
public readonly record struct WinAnsiText(byte[] Bytes, IReadOnlyList<char> Unmapped);

/// <summary>
/// The encoding the PDF standard fonts are addressed by.
/// </summary>
/// <remarks>
/// <para>
/// WinAnsiEncoding is code page 1252, and .NET 8 does not carry 1252 without an
/// extra package — only Latin-1. Latin-1 is not enough: it has no em dash, no
/// curly quotes and no Euro sign, and this system generates titles like
/// "Membership Agreement — Jean Dupont" with a real em dash. So the mapping is
/// written out here. It is small: identity for ASCII and for Latin-1's upper half,
/// plus the twenty-seven typographic characters Microsoft put in the 0x80-0x9F
/// range that Latin-1 leaves as control codes.
/// </para>
/// <para>
/// What it CANNOT represent, it reports rather than hides. A contract that quietly
/// renders a member's name as "?????" is worse than one that refuses, so the caller
/// is told and can decide — see <see cref="ContractPdf"/>, which logs it and marks
/// the document. Accented Latin characters are all representable, which covers the
/// French, German and Italian names this is actually used for.
/// </para>
/// </remarks>
public static class WinAnsi
{
    /// <summary>Stands in for a character WinAnsi has no glyph for.</summary>
    private const byte Substitute = (byte)'?';

    /// <summary>
    /// The characters WinAnsi puts in 0x80-0x9F, which Latin-1 leaves undefined.
    /// </summary>
    private static readonly Dictionary<char, byte> HighRange = new()
    {
        ['\u20AC'] = 0x80, // euro
        ['\u201A'] = 0x82, // single low-9 quote
        ['\u0192'] = 0x83, // florin
        ['\u201E'] = 0x84, // double low-9 quote
        ['\u2026'] = 0x85, // ellipsis
        ['\u2020'] = 0x86, // dagger
        ['\u2021'] = 0x87, // double dagger
        ['\u02C6'] = 0x88, // modifier circumflex
        ['\u2030'] = 0x89, // per mille
        ['\u0160'] = 0x8A, // S caron
        ['\u2039'] = 0x8B, // single left angle quote
        ['\u0152'] = 0x8C, // OE ligature
        ['\u017D'] = 0x8E, // Z caron
        ['\u2018'] = 0x91, // left single quote
        ['\u2019'] = 0x92, // right single quote (apostrophe)
        ['\u201C'] = 0x93, // left double quote
        ['\u201D'] = 0x94, // right double quote
        ['\u2022'] = 0x95, // bullet
        ['\u2013'] = 0x96, // en dash
        ['\u2014'] = 0x97, // em dash
        ['\u02DC'] = 0x98, // small tilde
        ['\u2122'] = 0x99, // trademark
        ['\u0161'] = 0x9A, // s caron
        ['\u203A'] = 0x9B, // single right angle quote
        ['\u0153'] = 0x9C, // oe ligature
        ['\u017E'] = 0x9E, // z caron
        ['\u0178'] = 0x9F, // Y diaeresis
    };

    /// <summary>
    /// Characters worth spelling differently rather than substituting.
    /// </summary>
    /// <remarks>
    /// A non-breaking space is a space; a soft hyphen at the end of a line we are
    /// re-wrapping anyway is nothing. Turning either into "?" would be a visible
    /// defect for an invisible cause.
    /// </remarks>
    private static readonly Dictionary<char, string> Rewrites = new()
    {
        ['\u0009'] = "    ", // tab - laid out as spaces, never as "?"
        ['\u00A0'] = " ", // no-break space
        ['\u2007'] = " ", // figure space
        ['\u202F'] = " ", // narrow no-break space, common in French amounts
        ['\u2009'] = " ", // thin space
        ['\u00AD'] = "", // soft hyphen
        ['\u200B'] = "", // zero-width space
        ['\uFEFF'] = "", // byte-order mark
        ['\u2212'] = "-", // minus sign
        ['\u2010'] = "-", // hyphen
        ['\u2011'] = "-", // non-breaking hyphen
        ['\u2012'] = "-", // figure dash, as in a year range
        ['\u00B4'] = "'", // acute accent used as an apostrophe
        ['\u2032'] = "'", // prime
        ['\u2033'] = "\"", // double prime
    };

    /// <summary>Encodes text, reporting anything it had to substitute.</summary>
    public static WinAnsiText Encode(string? text)
    {
        if (string.IsNullOrEmpty(text)) return new WinAnsiText(Array.Empty<byte>(), Array.Empty<char>());

        var bytes = new List<byte>(text.Length);
        List<char>? unmapped = null;

        foreach (var raw in text)
        {
            if (Rewrites.TryGetValue(raw, out var replacement))
            {
                foreach (var r in replacement) bytes.Add((byte)r);
                continue;
            }

            var ch = raw;

            // ASCII, then Latin-1's upper half, are the same code in WinAnsi. 0x7F
            // and 0x80-0x9F are not: Latin-1 leaves them as controls, so they are
            // handled by the table below rather than passed through.
            if (ch is >= '\u0020' and < '\u007F')
            {
                bytes.Add((byte)ch);
                continue;
            }
            if (ch is >= '\u00A1' and <= '\u00FF')
            {
                bytes.Add((byte)ch);
                continue;
            }
            if (HighRange.TryGetValue(ch, out var code))
            {
                bytes.Add(code);
                continue;
            }

            // Last chance before substituting: a composed character may decompose
            // into a base letter WinAnsi does have. "ā" becomes "a", which is a
            // better record of a name than "?".
            var folded = Fold(ch);
            if (folded is not null)
            {
                foreach (var f in folded) bytes.Add((byte)f);
                continue;
            }

            bytes.Add(Substitute);
            unmapped ??= new List<char>();
            if (!unmapped.Contains(ch)) unmapped.Add(ch);
        }

        return new WinAnsiText(
            bytes.ToArray(),
            (IReadOnlyList<char>?)unmapped ?? Array.Empty<char>());
    }

    /// <summary>
    /// The ASCII letter behind a decomposable character, or null.
    /// </summary>
    /// <remarks>
    /// Only accepted when the decomposition's base character is itself plain ASCII,
    /// so this cannot invent a letter — "ā" gives "a" and "€" gives nothing.
    /// </remarks>
    private static string? Fold(char ch)
    {
        var decomposed = ch.ToString().Normalize(NormalizationForm.FormD);
        var kept = new StringBuilder();
        foreach (var c in decomposed)
        {
            if (c is >= '\u0020' and < '\u007F') kept.Append(c);
            else if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                     != System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                return null; // something substantive we cannot represent
            }
        }
        return kept.Length > 0 ? kept.ToString() : null;
    }

    /// <summary>
    /// The nearest plain-ASCII spelling of some text.
    /// </summary>
    /// <remarks>
    /// For the places that cannot carry even Latin-1. An HTTP header is one: a
    /// <c>Content-Disposition</c> whose filename holds "Anais" with a diaeresis
    /// makes ASP.NET Core throw, which turned every PDF download for an accented
    /// counterparty name into a 500 — and accented names are most of them here.
    ///
    /// Folds by decomposition and keeps only the ASCII that falls out, so it never
    /// invents a letter: "Zurich" with an umlaut gives "Zurich", and a name with no
    /// Latin behind it gives nothing rather than mojibake.
    /// </remarks>
    public static string ToAsciiApprox(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var decomposed = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (c is >= '\u0020' and < '\u007F') sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Encoded bytes as a PDF string literal, brackets included.
    /// </summary>
    /// <remarks>
    /// Backslash, and both parentheses, end a literal early or unbalance it, so they
    /// are escaped. Everything outside printable ASCII goes out as a three-digit
    /// octal escape: legal, unambiguous, and it keeps the file itself ASCII, which
    /// makes a generated PDF diffable and readable in a terminal when something
    /// looks wrong.
    /// </remarks>
    public static string Literal(ReadOnlySpan<byte> encoded)
    {
        var sb = new StringBuilder(encoded.Length + 2);
        sb.Append('(');
        foreach (var b in encoded)
        {
            switch (b)
            {
                case (byte)'\\': sb.Append("\\\\"); break;
                case (byte)'(': sb.Append("\\("); break;
                case (byte)')': sb.Append("\\)"); break;
                default:
                    if (b is >= 0x20 and < 0x7F) sb.Append((char)b);
                    else sb.Append('\\').Append(Convert.ToString(b, 8).PadLeft(3, '0'));
                    break;
            }
        }
        sb.Append(')');
        return sb.ToString();
    }
}
