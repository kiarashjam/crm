using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A pipeline edit changes the steps it sends and keeps the rest.
/// </summary>
/// <remarks>
/// PUT /api/leads/{id} used to replace the whole pipeline with what the browser sent. A
/// rep with the lead open from before an e-signed contract went out recorded "meeting
/// attended", and the contract steps the e-signature had written were gone: the lead read
/// as not yet at the contract, and its status went backwards.
/// </remarks>
public class LeadPipelinePatchTests
{
    private static async Task<(CustomWebApplicationFactory Factory, HttpClient Client, Guid LeadId)> LeadAsync()
    {
        var factory = new CustomWebApplicationFactory();
        var (client, userId) = await SignInAsync(factory, "rep@example.ch");
        var org = await CreateAsync(factory, userId, "Club Nautique");
        client.In(org);
        var res = await client.PostAsync("/api/leads", Body(new { name = "Jean Muster", email = "jean@example.ch", source = "Website", status = "New" }));
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue(body);
        using var doc = JsonDocument.Parse(body);
        return (factory, client, doc.RootElement.GetProperty("id").GetGuid());
    }

    private static Task<HttpResponseMessage> Patch(HttpClient client, Guid id, string pipelineState) =>
        client.PutAsync($"/api/leads/{id}", Body(new { pipelineState }));

    // What was saved, in whichever workspace: outside a request the app reads none.
    private static async Task<JsonElement> StoredPipeline(CustomWebApplicationFactory factory, Guid id)
    {
        var lead = await factory.Services.Read(db => db.Leads.AsNoTracking().Single(l => l.Id == id));
        using var doc = JsonDocument.Parse(lead.PipelineState ?? "{}");
        return doc.RootElement.Clone();
    }

    [Fact]
    public async Task AnEditFromAPageOpenedBeforeTheContractWentOutKeepsTheContractSteps()
    {
        var (factory, client, id) = await LeadAsync();
        using var _ = factory;
        (await Patch(client, id, """{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled","meetingDate":"2026-11-20"}""")).EnsureSuccessStatusCode();
        // What the contract flow writes when the agreement goes out for signature.
        (await Patch(client, id, """{"contractStatus":"yes","contractSentDate":"2026-11-21","contractSigned":"pending"}""")).EnsureSuccessStatusCode();

        // The rep's tracker, opened before that, sends the step they changed.
        var res = await Patch(client, id, """{"meetingAttended":true}""");

        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());
        var stored = await StoredPipeline(factory, id);
        stored.GetProperty("contractStatus").GetString().Should().Be("yes");
        stored.GetProperty("contractSigned").GetString().Should().Be("pending");
        stored.GetProperty("meetingAttended").GetBoolean().Should().BeTrue();
        stored.GetProperty("meetingDate").GetString().Should().Be("2026-11-20");
        // And the answer carries the whole merged pipeline, which is what the page shows next.
        using var answer = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        answer.RootElement.GetProperty("pipelineState").GetString().Should().Contain("contractSigned");
    }

    [Fact]
    public async Task AStepSentAsNullIsTakenOff()
    {
        var (factory, client, id) = await LeadAsync();
        using var _ = factory;
        (await Patch(client, id, """{"outreachStatus":"contacted","contactOutcome":"meeting_scheduled","meetingDate":"2026-11-20"}""")).EnsureSuccessStatusCode();

        (await Patch(client, id, """{"meetingDate":null,"contactOutcome":"follow_up"}""")).EnsureSuccessStatusCode();

        var stored = await StoredPipeline(factory, id);
        stored.TryGetProperty("meetingDate", out var removed).Should().BeFalse(removed.ToString());
        stored.GetProperty("contactOutcome").GetString().Should().Be("follow_up");
        stored.GetProperty("outreachStatus").GetString().Should().Be("contacted");
    }

    [Fact]
    public async Task AnEmptyPipelineStillClearsItAll()
    {
        var (factory, client, id) = await LeadAsync();
        using var _ = factory;
        (await Patch(client, id, """{"outreachStatus":"contacted"}""")).EnsureSuccessStatusCode();

        (await Patch(client, id, "")).EnsureSuccessStatusCode();

        (await factory.Services.Read(db => db.Leads.AsNoTracking().Single(l => l.Id == id)))
            .PipelineState.Should().BeNull();
    }

    [Fact]
    public async Task AChangeThatIsNotAnObjectIsRefusedAndNothingIsLost()
    {
        var (factory, client, id) = await LeadAsync();
        using var _ = factory;
        (await Patch(client, id, """{"outreachStatus":"contacted"}""")).EnsureSuccessStatusCode();

        var res = await Patch(client, id, "[\"contacted\"]");

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await StoredPipeline(factory, id)).GetProperty("outreachStatus").GetString().Should().Be("contacted");
    }
}
