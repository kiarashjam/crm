using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ACI.Application.Common;
using ACI.Application.Configuration;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ACI.Application.Tests.Services;

/// <summary>
/// The one outbox every sender uses: what it refuses, how it fills and addresses a
/// message, and what happens when the mail server or a webhook receiver does not take it.
/// </summary>
public class OutboxServiceTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Rep = Guid.NewGuid();
    private static readonly Guid Lead = Guid.NewGuid();

    private readonly FakeOutboxRepository _repo = new();
    private readonly Mock<IEmailSender> _email = new();
    private readonly Mock<IWebhookTransport> _webhooks = new();
    private readonly Mock<IOrganizationRepository> _organizations = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IContactRepository> _contacts = new();
    private readonly Mock<ISequenceRepository> _targets = new();
    private readonly Mock<IActivityService> _activities = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<IOutboxSignal> _signal = new();
    private readonly EmailSettings _settings = new() { FrontendBaseUrl = "https://crm.example.ch", FromAddress = "info@pavillon46.ch" };
    private readonly Organization _workspace = new() { Id = Org, Name = "Pavillon 46", OwnerUserId = Owner };
    private readonly List<OutboxEmail> _sent = new();
    private OutboxSendResult _answer = OutboxSendResult.Sent;

    public OutboxServiceTests()
    {
        _organizations.Setup(o => o.GetByIdAsync(Org, It.IsAny<CancellationToken>())).ReturnsAsync(() => _workspace);
        foreach (var (id, email, name) in new[] { (Owner, "owner@pavillon46.ch", "Olga Owner"), (Rep, "anna@pavillon46.ch", "Anna Muster") })
        {
            var role = id == Owner ? OrgMemberRole.Owner : OrgMemberRole.Member;
            _organizations.Setup(o => o.GetMemberRoleAsync(id, Org, It.IsAny<CancellationToken>())).ReturnsAsync(role);
            _organizations.Setup(o => o.GetMemberAsync(Org, id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OrganizationMember { OrganizationId = Org, UserId = id, Role = role });
            _users.Setup(u => u.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(new User { Id = id, Email = email, Name = name });
        }
        _targets.Setup(t => t.TargetStateAsync("lead", Lead, Org, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SequenceTargetState(true, "Jean-Michel Dupont", "jean@example.ch", "Club Nautique"));
        _email.Setup(e => e.SendOutboxAsync(It.IsAny<OutboxEmail>(), It.IsAny<CancellationToken>()))
            .Returns((OutboxEmail mail, CancellationToken _) =>
            {
                if (_answer.Outcome == OutboxSendOutcome.Sent) _sent.Add(mail);
                return Task.FromResult(_answer);
            });
        _activities.Setup(a => a.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CreateActivityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new ActivityDto(Guid.NewGuid(), "email", null, null, null, null, null, null, DateTime.UtcNow, null)));
    }

    private OutboxService Service()
    {
        var links = new Mock<IUnsubscribeLinks>();
        links.Setup(l => l.CreateToken(Org, It.IsAny<string>())).Returns((Guid _, string email) => $"t-{email.Split('@')[0]}");
        var outreach = new OutreachMail(_organizations.Object, _users.Object, links.Object, Options.Create(_settings), NullLogger<OutreachMail>.Instance);
        return new OutboxService(_repo, _email.Object, _webhooks.Object, outreach, _contacts.Object, _targets.Object, _organizations.Object,
            _activities.Object, _notifications.Object, _signal.Object, Options.Create(_settings), NullLogger<OutboxService>.Instance);
    }

    private static OutboundEmailRequest Email(params string[] to) => new()
    {
        OrganizationId = Org,
        Source = OutboundSources.Composer,
        To = to.Length == 0 ? ["jean@example.ch"] : to,
        Subject = "Votre visite",
        Body = "Bonjour",
        CreatedByUserId = Rep,
        ReplyToUserId = Rep,
    };

    /// <summary>Queues it and runs one dispatcher pass over everything due.</summary>
    private async Task<OutboundMessage> QueueAndDispatchAsync(OutboundEmailRequest request)
    {
        var service = Service();
        var queued = await service.QueueEmailAsync(request);
        queued.IsSuccess.Should().BeTrue(queued.IsFailure ? queued.Error.Description : "");
        await DispatchAsync(service);
        return queued.Value;
    }

    private async Task DispatchAsync(OutboxService? service = null)
    {
        service ??= Service();
        foreach (var due in await _repo.DueAsync(DateTime.UtcNow, 50)) await service.ProcessAsync(due);
    }

    private void MakeDue()
    {
        foreach (var m in _repo.Messages.Where(m => m.Status == OutboundStatuses.Queued)) m.NextAttemptAtUtc = DateTime.UtcNow.AddSeconds(-1);
    }

    // ── what it refuses ─────────────────────────────────────────────────────────

    [Fact]
    public async Task AnAddressMarkedDoNotContactIsNotQueued_AndIsNamed()
    {
        _contacts.Setup(c => c.FindDoNotContactAddressAsync(Org, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, IReadOnlyCollection<string> all, CancellationToken _) => all.Contains("Anne@Example.ch") ? "Anne@Example.ch" : null);

        var result = await Service().QueueEmailAsync(Email("jean@example.ch") with { Cc = ["Anne@Example.ch"] });

        result.Error.Code.Should().Be("Contact.DoNotContact");
        result.Error.Description.Should().Contain("Anne@Example.ch");
        _repo.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ALeadWhosePersonSaidStopIsNotQueued_WhateverAddressIsUsed()
    {
        _contacts.Setup(c => c.IsDoNotContactAsync(Org, Lead, null, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await Service().QueueEmailAsync(Email("jean.private@example.ch") with { LeadId = Lead });

        result.Error.Code.Should().Be("Contact.DoNotContact");
        _repo.Messages.Should().BeEmpty();
    }

    [Theory]
    [InlineData(EmailSuppressionReasons.Bounced, "bounced on 3 Nov 2026")]
    [InlineData(EmailSuppressionReasons.SpamReport, "marked this workspace's email as spam on 3 Nov 2026")]
    public async Task AnAddressTheMailServiceReportedIsNotQueued(string reason, string because)
    {
        _repo.Suppressions.Add(new EmailSuppression
        {
            Id = Guid.NewGuid(), OrganizationId = Org, Email = "jean@example.ch", Reason = reason, CreatedAtUtc = new DateTime(2026, 11, 3, 9, 0, 0, DateTimeKind.Utc),
        });

        var result = await Service().QueueEmailAsync(Email("Jean@Example.ch"));

        result.Error.Code.Should().Be("Email.Suppressed");
        result.Error.Description.Should().Contain(because);
    }

    [Fact]
    public async Task AStopThatArrivesWhileItWaitsWins()
    {
        // Queued at 9:00, unsubscribed at 9:01, due at 9:02: it must not go.
        var service = Service();
        var queued = (await service.QueueEmailAsync(Email() with { CountsAsResponse = true })).Value;
        _contacts.Setup(c => c.FindDoNotContactAddressAsync(Org, It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("jean@example.ch");

        await DispatchAsync(service);

        _sent.Should().BeEmpty();
        queued.Status.Should().Be(OutboundStatuses.Suppressed);
        // Whoever wrote it was told it was on its way, so they hear that it is not.
        _notifications.Verify(n => n.CreateAsync(Rep, Org, It.Is<CreateNotificationRequest>(r => r.Title == "An email was not sent"), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task AnEmailAboutALeadDeletedWhileItWaitedDoesNotGo()
    {
        // Queued, then the lead erased (or its import undone) before the next attempt.
        var service = Service();
        var queued = (await service.QueueEmailAsync(Email() with { LeadId = Lead, CountsAsResponse = true })).Value;
        _targets.Setup(t => t.TargetStateAsync("lead", Lead, Org, It.IsAny<CancellationToken>())).ReturnsAsync(SequenceTargetState.Missing);

        await DispatchAsync(service);

        _sent.Should().BeEmpty();
        queued.Status.Should().Be(OutboundStatuses.Cancelled);
        queued.LastError.Should().Be("The lead it was for has been deleted, so it was not sent.");
        _notifications.Verify(n => n.CreateAsync(Rep, Org, It.Is<CreateNotificationRequest>(r => r.Title == "An email was not sent"), It.IsAny<CancellationToken>()));
        _activities.Verify(a => a.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CreateActivityRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── how it is written and addressed ─────────────────────────────────────────

    [Fact]
    public async Task EachVisibleRecipientCanUnsubscribe_ButBccIsNotNamed()
    {
        await QueueAndDispatchAsync(Email("jean@example.ch", "anne@example.ch") with { Cc = ["boss@club.ch"], Bcc = ["archive@club.ch"] });

        var sent = _sent.Single();
        foreach (var address in new[] { "jean@example.ch", "anne@example.ch", "boss@club.ch" })
        {
            sent.TextBody.Should().Contain($"{address}: https://crm.example.ch/unsubscribe/t-{address.Split('@')[0]}");
            sent.HtmlBody.Should().Contain($"href=\"https://crm.example.ch/unsubscribe/t-{address.Split('@')[0]}\"");
        }
        sent.TextBody.Should().StartWith("Bonjour").And.Contain("Don't want emails from Pavillon 46?").And.NotContain("archive@club.ch");
        sent.Bcc.Should().Equal("archive@club.ch");
    }

    [Fact]
    public async Task ItGoesOutUnderTheWorkspacesName_WithRepliesToWhoeverWroteIt()
    {
        await QueueAndDispatchAsync(Email());

        var sent = _sent.Single();
        sent.FromName.Should().Be("Pavillon 46");
        sent.FromAddress.Should().BeNull("no verified domain: the configured shared address");
        sent.ReplyTo.Should().Equal("anna@pavillon46.ch");
        sent.MessageId.Should().MatchRegex(@"^<[0-9a-f]{32}@pavillon46\.ch>$");
    }

    [Fact]
    public async Task AWorkspaceNameLongerThanTheFromNameColumnIsCut_NotFatal()
    {
        // Organizations.Name holds 256 characters, OutboundMessages.FromName 200. On SQL
        // Server a longer name failed the insert, so nothing that workspace sent could queue.
        _workspace.Name = new string('W', 256);

        var message = await QueueAndDispatchAsync(Email());

        message.FromName.Should().HaveLength(OutboxService.MaxFromName);
        _sent.Single().FromName.Should().Be(new string('W', OutboxService.MaxFromName));
    }

    [Fact]
    public async Task SomeoneWhoHasLeftTheWorkspaceIsNotTheReplyTo_TheOwnerIs()
    {
        var former = Guid.NewGuid();
        _users.Setup(u => u.GetByIdAsync(former, It.IsAny<CancellationToken>())).ReturnsAsync(new User { Id = former, Email = "gone@example.ch" });

        await QueueAndDispatchAsync(Email() with { ReplyToUserId = former });

        _sent.Single().ReplyTo.Should().Equal("owner@pavillon46.ch");
    }

    [Fact]
    public async Task WithReplyCapture_ARepliesAddressOfItsOwnIsAdded_AndTheRepStillGetsTheReply()
    {
        _settings.InboundReplyDomain = "Reply.Cadence.Example";
        _settings.InboundParseSecret = "s3cret";

        var message = await QueueAndDispatchAsync(Email());

        message.ReplyToken.Should().MatchRegex("^[a-z2-7]{21}$");
        _sent.Single().ReplyTo.Should().Equal("anna@pavillon46.ch", $"reply+{message.ReplyToken}@reply.cadence.example");
    }

    [Fact]
    public async Task WithoutTheInboundEndpointSwitchedOn_NoReplyAddressIsNamed()
    {
        // Replies sent there would be posted to an endpoint that turns them away.
        _settings.InboundReplyDomain = "reply.cadence.example";

        var message = await QueueAndDispatchAsync(Email());

        message.ReplyToken.Should().BeNull();
        _sent.Single().ReplyTo.Should().Equal("anna@pavillon46.ch");
    }

    [Theory]
    [InlineData("acme.example={org}", "hello@acme.example")]
    [InlineData("club.example={other}, Acme.Example. = {org}", "hello@acme.example")]
    [InlineData("other.example={org}", null)]
    [InlineData("", null)]
    // Any workspace may save any address: a domain verified for another workspace, or
    // listed for none, must not let this one send as it.
    [InlineData("acme.example={other}", null)]
    [InlineData("acme.example", null)]
    public async Task AWorkspaceSendsFromItsOwnAddressOnlyOnceItsDomainIsVerifiedForIt(string verified, string? from)
    {
        _workspace.SendingAddress = "hello@acme.example";
        _settings.VerifiedSendingDomains = verified.Replace("{org}", Org.ToString()).Replace("{other}", Guid.NewGuid().ToString());

        var message = await QueueAndDispatchAsync(Email());

        _sent.Single().FromAddress.Should().Be(from);
        message.MessageIdHeader.Should().EndWith(from == null ? "@pavillon46.ch>" : "@acme.example>");
    }

    [Theory]
    [InlineData("pavillon46.ch={org}", "members@pavillon46.ch")]
    // A sender a request names (the member portal's, from Welcome & portal) is any address
    // someone typed: on a domain not verified for this workspace it is left out, and the email
    // goes from the usual address, still under the name and reply-to it names.
    [InlineData("pavillon46.ch={other}", null)]
    [InlineData("", null)]
    public async Task ASenderTheRequestNames_StandsIn_ItsAddressOnlyOnADomainVerifiedForTheWorkspace(string verified, string? from)
    {
        _settings.VerifiedSendingDomains = verified.Replace("{org}", Org.ToString()).Replace("{other}", Guid.NewGuid().ToString());

        var message = await QueueAndDispatchAsync(Email() with
        {
            FromName = "Pavillon 46 Members\r\nBcc: someone@example.ch",
            FromAddress = "Members@Pavillon46.ch",
            ReplyTo = "concierge@pavillon46.ch",
        });

        var sent = _sent.Single();
        sent.FromAddress.Should().Be(from);
        sent.FromName.Should().Be("Pavillon 46 Members Bcc: someone@example.ch", "a name stays on one header line");
        sent.ReplyTo.Should().Equal("concierge@pavillon46.ch");
        message.MessageIdHeader.Should().EndWith("@pavillon46.ch>");
    }

    [Fact]
    public async Task AReplyToTheRequestNamesThatIsNoAddress_LeavesTheUsualOne()
    {
        await QueueAndDispatchAsync(Email() with { ReplyTo = "not an address" });

        _sent.Single().ReplyTo.Should().Equal("anna@pavillon46.ch");
    }

    [Fact]
    public async Task PlaceholdersAreFilledFromTheRecord_InTheSubjectToo()
    {
        await QueueAndDispatchAsync(Email() with
        {
            LeadId = Lead,
            Subject = "{{firstName}}, about {{company}}",
            Body = "Hi {{firstName}} {{lastName}}, {{senderName}} from {{workspace}} here. {{unknown}}",
            ReplyToUserId = Rep,
        });

        var sent = _sent.Single();
        sent.Subject.Should().Be("Jean-Michel, about Club Nautique");
        sent.TextBody.Should().StartWith("Hi Jean-Michel Dupont, Anna Muster from Pavillon 46 here. \n", "unattended mail leaves out what it cannot fill");
    }

    [Fact]
    public async Task APersonWaitingIsToldWhichPlaceholderCannotBeFilled()
    {
        var result = await Service().QueueEmailAsync(Email() with { PersonWaiting = true, Body = "Hi {{firstName}}, {{company}}" });

        result.Error.Code.Should().Be("Email.MergeField");
        result.Error.Description.Should().Contain("{{firstName}}, {{company}}");
        _repo.Messages.Should().BeEmpty();
    }

    [Fact]
    public void AFallbackFillsAnEmptyPlaceholder()
    {
        var values = MergeFields.For(null, "jean@example.ch", null, null, "Pavillon 46");

        MergeFields.Fill("Hi {{ firstName | there }}, from {{workspace|us}}", values, out var unfilled).Should().Be("Hi there, from Pavillon 46");
        unfilled.Should().BeEmpty();
        MergeFields.Fill("{{company}} {{Company}}", values, out unfilled).Should().Be(" ");
        unfilled.Should().Equal("{{company}}");
    }

    // ── sending ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ASentEmailIsWrittenOnTheTimelineOnce()
    {
        var message = await QueueAndDispatchAsync(Email() with { LeadId = Lead, CountsAsResponse = true });
        await DispatchAsync();

        message.Status.Should().Be(OutboundStatuses.Sent);
        message.ActivityId.Should().NotBeNull();
        _activities.Verify(a => a.CreateAsync(Rep, Org, It.Is<CreateActivityRequest>(r =>
            r.Type == "email" && r.Subject == "Email: Votre visite" && r.LeadId == Lead && !r.IsAutomated && r.Participants == "jean@example.ch"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UnattendedMailIsOnTheTimelineAsAutomated()
    {
        await QueueAndDispatchAsync(Email() with { LeadId = Lead, Source = OutboundSources.Sequence, Subject = "Hello" });

        _activities.Verify(a => a.CreateAsync(Rep, Org, It.Is<CreateActivityRequest>(r => r.Subject == "Hello" && r.IsAutomated), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task WhenTheServerDoesNotTakeItItIsTriedAgainLater_WithGrowingGaps()
    {
        _answer = new OutboxSendResult(OutboxSendOutcome.Retry, "421 try later");
        var message = await QueueAndDispatchAsync(Email());

        message.Status.Should().Be(OutboundStatuses.Queued);
        message.Attempts.Should().Be(1);
        message.LastError.Should().Be("421 try later");
        message.NextAttemptAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(1), TimeSpan.FromSeconds(10));
        message.ClaimedUntilUtc.Should().BeNull();

        MakeDue();
        await DispatchAsync();
        message.NextAttemptAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(5), TimeSpan.FromSeconds(35));

        _answer = OutboxSendResult.Sent;
        MakeDue();
        await DispatchAsync();
        message.Status.Should().Be(OutboundStatuses.Sent);
        _sent.Should().ContainSingle();
    }

    [Fact]
    public async Task AfterTheLastAttemptItFails_AndWhoeverQueuedItHears()
    {
        _answer = new OutboxSendResult(OutboxSendOutcome.Retry, "Connection refused");
        var message = await QueueAndDispatchAsync(Email() with { LeadId = Lead });
        for (var i = 1; i < OutboxService.MaxAttempts; i++)
        {
            MakeDue();
            await DispatchAsync();
        }

        message.Attempts.Should().Be(OutboxService.MaxAttempts);
        message.Status.Should().Be(OutboundStatuses.Failed);
        message.NextAttemptAtUtc.Should().BeNull();
        _notifications.Verify(n => n.CreateAsync(Rep, Org, It.Is<CreateNotificationRequest>(r =>
            r.Title == "An email could not be sent" && r.Link == $"/leads/{Lead}" && r.Message!.Contains("Connection refused")), It.IsAny<CancellationToken>()), Times.Once);
        OutboxService.RetryDelays.Aggregate(TimeSpan.Zero, (a, b) => a + b).Should().BeCloseTo(TimeSpan.FromHours(46), TimeSpan.FromHours(1));
    }

    [Fact]
    public async Task ARefusalForGoodIsNotRetried()
    {
        _answer = new OutboxSendResult(OutboxSendOutcome.Rejected, "550 No such user", 550);

        var message = await QueueAndDispatchAsync(Email());
        MakeDue();
        await DispatchAsync();

        message.Status.Should().Be(OutboundStatuses.Failed);
        message.Attempts.Should().Be(1);
        message.LastStatusCode.Should().Be(550);
    }

    [Fact]
    public async Task ASequenceEmailTheOutboxGivesUpOnStopsItsEnrollment()
    {
        var enrollment = new OutreachEnrollment { Id = Guid.NewGuid(), OrganizationId = Org, Status = "active", NextActionAtUtc = DateTime.UtcNow };
        _targets.Setup(t => t.GetEnrollmentAsync(enrollment.Id, Org, It.IsAny<CancellationToken>())).ReturnsAsync(enrollment);
        _answer = new OutboxSendResult(OutboxSendOutcome.Rejected, "550 No such user", 550);

        await QueueAndDispatchAsync(Email() with { Source = OutboundSources.Sequence, SourceId = enrollment.Id });

        enrollment.Status.Should().Be("stopped");
        _targets.Verify(t => t.SaveEnrollmentAsync(enrollment, It.IsAny<CancellationToken>()));
    }

    [Theory]
    [InlineData("stopped", false)]
    [InlineData(null, false)]
    [InlineData("completed", true)]
    [InlineData("paused", true)]
    public async Task ASequenceEmailStillWaitingWhenItsSequenceEndsIsNotSent(string? statusMeanwhile, bool sent)
    {
        // The step queued it, and the mail server did not take it. Meanwhile the person
        // answered (which stops the enrollment), or someone took them out of the sequence
        // (null: the enrollment is gone). The last step's email completes the enrollment
        // the moment it is queued, so a completed one still sends.
        var enrollment = new OutreachEnrollment { Id = Guid.NewGuid(), OrganizationId = Org, Status = "active" };
        OutreachEnrollment? current = enrollment;
        _targets.Setup(t => t.GetEnrollmentAsync(enrollment.Id, Org, It.IsAny<CancellationToken>())).ReturnsAsync(() => current);
        _answer = new OutboxSendResult(OutboxSendOutcome.Retry, "421 Try again later", 421);
        var message = await QueueAndDispatchAsync(Email() with { Source = OutboundSources.Sequence, SourceId = enrollment.Id, LeadId = Lead });
        message.Status.Should().Be(OutboundStatuses.Queued);

        if (statusMeanwhile == null) current = null;
        else enrollment.Status = statusMeanwhile;
        _answer = OutboxSendResult.Sent;
        MakeDue();
        await DispatchAsync();

        _sent.Should().HaveCount(sent ? 1 : 0);
        message.Status.Should().Be(sent ? OutboundStatuses.Sent : OutboundStatuses.Cancelled);
        if (!sent) message.LastError.Should().Contain("not sent");
    }

    [Fact]
    public async Task APersonWaitingGetsTheFirstAttemptAtOnce_HeldFromTheDispatcherMeanwhile()
    {
        var service = Service();
        var message = (await service.QueueEmailAsync(Email() with { PersonWaiting = true })).Value;

        message.Attempts.Should().Be(1);
        message.ClaimedUntilUtc.Should().BeAfter(DateTime.UtcNow);
        (await _repo.DueAsync(DateTime.UtcNow, 50)).Should().BeEmpty("the person's own attempt holds it");
        _signal.Verify(s => s.Notify(), Times.Never);

        (await service.SendNowAsync(message)).Result.Should().Be(OutboxAttemptResult.Sent);
        message.Status.Should().Be(OutboundStatuses.Sent);
    }

    [Fact]
    public async Task WithNoMailServer_APersonsEmailFailsForGood_WhileUnattendedMailWaitsForOne()
    {
        _answer = OutboxSendResult.NotConfigured;
        var service = Service();
        var composed = (await service.QueueEmailAsync(Email() with { PersonWaiting = true })).Value;

        (await service.SendNowAsync(composed)).Result.Should().Be(OutboxAttemptResult.NotConfigured);
        composed.Status.Should().Be(OutboundStatuses.Failed, "the person was told it was not sent, so it must not go later");

        var automated = await QueueAndDispatchAsync(Email() with { Source = OutboundSources.Automation });
        automated.Status.Should().Be(OutboundStatuses.Queued);
    }

    [Fact]
    public async Task AnotherInstanceThatTookItFirstSendsIt_NotThisOne()
    {
        var service = Service();
        var message = (await service.QueueEmailAsync(Email())).Value;
        var due = (await _repo.DueAsync(DateTime.UtcNow, 50)).Single();
        (await _repo.TryClaimAsync(due.Id, due.Attempts, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(5))).Should().BeTrue();

        await service.ProcessAsync(due);

        _email.Verify(e => e.SendOutboxAsync(It.IsAny<OutboxEmail>(), It.IsAny<CancellationToken>()), Times.Never);
        message.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task QueuingWakesTheDispatcher()
    {
        await Service().QueueEmailAsync(Email());

        _signal.Verify(s => s.Notify(), Times.Once);
    }

    // ── people ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task OnlyWhoeverQueuedItOrAManagerCancelsIt_AndOnlyWhileItWaits()
    {
        var service = Service();
        var message = (await service.QueueEmailAsync(Email())).Value;
        var stranger = Guid.NewGuid();
        _organizations.Setup(o => o.GetMemberRoleAsync(stranger, Org, It.IsAny<CancellationToken>())).ReturnsAsync(OrgMemberRole.Member);
        _organizations.Setup(o => o.GetMemberAsync(Org, stranger, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrganizationMember { OrganizationId = Org, UserId = stranger, Role = OrgMemberRole.Member });

        (await service.CancelAsync(message.Id, Org, stranger)).Error.Code.Should().Be("Email.NotOwner");
        (await service.CancelAsync(message.Id, Guid.NewGuid(), Rep)).Error.Code.Should().Be("Email.NotFound", "another workspace's message");
        (await service.CancelAsync(message.Id, Org, Rep)).IsSuccess.Should().BeTrue();
        message.Status.Should().Be(OutboundStatuses.Cancelled);
        (await service.CancelAsync(message.Id, Org, Owner)).Error.Code.Should().Be("Email.Conflict");
    }

    // ── webhooks ────────────────────────────────────────────────────────────────

    private readonly List<(Uri Url, string Body, IReadOnlyDictionary<string, string> Headers)> _posts = new();

    private void Receiver(params int?[] answers)
    {
        var queue = new Queue<int?>(answers);
        _webhooks.Setup(w => w.PostAsync(It.IsAny<Uri>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Returns((Uri url, string body, IReadOnlyDictionary<string, string> headers, CancellationToken _) =>
            {
                _posts.Add((url, body, headers));
                var status = queue.Count > 0 ? queue.Dequeue() : 200;
                return Task.FromResult(new WebhookPostResult(status, status is >= 200 and < 300 ? null : status == null ? "timed out" : $"answered {status}"));
            });
    }

    [Fact]
    public async Task AWorkspaceThatPostsNowhereQueuesNothing()
    {
        await Service().QueueAsync(Org, "lead.created", new { Id = Lead });

        _repo.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task AnEventIsPostedSigned_WithItsIdOnEveryAttempt_AndTheFieldsReceiversAlreadyUse()
    {
        _workspace.OutboundWebhookUrl = "https://hooks.example.com/cadence";
        _workspace.OutboundWebhookSecret = "s3cret";
        Receiver(503, 200);
        var service = Service();

        await service.QueueAsync(Org, "lead.created", new { Id = Lead, Name = "Jean", Email = "jean@example.ch" });
        await DispatchAsync(service);
        MakeDue();
        await DispatchAsync(service);

        _posts.Should().HaveCount(2);
        var message = _repo.Messages.Single();
        message.Status.Should().Be(OutboundStatuses.Sent);
        message.Attempts.Should().Be(2);
        foreach (var (url, body, headers) in _posts)
        {
            url.Should().Be(new Uri("https://hooks.example.com/cadence"));
            headers["X-Cadence-Event-Id"].Should().Be(message.Id.ToString("D"), "the same on every attempt, so a receiver can drop a repeat");
            headers["X-Cadence-Event"].Should().Be("lead.created");
            var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("s3cret"), Encoding.UTF8.GetBytes(body))).ToLowerInvariant();
            headers["X-Cadence-Signature"].Should().Be(expected);
            using var doc = JsonDocument.Parse(body);
            doc.RootElement.GetProperty("eventName").GetString().Should().Be("lead.created");
            doc.RootElement.GetProperty("payload").GetProperty("Id").GetGuid().Should().Be(Lead, "the payload is serialized as it always was");
            doc.RootElement.GetProperty("payload").GetProperty("Email").GetString().Should().Be("jean@example.ch");
            doc.RootElement.GetProperty("id").GetGuid().Should().Be(message.Id);
            doc.RootElement.GetProperty("organizationId").GetGuid().Should().Be(Org);
            doc.RootElement.GetProperty("sentAtUtc").GetString().Should().EndWith("Z");
        }
    }

    [Fact]
    public async Task AReceiverThatSaysGoneIsNotTriedAgain()
    {
        _workspace.OutboundWebhookUrl = "https://hooks.example.com/cadence";
        Receiver(410);
        var service = Service();

        await service.QueueAsync(Org, "deal.won", new { id = Guid.NewGuid() });
        await DispatchAsync(service);
        MakeDue();
        await DispatchAsync(service);

        _posts.Should().ContainSingle();
        _repo.Messages.Single().Status.Should().Be(OutboundStatuses.Failed);
        _repo.Messages.Single().LastStatusCode.Should().Be(410);
    }

    [Fact]
    public async Task AnEventForAnAddressSinceRemovedIsCancelled_NotSent()
    {
        _workspace.OutboundWebhookUrl = "https://hooks.example.com/cadence";
        Receiver();
        var service = Service();
        await service.QueueAsync(Org, "deal.won", new { id = Guid.NewGuid() });
        _workspace.OutboundWebhookUrl = null;

        await DispatchAsync(service);

        _posts.Should().BeEmpty();
        _repo.Messages.Single().Status.Should().Be(OutboundStatuses.Cancelled);
    }

    [Fact]
    public async Task OnlyTheOwnerAndManagersSeeTheDeliveries()
    {
        _workspace.OutboundWebhookUrl = "https://hooks.example.com/cadence";
        Receiver(500);
        var service = Service();
        await service.QueueAsync(Org, "lead.created", new { Id = Lead });
        await DispatchAsync(service);

        (await service.WebhookDeliveriesAsync(Org, Rep)).Error.Code.Should().Be("Permission.settings.manage");
        var delivery = (await service.WebhookDeliveriesAsync(Org, Owner)).Value.Single();
        delivery.EventName.Should().Be("lead.created");
        delivery.Status.Should().Be("queued");
        delivery.LastStatusCode.Should().Be(500);
        delivery.NextAttemptAtUtc.Should().NotBeNull();
    }
}
