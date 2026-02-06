using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Tests.Services;

/// <summary>
/// Unit tests for OrganizationService.
/// </summary>
public class OrganizationServiceTests
{
    private readonly Mock<IOrganizationRepository> _organizationRepositoryMock;
    private readonly Mock<ILeadStatusRepository> _leadStatusRepositoryMock;
    private readonly Mock<ILeadSourceRepository> _leadSourceRepositoryMock;
    private readonly Mock<ILogger<OrganizationService>> _loggerMock;
    private readonly OrganizationService _sut;

    public OrganizationServiceTests()
    {
        _organizationRepositoryMock = new Mock<IOrganizationRepository>();
        _leadStatusRepositoryMock = new Mock<ILeadStatusRepository>();
        _leadSourceRepositoryMock = new Mock<ILeadSourceRepository>();
        _loggerMock = new Mock<ILogger<OrganizationService>>();

        _sut = new OrganizationService(
            _organizationRepositoryMock.Object,
            _leadStatusRepositoryMock.Object,
            _leadSourceRepositoryMock.Object,
            _loggerMock.Object);
    }

    #region ListMyOrganizationsAsync Tests

    [Fact]
    public async Task ListMyOrganizationsAsync_ReturnsOrganizations_WhenUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgs = new List<Organization>
        {
            new() { Id = Guid.NewGuid(), Name = "Org 1", OwnerUserId = userId },
            new() { Id = Guid.NewGuid(), Name = "Org 2", OwnerUserId = Guid.NewGuid() }
        };

        _organizationRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orgs);

        // Act
        var result = await _sut.ListMyOrganizationsAsync(userId);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Org 1");
        result[0].IsOwner.Should().BeTrue();
        result[1].Name.Should().Be("Org 2");
        result[1].IsOwner.Should().BeFalse();
    }

    [Fact]
    public async Task ListMyOrganizationsAsync_ReturnsEmptyList_WhenUserHasNoOrganizations()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _organizationRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Organization>());

        // Act
        var result = await _sut.ListMyOrganizationsAsync(userId);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetOrganizationAsync Tests

    [Fact]
    public async Task GetOrganizationAsync_ReturnsOrganization_WhenUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var org = new Organization { Id = orgId, Name = "Test Org", OwnerUserId = userId };

        _organizationRepositoryMock
            .Setup(r => r.IsMemberAsync(userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _organizationRepositoryMock
            .Setup(r => r.GetByIdAsync(orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(org);

        // Act
        var result = await _sut.GetOrganizationAsync(orgId, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(orgId);
        result.Value.Name.Should().Be("Test Org");
        result.Value.IsOwner.Should().BeTrue();
    }

    [Fact]
    public async Task GetOrganizationAsync_ReturnsNotMemberError_WhenUserIsNotMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        _organizationRepositoryMock
            .Setup(r => r.IsMemberAsync(userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.GetOrganizationAsync(orgId, userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Organization.NotMember");
    }

    [Fact]
    public async Task GetOrganizationAsync_ReturnsNotFoundError_WhenOrganizationDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        _organizationRepositoryMock
            .Setup(r => r.IsMemberAsync(userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _organizationRepositoryMock
            .Setup(r => r.GetByIdAsync(orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Organization?)null);

        // Act
        var result = await _sut.GetOrganizationAsync(orgId, userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Organization.NotFound");
    }

    #endregion

    #region CreateOrganizationAsync Tests

    [Fact]
    public async Task CreateOrganizationAsync_ReturnsOrganization_WhenValidRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateOrganizationRequest { Name = "New Org" };

        _organizationRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Organization>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Organization org, CancellationToken _) => org);

        _organizationRepositoryMock
            .Setup(r => r.AddMemberAsync(It.IsAny<Guid>(), userId, OrgMemberRole.Owner, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _organizationRepositoryMock
            .Setup(r => r.BackfillUserDataToOrganizationAsync(userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _leadStatusRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LeadStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeadStatus status, CancellationToken _) => status);

        _leadSourceRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LeadSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeadSource source, CancellationToken _) => source);

        // Act
        var result = await _sut.CreateOrganizationAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("New Org");
        result.Value.IsOwner.Should().BeTrue();
        result.Value.OwnerUserId.Should().Be(userId);
    }

    [Fact]
    public async Task CreateOrganizationAsync_UsesDefaultName_WhenNameIsEmpty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateOrganizationRequest { Name = "" };

        _organizationRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Organization>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Organization org, CancellationToken _) => org);

        _organizationRepositoryMock
            .Setup(r => r.AddMemberAsync(It.IsAny<Guid>(), userId, OrgMemberRole.Owner, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _organizationRepositoryMock
            .Setup(r => r.BackfillUserDataToOrganizationAsync(userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _leadStatusRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LeadStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeadStatus status, CancellationToken _) => status);

        _leadSourceRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LeadSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeadSource source, CancellationToken _) => source);

        // Act
        var result = await _sut.CreateOrganizationAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("My Organization");
    }

    [Fact]
    public async Task CreateOrganizationAsync_SeedsDefaultLeadStatuses()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var request = new CreateOrganizationRequest { Name = "Test Org" };
        var addedStatuses = new List<LeadStatus>();

        _organizationRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<Organization>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Organization org, CancellationToken _) => org);

        _organizationRepositoryMock
            .Setup(r => r.AddMemberAsync(It.IsAny<Guid>(), userId, OrgMemberRole.Owner, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _organizationRepositoryMock
            .Setup(r => r.BackfillUserDataToOrganizationAsync(userId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _leadStatusRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LeadStatus>(), It.IsAny<CancellationToken>()))
            .Callback<LeadStatus, CancellationToken>((status, _) => addedStatuses.Add(status))
            .ReturnsAsync((LeadStatus status, CancellationToken _) => status);

        _leadSourceRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<LeadSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LeadSource source, CancellationToken _) => source);

        // Act
        var result = await _sut.CreateOrganizationAsync(userId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        addedStatuses.Should().HaveCount(10); // 10 default statuses
        addedStatuses.Select(s => s.Name).Should().Contain("New");
        addedStatuses.Select(s => s.Name).Should().Contain("Qualified");
    }

    #endregion

    #region GetMembersAsync Tests

    [Fact]
    public async Task GetMembersAsync_ReturnsMembers_WhenUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var members = new List<OrganizationMember>
        {
            new() { UserId = userId, Role = OrgMemberRole.Owner, User = new User { Id = userId, Name = "Owner", Email = "owner@test.com" } },
            new() { UserId = Guid.NewGuid(), Role = OrgMemberRole.Member, User = new User { Id = Guid.NewGuid(), Name = "Member", Email = "member@test.com" } }
        };

        _organizationRepositoryMock
            .Setup(r => r.IsMemberAsync(userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _organizationRepositoryMock
            .Setup(r => r.GetMembersAsync(orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(members);

        // Act
        var result = await _sut.GetMembersAsync(orgId, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(m => m.Name == "Owner" && m.Role == OrgMemberRole.Owner);
        result.Value.Should().Contain(m => m.Name == "Member" && m.Role == OrgMemberRole.Member);
    }

    [Fact]
    public async Task GetMembersAsync_ReturnsNotMemberError_WhenUserIsNotMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        _organizationRepositoryMock
            .Setup(r => r.IsMemberAsync(userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.GetMembersAsync(orgId, userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Organization.NotMember");
    }

    #endregion

    #region UpdateMemberRoleAsync Tests

    [Fact]
    public async Task UpdateMemberRoleAsync_ReturnsSuccess_WhenOwnerUpdatesRole()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var member = new OrganizationMember { UserId = memberId, Role = OrgMemberRole.Member };

        _organizationRepositoryMock
            .Setup(r => r.GetMemberRoleAsync(ownerId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrgMemberRole.Owner);

        _organizationRepositoryMock
            .Setup(r => r.GetMemberAsync(orgId, memberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        _organizationRepositoryMock
            .Setup(r => r.UpdateMemberRoleAsync(orgId, memberId, OrgMemberRole.Manager, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.UpdateMemberRoleAsync(orgId, ownerId, memberId, OrgMemberRole.Manager);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_ReturnsNotOwnerError_WhenNonOwnerTriesToUpdate()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        _organizationRepositoryMock
            .Setup(r => r.GetMemberRoleAsync(requesterId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrgMemberRole.Member);

        // Act
        var result = await _sut.UpdateMemberRoleAsync(orgId, requesterId, memberId, OrgMemberRole.Manager);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Organization.NotOwner");
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_ReturnsError_WhenTryingToAssignOwnerRole()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        _organizationRepositoryMock
            .Setup(r => r.GetMemberRoleAsync(ownerId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrgMemberRole.Owner);

        // Act
        var result = await _sut.UpdateMemberRoleAsync(orgId, ownerId, memberId, OrgMemberRole.Owner);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Organization.CannotAssignOwner");
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_ReturnsError_WhenTryingToChangeOwnersRole()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var ownerMember = new OrganizationMember { UserId = ownerId, Role = OrgMemberRole.Owner };

        _organizationRepositoryMock
            .Setup(r => r.GetMemberRoleAsync(ownerId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrgMemberRole.Owner);

        _organizationRepositoryMock
            .Setup(r => r.GetMemberAsync(orgId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownerMember);

        // Act
        var result = await _sut.UpdateMemberRoleAsync(orgId, ownerId, ownerId, OrgMemberRole.Manager);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Organization.CannotChangeOwnerRole");
    }

    #endregion

    #region RemoveMemberAsync Tests

    [Fact]
    public async Task RemoveMemberAsync_ReturnsSuccess_WhenOwnerRemovesMember()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var member = new OrganizationMember { UserId = memberId, Role = OrgMemberRole.Member };

        _organizationRepositoryMock
            .Setup(r => r.GetMemberRoleAsync(ownerId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrgMemberRole.Owner);

        _organizationRepositoryMock
            .Setup(r => r.GetMemberAsync(orgId, memberId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(member);

        _organizationRepositoryMock
            .Setup(r => r.RemoveMemberAsync(orgId, memberId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.RemoveMemberAsync(orgId, ownerId, memberId);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsNotOwnerError_WhenNonOwnerTriesToRemove()
    {
        // Arrange
        var requesterId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        _organizationRepositoryMock
            .Setup(r => r.GetMemberRoleAsync(requesterId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrgMemberRole.Member);

        // Act
        var result = await _sut.RemoveMemberAsync(orgId, requesterId, memberId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Organization.NotOwner");
    }

    [Fact]
    public async Task RemoveMemberAsync_ReturnsCannotRemoveOwnerError_WhenTryingToRemoveOwner()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var ownerMember = new OrganizationMember { UserId = ownerId, Role = OrgMemberRole.Owner };

        _organizationRepositoryMock
            .Setup(r => r.GetMemberRoleAsync(ownerId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrgMemberRole.Owner);

        _organizationRepositoryMock
            .Setup(r => r.GetMemberAsync(orgId, ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownerMember);

        // Act
        var result = await _sut.RemoveMemberAsync(orgId, ownerId, ownerId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Organization.CannotRemoveOwner");
    }

    #endregion

    #region GetWebhookInfoAsync Tests

    [Fact]
    public async Task GetWebhookInfoAsync_ReturnsWebhookInfo_WhenUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var org = new Organization 
        { 
            Id = orgId, 
            Name = "Test Org",
            WebhookApiKey = "aci_testkey123",
            WebhookApiKeyCreatedAtUtc = DateTime.UtcNow
        };

        _organizationRepositoryMock
            .Setup(r => r.IsMemberAsync(userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _organizationRepositoryMock
            .Setup(r => r.GetByIdAsync(orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(org);

        // Act
        var result = await _sut.GetWebhookInfoAsync(orgId, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HasApiKey.Should().BeTrue();
        result.Value.ApiKey.Should().Be("aci_testkey123");
    }

    [Fact]
    public async Task GetWebhookInfoAsync_ReturnsNotMemberError_WhenUserIsNotMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        _organizationRepositoryMock
            .Setup(r => r.IsMemberAsync(userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.GetWebhookInfoAsync(orgId, userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Organization.NotMember");
    }

    #endregion

    #region GenerateWebhookApiKeyAsync Tests

    [Fact]
    public async Task GenerateWebhookApiKeyAsync_ReturnsApiKey_WhenUserIsOwnerOrManager()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var org = new Organization { Id = orgId, Name = "Test Org" };

        _organizationRepositoryMock
            .Setup(r => r.GetMemberRoleAsync(userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrgMemberRole.Owner);

        _organizationRepositoryMock
            .Setup(r => r.GetByIdAsync(orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(org);

        _organizationRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<Organization>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _sut.GenerateWebhookApiKeyAsync(orgId, userId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().StartWith("aci_");
        result.Value.Length.Should().BeGreaterThan(10);
    }

    [Fact]
    public async Task GenerateWebhookApiKeyAsync_ReturnsNotOwnerOrManagerError_WhenUserIsMember()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();

        _organizationRepositoryMock
            .Setup(r => r.GetMemberRoleAsync(userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(OrgMemberRole.Member);

        // Act
        var result = await _sut.GenerateWebhookApiKeyAsync(orgId, userId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Organization.NotOwnerOrManager");
    }

    #endregion
}
