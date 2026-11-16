using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ACI.Application.Tests.Services;

/// <summary>
/// The engine is dispatched after the save that set its rules off has been committed (a lead,
/// a booking, a signature), so nothing it fails to read may turn that save into an error.
/// </summary>
public class AutomationEngineDispatchTests
{
    [Fact]
    public async Task ARuleWhoseFiringCannotBeLookedUp_DoesNotThrowIntoTheSaveThatSetItOff()
    {
        var org = Guid.NewGuid();
        var rule = new AutomationRule
        {
            Id = Guid.NewGuid(),
            OrganizationId = org,
            Name = "Website leads",
            Trigger = AutomationCatalog.LeadCreated,
            Enabled = true,
            ConditionsJson = """{"match":"all","conditions":[{"field":"source","op":"is","value":"Website"}]}""",
            ActionsJson = """[{"type":"notify","config":{}}]""",
        };
        var rules = new Mock<IAutomationRepository>();
        rules.Setup(r => r.EnabledForTriggerAsync(org, AutomationCatalog.LeadCreated, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AutomationRule> { rule });
        var runs = new Mock<IAutomationRunStore>();
        // The database stops answering when the engine asks whether the rule fired for the lead.
        runs.Setup(r => r.HasFiredAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The database could not be reached."));
        var engine = new AutomationEngine(rules.Object, runs.Object, Mock.Of<IAutomationRecordReader>(), Mock.Of<ITaskService>(),
            Mock.Of<INotificationService>(), Mock.Of<ILeadRepository>(), Mock.Of<IDealRepository>(), Mock.Of<IOutbox>(),
            Mock.Of<ISequenceService>(), Mock.Of<ISequenceRepository>(), Mock.Of<IOrganizationRepository>(),
            NullLogger<AutomationEngine>.Instance);

        var dispatch = () => engine.DispatchAsync(AutomationCatalog.LeadCreated,
            new AutomationEvent(org, Guid.NewGuid(), "lead-1:New", "Jean Dupont", LeadId: Guid.NewGuid()));

        await dispatch.Should().NotThrowAsync("the lead was saved before its rules were dispatched");
        runs.Verify(r => r.StartAsync(It.IsAny<AutomationRun>(), It.IsAny<CancellationToken>()), Times.Never,
            "a rule whose conditions could not be checked does not run");
    }
}
