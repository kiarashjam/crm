using System.Net.Mail;
using System.Text.Json;

namespace ACI.WebApi.Tests;

/// <summary>
/// The committed sender address, which nothing else checks.
/// </summary>
/// <remarks>
/// <para>
/// <c>Email:FromAddress</c> is the one email setting that is committed rather than
/// supplied by the environment, and a blank or malformed value does not fail
/// anywhere: <c>SmtpEmailSender</c> logs an error and returns <c>false</c>, so every
/// message is silently not sent. The product is careful to report that honestly —
/// <c>emailSent: false</c>, no <c>executedCopySentAtUtc</c> — which means a typo
/// here looks exactly like an unconfigured relay rather than like a mistake.
/// </para>
/// <para>
/// Reads the real <c>appsettings.json</c> rather than a fixture, because the point
/// is the value that ships.
/// </para>
/// </remarks>
public class EmailSenderConfigTests
{
    private static JsonElement EmailSection()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "backend")))
        {
            dir = dir.Parent;
        }
        dir.Should().NotBeNull("the repository root should be findable from the test assembly");

        var path = Path.Combine(dir!.FullName, "backend", "src", "ACI.WebApi", "appsettings.json");
        File.Exists(path).Should().BeTrue($"expected appsettings.json at {path}");

        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        doc.RootElement.TryGetProperty("Email", out var email).Should().BeTrue(
            "the Email section is what configures every message the product sends");
        return email.Clone();
    }

    [Fact]
    public void TheCommittedSenderIsAUsableAddress()
    {
        var from = EmailSection().GetProperty("FromAddress").GetString();

        from.Should().NotBeNullOrWhiteSpace(
            "an empty sender disables every email, and the only sign of it is a log line");

        // MailAddress is the same parse MailboxAddress will do at send time, so a
        // value that fails here would fail on the first real message instead.
        var parsed = () => new MailAddress(from!);
        parsed.Should().NotThrow($"\"{from}\" has to be a valid email address");

        from!.Trim().Should().Be(from, "leading or trailing space in a header value");
    }

    [Fact]
    public void TheRelayAndSenderAgreeWithWhatTheSetupGuideSaysToVerify()
    {
        // SendGrid refuses mail from an unverified sender, so the address a person
        // is told to verify has to be the address the application actually sends
        // from. Those are written down in two places — this file and
        // SECRETS_SETUP.md — and a mismatch produces a 403 from the relay with
        // nothing in the app to explain it.
        var email = EmailSection();
        var from = email.GetProperty("FromAddress").GetString()!;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "backend")))
        {
            dir = dir.Parent;
        }
        var guide = Path.Combine(dir!.FullName, "SECRETS_SETUP.md");
        File.Exists(guide).Should().BeTrue($"expected the setup guide at {guide}");

        File.ReadAllText(guide).Should().Contain(from,
            "the guide tells the reader which sender to verify in SendGrid; if it "
            + "names a different address than the one that ships, following it "
            + "verifies the wrong mailbox and nothing sends");
    }
}
