using System.Globalization;
using System.Text;

namespace ACI.Application.Common;

/// <summary>
/// How two records are compared when looking for duplicates: an email, a phone, a name, a
/// web domain, each written one way. The browser's demo mode uses the same rules
/// (src/app/api/dedupe.ts).
/// </summary>
public static class MatchKeys
{
    /// <summary>"  Ada@Example.CH " and "ada@example.ch" are one address. Empty says nothing.</summary>
    public static string? Email(string? value)
    {
        var v = value?.Trim().ToLowerInvariant();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    /// <summary>
    /// The last nine digits, so "+41 79 123 45 67", "0041 79 123 45 67" and "079 123 45 67"
    /// are one number. Fewer than seven digits says nothing (an extension, a typo).
    /// </summary>
    public static string? Phone(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length < 7) return null;
        return digits.Length > 9 ? digits[^9..] : digits;
    }

    /// <summary>
    /// Lower case, no accents, single spaces: "  Zoë   Martin" and "zoe martin" are one name.
    /// A name of fewer than two letters says nothing.
    /// </summary>
    public static string? Name(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var plain = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            plain.Append(char.IsWhiteSpace(ch) ? ' ' : ch);
        }
        var name = string.Join(' ', plain.ToString().Normalize(NormalizationForm.FormC).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (name.Length < 2) return null;
        return name.Length > 256 ? name[..256] : name;
    }

    /// <summary>"https://www.Acme.ch/about" and "acme.ch" are one domain.</summary>
    public static string? Domain(string? value)
    {
        var v = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(v)) return null;
        var scheme = v.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0) v = v[(scheme + 3)..];
        if (v.StartsWith("www.", StringComparison.Ordinal)) v = v[4..];
        var end = v.IndexOfAny(new[] { '/', '?', '#' });
        if (end >= 0) v = v[..end];
        var port = v.LastIndexOf(':');
        if (port >= 0) v = v[..port];
        return string.IsNullOrEmpty(v) ? null : v;
    }
}
