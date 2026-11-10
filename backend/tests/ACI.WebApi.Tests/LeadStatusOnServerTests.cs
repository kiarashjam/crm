using System.Net;
using System.Text.Json;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A lead's status follows its pipeline on the server, whichever code path wrote the
/// pipeline, over real HTTP.
/// </summary>
/// <remarks>
/// The status used to be worked out only in the browser, which then sent it with the
/// pipeline. So an e-signed contract, a merge, or any API caller moved the pipeline
/// and left the status, the filters, the stats, the score and the
/// lead.status_changed automations where they were.
/// </remarks>
public class LeadStatusOnServerTests
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>A workspace with the statuses every organisation is seeded with.</summary>
    private static async Task<(HttpClient Client, Guid UserId, Guid OrgId)> WorkspaceAsync(CustomWebApplicationFactory factory)
    {
        var (client, userId) = await SignInAsync(factory, "owner@example.ch");
        var orgId = await CreateAsync(factory, userId, "Pavillon46");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var names = LeadStatusVocabulary.Default;
            for (var i = 0; i < names.Length; i++)
                db.LeadStatuses.Add(new LeadStatus { Id = Guid.NewGuid(), OrganizationId = orgId, Name = names[i], DisplayOrder = i });
            await db.SaveChangesAsync();
        }
        client.In(orgId);
        return (client, userId, orgId);
    }

    private static async Task<Guid> NewLeadAsync(HttpClient client, string email = "jean@example.ch") =>
        (await Ok(await client.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email, status = "New" })), "create lead"))
            .GetProperty("id").GetGuid();

    private static async Task<string?> StatusOfAsync(HttpClient client, Guid leadId) =>
        (await Ok(await client.GetAsync($"/api/leads/{leadId}"), "read lead")).GetProperty("status").GetString();

    [Fact]
    public async Task APipelineSavedWithoutAStatusMovesTheStatus()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, orgId) = await WorkspaceAsync(factory);
        var leadId = await NewLeadAsync(client);

        var saved = await Ok(await client.PutAsync($"/api/leads/{leadId}", Body(new
        {
            pipelineState = """{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled","meetingAttended":true}""",
        })), "save the pipeline");

        saved.GetProperty("status").GetString().Should().Be("Connected");
        var connectedId = await factory.Services.Read(db => db.LeadStatuses.Single(s => s.OrganizationId == orgId && s.Name == "Connected").Id);
        saved.GetProperty("leadStatusId").GetGuid().Should().Be(connectedId);

        var history = await Ok(await client.GetAsync($"/api/lead-ops/history/{leadId}"), "status history");
        history.EnumerateArray().Should().Contain(e => e.GetProperty("toStatus").GetString() == "Connected");

        var timeline = await Ok(await client.GetAsync($"/api/activities/lead/{leadId}"), "timeline");
        timeline.EnumerateArray().Should().Contain(a =>
            a.GetProperty("subject").GetString() == "Status auto-updated to Connected"
            && a.GetProperty("body").GetString()!.Contains("Attended the meeting"));
    }

    [Fact]
    public async Task AnESignedContractMovesTheStatusAndRunsTheStatusAutomations()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await WorkspaceAsync(factory);
        await Ok(await client.PostAsync("/api/automations", Body(new
        {
            name = "On status change", trigger = "lead.status_changed", enabled = true,
            actions = new[] { new { type = "create_task", config = new { title = "Follow the status change", dueInDays = "1" } } },
        })), "add rule");
        var leadId = await NewLeadAsync(client);

        var draft = await Ok(await client.PostAsync("/api/contracts/draft", Body(new { leadId, title = "Membership Agreement" })), "draft");
        var contractId = draft.GetProperty("id").GetString();
        await Ok(await client.PutAsync($"/api/contracts/{contractId}", Body(new
        {
            title = "Membership Agreement",
            body = "MEMBERSHIP AGREEMENT\n\nCHF 1'450.00 per year.\n",
            counterpartyName = "Jean Dupont",
            counterpartyEmail = "jean@example.ch",
        })), "edit");
        var sent = await Ok(await client.PostAsync($"/api/contracts/{contractId}/send", content: null), "send");

        (await StatusOfAsync(client, leadId)).Should().Be("Awaiting Signature",
            "sending is recorded on the pipeline, and nobody has the lead open in a browser");
        var tasks = await Ok(await client.GetAsync($"/api/tasks/by-lead/{leadId}"), "tasks");
        tasks.GetArrayLength().Should().Be(1, "the workspace's lead.status_changed automation ran");

        // The counterparty signs at night, then the club countersigns.
        var token = sent.GetProperty("signingUrl").GetString()!.Split("/sign/").Last();
        using var anon = factory.CreateClient();
        await Ok(await anon.PostAsync($"/api/public/contracts/{token}/sign", Body(new { signatureName = "Jean Dupont", agreed = true })), "sign");
        await Ok(await client.PostAsync($"/api/contracts/{contractId}/countersign", Body(new { signatureName = "Owner", agreed = true })), "countersign");

        (await StatusOfAsync(client, leadId)).Should().Be("Signed");
    }

    [Fact]
    public async Task ADeclinedContractMarksTheLeadLostAndCreditsNobodyHere()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await WorkspaceAsync(factory);
        var leadId = await NewLeadAsync(client);
        var draft = await Ok(await client.PostAsync("/api/contracts/draft", Body(new { leadId, title = "Membership Agreement" })), "draft");
        var contractId = draft.GetProperty("id").GetString();
        await Ok(await client.PutAsync($"/api/contracts/{contractId}", Body(new
        {
            title = "Membership Agreement", body = "MEMBERSHIP AGREEMENT\n",
            counterpartyName = "Jean Dupont", counterpartyEmail = "jean@example.ch",
        })), "edit");
        var sent = await Ok(await client.PostAsync($"/api/contracts/{contractId}/send", content: null), "send");
        var token = sent.GetProperty("signingUrl").GetString()!.Split("/sign/").Last();

        using var anon = factory.CreateClient();
        await Ok(await anon.PostAsync($"/api/public/contracts/{token}/decline", Body(new { reason = "Too expensive" })), "decline");

        (await StatusOfAsync(client, leadId)).Should().Be(LeadStatusVocabulary.Lost);
        var lastMove = await factory.Services.Read(db => db.LeadStatusEvents
            .Where(e => e.LeadId == leadId).OrderByDescending(e => e.ChangedAtUtc).First());
        lastMove.ToStatus.Should().Be(LeadStatusVocabulary.Lost);
        lastMove.ChangedByUserId.Should().BeNull("the counterparty declined; nobody in the workspace changed it");
    }

    [Fact]
    public async Task MergingInALeadThatHadProgressBringsItsStatus()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await WorkspaceAsync(factory);
        var keep = await NewLeadAsync(client, "jean@example.ch");
        var drop = (await Ok(await client.PostAsync("/api/leads", Body(new
        {
            name = "Jean Dupont", email = "jean.dupont@example.ch", status = "New",
            pipelineState = """{"contractStatus":"yes","contractSigned":"yes"}""",
        })), "the duplicate")).GetProperty("id").GetGuid();

        var merged = await client.PostAsync($"/api/leads/{keep}/merge", Body(new { otherLeadId = drop }));
        merged.StatusCode.Should().Be(HttpStatusCode.NoContent, await merged.Content.ReadAsStringAsync());

        (await StatusOfAsync(client, keep)).Should().Be("Signed");
    }

    [Fact]
    public async Task ALeadCreatedWithRecordedWorkStartsAtTheStatusItImplies()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await WorkspaceAsync(factory);

        var created = await Ok(await client.PostAsync("/api/leads", Body(new
        {
            name = "Jean Dupont", email = "jean@example.ch", status = "New",
            pipelineState = """{"outreachStatus":"attempted_no_answer"}""",
        })), "create with a pipeline");

        created.GetProperty("status").GetString().Should().Be("Attempted Contact");
    }

    [Fact]
    public async Task APipelineTheBrowserCanReadIsSavedRatherThanFailingTheRequest()
    {
        // JSON.parse accepts an escaped half of a surrogate pair, so the browser reads
        // this pipeline as signed. The server reads every pipeline it is sent now, and
        // its reader threw on it, so the save was refused as an invalid request.
        using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await WorkspaceAsync(factory);
        var leadId = await NewLeadAsync(client);

        var saved = await Ok(await client.PutAsync($"/api/leads/{leadId}", Body(new
        {
            pipelineState = """{"\ud83d":true,"contractSigned":"yes"}""",
        })), "save the pipeline");

        saved.GetProperty("status").GetString().Should().Be("Signed");
    }

    [Fact]
    public async Task APersonalWorkspaceHasNoStatusListSoTheStatusStays()
    {
        // Nothing to choose from, and the browser does not derive there either:
        // inventing a status is the one thing the rule never does.
        using var factory = new CustomWebApplicationFactory();
        var (client, _) = await SignInAsync(factory, "solo@example.ch");
        var leadId = await NewLeadAsync(client);

        var saved = await Ok(await client.PutAsync($"/api/leads/{leadId}", Body(new
        {
            pipelineState = """{"contractSigned":"yes"}""",
        })), "save the pipeline");

        saved.GetProperty("status").GetString().Should().Be("New");
        saved.GetProperty("pipelineState").GetString().Should().Contain("contractSigned");
    }
}
