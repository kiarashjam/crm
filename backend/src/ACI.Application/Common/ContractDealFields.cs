using System.Globalization;
using System.Text;

namespace ACI.Application.Common;

/// <summary>One of a deal's lines, as a contract's fee table prints it.</summary>
/// <param name="VatRate">The VAT rate in percent the unit price includes; null when none was given.</param>
public sealed record ContractFeeLine(string Name, decimal Quantity, decimal UnitPrice, decimal? VatRate);

/// <summary>
/// The deal behind a contract, as merge fields a template can draw on: what is being sold,
/// line by line, for how much, and the VAT inside it, all in the deal's currency.
/// </summary>
/// <remarks>
/// <para>
/// The fields are <c>deal.name</c>, <c>deal.value</c> (the deal's amount with its currency),
/// <c>deal.currency</c>, <c>deal.lineItems</c> (a fee table), <c>deal.total</c> (the lines'
/// total, or the deal's value when it has no lines) and <c>deal.vatIncluded</c> (the VAT inside
/// the lines, per rate). A field with nothing behind it is left out, so the template shows it
/// unresolved and the contract cannot be sent until someone fills it, as every other field.
/// </para>
/// <para>
/// The fee table is plain text, because the body is the instrument: it is frozen, hashed and
/// signed character for character, and shown as it is on the signing page. Its columns are
/// separated by "|" and padded, so it reads as a table in any fixed-width view, and the PDF
/// sets it as one (ContractBlocks reads the bars as columns).
/// </para>
/// <para>
/// Prices include their VAT, as the club's catalogue and charges do: a line's total is what
/// the customer pays for it, the deal's value stays the sum of what they pay, and the VAT
/// printed is the part of each rate's total that is VAT (amount × rate ÷ (100 + rate)).
/// </para>
/// </remarks>
public static class ContractDealFields
{
    /// <summary>The merge fields for a deal, in the workspace's number format.</summary>
    public static Dictionary<string, string?> For(string? dealName, decimal? amount, string? currency,
        IReadOnlyList<ContractFeeLine> lines, CultureInfo culture)
    {
        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        var code = string.IsNullOrWhiteSpace(currency) ? null : currency.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(dealName)) values["deal.name"] = dealName.Trim();
        if (code != null) values["deal.currency"] = code;
        if (code != null && amount is { } value) values["deal.value"] = Money(value, code, culture);
        if (code != null && lines.Count > 0)
        {
            values["deal.lineItems"] = FeeTable(lines, code, culture);
            values["deal.total"] = Money(Total(lines), code, culture);
            var vat = VatByRate(lines);
            if (vat.Count > 0)
                values["deal.vatIncluded"] = string.Join(", ", vat.Select(v => $"{Money(v.Vat, code, culture)} ({Rate(v.Rate, culture)})"));
        }
        else if (code != null && amount is { } only)
        {
            values["deal.total"] = Money(only, code, culture);
        }
        return values;
    }

    /// <summary>"CHF 4,800.00", in <paramref name="culture"/>'s digits.</summary>
    public static string Money(decimal amount, string currency, CultureInfo culture) =>
        $"{(amount < 0 ? "-" : "")}{currency} {Math.Abs(amount).ToString("N2", culture)}";

    /// <summary>What the lines add up to, each line's total rounded to the cent.</summary>
    public static decimal Total(IReadOnlyList<ContractFeeLine> lines) => lines.Sum(LineTotal);

    /// <summary>A line's total: quantity times unit price, to the cent.</summary>
    public static decimal LineTotal(ContractFeeLine line) =>
        Math.Round(line.Quantity * line.UnitPrice, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The VAT inside the lines, per rate: each rate's total of line totals, times rate over
    /// 100 + rate, to the cent. Lines with no rate are left out; a 0 % rate is listed with none.
    /// </summary>
    public static IReadOnlyList<(decimal Rate, decimal Vat)> VatByRate(IReadOnlyList<ContractFeeLine> lines) =>
        lines.Where(l => l.VatRate is > 0)
            .GroupBy(l => l.VatRate!.Value)
            .OrderByDescending(g => g.Key)
            .Select(g => (g.Key, Math.Round(g.Sum(LineTotal) * g.Key / (100 + g.Key), 2, MidpointRounding.AwayFromZero)))
            .ToList();

    /// <summary>
    /// The lines as a plain-text table: item, quantity, unit price, VAT (when any line has a
    /// rate) and total, then the total and the VAT it includes.
    /// </summary>
    public static string FeeTable(IReadOnlyList<ContractFeeLine> lines, string currency, CultureInfo culture)
    {
        var withVat = lines.Any(l => l.VatRate != null);
        var header = withVat
            ? new[] { "Item", "Qty", $"Unit price ({currency})", "VAT", $"Total ({currency})" }
            : new[] { "Item", "Qty", $"Unit price ({currency})", $"Total ({currency})" };
        var rows = new List<string[]>();
        foreach (var line in lines)
        {
            var cells = new List<string>
            {
                OneLine(line.Name),
                line.Quantity.ToString("0.####", culture),
                line.UnitPrice.ToString("N2", culture),
            };
            if (withVat) cells.Add(line.VatRate is { } rate ? Rate(rate, culture) : "-");
            cells.Add(LineTotal(line).ToString("N2", culture));
            rows.Add(cells.ToArray());
        }
        var totals = new List<string[]> { Footer(header.Length, "Total", Total(lines).ToString("N2", culture)) };
        foreach (var (rate, vat) in VatByRate(lines))
            totals.Add(Footer(header.Length, $"VAT {Rate(rate, culture)} included", vat.ToString("N2", culture)));

        var all = rows.Prepend(header).Concat(totals).ToList();
        var widths = Enumerable.Range(0, header.Length).Select(c => all.Max(r => r[c].Length)).ToArray();
        string Row(string[] cells) => string.Join(" | ", cells.Select((cell, c) => c == 0 ? cell.PadRight(widths[c]) : cell.PadLeft(widths[c]))).TrimEnd();
        var rule = string.Join("-+-", widths.Select(w => new string('-', w)));

        var table = new StringBuilder();
        table.Append(Row(header)).Append('\n').Append(rule).Append('\n');
        foreach (var row in rows) table.Append(Row(row)).Append('\n');
        table.Append(rule);
        foreach (var row in totals) table.Append('\n').Append(Row(row));
        // Said once, under the table, and only when a rate is printed: prices are what the
        // customer pays, and a reader of a Swiss contract expects to be told VAT is inside them.
        if (withVat) table.Append("\n\nPrices include VAT at the rates shown.");
        return table.ToString();
    }

    private static string[] Footer(int columns, string label, string amount)
    {
        var cells = Enumerable.Repeat("", columns).ToArray();
        cells[0] = label;
        cells[^1] = amount;
        return cells;
    }

    /// <summary>"8.1%".</summary>
    public static string Rate(decimal rate, CultureInfo culture) => rate.ToString("0.##", culture) + "%";

    /// <summary>
    /// A line's name on one line of the table, with no bar in it to split its cell: a name typed
    /// with a "|" or a line break would otherwise make a column of its own.
    /// </summary>
    private static string OneLine(string name) =>
        string.Join(' ', name.Replace('|', '/').Split(LineBreaks, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim())).Trim();

    private static readonly char[] LineBreaks = ['\r', '\n', '\t'];

    /// <summary>The workspace's number format ("de-CH"), or the invariant one when it is not a culture this server knows.</summary>
    public static CultureInfo CultureOf(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale)) return CultureInfo.InvariantCulture;
        try
        {
            return CultureInfo.GetCultureInfo(locale.Trim());
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
