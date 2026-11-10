using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;

namespace ACI.Application.Tests.Services;

/// <summary>
/// A bulk lead edit goes on past a lead someone saved in the same moment.
/// </summary>
/// <remarks>
/// Every lead save is checked against the version it was read at (see IVersioned), so a
/// lead saved by someone else between the bulk edit reading it and writing it refuses the
/// write. It is counted as not updated, and the leads after it are still updated, rather
/// than the whole request failing part of the way through.
/// </remarks>
public class LeadOpsBulkRaceTests
{
    [Fact]
    public async Task ALeadSavedMeanwhileIsCountedAsNotUpdatedAndTheRestAreUpdated()
    {
        var userId = Guid.NewGuid();
        var raced = new Lead { Id = Guid.NewGuid(), UserId = userId, Name = "Anna", Email = "anna@example.ch", Status = "New" };
        var calm = new Lead { Id = Guid.NewGuid(), UserId = userId, Name = "Ben", Email = "ben@example.ch", Status = "New" };
        var repository = new Mock<ILeadRepository>();
        repository.Setup(r => r.GetByIdAsync(raced.Id, userId, null, It.IsAny<CancellationToken>())).ReturnsAsync(raced);
        repository.Setup(r => r.GetByIdAsync(calm.Id, userId, null, It.IsAny<CancellationToken>())).ReturnsAsync(calm);
        repository.Setup(r => r.UpdateAsync(It.Is<Lead>(l => l.Id == raced.Id), userId, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyConflictException());
        repository.Setup(r => r.UpdateAsync(It.Is<Lead>(l => l.Id == calm.Id), userId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead l, Guid _, Guid? __, CancellationToken ___) => l);
        var service = new LeadOpsService(
            Mock.Of<ILeadOpsRepository>(), Mock.Of<ILeadService>(), repository.Object, Mock.Of<INotificationService>(),
            Mock.Of<ILeadAutomation>(), Mock.Of<IActivityService>());

        var result = await service.BulkUpdateAsync(userId, null, new BulkLeadUpdateRequest(new[] { raced.Id, calm.Id }, Source: "Referral"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(new BulkLeadUpdateResult(Updated: 1, Missing: 1));
        repository.Verify(r => r.UpdateAsync(It.Is<Lead>(l => l.Id == calm.Id && l.Source == "Referral"), userId, null, It.IsAny<CancellationToken>()), Times.Once);
    }
}
