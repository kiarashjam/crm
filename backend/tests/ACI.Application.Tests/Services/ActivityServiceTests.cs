using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Tests.Services;

/// <summary>
/// Unit tests for ActivityService.
/// </summary>
public class ActivityServiceTests
{
    private readonly Mock<IActivityRepository> _activityRepositoryMock;
    private readonly Mock<ILeadRepository> _leadRepositoryMock;
    private readonly Mock<IContactRepository> _contactRepositoryMock;
    private readonly Mock<IDealRepository> _dealRepositoryMock;
    private readonly Mock<ILogger<ActivityService>> _loggerMock;
    private readonly ActivityService _sut;

    public ActivityServiceTests()
    {
        _activityRepositoryMock = new Mock<IActivityRepository>();
        _leadRepositoryMock = new Mock<ILeadRepository>();
        _contactRepositoryMock = new Mock<IContactRepository>();
        _dealRepositoryMock = new Mock<IDealRepository>();
        _loggerMock = new Mock<ILogger<ActivityService>>();

        _sut = new ActivityService(
            _activityRepositoryMock.Object,
            _leadRepositoryMock.Object,
            _contactRepositoryMock.Object,
            _dealRepositoryMock.Object,
            _loggerMock.Object);
    }

    #region GetByUserIdAsync Tests

    [Fact]
    public async Task GetByUserIdAsync_ReturnsActivities_WhenActivitiesExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var activities = new List<Activity>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Type = "call", Subject = "Call 1" },
            new() { Id = Guid.NewGuid(), UserId = userId, Type = "email", Subject = "Email 1" }
        };

        _activityRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activities);

        // Act
        var result = await _sut.GetByUserIdAsync(userId, orgId);

        // Assert
        result.Should().HaveCount(2);
        result[0].Type.Should().Be("call");
        result[1].Type.Should().Be("email");
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsEmptyList_WhenNoActivitiesExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        _activityRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Activity>());

        // Act
        var result = await _sut.GetByUserIdAsync(userId, orgId);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetByContactIdAsync Tests

    [Fact]
    public async Task GetByContactIdAsync_ReturnsActivities_WhenActivitiesExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var activities = new List<Activity>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Type = "meeting", ContactId = contactId }
        };

        _activityRepositoryMock
            .Setup(r => r.GetByContactIdAsync(contactId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activities);

        // Act
        var result = await _sut.GetByContactIdAsync(contactId, userId, orgId);

        // Assert
        result.Should().HaveCount(1);
        result[0].ContactId.Should().Be(contactId);
    }

    #endregion

    #region GetByDealIdAsync Tests

    [Fact]
    public async Task GetByDealIdAsync_ReturnsActivities_WhenActivitiesExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var dealId = Guid.NewGuid();
        var activities = new List<Activity>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Type = "call", DealId = dealId }
        };

        _activityRepositoryMock
            .Setup(r => r.GetByDealIdAsync(dealId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activities);

        // Act
        var result = await _sut.GetByDealIdAsync(dealId, userId, orgId);

        // Assert
        result.Should().HaveCount(1);
        result[0].DealId.Should().Be(dealId);
    }

    #endregion

    #region GetByLeadIdAsync Tests

    [Fact]
    public async Task GetByLeadIdAsync_ReturnsActivities_WhenActivitiesExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var activities = new List<Activity>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Type = "note", LeadId = leadId }
        };

        _activityRepositoryMock
            .Setup(r => r.GetByLeadIdAsync(leadId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activities);

        // Act
        var result = await _sut.GetByLeadIdAsync(leadId, userId, orgId);

        // Assert
        result.Should().HaveCount(1);
        result[0].LeadId.Should().Be(leadId);
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ReturnsActivity_WhenValidRequest_WithContactId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var request = new CreateActivityRequest
        {
            Type = "call",
            Subject = "Follow-up call",
            Body = "Discussed next steps",
            ContactId = contactId,
            DealId = null,
            LeadId = null,
            Participants = "John, Jane"
        };

        _contactRepositoryMock
            .Setup(r => r.GetByIdAsync(contactId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Contact { Id = contactId, UserId = userId, Name = "Test Contact", Email = "test@example.com" });

        _activityRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Activity a, CancellationToken _) => a);

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be("call");
        result.Value.Subject.Should().Be("Follow-up call");
        result.Value.ContactId.Should().Be(contactId);
    }

    [Fact]
    public async Task CreateAsync_ReturnsActivity_WhenValidRequest_WithDealId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var dealId = Guid.NewGuid();
        var request = new CreateActivityRequest
        {
            Type = "meeting",
            Subject = "Contract review",
            Body = "Review terms",
            ContactId = null,
            DealId = dealId,
            LeadId = null,
            Participants = null
        };

        _dealRepositoryMock
            .Setup(r => r.GetByIdAsync(dealId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Deal { Id = dealId, UserId = userId, Name = "Test Deal", Value = "10000" });

        _activityRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Activity a, CancellationToken _) => a);

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be("meeting");
        result.Value.DealId.Should().Be(dealId);
    }

    [Fact]
    public async Task CreateAsync_ReturnsActivity_WhenValidRequest_WithLeadId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var request = new CreateActivityRequest
        {
            Type = "email",
            Subject = "Introduction",
            Body = "Hello",
            ContactId = null,
            DealId = null,
            LeadId = leadId,
            Participants = null
        };

        _leadRepositoryMock
            .Setup(r => r.GetByIdAsync(leadId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Lead { Id = leadId, UserId = userId, Name = "Test Lead", Email = "lead@example.com" });

        _activityRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Activity a, CancellationToken _) => a);

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be("email");
        result.Value.LeadId.Should().Be(leadId);
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_WhenNoRelatedEntityProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var request = new CreateActivityRequest
        {
            Type = "call",
            Subject = "Test",
            Body = null,
            ContactId = null,
            DealId = null,
            LeadId = null,
            Participants = null
        };

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Activity.NoRelatedEntity");
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_WhenInvalidActivityType()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var request = new CreateActivityRequest
        {
            Type = "invalid_type",
            Subject = "Test",
            Body = null,
            ContactId = contactId,
            DealId = null,
            LeadId = null,
            Participants = null
        };

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Activity.InvalidType");
    }

    [Theory]
    [InlineData("call")]
    [InlineData("meeting")]
    [InlineData("email")]
    [InlineData("note")]
    [InlineData("CALL")]
    [InlineData("Meeting")]
    public async Task CreateAsync_AcceptsValidActivityTypes(string activityType)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var request = new CreateActivityRequest
        {
            Type = activityType,
            Subject = "Test Activity",
            Body = null,
            ContactId = contactId,
            DealId = null,
            LeadId = null,
            Participants = null
        };

        _contactRepositoryMock
            .Setup(r => r.GetByIdAsync(contactId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Contact { Id = contactId, UserId = userId, Name = "Test", Email = "test@example.com" });

        _activityRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Activity a, CancellationToken _) => a);

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be(activityType.ToLowerInvariant());
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_WhenContactNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var request = new CreateActivityRequest
        {
            Type = "call",
            Subject = "Test",
            Body = null,
            ContactId = contactId,
            DealId = null,
            LeadId = null,
            Participants = null
        };

        _contactRepositoryMock
            .Setup(r => r.GetByIdAsync(contactId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact?)null);

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Activity.RelatedEntityNotFound");
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_WhenDealNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var dealId = Guid.NewGuid();
        var request = new CreateActivityRequest
        {
            Type = "meeting",
            Subject = "Test",
            Body = null,
            ContactId = null,
            DealId = dealId,
            LeadId = null,
            Participants = null
        };

        _dealRepositoryMock
            .Setup(r => r.GetByIdAsync(dealId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Deal?)null);

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Activity.RelatedEntityNotFound");
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_WhenLeadNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var request = new CreateActivityRequest
        {
            Type = "email",
            Subject = "Test",
            Body = null,
            ContactId = null,
            DealId = null,
            LeadId = leadId,
            Participants = null
        };

        _leadRepositoryMock
            .Setup(r => r.GetByIdAsync(leadId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Activity.RelatedEntityNotFound");
    }

    [Fact]
    public async Task CreateAsync_DefaultsToNote_WhenTypeIsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var request = new CreateActivityRequest
        {
            Type = null,
            Subject = "Quick note",
            Body = "Some content",
            ContactId = contactId,
            DealId = null,
            LeadId = null,
            Participants = null
        };

        _contactRepositoryMock
            .Setup(r => r.GetByIdAsync(contactId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Contact { Id = contactId, UserId = userId, Name = "Test", Email = "test@example.com" });

        _activityRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Activity>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Activity a, CancellationToken _) => a);

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be("note");
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ReturnsSuccess_WhenActivityDeleted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        _activityRepositoryMock
            .Setup(r => r.DeleteAsync(activityId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.DeleteAsync(activityId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNotFoundError_WhenActivityDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var activityId = Guid.NewGuid();

        _activityRepositoryMock
            .Setup(r => r.DeleteAsync(activityId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.DeleteAsync(activityId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Activity.NotFound");
    }

    #endregion

    #region GetPagedAsync Tests

    [Fact]
    public async Task GetPagedAsync_ReturnsPagedResult_WithCorrectPagination()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var activities = new List<Activity>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Type = "call", Subject = "Call 1" },
            new() { Id = Guid.NewGuid(), UserId = userId, Type = "email", Subject = "Email 1" }
        };

        _activityRepositoryMock
            .Setup(r => r.GetPagedAsync(userId, orgId, 0, 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((activities, 30)); // 30 total items

        // Act
        var result = await _sut.GetPagedAsync(userId, orgId, page: 1, pageSize: 10);

        // Assert
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(30);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task GetPagedAsync_FiltersByActivityType_WhenProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var activities = new List<Activity>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Type = "call", Subject = "Call 1" }
        };

        _activityRepositoryMock
            .Setup(r => r.GetPagedAsync(userId, orgId, 0, 10, null, "call", It.IsAny<CancellationToken>()))
            .ReturnsAsync((activities, 5));

        // Act
        var result = await _sut.GetPagedAsync(userId, orgId, page: 1, pageSize: 10, activityType: "call");

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Type.Should().Be("call");
    }

    #endregion
}
