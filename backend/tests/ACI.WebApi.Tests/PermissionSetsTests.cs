using System.Net;
using System.Text.Json;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// What each preset lets a member do, over HTTP as the app does it: finance takes payments
/// and does not work the pipeline, a sales rep works leads but neither deletes nor exports,
/// the front desk checks members in and takes no money, a read-only accountant reads and
/// exports. Members from before presets keep everything they could do; the owner changes
/// what anyone may do, and it holds from their next request.
/// </summary>
public class PermissionSetsTests
{
    private sealed record Club(CustomWebApplicationFactory App, Guid Org, HttpClient Owner, Guid OwnerId) : IDisposable
    {
        public void Dispose() => App.Dispose();
    }

    private static async Task<Club> ClubAsync()
    {
        var app = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(app, "olga@pavillon46.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon46");
        return new Club(app, org, owner.In(org), ownerId);
    }

    /// <summary>Someone the owner adds on the Team page with a preset.</summary>
    private static async Task<(HttpClient Client, Guid UserId)> JoinAsync(Club club, string email, string preset)
    {
        var (client, userId) = await SignInAsync(club.App, email);
        using (var scope = club.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Users.Single(u => u.Id == userId).EmailVerifiedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        var added = await Ok(await club.Owner.PostAsync($"/api/organizations/{club.Org}/members", Body(new { email, role = OrgMemberRole.Member, preset })), $"add {email} as {preset}");
        added.GetProperty("preset").GetString().Should().Be(preset);
        return (client.In(club.Org), userId);
    }

    /// <summary>Someone who joined before presets: a role and nothing else.</summary>
    private static async Task<(HttpClient Client, Guid UserId)> OldMemberAsync(Club club, string email, OrgMemberRole role)
    {
        var (client, userId) = await SignInAsync(club.App, email);
        await AddMemberAsync(club.App, club.Org, userId, role);
        return (client.In(club.Org), userId);
    }

    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should work, got {(int)res.StatusCode}: {body}");
        return body.Length == 0 || res.Content.Headers.ContentType?.MediaType?.Contains("json") != true
            ? default
            : JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>Refused for want of <paramref name="permission"/>, saying so.</summary>
    private static async Task RefusedForAsync(HttpResponseMessage res, string permission, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{step} should be refused, got {(int)res.StatusCode}: {body}");
        body.Should().Contain("Your role here does not let you", step);
        body.Should().Contain(permission, step);
    }

    /// <summary>Refused, saying why.</summary>
    private static async Task RefusedSayingAsync(HttpResponseMessage res, string why, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"{step} should be refused, got {(int)res.StatusCode}: {body}");
        body.Should().Contain(why, step);
    }

    private static async Task<Guid> ClubMemberAsync(HttpClient client) =>
        (await Ok(await client.PostAsync("/api/members", Body(new { firstName = "Jean", lastName = "Dupont", email = "jean@example.ch", tier = "Gold", status = "Active" })),
            "add a club member")).GetProperty("id").GetGuid();

    private static async Task<string> ChargeAsync(HttpClient client, Guid memberId) =>
        (await Ok(await client.PostAsync("/api/charges", Body(new { memberId, kind = "Bar", description = "Bar tab", amount = 80 })), "charge the member"))
            .GetProperty("id").GetString()!;

    // The test host's in-memory database cannot run the bulk updates a lead's delete does,
    // so a task stands in for a record a member deletes; the check is the same attribute.
    private static async Task<Guid> TaskAsync(HttpClient client, string title = "Call Marie back") =>
        (await Ok(await client.PostAsync("/api/tasks", Body(new { title })), "create a task")).GetProperty("id").GetGuid();

    private static async Task<Guid> LeadAsync(HttpClient client, string name = "Marie Curie") =>
        (await Ok(await client.PostAsync("/api/leads", Body(new { name, email = $"{name.Split(' ')[0].ToLowerInvariant()}@example.ch", status = "New" })), "create a lead"))
            .GetProperty("id").GetGuid();

    [Fact]
    public async Task TheTeamPageOffersEveryPresetAndPermission()
    {
        using var club = await ClubAsync();

        var catalogue = await Ok(await club.Owner.GetAsync($"/api/organizations/{club.Org}/permission-sets"), "read the permission sets");

        catalogue.GetProperty("presets").EnumerateArray().Select(p => p.GetProperty("key").GetString()).Should().Equal(
            "owner", "manager", "sales_rep", "membership_director", "finance", "front_desk", "read_only_accountant");
        catalogue.GetProperty("presets").EnumerateArray().Single(p => p.GetProperty("key").GetString() == "owner")
            .GetProperty("assignable").GetBoolean().Should().BeFalse("there is one owner, and ownership is transferred, not handed out");
        catalogue.GetProperty("permissions").EnumerateArray().Select(p => p.GetProperty("key").GetString())
            .Should().BeEquivalentTo(MemberPermissions.All.Select(p => p.Key));
        var me = await Ok(await club.Owner.GetAsync($"/api/organizations/{club.Org}"), "read the workspace");
        me.GetProperty("permissionPreset").GetString().Should().Be("owner");
        me.GetProperty("permissions").EnumerateArray().Should().HaveCount(MemberPermissions.All.Count);
    }

    [Fact]
    public async Task FinanceTakesPaymentsAndVoidsButDoesNotWorkTheSalesRecords()
    {
        using var club = await ClubAsync();
        var memberId = await ClubMemberAsync(club.Owner);
        var charge = await ChargeAsync(club.Owner, memberId);
        var (finance, _) = await JoinAsync(club, "finance@pavillon46.ch", "finance");

        await Ok(await finance.PostAsync("/api/payments", Body(new { memberId, amount = 30, method = "Cash", chargeId = charge })), "finance takes a payment");
        var second = await ChargeAsync(finance, memberId);
        await Ok(await finance.PostAsync($"/api/charges/{second}/void", Body(new { reason = "Posted twice" })), "finance voids a charge");
        await Ok(await finance.GetAsync("/api/contacts/export"), "finance exports the contacts");

        await RefusedForAsync(await finance.PostAsync("/api/leads", Body(new { name = "Marie Curie", status = "New" })), MemberPermissions.EditRecords, "finance creating a lead");
        await RefusedForAsync(await finance.PostAsync("/api/members", Body(new { firstName = "Anne", lastName = "Muster", tier = "Gold", status = "Active" })),
            MemberPermissions.ClubMembers, "finance adding a club member");
        await RefusedForAsync(await finance.PostAsync("/api/visits", Body(new { memberId })), MemberPermissions.FrontDesk, "finance checking a member in");
    }

    [Fact]
    public async Task ASalesRepWorksLeadsButCannotDeleteExportOrTouchMoneyAndSettings()
    {
        using var club = await ClubAsync();
        var memberId = await ClubMemberAsync(club.Owner);
        var (rep, _) = await JoinAsync(club, "anna@pavillon46.ch", "sales_rep");

        var lead = await LeadAsync(rep);
        await Ok(await rep.PutAsync($"/api/leads/{lead}", Body(new { status = "Contacted" })), "the rep moves their lead on");
        var task = await TaskAsync(rep);

        await RefusedForAsync(await rep.DeleteAsync($"/api/leads/{lead}"), MemberPermissions.DeleteRecords, "the rep deleting a lead");
        (await club.App.Services.Read(db => db.Leads.Any(l => l.Id == lead))).Should().BeTrue("the lead was not deleted");
        await RefusedForAsync(await rep.DeleteAsync($"/api/tasks/{task}"), MemberPermissions.DeleteRecords, "the rep deleting a task");
        await RefusedForAsync(await rep.PostAsync($"/api/leads/{lead}/merge", Body(new { otherLeadId = Guid.NewGuid() })), MemberPermissions.DeleteRecords, "the rep merging leads");
        await RefusedForAsync(await rep.GetAsync("/api/contacts/export"), MemberPermissions.ExportRecords, "the rep exporting the contacts");
        await RefusedForAsync(await rep.GetAsync("/api/companies/export"), MemberPermissions.ExportRecords, "the rep exporting the companies");
        await RefusedForAsync(await rep.PostAsync("/api/charges", Body(new { memberId, kind = "Bar", description = "Bar tab", amount = 80 })),
            MemberPermissions.ManageMoney, "the rep charging a member");
        await RefusedForAsync(await rep.PostAsync("/api/pipelines", Body(new { name = "Memberships" })), MemberPermissions.ManageSettings, "the rep adding a pipeline");
        await RefusedForAsync(await rep.PostAsync("/api/automations", Body(new { name = "Rule", trigger = "lead.created", enabled = true, actions = Array.Empty<object>() })),
            MemberPermissions.ManageSettings, "the rep adding an automation");

        var addSomeone = await rep.PostAsync($"/api/organizations/{club.Org}/members", Body(new { email = "x@example.ch", role = OrgMemberRole.Member }));
        addSomeone.StatusCode.Should().Be(HttpStatusCode.Forbidden, "managing the team takes members.manage");
    }

    [Fact]
    public async Task TheFrontDeskChecksMembersInButTakesNoMoney()
    {
        using var club = await ClubAsync();
        var memberId = await ClubMemberAsync(club.Owner);
        var charge = await ChargeAsync(club.Owner, memberId);
        var (desk, _) = await JoinAsync(club, "desk@pavillon46.ch", "front_desk");

        await Ok(await desk.PostAsync("/api/visits", Body(new { memberId, venue = "Lounge" })), "the front desk checks a member in");
        await Ok(await desk.GetAsync("/api/charges"), "the front desk reads the charges");

        await RefusedForAsync(await desk.PostAsync("/api/charges", Body(new { memberId, kind = "Bar", description = "Bar tab", amount = 80 })),
            MemberPermissions.ManageMoney, "the front desk charging a member");
        await RefusedForAsync(await desk.PostAsync("/api/payments", Body(new { memberId, amount = 80, method = "Cash", chargeId = charge })),
            MemberPermissions.ManageMoney, "the front desk taking a payment");
        await RefusedForAsync(await desk.PostAsync($"/api/charges/{charge}/void", Body(new { reason = "Comp" })), MemberPermissions.ManageMoney, "the front desk voiding a charge");
        await RefusedForAsync(await desk.PostAsync("/api/leads", Body(new { name = "Marie Curie", status = "New" })), MemberPermissions.EditRecords, "the front desk creating a lead");
    }

    [Fact]
    public async Task AReadOnlyAccountantReadsAndExportsAndChangesNothing()
    {
        using var club = await ClubAsync();
        var memberId = await ClubMemberAsync(club.Owner);
        await LeadAsync(club.Owner);
        var (accountant, accountantId) = await JoinAsync(club, "books@pavillon46.ch", "read_only_accountant");

        (await Ok(await accountant.GetAsync("/api/leads"), "the accountant reads the leads")).ValueKind.Should().NotBe(JsonValueKind.Undefined);
        await Ok(await accountant.GetAsync("/api/contacts/export"), "the accountant exports the contacts");
        await Ok(await accountant.GetAsync("/api/charges"), "the accountant reads the charges");

        foreach (var (res, step) in new[]
        {
            (await accountant.PostAsync("/api/leads", Body(new { name = "Marie Curie", status = "New" })), "creating a lead"),
            (await accountant.PostAsync("/api/payments", Body(new { memberId, amount = 10, method = "Cash" })), "taking a payment"),
            (await accountant.PostAsync("/api/visits", Body(new { memberId })), "checking a member in"),
        })
            res.StatusCode.Should().Be(HttpStatusCode.Forbidden, $"the accountant {step}");

        var membership = await club.App.Services.Read(db => db.OrganizationMembers.Single(m => m.OrganizationId == club.Org && m.UserId == accountantId));
        membership.Role.Should().Be(OrgMemberRole.Viewer, "the role older clients read says read-only too");
        membership.PermissionPreset.Should().Be("read_only_accountant");
        membership.Permissions.Should().BeNull("their permissions are the preset's");
    }

    [Fact]
    public async Task MembersFromBeforePresetsKeepWhatTheirRoleLetThemDo()
    {
        using var club = await ClubAsync();
        var memberId = await ClubMemberAsync(club.Owner);
        var (member, memberUserId) = await OldMemberAsync(club, "marc@pavillon46.ch", OrgMemberRole.Member);
        var (viewer, _) = await OldMemberAsync(club, "vera@pavillon46.ch", OrgMemberRole.Viewer);
        var (manager, _) = await OldMemberAsync(club, "max@pavillon46.ch", OrgMemberRole.Manager);

        // A Member: records, deletes, exports, the club's money and front desk, as before.
        await LeadAsync(member);
        await Ok(await member.DeleteAsync($"/api/tasks/{await TaskAsync(member)}"), "an old Member deletes a task");
        await Ok(await member.DeleteAsync($"/api/members/{await ClubMemberAsync(member)}"), "an old Member deletes a club member");
        await Ok(await member.GetAsync("/api/contacts/export"), "an old Member exports");
        var charge = await ChargeAsync(member, memberId);
        await Ok(await member.PostAsync("/api/visits", Body(new { memberId })), "an old Member checks a member in");
        // And still not what took a manager.
        await RefusedForAsync(await member.PostAsync($"/api/charges/{charge}/void", Body(new { reason = "Comp" })), MemberPermissions.VoidMoney, "an old Member voiding");
        await RefusedForAsync(await member.PostAsync("/api/pipelines", Body(new { name = "Memberships" })), MemberPermissions.ManageSettings, "an old Member adding a pipeline");

        // A Viewer: reads and exports, changes nothing.
        await Ok(await viewer.GetAsync("/api/contacts/export"), "an old Viewer exports");
        (await viewer.PostAsync("/api/leads", Body(new { name = "Marie Curie", status = "New" }))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // A Manager: everything but the owner's own say over permissions.
        await Ok(await manager.PostAsync($"/api/charges/{charge}/void", Body(new { reason = "Comp" })), "an old Manager voids a charge");
        await Ok(await manager.PostAsync("/api/pipelines", Body(new { name = "Memberships" })), "an old Manager adds a pipeline");

        // The Team page shows the old Member as a sales rep with more than the preset.
        var members = await Ok(await club.Owner.GetAsync($"/api/organizations/{club.Org}/members"), "list the members");
        var shown = members.EnumerateArray().Single(m => m.GetProperty("userId").GetGuid() == memberUserId);
        shown.GetProperty("preset").GetString().Should().Be("sales_rep");
        shown.GetProperty("customised").GetBoolean().Should().BeTrue();
        shown.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).Should().BeEquivalentTo(MemberPermissions.LegacyMember);
    }

    [Fact]
    public async Task TheOwnerChangesWhatAMemberMayDo_AndItHoldsFromTheirNextRequest()
    {
        using var club = await ClubAsync();
        var (rep, repId) = await JoinAsync(club, "anna@pavillon46.ch", "sales_rep");
        var task = await TaskAsync(rep);
        await RefusedForAsync(await rep.DeleteAsync($"/api/tasks/{task}"), MemberPermissions.DeleteRecords, "a sales rep deleting");

        var changed = await Ok(await club.Owner.PutAsync($"/api/organizations/{club.Org}/members/{repId}/permissions",
            Body(new { preset = "sales_rep", permissions = new[] { MemberPermissions.EditRecords, MemberPermissions.DeleteRecords } })), "the owner lets Anna delete");
        changed.GetProperty("customised").GetBoolean().Should().BeTrue();
        await Ok(await rep.DeleteAsync($"/api/tasks/{task}"), "Anna deletes, with the same sign-in");

        await Ok(await club.Owner.PutAsync($"/api/organizations/{club.Org}/members/{repId}/permissions", Body(new { preset = "front_desk" })), "the owner moves Anna to the front desk");
        await RefusedForAsync(await rep.PostAsync("/api/leads", Body(new { name = "Marie Curie", status = "New" })), MemberPermissions.EditRecords, "Anna at the front desk creating a lead");
        var membership = await club.App.Services.Read(db => db.OrganizationMembers.Single(m => m.OrganizationId == club.Org && m.UserId == repId));
        membership.PermissionPreset.Should().Be("front_desk");
        membership.Permissions.Should().BeNull("following a preset again clears her own list");

        (await club.Owner.PutAsync($"/api/organizations/{club.Org}/members/{club.OwnerId}/permissions", Body(new { preset = "sales_rep" })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "the owner can always do everything");
        (await club.Owner.PutAsync($"/api/organizations/{club.Org}/members/{repId}/permissions", Body(new { preset = "owner" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "ownership is not a preset to hand out");
    }

    [Fact]
    public async Task OnlyTheOwnerFineTunes_AndNobodyHandsOutMoreThanTheyHold()
    {
        using var club = await ClubAsync();
        var (manager, _) = await JoinAsync(club, "max@pavillon46.ch", "manager");
        var (_, repId) = await JoinAsync(club, "anna@pavillon46.ch", "sales_rep");
        var (_, deskId) = await JoinAsync(club, "desk@pavillon46.ch", "front_desk");
        var (lina, linaId) = await JoinAsync(club, "lina@pavillon46.ch", "sales_rep");
        string Permissions(Guid member) => $"/api/organizations/{club.Org}/members/{member}/permissions";

        // A manager picks presets, but single permissions are the owner's to set.
        await Ok(await manager.PutAsync(Permissions(repId), Body(new { preset = "finance" })), "a manager moves Anna to finance");
        (await manager.PutAsync(Permissions(repId), Body(new { preset = "sales_rep", permissions = new[] { MemberPermissions.EditRecords, MemberPermissions.SeeAllDeals } })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "only the owner sets single permissions");

        // Lina may manage the team, with less than a manager holds.
        await Ok(await club.Owner.PutAsync(Permissions(linaId), Body(new { preset = "sales_rep", permissions = new[] { MemberPermissions.EditRecords, MemberPermissions.ExportRecords, MemberPermissions.ManageMembers } })),
            "the owner lets Lina manage the team");
        await RefusedSayingAsync(await lina.PutAsync(Permissions(deskId), Body(new { preset = "sales_rep" })), "You can only give a member what you can do yourself", "Lina changing the front desk, who can do what she cannot");
        await RefusedSayingAsync(await lina.PutAsync($"/api/organizations/{club.Org}/members/{repId}/role", Body(new { role = OrgMemberRole.Manager })),
            "You can only give a member what you can do yourself", "Lina making Anna a manager by the old role");
        (await lina.DeleteAsync($"/api/organizations/{club.Org}/members/{deskId}")).StatusCode.Should().Be(HttpStatusCode.Forbidden, "nor remove someone who can do more");
        (await lina.PostAsync($"/api/invites/{club.Org}", Body(new { email = "new@example.ch" }))).StatusCode
            .Should().Be(HttpStatusCode.Forbidden, "an invitation makes a Member, who could do more than Lina");

        var (_, newcomerId) = await OldMemberAsync(club, "nina@pavillon46.ch", OrgMemberRole.Viewer);
        await Ok(await lina.PutAsync(Permissions(newcomerId), Body(new { preset = "sales_rep" })), "Lina makes a read-only member a sales rep: both are within what she can do");
    }

    [Fact]
    public async Task APrivateDealShowsToWhoeverMaySeeEveryDeal()
    {
        using var club = await ClubAsync();
        var (rep, repId) = await JoinAsync(club, "anna@pavillon46.ch", "sales_rep");
        var (oldManager, _) = await OldMemberAsync(club, "max@pavillon46.ch", OrgMemberRole.Manager);
        var deal = Guid.NewGuid();
        using (var scope = club.App.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Deals.Add(new Deal
            {
                Id = deal, UserId = club.OwnerId, AssigneeId = club.OwnerId, OrganizationId = club.Org, Name = "Board seat", Value = "50000",
                Currency = "CHF", Probability = 50, IsPrivate = true, CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        (await rep.GetAsync($"/api/deals/{deal}")).StatusCode.Should().Be(HttpStatusCode.NotFound, "a sales rep sees the team's deals but not private ones");
        await Ok(await oldManager.GetAsync($"/api/deals/{deal}"), "a manager from before presets still sees it");

        await Ok(await club.Owner.PutAsync($"/api/organizations/{club.Org}/members/{repId}/permissions",
            Body(new { preset = "sales_rep", permissions = new[] { MemberPermissions.EditRecords, MemberPermissions.SeeAllDeals } })), "the owner lets Anna see every deal");
        await Ok(await rep.GetAsync($"/api/deals/{deal}"), "Anna, allowed to see every deal");
    }
}
