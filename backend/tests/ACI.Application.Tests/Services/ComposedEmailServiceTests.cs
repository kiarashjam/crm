using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace ACI.Application.Tests.Services;

/// <summary>
/// Email from the composer: only "sent" when it was, and a clear reason when not.
/// </summary>
public class ComposedEmailServiceTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private readonly Mock<IEmailSender> _sender = new();
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Rep = Guid.NewGuid();
    private readonly Mock<IOrganizationRepository> _organizations = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IContactRepository> _contacts = new();
    private ComposedEmail? _sent;

    private ComposedEmailService Service(ComposedEmailOutcome outcome = ComposedEmailOutcome.Sent)
    {
        _sender.Setup(s => s.SendComposedAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .Callback((ComposedEmail email, CancellationToken _) => _sent = email)
            .ReturnsAsync(outcome);
        _organizations.Setup(o => o.GetByIdAsync(Org, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ACI.Domain.Entities.Organization { Id = Org, Name = "Pavillon 46", OwnerUserId = Owner });
        foreach (var (id, email) in new[] { (Owner, "owner@pavillon46.ch"), (Rep, "anna@pavillon46.ch") })
        {
            _organizations.Setup(o => o.GetMemberRoleAsync(id, Org, It.IsAny<CancellationToken>())).ReturnsAsync(ACI.Domain.Enums.OrgMemberRole.Member);
            _users.Setup(u => u.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new ACI.Domain.Entities.User { Id = id, Email = email });
        }
        return new ComposedEmailService(
            _sender.Object, new OutreachMail(_organizations.Object, _users.Object), _contacts.Object, NullLogger<ComposedEmailService>.Instance);
    }

    [Fact]
    public async Task AReplyGoesToWhoeverWroteIt()
    {
        // Every workspace sends from one shared address, and nothing set Reply-To, so
        // a prospect's answer landed in that shared inbox instead of with the rep.
        await Service().SendAsync(Org, new SendEmailRequest("jean@example.ch", null, null, "Votre visite", "Bonjour"), Rep);

        _sent!.ReplyTo.Should().Be("anna@pavillon46.ch");
        _sent.FromName.Should().Be("Pavillon 46");
    }

    [Fact]
    public async Task SomeoneWhoHasLeftTheWorkspaceIsNotTheReplyTo_TheOwnerIs()
    {
        var former = Guid.NewGuid();
        _users.Setup(u => u.GetByIdAsync(former, It.IsAny<CancellationToken>())).ReturnsAsync(new ACI.Domain.Entities.User { Id = former, Email = "gone@example.ch" });

        await Service().SendAsync(Org, new SendEmailRequest("jean@example.ch", null, null, "Votre visite", "Bonjour"), former);

        _sent!.ReplyTo.Should().Be("owner@pavillon46.ch");
    }

    [Fact]
    public async Task AnAddressMarkedDoNotContactIsNotSentTo()
    {
        // The flag used to be honoured only by hiding the Email button on a contact's
        // page; the server sent to anyone.
        _contacts.Setup(c => c.FindDoNotContactAddressAsync(Org, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, IReadOnlyCollection<string> all, CancellationToken _) => all.Contains("Anne@Example.ch") ? "Anne@Example.ch" : null);

        var result = await Service().SendAsync(Org, new SendEmailRequest("jean@example.ch", "Anne@Example.ch", null, "Hello", "Hi"));

        result.Error.Code.Should().Be("Contact.DoNotContact");
        result.Error.Description.Should().Contain("Anne@Example.ch");
        _sender.Verify(s => s.SendComposedAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ALeadWhosePersonSaidStopIsNotSentTo()
    {
        var lead = Guid.NewGuid();
        _contacts.Setup(c => c.IsDoNotContactAsync(Org, lead, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await Service().SendAsync(Org, new SendEmailRequest("jean.private@example.ch", null, null, "Hello", "Hi", LeadId: lead));

        result.Error.Code.Should().Be("Contact.DoNotContact");
        _sender.Verify(s => s.SendComposedAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendsToEveryRecipientWithBothParts()
    {
        var result = await Service().SendAsync(Org, new SendEmailRequest(
            "jean@example.ch; anne@example.ch", "boss@club.ch", "archive@club.ch", "Votre visite", "Bonjour <Jean>,\nÀ bientôt"));

        result.IsSuccess.Should().BeTrue();
        _sent!.To.Should().Equal("jean@example.ch", "anne@example.ch");
        _sent.Cc.Should().Equal("boss@club.ch");
        _sent.Bcc.Should().Equal("archive@club.ch");
        _sent.TextBody.Should().Be("Bonjour <Jean>,\nÀ bientôt");
        _sent.HtmlBody.Should().Contain("Bonjour &lt;Jean&gt;,<br>").And.NotContain("<Jean>");
        result.Value.Status.Should().Be("sent");
        result.Value.To.Should().Be("jean@example.ch, anne@example.ch");
        _sent.FromName.Should().Be("Pavillon 46", "a lead should see the club's name, not the product's");
    }

    [Fact]
    public async Task NoServerMeansNotSent_AndSaysSo()
    {
        // The composer used to report "Email sent" whatever happened.
        var result = await Service(ComposedEmailOutcome.NotConfigured)
            .SendAsync(Org, new SendEmailRequest("jean@example.ch", null, null, "Hello", "Hi"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Email.NotConfigured");
    }

    [Fact]
    public async Task ARefusedSendIsAFailure()
    {
        var result = await Service(ComposedEmailOutcome.Failed)
            .SendAsync(Org, new SendEmailRequest("jean@example.ch", null, null, "Hello", "Hi"));

        result.Error.Code.Should().Be("Email.SendFailed");
    }

    [Theory]
    [InlineData("", "Hello", "Email.NoRecipient")]
    [InlineData("jean@example", "Hello", "Email.InvalidAddress")]
    [InlineData("jean@example.ch", "  ", "Email.NoSubject")]
    public async Task ChecksBeforeSending(string to, string subject, string code)
    {
        var result = await Service().SendAsync(Org, new SendEmailRequest(to, null, null, subject, "Hi"));

        result.Error.Code.Should().Be(code);
        _sender.Verify(s => s.SendComposedAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NamesTheAddressThatIsWrong()
    {
        var result = await Service().SendAsync(Org, new SendEmailRequest("jean@example.ch", "not-an-address", null, "Hello", "Hi"));

        result.Error.Description.Should().Contain("\"not-an-address\"");
    }

    [Fact]
    public async Task IsNotAMailingList()
    {
        var many = string.Join(",", Enumerable.Range(0, ComposedEmailService.MaxRecipients + 1).Select(i => $"p{i}@example.ch"));
        var result = await Service().SendAsync(Org, new SendEmailRequest(many, null, null, "Hello", "Hi"));

        result.Error.Code.Should().Be("Email.TooManyRecipients");
    }

    [Fact]
    public async Task ASubjectStaysOneLine()
    {
        await Service().SendAsync(Org, new SendEmailRequest("jean@example.ch", null, null, "Hello\r\nBcc: everyone@example.ch", "Hi"));

        _sent!.Subject.Should().Be("Hello Bcc: everyone@example.ch");
    }
}
