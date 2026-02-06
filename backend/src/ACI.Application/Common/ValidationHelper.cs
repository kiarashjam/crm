using System.Text.RegularExpressions;

namespace ACI.Application.Common;

/// <summary>
/// Helper class for validation operations.
/// </summary>
public static partial class ValidationHelper
{
    /// <summary>
    /// Email validation regex pattern following RFC 5322 simplified.
    /// </summary>
    private static readonly Regex EmailRegex = MyEmailRegex();

    /// <summary>
    /// Validates if the given string is a valid email format.
    /// </summary>
    /// <param name="email">The email to validate.</param>
    /// <returns>True if valid email format, false otherwise.</returns>
    public static bool IsValidEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        // Check length (max 254 per RFC 5321)
        if (email.Length > 254)
            return false;

        return EmailRegex.IsMatch(email);
    }

    /// <summary>
    /// Validates if the given string is a valid phone number format.
    /// Allows common formats: digits, spaces, dashes, parentheses, plus sign.
    /// </summary>
    /// <param name="phone">The phone number to validate.</param>
    /// <returns>True if valid phone format, false otherwise.</returns>
    public static bool IsValidPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return true; // Phone is optional, empty is valid

        // Allow: digits, spaces, dashes, parentheses, plus sign, dots
        // Min 7 characters (shortest valid phone), max 50
        var cleaned = phone.Trim();
        if (cleaned.Length < 7 || cleaned.Length > 50)
            return false;

        return PhoneRegex().IsMatch(cleaned);
    }

    /// <summary>
    /// Validates if the given string is a valid domain format.
    /// </summary>
    /// <param name="domain">The domain to validate.</param>
    /// <returns>True if valid domain format, false otherwise.</returns>
    public static bool IsValidDomain(string? domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return true; // Domain is optional, empty is valid

        if (domain.Length > 253)
            return false;

        return DomainRegex().IsMatch(domain);
    }

    /// <summary>
    /// Checks if a string contains only whitespace.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns>True if null, empty, or whitespace only.</returns>
    public static bool IsNullOrWhitespace(string? value)
        => string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// Checks if a string exceeds the maximum length.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <param name="maxLength">Maximum allowed length.</param>
    /// <returns>True if exceeds max length.</returns>
    public static bool ExceedsMaxLength(string? value, int maxLength)
        => value != null && value.Length > maxLength;

    [GeneratedRegex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$", RegexOptions.Compiled)]
    private static partial Regex MyEmailRegex();

    [GeneratedRegex(@"^[\d\s\-\+\(\)\.]+$", RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?(\.[a-zA-Z]{2,})+$", RegexOptions.Compiled)]
    private static partial Regex DomainRegex();
}
