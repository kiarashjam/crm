using System.Reflection;
using ACI.Domain.Common;
using ACI.WebApi.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace ACI.WebApi.Tests;

/// <summary>
/// Every endpoint that changes something says what it takes (RequiresAttribute), or is on
/// a short list of the ones that need no permission: the caller's own account, anonymous
/// endpoints a key or link answers for, and a few the service checks itself.
/// </summary>
/// <remarks>
/// Without this, an endpoint added next month answers every member who can change
/// anything at all, the front desk and finance included, and nothing says so.
/// </remarks>
public class EndpointPermissionTests
{
    /// <summary>The endpoints that need no permission, and why.</summary>
    private static readonly Dictionary<string, string> NoPermissionNeeded = new()
    {
        ["AuthController.*"] = "the caller's own sign-in, password and two-step codes",
        ["SettingsController.*"] = "the caller's own preferences",
        ["NotificationsController.*"] = "the caller's own bell",
        ["OrganizationsController.Create"] = "a new organization of their own",
        ["InvitesController.Accept"] = "their own membership",
        ["JoinRequestsController.Create"] = "asking to join, which a member of the team then accepts",
        // The organization is the route's, which need not be the header's, so the service
        // checks the caller's permissions in it (OrganizationService and friends).
        ["OrganizationsController.UpdateSettings"] = "OrganizationService: settings.manage",
        ["OrganizationsController.UpdateEmailSending"] = "EmailSendingService: settings.manage",
        ["OrganizationsController.LiftEmailSuppression"] = "EmailSendingService: settings.manage",
        ["OrganizationsController.AddMember"] = "OrganizationService: members.manage, and no more than the caller holds",
        ["OrganizationsController.SetMemberPermissions"] = "OrganizationService: members.manage; lists of their own the owner's alone",
        ["OrganizationsController.UpdateMemberRole"] = "OrganizationService: members.manage",
        ["OrganizationsController.RemoveMember"] = "OrganizationService: members.manage",
        ["OrganizationsController.GenerateWebhookApiKey"] = "OrganizationService: settings.manage",
        ["InvitesController.Create"] = "InviteService: members.manage",
        ["InvitesController.Resend"] = "InviteService: members.manage",
        ["JoinRequestsController.Accept"] = "JoinRequestService: members.manage",
        ["JoinRequestsController.Reject"] = "JoinRequestService: members.manage",
        ["WebhookController.GenerateApiKey"] = "OrganizationService: settings.manage",
        ["WebhookController.GenerateMcpApiKey"] = "OrganizationService: settings.manage",
        ["WebhookController.UpdateWebhookPassword"] = "OrganizationService: settings.manage",
        ["WebhookController.UpdateOutbound"] = "OrganizationService: settings.manage",
    };

    private sealed record Endpoint(Type Controller, MethodInfo Action, string Verb)
    {
        public string Name => $"{Controller.Name}.{Action.Name}";
        public override string ToString() => $"{Verb} {Name}";
    }

    private static IEnumerable<Endpoint> Writes() =>
        from controller in typeof(Program).Assembly.GetTypes()
        where typeof(ControllerBase).IsAssignableFrom(controller) && !controller.IsAbstract
        from action in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
        from verb in action.GetCustomAttributes<HttpMethodAttribute>().SelectMany(a => a.HttpMethods)
        where verb is not ("GET" or "HEAD" or "OPTIONS")
        select new Endpoint(controller, action, verb);

    private static IEnumerable<RequiresAttribute> RequiresOn(Endpoint e) =>
        e.Controller.GetCustomAttributes<RequiresAttribute>().Concat(e.Action.GetCustomAttributes<RequiresAttribute>());

    private static bool Anonymous(Endpoint e) =>
        e.Controller.GetCustomAttribute<AllowAnonymousAttribute>() != null || e.Action.GetCustomAttribute<AllowAnonymousAttribute>() != null;

    private static bool Listed(Endpoint e) =>
        NoPermissionNeeded.ContainsKey(e.Name) || NoPermissionNeeded.ContainsKey($"{e.Controller.Name}.*");

    [Fact]
    public void EveryEndpointThatChangesSomethingSaysWhatItTakes()
    {
        var writes = Writes().ToList();
        writes.Should().HaveCountGreaterThan(150, "the controllers should have been found");

        var unguarded = writes.Where(e => !RequiresOn(e).Any() && !Anonymous(e) && !Listed(e)).Select(e => e.ToString()).ToList();

        unguarded.Should().BeEmpty(
            "an endpoint that changes something needs [Requires(...)] with the permission it takes, or a line in NoPermissionNeeded saying why it needs none");
    }

    [Fact]
    public void TheListOfEndpointsThatNeedNoPermissionNamesOnlyRealOnes()
    {
        var names = Writes().SelectMany(e => new[] { e.Name, $"{e.Controller.Name}.*" }).ToHashSet();

        NoPermissionNeeded.Keys.Where(k => !names.Contains(k)).Should().BeEmpty("a line for an endpoint that is gone would excuse the next one of that name");
        Writes().Where(e => Listed(e) && RequiresOn(e).Any()).Select(e => e.ToString()).Should().BeEmpty("an endpoint is either guarded or listed, not both");
    }

    [Fact]
    public void EveryPermissionAnEndpointAsksForIsOneTheTeamPageOffers()
    {
        var asked = typeof(Program).Assembly.GetTypes()
            .SelectMany(t => t.GetCustomAttributes<RequiresAttribute>()
                .Concat(t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly).SelectMany(m => m.GetCustomAttributes<RequiresAttribute>())))
            .SelectMany(r => r.Permissions)
            .Distinct()
            .ToList();

        asked.Should().NotBeEmpty();
        asked.Where(p => !MemberPermissions.IsPermission(p)).Should().BeEmpty();
    }

    /// <summary>Deleting or merging a record takes records.delete on top of what changing it takes.</summary>
    public static readonly TheoryData<string, string> Deletes = new()
    {
        { "LeadsController", "Delete" }, { "LeadsController", "Merge" },
        { "DealsController", "Delete" }, { "DealsController", "Merge" },
        { "ContactsController", "Delete" }, { "ContactsController", "Merge" },
        { "CompaniesController", "Delete" }, { "CompaniesController", "Merge" },
        { "ActivitiesController", "Delete" }, { "TasksController", "Delete" }, { "ProductsController", "Delete" },
        { "SequencesController", "Delete" }, { "ImportsController", "Undo" },
        { "MembersController", "Delete" }, { "MembersController", "Merge" }, { "VisitsController", "Delete" }, { "LoyaltyController", "Delete" },
        { "CampaignsController", "Delete" }, { "EventsController", "Delete" }, { "DonationsController", "Delete" },
        { "CommunicationsController", "Delete" }, { "ApplicationsController", "Delete" }, { "ReservationsController", "Delete" },
        { "HouseholdsController", "Delete" }, { "SubscriptionsController", "Delete" },
    };

    [Theory]
    [MemberData(nameof(Deletes))]
    public void DeletingARecordTakesTheDeletePermission(string controller, string action)
    {
        var endpoint = Writes().Where(e => e.Controller.Name == controller && e.Action.Name == action).ToList();
        endpoint.Should().ContainSingle($"{controller}.{action} should exist");
        RequiresOn(endpoint[0]).Should().Contain(r => r.Permissions.SequenceEqual(new[] { MemberPermissions.DeleteRecords }) && !r.WritesOnly && !r.Any);
    }

    /// <summary>Money, settings, exports and the club's areas, each behind its own permission.</summary>
    public static readonly TheoryData<string, string, string> Areas = new()
    {
        { "ChargesController", "Create", MemberPermissions.ManageMoney },
        { "PaymentsController", "Create", MemberPermissions.ManageMoney },
        { "SubscriptionsController", "Bill", MemberPermissions.ManageMoney },
        { "DonationsController", "Create", MemberPermissions.ManageMoney },
        { "GiftCardsController", "Create", MemberPermissions.ManageMoney },
        { "ChargesController", "Void", MemberPermissions.VoidMoney },
        { "PaymentsController", "Void", MemberPermissions.VoidMoney },
        { "GiftCardsController", "Void", MemberPermissions.VoidMoney },
        // A check-in's points are written by the app as a ledger entry, so the front desk may add one.
        { "LoyaltyController", "Add", MemberPermissions.FrontDesk },
        { "PipelinesController", "Create", MemberPermissions.ManageSettings },
        { "LeadStatusesController", "Update", MemberPermissions.ManageSettings },
        { "AutomationsController", "Create", MemberPermissions.ManageSettings },
        { "HospitalityController", "Save", MemberPermissions.ManageSettings },
        { "TeamsController", "Create", MemberPermissions.ManageMembers },
        { "MembersController", "Create", MemberPermissions.ClubMembers },
        { "HouseholdsController", "Create", MemberPermissions.ClubMembers },
        { "VisitsController", "Create", MemberPermissions.FrontDesk },
        { "ReservationsController", "Create", MemberPermissions.FrontDesk },
        { "TabsController", "Close", MemberPermissions.FrontDesk },
        { "LeadsController", "Create", MemberPermissions.EditRecords },
        { "DealsController", "Update", MemberPermissions.EditRecords },
    };

    [Theory]
    [MemberData(nameof(Areas))]
    public void EachAreaTakesItsOwnPermission(string controller, string action, string permission)
    {
        var endpoint = Writes().Where(e => e.Controller.Name == controller && e.Action.Name == action).ToList();
        endpoint.Should().ContainSingle($"{controller}.{action} should exist");
        RequiresOn(endpoint[0]).Should().Contain(r => r.Permissions.Contains(permission));
    }

    [Theory]
    [InlineData("ContactsController")]
    [InlineData("CompaniesController")]
    public void ExportingAListTakesTheExportPermission(string controller)
    {
        var export = typeof(Program).Assembly.GetTypes().Single(t => t.Name == controller).GetMethod("Export")!;
        export.GetCustomAttributes<HttpGetAttribute>().Should().ContainSingle();
        export.GetCustomAttributes<RequiresAttribute>().Should().Contain(r => r.Permissions.SequenceEqual(new[] { MemberPermissions.ExportRecords }) && !r.WritesOnly,
            "the export is a read, so a check that skips reads would let anyone take the list away");
    }
}
