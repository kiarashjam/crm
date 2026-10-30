using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Domain.Entities;

namespace ACI.Application.Services;

/// <summary>How the importer compares records and reads cells.</summary>
internal static class ImportValues
{
    private static readonly PhoneAttribute PhoneFormat = new();

    /// <summary>An email as compared: trimmed, lower case.</summary>
    public static string EmailKey(string? email) => (email ?? "").Trim().ToLowerInvariant();

    /// <summary>
    /// A phone number as compared: its last nine digits, or nothing when it has fewer than
    /// seven. "+41 79 123 45 67", "0041 79 123 45 67" and "079 123 45 67" are one number,
    /// which comparing the digits as written never found. The same rule as the browser's
    /// duplicate finder (dedupe.ts).
    /// </summary>
    public static string PhoneKey(string? phone)
    {
        var digits = new string((phone ?? "").Where(char.IsDigit).ToArray());
        return digits.Length >= 7 ? digits[^Math.Min(9, digits.Length)..] : "";
    }

    /// <summary>A name as compared: lower case, single spaces.</summary>
    public static string NameKey(string? name) =>
        string.Join(' ', (name ?? "").Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));

    /// <summary>"https://www.Acme.ch/about" is "acme.ch". Empty when nothing is left.</summary>
    public static string DomainKey(string? domainOrWebsite)
    {
        var s = (domainOrWebsite ?? "").Trim().ToLowerInvariant();
        var scheme = s.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0) s = s[(scheme + 3)..];
        if (s.StartsWith("www.", StringComparison.Ordinal)) s = s[4..];
        var end = s.IndexOfAny(new[] { '/', '?', '#', ':' });
        if (end >= 0) s = s[..end];
        return s.TrimEnd('.');
    }

    public static bool IsPhone(string value) => PhoneFormat.IsValid(value) && ValidationHelper.IsValidPhone(value);

    /// <summary>A yes or a no as people write them in English, German, French and Italian; null for anything else.</summary>
    public static bool? YesNo(string value) => value.Trim().ToLowerInvariant() switch
    {
        "yes" or "y" or "true" or "1" or "x" or "ja" or "j" or "oui" or "o" or "si" or "sì" or "✓" or "✔" => true,
        "no" or "n" or "false" or "0" or "nein" or "non" or "-" => false,
        _ => null,
    };

    /// <summary>
    /// A cell for a custom field, as the record's own form would send it: a number or a
    /// tick for those types, text otherwise. Then checked by the same rule a save is
    /// (<see cref="CustomFieldService.Normalize"/>), so an import can store nothing a
    /// person could not. Null value with an error when the cell does not fit.
    /// </summary>
    public static (JsonElement? Value, string? Error) Custom(CustomFieldDefinition definition, string text)
    {
        JsonElement raw;
        switch (definition.Type)
        {
            case CustomFieldTypes.Number:
                if (!TryNumber(text, out var number)) return (null, $"{definition.Label} must be a number");
                raw = JsonSerializer.SerializeToElement(number);
                break;
            case CustomFieldTypes.Checkbox:
                if (YesNo(text) is not { } tick) return (null, $"{definition.Label} must be yes or no");
                raw = JsonSerializer.SerializeToElement(tick);
                break;
            case CustomFieldTypes.Date:
                if (!TryDate(text, out var date))
                    return (null, $"{definition.Label} must be a date such as 2025-12-31 or 31.12.2025");
                raw = JsonSerializer.SerializeToElement(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                break;
            case CustomFieldTypes.Select:
                // The options as written in the workspace, whatever the letter case in the file.
                var options = definition.OptionsJson == null
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(definition.OptionsJson) ?? new List<string>();
                var option = options.FirstOrDefault(o => string.Equals(o, text, StringComparison.OrdinalIgnoreCase));
                if (option == null) return (null, $"{definition.Label} must be one of: {string.Join(", ", options)}");
                raw = JsonSerializer.SerializeToElement(option);
                break;
            default:
                raw = JsonSerializer.SerializeToElement(text);
                break;
        }

        var (ok, value, expected) = CustomFieldService.Normalize(definition, raw);
        return ok ? (value, null) : (null, $"{definition.Label} {expected}");
    }

    /// <summary>
    /// A number as a spreadsheet may hold it: "1234.5", "1'234.50", "1 234,50". Only
    /// digits, separators and a leading sign; "1000-2000" or "about 5" is not a number.
    /// </summary>
    private static bool TryNumber(string text, out decimal number)
    {
        number = 0;
        var t = text.Trim();
        var body = t.StartsWith('-') || t.StartsWith('+') ? t[1..] : t;
        if (body.Length == 0 || !body.All(c => char.IsDigit(c) || c is '.' or ',' or '\'' or '’' or ' ' or ' ' or ' ')) return false;
        if (!MoneyText.TryParseAmount(body, out var magnitude)) return false;
        number = t.StartsWith('-') ? -magnitude : magnitude;
        return true;
    }

    /// <summary>
    /// A date written as 2025-12-31, as 31.12.2025 (how Excel writes it in Switzerland), or
    /// with slashes when only one reading is possible. 03/04/2025 could be either and is refused.
    /// </summary>
    private static bool TryDate(string text, out DateOnly date)
    {
        var t = text.Trim();
        // A time after the date (2025-12-31T09:00, 31.12.2025 09:00) says nothing about the day.
        var space = t.IndexOfAny(new[] { ' ', 'T' });
        if (space > 0) t = t[..space];
        if (DateOnly.TryParseExact(t, new[] { "yyyy-MM-dd", "yyyy-M-d" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;
        if (DateOnly.TryParseExact(t, new[] { "dd.MM.yyyy", "d.M.yyyy", "dd.MM.yy", "d.M.yy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return true;

        var parts = t.Split('/');
        if (parts.Length == 3 && int.TryParse(parts[0], out var a) && int.TryParse(parts[1], out var b) && int.TryParse(parts[2], out var y) && y >= 1000)
        {
            // Day first (31/12/2025) or month first (12/31/2025), whichever is the only one that can be.
            if (a > 12 && b <= 12) return TryMake(y, b, a, out date);
            if (b > 12 && a <= 12) return TryMake(y, a, b, out date);
        }
        date = default;
        return false;
    }

    private static bool TryMake(int year, int month, int day, out DateOnly date)
    {
        date = default;
        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month)) return false;
        date = new DateOnly(year, month, day);
        return true;
    }

    /// <summary>A value as shown in a list of changes: in quotes, and cut short when long.</summary>
    public static string Show(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "empty";
        var oneLine = value.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
        return oneLine.Length <= 60 ? $"“{oneLine}”" : $"“{oneLine[..57]}…”";
    }
}
