using System.Text;

namespace ACI.Application.Common;

/// <summary>
/// A spreadsheet saved as text, read into a header row and data rows: what the
/// importer works from.
/// </summary>
/// <remarks>
/// <para>
/// The browser used to split the file on line breaks and then on commas. A file
/// Excel saves in Switzerland separates its columns with ";", so every row arrived
/// as a single column and nothing could be mapped; a notes cell holding a line
/// break became two broken rows; and a quote written twice inside a cell (how a
/// quote is written in CSV) was read as two quotes.
/// </para>
/// <para>
/// This reads RFC 4180: a cell in double quotes may hold the separator, line breaks
/// and doubled quotes. The separator is whichever of ";", "," and tab splits the
/// first rows into the most columns that agree with each other, unless the file
/// says itself with a first line such as "sep=;". The bytes are UTF-8, or UTF-16
/// when they start with its byte-order mark ("Unicode text" from Excel), and
/// otherwise Windows-1252, the code page Excel writes "CSV" in on a Western European
/// Windows, so "Zürich" is not read as "Z?rich".
/// </para>
/// </remarks>
public sealed class CsvFile
{
    /// <summary>The separators a file may use, in the order a tie is settled.</summary>
    private static readonly char[] Separators = { ';', ',', '\t' };

    /// <summary>How many rows the separator is judged on; a long file says nothing more after them.</summary>
    private const int RowsToJudge = 50;

    private static readonly Encoding Utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    static CsvFile()
    {
        // Windows-1252 ships with .NET but has to be switched on.
        System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private CsvFile(char separator, string encoding, IReadOnlyList<string> headers, IReadOnlyList<CsvRow> rows)
    {
        Separator = separator;
        Encoding = encoding;
        Headers = headers;
        Rows = rows;
    }

    /// <summary>The column separator the file uses.</summary>
    public char Separator { get; }

    /// <summary>How the bytes were read: "UTF-8", "UTF-16" or "Windows-1252".</summary>
    public string Encoding { get; }

    /// <summary>The first row's cells, trimmed. A blank one is named after its place, "Column 3".</summary>
    public IReadOnlyList<string> Headers { get; }

    /// <summary>The rows after the header that have anything in them, in file order.</summary>
    public IReadOnlyList<CsvRow> Rows { get; }

    /// <summary>The separator as a person would name it.</summary>
    public string SeparatorName => Separator switch
    {
        ';' => "semicolon",
        '\t' => "tab",
        _ => "comma",
    };

    /// <summary>Reads a file. Fails, saying why, when it is empty, has no header, or has more than <paramref name="maxRows"/> rows.</summary>
    public static Result<CsvFile> Read(byte[] bytes, int maxRows)
    {
        var (text, encoding) = Decode(bytes);

        // Excel honours a first line "sep=;" and does not show it as a row, so neither do we.
        char? declared = null;
        if (text.StartsWith("sep=", StringComparison.OrdinalIgnoreCase) && text.Length > 4)
        {
            var end = text.IndexOfAny(new[] { '\r', '\n' });
            var directive = (end < 0 ? text : text[..end])[4..];
            if (directive.Length == 1 && Array.IndexOf(Separators, directive[0]) >= 0)
            {
                declared = directive[0];
                var next = end < 0 ? text.Length : end + (text[end] == '\r' && end + 1 < text.Length && text[end + 1] == '\n' ? 2 : 1);
                text = text[next..];
            }
        }

        if (text.Trim().Length == 0) return DomainErrors.Import.Empty;

        var separator = declared ?? GuessSeparator(text);
        var parsed = Parse(text, separator);
        if (parsed.UnclosedAtRow is int unclosed) return DomainErrors.Import.UnclosedQuote(unclosed);

        // Row numbers are the spreadsheet's: the header is row 1, and a blank row still
        // takes its place, so "row 12" is the row a person sees as 12 when they open the file.
        var records = parsed.Records;
        var headerIndex = records.FindIndex(r => !IsBlank(r));
        if (headerIndex < 0) return DomainErrors.Import.Empty;

        var headers = records[headerIndex]
            .Select((h, i) => Clean(h) is { Length: > 0 } name ? name : $"Column {i + 1}")
            .ToList();

        var rows = new List<CsvRow>();
        for (var i = headerIndex + 1; i < records.Count; i++)
        {
            if (IsBlank(records[i])) continue;
            if (rows.Count == maxRows) return DomainErrors.Import.TooManyRows(maxRows);
            rows.Add(new CsvRow(i + 1, records[i]));
        }
        if (rows.Count == 0) return DomainErrors.Import.NoRows;

        return new CsvFile(separator, encoding, headers, rows);
    }

    /// <summary>
    /// A cell as it should be stored: without the spaces around it, including non-breaking
    /// ones, and without the quote an export puts before a cell that would start a formula.
    /// </summary>
    /// <remarks>
    /// Cadence's own export (exportCsv.ts), like most, writes a phone number as
    /// "'+41 79 123 45 67" and a name starting with "=" as "'=...", so a spreadsheet shows
    /// them as text instead of running them. Read back as written, every international
    /// number in an exported and re-imported file was refused as not a phone number. Only
    /// a quote before = + - @, a tab or a carriage return is dropped, the exact cases the
    /// export quotes; "'s-Hertogenbosch" keeps its own.
    /// </remarks>
    public static string Clean(string? cell)
    {
        var text = Trimmed(cell);
        return text.Length > 1 && text[0] == '\'' && text[1] is '=' or '+' or '-' or '@' or '\t' or '\r'
            ? Trimmed(text[1..])
            : text;
    }

    private static string Trimmed(string? cell) => (cell ?? "").Trim().Trim(' ', '﻿').Trim();

    private static (string Text, string Encoding) Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return (System.Text.Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), "UTF-8");
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return (System.Text.Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), "UTF-16");
        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            return (System.Text.Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), "UTF-16");
        try
        {
            return (Utf8Strict.GetString(bytes), "UTF-8");
        }
        catch (DecoderFallbackException)
        {
            // Not UTF-8, so a single-byte code page. Every byte means something in
            // Windows-1252, so this cannot fail.
            return (System.Text.Encoding.GetEncoding(1252).GetString(bytes), "Windows-1252");
        }
    }

    /// <summary>
    /// The separator that splits the header into the most columns, with the most of the
    /// first rows agreeing on that count. A file of one column has none to find, and is
    /// read with commas.
    /// </summary>
    private static char GuessSeparator(string text)
    {
        var best = ',';
        var bestScore = (Rows: 0, Columns: 1);
        foreach (var candidate in Separators)
        {
            var records = Parse(text, candidate, RowsToJudge + 1).Records.Where(r => !IsBlank(r)).ToList();
            if (records.Count == 0) continue;
            var columns = records[0].Count;
            if (columns < 2) continue;
            var agreeing = records.Skip(1).Count(r => r.Count == columns);
            var score = (Rows: agreeing, Columns: columns);
            if (score.Rows > bestScore.Rows || (score.Rows == bestScore.Rows && score.Columns > bestScore.Columns))
            {
                best = candidate;
                bestScore = score;
            }
        }
        return best;
    }

    /// <param name="stopAfter">Stop once this many rows are read, for a quick look at the start of a file.</param>
    private static (List<List<string>> Records, int? UnclosedAtRow) Parse(string text, char separator, int stopAfter = int.MaxValue)
    {
        var records = new List<List<string>>();
        var record = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;
        var quoted = false;

        void EndCell()
        {
            record.Add(cell.ToString());
            cell.Clear();
            quoted = false;
        }

        var i = 0;
        while (i < text.Length && records.Count < stopAfter)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    // A quote written twice inside a quoted cell is one quote.
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i += 2;
                        continue;
                    }
                    inQuotes = false;
                    i++;
                    continue;
                }
                // The separator and line breaks are part of a quoted cell.
                cell.Append(c);
                i++;
                continue;
            }

            if (c == '"' && !quoted && cell.ToString().Trim().Length == 0)
            {
                // Opens a quoted cell. Spaces some programs write before the quote are dropped.
                cell.Clear();
                inQuotes = true;
                quoted = true;
                i++;
                continue;
            }
            if (c == separator)
            {
                EndCell();
                i++;
                continue;
            }
            if (c is '\r' or '\n')
            {
                EndCell();
                records.Add(record);
                record = new List<string>();
                i += c == '\r' && i + 1 < text.Length && text[i + 1] == '\n' ? 2 : 1;
                continue;
            }
            // Anything else, including a quote in the middle of an unquoted cell
            // (5" screen), is the cell's own text.
            cell.Append(c);
            i++;
        }

        if (records.Count >= stopAfter) return (records, null);
        // A quoted cell still open at the end of the file swallowed everything after
        // its opening quote. Reading on would put the rest of the file in one cell.
        if (inQuotes) return (records, records.Count + 1);
        if (cell.Length > 0 || record.Count > 0 || quoted)
        {
            EndCell();
            records.Add(record);
        }
        return (records, null);
    }

    private static bool IsBlank(List<string> record) => record.All(c => Clean(c).Length == 0);
}

/// <summary>One data row of a <see cref="CsvFile"/>.</summary>
/// <param name="Number">Its row number in the spreadsheet, counting the header as row 1.</param>
/// <param name="Cells">Its cells as written, quotes removed. A row may have fewer or more cells than the header.</param>
public sealed record CsvRow(int Number, IReadOnlyList<string> Cells)
{
    /// <summary>The cell in this column, trimmed; empty when the row is shorter.</summary>
    public string Cell(int column) => column >= 0 && column < Cells.Count ? CsvFile.Clean(Cells[column]) : "";
}
