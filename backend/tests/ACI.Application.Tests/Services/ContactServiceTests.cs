using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Tests.Services;

/// <summary>
/// Unit tests for ContactService.
/// </summary>
public class ContactServiceTests
{
    private readonly Mock<IContactRepository> _contactRepositoryMock;
    private readonly Mock<IActivityRepository> _activityRepositoryMock;
    private readonly Mock<ILogger<ContactService>> _loggerMock;
    private readonly ContactService _sut;

    public ContactServiceTests()
    {
        _contactRepositoryMock = new Mock<IContactRepository>();
        _activityRepositoryMock = new Mock<IActivityRepository>();
        _loggerMock = new Mock<ILogger<ContactService>>();
        
        _sut = new ContactService(
            _contactRepositoryMock.Object,
            _activityRepositoryMock.Object,
            _loggerMock.Object);
    }

    #region GetContactsAsync Tests

    [Fact]
    public async Task GetContactsAsync_ReturnsContacts_WhenContactsExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var contacts = new List<Contact>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "John Doe", Email = "john@example.com" },
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "Jane Doe", Email = "jane@example.com" }
        };

        _contactRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, orgId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contacts);
        
        _activityRepositoryMock
            .Setup(r => r.GetLastActivityByContactIdsAsync(userId, orgId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DateTime>());

        // Act
        var result = await _sut.GetContactsAsync(userId, orgId);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("John Doe");
        result[1].Name.Should().Be("Jane Doe");
    }

    [Fact]
    public async Task GetContactsAsync_ReturnsEmptyList_WhenNoContactsExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        _contactRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, orgId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Contact>());
        
        _activityRepositoryMock
            .Setup(r => r.GetLastActivityByContactIdsAsync(userId, orgId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DateTime>());

        // Act
        var result = await _sut.GetContactsAsync(userId, orgId);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ReturnsContact_WhenContactExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var contact = new Contact 
        { 
            Id = contactId, 
            UserId = userId, 
            Name = "John Doe", 
            Email = "john@example.com" 
        };

        _contactRepositoryMock
            .Setup(r => r.GetByIdAsync(contactId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);
        
        _activityRepositoryMock
            .Setup(r => r.GetLastActivityByContactIdsAsync(userId, orgId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DateTime>());

        // Act
        var result = await _sut.GetByIdAsync(contactId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(contactId);
        result.Value.Name.Should().Be("John Doe");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNotFoundError_WhenContactDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        _contactRepositoryMock
            .Setup(r => r.GetByIdAsync(contactId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact?)null);

        // Act
        var result = await _sut.GetByIdAsync(contactId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Contact.NotFound");
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ReturnsContact_WhenValidRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var request = new CreateContactRequest
        {
            Name = "John Doe",
            Email = "john@example.com",
            Phone = "123-456-7890",
            JobTitle = "Developer",
            CompanyId = null
        };

        _contactRepositoryMock
            .Setup(r => r.EmailExistsAsync(request.Email, orgId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _contactRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact c, CancellationToken _) => c);

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("John Doe");
        result.Value.Email.Should().Be("john@example.com");
        result.Value.Phone.Should().Be("123-456-7890");
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_WhenNameIsEmpty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var request = new CreateContactRequest
        {
            Name = "",
            Email = "john@example.com",
            Phone = null,
            JobTitle = null,
            CompanyId = null
        };

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Contact.NameRequired");
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_WhenEmailIsEmpty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var request = new CreateContactRequest
        {
            Name = "John Doe",
            Email = "",
            Phone = null,
            JobTitle = null,
            CompanyId = null
        };

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Contact.EmailRequired");
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_WhenEmailAlreadyExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var request = new CreateContactRequest
        {
            Name = "John Doe",
            Email = "john@example.com",
            Phone = null,
            JobTitle = null,
            CompanyId = null
        };

        _contactRepositoryMock
            .Setup(r => r.EmailExistsAsync(request.Email, orgId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("DuplicateEmail");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ReturnsUpdatedContact_WhenValidRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var existingContact = new Contact
        {
            Id = contactId,
            UserId = userId,
            Name = "John Doe",
            Email = "john@example.com"
        };
        var request = new UpdateContactRequest
        {
            Name = "John Updated",
            Email = null,
            Phone = "999-999-9999",
            CompanyId = null,
            JobTitle = null,
            DoNotContact = null,
            PreferredContactMethod = null
        };

        _contactRepositoryMock
            .Setup(r => r.GetByIdAsync(contactId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContact);

        _contactRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Contact>(), userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact c, Guid _, Guid? _, CancellationToken _) => c);

        // Act
        var result = await _sut.UpdateAsync(contactId, userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("John Updated");
        result.Value.Phone.Should().Be("999-999-9999");
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFoundError_WhenContactDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var request = new UpdateContactRequest
        {
            Name = "Updated Name",
            Email = null,
            Phone = null,
            CompanyId = null,
            JobTitle = null,
            DoNotContact = null,
            PreferredContactMethod = null
        };

        _contactRepositoryMock
            .Setup(r => r.GetByIdAsync(contactId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact?)null);

        // Act
        var result = await _sut.UpdateAsync(contactId, userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Contact.NotFound");
    }

    [Fact]
    public async Task UpdateAsync_ReturnsError_WhenEmailAlreadyExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var existingContact = new Contact
        {
            Id = contactId,
            UserId = userId,
            Name = "John Doe",
            Email = "john@example.com"
        };
        var request = new UpdateContactRequest
        {
            Name = null,
            Email = "taken@example.com",
            Phone = null,
            CompanyId = null,
            JobTitle = null,
            DoNotContact = null,
            PreferredContactMethod = null
        };

        _contactRepositoryMock
            .Setup(r => r.GetByIdAsync(contactId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingContact);

        _contactRepositoryMock
            .Setup(r => r.EmailExistsAsync("taken@example.com", orgId, contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.UpdateAsync(contactId, userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Contain("DuplicateEmail");
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ReturnsSuccess_WhenContactDeleted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        _contactRepositoryMock
            .Setup(r => r.DeleteAsync(contactId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.DeleteAsync(contactId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNotFoundError_WhenContactDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        _contactRepositoryMock
            .Setup(r => r.DeleteAsync(contactId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.DeleteAsync(contactId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Contact.NotFound");
    }

    #endregion

    #region ArchiveAsync Tests

    [Fact]
    public async Task ArchiveAsync_ReturnsSuccess_WhenContactArchived()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        _contactRepositoryMock
            .Setup(r => r.ArchiveAsync(contactId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.ArchiveAsync(contactId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ArchiveAsync_ReturnsNotFoundError_WhenContactDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        _contactRepositoryMock
            .Setup(r => r.ArchiveAsync(contactId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.ArchiveAsync(contactId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Contact.NotFound");
    }

    #endregion

    #region UnarchiveAsync Tests

    [Fact]
    public async Task UnarchiveAsync_ReturnsSuccess_WhenContactUnarchived()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        _contactRepositoryMock
            .Setup(r => r.UnarchiveAsync(contactId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.UnarchiveAsync(contactId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UnarchiveAsync_ReturnsNotFoundError_WhenContactDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var contactId = Guid.NewGuid();

        _contactRepositoryMock
            .Setup(r => r.UnarchiveAsync(contactId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.UnarchiveAsync(contactId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Contact.NotFound");
    }

    #endregion

    #region SearchAsync Tests

    [Fact]
    public async Task SearchAsync_ReturnsMatchingContacts_WhenQueryMatches()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var query = "John";
        var contacts = new List<Contact>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Name = "John Doe", Email = "john@example.com" }
        };

        _contactRepositoryMock
            .Setup(r => r.SearchAsync(userId, orgId, query, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contacts);
        
        _activityRepositoryMock
            .Setup(r => r.GetLastActivityByContactIdsAsync(userId, orgId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, DateTime>());

        // Act
        var result = await _sut.SearchAsync(userId, orgId, query);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("John Doe");
    }

    #endregion
}
