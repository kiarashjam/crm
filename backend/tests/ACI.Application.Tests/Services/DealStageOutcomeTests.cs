using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Tests.Services;

/// <summary>
/// A deal's stage decides whether it is won, lost or open.
/// </summary>
/// <remarks>
/// The board moves deals by stage. Reports, the forecast and the won/lost automations
/// all read IsWon, which only the "close deal" dialog used to set, so a deal moved
/// into a won stage still counted as open everywhere else.
/// </remarks>
public class DealStageOutcomeTests
{
    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();
    private readonly Guid _pipeline = Guid.NewGuid();
    private readonly DealStage _open, _won, _lost;
    private readonly Mock<IDealRepository> _deals = new();
    private readonly Mock<IDealStageRepository> _stages = new();
    private readonly Mock<IAutomationEngine> _automations = new();
    private readonly DealService _sut;
    private readonly Deal _deal;

    public DealStageOutcomeTests()
    {
        _open = Stage("Proposal", isWon: false, isLost: false);
        _won = Stage("Signed", isWon: true, isLost: false);
        _lost = Stage("No budget", isWon: false, isLost: true);
        _deal = new Deal { Id = Guid.NewGuid(), UserId = User, OrganizationId = Org, Name = "Corporate membership", Value = "12000", DealStageId = _open.Id };

        _deals.Setup(r => r.GetByIdAsync(_deal.Id, User, Org, It.IsAny<CancellationToken>())).ReturnsAsync(_deal);
        _deals.Setup(r => r.UpdateAsync(It.IsAny<Deal>(), User, Org, It.IsAny<CancellationToken>())).ReturnsAsync((Deal d, Guid _, Guid? _, CancellationToken _) => d);

        _sut = new DealService(_deals.Object, Mock.Of<IActivityRepository>(), Mock.Of<ILogger<DealService>>(),
            _automations.Object, stages: _stages.Object);
    }

    private DealStage Stage(string name, bool isWon, bool isLost)
    {
        var stage = new DealStage { Id = Guid.NewGuid(), PipelineId = _pipeline, Name = name, IsWon = isWon, IsLost = isLost };
        _stages.Setup(r => r.GetByIdAsync(stage.Id, Org, It.IsAny<CancellationToken>())).ReturnsAsync(stage);
        return stage;
    }

    private Task<ACI.Application.Common.Result<DealDto>> MoveTo(DealStage stage) =>
        _sut.UpdateAsync(_deal.Id, User, Org, new UpdateDealRequest { DealStageId = stage.Id, Stage = stage.Name });

    [Fact]
    public async Task MovingIntoAWonStageWinsTheDealAndRunsItsAutomations()
    {
        (await MoveTo(_won)).IsSuccess.Should().BeTrue();

        _deal.IsWon.Should().BeTrue();
        _deal.ClosedAtUtc.Should().NotBeNull();
        _deal.PipelineId.Should().Be(_pipeline);
        _automations.Verify(a => a.DispatchAsync("deal.won", It.IsAny<AutomationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MovingIntoALostStageLosesIt()
    {
        await MoveTo(_lost);

        _deal.IsWon.Should().BeFalse();
        _automations.Verify(a => a.DispatchAsync("deal.lost", It.IsAny<AutomationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MovingBackToAnOpenStageReopensIt()
    {
        await MoveTo(_won);
        await MoveTo(_open);

        _deal.IsWon.Should().BeNull();
        _deal.ClosedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task AStageFromAnotherWorkspaceIsRefused()
    {
        var foreign = Guid.NewGuid();
        _stages.Setup(r => r.GetByIdAsync(foreign, Org, It.IsAny<CancellationToken>())).ReturnsAsync((DealStage?)null);

        var result = await _sut.UpdateAsync(_deal.Id, User, Org, new UpdateDealRequest { DealStageId = foreign });

        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("DealStage.NotFound");
        _deals.Verify(r => r.UpdateAsync(It.IsAny<Deal>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AnExplicitOutcomeFromTheCloseDialogWins()
    {
        await _sut.UpdateAsync(_deal.Id, User, Org, new UpdateDealRequest { DealStageId = _won.Id, IsWon = false, ClosedReason = "Lost to a competitor" });

        _deal.IsWon.Should().BeFalse();
    }
}
