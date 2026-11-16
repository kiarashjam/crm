using System.Globalization;
using System.Text;

namespace ACI.Application.Common;

/// <summary>
/// Writes a CSV file a spreadsheet opens safely, with the same rules as the browser's
/// exports (src/app/lib/exportCsv.ts).
/// </summary>
/// <remarks>
/// A spreadsheet runs a cell that starts with = + - or @ as a formula, and a leading tab or
/// carriage return can hide that start. Names and notes reach the CRM from public forms and
/// webhooks, so a "name" such as =HYPERLINK(...) would become a live link in a colleague's
/// Excel. Such a text cell is written with a leading quote, which the spreadsheet shows as
/// text. The file starts with a byte-order mark so Excel reads it as UTF-8.
/// <para>
/// Columns are split by a comma, or by a semicolon for a workspace that chose it (Settings,
/// Regional): Excel set up for Switzerland, and most of continental Europe, splits a CSV it
/// opens on semicolons, so a comma file opened there is one long column.
/// </para>
/// </remarks>
public sealed class CsvExport
{
    public const char Comma = ',';
    public const char Semicolon = ';';

    private readonly StringBuilder _text = new();
    private readonly char _separator;

    public CsvExport(params string[] headers) : this(Comma, headers) { }

    /// <param name="separator"><see cref="Comma"/> or <see cref="Semicolon"/>.</param>
    /// <param name="headers">The first row.</param>
    public CsvExport(char separator, params string[] headers)
    {
        _separator = separator == Semicolon ? Semicolon : Comma;
        Row(headers);
    }

    /// <summary>The separator for a workspace's choice: a semicolon when it chose one, else a comma.</summary>
    public static char SeparatorFor(bool semicolon) => semicolon ? Semicolon : Comma;

    public CsvExport Row(params string?[] cells)
    {
        for (var i = 0; i < cells.Length; i++)
        {
            if (i > 0) _text.Append(_separator);
            _text.Append(Cell(cells[i], _separator));
        }
        _text.Append("\r\n");
        return this;
    }

    /// <summary>
    /// A row of text and numbers: a decimal or an int is written as a number, never given the
    /// leading quote (a negative amount stays a number the spreadsheet can add up); anything
    /// else is text, made safe as <see cref="Row"/> makes it.
    /// </summary>
    public CsvExport Mixed(params object?[] cells)
    {
        for (var i = 0; i < cells.Length; i++)
        {
            if (i > 0) _text.Append(_separator);
            _text.Append(cells[i] switch
            {
                decimal d => Number(d),
                int n => n.ToString(CultureInfo.InvariantCulture),
                null => "",
                var text => Cell(text.ToString(), _separator),
            });
        }
        _text.Append("\r\n");
        return this;
    }

    /// <summary>One text cell of a comma file, made safe and quoted when it needs to be.</summary>
    public static string Cell(string? value) => Cell(value, Comma);

    /// <summary>
    /// One text cell, made safe and quoted when it holds the separator, a quote or a line break
    /// ("CHF 40000; EUR 25000" is one cell of a semicolon file).
    /// </summary>
    public static string Cell(string? value, char separator)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var s = value;
        if (s[0] is '=' or '+' or '-' or '@' or '\t' or '\r') s = "'" + s;
        return s.IndexOfAny(new[] { separator, '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    /// <summary>
    /// A number, as a number. Passed to <see cref="Row"/> it is a text cell like any other, so a
    /// negative one gets the leading quote; <see cref="Mixed"/> keeps it a number.
    /// </summary>
    public static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>A moment, in UTC, as a spreadsheet reads it: 2026-11-09 14:05.</summary>
    public static string Utc(DateTime? value) =>
        value is DateTime d ? DateTime.SpecifyKind(d, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "";

    public static string YesNo(bool value) => value ? "Yes" : "No";

    public byte[] ToBytes() => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(_text.ToString())).ToArray();
}
