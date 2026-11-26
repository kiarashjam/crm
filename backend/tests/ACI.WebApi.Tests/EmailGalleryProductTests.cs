using ACI.Application.Common;

namespace ACI.WebApi.Tests;

/// <summary>
/// Every email of CadenceE's own and the contract emails, with realistic sample data, written to
/// EMAIL_PREVIEW_DIR when it is set (and nothing otherwise), one file per email named
/// &lt;look&gt;-&lt;email&gt;.html: email design is checked by looking at it, in a browser or a
/// screenshot, on a desktop, a phone and in dark mode.
/// </summary>
public class EmailGalleryProductTests
{
    /// <summary>The emails, by the file each is written to.</summary>
    public static IEnumerable<(string File, EmailContent Email)> Gallery()
    {
        const string app = "https://app.cadencee.ai";
        yield return ("product-password-reset", AccountEmailContent.PasswordReset(
            "anna.muller@pavillon46.ch", "Anna Müller", $"{app}/reset-password?token=Qm9uam91ciBBbm5hIQ"));
        yield return ("product-email-verification", AccountEmailContent.EmailVerification(
            "luc.favre@pavillon46.ch", "Luc Favre", $"{app}/verify-email?token=VmVyaWZ5IEx1Yw"));
        yield return ("product-workspace-invitation", AccountEmailContent.WorkspaceInvitation(
            "claire.rochat@bluewin.ch", "Pavillon 46", "Miguel van Ackere", $"{app}/invites/accept?token=SW52aXRlIENsYWlyZQ", "read_only"));
        yield return ("product-task-reminder", AccountEmailContent.TaskReminder(
            "Anna Müller", "Call Ada Rossi about her Founding membership renewal",
            new DateTime(2026, 11, 27, 13, 30, 0, DateTimeKind.Utc), $"{app}/tasks/3f1c2a9e"));

        // The contracts: the counterparty's in the club's look, the team's in CadenceE's.
        const string title = "Founding Membership Agreement 2027";
        yield return ("club-contract-for-signature", ContractEmailContent.ForSignature(
            "CadenceE", "Jean-Marc Dubois", "Pavillon 46", title, $"{app}/sign/c2lnbiBKZWFuLU1hcmM", hasAttachment: true));
        yield return ("club-contract-reminder", ContractEmailContent.Reminder(
            "CadenceE", "Jean-Marc Dubois", "Pavillon 46", title,
            new DateTime(2026, 11, 23, 9, 12, 0, DateTimeKind.Utc), new DateTime(2026, 12, 23, 9, 12, 0, DateTimeKind.Utc)));
        yield return ("product-contract-signed", ContractEmailContent.SignedNotification(
            "CadenceE", "Miguel van Ackere", "Jean-Marc Dubois", title, $"{app}/leads/8b0f4e21"));
        yield return ("club-contract-executed", ContractEmailContent.Executed(
            "CadenceE", "Jean-Marc Dubois", "Pavillon 46", title, ContractBody, SignatureRecord, hasAttachment: true));
    }

    private const string ContractBody =
        "FOUNDING MEMBERSHIP AGREEMENT 2027\n\n" +
        "Between Pavillon 46 SA, Avenue de Rhodanie 46, 1007 Lausanne (\"the Club\")\n" +
        "and Jean-Marc Dubois, Chemin des Fleurettes 12, 1007 Lausanne (\"the Member\").\n\n" +
        "1. Membership. The Club admits the Member as a Founding Member from 1 January 2027 to 31 December 2027.\n\n" +
        "2. Fees. The annual fee is CHF 4,800.00, payable in advance by 15 January 2027. A deposit of CHF 1,200.00 " +
        "is due on signature and is credited against the annual fee.\n\n" +
        "3. Renewal. The membership renews for a further year unless either party gives notice in writing " +
        "by 30 September 2027.\n\n" +
        "4. Governing law. This agreement is governed by Swiss law; the courts of Lausanne have jurisdiction.";

    private const string SignatureRecord =
        "SIGNATURE RECORD\n\n" +
        "Counterparty : Jean-Marc Dubois\n" +
        "Signed       : 2026-11-24 18:42:07 UTC\n" +
        "From         : 85.218.14.32\n\n" +
        "Pavillon 46  : Miguel van Ackere\n" +
        "Signed       : 2026-11-25 08:15:51 UTC\n\n" +
        "Document hash: 4f1d9c0e8b7a6e2f3c5d1a9b8e7f6c5d4b3a2918f7e6d5c4b3a29180f7e6d5c4\n\n" +
        "This is a simple electronic signature: a typed name recorded with its\n" +
        "timestamp, origin and a hash of the exact text signed. It is not a\n" +
        "qualified electronic signature under ZertES or eIDAS.";

    [Fact]
    public void EveryEmailIsWrittenForLookingAt_WhenAskedFor()
    {
        var all = Gallery().ToList();
        all.Select(g => g.File).Should().OnlyHaveUniqueItems();
        foreach (var (file, email) in all)
        {
            file.Should().MatchRegex("^(product|club)-[a-z0-9-]+$");
            email.Html.Should().StartWith("<!DOCTYPE html>", file);
            email.Text.Should().NotBeNullOrWhiteSpace(file);
        }

        var dir = Environment.GetEnvironmentVariable("EMAIL_PREVIEW_DIR");
        if (string.IsNullOrWhiteSpace(dir)) return;
        Directory.CreateDirectory(dir);
        foreach (var (file, email) in all)
        {
            File.WriteAllText(Path.Combine(dir, file + ".html"), email.Html);
            File.WriteAllText(Path.Combine(dir, file + ".txt"), $"Subject: {email.Subject}\n\n{email.Text}");
        }
    }
}
