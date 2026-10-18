using System.Net;

namespace ACI.Application.Common;

/// <summary>The HTML part for a message written as plain text.</summary>
public static class PlainTextEmail
{
    /// <summary>
    /// The same words, escaped, with line breaks kept — plus, when given, a
    /// tracking image, which belongs in the HTML part only (a plain-text part would
    /// show the tag literally).
    /// </summary>
    public static string ToHtml(string text, string? trackingUrl = null)
    {
        var paragraphs = WebUtility.HtmlEncode(text).Replace("\r\n", "\n").Replace("\n", "<br>\n");
        var pixel = trackingUrl == null
            ? ""
            : $"\n<img src=\"{WebUtility.HtmlEncode(trackingUrl)}\" width=\"1\" height=\"1\" alt=\"\" style=\"display:block;border:0\">";
        return $"<!doctype html><html><body><div style=\"font-family:sans-serif;font-size:15px;line-height:1.5\">{paragraphs}</div>{pixel}</body></html>";
    }
}
