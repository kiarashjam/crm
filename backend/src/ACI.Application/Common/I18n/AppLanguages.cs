using System.Globalization;

namespace ACI.Application.Common.I18n;

/// <summary>
/// The languages Cadence writes in: English, German (Swiss spelling) and French (as written in
/// Romandy). The app's own words are in the frontend's catalogues; what the server writes to a
/// member (statements, reminders, welcome and event emails, the QR-bill) is in
/// <see cref="MemberTexts"/>.
/// </summary>
public static class AppLanguages
{
    public const string English = "en";
    public const string German = "de";
    public const string French = "fr";

    /// <summary>Every language there is a catalogue for, English first.</summary>
    public static readonly IReadOnlyList<string> All = [English, German, French];

    /// <summary>
    /// One of ours from a language or locale name ("de-CH", "FR", "fr_ch"), or null for any other
    /// ("it-CH") and for nothing at all.
    /// </summary>
    public static string? Normalise(string? raw)
    {
        var code = (raw ?? "").Trim().ToLowerInvariant().Split('-', '_')[0];
        return All.Contains(code) ? code : null;
    }

    /// <summary>Whether a value sent for a language is one of ours or empty (no choice).</summary>
    public static bool IsValidChoice(string? raw) => string.IsNullOrWhiteSpace(raw) || Normalise(raw) != null;

    /// <summary>
    /// The language to write to someone in: their own when they have one, else the workspace's
    /// (from its locale: "fr-CH" writes French), else English.
    /// </summary>
    public static string Resolve(string? preferred, string? workspaceLocale) =>
        Normalise(preferred) ?? Normalise(workspaceLocale) ?? English;

    /// <summary>
    /// The culture dates are written in for a language and a workspace: the language with the
    /// workspace's region, so month names are the reader's and the order is the region's
    /// ("fr" in a de-CH workspace writes fr-CH).
    /// </summary>
    public static CultureInfo DateCulture(string language, string? workspaceLocale)
    {
        var region = RegionOf(workspaceLocale);
        if (region != null)
        {
            try
            {
                return CultureInfo.GetCultureInfo($"{language}-{region}", predefinedOnly: true);
            }
            catch (CultureNotFoundException)
            {
                // A language the region has no culture for: the language alone.
            }
        }
        try
        {
            return CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }

    private static string? RegionOf(string? locale)
    {
        var parts = (locale ?? "").Trim().Split('-', '_');
        return parts.Skip(1).FirstOrDefault(p => p.Length == 2 && p.All(char.IsLetter))?.ToUpperInvariant();
    }
}
