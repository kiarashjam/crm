using System.Text.RegularExpressions;

namespace ACI.Application.Common;

/// <summary>
/// The placeholders a newsletter to leads may use, and how each lead's are filled in before
/// the email is queued.
/// </summary>
/// <remarks>
/// Everything filled into the HTML is HTML-encoded: a name is text, never markup, and a link
/// is written as an attribute takes it. A placeholder may give a fallback for a lead without
/// the value: <c>{{firstName|there}}</c>.
/// </remarks>
public static partial class NewsletterTemplate
{
    public const string FirstName = "firstName";
    public const string LastName = "lastName";
    public const string Name = "name";
    public const string BookingUrl = "bookingUrl";
    public const string BookingCode = "bookingCode";
    public const string Workspace = "workspace";

    /// <summary>Every placeholder a newsletter may use, as the editor offers them.</summary>
    public static readonly IReadOnlyList<string> Fields = [FirstName, LastName, Name, BookingUrl, BookingCode, Workspace];

    [GeneratedRegex(@"\{\{\s*(?<key>[A-Za-z][A-Za-z0-9_]*)\s*(?:\|(?<fallback>[^{}]*))?\}\}")]
    private static partial Regex Placeholder();

    /// <summary>One lead's values. <paramref name="BookingUrl"/> is null when the workspace has no booking page or secret.</summary>
    public sealed record Values(string Name, string? BookingUrl, string BookingCode, string? Workspace);

    /// <summary>The placeholders the text uses, as written, once each.</summary>
    public static IReadOnlyList<string> Used(string? text) =>
        Placeholder().Matches(text ?? "").Select(m => m.Groups["key"].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Placeholders in the text that are none of <see cref="Fields"/>, as written ("{{phone}}").</summary>
    public static IReadOnlyList<string> Unknown(params string?[] texts) =>
        texts.SelectMany(Used).Where(k => !Fields.Contains(k, StringComparer.OrdinalIgnoreCase))
            .Select(k => "{{" + k + "}}").Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>Whether the text uses {{bookingUrl}} without a fallback, which a workspace with no booking page cannot fill.</summary>
    public static bool NeedsBookingUrl(params string?[] texts) =>
        texts.Any(t => Placeholder().Matches(t ?? "").Any(m =>
            m.Groups["key"].Value.Equals(BookingUrl, StringComparison.OrdinalIgnoreCase) && !m.Groups["fallback"].Success));

    /// <summary>
    /// The text with each placeholder replaced by the lead's value, else its fallback, else
    /// nothing; <paramref name="html"/> encodes each value for HTML.
    /// </summary>
    public static string Fill(string? template, Values values, bool html)
    {
        var full = values.Name.Trim();
        var space = full.IndexOf(' ');
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            [FirstName] = space < 0 ? full : full[..space],
            [LastName] = space < 0 ? "" : full[(space + 1)..].Trim(),
            [Name] = full,
            [BookingUrl] = values.BookingUrl,
            [BookingCode] = values.BookingCode,
            [Workspace] = values.Workspace?.Trim(),
        };
        return Placeholder().Replace(template ?? "", match =>
        {
            var key = match.Groups["key"].Value;
            var value = map.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)
                ? v.Trim()
                : match.Groups["fallback"].Success ? match.Groups["fallback"].Value.Trim() : "";
            return html ? OnboardingTemplate.Html(value) : value;
        });
    }
}
