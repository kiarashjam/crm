using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Tests.Services;

/// <summary>
/// The server's own writes to a lead finish their work when someone saved the lead in the
/// same moment, instead of failing part of the way through.
/// </summary>
/// <remarks>
/// Every lead save is refused once the lead changed after it was read (see IVersioned).
/// A request that has already saved something by then (a logged call, a conversion's new
/// contact) used to fail after that: the person was told it had not worked, did it again,
/// and the first time was left behind.
/// </remarks>
public class LeadWriteRaceTests
{
    private static Lead Lead(Guid id, Guid userId) => new()
    {
        Id = id, UserId = userId, Name = "Anna Keller", Email = "anna@example.ch", Status = "New",
        CreatedAtUtc = DateTime.UtcNow.AddDays(-2),
    };

    [Fact]
    public async Task AFirstResponseOnALeadSavedMeanwhileIsRecordedOnTheNewerCopy()
    {
        var userId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var stale = Lead(id, userId);
        var fresh = Lead(id, userId);
        fresh.Description = "Saved by a colleague a moment ago";
        var ops = new Mock<ILeadOpsRepository>();
        ops.SetupSequence(o => o.GetLeadAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(stale).ReturnsAsync(fresh);
        ops.Setup(o => o.GetScoringRulesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<LeadScoringRule>());
        ops.Setup(o => o.SaveLeadAsync(stale, It.IsAny<CancellationToken>())).ThrowsAsync(new ConcurrencyConflictException());
        ops.Setup(o => o.SaveLeadAsync(fresh, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var automation = new LeadAutomation(ops.Object, Mock.Of<ICompanyRepository>());

        await automation.MarkFirstResponseAsync(id, userId, null, "call");

        fresh.FirstResponseAtUtc.Should().NotBeNull("the answer is recorded on the copy that was saved");
        fresh.LastContactedAt.Should().NotBeNull();
        ops.Verify(o => o.SaveLeadAsync(fresh, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnSlaRunGoesOnPastALeadSavedMeanwhile()
    {
        var userId = Guid.NewGuid();
        var raced = Lead(Guid.NewGuid(), userId);
        var calm = Lead(Guid.NewGuid(), userId);
        var ops = new Mock<ILeadOpsRepository>();
        ops.Setup(o => o.OverdueFirstResponseAsync(It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { raced, calm });
        ops.Setup(o => o.SaveLeadAsync(raced, It.IsAny<CancellationToken>())).ThrowsAsync(new ConcurrencyConflictException());
        ops.Setup(o => o.SaveLeadAsync(calm, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var notifications = new Mock<INotificationService>();
        var service = new LeadOpsService(
            ops.Object, Mock.Of<ILeadService>(), Mock.Of<ILeadRepository>(), notifications.Object,
            Mock.Of<ILeadAutomation>(), Mock.Of<IActivityService>());

        await service.NotifyOverdueAsync();

        notifications.Verify(n => n.CreateAsync(userId, null,
            It.Is<CreateNotificationRequest>(r => r.Link == $"/leads/{calm.Id}"), It.IsAny<CancellationToken>()), Times.Once);
        notifications.Verify(n => n.CreateAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(),
            It.Is<CreateNotificationRequest>(r => r.Link == $"/leads/{raced.Id}"), It.IsAny<CancellationToken>()), Times.Never,
            "it was saved by someone else, most likely the call that answered it");
    }

    [Fact]
    public async Task AConversionMarksTheNewerCopyWhenTheLeadWasSavedMeanwhile()
    {
        var userId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var stale = Lead(id, userId);
        var fresh = Lead(id, userId);
        var (sut, leads, contacts) = Conversion();
        leads.SetupSequence(r => r.GetByIdAsync(id, userId, null, It.IsAny<CancellationToken>())).ReturnsAsync(stale).ReturnsAsync(fresh);
        leads.Setup(r => r.UpdateAsync(stale, userId, null, It.IsAny<CancellationToken>())).ThrowsAsync(new ConcurrencyConflictException());
        leads.Setup(r => r.UpdateAsync(fresh, userId, null, It.IsAny<CancellationToken>())).ReturnsAsync(fresh);

        var result = await sut.ConvertAsync(id, userId, null, new ConvertLeadRequest { CreateContact = true });

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : "");
        fresh.IsConverted.Should().BeTrue();
        fresh.ConvertedToContactId.Should().Be(result.Value.ContactId);
        contacts.Verify(c => c.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Once, "the contact is made once");
    }

    [Fact]
    public async Task AConversionThatAnotherRequestFinishedFirstSaysSo()
    {
        var userId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var stale = Lead(id, userId);
        var converted = Lead(id, userId);
        converted.IsConverted = true;
        var (sut, leads, _) = Conversion();
        leads.SetupSequence(r => r.GetByIdAsync(id, userId, null, It.IsAny<CancellationToken>())).ReturnsAsync(stale).ReturnsAsync(converted);
        leads.Setup(r => r.UpdateAsync(It.IsAny<Lead>(), userId, null, It.IsAny<CancellationToken>())).ThrowsAsync(new ConcurrencyConflictException());

        var result = await sut.ConvertAsync(id, userId, null, new ConvertLeadRequest { CreateContact = true });

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(DomainErrors.Lead.AlreadyConverted);
        leads.Verify(r => r.UpdateAsync(It.IsAny<Lead>(), userId, null, It.IsAny<CancellationToken>()), Times.Once,
            "a lead already converted is not written again");
    }

    private static (LeadService Sut, Mock<ILeadRepository> Leads, Mock<IContactRepository> Contacts) Conversion()
    {
        var leads = new Mock<ILeadRepository>();
        var contacts = new Mock<IContactRepository>();
        contacts.Setup(c => c.GetByEmailAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact?)null);
        contacts.Setup(c => c.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact c, CancellationToken _) => c);
        var automation = new Mock<ILeadAutomation>();
        var sut = new LeadService(
            leads.Object, Mock.Of<ICompanyRepository>(), contacts.Object, Mock.Of<IDealRepository>(), automation.Object,
            WorkspaceRecordsMock.InWorkspace().Object, new StatusSyncHarness().Create(leads.Object, automation.Object),
            Mock.Of<ILogger<LeadService>>());
        return (sut, leads, contacts);
    }
}
