using System.Text;

namespace ACI.Application.Common.Payments;

/// <summary>
/// International bank account numbers: reading one as typed (with or without spaces), its
/// check digits (ISO 13616, modulo 97), and whether it is a Swiss QR-IBAN.
/// </summary>
public static class Iban
{
    /// <summary>The IBAN without spaces, in capitals.</summary>
    public static string Normalize(string? raw)
    {
        var builder = new StringBuilder();
        foreach (var c in raw ?? "")
            if (!char.IsWhiteSpace(c) && c != '-') builder.Append(char.ToUpperInvariant(c));
        return builder.ToString();
    }

    /// <summary>
    /// Whether the text is an IBAN whose check digits are right: two letters, two digits and up
    /// to thirty letters or digits, 21 characters for Switzerland and Liechtenstein.
    /// </summary>
    public static bool IsValid(string? raw)
    {
        var iban = Normalize(raw);
        if (iban.Length is < 15 or > 34) return false;
        if (!char.IsAsciiLetterUpper(iban[0]) || !char.IsAsciiLetterUpper(iban[1])) return false;
        if (!char.IsAsciiDigit(iban[2]) || !char.IsAsciiDigit(iban[3])) return false;
        if (iban.Any(c => !char.IsAsciiLetterUpper(c) && !char.IsAsciiDigit(c))) return false;
        if (iban.StartsWith("CH", StringComparison.Ordinal) || iban.StartsWith("LI", StringComparison.Ordinal))
            if (iban.Length != 21) return false;
        return Mod97(iban[4..] + iban[..4]) == 1;
    }

    /// <summary>Whether it is a valid Swiss or Liechtenstein IBAN, the only ones a QR-bill takes.</summary>
    public static bool IsSwiss(string? raw)
    {
        var iban = Normalize(raw);
        return IsValid(iban) && (iban.StartsWith("CH", StringComparison.Ordinal) || iban.StartsWith("LI", StringComparison.Ordinal));
    }

    /// <summary>
    /// Whether it is a QR-IBAN: a Swiss IBAN whose bank number (the institution id, its fifth
    /// to ninth characters) is from 30000 to 31999. A QR-bill to one carries a QR reference.
    /// </summary>
    public static bool IsQrIban(string? raw)
    {
        var iban = Normalize(raw);
        if (!IsSwiss(iban)) return false;
        return int.TryParse(iban.AsSpan(4, 5), out var institution) && institution is >= 30000 and <= 31999;
    }

    /// <summary>The IBAN in groups of four, as it is printed: "CH44 3199 9123 0008 8901 2".</summary>
    public static string Format(string? raw)
    {
        var iban = Normalize(raw);
        var builder = new StringBuilder();
        for (var i = 0; i < iban.Length; i++)
        {
            if (i > 0 && i % 4 == 0) builder.Append(' ');
            builder.Append(iban[i]);
        }
        return builder.ToString();
    }

    /// <summary>
    /// The remainder modulo 97 of the number the text stands for, each letter read as two digits
    /// (A = 10 … Z = 35), worked a digit at a time so no number grows past what an int holds.
    /// </summary>
    internal static int Mod97(string text)
    {
        var remainder = 0;
        foreach (var c in text)
        {
            if (char.IsAsciiDigit(c)) remainder = (remainder * 10 + (c - '0')) % 97;
            else if (char.IsAsciiLetterUpper(c)) remainder = (remainder * 100 + (c - 'A' + 10)) % 97;
            else return -1;
        }
        return remainder;
    }
}

/// <summary>
/// The payment references a Swiss QR-bill carries: a QR reference (QRR, 27 digits ending in a
/// check digit, only with a QR-IBAN) and an ISO 11649 creditor reference (SCOR, "RF", two check
/// digits and up to 21 letters or digits, with a plain IBAN).
/// </summary>
public static class PaymentReferences
{
    private static readonly int[] Mod10Table = [0, 9, 4, 6, 8, 2, 7, 1, 3, 5];

    /// <summary>The check digit of a QR reference: modulo 10, recursive, as the old orange slip used.</summary>
    public static int QrCheckDigit(string digits)
    {
        var carry = 0;
        foreach (var c in digits)
        {
            if (!char.IsAsciiDigit(c)) throw new ArgumentException("A QR reference is digits only.", nameof(digits));
            carry = Mod10Table[(carry + (c - '0')) % 10];
        }
        return (10 - carry) % 10;
    }

    /// <summary>
    /// The QR reference for <paramref name="number"/>: the bank's prefix if it gave one, the number
    /// padded with zeros to 26 digits in all, and the check digit. Null when they do not fit.
    /// </summary>
    public static string? QrReference(string? prefix, long number)
    {
        var start = (prefix ?? "").Trim();
        if (start.Any(c => !char.IsAsciiDigit(c)) || number < 0) return null;
        var digits = number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (start.Length + digits.Length > 26) return null;
        var body = start + digits.PadLeft(26 - start.Length, '0');
        return body + QrCheckDigit(body);
    }

    /// <summary>Whether the text is a QR reference: 27 digits whose last is the check digit of the rest.</summary>
    public static bool IsQrReference(string? raw)
    {
        var reference = Compact(raw);
        return reference.Length == 27 && reference.All(char.IsAsciiDigit) && QrCheckDigit(reference[..26]) == reference[26] - '0';
    }

    /// <summary>The ISO 11649 creditor reference for a body of letters and digits: "RF", its check digits, the body.</summary>
    public static string CreditorReference(string body)
    {
        var text = Compact(body);
        if (text.Length is 0 or > 21 || text.Any(c => !char.IsAsciiLetterUpper(c) && !char.IsAsciiDigit(c)))
            throw new ArgumentException("A creditor reference's body is 1 to 21 letters or digits.", nameof(body));
        var check = 98 - Iban.Mod97(text + "RF00");
        return $"RF{check:00}{text}";
    }

    /// <summary>The creditor reference for <paramref name="number"/>, its digits padded to ten.</summary>
    public static string CreditorReference(long number) =>
        CreditorReference(number.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(10, '0'));

    /// <summary>Whether the text is an ISO 11649 creditor reference with the right check digits.</summary>
    public static bool IsCreditorReference(string? raw)
    {
        var reference = Compact(raw);
        if (reference.Length is < 5 or > 25 || !reference.StartsWith("RF", StringComparison.Ordinal)) return false;
        if (!char.IsAsciiDigit(reference[2]) || !char.IsAsciiDigit(reference[3])) return false;
        if (reference.Any(c => !char.IsAsciiLetterUpper(c) && !char.IsAsciiDigit(c))) return false;
        return Iban.Mod97(reference[4..] + reference[..4]) == 1;
    }

    /// <summary>The reference without spaces, in capitals, as a bank file or a person may write it.</summary>
    public static string Compact(string? raw)
    {
        var builder = new StringBuilder();
        foreach (var c in raw ?? "")
            if (!char.IsWhiteSpace(c)) builder.Append(char.ToUpperInvariant(c));
        return builder.ToString();
    }

    /// <summary>As printed on the slip: a QR reference as 2 digits then groups of 5, a creditor reference in groups of 4.</summary>
    public static string Format(string? raw)
    {
        var reference = Compact(raw);
        var builder = new StringBuilder();
        if (reference.Length == 27 && reference.All(char.IsAsciiDigit))
        {
            builder.Append(reference[..2]);
            for (var i = 2; i < reference.Length; i += 5) builder.Append(' ').Append(reference.AsSpan(i, Math.Min(5, reference.Length - i)));
            return builder.ToString();
        }
        for (var i = 0; i < reference.Length; i++)
        {
            if (i > 0 && i % 4 == 0) builder.Append(' ');
            builder.Append(reference[i]);
        }
        return builder.ToString();
    }
}
