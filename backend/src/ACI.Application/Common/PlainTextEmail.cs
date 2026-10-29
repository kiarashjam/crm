using System.Net;

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
    public static string ToHtml(string text, string? trackingUrl = null, string? workspaceName = null, IReadOnlyList<UnsubscribeLink>? unsubscribe = null)
    {
        var paragraphs = WebUtility.HtmlEncode(text).Replace("\r\n", "\n").Replace("\n", "<br>\n");
        var footer = unsubscribe is { Count: > 0 } ? HtmlFooter(workspaceName, unsubscribe) : "";
        var pixel = trackingUrl == null
            ? ""
            : $"\n<img src=\"{WebUtility.HtmlEncode(trackingUrl)}\" width=\"1\" height=\"1\" alt=\"\" style=\"display:block;border:0\">";
        return $"<!doctype html><html><body><div style=\"font-family:sans-serif;font-size:15px;line-height:1.5\">{paragraphs}</div>{footer}{pixel}</body></html>";
    }

    /// <summary>
    /// The text part's footer: how to stop this workspace's email. One link per
    /// address when a message went to several people, each named, so nobody has to
    /// guess which is theirs.
    /// </summary>
    public static string TextFooter(string? workspaceName, IReadOnlyList<UnsubscribeLink> unsubscribe)
    {
        if (unsubscribe.Count == 0) return "";
        var lead = $"Don't want emails from {Who(workspaceName)}?";
        if (unsubscribe.Count == 1) return $"\n\n--\n{lead} Unsubscribe: {unsubscribe[0].Url}\n";
        var lines = string.Join("\n", unsubscribe.Select(link => $"{link.Address}: {link.Url}"));
        return $"\n\n--\n{lead} Unsubscribe:\n{lines}\n";
    }

    private static string HtmlFooter(string? workspaceName, IReadOnlyList<UnsubscribeLink> unsubscribe)
    {
        var lead = WebUtility.HtmlEncode($"Don't want emails from {Who(workspaceName)}?");
        var links = unsubscribe.Count == 1
            ? $"<a href=\"{WebUtility.HtmlEncode(unsubscribe[0].Url)}\">Unsubscribe</a>"
            : "Unsubscribe " + string.Join(" · ", unsubscribe.Select(link =>
                $"<a href=\"{WebUtility.HtmlEncode(link.Url)}\">{WebUtility.HtmlEncode(link.Address)}</a>"));
        return $"\n<p style=\"font-family:sans-serif;font-size:12px;line-height:1.5;color:#6b7280;margin-top:24px\">{lead} {links}</p>";
    }

    private static string Who(string? workspaceName) =>
        string.IsNullOrWhiteSpace(workspaceName) ? "us" : workspaceName.Trim();
}
