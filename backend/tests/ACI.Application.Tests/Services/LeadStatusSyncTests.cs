using ACI.Application.Common;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;

namespace ACI.Application.Tests.Services;

/// <summary>
/// The server applying the status rule to a lead: with the organisation's own
/// statuses, and with a record of what it did and why.
/// </summary>
public class LeadStatusSyncTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();

    private static readonly string[] Agreed =
    {
        "New", "Attempted Contact", "Contacted", "Connected",
        "Contract Pending", "Awaiting Signature", "Signed", "Lost / Not Interested", "Do Not Contact",
    };

    private readonly StatusSyncHarness _harness = new StatusSyncHarness().WithStatuses(Org, Agreed);
    private readonly Mock<ILeadRepository> _leads = new();
    private readonly Mock<ILeadAutomation> _automation = new();
    private readonly LeadStatusSync _sut;

    public LeadStatusSyncTests()
    {
        _leads.Setup(r => r.UpdateAsync(It.IsAny<Lead>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead l, Guid _, Guid? _, CancellationToken _) => l);
        _sut = _harness.Create(_leads.Object, _automation.Object);
    }

    private static Lead ALead(string status = "New", string? pipeline = null, Guid? org = null) => new()
    {
        Id = Guid.NewGuid(), UserId = User, OrganizationId = org ?? Org, Name = "Jean Dupont",
        Email = "jean@example.ch", Status = status, PipelineState = pipeline, CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
    };

    [Fact]
    public async Task SettlingWritesTheStatusThePipelineImpliesWithItsId()
    {
        var lead = ALead(pipeline: """{"contractStatus":"yes","contractSigned":"pending"}""");

        var plan = await _sut.SettleAsync(lead);

        plan.Applies.Should().BeTrue();
        lead.Status.Should().Be("Awaiting Signature");
        lead.LeadStatusId.Should().Be(_harness.IdOf(Org, "Awaiting Signature"),
            "the id goes with the name, or the lead points at a status it no longer has");
    }

    [Fact]
    public async Task APersonalWorkspaceHasNoListToChooseFromSoNothingMoves()
    {
        var lead = ALead(pipeline: """{"outreachStatus":"contacted"}""");
        lead.OrganizationId = null;

        var plan = await _sut.SettleAsync(lead);

        plan.Reason.Should().Be(LeadStatusDerivation.SkipReason.StatusesUnavailable);
        lead.Status.Should().Be("New");
    }

    [Fact]
    public async Task AParkedStatusIsOnlyOfferedTheChange()
    {
        var lead = ALead("Do Not Contact", """{"contractSigned":"yes"}""");

        var plan = await _sut.SettleAsync(lead);

        plan.Kind.Should().Be(LeadStatusDerivation.PlanKind.Suggest);
        lead.Status.Should().Be("Do Not Contact");
        lead.LeadStatusId.Should().BeNull();
    }

    [Fact]
    public async Task AConvertedLeadIsLeftAlone()
    {
        var lead = ALead(pipeline: """{"contractSigned":"yes"}""");
        lead.IsConverted = true;

        (await _sut.SettleAsync(lead)).Reason.Should().Be(LeadStatusDerivation.SkipReason.Converted);
        lead.Status.Should().Be("New");
    }

    [Fact]
    public async Task AnAutomaticMoveIsRecordedExplainedAndAnnounced()
    {
        var lead = ALead(pipeline: """{"meetingAttended":true,"stillInterested":true}""");
        var plan = await _sut.SettleAsync(lead);

        await _sut.AnnounceAsync(lead, "New", User, null, plan);

        var row = _harness.Events.Should().ContainSingle().Subject;
        row.FromStatus.Should().Be("New");
        row.ToStatus.Should().Be("Contract Pending");
        row.ChangedByUserId.Should().BeNull("nobody in the workspace made this change");

        var entry = _harness.Logged.Should().ContainSingle().Subject;
        entry.Type.Should().Be("system");
        entry.LeadId.Should().Be(lead.Id);
        entry.Subject.Should().Be("Status auto-updated to Contract Pending");
        entry.Body.Should().Contain("Still interested after the meeting").And.Contain("[auto-status rule=phase2_qualified");

        _harness.Dispatched.Should().ContainSingle(d => d.Trigger == "lead.status_changed" && d.Event.LeadId == lead.Id);
    }

    [Fact]
    public async Task AMoveThatDidNotComeFromThePipelineIsNotExplainedAsOne()
    {
        var lead = ALead("Contacted");

        await _sut.AnnounceAsync(lead, "New", User, User, plan: null);

        _harness.Events.Should().ContainSingle(e => e.ChangedByUserId == User);
        _harness.Logged.Should().BeEmpty();
        _harness.Dispatched.Should().ContainSingle();
    }

    [Fact]
    public async Task SavingAMoveOffNewStopsTheFirstResponseClockAndRescores()
    {
        var lead = ALead(pipeline: """{"contractStatus":"yes","contractSigned":"pending"}""");
        string? statusWhenScored = null;
        _automation.Setup(a => a.RescoreAsync(lead, It.IsAny<CancellationToken>()))
            .Callback((Lead l, CancellationToken _) => statusWhenScored = l.Status)
            .Returns(Task.CompletedTask);

        await _sut.SaveAsync(lead, User, Org, User);

        lead.FirstResponseAtUtc.Should().NotBeNull();
        statusWhenScored.Should().Be("Awaiting Signature", "'reached stage' score rules read the new status");
        _leads.Verify(r => r.UpdateAsync(lead, User, Org, It.IsAny<CancellationToken>()), Times.Once);
        _harness.Events.Should().ContainSingle(e => e.ToStatus == "Awaiting Signature");
    }

    [Fact]
    public async Task SavingWithoutAMoveStillSavesThePipelineButAnnouncesNothing()
    {
        var lead = ALead("Do Not Contact", """{"contractSigned":"yes"}""");

        await _sut.SaveAsync(lead, User, Org, User);

        _leads.Verify(r => r.UpdateAsync(lead, User, Org, It.IsAny<CancellationToken>()), Times.Once);
        _automation.Verify(a => a.RescoreAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()), Times.Never);
        _harness.Events.Should().BeEmpty();
        _harness.Dispatched.Should().BeEmpty();
        lead.FirstResponseAtUtc.Should().BeNull();
    }
}
