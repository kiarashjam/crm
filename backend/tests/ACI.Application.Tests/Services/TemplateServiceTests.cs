using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Tests.Services;

/// <summary>
/// Unit tests for TemplateService.
/// </summary>
public class TemplateServiceTests
{
    private readonly Mock<ITemplateRepository> _templateRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ILogger<TemplateService>> _loggerMock;
    private readonly TemplateService _sut;

    public TemplateServiceTests()
    {
        _templateRepositoryMock = new Mock<ITemplateRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _loggerMock = new Mock<ILogger<TemplateService>>();

        _sut = new TemplateService(
            _templateRepositoryMock.Object,
            _userRepositoryMock.Object,
            _loggerMock.Object);
    }

    #region GetUserTemplatesAsync Tests

    [Fact]
    public async Task GetUserTemplatesAsync_ReturnsUserTemplates_WhenTemplatesExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var templates = new List<Template>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Title = "Template 1", Content = "Content 1", CopyTypeId = CopyTypeId.SalesEmail },
            new() { Id = Guid.NewGuid(), UserId = userId, Title = "Template 2", Content = "Content 2", CopyTypeId = CopyTypeId.FollowUp }
        };

        _templateRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        _templateRepositoryMock
            .Setup(r => r.GetSharedTemplatesAsync(orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Template>());

        _templateRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Template>());

        // Act
        var result = await _sut.GetUserTemplatesAsync(userId, orgId);

        // Assert
        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Template 1");
        result[1].Title.Should().Be("Template 2");
    }

    [Fact]
    public async Task GetUserTemplatesAsync_IncludesSharedTemplates_WhenOrganizationIdProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        
        var userTemplates = new List<Template>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Title = "My Template", Content = "Content", CopyTypeId = CopyTypeId.SalesEmail }
        };
        
        var sharedTemplates = new List<Template>
        {
            new() { Id = Guid.NewGuid(), UserId = otherUserId, Title = "Shared Template", Content = "Content", IsSharedWithOrganization = true, CopyTypeId = CopyTypeId.SalesEmail }
        };

        _templateRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(userTemplates);

        _templateRepositoryMock
            .Setup(r => r.GetSharedTemplatesAsync(orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedTemplates);

        _templateRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Template>());

        // Act
        var result = await _sut.GetUserTemplatesAsync(userId, orgId);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(t => t.Title == "My Template");
        result.Should().Contain(t => t.Title == "Shared Template");
    }

    [Fact]
    public async Task GetUserTemplatesAsync_IncludesSystemTemplates()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var systemTemplates = new List<Template>
        {
            new() { Id = Guid.NewGuid(), UserId = Guid.Empty, Title = "System Template", Content = "Content", IsSystemTemplate = true, CopyTypeId = CopyTypeId.SalesEmail }
        };

        _templateRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Template>());

        _templateRepositoryMock
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(systemTemplates);

        // Act
        var result = await _sut.GetUserTemplatesAsync(userId, null);

        // Assert
        result.Should().HaveCount(1);
        result[0].Title.Should().Be("System Template");
        result[0].IsSystemTemplate.Should().BeTrue();
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ReturnsTemplate_WhenUserOwnsTemplate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var template = new Template
        {
            Id = templateId,
            UserId = userId,
            Title = "My Template",
            Content = "Content",
            CopyTypeId = CopyTypeId.SalesEmail
        };

        _templateRepositoryMock
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        // Act
        var result = await _sut.GetByIdAsync(userId, templateId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(templateId);
        result.Value.Title.Should().Be("My Template");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTemplate_WhenTemplateIsSharedWithOrganization()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var template = new Template
        {
            Id = templateId,
            UserId = otherUserId,
            Title = "Shared Template",
            Content = "Content",
            IsSharedWithOrganization = true,
            CopyTypeId = CopyTypeId.SalesEmail
        };

        _templateRepositoryMock
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        // Act
        var result = await _sut.GetByIdAsync(userId, templateId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Shared Template");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTemplate_WhenTemplateIsSystemTemplate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var template = new Template
        {
            Id = templateId,
            UserId = Guid.Empty,
            Title = "System Template",
            Content = "Content",
            IsSystemTemplate = true,
            CopyTypeId = CopyTypeId.SalesEmail
        };

        _templateRepositoryMock
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        // Act
        var result = await _sut.GetByIdAsync(userId, templateId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsSystemTemplate.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNotFoundError_WhenTemplateDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        _templateRepositoryMock
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        // Act
        var result = await _sut.GetByIdAsync(userId, templateId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Template.NotFound");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNotOwnedError_WhenUserDoesNotHaveAccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var template = new Template
        {
            Id = templateId,
            UserId = otherUserId,
            Title = "Private Template",
            Content = "Content",
            IsSharedWithOrganization = false,
            IsSystemTemplate = false,
            CopyTypeId = CopyTypeId.SalesEmail
        };

        _templateRepositoryMock
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        // Act
        var result = await _sut.GetByIdAsync(userId, templateId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Template.NotOwned");
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ReturnsTemplate_WhenValidRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var request = new CreateTemplateRequest
        {
            Title = "New Template",
            Description = "A new template",
            Category = "Sales",
            CopyTypeId = "sales-email",
            Goal = "Close deals",
            Content = "Template content with {{placeholder}}",
            BrandTone = "Professional",
            Length = "medium",
            IsSharedWithOrganization = false
        };

        _templateRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template t, CancellationToken _) => t);

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("New Template");
        result.Value.Category.Should().Be("Sales");
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_WhenTitleIsEmpty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateTemplateRequest
        {
            Title = "",
            Description = null,
            Category = null,
            CopyTypeId = "sales-email",
            Goal = null,
            Content = "Content",
            BrandTone = null,
            Length = null,
            IsSharedWithOrganization = false
        };

        // Act
        var result = await _sut.CreateAsync(userId, null, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Template.TitleRequired");
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_WhenContentIsEmpty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateTemplateRequest
        {
            Title = "Template",
            Description = null,
            Category = null,
            CopyTypeId = "sales-email",
            Goal = null,
            Content = "",
            BrandTone = null,
            Length = null,
            IsSharedWithOrganization = false
        };

        // Act
        var result = await _sut.CreateAsync(userId, null, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Template.ContentRequired");
    }

    [Fact]
    public async Task CreateAsync_DefaultsCategory_WhenNotProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateTemplateRequest
        {
            Title = "Template",
            Description = null,
            Category = null,
            CopyTypeId = "sales-email",
            Goal = null,
            Content = "Content",
            BrandTone = null,
            Length = null,
            IsSharedWithOrganization = false
        };

        _templateRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template t, CancellationToken _) => t);

        // Act
        var result = await _sut.CreateAsync(userId, null, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Category.Should().Be("Custom");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ReturnsUpdatedTemplate_WhenValidRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var existingTemplate = new Template
        {
            Id = templateId,
            UserId = userId,
            Title = "Old Title",
            Content = "Old Content",
            CopyTypeId = CopyTypeId.SalesEmail
        };
        var request = new UpdateTemplateRequest
        {
            Title = "Updated Title",
            Description = "Updated Description",
            Content = "Updated Content",
            Category = null,
            CopyTypeId = null,
            Goal = null,
            BrandTone = null,
            Length = null,
            IsSharedWithOrganization = null
        };

        _templateRepositoryMock
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTemplate);

        _templateRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template t, CancellationToken _) => t);

        // Act
        var result = await _sut.UpdateAsync(userId, templateId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Updated Title");
        result.Value.Content.Should().Be("Updated Content");
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFoundError_WhenTemplateDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var request = new UpdateTemplateRequest
        {
            Title = "Updated",
            Description = null,
            Content = null,
            Category = null,
            CopyTypeId = null,
            Goal = null,
            BrandTone = null,
            Length = null,
            IsSharedWithOrganization = null
        };

        _templateRepositoryMock
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        // Act
        var result = await _sut.UpdateAsync(userId, templateId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Template.NotFound");
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotOwnedError_WhenUserDoesNotOwnTemplate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var existingTemplate = new Template
        {
            Id = templateId,
            UserId = otherUserId,
            Title = "Other's Template",
            Content = "Content",
            CopyTypeId = CopyTypeId.SalesEmail
        };
        var request = new UpdateTemplateRequest
        {
            Title = "Trying to update",
            Description = null,
            Content = null,
            Category = null,
            CopyTypeId = null,
            Goal = null,
            BrandTone = null,
            Length = null,
            IsSharedWithOrganization = null
        };

        _templateRepositoryMock
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTemplate);

        // Act
        var result = await _sut.UpdateAsync(userId, templateId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Template.NotOwned");
    }

    [Fact]
    public async Task UpdateAsync_ReturnsCannotModifySystemError_WhenTemplateIsSystem()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var existingTemplate = new Template
        {
            Id = templateId,
            UserId = userId,
            Title = "System Template",
            Content = "Content",
            IsSystemTemplate = true,
            CopyTypeId = CopyTypeId.SalesEmail
        };
        var request = new UpdateTemplateRequest
        {
            Title = "Trying to update system",
            Description = null,
            Content = null,
            Category = null,
            CopyTypeId = null,
            Goal = null,
            BrandTone = null,
            Length = null,
            IsSharedWithOrganization = null
        };

        _templateRepositoryMock
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTemplate);

        // Act
        var result = await _sut.UpdateAsync(userId, templateId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Template.CannotModifySystem");
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ReturnsSuccess_WhenTemplateDeleted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var template = new Template
        {
            Id = templateId,
            UserId = userId,
            Title = "My Template",
            Content = "Content",
            CopyTypeId = CopyTypeId.SalesEmail
        };

        _templateRepositoryMock
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        _templateRepositoryMock
            .Setup(r => r.DeleteAsync(templateId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.DeleteAsync(userId, templateId);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNotFoundError_WhenTemplateDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var templateId = Guid.NewGuid();

        _templateRepositoryMock
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        // Act
        var result = await _sut.DeleteAsync(userId, templateId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Template.NotFound");
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNotOwnedError_WhenUserDoesNotOwnTemplate()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var template = new Template
        {
            Id = templateId,
            UserId = otherUserId,
            Title = "Other's Template",
            Content = "Content",
            CopyTypeId = CopyTypeId.SalesEmail
        };

        _templateRepositoryMock
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        // Act
        var result = await _sut.DeleteAsync(userId, templateId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Template.NotOwned");
    }

    [Fact]
    public async Task DeleteAsync_ReturnsCannotModifySystemError_WhenTemplateIsSystem()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var templateId = Guid.NewGuid();
        var template = new Template
        {
            Id = templateId,
            UserId = userId,
            Title = "System Template",
            Content = "Content",
            IsSystemTemplate = true,
            CopyTypeId = CopyTypeId.SalesEmail
        };

        _templateRepositoryMock
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        // Act
        var result = await _sut.DeleteAsync(userId, templateId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Template.CannotModifySystem");
    }

    #endregion

    #region IncrementUseCountAsync Tests

    [Fact]
    public async Task IncrementUseCountAsync_CallsRepository()
    {
        // Arrange
        var templateId = Guid.NewGuid();

        _templateRepositoryMock
            .Setup(r => r.IncrementUseCountAsync(templateId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.IncrementUseCountAsync(templateId);

        // Assert
        _templateRepositoryMock.Verify(r => r.IncrementUseCountAsync(templateId, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
