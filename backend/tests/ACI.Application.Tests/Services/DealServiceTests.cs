using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Tests.Services;

/// <summary>
/// Unit tests for DealService.
/// </summary>
public class DealServiceTests
{
    private readonly Mock<IDealRepository> _dealRepositoryMock;
    private readonly Mock<IActivityRepository> _activityRepositoryMock;
    private readonly Mock<ILogger<DealService>> _loggerMock;
    private readonly DealService _sut;

    public DealServiceTests()
    {
        _dealRepositoryMock = new Mock<IDealRepository>();
        _activityRepositoryMock = new Mock<IActivityRepository>();
        _loggerMock = new Mock<ILogger<DealService>>();

        _sut = new DealService(
            _dealRepositoryMock.Object,
            _activityRepositoryMock.Object,
            _loggerMock.Object);
    }

    #region GetDealsAsync Tests

    [Fact]
    public async Task GetDealsAsync_ReturnsDeals_WhenDealsExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var deals = new List<Deal>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Deal 1", Value = "10000" },
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Deal 2", Value = "20000" }
        };

        _dealRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deals);

        _activityRepositoryMock
            .Setup(r => r.GetLastActivityByDealIdsAsync(userId, orgId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DateTime>());

        // Act
        var result = await _sut.GetDealsAsync(userId, orgId);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Deal 1");
        result[1].Name.Should().Be("Deal 2");
    }

    [Fact]
    public async Task GetDealsAsync_ReturnsEmptyList_WhenNoDealsExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        _dealRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Deal>());

        _activityRepositoryMock
            .Setup(r => r.GetLastActivityByDealIdsAsync(userId, orgId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DateTime>());

        // Act
        var result = await _sut.GetDealsAsync(userId, orgId);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ReturnsDeal_WhenDealExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var dealId = Guid.NewGuid();
        var deal = new Deal
        {
            Id = dealId,
            UserId = userId,
            Name = "Test Deal",
            Value = "50000"
        };

        _dealRepositoryMock
            .Setup(r => r.GetByIdAsync(dealId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deal);

        _activityRepositoryMock
            .Setup(r => r.GetLastActivityByDealIdsAsync(userId, orgId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DateTime>());

        // Act
        var result = await _sut.GetByIdAsync(dealId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(dealId);
        result.Value.Name.Should().Be("Test Deal");
        result.Value.Value.Should().Be("50000");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNotFoundError_WhenDealDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var dealId = Guid.NewGuid();

        _dealRepositoryMock
            .Setup(r => r.GetByIdAsync(dealId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Deal?)null);

        // Act
        var result = await _sut.GetByIdAsync(dealId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Deal.NotFound");
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ReturnsDeal_WhenValidRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var request = new CreateDealRequest
        {
            Name = "New Deal",
            Value = "100000",
            Currency = "USD",
            Stage = "Negotiation",
            PipelineId = null,
            DealStageId = null,
            CompanyId = null,
            ContactId = null,
            AssigneeId = null,
            ExpectedCloseDateUtc = null
        };

        _dealRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Deal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Deal d, CancellationToken _) => d);

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("New Deal");
        result.Value.Value.Should().Be("100000");
        result.Value.Currency.Should().Be("USD");
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_WhenNameIsEmpty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var request = new CreateDealRequest
        {
            Name = "",
            Value = "10000",
            Currency = null,
            Stage = null,
            PipelineId = null,
            DealStageId = null,
            CompanyId = null,
            ContactId = null,
            AssigneeId = null,
            ExpectedCloseDateUtc = null
        };

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Deal.NameRequired");
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_WhenValueIsEmpty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var request = new CreateDealRequest
        {
            Name = "Test Deal",
            Value = "",
            Currency = null,
            Stage = null,
            PipelineId = null,
            DealStageId = null,
            CompanyId = null,
            ContactId = null,
            AssigneeId = null,
            ExpectedCloseDateUtc = null
        };

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Deal.ValueRequired");
    }

    [Fact]
    public async Task CreateAsync_SetsPipelineId_WhenProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var pipelineId = Guid.NewGuid();
        var request = new CreateDealRequest
        {
            Name = "Pipeline Deal",
            Value = "50000",
            Currency = "EUR",
            Stage = null,
            PipelineId = pipelineId,
            DealStageId = null,
            CompanyId = null,
            ContactId = null,
            AssigneeId = null,
            ExpectedCloseDateUtc = null
        };

        _dealRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Deal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Deal d, CancellationToken _) => d);

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PipelineId.Should().Be(pipelineId);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ReturnsUpdatedDeal_WhenValidRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var dealId = Guid.NewGuid();
        var existingDeal = new Deal
        {
            Id = dealId,
            UserId = userId,
            Name = "Old Deal",
            Value = "10000"
        };
        var request = new UpdateDealRequest
        {
            Name = "Updated Deal",
            Value = "20000",
            Currency = "GBP",
            Stage = "Closed Won",
            PipelineId = null,
            DealStageId = null,
            CompanyId = null,
            ContactId = null,
            AssigneeId = null,
            ExpectedCloseDateUtc = null,
            IsWon = true
        };

        _dealRepositoryMock
            .Setup(r => r.GetByIdAsync(dealId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDeal);

        _dealRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Deal>(), userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Deal d, Guid _, Guid? _, CancellationToken _) => d);

        // Act
        var result = await _sut.UpdateAsync(dealId, userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Updated Deal");
        result.Value.Value.Should().Be("20000");
        result.Value.Currency.Should().Be("GBP");
        result.Value.IsWon.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFoundError_WhenDealDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var dealId = Guid.NewGuid();
        var request = new UpdateDealRequest
        {
            Name = "Updated Name",
            Value = null,
            Currency = null,
            Stage = null,
            PipelineId = null,
            DealStageId = null,
            CompanyId = null,
            ContactId = null,
            AssigneeId = null,
            ExpectedCloseDateUtc = null,
            IsWon = null
        };

        _dealRepositoryMock
            .Setup(r => r.GetByIdAsync(dealId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Deal?)null);

        // Act
        var result = await _sut.UpdateAsync(dealId, userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Deal.NotFound");
    }

    [Fact]
    public async Task UpdateAsync_AppliesPartialUpdates_OnlyProvidedFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var dealId = Guid.NewGuid();
        var existingDeal = new Deal
        {
            Id = dealId,
            UserId = userId,
            Name = "Original Name",
            Value = "10000",
            Currency = "USD",
            Stage = "Proposal"
        };
        var request = new UpdateDealRequest
        {
            Name = null, // Don't update name
            Value = "25000", // Update value
            Currency = null, // Don't update currency
            Stage = null,
            PipelineId = null,
            DealStageId = null,
            CompanyId = null,
            ContactId = null,
            AssigneeId = null,
            ExpectedCloseDateUtc = null,
            IsWon = null
        };

        _dealRepositoryMock
            .Setup(r => r.GetByIdAsync(dealId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDeal);

        _dealRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Deal>(), userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Deal d, Guid _, Guid? _, CancellationToken _) => d);

        // Act
        var result = await _sut.UpdateAsync(dealId, userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Original Name"); // Unchanged
        result.Value.Value.Should().Be("25000"); // Updated
        result.Value.Currency.Should().Be("USD"); // Unchanged
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ReturnsSuccess_WhenDealDeleted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var dealId = Guid.NewGuid();

        _dealRepositoryMock
            .Setup(r => r.DeleteAsync(dealId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.DeleteAsync(dealId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNotFoundError_WhenDealDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var dealId = Guid.NewGuid();

        _dealRepositoryMock
            .Setup(r => r.DeleteAsync(dealId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.DeleteAsync(dealId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Deal.NotFound");
    }

    #endregion

    #region SearchAsync Tests

    [Fact]
    public async Task SearchAsync_ReturnsMatchingDeals_WhenQueryMatches()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var query = "Enterprise";
        var deals = new List<Deal>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Enterprise Deal", Value = "100000" }
        };

        _dealRepositoryMock
            .Setup(r => r.SearchAsync(userId, orgId, query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deals);

        _activityRepositoryMock
            .Setup(r => r.GetLastActivityByDealIdsAsync(userId, orgId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DateTime>());

        // Act
        var result = await _sut.SearchAsync(userId, orgId, query);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Enterprise Deal");
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmptyList_WhenNoMatches()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var query = "NonExistent";

        _dealRepositoryMock
            .Setup(r => r.SearchAsync(userId, orgId, query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Deal>());

        _activityRepositoryMock
            .Setup(r => r.GetLastActivityByDealIdsAsync(userId, orgId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DateTime>());

        // Act
        var result = await _sut.SearchAsync(userId, orgId, query);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetDealsPagedAsync Tests

    [Fact]
    public async Task GetDealsPagedAsync_ReturnsPagedResult_WithCorrectPagination()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var deals = new List<Deal>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Deal 1", Value = "10000" },
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Deal 2", Value = "20000" }
        };

        _dealRepositoryMock
            .Setup(r => r.GetPagedAsync(userId, orgId, 0, 10, null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((deals, 25)); // 25 total items

        _activityRepositoryMock
            .Setup(r => r.GetLastActivityByDealIdsAsync(userId, orgId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DateTime>());

        // Act
        var result = await _sut.GetDealsPagedAsync(userId, orgId, page: 1, pageSize: 10);

        // Assert
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(25);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.TotalPages.Should().Be(3);
    }

    #endregion
}
