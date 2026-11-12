using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The Sales and Club modules a workspace can turn off: both on until someone who manages
/// the settings turns one off, one answer from every endpoint of a module that is off,
/// nothing lost when it comes back, and no module turned off while it still has work of its
/// own running that nobody could then see or stop.
/// </summary>
public class WorkspaceModulesTests
{
    private sealed record Team(CustomWebApplicationFactory App, HttpClient Owner, Guid Org);

    private static async Task<Team> TeamAsync(CustomWebApplicationFactory app)
    {
        var (owner, ownerId) = await SignInAsync(app, "olga@pavillon46.ch");
        var org = await CreateAsync(app, ownerId, "Pavillon46");
        owner.In(org);
        return new Team(app, owner, org);
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<JsonElement> Refused(HttpResponseMessage res, HttpStatusCode status, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(status, $"{step}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>The module's one refusal: 403, Module.Off, the module named, a sentence that says how to turn it on.</summary>
    private static async Task ShouldBeOff(HttpResponseMessage res, string module, string step)
    {
        var problem = await Refused(res, HttpStatusCode.Forbidden, step);
        problem.GetProperty("errorCode").GetString().Should().Be("Module.Off", step);
        problem.GetProperty("module").GetString().Should().Be(module, step);
        problem.GetProperty("detail").GetString().Should().Contain("turned off").And.Contain("Settings");
    }

    private static Task<HttpResponseMessage> Set(Team team, object modules) =>
        team.Owner.PutAsync($"/api/organizations/{team.Org}/modules", Body(modules));

    private static async Task<JsonElement> WorkspaceAsListed(Team team) =>
        (await Json(await team.Owner.GetAsync("/api/organizations"), "list workspaces")).EnumerateArray()
            .Single(o => o.GetProperty("id").GetGuid() == team.Org);

    [Fact]
    public async Task BothModulesAreOnForAWorkspaceUntilSomeoneTurnsOneOff()
    {
        using var app = new CustomWebApplicationFactory();
        var team = await TeamAsync(app);

        var listed = await WorkspaceAsListed(team);
        listed.GetProperty("salesEnabled").GetBoolean().Should().BeTrue();
        listed.GetProperty("clubEnabled").GetBoolean().Should().BeTrue();
        await Json(await team.Owner.GetAsync("/api/members"), "the club's members");
        await Json(await team.Owner.GetAsync("/api/leads"), "the sales leads");
    }

    [Fact]
    public async Task TheClubTurnedOff_RefusesEveryClubEndpointTheSameWayForEveryone_AndComesBackAsItWas()
    {
        using var app = new CustomWebApplicationFactory();
        var team = await TeamAsync(app);
        var (viewer, viewerId) = await SignInAsync(app, "viewer@pavillon46.ch");
        await AddMemberAsync(app, team.Org, viewerId, OrgMemberRole.Viewer);
        viewer.In(team.Org);
        var anna = (await Json(await team.Owner.PostAsync("/api/members", Body(new { firstName = "Anna", lastName = "Keller", email = "anna@example.ch", tier = "Gold", status = "Active" })), "add Anna"))
            .GetProperty("id").GetGuid();

        var off = await Json(await Set(team, new { club = false }), "turn the club off");
        off.GetProperty("club").GetBoolean().Should().BeFalse();
        off.GetProperty("sales").GetBoolean().Should().BeTrue();
        (await WorkspaceAsListed(team)).GetProperty("clubEnabled").GetBoolean().Should().BeFalse("the app hides the club from the list it already loads");

        await ShouldBeOff(await team.Owner.GetAsync("/api/members"), "club", "the owner reads members");
        await ShouldBeOff(await team.Owner.PostAsync("/api/members", Body(new { firstName = "Bruno", lastName = "Meier", email = "bruno@example.ch" })), "club", "the owner adds a member");
        await ShouldBeOff(await team.Owner.GetAsync("/api/charges"), "club", "the house accounts");
        await ShouldBeOff(await team.Owner.GetAsync("/api/campaigns"), "club", "campaigns");
        await ShouldBeOff(await team.Owner.GetAsync($"/api/members/{anna}/statement"), "club", "a statement");
        // The same answer for someone who could not have written anyway: nobody hears about
        // their role for a module that is not there.
        await ShouldBeOff(await viewer.PostAsync("/api/members", Body(new { firstName = "Cleo", lastName = "Graf", email = "cleo@example.ch" })), "club", "a viewer writes");

        // Sales and what both modules share go on as before.
        await Json(await team.Owner.GetAsync("/api/leads"), "the leads");
        await Json(await team.Owner.GetAsync("/api/contacts"), "the contacts");
        await Json(await team.Owner.GetAsync("/api/tasks"), "the tasks");

        await Json(await Set(team, new { club = true }), "turn the club back on");
        var members = await Json(await team.Owner.GetAsync("/api/members"), "members again");
        members.EnumerateArray().Select(m => m.GetProperty("id").GetGuid()).Should().Contain(anna, "turning a module off deletes nothing");
    }

    [Fact]
    public async Task SalesTurnedOff_RefusesLeadsDealsAndTheirReports_LeavesThemOutOfSearch_AndRefusesAFileOfLeads()
    {
        using var app = new CustomWebApplicationFactory();
        var team = await TeamAsync(app);
        await Json(await team.Owner.PostAsync("/api/leads", Body(new { name = "Zora Example", email = "zora@example.ch", status = "New" })), "add a lead");
        await Json(await team.Owner.PostAsync("/api/contacts", Body(new { name = "Zora Contact", email = "zora.contact@example.ch" })), "add a contact");

        await Json(await Set(team, new { sales = false }), "turn sales off");

        await ShouldBeOff(await team.Owner.GetAsync("/api/leads"), "sales", "leads");
        await ShouldBeOff(await team.Owner.GetAsync("/api/deals"), "sales", "deals");
        await ShouldBeOff(await team.Owner.GetAsync("/api/pipelines"), "sales", "pipelines");
        await ShouldBeOff(await team.Owner.GetAsync("/api/reporting/dashboard"), "sales", "the sales dashboard");
        await ShouldBeOff(await team.Owner.GetAsync("/api/sequences"), "sales", "sequences");
        await ShouldBeOff(await team.Owner.PostAsync("/api/imports/analyze", Body(new { kind = "lead", fileName = "leads.csv", content = "bmFtZQpab3Jh" })), "sales", "a file of leads");

        var found = await Json(await team.Owner.GetAsync("/api/search?q=Zora"), "search");
        found.GetProperty("leads").GetArrayLength().Should().Be(0, "a lead's page would only say the module is off");
        found.GetProperty("contacts").EnumerateArray().Select(c => c.GetProperty("name").GetString())
            .Should().Contain("Zora Contact", "contacts belong to both modules");

        // The club and the shared records go on.
        await Json(await team.Owner.GetAsync("/api/members"), "members");
        await Json(await team.Owner.GetAsync("/api/contacts"), "contacts");
        (await team.Owner.PostAsync("/api/imports/analyze", Body(new { kind = "contact", fileName = "c.csv", content = "bmFtZQpab3Jh" })))
            .StatusCode.Should().Be(HttpStatusCode.OK, "a file of contacts is for both modules");
    }

    [Fact]
    public async Task OnlyWhoeverManagesTheSettingsTurnsAModuleOnOrOff()
    {
        using var app = new CustomWebApplicationFactory();
        var team = await TeamAsync(app);
        var (rep, repId) = await SignInAsync(app, "rep@pavillon46.ch");
        await AddMemberAsync(app, team.Org, repId, OrgMemberRole.Member);
        rep.In(team.Org);
        var (manager, managerId) = await SignInAsync(app, "manager@pavillon46.ch");
        await AddMemberAsync(app, team.Org, managerId, OrgMemberRole.Manager);
        manager.In(team.Org);

        var refused = await Refused(await rep.PutAsync($"/api/organizations/{team.Org}/modules", Body(new { club = false })), HttpStatusCode.Forbidden, "a sales rep");
        refused.GetProperty("errorCode").GetString().Should().Be("Permission.settings.manage");
        (await WorkspaceAsListed(team)).GetProperty("clubEnabled").GetBoolean().Should().BeTrue();

        await Json(await manager.PutAsync($"/api/organizations/{team.Org}/modules", Body(new { club = false })), "a manager");
        (await WorkspaceAsListed(team)).GetProperty("clubEnabled").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task OneModuleAlwaysStaysOn()
    {
        using var app = new CustomWebApplicationFactory();
        var team = await TeamAsync(app);

        var both = await Refused(await Set(team, new { sales = false, club = false }), HttpStatusCode.BadRequest, "both off at once");
        both.GetProperty("errorCode").GetString().Should().Be("Module.OneStaysOn");

        await Json(await Set(team, new { sales = false }), "sales off");
        (await Refused(await Set(team, new { club = false }), HttpStatusCode.BadRequest, "then the club too"))
            .GetProperty("errorCode").GetString().Should().Be("Module.OneStaysOn");
        var listed = await WorkspaceAsListed(team);
        listed.GetProperty("salesEnabled").GetBoolean().Should().BeFalse();
        listed.GetProperty("clubEnabled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task TheClubIsNotTurnedOffWhileItsBillingRunsOnItsOwnOrACampaignWaitsToGo()
    {
        using var app = new CustomWebApplicationFactory();
        var team = await TeamAsync(app);

        await Json(await team.Owner.PutAsync("/api/hospitality/settings", Body(new { billing = new { automaticBilling = true, remindersEnabled = true } })), "switch automatic billing on");
        var busy = await Refused(await Set(team, new { club = false }), HttpStatusCode.Conflict, "billing still runs");
        busy.GetProperty("errorCode").GetString().Should().Be("Module.StillRunning.Conflict");
        busy.GetProperty("detail").GetString().Should().Contain("automatic billing").And.Contain("payment reminders");
        (await WorkspaceAsListed(team)).GetProperty("clubEnabled").GetBoolean().Should().BeTrue("nothing changed");

        await Json(await team.Owner.PutAsync("/api/hospitality/settings", Body(new { billing = new { automaticBilling = false, remindersEnabled = false } })), "switch it off");
        var campaign = await Json(await team.Owner.PostAsync("/api/campaigns", Body(new
        {
            name = "Gala", channel = "Email", category = "Event", purpose = "service", subject = "The gala", body = "Join us.",
            segment = new { statuses = new[] { "Active" } }, scheduledAtUtc = DateTime.UtcNow.AddDays(3),
        })), "schedule a campaign");
        (await Refused(await Set(team, new { club = false }), HttpStatusCode.Conflict, "a campaign waits to go"))
            .GetProperty("detail").GetString().Should().Contain("campaign");

        await Json(await team.Owner.PutAsync($"/api/campaigns/{campaign.GetProperty("id").GetGuid()}",
            Body(new { scheduledAtUtc = (DateTime?)null, version = campaign.GetProperty("version").GetGuid() })), "take it off the schedule");
        await Json(await Set(team, new { club = false }), "now the club can be turned off");
    }

    [Fact]
    public async Task SalesIsNotTurnedOffWhileLeadsAreStillInOutreachSequences()
    {
        using var app = new CustomWebApplicationFactory();
        var team = await TeamAsync(app);
        var enrolment = new OutreachEnrollment
        {
            Id = Guid.NewGuid(), OrganizationId = team.Org, SequenceId = Guid.NewGuid(), EnrolledByUserId = Guid.NewGuid(),
            TargetType = "lead", TargetId = Guid.NewGuid(), TargetName = "Zora", Status = "active", EnrolledAtUtc = DateTime.UtcNow,
            NextActionAtUtc = DateTime.UtcNow.AddDays(1),
        };
        using (var scope = app.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.EveryWorkspace();
            db.OutreachEnrollments.Add(enrolment);
            await db.SaveChangesAsync();
        }

        (await Refused(await Set(team, new { sales = false }), HttpStatusCode.Conflict, "a lead is still in a sequence"))
            .GetProperty("detail").GetString().Should().Contain("outreach sequence");

        using (var scope = app.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.EveryWorkspace();
            db.OutreachEnrollments.Single(e => e.Id == enrolment.Id).Status = "stopped";
            await db.SaveChangesAsync();
        }
        await Json(await Set(team, new { sales = false }), "the sequence stopped");
    }

    [Fact]
    public async Task WithSalesOff_ALeadFromAWebsiteFormIsKept_ButNoAutomationStartsAnOutreachSequenceForIt()
    {
        // Sales is not turned off while a lead is in a sequence, since nobody could see or stop
        // its emails once the Sequences page was gone. A lead a website form posted afterwards
        // was still put in one by an automation, and those emails started all the same.
        using var app = new CustomWebApplicationFactory();
        var team = await TeamAsync(app);
        var sequenceId = (await Json(await team.Owner.PostAsync("/api/sequences", Body(new
        {
            name = "New lead outreach", status = "active",
            steps = new[] { new { type = "email", dayOffset = 0, subject = "Hello", body = "Hi {{firstName}}" } },
        })), "create a sequence")).GetProperty("id").GetGuid();
        await Json(await team.Owner.PostAsync("/api/automations", Body(new
        {
            name = "Outreach for new leads", trigger = "lead.created", enabled = true,
            actions = new[] { new { type = "add_to_sequence", config = new { sequenceId = sequenceId.ToString() } } },
        })), "add the rule");
        var key = (await Json(await team.Owner.PostAsync($"/api/webhook/organizations/{team.Org}/generate-key", null), "a key for the website form"))
            .GetProperty("apiKey").GetString()!;
        using var form = app.CreateClient();
        form.DefaultRequestHeaders.Add("X-Api-Key", key);

        // With Sales on, the rule puts a new lead in the sequence.
        (await form.PostAsync("/api/webhook/leads", Body(new { name = "Anna Keller", email = "anna@example.ch" })))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await app.Services.Read(db => db.OutreachEnrollments.Count(e => e.OrganizationId == team.Org && e.Status == "active"))).Should().Be(1);
        using (var scope = app.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.EveryWorkspace();
            foreach (var enrolment in db.OutreachEnrollments.Where(e => e.OrganizationId == team.Org)) enrolment.Status = "stopped";
            await db.SaveChangesAsync();
        }
        await Json(await Set(team, new { sales = false }), "turn Sales off once the sequence is stopped");

        (await form.PostAsync("/api/webhook/leads", Body(new { name = "Jean Dupont", email = "jean@example.ch" })))
            .StatusCode.Should().Be(HttpStatusCode.Created, "the enquiry is kept for when Sales is on again");
        (await app.Services.Read(db => db.Leads.Count(l => l.OrganizationId == team.Org))).Should().Be(2);
        (await app.Services.Read(db => db.OutreachEnrollments.Count(e => e.OrganizationId == team.Org && e.Status == "active")))
            .Should().Be(0, "a sequence nobody can see or stop is not started");
    }

    [Fact]
    public async Task PeopleOutsideTheWorkspaceStillReachWhatWasSentToThem_WithTheClubOff()
    {
        using var app = new CustomWebApplicationFactory();
        var team = await TeamAsync(app);
        var token = (await Json(await team.Owner.PutAsync("/api/applications/form", Body(new { isActive = true })), "open the public form"))
            .GetProperty("publicToken").GetString();

        await Json(await Set(team, new { club = false }), "turn the club off");

        using var anon = app.CreateClient();
        await Json(await anon.GetAsync($"/api/public/apply/{token}"), "a visitor opens the form already on the club's website");
        var applied = await anon.PostAsync($"/api/public/apply/{token}", Body(new { firstName = "Theo", lastName = "Kim", email = "theo@example.ch", requestedTier = "Gold" }));
        applied.IsSuccessStatusCode.Should().BeTrue($"an application is kept for the club, not refused: {await applied.Content.ReadAsStringAsync()}");
        (await app.Services.Read(db => db.ClubApplications.Count(a => a.OrganizationId == team.Org))).Should().Be(1);
    }

    [Fact]
    public async Task AnotherWorkspacesModulesCannotBeChanged()
    {
        using var app = new CustomWebApplicationFactory();
        var team = await TeamAsync(app);
        var (stranger, strangerId) = await SignInAsync(app, "stranger@elsewhere.ch");
        var theirs = await CreateAsync(app, strangerId, "Elsewhere");
        stranger.In(theirs);

        (await stranger.PutAsync($"/api/organizations/{team.Org}/modules", Body(new { club = false })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await WorkspaceAsListed(team)).GetProperty("clubEnabled").GetBoolean().Should().BeTrue();
    }
}
