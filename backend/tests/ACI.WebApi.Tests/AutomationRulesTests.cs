using System.Net;
using System.Text.Json;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Workspace automations: who may change them, what their actions may point at, and
/// what runs when a lead is created.
/// </summary>
public class AutomationRulesTests
{
    /// <summary>The app with a mail server that remembers what it was asked to send.</summary>
    private sealed class WithMailServer : CustomWebApplicationFactory
    {
        public readonly List<string> SentTo = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                var sender = new Mock<IEmailSender>();
                sender.Setup(s => s.SendMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Callback((string to, string _, string _, string _, CancellationToken _) => SentTo.Add(to))
                    .ReturnsAsync(true);
                services.AddSingleton(sender.Object);
            });
        }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static object Rule(string type, object config, string trigger = "lead.created") =>
        new { name = "On new lead", trigger, enabled = true, actions = new[] { new { type, config } } };

    private static Task<HttpResponseMessage> AddLeadAsync(HttpClient client, string email = "jean@example.ch") =>
        client.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email, status = "New" }));

    [Fact]
    public async Task OnlyTheOwnerOrAManagerChangesRules_EveryoneSeesThem()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        var (member, memberId) = await SignInAsync(factory, "member@example.ch");
        await AddMemberAsync(factory, org, memberId, OrgMemberRole.Member);
        member.In(org);
        var (manager, managerId) = await SignInAsync(factory, "manager@example.ch");
        await AddMemberAsync(factory, org, managerId, OrgMemberRole.Manager);
        manager.In(org);

        var refused = await member.PostAsync("/api/automations", Body(Rule("notify", new { message = "hi" })));
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Only the owner or a manager can change automations.");

        var rule = await Json(await manager.PostAsync("/api/automations", Body(Rule("notify", new { message = "hi" }))), "manager adds");
        var id = rule.GetProperty("id").GetGuid();
        (await member.PutAsync($"/api/automations/{id}", Body(new { enabled = false }))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.DeleteAsync($"/api/automations/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Json(await member.GetAsync("/api/automations"), "member lists")).GetArrayLength().Should().Be(1);
        (await owner.DeleteAsync($"/api/automations/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AnActionMustPointInsideTheWorkspace()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        var (_, outsiderId) = await SignInAsync(factory, "outsider@example.ch");

        var outsider = await owner.PostAsync("/api/automations", Body(Rule("assign", new { userId = outsiderId.ToString() })));
        outsider.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await outsider.Content.ReadAsStringAsync()).Should().Contain("Assign to someone in this workspace.");
        // What the rule builder used to save: a name, which the engine never read.
        (await owner.PostAsync("/api/automations", Body(Rule("assign", new { assigneeName = "Kia" })))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.PostAsync("/api/automations", Body(Rule("add_to_sequence", new { sequence = "Welcome" })))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.PostAsync("/api/automations", Body(Rule("send_email", new { subject = "Hi" })))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await Json(await owner.PostAsync("/api/automations", Body(Rule("assign", new { userId = ownerId.ToString() }))), "assign to a member");
    }

    [Fact]
    public async Task ANewLeadRunsTheWorkspacesRules()
    {
        await using var factory = new WithMailServer();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        var (_, repId) = await SignInAsync(factory, "rep@example.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        await Json(await owner.PostAsync("/api/automations", Body(new
        {
            name = "Welcome", trigger = "lead.created", enabled = true,
            actions = new object[]
            {
                new { type = "create_task", config = new { title = "Call them", dueInDays = "1" } },
                new { type = "assign", config = new { userId = repId.ToString() } },
                new { type = "send_email", config = new { subject = "Welcome", body = "Hi {{firstName}}" } },
            },
        })), "add rule");

        var lead = await Json(await AddLeadAsync(owner), "add lead");
        var leadId = lead.GetProperty("id").GetGuid();

        var task = (await Json(await owner.GetAsync($"/api/tasks/by-lead/{leadId}"), "tasks")).EnumerateArray().Single();
        task.GetProperty("title").GetString().Should().Be("Call them");
        // The rule assigns the lead to the rep, so the rep is the one who has to call
        // them — not the owner who happened to create the lead.
        task.GetProperty("assigneeId").GetGuid().Should().Be(repId);
        (await factory.Services.Read(db => db.Leads.Single(l => l.Id == leadId).AssignedToUserId)).Should().Be(repId);
        factory.SentTo.Should().Equal("jean@example.ch");
    }

    [Fact]
    public async Task ARulesTaskGoesToWhoeverWorksTheLead_NotToWhoeverSetItOff()
    {
        await using var factory = new WithMailServer();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        var (_, repId) = await SignInAsync(factory, "rep@example.ch");
        await AddMemberAsync(factory, org, repId, OrgMemberRole.Member);
        await Json(await owner.PostAsync("/api/automations", Body(Rule(
            "create_task", new { title = "Follow up on the new status", dueInDays = "0" }, trigger: "lead.status_changed"))), "add rule");
        var leadId = (await Json(await AddLeadAsync(owner), "add lead")).GetProperty("id").GetGuid();
        await Json(await owner.PutAsync($"/api/leads/{leadId}/assign", Body(new { assignedToUserId = repId })), "assign to the rep");

        await Json(await owner.PutAsync($"/api/leads/{leadId}", Body(new { status = "Contacted" })), "owner moves the lead on");

        var task = (await Json(await owner.GetAsync($"/api/tasks/by-lead/{leadId}"), "tasks")).EnumerateArray().Single();
        task.GetProperty("assigneeId").GetGuid().Should().Be(repId);
    }

    [Fact]
    public async Task ARulesTaskForAnUnassignedLeadStaysWithWhoeverSetItOff()
    {
        await using var factory = new WithMailServer();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        owner.In(await CreateAsync(factory, ownerId, "Pavillon46"));
        await Json(await owner.PostAsync("/api/automations", Body(Rule("create_task", new { title = "Call them", dueInDays = "1" }))), "add rule");

        var leadId = (await Json(await AddLeadAsync(owner), "add lead")).GetProperty("id").GetGuid();

        var task = (await Json(await owner.GetAsync($"/api/tasks/by-lead/{leadId}"), "tasks")).EnumerateArray().Single();
        task.GetProperty("assigneeId").ValueKind.Should().Be(JsonValueKind.Null);
        (await factory.Services.Read(db => db.TaskItems.Single(t => t.LeadId == leadId).UserId)).Should().Be(ownerId);
    }

    [Fact]
    public async Task RulesLeftWithNoOrganizationRunForNobody()
    {
        // Before rules needed an organization, a personal user's rule landed in the shared
        // "no organization" bucket and ran on every other personal user's new leads,
        // mailing their contacts under their name.
        await using var factory = new WithMailServer();
        var (stranger, strangerId) = await SignInAsync(factory, "stranger@example.ch");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.AutomationRules.Add(new AutomationRule
            {
                Id = Guid.NewGuid(), OrganizationId = null, OwnerUserId = strangerId, Name = "Old rule", Enabled = true,
                Trigger = "lead.created", ActionsJson = """[{"type":"send_email","config":{"subject":"Hi","body":"Hello"}}]""",
                CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        var (someoneElse, _) = await SignInAsync(factory, "someone@example.ch");

        (await AddLeadAsync(someoneElse, "their.contact@example.ch")).IsSuccessStatusCode.Should().BeTrue();

        factory.SentTo.Should().BeEmpty();
    }
}
