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
    private readonly Mock<IOrganizationRepository> _organizations = new();
    private ComposedEmail? _sent;

    private ComposedEmailService Service(ComposedEmailOutcome outcome = ComposedEmailOutcome.Sent)
    {
        _sender.Setup(s => s.SendComposedAsync(It.IsAny<ComposedEmail>(), It.IsAny<CancellationToken>()))
            .Callback((ComposedEmail email, CancellationToken _) => _sent = email)
            .ReturnsAsync(outcome);
        _organizations.Setup(o => o.GetByIdAsync(Org, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ACI.Domain.Entities.Organization { Id = Org, Name = "Pavillon 46" });
        return new ComposedEmailService(_sender.Object, _organizations.Object, NullLogger<ComposedEmailService>.Instance);
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
