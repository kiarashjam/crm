using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Tests.Services;

/// <summary>
/// Unit tests for LeadService.
/// </summary>
public class LeadServiceTests
{
    private readonly Mock<ILeadRepository> _leadRepositoryMock;
    private readonly Mock<ICompanyRepository> _companyRepositoryMock;
    private readonly Mock<IContactRepository> _contactRepositoryMock;
    private readonly Mock<IDealRepository> _dealRepositoryMock;
    private readonly Mock<ILogger<LeadService>> _loggerMock;
    private readonly LeadService _sut;

    public LeadServiceTests()
    {
        _leadRepositoryMock = new Mock<ILeadRepository>();
        _companyRepositoryMock = new Mock<ICompanyRepository>();
        _contactRepositoryMock = new Mock<IContactRepository>();
        _dealRepositoryMock = new Mock<IDealRepository>();
        _loggerMock = new Mock<ILogger<LeadService>>();

        _sut = new LeadService(
            _leadRepositoryMock.Object,
            _companyRepositoryMock.Object,
            _contactRepositoryMock.Object,
            _dealRepositoryMock.Object,
            _loggerMock.Object);
    }

    #region GetLeadsAsync Tests

    [Fact]
    public async Task GetLeadsAsync_ReturnsLeads_WhenLeadsExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var leads = new List<Lead>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Lead 1", Email = "lead1@example.com" },
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Lead 2", Email = "lead2@example.com" }
        };

        _leadRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(leads);

        // Act
        var result = await _sut.GetLeadsAsync(userId, orgId);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Lead 1");
        result[1].Name.Should().Be("Lead 2");
    }

    [Fact]
    public async Task GetLeadsAsync_ReturnsEmptyList_WhenNoLeadsExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        _leadRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Lead>());

        // Act
        var result = await _sut.GetLeadsAsync(userId, orgId);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ReturnsLead_WhenLeadExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var lead = new Lead
        {
            Id = leadId,
            UserId = userId,
            Name = "Test Lead",
            Email = "test@example.com"
        };

        _leadRepositoryMock
            .Setup(r => r.GetByIdAsync(leadId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(lead);

        // Act
        var result = await _sut.GetByIdAsync(leadId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(leadId);
        result.Value.Name.Should().Be("Test Lead");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNotFoundError_WhenLeadDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        _leadRepositoryMock
            .Setup(r => r.GetByIdAsync(leadId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead?)null);

        // Act
        var result = await _sut.GetByIdAsync(leadId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Lead.NotFound");
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ReturnsLead_WhenValidRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var request = new CreateLeadRequest
        {
            Name = "New Lead",
            Email = "newlead@example.com",
            Phone = "123-456-7890",
            CompanyId = null,
            Source = "Website",
            Status = "New"
        };

        _leadRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Lead>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Lead l, CancellationToken _) => l);

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("New Lead");
        result.Value.Email.Should().Be("newlead@example.com");
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_WhenNameIsEmpty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var request = new CreateLeadRequest
        {
            Name = "",
            Email = "test@example.com",
            Phone = null,
            CompanyId = null,
            Source = null,
            Status = "New"
        };

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Lead.NameRequired");
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_WhenEmailIsEmpty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var request = new CreateLeadRequest
        {
            Name = "Test Lead",
            Email = "",
            Phone = null,
            CompanyId = null,
            Source = null,
            Status = "New"
        };

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Lead.EmailRequired");
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ReturnsSuccess_WhenLeadDeleted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        _leadRepositoryMock
            .Setup(r => r.DeleteAsync(leadId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.DeleteAsync(leadId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNotFoundError_WhenLeadDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var leadId = Guid.NewGuid();

        _leadRepositoryMock
            .Setup(r => r.DeleteAsync(leadId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.DeleteAsync(leadId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Lead.NotFound");
    }

    #endregion

    #region SearchAsync Tests

    [Fact]
    public async Task SearchAsync_ReturnsMatchingLeads_WhenQueryMatches()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var query = "Test";
        var leads = new List<Lead>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Test Lead", Email = "test@example.com" }
        };

        _leadRepositoryMock
            .Setup(r => r.SearchAsync(userId, orgId, query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(leads);

        // Act
        var result = await _sut.SearchAsync(userId, orgId, query);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Test Lead");
    }

    #endregion
}
