namespace ACI.Application.Common;

/// <summary>A composed message: subject, plain-text part, HTML part.</summary>
public record EmailContent(string Subject, string Text, string Html);

/// <summary>
/// The wording and markup of the three contract emails.
/// </summary>
/// <remarks>
/// <para>
/// Pulled out of <c>SmtpEmailSender</c> deliberately. While these lived as private
/// string concatenation inside the sender there was no way to assert anything about
/// them without opening an SMTP connection — which is why, in most codebases,
/// email content is the least tested thing that customers actually read. Out here
/// they are pure functions of their arguments, so the escaping, the links and the
/// full contract text are all checkable, and the HTML can be rendered and looked at.
/// </para>
/// <para>
/// Two constraints drive the markup. Every mail client strips a <c>&lt;style&gt;</c>
/// block, so everything is inlined; and layout support varies enough that a single
/// column with no floats is the only thing that degrades gracefully everywhere.
/// Each message carries BOTH parts — a text-only client still gets the whole
/// contract, not a "please enable HTML" stub.
/// </para>
/// </remarks>
public static class ContractEmailContent
{
    private const string PStyle = "margin:0 0 14px;font-size:15px;line-height:1.55;color:#0f172a;";
    private const string MutedStyle = "margin:18px 0 0;font-size:13px;line-height:1.5;color:#64748b;";
    private const string PreStyle =
        "white-space:pre-wrap;word-wrap:break-word;font-family:ui-monospace,SFMono-Regular,Menlo,monospace;" +
        "font-size:13px;line-height:1.6;color:#0f172a;background:#f8fafc;border:1px solid #e2e8f0;" +
        "border-radius:10px;padding:16px;margin:0 0 16px;";
    private const string SignatureBoxStyle =
        "border:1px solid #a7f3d0;background:#ecfdf5;border-radius:10px;padding:14px 16px;margin:0 0 8px;";

    /// <summary>
    /// HTML-escapes a value coming from the database.
    /// </summary>
    /// <remarks>
    /// Every interpolation into the HTML goes through this. A contract title is
    /// typed by a person, and a title containing <c>&lt;script&gt;</c> would
    /// otherwise be delivered as markup into the counterparty's mail client.
    /// Quotes are escaped too because several of these land inside attributes.
    /// </remarks>
    public static string Escape(string? s) => string.IsNullOrEmpty(s)
        ? string.Empty
        : s.Replace("&", "&amp;")
           .Replace("<", "&lt;")
           .Replace(">", "&gt;")
           .Replace("\"", "&quot;")
           .Replace("'", "&#39;");

    private static string Greeting(string? recipientName)
        => string.IsNullOrWhiteSpace(recipientName) ? string.Empty : " " + recipientName.Trim();

    private static string Button(string url, string label) =>
        $"<p style=\"margin:22px 0;\"><a href=\"{Escape(url)}\" " +
        "style=\"display:inline-block;background:#ea580c;color:#ffffff;text-decoration:none;" +
        "font-weight:600;font-size:15px;padding:12px 22px;border-radius:10px;\">" +
        $"{Escape(label)}</a></p>" +
        // Always paired with the bare URL. Plenty of clients strip or fail to
        // render the anchor, and a button that does nothing with no way to
        // recover is how a contract goes unsigned.
        $"<p style=\"{MutedStyle}\">If the button does not work, paste this into your browser:<br>" +
        $"<span style=\"color:#0f172a;word-break:break-all;\">{Escape(url)}</span></p>";

    private static string Document(string senderName, string heading, string intro, string bodyHtml, string? footer) =>
        "<!doctype html><html><body style=\"margin:0;padding:24px;background:#f1f5f9;" +
        "font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;\">" +
        "<div style=\"max-width:640px;margin:0 auto;background:#ffffff;border-radius:16px;" +
        "border:1px solid #e2e8f0;padding:28px 30px;\">" +
        $"<h1 style=\"margin:0 0 6px;font-size:20px;line-height:1.3;color:#0f172a;\">{Escape(heading)}</h1>" +
        $"<p style=\"{PStyle}\">{intro}</p>" +
        bodyHtml +
        (string.IsNullOrWhiteSpace(footer)
            ? string.Empty
            : $"<p style=\"{MutedStyle}\">— {Escape(footer)}</p>") +
        $"<p style=\"{MutedStyle}border-top:1px solid #e2e8f0;padding-top:14px;\">" +
        $"Sent by {Escape(senderName)}.</p>" +
        "</div></body></html>";

    /// <summary>Step 3: the counterparty is asked to read and sign.</summary>
    public static EmailContent ForSignature(
        string senderName, string recipientName, string organizationName,
        string contractTitle, string signUrl)
    {
        var subject = $"{organizationName}: please review and sign “{contractTitle}”";
        var text =
            $"Hi{Greeting(recipientName)},\n\n" +
            $"{organizationName} has sent you a contract to review and sign:\n\n" +
            $"    {contractTitle}\n\n" +
            "You can read it in full and sign it here:\n\n" +
            $"    {signUrl}\n\n" +
            "Nothing is agreed until you sign. If anything looks wrong, reply to this " +
            "email instead of signing and we will sort it out.\n\n" +
            $"— {organizationName}\n";

        var html = Document(senderName,
            heading: "Please review and sign",
            intro: $"{Escape(organizationName)} has sent you a contract to review and sign.",
            bodyHtml:
                $"<p style=\"{PStyle}\"><strong>{Escape(contractTitle)}</strong></p>" +
                Button(signUrl, "Read and sign the contract") +
                $"<p style=\"{MutedStyle}\">Nothing is agreed until you sign. If anything looks wrong, " +
                "reply to this email instead of signing and we will sort it out.</p>",
            footer: organizationName);

        return new EmailContent(subject, text, html);
    }

    /// <summary>Step 3 done: we are told to countersign.</summary>
    public static EmailContent SignedNotification(
        string senderName, string recipientName, string counterpartyName,
        string contractTitle, string contractUrl)
    {
        var subject = $"{counterpartyName} signed “{contractTitle}” — your signature is next";
        var text =
            $"Hi{Greeting(recipientName)},\n\n" +
            $"{counterpartyName} has signed “{contractTitle}”.\n\n" +
            "Add your countersignature to execute it and send the finished copy to everyone:\n\n" +
            $"    {contractUrl}\n";

        var html = Document(senderName,
            heading: "They have signed",
            intro: $"{Escape(counterpartyName)} has signed <strong>{Escape(contractTitle)}</strong>.",
            bodyHtml:
                $"<p style=\"{PStyle}\">Add your countersignature to execute it and send the " +
                "finished copy to everyone.</p>" +
                Button(contractUrl, "Countersign the contract"),
            footer: null);

        return new EmailContent(subject, text, html);
    }

    /// <summary>
    /// Step 4: the executed contract, to both parties.
    /// </summary>
    /// <remarks>
    /// The full text travels INLINE rather than as an attachment. There is no PDF
    /// library and no file storage in this system, and an email that promises an
    /// attachment it does not carry is worse than one that simply contains the
    /// document. Inline text is also readable in every client, quotable in a
    /// reply, and printable — and it is the same string whose hash was stamped at
    /// send time, so it is the evidence rather than a rendering of it.
    /// </remarks>
    public static EmailContent Executed(
        string senderName, string recipientName, string organizationName,
        string contractTitle, string contractBody, string signatureBlock)
    {
        var rule = new string('-', 60);
        var subject = $"Signed by both parties: “{contractTitle}”";
        var text =
            $"Hi{Greeting(recipientName)},\n\n" +
            $"“{contractTitle}” has now been signed by both parties. The full text " +
            "and the signature record are below — keep this email as your copy.\n\n" +
            rule + "\n\n" +
            contractBody + "\n\n" +
            rule + "\n\n" +
            signatureBlock + "\n\n" +
            $"— {organizationName}\n";

        var html = Document(senderName,
            heading: "Signed by both parties",
            intro: $"<strong>{Escape(contractTitle)}</strong> has now been signed by both parties. " +
                   "The full text and the signature record are below — keep this email as your copy.",
            bodyHtml:
                $"<pre style=\"{PreStyle}\">{Escape(contractBody)}</pre>" +
                $"<div style=\"{SignatureBoxStyle}\"><pre style=\"{PreStyle}margin:0;background:none;border:0;padding:0;\">" +
                $"{Escape(signatureBlock)}</pre></div>",
            footer: organizationName);

        return new EmailContent(subject, text, html);
    }
}
