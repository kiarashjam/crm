using ACI.Application.Configuration;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.Infrastructure.Repositories;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ACI.WebApi.Tests;

/// <summary>
/// Accepting an invitation: the emailed token, an account on the invited address, and
/// a confirmed address. All three, or nobody joins.
/// </summary>
/// <remarks>
/// The accept claims the invite with ExecuteUpdateAsync, which the in-memory provider
/// the HTTP tests use cannot run, so these run the real service and repositories on
/// SQLite. The HTTP side (no accept-by-id, 403 for an unconfirmed account) is in
/// <see cref="EmailVerificationTests"/>.
/// </remarks>
public class InviteAcceptanceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly AppDbContext _db;
    private readonly InviteService _invites;
    private readonly List<string> _acceptUrls = new();
    /// <summary>False: the mail server refuses, and the invitee receives nothing.</summary>
    private bool _mailGoesOut = true;
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _orgId = Guid.NewGuid();

    public InviteAcceptanceTests()
    {
        _db = SqliteDb.Open(_connection);
        var sender = new Mock<IEmailSender>();
        sender.Setup(s => s.SendOrganizationInviteEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string _, string _, string _, string url, CancellationToken _) =>
            {
                if (_mailGoesOut) _acceptUrls.Add(url);
            })
            .ReturnsAsync(() => _mailGoesOut);
        _invites = new InviteService(
            new InviteRepository(_db),
            new OrganizationRepository(_db),
            new UserRepository(_db),
            sender.Object,
            Options.Create(new EmailSettings { FrontendBaseUrl = "https://crm.example.ch" }),
            NullLogger<InviteService>.Instance);

        _db.Users.Add(new User { Id = _ownerId, Name = "Manager", Email = "manager@club.ch", CreatedAtUtc = DateTime.UtcNow, EmailVerifiedAtUtc = DateTime.UtcNow });
        _db.Organizations.Add(new Organization { Id = _orgId, Name = "Pavillon 46", OwnerUserId = _ownerId, CreatedAtUtc = DateTime.UtcNow });
        _db.OrganizationMembers.Add(new OrganizationMember { OrganizationId = _orgId, UserId = _ownerId, Role = OrgMemberRole.Owner, JoinedAtUtc = DateTime.UtcNow });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private Guid Account(string email, bool confirmed)
    {
        var id = Guid.NewGuid();
        _db.Users.Add(new User
        {
            Id = id, Name = email, Email = email, CreatedAtUtc = DateTime.UtcNow,
            EmailVerifiedAtUtc = confirmed ? DateTime.UtcNow : null,
        });
        _db.SaveChanges();
        return id;
    }

    private async Task<string> InviteAsync(string email)
    {
        var created = await _invites.CreateInviteAsync(_orgId, _ownerId, new CreateInviteRequest { Email = email });
        created.IsSuccess.Should().BeTrue(created.IsFailure ? created.Error.Description : "");
        var url = _acceptUrls.Last();
        url.Should().StartWith("https://crm.example.ch/invites/accept?token=");
        return Uri.UnescapeDataString(url[(url.IndexOf("token=", StringComparison.Ordinal) + "token=".Length)..]);
    }

    private bool IsMember(Guid userId) => _db.OrganizationMembers.Any(m => m.OrganizationId == _orgId && m.UserId == userId);

    [Fact]
    public async Task TheInviteeJoinsWithTheEmailedLink()
    {
        var token = await InviteAsync("newhire@club.ch");
        var newHire = Account("newhire@club.ch", confirmed: true);

        var accepted = await _invites.AcceptInviteAsync(token, newHire);

        accepted.IsSuccess.Should().BeTrue(accepted.IsFailure ? accepted.Error.Description : "");
        IsMember(newHire).Should().BeTrue();
    }

    [Fact]
    public async Task AnUnconfirmedAccountOnTheInvitedAddressDoesNotJoin()
    {
        var token = await InviteAsync("newhire@club.ch");
        var squatter = Account("newhire@club.ch", confirmed: false);

        var accepted = await _invites.AcceptInviteAsync(token, squatter);

        accepted.Error.Code.Should().Be("Auth.EmailNotVerified");
        IsMember(squatter).Should().BeFalse();
        (await _invites.ListMyPendingInvitesAsync(squatter)).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task AnyOtherTokenDoesNotJoin_IncludingTheStoredHash()
    {
        await InviteAsync("newhire@club.ch");
        var newHire = Account("newhire@club.ch", confirmed: true);
        var storedHash = _db.Invites.Single().TokenHash;

        (await _invites.AcceptInviteAsync(storedHash, newHire)).IsFailure.Should().BeTrue("a copy of the table accepts nothing");
        (await _invites.AcceptInviteAsync("not-the-token", newHire)).IsFailure.Should().BeTrue();
        IsMember(newHire).Should().BeFalse();
    }

    [Fact]
    public async Task AnInvitationWhoseEmailDidNotGoOutIsNotCreated()
    {
        // Only the emailed link accepts. An invitation saved without its email reaching
        // anyone could never be used, yet was listed as pending and reported as sent.
        _mailGoesOut = false;

        var created = await _invites.CreateInviteAsync(_orgId, _ownerId, new CreateInviteRequest { Email = "newhire@club.ch" });

        created.IsFailure.Should().BeTrue();
        created.Error.Code.Should().Be("Invite.EmailNotSent");
        _db.Invites.Should().BeEmpty();
    }

    [Fact]
    public async Task AResendThatCannotBeEmailedKeepsTheLinkAlreadySent()
    {
        var first = await InviteAsync("newhire@club.ch");
        var newHire = Account("newhire@club.ch", confirmed: true);
        _mailGoesOut = false;

        (await _invites.ResendInviteAsync(_db.Invites.Single().Id, _ownerId)).IsFailure.Should().BeTrue();

        (await _invites.AcceptInviteAsync(first, newHire)).IsSuccess.Should().BeTrue("nothing replaced the link the invitee has");
    }

    [Fact]
    public async Task ResendingReplacesTheLink()
    {
        var first = await InviteAsync("newhire@club.ch");
        var resent = await _invites.ResendInviteAsync(_db.Invites.Single().Id, _ownerId);
        resent.IsSuccess.Should().BeTrue();
        var url = _acceptUrls.Last();
        var second = Uri.UnescapeDataString(url[(url.IndexOf("token=", StringComparison.Ordinal) + "token=".Length)..]);
        var newHire = Account("newhire@club.ch", confirmed: true);

        (await _invites.AcceptInviteAsync(first, newHire)).IsFailure.Should().BeTrue("the first link was replaced");
        (await _invites.AcceptInviteAsync(second, newHire)).IsSuccess.Should().BeTrue();
    }
}
