using System.IO.Compression;
using System.Text;

namespace ACI.WebApi.Tests.Helpers;

/// <summary>
/// The text runs a PDF the app built draws, read out of its (compressed) content streams: the
/// strings of its Tj operators, one per line. Enough to say what a statement printed.
/// </summary>
public static class PdfText
{
    public static string Of(byte[] pdf)
    {
        var text = new StringBuilder();
        var raw = Encoding.Latin1.GetString(pdf);
        var at = 0;
        while ((at = raw.IndexOf("stream", at, StringComparison.Ordinal)) >= 0)
        {
            var start = at + "stream".Length;
            if (raw.AsSpan(start).StartsWith("\r\n")) start += 2;
            else if (raw.AsSpan(start).StartsWith("\n")) start += 1;
            else
            {
                at = start;
                continue;
            }
            var end = raw.IndexOf("endstream", start, StringComparison.Ordinal);
            if (end < 0) break;
            var bytes = pdf.AsSpan(start, end - start).ToArray();
            string content;
            try
            {
                using var input = new MemoryStream(bytes);
                using var zlib = new ZLibStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                zlib.CopyTo(output);
                content = Encoding.Latin1.GetString(output.ToArray());
            }
            catch (InvalidDataException)
            {
                content = Encoding.Latin1.GetString(bytes);
            }
            ReadStrings(content, text);
            at = end + "endstream".Length;
        }
        return text.ToString();
    }

    private static void ReadStrings(string content, StringBuilder text)
    {
        for (var i = 0; i < content.Length; i++)
        {
            if (content[i] != '(') continue;
            var run = new StringBuilder();
            var depth = 1;
            for (i++; i < content.Length && depth > 0; i++)
            {
                var c = content[i];
                if (c == '\\' && i + 1 < content.Length)
                {
                    var next = content[++i];
                    run.Append(next switch { 'n' => '\n', 'r' => '\r', 't' => '\t', _ => next });
                    continue;
                }
                if (c == '(') depth++;
                if (c == ')' && --depth == 0) break;
                run.Append(c);
            }
            text.Append(run).Append('\n');
        }
    }
}
