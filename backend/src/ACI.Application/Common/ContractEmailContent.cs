using System.Globalization;

namespace ACI.Application.Common;

/// <summary>A composed message: subject, plain-text part, HTML part.</summary>
public record EmailContent(string Subject, string Text, string Html);

/// <summary>
/// The wording of the contract emails, drawn in the one email design (<see cref="EmailLayout"/>).
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
/// What goes to the counterparty (the request to sign, its reminder, the executed copy) wears
/// the club's look, under the workspace's name: it is the workspace's contract, not the
/// software's. The note that the counterparty has signed goes to the team, in CadenceE's own.
/// Each message carries BOTH parts — a text-only client still gets the whole contract, not a
/// "please enable HTML" stub.
/// </para>
/// </remarks>
public static class ContractEmailContent
{
    private static string Hi(string? recipientName)
        => string.IsNullOrWhiteSpace(recipientName) ? "Hi," : $"Hi {recipientName.Trim()},";

    private static EmailDesign Counterparty(string organizationName, string senderName) =>
        new(EmailLook.Club, organizationName)
        {
            Language = "en",
            FooterNote = string.IsNullOrWhiteSpace(organizationName)
                ? $"Sent by {senderName}."
                : $"Sent by {senderName} on behalf of {organizationName.Trim()}.",
        };

    private static string Day(DateTime utc) => utc.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>Step 3: the counterparty is asked to read and sign.</summary>
    /// <param name="hasAttachment">
    /// Whether the unsigned copy travels with the message. Said out loud, because an
    /// attachment nobody mentions is an attachment nobody opens — and a recipient
    /// who has been told to expect one will notice if a relay strips it.
    /// </param>
    public static EmailContent ForSignature(
        string senderName, string recipientName, string organizationName,
        string contractTitle, string signUrl, bool hasAttachment = false)
    {
        var subject = $"{organizationName}: please review and sign “{contractTitle}”";
        var design = Counterparty(organizationName, senderName);
        design.Eyebrow = "Contract for signature";
        design.Title = "Please review and sign";
        design.Preheader = $"{organizationName} has sent you “{contractTitle}” to read and sign.";
        design
            .Paragraph(Hi(recipientName))
            .Paragraph($"{organizationName} has sent you a contract to review and sign.")
            .Facts([("Contract", contractTitle), ("From", organizationName), ("Attached", hasAttachment ? "An unsigned copy (PDF)" : null)])
            .Paragraph(hasAttachment
                ? "A copy is attached to this email so you can read it at your leisure. It is marked unsigned until both parties have signed."
                : null)
            .Button("Read and sign the contract", signUrl)
            .Note($"Nothing is agreed until you sign. If anything looks wrong, reply to this email instead of signing and we will sort it out. " +
                  $"The link works for {ContractSigningToken.Lifetime.TotalDays:0} days.")
            .SignOff("Kind regards,", organizationName);
        return EmailLayout.Compose(design, subject);
    }

    /// <summary>
    /// The one reminder to a counterparty who has not opened a contract three days after it was
    /// sent (ContractReminderBackgroundService). Only the signing link's hash is kept, so it
    /// points back to the original email rather than carrying a link.
    /// </summary>
    /// <param name="sentAtUtc">When the contract was sent.</param>
    /// <param name="linkExpiresAtUtc">When the link in the original email stops working.</param>
    public static EmailContent Reminder(
        string senderName, string recipientName, string organizationName, string contractTitle,
        DateTime? sentAtUtc = null, DateTime? linkExpiresAtUtc = null)
    {
        var design = Counterparty(organizationName, senderName);
        design.Eyebrow = "Reminder";
        design.Title = "A gentle reminder";
        design.Preheader = $"“{contractTitle}” is still waiting for your signature.";
        design
            .Paragraph(Hi(recipientName))
            .Paragraph("A contract is waiting for your signature. Please use the link from the original email to read and sign it.")
            .Facts([
                ("Contract", contractTitle),
                ("From", string.IsNullOrWhiteSpace(organizationName) ? null : organizationName),
                ("Sent on", sentAtUtc is { } sent ? Day(sent) : null),
                ("Link works until", linkExpiresAtUtc is { } expires ? Day(expires) : null),
            ])
            .Paragraph("Nothing is agreed until you sign.", muted: true)
            .SignOff("Kind regards,", organizationName);
        return EmailLayout.Compose(design, $"Reminder: {contractTitle}");
    }

    /// <summary>Step 3 done: we are told to countersign.</summary>
    public static EmailContent SignedNotification(
        string senderName, string recipientName, string counterpartyName,
        string contractTitle, string contractUrl)
    {
        var subject = $"{counterpartyName} signed “{contractTitle}” — your signature is next";
        var who = string.IsNullOrWhiteSpace(counterpartyName) ? null : counterpartyName.Trim();
        var design = new EmailDesign(EmailLook.Product)
            {
                Language = "en",
                Eyebrow = "Contract signed",
                Title = who == null ? "They have signed" : $"{who} has signed",
                Preheader = $"“{contractTitle}” is signed on their side. Your countersignature is next.",
                FooterNote = "You received this because a contract you created was signed.",
            }
            .Paragraph(Hi(recipientName))
            .Note($"{who ?? "The counterparty"} has signed “{contractTitle}”.", EmailTone.Success)
            .Paragraph("Add your countersignature to execute it and send the finished copy to everyone.")
            .Button("Countersign the contract", contractUrl);
        return EmailLayout.Compose(design, subject);
    }

    /// <summary>
    /// Step 4: the executed contract, to both parties.
    /// </summary>
    /// <remarks>
    /// The full text travels INLINE as well as in any attachment: inline text is readable in
    /// every client, quotable in a reply, and printable — and it is the same string whose hash
    /// was stamped at send time, so it is the evidence rather than a rendering of it. The
    /// signature record, with both names, both times and that hash, follows it under a note in
    /// the success colours that says what it is.
    /// </remarks>
    /// <param name="hasAttachment">
    /// Whether the typeset copy travels with the message. The text stays in the body
    /// either way: an inline copy survives a stripped attachment, an archive-to-text
    /// rule, and a reply that quotes it.
    /// </param>
    public static EmailContent Executed(
        string senderName, string recipientName, string organizationName,
        string contractTitle, string contractBody, string signatureBlock,
        bool hasAttachment = false)
    {
        var subject = $"Signed by both parties: “{contractTitle}”";
        var design = Counterparty(organizationName, senderName);
        design.Eyebrow = "Signed contract";
        design.Title = "Signed by both parties";
        design.Preheader = $"“{contractTitle}” is signed by both parties. Your copy is inside.";
        design
            .Paragraph(Hi(recipientName))
            .Paragraph($"“{contractTitle}” has now been signed by both parties.")
            .Paragraph(hasAttachment
                ? "The signed contract is attached as a PDF for your records. The full text and the signature record are also below, " +
                  "so this email is a complete copy on its own."
                : "The full text and the signature record are below — keep this email as your copy.")
            .Heading(contractTitle)
            .Preformatted(contractBody)
            .Note("Signed by both parties. The signature record below is the evidence: who signed and when, " +
                  "and the hash of the exact text above.", EmailTone.Success)
            // Set out as written, as the contract is: its columns line up, and the 64-character
            // hash wraps on a phone where a note's text would push the email off the screen.
            .Preformatted(signatureBlock)
            .SignOff("Kind regards,", organizationName);
        return EmailLayout.Compose(design, subject);
    }
}
