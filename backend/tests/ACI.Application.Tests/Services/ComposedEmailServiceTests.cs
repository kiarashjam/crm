using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace ACI.Application.Tests.Services;

/// <summary>
/// Email from the composer and the assistant: checked here, sent through the outbox,
/// and only "sent" when it was. What the outbox does with it (footer, Reply-To,
/// suppression) is in OutboxServiceTests.
/// </summary>
public class ComposedEmailServiceTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Rep = Guid.NewGuid();
    private readonly Mock<IOutbox> _outbox = new();
    private OutboundEmailRequest? _queued;

    private ComposedEmailService Service(OutboxAttempt? attempt = null, Error? refusal = null)
    {
        _outbox.Setup(o => o.QueueEmailAsync(It.IsAny<OutboundEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OutboundEmailRequest request, CancellationToken _) =>
            {
                _queued = request;
                if (refusal != null) return Result.Failure<OutboundMessage>(refusal);
                return Result.Success(new OutboundMessage
                {
                    Id = Guid.NewGuid(), OrganizationId = Org, To = string.Join(", ", request.To),
                    Cc = request.Cc.Count == 0 ? null : string.Join(", ", request.Cc), Bcc = request.Bcc.Count == 0 ? null : string.Join(", ", request.Bcc),
                    Subject = request.Subject, Body = request.Body, CreatedAtUtc = DateTime.UtcNow,
                });
            });
        _outbox.Setup(o => o.SendNowAsync(It.IsAny<OutboundMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(attempt ?? new OutboxAttempt(OutboxAttemptResult.Sent));
        return new ComposedEmailService(_outbox.Object, NullLogger<ComposedEmailService>.Instance);
    }

    [Fact]
    public async Task ItGoesThroughTheOutbox_AsThePersonsOwnResponse_WithTheFirstAttemptNow()
    {
        var lead = Guid.NewGuid();
        var result = await Service().SendAsync(Org, new SendEmailRequest(
            "jean@example.ch; anne@example.ch", "boss@club.ch", "archive@club.ch", "Votre visite", "Bonjour <Jean>", LeadId: lead), Rep);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("sent");
        result.Value.To.Should().Be("jean@example.ch, anne@example.ch");
        _queued!.To.Should().Equal("jean@example.ch", "anne@example.ch");
        _queued.Cc.Should().Equal("boss@club.ch");
        _queued.Bcc.Should().Equal("archive@club.ch");
        _queued.Source.Should().Be(OutboundSources.Composer);
        _queued.CreatedByUserId.Should().Be(Rep);
        _queued.ReplyToUserId.Should().Be(Rep, "a reply goes to whoever wrote it");
        _queued.LeadId.Should().Be(lead);
        _queued.CountsAsResponse.Should().BeTrue();
        _queued.PersonWaiting.Should().BeTrue();
        _outbox.Verify(o => o.SendNowAsync(It.IsAny<OutboundMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TheAssistantSaysItIsTheAssistant()
    {
        await Service().SendAsync(Org, new SendEmailRequest("jean@example.ch", null, null, "Hello", "Hi", Source: "Assistant"), Rep);

        _queued!.Source.Should().Be(OutboundSources.Assistant);
    }

    [Fact]
    public async Task NotTakenYetIsQueued_NotSent()
    {
        var result = await Service(new OutboxAttempt(OutboxAttemptResult.Queued, "421 busy"))
            .SendAsync(Org, new SendEmailRequest("jean@example.ch", null, null, "Hello", "Hi"), Rep);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("queued");
    }

    [Fact]
    public async Task NoServerMeansNotSent_AndSaysSo()
    {
        // The composer used to report "Email sent" whatever happened.
        var result = await Service(new OutboxAttempt(OutboxAttemptResult.NotConfigured))
            .SendAsync(Org, new SendEmailRequest("jean@example.ch", null, null, "Hello", "Hi"));

        result.Error.Code.Should().Be("Email.NotConfigured");
    }

    [Fact]
    public async Task ARefusalForGoodIsAFailure_WithTheServersWords()
    {
        var result = await Service(new OutboxAttempt(OutboxAttemptResult.Failed, "The mail server refused it. 550 No such user"))
            .SendAsync(Org, new SendEmailRequest("jean@example.ch", null, null, "Hello", "Hi"));

        result.Error.Code.Should().Be("Email.SendFailed");
        result.Error.Description.Should().Contain("550 No such user");
    }

    [Fact]
    public async Task TheOutboxsRefusalIsPassedOn()
    {
        var result = await Service(refusal: new Error("Contact.DoNotContact", "anne@example.ch is marked do not contact, so nothing was sent."))
            .SendAsync(Org, new SendEmailRequest("anne@example.ch", null, null, "Hello", "Hi"));

        result.Error.Code.Should().Be("Contact.DoNotContact");
        _outbox.Verify(o => o.SendNowAsync(It.IsAny<OutboundMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("", "Hello", "Email.NoRecipient")]
    [InlineData("jean@example", "Hello", "Email.InvalidAddress")]
    [InlineData("jean@example.ch", "  ", "Email.NoSubject")]
    public async Task ChecksBeforeSending(string to, string subject, string code)
    {
        var result = await Service().SendAsync(Org, new SendEmailRequest(to, null, null, subject, "Hi"));

        result.Error.Code.Should().Be(code);
        _outbox.Verify(o => o.QueueEmailAsync(It.IsAny<OutboundEmailRequest>(), It.IsAny<CancellationToken>()), Times.Never);
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

        _queued!.Subject.Should().Be("Hello Bcc: everyone@example.ch");
    }
}
