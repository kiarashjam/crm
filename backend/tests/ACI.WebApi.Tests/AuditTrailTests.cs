using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The audit trail records who changed what, from the server's side of every save.
/// </summary>
/// <remarks>
/// <para>
/// Before this, the audit log page read a list kept in each browser, and nothing in
/// the app ever wrote to it: every team saw either nothing or demo rows. The server
/// now writes a line as part of the same save that changes a lead, contact, company,
/// deal or task, so the trail covers the app, the webhook, capture forms and the
/// background jobs alike.
/// </para>
/// <para>
/// These tests pin the three things that make it trustworthy: the words a person
/// reads, the silence on bookkeeping that workers rewrite every minute, and the
/// walls between workspaces.
/// </para>
/// </remarks>
public class AuditTrailTests
{
    private static async Task<JsonElement> ReadJson(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<Guid> CreateLeadAsync(HttpClient client, string name)
    {
        var lead = await ReadJson(await client.PostAsync("/api/leads", Body(new
        {
            name,
            email = $"{Guid.NewGuid():N}@example.com",
            status = "New",
        })), "create lead");
        return lead.GetProperty("id").GetGuid();
    }

    private static async Task<List<(string Action, string Summary, string Actor, string? Label)>> TrailAsync(
        HttpClient client, string query)
    {
        var rows = await ReadJson(await client.GetAsync($"/api/audit?{query}"), "read the audit trail");
        return rows.EnumerateArray().Select(r => (
            r.GetProperty("action").GetString()!,
            r.GetProperty("summary").GetString()!,
            r.GetProperty("actorName").GetString()!,
            r.TryGetProperty("entityLabel", out var l) && l.ValueKind == JsonValueKind.String ? l.GetString() : null)).ToList();
    }

    [Fact]
    public async Task TheServerRecordsALeadsLifeInPlainWords()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "pierre@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        var client = owner.In(org);

        var id = await CreateLeadAsync(client, "Ines Moreau");
        (await client.PutAsync($"/api/leads/{id}", Body(new { name = "Inès Moreau", email = "ines@example.ch" })))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsync($"/api/leads/{id}", Body(new { status = "Contacted" })))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var trail = await TrailAsync(client, $"entityType=lead&entityId={id}");

        trail.Select(t => (t.Action, t.Summary)).Should().Equal(
            ("status_changed", "Status changed from New to Contacted"),
            ("updated", "Updated name and email"),
            ("created", "Lead created"));
        trail.Should().OnlyContain(t => t.Actor == "pierre@example.ch", "every change was made by the signed-in owner");
        trail[0].Label.Should().Be("Inès Moreau");
    }

    [Fact]
    public async Task ADeletedRecordKeepsItsNameInTheTrail()
    {
        // A task, because deleting a lead also clears links with ExecuteUpdateAsync,
        // which the in-memory test database cannot run. The recording is the same.
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "pierre@example.ch");
        var client = owner.In(await CreateAsync(factory, ownerId, "Pavillon46"));

        var task = await ReadJson(await client.PostAsync("/api/tasks", Body(new { title = "Call back Inès" })), "create task");
        var id = task.GetProperty("id").GetGuid();
        (await client.DeleteAsync($"/api/tasks/{id}")).IsSuccessStatusCode.Should().BeTrue();

        (await TrailAsync(client, $"entityType=task&entityId={id}")).Should().Equal(
            ("deleted", "Task deleted", "pierre@example.ch", "Call back Inès"),
            ("created", "Task created", "pierre@example.ch", "Call back Inès"));
    }

    [Fact]
    public async Task AssignmentNamesWhoTheLeadWentTo()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "pierre@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        var (_, saraId) = await SignInAsync(factory, "sara@example.ch");
        await AddMemberAsync(factory, org, saraId, OrgMemberRole.Manager);
        var client = owner.In(org);

        var id = await CreateLeadAsync(client, "Jordan Lee");
        (await client.PutAsync($"/api/leads/{id}/assign", Body(new { assignedToUserId = saraId })))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PutAsync($"/api/leads/{id}/assign", Body(new { assignedToUserId = (Guid?)null })))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var trail = await TrailAsync(client, $"entityId={id}");
        trail.Where(t => t.Action == "assigned").Select(t => t.Summary)
            .Should().Equal("Unassigned", "Assigned to sara@example.ch");
    }

    [Fact]
    public async Task BookkeepingWritesNothingAndAJobIsTheSystem()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "pierre@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        var client = owner.In(org);
        var id = await CreateLeadAsync(client, "Mika Häkkinen");

        // What the follow-up worker and lead scoring rewrite on a timer, with no
        // signed-in user behind them.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var lead = await db.Leads.SingleAsync(l => l.Id == id);
            lead.SlaBreached = true;
            lead.SlaNotifiedAtUtc = DateTime.UtcNow;
            lead.LeadScore = 42;
            lead.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        (await TrailAsync(client, $"entityId={id}")).Select(t => t.Action).Should().Equal("created");

        // A real edit from a job is recorded, and attributed to the system.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var lead = await db.Leads.SingleAsync(l => l.Id == id);
            lead.Industry = "Hospitality";
            await db.SaveChangesAsync();
        }
        var latest = (await TrailAsync(client, $"entityId={id}"))[0];
        latest.Should().Be(("updated", "Updated industry", "System", "Mika Häkkinen"));
    }

    [Fact]
    public async Task StagesWinsAndTasksReadAsAPersonWouldSayThem()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "pierre@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");

        Guid dealId, taskId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var pipeline = new Pipeline { Id = Guid.NewGuid(), OrganizationId = org, Name = "Memberships" };
            var proposal = new DealStage { Id = Guid.NewGuid(), PipelineId = pipeline.Id, Name = "Proposal", DisplayOrder = 1 };
            var won = new DealStage { Id = Guid.NewGuid(), PipelineId = pipeline.Id, Name = "Won", DisplayOrder = 2, IsWon = true };
            var deal = new Deal
            {
                Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Name = "Founding membership",
                Value = "12000", PipelineId = pipeline.Id, DealStageId = proposal.Id, CreatedAtUtc = DateTime.UtcNow,
            };
            var task = new TaskItem { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Title = "Send the contract", CreatedAtUtc = DateTime.UtcNow };
            db.AddRange(pipeline, proposal, won, deal, task);
            await db.SaveChangesAsync();
            (dealId, taskId) = (deal.Id, task.Id);

            deal.DealStageId = won.Id;
            deal.IsWon = true;
            task.Completed = true;
            await db.SaveChangesAsync();
        }

        var client = owner.In(org);
        (await TrailAsync(client, $"entityId={dealId}"))[0].Summary
            .Should().Be("Stage changed from Proposal to Won; marked as won");
        (await TrailAsync(client, $"entityId={taskId}"))[0].Summary.Should().Be("Marked as done");
    }

    [Fact]
    public async Task EachWorkspaceSeesOnlyItsOwnHistory()
    {
        using var factory = new CustomWebApplicationFactory();
        var (pierre, pierreId) = await SignInAsync(factory, "pierre@example.ch");
        var orgA = await CreateAsync(factory, pierreId, "Pavillon46");
        var leadA = await CreateLeadAsync(pierre.In(orgA), "Private to A");

        var (other, otherId) = await SignInAsync(factory, "other@example.ch");
        var orgB = await CreateAsync(factory, otherId, "Another club");

        (await TrailAsync(other.In(orgB), "")).Should().BeEmpty("workspace B has no history yet");
        (await TrailAsync(other.In(orgB), $"entityId={leadA}")).Should().BeEmpty("a record id from A must not open its history");
        // Naming a workspace you don't belong to strips the header, leaving your personal one.
        (await TrailAsync(other.In(orgA), "")).Should().BeEmpty();

        // The owner's personal workspace is separate from their organization's. (In()
        // sets a default header on the client, so take it off to ask as nobody's member.)
        pierre.DefaultRequestHeaders.Remove("X-Organization-Id");
        (await TrailAsync(pierre, "")).Should().BeEmpty();
        // Creating the lead also created its contact and company; all three are A's.
        (await TrailAsync(pierre.In(orgA), "")).Select(t => t.Summary)
            .Should().BeEquivalentTo("Lead created", "Contact created", "Company created");
    }

    [Fact]
    public async Task AClientLineCarriesTheServersActorAndViewersCannotWrite()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "pierre@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        var (viewer, viewerId) = await SignInAsync(factory, "miguel@example.ch");
        await AddMemberAsync(factory, org, viewerId, OrgMemberRole.Viewer);

        var line = await ReadJson(await owner.In(org).PostAsync("/api/audit", Body(new
        {
            entityType = "lead",
            entityLabel = "Alex Turner",
            action = "email_sent",
            summary = "Sent “Quick hello”",
            actorName = "Admin",
        })), "record a client line");
        line.GetProperty("actorName").GetString().Should().Be("pierre@example.ch", "the client cannot choose who it was");

        (await viewer.In(org).PostAsync("/api/audit", Body(new { entityType = "lead", action = "updated", summary = "Edited" })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await TrailAsync(viewer.In(org), "")).Should().ContainSingle(t => t.Summary == "Sent “Quick hello”",
            "a Viewer can still read the trail");
    }

    [Fact]
    public async Task TheTrailRefusesWhatItCannotRecord()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "pierre@example.ch");
        var client = owner.In(await CreateAsync(factory, ownerId, "Pavillon46"));

        (await client.GetAsync("/api/audit?entityType=spaceship")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsync("/api/audit", Body(new { entityType = "lead", action = "teleported", summary = "Gone" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await client.PostAsync("/api/audit", Body(new { entityType = "lead", action = "updated", summary = "  " })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await factory.CreateClient().GetAsync("/api/audit")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
