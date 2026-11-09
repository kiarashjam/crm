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
/// </remarks>
public sealed class CsvExport
{
    private readonly StringBuilder _text = new();

    public CsvExport(params string[] headers) => Row(headers);

    public CsvExport Row(params string?[] cells)
    {
        for (var i = 0; i < cells.Length; i++)
        {
            if (i > 0) _text.Append(',');
            _text.Append(Cell(cells[i]));
        }
        _text.Append("\r\n");
        return this;
    }

    /// <summary>One text cell, made safe and quoted when it needs to be.</summary>
    public static string Cell(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var s = value;
        if (s[0] is '=' or '+' or '-' or '@' or '\t' or '\r') s = "'" + s;
        return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    /// <summary>A number, as a number: never given the leading quote a text cell can get.</summary>
    public static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>A moment, in UTC, as a spreadsheet reads it: 2026-11-09 14:05.</summary>
    public static string Utc(DateTime? value) =>
        value is DateTime d ? DateTime.SpecifyKind(d, DateTimeKind.Utc).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "";

    public static string YesNo(bool value) => value ? "Yes" : "No";

    public byte[] ToBytes() => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(_text.ToString())).ToArray();
}
