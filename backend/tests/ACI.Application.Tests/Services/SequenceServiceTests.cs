using ACI.Application.Common;
using ACI.Application.Configuration;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ACI.Application.Tests.Services;

/// <summary>
/// The sequence runner writes to real people unattended. Each test pins a way it
/// used to get that wrong.
/// </summary>
public class SequenceServiceTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Owner = Guid.NewGuid();

    private readonly Mock<ISequenceRepository> _repo = new();
    private readonly Mock<IEmailSender> _email = new();
    private readonly Mock<IActivityService> _activities = new();
    private readonly Mock<ITaskService> _tasks = new();
    private readonly Mock<IOutreachMail> _outreach = new();
    private readonly EmailSettings _settings = new() { ApiBaseUrl = "http://localhost:5160" };

    public SequenceServiceTests()
    {
        _outreach.Setup(o => o.SenderAsync(It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid? _, Guid? user, CancellationToken _) => new OutreachSender("Club Nautique", user == Rep ? "rep@club.ch" : "owner@club.ch"));
    }

    private static readonly Guid Rep = Guid.NewGuid();

    private SequenceService Service() => new(
        _repo.Object, _email.Object, _outreach.Object, _activities.Object, _tasks.Object,
        Options.Create(_settings), NullLogger<SequenceService>.Instance);

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

    // ── what reaches the inbox ───────────────────────────────────────────────

    [Fact]
    public async Task TheEmailHasNoRawMarkupInIt()
    {
        // Every sequence email to a lead used to end with
        //   <img src="http://localhost:5160/api/public/track/…" width="1" …/>
        // because the pixel was appended to a body sent as plain text only.
        var sequence = Sequence(EmailStep(0, "Hello {{firstName}}", "Hi {{firstName}},\nWe would love to show you {{company}}."));
        var enrollment = Due(sequence);
        Ready(sequence, enrollment, Reachable);
        string? text = null, html = null, subject = null, to = null;
        _email.Setup(e => e.SendOutreachAsync(It.IsAny<OutreachEmail>(), It.IsAny<CancellationToken>()))
            .Callback<OutreachEmail, CancellationToken>((m, _) => { to = m.To; subject = m.Subject; text = m.TextBody; html = m.HtmlBody; })
            .ReturnsAsync(true);

        await Service().RunDueAsync();

        text.Should().NotContain("<img").And.NotContain("localhost");
        // Placeholders filled in the subject and for {{company}} too — both went out literally.
        subject.Should().Be("Hello Jean-Michel");
        text.Should().Be("Hi Jean-Michel,\nWe would love to show you Club Nautique.");
        // The HTML part is the same words, escaped, with line breaks kept.
        html.Should().Contain("Hi Jean-Michel,<br>");
        // Sent to the address on the record today, not the one copied at enrollment.
        to.Should().Be("jean-michel@example.ch");
    }

    [Theory]
    [InlineData(true, "rep@club.ch")]
    [InlineData(false, "owner@club.ch")]
    public async Task TheEmailIsTheWorkspacesAndAReplyReachesWhoeverWorksTheLead(bool routedToRep, string replyTo)
    {
        // It went out as "Cadence" with no Reply-To, so an answer landed in the shared
        // sender inbox. With nobody working the lead, the enroller gets the reply.
        var sequence = Sequence(EmailStep(0, "Hello", "Hi"));
        var enrollment = Due(sequence);
        Ready(sequence, enrollment, Reachable with { OwnerUserId = routedToRep ? Rep : null });
        OutreachEmail? sent = null;
        _email.Setup(e => e.SendOutreachAsync(It.IsAny<OutreachEmail>(), It.IsAny<CancellationToken>()))
            .Callback<OutreachEmail, CancellationToken>((m, _) => sent = m)
            .ReturnsAsync(true);

        await Service().RunDueAsync();

        sent!.FromName.Should().Be("Club Nautique");
        sent.ReplyTo.Should().Be(replyTo);
        _outreach.Verify(o => o.SenderAsync(Org, routedToRep ? Rep : Owner, It.IsAny<CancellationToken>()));
    }

    [Fact]
    public void TheTrackingPixelIsOnlyAddedWhereItCanWork()
    {
        _settings.ApiBaseUrl = "http://localhost:5160";
        Service().TrackingUrl(Guid.NewGuid()).Should().BeNull("a recipient can never fetch localhost");
        _settings.ApiBaseUrl = "http://api.example.ch";
        Service().TrackingUrl(Guid.NewGuid()).Should().BeNull("plain http is blocked by most mail clients");
        _settings.ApiBaseUrl = "https://api.example.ch/";
        var lead = Guid.NewGuid();
        Service().TrackingUrl(lead).Should().Be($"https://api.example.ch/api/public/track/{lead}");

        SequenceService.HtmlFor("Hi", "https://api.example.ch/api/public/track/x").Should().Contain("<img src=\"https://api.example.ch/api/public/track/x\"");
        SequenceService.HtmlFor("Hi", null).Should().NotContain("<img");
    }

    [Fact]
    public void TextIsEscapedInTheHtmlPart()
    {
        SequenceService.HtmlFor("<script>alert(1)</script> & co", null)
            .Should().Contain("&lt;script&gt;alert(1)&lt;/script&gt; &amp; co")
            .And.NotContain("<script>");
    }

    // ── a step only advances once it has happened ────────────────────────────

    [Fact]
    public async Task AFailedSendIsRetriedLaterAndNotLoggedAsSent()
    {
        // The send result was ignored: the step advanced and an "email" activity
        // was logged even when nothing went out.
        var sequence = Sequence(EmailStep(0, "Hi", "Body"), EmailStep(1, "Again", "Body"));
        var enrollment = Due(sequence);
        Ready(sequence, enrollment, Reachable);
        _email.Setup(e => e.SendOutreachAsync(It.IsAny<OutreachEmail>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Service().RunDueAsync();

        enrollment.CurrentStep.Should().Be(0, "nothing was sent, so the step has not happened");
        enrollment.Status.Should().Be("active");
        enrollment.NextActionAtUtc.Should().BeCloseTo(DateTime.UtcNow.Add(SequenceService.RetryAfter), TimeSpan.FromMinutes(1));
        _activities.Verify(a => a.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CreateActivityRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ASentEmailAdvancesAndIsLoggedAsAutomated()
    {
        // Logged as automated so it does not count as a person's first response —
        // an automated day-0 email used to clear the response-time alert.
        var sequence = Sequence(EmailStep(0, "Hi", "Body"), EmailStep(1, "Again", "Body"));
        var enrollment = Due(sequence);
        Ready(sequence, enrollment, Reachable);
        _email.Setup(e => e.SendOutreachAsync(It.IsAny<OutreachEmail>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Service().RunDueAsync();

        enrollment.CurrentStep.Should().Be(1);
        _activities.Verify(a => a.CreateAsync(Owner, Org,
            It.Is<CreateActivityRequest>(r => r.Type == "email" && r.IsAutomated && r.LeadId == enrollment.TargetId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ARunnerThatLosesTheClaimSendsNothing()
    {
        // Two app instances read the same due rows; only the one that claims sends.
        var sequence = Sequence(EmailStep(0, "Hi", "Body"));
        var enrollment = Due(sequence);
        Ready(sequence, enrollment, Reachable, claimed: false);

        await Service().RunDueAsync();

        _email.Verify(e => e.SendOutreachAsync(It.IsAny<OutreachEmail>(), It.IsAny<CancellationToken>()), Times.Never);
        _repo.Verify(r => r.SaveEnrollmentAsync(It.IsAny<OutreachEnrollment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── when to stop ─────────────────────────────────────────────────────────

    public static TheoryData<SequenceTargetState, string> Stops => new()
    {
        { SequenceTargetState.Missing, "no longer in this workspace" },
        { Reachable with { DoNotContact = true }, "do not contact" },
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
        // the remaining emails.
        SequenceService.StopReason(target).Should().Contain(because);

        var sequence = Sequence(EmailStep(0, "Hi", "Body"));
        var enrollment = Due(sequence);
        Ready(sequence, enrollment, target);
        await Service().RunDueAsync();

        enrollment.Status.Should().Be("stopped");
        enrollment.NextActionAtUtc.Should().BeNull();
        _email.Verify(e => e.SendOutreachAsync(It.IsAny<OutreachEmail>(), It.IsAny<CancellationToken>()), Times.Never);
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
