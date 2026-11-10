namespace ACI.Application.Common;

using System.Globalization;

/// <summary>
/// Reading an amount out of text: the free-text <c>Deal.Value</c> of deals saved before
/// amounts were numbers, and the text older clients still send instead of an amount.
/// </summary>
/// <remarks>
/// <para>
/// A deal's money is now <c>Deal.Amount</c>, a number, with <c>Value</c> written from it
/// (<see cref="DealMoney"/>). Before that, values were stored as text, so they arrived in
/// whatever shape whoever typed them used: <c>"85,500"</c>, <c>"CHF 85 500"</c>, <c>"€50.000"</c>,
/// <c>"$1,234.56"</c>. The reporting queries previously did
/// <c>value.Replace("$", "").Replace(",", "")</c> and fell through to <c>0</c>
/// when <c>decimal.TryParse</c> failed — so every deal priced in anything but
/// dollars was silently counted as worth nothing, and the pipeline total was
/// quietly short by however much those deals were worth.
/// </para>
/// <para>
/// Failure is reported rather than coerced. A caller that cannot read an amount
/// needs to say so, because "we could not read 3 deals" and "3 deals are worth
/// nothing" are different facts and only one of them is a data-entry problem.
/// </para>
/// </remarks>
public static class MoneyText
{
    /// <summary>
    /// Parses an amount, returning false when the text holds no readable number.
    /// </summary>
    /// <remarks>
    /// Where both separators appear, the LAST one is taken as the decimal point,
    /// which resolves "1,234.56" and "1.234,56" correctly without knowing the
    /// writer's locale. A lone comma is a decimal point only when one or two
    /// digits follow it — "85,500" is eighty-five thousand, not 85.5.
    /// </remarks>
    public static bool TryParseAmount(string? raw, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        // Only a LEADING minus, or accounting parentheses, means negative. Testing
        // for '-' anywhere would negate "1000-2000" and anything with a stray dash.
        var trimmed = raw.Trim();
        var negative = trimmed.StartsWith('-') || (trimmed.StartsWith('(') && trimmed.EndsWith(')'));

        // Keep only what can carry magnitude. This drops currency symbols and
        // codes ("CHF", "€"), spaces used as thousands separators, and stray text.
        var kept = new System.Text.StringBuilder(raw.Length);
        foreach (var c in raw)
        {
            if (char.IsDigit(c) || c == '.' || c == ',') kept.Append(c);
        }
        var s = kept.ToString();
        if (s.Length == 0 || !s.Any(char.IsDigit)) return false;

        var lastDot = s.LastIndexOf('.');
        var lastComma = s.LastIndexOf(',');

        string normalised;
        if (lastDot >= 0 && lastComma >= 0)
        {
            // Both present: the later one is the decimal separator.
            var decimalAt = Math.Max(lastDot, lastComma);
            var whole = s[..decimalAt].Replace(".", string.Empty).Replace(",", string.Empty);
            var frac = s[(decimalAt + 1)..].Replace(".", string.Empty).Replace(",", string.Empty);
            normalised = frac.Length == 0 ? whole : whole + "." + frac;
        }
        else if (lastComma >= 0)
        {
            var frac = s[(lastComma + 1)..];
            normalised = frac.Length is 1 or 2
                ? s[..lastComma].Replace(",", string.Empty) + "." + frac
                : s.Replace(",", string.Empty);
        }
        else if (lastDot >= 0)
        {
            var frac = s[(lastDot + 1)..];
            // A lone dot with exactly three digits after it, and at most three
            // before, is a grouped thousands separator: "1.234", "85.500". The
            // bound on the integer part matters — without it "1234.567" became
            // 1234567. Anything else is a decimal point.
            var groupedThousands = frac.Length == 3
                && s.IndexOf('.') == lastDot
                && s[..lastDot].Length is > 0 and <= 3;
            normalised = groupedThousands
                ? s.Replace(".", string.Empty)
                : s[..lastDot].Replace(".", string.Empty) + "." + frac;
        }
        else
        {
            normalised = s;
        }

        if (normalised.Length == 0 || normalised == ".") return false;
        if (!decimal.TryParse(normalised, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        value = negative ? -parsed : parsed;
        return true;
    }

    /// <summary>The largest amount a decimal(18,2) column holds.</summary>
    private const decimal MaxStoredAmount = 9_999_999_999_999_999.99m;

    /// <summary>
    /// A deal value as the number stored next to it in <c>Deal.Amount</c>: rounded to
    /// cents, or null when the text holds no readable number.
    /// </summary>
    /// <remarks>
    /// The column is decimal(18,2). A value typed with more digits than that would make
    /// the whole save fail on SQL Server, so it is treated as unreadable instead, which
    /// is how reports already disclose a value they could not read.
    /// </remarks>
    public static decimal? StoredAmount(string? raw)
    {
        if (!TryParseAmount(raw, out var value)) return null;
        return StoredAmount(value);
    }

    /// <summary>
    /// An amount as <c>Deal.Amount</c> holds it: rounded to cents, or null when it has more
    /// digits than decimal(18,2) holds. Line items add up to totals like 99.39999999999999,
    /// which the column cannot store and nobody meant.
    /// </summary>
    public static decimal? StoredAmount(decimal value)
    {
        var cents = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        return Math.Abs(cents) > MaxStoredAmount ? null : cents;
    }

    /// <summary>
    /// The text <c>Deal.Value</c> keeps for an amount: plain digits, a point and cents only
    /// when there are any ("85500", "99.40"). Older clients, the MCP tools and outbound
    /// webhooks still read the text, and this is the one shape every parser agrees on.
    /// </summary>
    public static string ValueText(decimal amount) =>
        amount == decimal.Truncate(amount)
            ? decimal.Truncate(amount).ToString("0", CultureInfo.InvariantCulture)
            : amount.ToString("0.00", CultureInfo.InvariantCulture);
}
