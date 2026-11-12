using System.Reflection;
using ACI.Domain.Common;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Mvc;

namespace ACI.WebApi.Tests;

/// <summary>
/// Every controller says which module it belongs to (InModuleAttribute), or is on a short
/// list of the ones that belong to none, saying why.
/// </summary>
/// <remarks>
/// Without this, a club endpoint added next month keeps answering in a workspace that has
/// turned the club off, while its pages say the module is off.
/// </remarks>
public class EndpointModuleTests
{
    /// <summary>The controllers in no module, and why.</summary>
    private static readonly Dictionary<string, string> InNoModule = new()
    {
        ["AuthController"] = "the caller's own sign-in, password and two-step codes",
        ["SettingsController"] = "the caller's own preferences",
        ["NotificationsController"] = "the caller's own bell",
        ["OrganizationsController"] = "the workspace itself, where its modules are turned on and off",
        ["InvitesController"] = "joining the workspace",
        ["JoinRequestsController"] = "joining the workspace",
        ["TeamsController"] = "the workspace's team",
        ["AuditController"] = "the workspace's history, of both modules",
        ["PrivacyController"] = "a person's export and erasure, which reach both modules: refusing one with a module off would leave the person in it",
        ["ContactsController"] = "contacts belong to both: members and applicants are linked to them",
        ["CompaniesController"] = "companies belong to both",
        ["ContactRelationshipsController"] = "relationships between contacts, of both modules",
        ["DuplicatesController"] = "duplicate contacts and companies, of both modules",
        ["TasksController"] = "tasks belong to both",
        ["ActivitiesController"] = "the timeline of contacts, which belong to both",
        ["CalendarController"] = "tasks and meetings, which belong to both; the club's events are read from their own endpoints",
        ["WorkQueueController"] = "each person's tasks, which belong to both",
        ["AttachmentsController"] = "files on records of both modules",
        ["CustomFieldsController"] = "fields on records of both modules",
        ["SearchController"] = "searches both; with Sales off it leaves leads and deals out of what it finds",
        ["EmailController"] = "email to contacts, which belong to both",
        ["TemplatesController"] = "email templates, used for contacts of both modules",
        ["CopyController"] = "the AI writer, which drafts the email composer's messages to contacts of both modules",
        ["AutomationsController"] = "rules on records of both modules",
        ["ImportsController"] = "files of contacts and companies are for both; a file of leads is refused with Sales off (the action checks)",
        ["WebhookController"] = "the workspace's keys, and leads posted by website forms: refusing those would lose enquiries nobody could be told about",
        ["IntegrationController"] = "linked systems, keyed by the workspace's MCP key",
        ["McpController"] = "AI assistants, keyed by the workspace's MCP key",
        ["PublicLeadController"] = "a public capture form already on a website: refusing it would lose enquiries",
        ["PublicContractsController"] = "someone outside signing a contract they were sent",
        ["PublicEmailController"] = "the mail service reporting deliveries and replies",
        ["PublicUnsubscribeController"] = "an unsubscribe link already sent, which must always work",
        ["PublicPaymentsController"] = "a pay link already sent and the payment provider's webhook: money coming in is never refused",
        ["PublicApplicationsController"] = "a public application form already on the club's website: the application is kept for the club",
        ["PublicOnboardingController"] = "a member portal reporting on a handoff already made: its report is kept, so the deal's timeline is right when Sales is on again",
        ["EmailImagesController"] = "images in the workspace's emails, which go to contacts of both modules",
        ["PublicEmailImagesController"] = "an image in an email already sent: mail apps fetch it every time the email is opened",
    };

    private static IEnumerable<Type> Controllers() =>
        typeof(Program).Assembly.GetTypes().Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

    private static string? ModuleOf(Type controller) => controller.GetCustomAttribute<InModuleAttribute>()?.Module;

    [Fact]
    public void EveryControllerSaysWhichModuleItIsInOrWhyItIsInNone()
    {
        var controllers = Controllers().ToList();
        controllers.Should().HaveCountGreaterThan(60, "the controllers should have been found");

        controllers.Where(c => ModuleOf(c) == null && !InNoModule.ContainsKey(c.Name)).Select(c => c.Name)
            .Should().BeEmpty("a controller needs [InModule(...)] with the module it belongs to, or a line in InNoModule saying why it has none");
        controllers.Where(c => ModuleOf(c) is { } m && !WorkspaceModules.IsModule(m)).Select(c => c.Name)
            .Should().BeEmpty("a module is sales or club");
    }

    [Fact]
    public void TheListOfControllersInNoModuleNamesOnlyRealOnesThatAreInNone()
    {
        var names = Controllers().ToDictionary(c => c.Name);
        InNoModule.Keys.Where(k => !names.ContainsKey(k)).Should().BeEmpty("a line for a controller that is gone would excuse the next one of that name");
        InNoModule.Keys.Where(k => names.TryGetValue(k, out var c) && ModuleOf(c) != null).Should().BeEmpty("a controller is in a module or listed, not both");
    }

    public static readonly TheoryData<string, string> Modules = new()
    {
        { "MembersController", WorkspaceModules.Club }, { "ChargesController", WorkspaceModules.Club },
        { "PaymentsController", WorkspaceModules.Club }, { "TabsController", WorkspaceModules.Club },
        { "EventsController", WorkspaceModules.Club }, { "ReservationsController", WorkspaceModules.Club },
        { "CampaignsController", WorkspaceModules.Club }, { "ApplicationsController", WorkspaceModules.Club },
        { "HospitalityController", WorkspaceModules.Club }, { "BillingController", WorkspaceModules.Club },
        { "MembershipLinksController", WorkspaceModules.Club }, { "BankFilesController", WorkspaceModules.Club },
        { "SalesController", WorkspaceModules.Club }, { "PosController", WorkspaceModules.Club },
        { "MemberCodesController", WorkspaceModules.Club },
        { "LeadsController", WorkspaceModules.Sales }, { "DealsController", WorkspaceModules.Sales },
        { "PipelinesController", WorkspaceModules.Sales }, { "SequencesController", WorkspaceModules.Sales },
        { "ReportingController", WorkspaceModules.Sales }, { "ContractsController", WorkspaceModules.Sales },
        { "OnboardingController", WorkspaceModules.Sales },
    };

    [Theory]
    [MemberData(nameof(Modules))]
    public void EachAreaIsInItsModule(string controller, string module)
    {
        var type = Controllers().Single(c => c.Name == controller);
        ModuleOf(type).Should().Be(module);
    }

    [Fact]
    public void TheModuleIsCheckedBeforeTheCallersPermissions()
    {
        new InModuleAttribute(WorkspaceModules.Club).Order.Should().BeLessThan(new RequiresAttribute(MemberPermissions.ClubMembers).Order,
            "a member is told the module is off, not that their role does not allow something that is not there");
    }
}
