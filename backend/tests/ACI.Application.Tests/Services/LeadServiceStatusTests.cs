using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace ACI.Application.Tests.Services;

/// <summary>
/// Saving a lead's pipeline sets its status on the server, whoever sends it.
/// </summary>
/// <remarks>
/// The browser used to work the status out and send it with the pipeline. Any other
/// caller (an integration, a script, a stale tab) sent the pipeline alone, and the
/// status stayed where it was.
/// </remarks>
public class LeadServiceStatusTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Org = Guid.NewGuid();

    private readonly StatusSyncHarness _harness = new StatusSyncHarness().WithStatuses(Org,
        "New", "Attempted Contact", "Contacted", "Connected",
        "Contract Pending", "Awaiting Signature", "Signed", "Lost / Not Interested", "Do Not Contact");
    private readonly Mock<ILeadRepository> _leads = new();
    private readonly Mock<ILeadAutomation> _automation = new();
    private readonly LeadService _sut;
    private Lead _lead = null!;
    private readonly List<Lead> _added = new();

    public LeadServiceStatusTests()
    {
        _automation.Setup(a => a.FindOpenDuplicateAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);
        _leads.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), User, Org, It.IsAny<CancellationToken>())).ReturnsAsync(() => _lead);
        _leads.Setup(r => r.UpdateAsync(It.IsAny<Lead>(), User, Org, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead l, Guid _, Guid? _, CancellationToken _) => l);
        _leads.Setup(r => r.AddAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead l, CancellationToken _) => { _added.Add(l); return l; });
        _sut = new LeadService(_leads.Object, new Mock<ICompanyRepository>().Object, new Mock<IContactRepository>().Object,
            new Mock<IDealRepository>().Object, _automation.Object, WorkspaceRecordsMock.InWorkspace().Object,
            _harness.Create(_leads.Object, _automation.Object), NullLogger<LeadService>.Instance,
            _harness.Automations.Object);
    }

    private Lead Given(string status = "New", string? pipeline = null) => _lead = new Lead
    {
        Id = Guid.NewGuid(), UserId = User, OrganizationId = Org, Name = "Jean Dupont", Email = "jean@example.ch",
        Status = status, PipelineState = pipeline, CreatedAtUtc = DateTime.UtcNow.AddHours(-6),
        FirstResponseDueAtUtc = DateTime.UtcNow.AddHours(-2),
    };

    [Fact]
    public async Task APipelineSentAloneMovesTheStatus()
    {
        Given();

        var result = await _sut.UpdateAsync(_lead.Id, User, Org, new UpdateLeadRequest
        {
            PipelineState = """{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled","meetingAttended":true}""",
        });

        result.Value.Status.Should().Be("Connected");
        result.Value.LeadStatusId.Should().Be(_harness.IdOf(Org, "Connected"));
        _harness.Events.Should().ContainSingle(e => e.FromStatus == "New" && e.ToStatus == "Connected" && e.ChangedByUserId == User);
        _harness.Logged.Should().ContainSingle(a => a.Subject == "Status auto-updated to Connected");
        _harness.Dispatched.Should().ContainSingle(d => d.Trigger == "lead.status_changed");
    }

    [Fact]
    public async Task WhenAStatusSentWithThePipelineDisagreesThePipelineWins()
    {
        // A stale tab, or a caller that does not know the rule.
        Given();

        var result = await _sut.UpdateAsync(_lead.Id, User, Org, new UpdateLeadRequest
        {
            Status = "Contacted",
            PipelineState = """{"contractSigned":"yes"}""",
        });

        result.Value.Status.Should().Be("Signed");
    }

    [Fact]
    public async Task AParkedStatusSentWithThePipelineStays()
    {
        Given();

        var result = await _sut.UpdateAsync(_lead.Id, User, Org, new UpdateLeadRequest
        {
            Status = "Do Not Contact",
            PipelineState = """{"contractSigned":"yes"}""",
        });

        result.Value.Status.Should().Be("Do Not Contact", "a deliberate parking status outranks any inference");
        _harness.Logged.Should().BeEmpty("the pipeline did not move it");
    }

    [Fact]
    public async Task ASaveWithoutAPipelineLeavesTheStatusAsSent()
    {
        Given(pipeline: """{"contractSigned":"yes"}""");

        var result = await _sut.UpdateAsync(_lead.Id, User, Org, new UpdateLeadRequest { Status = "Contacted" });

        result.Value.Status.Should().Be("Contacted");
    }

    [Fact]
    public async Task EditingAnotherFieldDoesNotRederiveTheStatus()
    {
        // A lead whose status has drifted is for a person to reconcile, not for a phone
        // number edit to rewrite.
        Given("Contacted", """{"contractSigned":"yes"}""");

        var result = await _sut.UpdateAsync(_lead.Id, User, Org, new UpdateLeadRequest { Phone = "+41790000000" });

        result.Value.Status.Should().Be("Contacted");
        _harness.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task ADerivedMoveOffNewIsAFirstResponseAndIsScoredOnTheNewStatus()
    {
        Given();
        string? statusWhenScored = null;
        _automation.Setup(a => a.RescoreAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .Callback((Lead l, CancellationToken _) => statusWhenScored = l.Status)
            .Returns(Task.CompletedTask);

        await _sut.UpdateAsync(_lead.Id, User, Org, new UpdateLeadRequest { PipelineState = """{"outreachStatus":"attempted_no_answer"}""" });

        _lead.FirstResponseAtUtc.Should().NotBeNull();
        statusWhenScored.Should().Be("Attempted Contact");
    }

    [Fact]
    public async Task ALeadCreatedWithRecordedWorkStartsAtTheStatusItImplies()
    {
        var result = await _sut.CreateAsync(User, Org, new CreateLeadRequest
        {
            Name = "Jean Dupont", Email = "jean@example.ch", Status = "New",
            PipelineState = """{"outreachStatus":"contacted","meetingAttended":true,"stillInterested":true}""",
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Contract Pending");
        _added.Single().PipelineState.Should().Contain("stillInterested");
        _added.Single().FirstResponseAtUtc.Should().NotBeNull("it arrives already worked");
        _automation.Verify(a => a.RecordStatusAsync(It.IsAny<Lead>(), null, "Contract Pending", User, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ALeadCreatedWithoutAPipelineKeepsTheStatusItWasGiven()
    {
        var result = await _sut.CreateAsync(User, Org, new CreateLeadRequest { Name = "Jean Dupont", Email = "jean@example.ch", Status = "New" });

        result.Value.Status.Should().Be("New");
    }

    [Fact]
    public async Task AMergeThatBringsAPipelineBringsItsStatus()
    {
        // The kept lead had no pipeline; the one folded into it had reached the signature.
        var keep = Given();
        var before = new Lead { Id = keep.Id, UserId = User, OrganizationId = Org, Name = keep.Name, Email = keep.Email, Status = "New" };
        var after = new Lead
        {
            Id = keep.Id, UserId = User, OrganizationId = Org, Name = keep.Name, Email = keep.Email, Status = "New",
            PipelineState = """{"contractSigned":"yes"}""", CreatedAtUtc = DateTime.UtcNow.AddDays(-3),
        };
        var reads = new Queue<Lead>(new[] { before, after });
        _leads.Setup(r => r.GetByIdAsync(keep.Id, User, Org, It.IsAny<CancellationToken>())).ReturnsAsync(() => reads.Dequeue());
        _leads.Setup(r => r.MergeIntoAsync(keep.Id, It.IsAny<Guid>(), User, Org, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        (await _sut.MergeAsync(keep.Id, Guid.NewGuid(), User, Org)).IsSuccess.Should().BeTrue();

        after.Status.Should().Be("Signed");
        _leads.Verify(r => r.UpdateAsync(after, User, Org, It.IsAny<CancellationToken>()), Times.Once);
        _harness.Events.Should().ContainSingle(e => e.ToStatus == "Signed");
    }

    [Fact]
    public async Task AMergeThatLeavesThePipelineAsItWasLeavesTheStatus()
    {
        var keep = Given("Contacted", """{"contractSigned":"yes"}""");
        _leads.Setup(r => r.MergeIntoAsync(keep.Id, It.IsAny<Guid>(), User, Org, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await _sut.MergeAsync(keep.Id, Guid.NewGuid(), User, Org);

        keep.Status.Should().Be("Contacted");
        _leads.Verify(r => r.UpdateAsync(It.IsAny<Lead>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
