using System.Net;
using ACI.Application.Common.I18n;

namespace ACI.Application.Common;

/// <summary>A link that stops a workspace emailing one address.</summary>
public sealed record UnsubscribeLink(string Address, string Url);

/// <summary>The HTML part for a message written as plain text.</summary>
public static class PlainTextEmail
{
    /// <summary>
    /// The same words, escaped, with line breaks kept — plus, when given, a
    /// tracking image, which belongs in the HTML part only (a plain-text part would
    /// show the tag literally), and the unsubscribe footer.
    /// </summary>
    /// <param name="question">The footer's question, when it is not "Don't want emails from …?".</param>
    /// <param name="language">The language of the footer's link (a member's marketing email is in theirs).</param>
    public static string ToHtml(string text, string? trackingUrl = null, string? workspaceName = null, IReadOnlyList<UnsubscribeLink>? unsubscribe = null, string? question = null,
        string language = AppLanguages.English)
    {
        var paragraphs = WebUtility.HtmlEncode(text).Replace("\r\n", "\n").Replace("\n", "<br>\n");
        var footer = unsubscribe is { Count: > 0 } ? HtmlFooter(workspaceName, unsubscribe, question, language) : "";
        var pixel = trackingUrl == null
            ? ""
            : $"\n<img src=\"{WebUtility.HtmlEncode(trackingUrl)}\" width=\"1\" height=\"1\" alt=\"\" style=\"display:block;border:0\">";
        // Written by a person, so it looks like a person's email: clean type, no card, no banner.
        return "<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"></head>" +
               "<body style=\"margin:0;padding:0;\"><div style=\"max-width:640px;padding:8px 4px;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto," +
               "'Helvetica Neue',Helvetica,Arial,sans-serif;font-size:15px;line-height:24px;color:#1f2937;\">" +
               $"{paragraphs}</div>{footer}{pixel}</body></html>";
    }

    /// <summary>
    /// A designed email's own HTML with the same footer as <see cref="ToHtml"/> (and the
    /// tracking image) at the foot of its body, centred under the design; at its end when it
    /// is a fragment with no body.
    /// </summary>
    public static string WithFooter(string html, string? trackingUrl = null, string? workspaceName = null, IReadOnlyList<UnsubscribeLink>? unsubscribe = null,
        string? question = null, string language = AppLanguages.English)
    {
        var pixel = trackingUrl == null
            ? ""
            : $"\n<img src=\"{WebUtility.HtmlEncode(trackingUrl)}\" width=\"1\" height=\"1\" alt=\"\" style=\"display:block;border:0\">";
        // Drawn by EmailLayout: the line is the last of its own footer, under the card, in its type
        // and colours (dark mode included), rather than a grey line of its own below the page.
        var layoutEnd = unsubscribe is { Count: > 0 } && html.Contains("class=\"em-page\"", StringComparison.Ordinal)
            ? html.LastIndexOf(LayoutEnd, StringComparison.Ordinal)
            : -1;
        if (layoutEnd >= 0)
        {
            var row = "<tr><td align=\"center\" class=\"em-muted\" style=\"padding:6px 24px 0;font-family:" + LayoutSans
                + ";font-size:12px;line-height:19px;color:" + LayoutMuted + ";\">"
                + FooterWords(workspaceName, unsubscribe!, question, language, $" class=\"em-muted\" style=\"color:{LayoutMuted};text-decoration:underline;\"") + "</td></tr>";
            html = html.Insert(layoutEnd, row);
            return pixel.Length == 0 ? html : html.Insert(html.LastIndexOf("</body", StringComparison.Ordinal), pixel + "\n");
        }
        var footer = unsubscribe is { Count: > 0 }
            ? $"\n<div style=\"text-align:center;padding:0 16px 24px\">{HtmlFooter(workspaceName, unsubscribe, question, language)}</div>"
            : "";
        var end = html.LastIndexOf("</body", StringComparison.OrdinalIgnoreCase);
        return end < 0 ? html + footer + pixel : html.Insert(end, footer + pixel + "\n");
    }

    /// <summary>How EmailLayout closes its page: the footer row of the column, then the column and the page.</summary>
    private const string LayoutEnd = "</table></td></tr></table></body>";

    private const string LayoutSans = "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,'Helvetica Neue',Helvetica,Arial,sans-serif";

    /// <summary>The muted ink of the club's look, the one designed email that carries this footer.</summary>
    private const string LayoutMuted = "#7a857f";

    /// <summary>
    /// The text part's footer: how to stop this workspace's email. One link per
    /// address when a message went to several people, each named, so nobody has to
    /// guess which is theirs.
    /// </summary>
    public static string TextFooter(string? workspaceName, IReadOnlyList<UnsubscribeLink> unsubscribe, string? question = null,
        string language = AppLanguages.English)
    {
        if (unsubscribe.Count == 0) return "";
        var lead = question ?? MemberTexts.T(language, "footer.question", ("club", Who(workspaceName, language)));
        var word = MemberTexts.T(language, "footer.unsubscribe");
        if (unsubscribe.Count == 1) return $"\n\n--\n{lead} {word}: {unsubscribe[0].Url}\n";
        var lines = string.Join("\n", unsubscribe.Select(link => $"{link.Address}: {link.Url}"));
        return $"\n\n--\n{lead} {word}:\n{lines}\n";
    }

    private static string HtmlFooter(string? workspaceName, IReadOnlyList<UnsubscribeLink> unsubscribe, string? question, string language) =>
        $"\n<p style=\"font-family:sans-serif;font-size:12px;line-height:1.5;color:#6b7280;margin-top:24px\">{FooterWords(workspaceName, unsubscribe, question, language, "")}</p>";

    /// <summary>The footer's question and its link, or a link per address, escaped; <paramref name="linkAttributes"/> styles the links.</summary>
    private static string FooterWords(string? workspaceName, IReadOnlyList<UnsubscribeLink> unsubscribe, string? question, string language, string linkAttributes)
    {
        var lead = WebUtility.HtmlEncode(question ?? MemberTexts.T(language, "footer.question", ("club", Who(workspaceName, language))));
        var word = WebUtility.HtmlEncode(MemberTexts.T(language, "footer.unsubscribe"));
        var links = unsubscribe.Count == 1
            ? $"<a href=\"{WebUtility.HtmlEncode(unsubscribe[0].Url)}\"{linkAttributes}>{word}</a>"
            : word + " " + string.Join(" · ", unsubscribe.Select(link =>
                $"<a href=\"{WebUtility.HtmlEncode(link.Url)}\"{linkAttributes}>{WebUtility.HtmlEncode(link.Address)}</a>"));
        return $"{lead} {links}";
    }

    /// <summary>
    /// The footer's question in a marketing email to a member, in their language: the link stops
    /// news and offers, not their statements.
    /// </summary>
    public static string MarketingQuestion(string? workspaceName, string language = AppLanguages.English) =>
        MemberTexts.T(language, "footer.marketingQuestion", ("club", Who(workspaceName, language)));

    private static string Who(string? workspaceName, string language) =>
        string.IsNullOrWhiteSpace(workspaceName) ? MemberTexts.T(language, "us") : workspaceName.Trim();
}
