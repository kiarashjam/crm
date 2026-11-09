using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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
public static partial class InboundParseReader
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
            !string.IsNullOrWhiteSpace(text) ? text : HtmlToText(html));
    }

    private static string? Header(HeaderList? headers, HeaderId id)
    {
        var value = headers?[id];
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

    [GeneratedRegex("<(head|style|script)[^>]*>.*?</\\1\\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Invisible();

    [GeneratedRegex("<(br|/p|/div|/tr|/li)[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreak();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tag();

    /// <summary>The words of an HTML-only reply, line breaks kept: enough for a timeline entry.</summary>
    private static string? HtmlToText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        var withBreaks = LineBreak().Replace(Invisible().Replace(html, ""), "\n");
        return WebUtility.HtmlDecode(Tag().Replace(withBreaks, "")).Trim();
    }
}
