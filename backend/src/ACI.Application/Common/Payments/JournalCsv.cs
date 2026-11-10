using System.Globalization;
using System.Text;
using ACI.Application.Services;

namespace ACI.Application.Common.Payments;

/// <summary>
/// The accounting journal as a CSV file Bexio, Abacus and most Swiss accounting programs
/// import: one booking per line, a debit and a credit account and one amount.
/// </summary>
/// <remarks>
/// <para>
/// The layout (docs/accounting-export.md has it column by column): semicolons between fields,
/// as Swiss Excel writes them; dates as dd.MM.yyyy; amounts with a full stop and no thousands
/// separator, always positive (which account is debited says the direction); UTF-8 with a
/// byte-order mark. Text that would start a spreadsheet formula gets a leading quote, as every
/// other export here does.
/// </para>
/// <para>
/// Columns: Date; Document; Debit; Credit; Amount; Currency; VatCode; VatRate; VatAmount;
/// Text; Member; Type; EntryId.
/// </para>
/// </remarks>
public static class JournalCsv
{
    public static readonly string[] Headers =
        ["Date", "Document", "Debit", "Credit", "Amount", "Currency", "VatCode", "VatRate", "VatAmount", "Text", "Member", "Type", "EntryId"];

    public static byte[] Write(IEnumerable<JournalRowDto> rows)
    {
        var text = new StringBuilder();
        Line(text, Headers);
        foreach (var row in rows)
            Line(text,
            [
                row.Date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                row.Document,
                row.Debit,
                row.Credit,
                Number(row.Amount),
                row.Currency,
                row.VatCode ?? "",
                row.VatRate is { } rate ? rate.ToString("0.0#", CultureInfo.InvariantCulture) : "",
                row.VatAmount is { } vat ? Number(vat) : "",
                row.Text,
                row.Member ?? "",
                row.Type,
                row.EntryId.ToString(),
            ]);
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text.ToString())).ToArray();
    }

    private static string Number(decimal value) => value.ToString("0.00", CultureInfo.InvariantCulture);

    private static void Line(StringBuilder text, IReadOnlyList<string> cells)
    {
        for (var i = 0; i < cells.Count; i++)
        {
            if (i > 0) text.Append(';');
            text.Append(Cell(cells[i]));
        }
        text.Append("\r\n");
    }

    /// <summary>One cell: kept from being read as a formula, and quoted when it holds the separator, a quote or a line break.</summary>
    public static string Cell(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var s = value;
        if (s[0] is '=' or '+' or '-' or '@' or '\t' or '\r') s = "'" + s;
        return s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
