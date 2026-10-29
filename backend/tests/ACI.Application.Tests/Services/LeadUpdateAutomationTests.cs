using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace ACI.Application.Tests.Services;

/// <summary>
/// What saving a lead does to its score and its response deadline.
/// </summary>
public class LeadUpdateAutomationTests
{
    private static readonly Guid User = Guid.NewGuid();
    private static readonly Guid Org = Guid.NewGuid();

    private readonly Mock<ILeadRepository> _leads = new();
    private readonly Mock<ILeadAutomation> _automation = new();
    private readonly LeadService _sut;
    private Lead _lead = null!;

    public LeadUpdateAutomationTests()
    {
        _automation.Setup(a => a.RescoreAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _automation.Setup(a => a.RecordStatusAsync(It.IsAny<Lead>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _leads.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), User, Org, It.IsAny<CancellationToken>())).ReturnsAsync(() => _lead);
        _leads.Setup(r => r.UpdateAsync(It.IsAny<Lead>(), User, Org, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead l, Guid _, Guid? _, CancellationToken _) => l);
        _sut = new LeadService(_leads.Object, new Mock<ICompanyRepository>().Object, new Mock<IContactRepository>().Object,
            new Mock<IDealRepository>().Object, _automation.Object, WorkspaceRecordsMock.InWorkspace().Object, NullLogger<LeadService>.Instance);
    }

    private Lead Given(string status = "New", int score = 35) => _lead = new Lead
    {
        Id = Guid.NewGuid(), UserId = User, OrganizationId = Org, Name = "Jean", Email = "jean@example.ch",
        Status = status, LeadScore = score, CreatedAtUtc = DateTime.UtcNow.AddHours(-6),
        FirstResponseDueAtUtc = DateTime.UtcNow.AddHours(-2),
    };

    [Fact]
    public async Task SavingTheFormWithTheSameScoreDoesNotFreezeIt()
    {
        // Both edit dialogs pre-fill the automatic score and send it back. Fixing a
        // phone number used to set ScoreIsManual — no rescore ever again.
        Given(score: 35);
        await _sut.UpdateAsync(_lead.Id, User, Org, new UpdateLeadRequest { Phone = "+41790000000", LeadScore = 35 });

        _lead.ScoreIsManual.Should().BeFalse();
        _automation.Verify(a => a.RescoreAsync(_lead, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangingTheScoreDoesMakeItManual()
    {
        Given(score: 35);
        await _sut.UpdateAsync(_lead.Id, User, Org, new UpdateLeadRequest { LeadScore = 80 });

        _lead.ScoreIsManual.Should().BeTrue();
        _lead.LeadScore.Should().Be(80);
    }

    [Fact]
    public async Task MovingOffNewThroughThePipelineIsAFirstResponse()
    {
        // The pipeline moves the status when outreach is logged. Only activities used
        // to count, so a lead already called still raised "First response is late".
        Given(status: "New");
        await _sut.UpdateAsync(_lead.Id, User, Org, new UpdateLeadRequest { Status = "Attempted Contact" });

        _lead.FirstResponseAtUtc.Should().NotBeNull();
        _lead.SlaBreached.Should().BeFalse();
    }

    [Fact]
    public async Task RecordingAContactDateIsAFirstResponse()
    {
        Given();
        await _sut.UpdateAsync(_lead.Id, User, Org, new UpdateLeadRequest { LastContactedAt = DateTime.UtcNow });

        _lead.FirstResponseAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task EditingAnUntouchedLeadIsNot()
    {
        Given(status: "New");
        await _sut.UpdateAsync(_lead.Id, User, Org, new UpdateLeadRequest { Phone = "+41790000000" });

        _lead.FirstResponseAtUtc.Should().BeNull("nothing about the lead says anyone has worked it");
    }
}
