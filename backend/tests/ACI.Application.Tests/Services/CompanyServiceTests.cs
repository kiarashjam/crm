using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Tests.Services;

/// <summary>
/// Unit tests for CompanyService.
/// </summary>
public class CompanyServiceTests
{
    private readonly Mock<ICompanyRepository> _companyRepositoryMock;
    private readonly Mock<ILogger<CompanyService>> _loggerMock;
    private readonly CompanyService _sut;

    public CompanyServiceTests()
    {
        _companyRepositoryMock = new Mock<ICompanyRepository>();
        _loggerMock = new Mock<ILogger<CompanyService>>();

        _sut = new CompanyService(
            _companyRepositoryMock.Object,
            _loggerMock.Object);
    }

    #region GetCompaniesAsync Tests

    [Fact]
    public async Task GetCompaniesAsync_ReturnsCompanies_WhenCompaniesExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var companies = new List<Company>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Acme Corp", Domain = "acme.com" },
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Tech Inc", Domain = "tech.com" }
        };

        _companyRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(companies);

        // Act
        var result = await _sut.GetCompaniesAsync(userId, orgId);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Acme Corp");
        result[1].Name.Should().Be("Tech Inc");
    }

    [Fact]
    public async Task GetCompaniesAsync_ReturnsEmptyList_WhenNoCompaniesExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        _companyRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Company>());

        // Act
        var result = await _sut.GetCompaniesAsync(userId, orgId);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ReturnsCompany_WhenCompanyExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var company = new Company
        {
            Id = companyId,
            UserId = userId,
            Name = "Test Company",
            Domain = "test.com",
            Industry = "Technology"
        };

        _companyRepositoryMock
            .Setup(r => r.GetByIdAsync(companyId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        // Act
        var result = await _sut.GetByIdAsync(companyId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(companyId);
        result.Value.Name.Should().Be("Test Company");
        result.Value.Domain.Should().Be("test.com");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNotFoundError_WhenCompanyDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        _companyRepositoryMock
            .Setup(r => r.GetByIdAsync(companyId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        // Act
        var result = await _sut.GetByIdAsync(companyId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Company.NotFound");
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ReturnsCompany_WhenValidRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var request = new CreateCompanyRequest
        {
            Name = "New Company",
            Domain = "newcompany.com",
            Industry = "Finance",
            Size = "100-500"
        };

        _companyRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company c, CancellationToken _) => c);

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("New Company");
        result.Value.Domain.Should().Be("newcompany.com");
        result.Value.Industry.Should().Be("Finance");
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_WhenNameIsEmpty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var request = new CreateCompanyRequest
        {
            Name = "",
            Domain = "test.com",
            Industry = null,
            Size = null
        };

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Company.NameRequired");
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_WhenDomainIsInvalid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var request = new CreateCompanyRequest
        {
            Name = "Test Company",
            Domain = "invalid-domain",
            Industry = null,
            Size = null
        };

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Company.DomainInvalid");
    }

    [Fact]
    public async Task CreateAsync_AcceptsNullDomain()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var request = new CreateCompanyRequest
        {
            Name = "Company Without Domain",
            Domain = null,
            Industry = "Retail",
            Size = null
        };

        _companyRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company c, CancellationToken _) => c);

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Domain.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_TrimsWhitespace_FromInputs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        // Note: Domain validation happens before trimming in the service,
        // so we don't include whitespace in the domain
        var request = new CreateCompanyRequest
        {
            Name = "  Trimmed Company  ",
            Domain = "trimmed.com",
            Industry = "  Tech  ",
            Size = "  50-100  "
        };

        _companyRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company c, CancellationToken _) => c);

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Trimmed Company");
        result.Value.Domain.Should().Be("trimmed.com");
        result.Value.Industry.Should().Be("Tech");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ReturnsUpdatedCompany_WhenValidRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var existingCompany = new Company
        {
            Id = companyId,
            UserId = userId,
            Name = "Old Company",
            Domain = "old.com"
        };
        var request = new UpdateCompanyRequest
        {
            Name = "Updated Company",
            Domain = "updated.com",
            Industry = "Healthcare",
            Size = "500+"
        };

        _companyRepositoryMock
            .Setup(r => r.GetByIdAsync(companyId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCompany);

        _companyRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Company>(), userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company c, Guid _, Guid? _, CancellationToken _) => c);

        // Act
        var result = await _sut.UpdateAsync(companyId, userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Updated Company");
        result.Value.Domain.Should().Be("updated.com");
        result.Value.Industry.Should().Be("Healthcare");
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFoundError_WhenCompanyDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var request = new UpdateCompanyRequest
        {
            Name = "Updated Name",
            Domain = null,
            Industry = null,
            Size = null
        };

        _companyRepositoryMock
            .Setup(r => r.GetByIdAsync(companyId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        // Act
        var result = await _sut.UpdateAsync(companyId, userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Company.NotFound");
    }

    [Fact]
    public async Task UpdateAsync_ReturnsError_WhenDomainIsInvalid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var existingCompany = new Company
        {
            Id = companyId,
            UserId = userId,
            Name = "Test Company",
            Domain = "test.com"
        };
        var request = new UpdateCompanyRequest
        {
            Name = null,
            Domain = "bad-domain",
            Industry = null,
            Size = null
        };

        _companyRepositoryMock
            .Setup(r => r.GetByIdAsync(companyId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCompany);

        // Act
        var result = await _sut.UpdateAsync(companyId, userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Company.DomainInvalid");
    }

    [Fact]
    public async Task UpdateAsync_AppliesPartialUpdates_OnlyProvidedFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var companyId = Guid.NewGuid();
        var existingCompany = new Company
        {
            Id = companyId,
            UserId = userId,
            Name = "Original Name",
            Domain = "original.com",
            Industry = "Original Industry",
            Size = "1-10"
        };
        var request = new UpdateCompanyRequest
        {
            Name = null, // Don't update
            Domain = "new.com", // Update
            Industry = null, // Don't update
            Size = null // Don't update
        };

        _companyRepositoryMock
            .Setup(r => r.GetByIdAsync(companyId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingCompany);

        _companyRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Company>(), userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company c, Guid _, Guid? _, CancellationToken _) => c);

        // Act
        var result = await _sut.UpdateAsync(companyId, userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Original Name"); // Unchanged
        result.Value.Domain.Should().Be("new.com"); // Updated
        result.Value.Industry.Should().Be("Original Industry"); // Unchanged
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ReturnsSuccess_WhenCompanyDeleted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        _companyRepositoryMock
            .Setup(r => r.DeleteAsync(companyId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.DeleteAsync(companyId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNotFoundError_WhenCompanyDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var companyId = Guid.NewGuid();

        _companyRepositoryMock
            .Setup(r => r.DeleteAsync(companyId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.DeleteAsync(companyId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Company.NotFound");
    }

    #endregion

    #region SearchAsync Tests

    [Fact]
    public async Task SearchAsync_ReturnsMatchingCompanies_WhenQueryMatches()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var query = "Tech";
        var companies = new List<Company>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Tech Corp", Domain = "tech.com" }
        };

        _companyRepositoryMock
            .Setup(r => r.SearchAsync(userId, orgId, query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(companies);

        // Act
        var result = await _sut.SearchAsync(userId, orgId, query);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Tech Corp");
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmptyList_WhenNoMatches()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var query = "NonExistent";

        _companyRepositoryMock
            .Setup(r => r.SearchAsync(userId, orgId, query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Company>());

        // Act
        var result = await _sut.SearchAsync(userId, orgId, query);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetCompaniesPagedAsync Tests

    [Fact]
    public async Task GetCompaniesPagedAsync_ReturnsPagedResult_WithCorrectPagination()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var companies = new List<Company>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Company 1", Domain = "c1.com" },
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Company 2", Domain = "c2.com" }
        };

        _companyRepositoryMock
            .Setup(r => r.GetPagedAsync(userId, orgId, 0, 10, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((companies, 50)); // 50 total items

        // Act
        var result = await _sut.GetCompaniesPagedAsync(userId, orgId, page: 1, pageSize: 10);

        // Assert
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(50);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.TotalPages.Should().Be(5);
    }

    #endregion
}
