using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace ACI.Application.Tests.Services;

/// <summary>
/// The sequence runner writes to real people unattended. Each test pins a way it
/// used to get that wrong. What happens to an email after a step queues it (filling,
/// sending, retrying, the timeline) is the outbox's, in OutboxServiceTests.
/// </summary>
public class SequenceServiceTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();
    private static readonly Guid Rep = Guid.NewGuid();

    private readonly Mock<ISequenceRepository> _repo = new();
    private readonly Mock<IOutbox> _outbox = new();
    private readonly Mock<IActivityService> _activities = new();
    private readonly Mock<ITaskService> _tasks = new();
    private readonly List<OutboundEmailRequest> _queued = new();

    public SequenceServiceTests()
    {
        _outbox.Setup(o => o.QueueEmailAsync(It.IsAny<OutboundEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OutboundEmailRequest request, CancellationToken _) =>
            {
                _queued.Add(request);
                return Result.Success(new OutboundMessage { Id = Guid.NewGuid(), OrganizationId = Org });
            });
    }

    private SequenceService Service() => new(
        _repo.Object, _outbox.Object, _activities.Object, _tasks.Object, NullLogger<SequenceService>.Instance);

    private static OutreachSequence Sequence(params OutreachStep[] steps)
    {
        var id = Guid.NewGuid();
        foreach (var step in steps) step.SequenceId = id;
        return new OutreachSequence { Id = id, OrganizationId = Org, Name = "Welcome", Status = "active", Steps = steps.ToList() };
    }

    private static OutreachStep EmailStep(int order, string subject, string body) =>
        new() { Id = Guid.NewGuid(), SortOrder = order, Type = "email", Subject = subject, Body = body, DayOffset = order == 0 ? 0 : 2 };

    private static OutreachEnrollment Due(OutreachSequence sequence, string targetType = "lead") => new()
    {
        Id = Guid.NewGuid(),
        SequenceId = sequence.Id,
        OrganizationId = Org,
        EnrolledByUserId = Owner,
        TargetType = targetType,
        TargetId = Guid.NewGuid(),
        TargetName = "Jean-Michel Dupont",
        TargetEmail = "old-address@example.ch",
        Status = "active",
        CurrentStep = 0,
        EnrolledAtUtc = DateTime.UtcNow.AddDays(-1),
        NextActionAtUtc = DateTime.UtcNow.AddMinutes(-5),
    };

    private void Ready(OutreachSequence sequence, OutreachEnrollment enrollment, SequenceTargetState target, bool claimed = true)
    {
        _repo.Setup(r => r.DueAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { enrollment });
        _repo.Setup(r => r.TryClaimAsync(enrollment.Id, It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(claimed);
        _repo.Setup(r => r.GetAsync(sequence.Id, Org, It.IsAny<CancellationToken>())).ReturnsAsync(sequence);
        _repo.Setup(r => r.TargetStateAsync(enrollment.TargetType, enrollment.TargetId, Org, It.IsAny<CancellationToken>()))
            .ReturnsAsync(target);
        _activities.Setup(a => a.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CreateActivityRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<ActivityDto>(new Error("Test", "not used")));
    }

    private static readonly SequenceTargetState Reachable =
        new(true, "Jean-Michel Dupont", "jean-michel@example.ch", "Club Nautique");

    // ── what a step queues ───────────────────────────────────────────────────

    [Fact]
    public async Task AStepQueuesItsEmailForTheAddressOnTheRecordToday()
    {
        var sequence = Sequence(EmailStep(0, "Hello {{firstName}}", "Hi {{firstName}},\nWe would love to show you {{company}}."));
        var enrollment = Due(sequence);
        Ready(sequence, enrollment, Reachable);

        await Service().RunDueAsync();

        var email = _queued.Single();
        // Sent to the address on the record today, not the one copied at enrollment.
        email.To.Should().Equal("jean-michel@example.ch");
        // The words as written: the outbox fills the placeholders, in the subject too.
        email.Subject.Should().Be("Hello {{firstName}}");
        email.Body.Should().Be("Hi {{firstName}},\nWe would love to show you {{company}}.");
        email.RecipientName.Should().Be("Jean-Michel Dupont");
        email.Source.Should().Be(OutboundSources.Sequence);
        email.SourceId.Should().Be(enrollment.Id);
        email.LeadId.Should().Be(enrollment.TargetId);
        email.TrackOpens.Should().BeTrue("the open image is for leads");
        email.CountsAsResponse.Should().BeFalse("a sequence's email is not somebody answering the lead");
        email.PersonWaiting.Should().BeFalse();
        email.CreatedByUserId.Should().Be(Owner);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AReplyReachesWhoeverWorksTheLead_OrElseWhoeverEnrolledThem(bool routedToRep)
    {
        // It went out as "Cadence" with no Reply-To, so an answer landed in the shared
        // sender inbox. With nobody working the lead, the enroller gets the reply.
        var sequence = Sequence(EmailStep(0, "Hello", "Hi"));
        var enrollment = Due(sequence);
        Ready(sequence, enrollment, Reachable with { OwnerUserId = routedToRep ? Rep : null });

        await Service().RunDueAsync();

        _queued.Single().ReplyToUserId.Should().Be(routedToRep ? Rep : Owner);
    }

    [Fact]
    public async Task AnEmptySubjectTakesTheSequencesName()
    {
        var sequence = Sequence(EmailStep(0, "  ", "Hi"));
        var enrollment = Due(sequence);
        Ready(sequence, enrollment, Reachable);

        await Service().RunDueAsync();

        _queued.Single().Subject.Should().Be("Welcome");
    }

    [Fact]
    public void TextIsEscapedInTheHtmlPart()
    {
        PlainTextEmail.ToHtml("<script>alert(1)</script> & co", null)
            .Should().Contain("&lt;script&gt;alert(1)&lt;/script&gt; &amp; co")
            .And.NotContain("<script>");
    }

    // ── a step and its email ─────────────────────────────────────────────────

    [Fact]
    public async Task AQueuedEmailAdvancesTheStep_AndTheRunnerDoesNotLogItAsWell()
    {
        // The outbox writes the email on the timeline once it has gone. A second entry
        // from the runner would show it twice, and once before it had gone.
        var sequence = Sequence(EmailStep(0, "Hi", "Body"), EmailStep(1, "Again", "Body"));
        var enrollment = Due(sequence);
        Ready(sequence, enrollment, Reachable);

        await Service().RunDueAsync();

        enrollment.CurrentStep.Should().Be(1);
        enrollment.NextActionAtUtc.Should().BeCloseTo(DateTime.UtcNow.AddDays(2), TimeSpan.FromMinutes(1));
        _activities.Verify(a => a.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CreateActivityRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TheNextStepWaitsWhileTheLastEmailIsStillInTheOutbox()
    {
        var sequence = Sequence(EmailStep(0, "Hi", "Body"), EmailStep(1, "Again", "Body"));
        var enrollment = Due(sequence);
        enrollment.CurrentStep = 1;
        Ready(sequence, enrollment, Reachable);
        _outbox.Setup(o => o.HasQueuedAsync(enrollment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Service().RunDueAsync();

        _queued.Should().BeEmpty();
        enrollment.CurrentStep.Should().Be(1, "the step has not happened");
        enrollment.Status.Should().Be("active");
        enrollment.NextActionAtUtc.Should().BeCloseTo(DateTime.UtcNow.Add(SequenceService.HoldFor), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task AnEmailTheOutboxRefusesStopsTheEnrollment()
    {
        var sequence = Sequence(EmailStep(0, "Hi", "Body"));
        var enrollment = Due(sequence);
        Ready(sequence, enrollment, Reachable);
        _outbox.Setup(o => o.QueueEmailAsync(It.IsAny<OutboundEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<OutboundMessage>(new Error("Email.Suppressed", "jean-michel@example.ch bounced on 3 Nov 2026, so email to it is suppressed and nothing was sent.")));

        await Service().RunDueAsync();

        enrollment.Status.Should().Be("stopped");
        enrollment.NextActionAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task ARunnerThatLosesTheClaimSendsNothing()
    {
        // Two app instances read the same due rows; only the one that claims sends.
        var sequence = Sequence(EmailStep(0, "Hi", "Body"));
        var enrollment = Due(sequence);
        Ready(sequence, enrollment, Reachable, claimed: false);

        await Service().RunDueAsync();

        _queued.Should().BeEmpty();
        _repo.Verify(r => r.SaveEnrollmentAsync(It.IsAny<OutreachEnrollment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── when to stop ─────────────────────────────────────────────────────────

    public static TheoryData<SequenceTargetState, string> Stops => new()
    {
        { SequenceTargetState.Missing, "no longer in this workspace" },
        { Reachable with { DoNotContact = true }, "do not contact" },
        { Reachable with { SuppressedAs = EmailSuppressionReasons.Bounced }, "bounced" },
        { Reachable with { SuppressedAs = EmailSuppressionReasons.SpamReport }, "spam" },
        { Reachable with { SuppressedAs = EmailSuppressionReasons.Unsubscribed }, "unsubscribed" },
        { Reachable with { IsArchived = true }, "archived" },
        { Reachable with { IsConverted = true }, "converted" },
        { Reachable with { Status = "Lost / Not Interested" }, "Lost / Not Interested" },
        { Reachable with { Status = "Signed" }, "Signed" },
        { Reachable with { Status = "Unqualified" }, "Unqualified" },
    };

    [Theory]
    [MemberData(nameof(Stops))]
    public async Task OutreachStopsForPeopleItMustNotReach(SequenceTargetState target, string because)
    {
        // The runner never looked: a do-not-contact contact, a deleted lead (after
        // an erasure request, say), a converted or lost lead all kept receiving
        // the remaining emails. A bounced address or a spam complaint now stops it too.
        SequenceService.StopReason(target).Should().Contain(because);

        var sequence = Sequence(EmailStep(0, "Hi", "Body"));
        var enrollment = Due(sequence);
        Ready(sequence, enrollment, target);
        await Service().RunDueAsync();

        enrollment.Status.Should().Be("stopped");
        enrollment.NextActionAtUtc.Should().BeNull();
        _queued.Should().BeEmpty();
    }

    [Fact]
    public void ALiveLeadIsNotStopped()
    {
        SequenceService.StopReason(Reachable with { Status = "Contacted" }).Should().BeNull();
    }

    [Fact]
    public async Task AnEnrollmentInADeletedSequenceStopsInsteadOfPollingForever()
    {
        var sequence = Sequence(EmailStep(0, "Hi", "Body"));
        var enrollment = Due(sequence);
        Ready(sequence, enrollment, Reachable);
        _repo.Setup(r => r.GetAsync(sequence.Id, Org, It.IsAny<CancellationToken>())).ReturnsAsync((OutreachSequence?)null);

        await Service().RunDueAsync();

        enrollment.Status.Should().Be("stopped");
        enrollment.NextActionAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task DeletingASequenceStopsItsEnrollments()
    {
        var sequence = Sequence(EmailStep(0, "Hi", "Body"));
        _repo.Setup(r => r.GetAsync(sequence.Id, Org, It.IsAny<CancellationToken>())).ReturnsAsync(sequence);

        (await Service().DeleteAsync(sequence.Id, Org)).Should().BeTrue();

        _repo.Verify(r => r.StopEnrollmentsAsync(sequence.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── enrolling ────────────────────────────────────────────────────────────

    [Fact]
    public async Task EnrollingTwiceDoesNotSendEverythingTwice()
    {
        var sequence = Sequence(EmailStep(0, "Hi", "Body"));
        var target = Guid.NewGuid();
        var open = Due(sequence);
        _repo.Setup(r => r.GetAsync(sequence.Id, Org, It.IsAny<CancellationToken>())).ReturnsAsync(sequence);
        _repo.Setup(r => r.TargetStateAsync("lead", target, Org, It.IsAny<CancellationToken>())).ReturnsAsync(Reachable);
        _repo.Setup(r => r.FindOpenEnrollmentAsync(sequence.Id, target, It.IsAny<CancellationToken>())).ReturnsAsync(open);

        var result = await Service().EnrollAsync(Owner, Org, new EnrollRequest(sequence.Id, "lead", target, "Jean", null));

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(open.Id, "the enrollment already running is returned");
        _repo.Verify(r => r.SaveEnrollmentAsync(It.IsAny<OutreachEnrollment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnrollingUsesTheRecordsAddressNotTheRequests()
    {
        // The request carried TargetEmail, and the runner used it — so anyone could
        // point a sequence at any address under the workspace's name.
        var sequence = Sequence(EmailStep(0, "Hi", "Body"));
        var target = Guid.NewGuid();
        _repo.Setup(r => r.GetAsync(sequence.Id, Org, It.IsAny<CancellationToken>())).ReturnsAsync(sequence);
        _repo.Setup(r => r.TargetStateAsync("lead", target, Org, It.IsAny<CancellationToken>())).ReturnsAsync(Reachable);
        OutreachEnrollment? saved = null;
        _repo.Setup(r => r.SaveEnrollmentAsync(It.IsAny<OutreachEnrollment>(), It.IsAny<CancellationToken>()))
            .Callback<OutreachEnrollment, CancellationToken>((e, _) => saved = e);

        await Service().EnrollAsync(Owner, Org, new EnrollRequest(sequence.Id, "lead", target, "Jean", "someone-else@evil.example"));

        saved!.TargetEmail.Should().Be("jean-michel@example.ch");
    }

    [Fact]
    public async Task ADoNotContactPersonCannotBeEnrolled()
    {
        var sequence = Sequence(EmailStep(0, "Hi", "Body"));
        var target = Guid.NewGuid();
        _repo.Setup(r => r.GetAsync(sequence.Id, Org, It.IsAny<CancellationToken>())).ReturnsAsync(sequence);
        _repo.Setup(r => r.TargetStateAsync("contact", target, Org, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Reachable with { DoNotContact = true });

        var result = await Service().EnrollAsync(Owner, Org, new EnrollRequest(sequence.Id, "contact", target, "Jean", null));

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Contain("do not contact");
    }

    [Fact]
    public async Task EnrollmentStatusAcceptsOnlyWhatAPersonMaySet()
    {
        (await Service().SetStatusAsync(Guid.NewGuid(), Org, "completed")).IsFailure.Should().BeTrue();

        var finished = new OutreachEnrollment { Id = Guid.NewGuid(), OrganizationId = Org, Status = "stopped" };
        _repo.Setup(r => r.GetEnrollmentAsync(finished.Id, Org, It.IsAny<CancellationToken>())).ReturnsAsync(finished);
        (await Service().SetStatusAsync(finished.Id, Org, "active")).IsFailure.Should().BeTrue("a stopped enrollment stays stopped");
    }
}
