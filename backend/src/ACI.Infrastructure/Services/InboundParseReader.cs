using System.Net;
using System.Text;
using System.Text.Json;
using ACI.Application.Common;
using MimeKit;
using MimeKit.Utils;

namespace ACI.Infrastructure.Services;

/// <summary>
/// Reads a SendGrid Inbound Parse post down to what matching a reply needs.
/// </summary>
/// <remarks>
/// SendGrid posts either the parsed parts (from, to, cc, subject, text, html, and the
/// raw header block in "headers") or, with "Send Raw" ticked, the whole message in
/// "email". Both are read: the threading headers (Message-ID, In-Reply-To, References)
/// come from the header block or the message, and the envelope's recipients from
/// "envelope", a JSON object, which is where a reply+token address shows when the
/// visible To does not carry it.
/// </remarks>
public static class InboundParseReader
{
    /// <summary>The fields of one post, as SendGrid names them. Null when the post did not carry one.</summary>
    public sealed record Fields(
        string? Email, string? Headers, string? From, string? To, string? Cc, string? Subject, string? Text, string? Html, string? Envelope);

    /// <summary>The reply, or null when the post has no sender to speak of.</summary>
    public static InboundReply? Read(Fields fields)
    {
        HeaderList? headers = null;
        string? text = fields.Text, html = fields.Html, from = fields.From, to = fields.To, cc = fields.Cc, subject = fields.Subject;
        if (!string.IsNullOrWhiteSpace(fields.Email))
        {
            try
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fields.Email));
                var message = MimeMessage.Load(stream);
                headers = message.Headers;
                text ??= message.TextBody;
                html ??= message.HtmlBody;
                from ??= message.From.ToString();
                to ??= message.To.ToString();
                cc ??= message.Cc.ToString();
                subject ??= message.Subject;
            }
            catch (FormatException)
            {
                // Not a message MimeKit can read: fall back to the parsed fields.
            }
        }
        if (headers == null && !string.IsNullOrWhiteSpace(fields.Headers))
        {
            try
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(fields.Headers.TrimEnd() + "\r\n\r\n"));
                headers = HeaderList.Load(stream);
            }
            catch (FormatException)
            {
                headers = null;
            }
        }

        var sender = Mailboxes(from).FirstOrDefault();
        if (sender == null) return null;

        var recipients = new List<string>();
        recipients.AddRange(EnvelopeRecipients(fields.Envelope));
        recipients.AddRange(Mailboxes(to).Select(m => m.Address));
        recipients.AddRange(Mailboxes(cc).Select(m => m.Address));

        var messageId = Header(headers, HeaderId.MessageId) is { } rawId ? Bracketed(MimeUtils.ParseMessageId(rawId) ?? rawId) : null;
        var inReplyTo = Header(headers, HeaderId.InReplyTo) is { } rawReply ? Bracketed(MimeUtils.ParseMessageId(rawReply) ?? rawReply) : null;
        var references = Header(headers, HeaderId.References) is { } rawRefs
            ? MimeUtils.EnumerateReferences(rawRefs).Select(Bracketed).ToList()
            : new List<string>();

        return new InboundReply(
            sender.Address,
            string.IsNullOrWhiteSpace(sender.Name) ? null : sender.Name,
            recipients.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            subject,
            messageId,
            inReplyTo,
            references,
            !string.IsNullOrWhiteSpace(text) ? text : HtmlToText(html),
            IsAutomatic(headers));
    }

    /// <summary>
    /// Whether a program wrote it: an out-of-office or other auto-reply. Mail programs say so
    /// in Auto-Submitted (RFC 3834: anything but "no"), and older ones in X-Autoreply,
    /// X-Autorespond or Precedence.
    /// </summary>
    private static bool IsAutomatic(HeaderList? headers)
    {
        if (headers == null) return false;
        if (Header(headers, "Auto-Submitted") is { } submitted && !submitted.StartsWith("no", StringComparison.OrdinalIgnoreCase)) return true;
        if (Header(headers, "X-Autoreply") != null || Header(headers, "X-Autorespond") != null || Header(headers, "X-Autoresponder") != null) return true;
        return Header(headers, "Precedence")?.ToLowerInvariant() is "auto_reply" or "bulk" or "junk";
    }

    private static string? Header(HeaderList? headers, HeaderId id)
    {
        var value = headers?[id];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? Header(HeaderList headers, string field)
    {
        var value = headers[field];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string Bracketed(string id) => $"<{id.Trim().TrimStart('<').TrimEnd('>')}>";

    private static IEnumerable<MailboxAddress> Mailboxes(string? list) =>
        !string.IsNullOrWhiteSpace(list) && InternetAddressList.TryParse(list, out var parsed) ? parsed.Mailboxes : [];

    private static IEnumerable<string> EnvelopeRecipients(string? envelope)
    {
        if (string.IsNullOrWhiteSpace(envelope)) return [];
        try
        {
            using var doc = JsonDocument.Parse(envelope);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("to", out var to) && to.ValueKind == JsonValueKind.Array)
                return to.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList();
        }
        catch (JsonException)
        {
            // An envelope that is not JSON names nobody.
        }
        return [];
    }

    /// <summary>
    /// How much of an HTML-only reply is read for its words. The timeline keeps the first
    /// ten thousand characters of a reply, and the person's own words come first.
    /// </summary>
    private const int MaxHtml = 500_000;

    /// <summary>The words of an HTML-only reply, line breaks kept: enough for a timeline entry.</summary>
    /// <remarks>
    /// One pass over the markup, so the time it takes grows with its length and no more.
    /// It used to be regular expressions, and the one that left out style and script went
    /// back over the rest of the message from every opening tag that was never closed:
    /// two hundred kilobytes of "&lt;style&gt;" held a request for most of a minute. Anyone
    /// may send mail to the reply domain, and every post was read this way before it was
    /// matched to anything.
    /// </remarks>
    private static string? HtmlToText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        if (html.Length > MaxHtml) html = html[..MaxHtml];
        var text = new StringBuilder();
        var at = 0;
        while (at < html.Length)
        {
            var open = html.IndexOf('<', at);
            if (open < 0)
            {
                text.Append(html, at, html.Length - at);
                break;
            }
            text.Append(html, at, open - at);

            if (string.CompareOrdinal(html, open, "<!--", 0, 4) == 0)
            {
                // A comment, Outlook's conditional blocks among them: nothing in it is shown.
                var commentEnd = html.IndexOf("-->", open + 4, StringComparison.Ordinal);
                if (commentEnd < 0) break;
                at = commentEnd + 3;
                continue;
            }

            var close = html.IndexOf('>', open + 1);
            if (close < 0)
            {
                // A "<" that opens no tag is text.
                text.Append(html, open, html.Length - open);
                break;
            }
            var name = TagName(html, open + 1, close);
            if (name is "head" or "style" or "script")
            {
                // Not for reading, up to its closing tag; never closed, nor is the rest.
                var end = html.IndexOf("</" + name, close + 1, StringComparison.OrdinalIgnoreCase);
                var endTag = end < 0 ? -1 : html.IndexOf('>', end);
                if (endTag < 0) break;
                at = endTag + 1;
                continue;
            }
            if (name is "br" or "/p" or "/div" or "/tr" or "/li") text.Append('\n');
            at = close + 1;
        }
        return WebUtility.HtmlDecode(text.ToString()).Trim();
    }

    /// <summary>A tag's name in lower case, with the slash of a closing tag: "br", "/p".</summary>
    private static string TagName(string html, int start, int end)
    {
        var at = start;
        if (at < end && html[at] == '/') at++;
        while (at < end && char.IsAsciiLetterOrDigit(html[at])) at++;
        return html[start..at].ToLowerInvariant();
    }
}
