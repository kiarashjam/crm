using System.Globalization;
using System.Text.RegularExpressions;

namespace ACI.Application.Common;

/// <summary>
/// The placeholders a welcome email may use, and how Cadence fills its own while
/// leaving the receiver's for the receiver.
/// </summary>
/// <remarks>
/// Cadence knows the person, the deal and the start; only the receiving system knows the
/// address of its portal and the link that lets the person set a password. So
/// <c>{{setPasswordUrl}}</c>, <c>{{portalUrl}}</c> and <c>{{loginEmail}}</c> reach it as
/// written, and it fills them when it sends. Everything Cadence fills into the HTML is
/// HTML-encoded: a name or a welcome message is text, never markup.
/// </remarks>
public static partial class OnboardingTemplate
{
    /// <summary>Filled by Cadence.</summary>
    public static readonly IReadOnlyList<string> CadenceFields = new[]
    {
        "firstName", "lastName", "name", "email", "company", "dealName", "welcomeMessage", "startDate", "senderName", "workspace",
    };

    /// <summary>Left for the receiving system, which alone knows them.</summary>
    public static readonly IReadOnlyList<string> ReceiverFields = new[] { "setPasswordUrl", "portalUrl", "loginEmail", "referralCode" };

    [GeneratedRegex(@"\{\{\s*(?<key>[A-Za-z][A-Za-z0-9_]*)\s*(?:\|(?<fallback>[^{}]*))?\}\}")]
    private static partial Regex Placeholder();

    public sealed record Values(
        string Name, string Email, string? Company, string? DealName, string? WelcomeMessage,
        DateTime? StartsOn, bool StartUndetermined, string Language, string? SenderName, string? Workspace);

    /// <summary>The start as a person reads it, in the email's language.</summary>
    public static string StartDate(DateTime? startsOn, bool undetermined, string language)
    {
        var french = language == "fr";
        if (startsOn is not DateTime date) return french ? "à confirmer" : "to be confirmed";
        var culture = CultureInfo.GetCultureInfo(french ? "fr-FR" : "en-GB");
        return date.ToString("d MMMM yyyy", culture);
    }

    private static Dictionary<string, string?> Map(Values v)
    {
        var full = v.Name.Trim();
        var space = full.IndexOf(' ');
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["firstName"] = space < 0 ? full : full[..space],
            ["lastName"] = space < 0 ? "" : full[(space + 1)..].Trim(),
            ["name"] = full,
            ["email"] = v.Email.Trim(),
            ["company"] = v.Company?.Trim(),
            ["dealName"] = v.DealName?.Trim(),
            ["welcomeMessage"] = v.WelcomeMessage?.Trim(),
            ["startDate"] = StartDate(v.StartsOn, v.StartUndetermined, v.Language),
            ["senderName"] = v.SenderName?.Trim(),
            ["workspace"] = v.Workspace?.Trim(),
        };
    }

    /// <summary>
    /// The text with Cadence's placeholders filled, else their fallback, else nothing,
    /// and the receiver's left as written. <paramref name="html"/> encodes each value,
    /// and the welcome message keeps its line breaks. <paramref name="unknown"/> names
    /// placeholders neither side fills, so the person can fix them before sending.
    /// </summary>
    public static string Fill(string? template, Values values, bool html, out IReadOnlyList<string> unknown)
    {
        var map = Map(values);
        var missing = new List<string>();
        var filled = Placeholder().Replace(template ?? "", match =>
        {
            var key = match.Groups["key"].Value;
            if (ReceiverFields.Contains(key, StringComparer.OrdinalIgnoreCase)) return match.Value;
            if (map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) return Encode(key, value.Trim(), html);
            if (match.Groups["fallback"].Success) return Encode(key, match.Groups["fallback"].Value.Trim(), html);
            if (!map.ContainsKey(key))
            {
                var written = "{{" + key + "}}";
                if (!missing.Contains(written, StringComparer.OrdinalIgnoreCase)) missing.Add(written);
            }
            return "";
        });
        unknown = missing;
        return filled;
    }

    /// <summary>
    /// The receiver's placeholders with stand-ins, for a preview in Cadence: the link goes
    /// nowhere, and the login is the person's email, as it will be.
    /// </summary>
    public static string WithSampleReceiverValues(string text, string email, bool html)
    {
        return Placeholder().Replace(text, match =>
        {
            var key = match.Groups["key"].Value;
            string? sample = key.ToLowerInvariant() switch
            {
                "setpasswordurl" => "#set-password",
                "portalurl" => "#portal",
                "loginemail" => email,
                "referralcode" => "PAV-7KQ",
                _ => null,
            };
            return sample == null ? match.Value : html ? Html(sample) : sample;
        });
    }

    [GeneratedRegex(@"https?://(?:localhost|127(?:\.\d{1,3}){3}|\[::1\])(?::\d+)?(?=/api/public/email-images/)", RegexOptions.IgnoreCase)]
    private static partial Regex LoopbackImageOrigin();

    /// <summary>
    /// The HTML with Cadence's own email images that point at this machine (an upload made
    /// while the server's public address was not set) pointing at <paramref name="origin"/>
    /// instead, so they show in the preview and in the email. Other images are left alone.
    /// </summary>
    public static string RebaseOwnImages(string? html, string origin)
    {
        if (string.IsNullOrEmpty(html)) return html ?? "";
        var target = origin.Trim().TrimEnd('/');
        return target.Length == 0 ? html : LoopbackImageOrigin().Replace(html, target);
    }

    /// <summary>
    /// Text made safe inside HTML: the five characters that mean markup are escaped, and
    /// nothing else, so "Hélène" stays readable in the HTML the portal receives.
    /// </summary>
    public static string Html(string? text) =>
        (text ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");

    [GeneratedRegex(@"<(head|style|script)\b[^>]*>[\s\S]*?</\1\s*>|<!--[\s\S]*?-->", RegexOptions.IgnoreCase)]
    private static partial Regex HiddenParts();

    [GeneratedRegex(@"<a\b[^>]*?\bhref\s*=\s*[""']([^""']+)[""'][^>]*>([\s\S]*?)</a\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex Links();

    [GeneratedRegex(@"<br\s*/?>|</(?:p|div|h[1-6]|li|tr|table|blockquote|section)\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEnds();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"<body\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BodyOpen();

    /// <summary>
    /// The words an email's HTML shows, for its plain-text part: a line for each block, and a
    /// link's address after its words. What is not shown (the head, styles, scripts, comments) is left out.
    /// </summary>
    public static string PlainText(string html)
    {
        var text = HiddenParts().Replace(html, "");
        text = Links().Replace(text, m =>
        {
            var words = Tags().Replace(m.Groups[2].Value, "").Trim();
            var href = m.Groups[1].Value.Trim();
            return words.Length == 0 || words == href ? href : $"{words} ({href})";
        });
        text = Tags().Replace(BlockEnds().Replace(text, "\n"), "");
        text = System.Net.WebUtility.HtmlDecode(text).Replace('\u00A0', ' ');
        var lines = text.Replace("\r\n", "\n").Split('\n').Select(l => string.Join(' ', l.Split(' ', StringSplitOptions.RemoveEmptyEntries)));
        // A line between paragraphs, never a run of empty ones.
        var kept = new List<string>();
        foreach (var line in lines)
            if (line.Length > 0 || (kept.Count > 0 && kept[^1].Length > 0)) kept.Add(line);
        return string.Join("\n", kept).Trim();
    }

    /// <summary>The HTML with a line across its top saying what it is: at the start of its body, or of the fragment.</summary>
    public static string WithBanner(string html, string note)
    {
        var banner = "<div style=\"background:#fff7ed;border-bottom:1px solid #fed7aa;color:#9a3412;font:13px/1.5 system-ui,-apple-system,Segoe UI,sans-serif;padding:10px 16px;text-align:center\">"
            + Html(note) + "</div>";
        var body = BodyOpen().Match(html);
        return body.Success ? html.Insert(body.Index + body.Length, banner) : banner + html;
    }

    private static string Encode(string key, string value, bool html)
    {
        if (!html) return value;
        var encoded = Html(value);
        return key.Equals("welcomeMessage", StringComparison.OrdinalIgnoreCase)
            ? encoded.Replace("\r\n", "\n").Replace("\n", "<br>")
            : encoded;
    }
}
