using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ACI.Application.Common;

/// <summary>
/// A reply as the mail service hands it over (SendGrid Inbound Parse), read down to
/// what matching it needs.
/// </summary>
/// <param name="Recipients">Every address it was sent to: the envelope's, To and Cc.</param>
/// <param name="MessageId">Its own Message-ID, angle brackets included, when it had one.</param>
/// <param name="InReplyTo">The Message-ID it answers, when it says.</param>
/// <param name="References">The thread's Message-IDs, oldest first.</param>
/// <param name="Text">The plain text, quoted history included.</param>
public sealed record InboundReply(
    string FromAddress,
    string? FromName,
    IReadOnlyList<string> Recipients,
    string? Subject,
    string? MessageId,
    string? InReplyTo,
    IReadOnlyList<string> References,
    string? Text);

/// <summary>One event from the mail service's delivery reports (SendGrid Event Webhook).</summary>
/// <param name="Event">delivered, deferred, bounce, dropped, spamreport, open, unsubscribe and others.</param>
/// <param name="OutboundMessageId">The outbox id the message carried in its X-SMTPAPI header, when it did.</param>
/// <param name="SmtpId">The message's Message-ID, as the mail service saw it.</param>
/// <param name="Type">For a bounce: "bounce" (for good) or "blocked" (refused for now).</param>
public sealed record MailEvent(
    string Event,
    string? Email,
    DateTime? OccurredAtUtc,
    Guid? OutboundMessageId,
    string? SmtpId,
    string? Reason,
    string? Type);

/// <summary>Reading what the mail service posts back: replies and delivery reports.</summary>
public static partial class InboundMail
{
    /// <summary>
    /// The events in an Event Webhook post, a JSON array. One that cannot be read is left
    /// out rather than failing the rest; a body that is not an array gives none.
    /// </summary>
    public static IReadOnlyList<MailEvent> ParseEvents(string json, string messageIdArg)
    {
        var events = new List<MailEvent>();
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return events;
        }
        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return events;
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                var name = Text(item, "event");
                if (string.IsNullOrWhiteSpace(name)) continue;
                DateTime? at = item.TryGetProperty("timestamp", out var ts) && ts.ValueKind == JsonValueKind.Number && ts.TryGetInt64(out var seconds)
                    ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
                    : null;
                events.Add(new MailEvent(
                    name.Trim().ToLowerInvariant(),
                    Text(item, "email")?.Trim(),
                    at,
                    Guid.TryParse(Text(item, messageIdArg), out var id) ? id : null,
                    Text(item, "smtp-id"),
                    Text(item, "reason") ?? Text(item, "response"),
                    Text(item, "type")));
            }
        }
        return events;
    }

    private static string? Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>
    /// Whether a signed Event Webhook post is genuine: SendGrid signs the timestamp header
    /// followed by the raw body with ECDSA (P-256, SHA-256), and shows the public key as
    /// base64. False for anything malformed.
    /// </summary>
    public static bool IsSignedBy(string publicKeyBase64, string? signatureBase64, string? timestamp, byte[] body)
    {
        if (string.IsNullOrWhiteSpace(signatureBase64) || string.IsNullOrWhiteSpace(timestamp)) return false;
        try
        {
            using var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeyBase64.Trim()), out _);
            var signed = Encoding.UTF8.GetBytes(timestamp.Trim()).Concat(body).ToArray();
            return key.VerifyData(signed, Convert.FromBase64String(signatureBase64.Trim()), HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return false;
        }
    }

    [GeneratedRegex(@"^\s*(On\s.+|Le\s.+|Am\s.+|El\s.+|Il\s.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex AttributionStart();

    // German puts the name after the verb: "… schrieb Anna <anna@club.ch>:".
    [GeneratedRegex(@"((wrote|a écrit|escribió|ha scritto)\s*:|schrieb\s.*:)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex AttributionEnd();

    [GeneratedRegex(@"^\s*(-{2,}\s*(Original Message|Ursprüngliche Nachricht|Message d'origine|Messaggio originale)\s*-{2,}|_{10,}|From:\s.+|Von:\s.+|De\s?:\s.+)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex Separator();

    /// <summary>
    /// What the person wrote, without the conversation their mail app quoted below it:
    /// cut at "On … wrote:" (and the French, German, Spanish and Italian forms, which
    /// may wrap over two lines), at an "Original Message" or Outlook separator, or at the
    /// first quoted line. All of it when that would leave nothing.
    /// </summary>
    public static string WithoutQuotedHistory(string? text, int max = 10_000)
    {
        // Only the start is read: at most max characters are kept, and the person's words
        // come before the history. A reply may be tens of megabytes, and the attribution
        // pattern takes time that grows with the square of a line's length, so reading all
        // of it let one reply hold the request for minutes.
        text ??= "";
        if (text.Length > LookAt(max)) text = text[..LookAt(max)];
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var end = lines.Length;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.TrimStart().StartsWith('>')) { end = i; break; }
            if (Separator().IsMatch(line)) { end = i; break; }
            if (AttributionStart().IsMatch(line)
                && (AttributionEnd().IsMatch(line) || (i + 1 < lines.Length && AttributionEnd().IsMatch(lines[i + 1]))))
            {
                end = i;
                break;
            }
        }
        var reply = string.Join('\n', lines.Take(end)).Trim();
        if (reply.Length == 0) reply = text.Trim();
        return reply.Length <= max ? reply : reply[..max];
    }

    /// <summary>
    /// How much of a reply is looked at: twice what is kept, so the line that starts the
    /// quoted history is still whole when the person's own words fill most of it.
    /// </summary>
    internal static int LookAt(int max) => Math.Max(max, 0) * 2 + 1_000;
}
