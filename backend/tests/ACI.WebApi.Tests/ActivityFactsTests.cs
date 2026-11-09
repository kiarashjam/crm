using System.Net;
using System.Text.Json;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// What an activity says beyond its text: when it happened, how a call or meeting went,
/// how long it took, and who reached out.
/// </summary>
/// <remarks>
/// An activity was only ever stamped with the moment it was written down, so a call made
/// in the morning and logged at night counted as an evening call: on the timeline, in
/// "last contacted", and as the lead's first response. And it could not say whether
/// anyone answered, for how long, or who rang whom.
/// </remarks>
public class ActivityFactsTests
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed, got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<(CustomWebApplicationFactory Factory, HttpClient Client, Guid LeadId)> LeadAsync()
    {
        var factory = new CustomWebApplicationFactory();
        var (client, userId) = await SignInAsync(factory, "rep@example.ch");
        client.In(await CreateAsync(factory, userId, "Pavillon46"));
        var lead = await Ok(await client.PostAsync("/api/leads", Body(new { name = "Jean Dupont", email = "jean@example.ch", status = "New" })), "create lead");
        return (factory, client, lead.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task ACallLoggedLateKeepsWhenItHappenedAndHowItWent()
    {
        var (factory, client, leadId) = await LeadAsync();
        using var _ = factory;
        var made = DateTime.UtcNow.AddHours(-6);

        var call = await Ok(await client.PostAsync("/api/activities", Body(new
        {
            type = "call", subject = "Membership call", leadId,
            occurredAtUtc = made, direction = "Outbound", outcome = "No answer", durationMinutes = 0,
        })), "log the call");

        call.GetProperty("occurredAtUtc").GetDateTime().Should().BeCloseTo(made, TimeSpan.FromSeconds(1));
        call.GetProperty("createdAtUtc").GetDateTime().Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        call.GetProperty("direction").GetString().Should().Be("outbound");
        call.GetProperty("outcome").GetString().Should().Be("no_answer", "spelled any way, an outcome is saved as the one word");
        call.GetProperty("durationMinutes").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task WithNoTimeGivenItHappenedWhenItWasLogged()
    {
        var (factory, client, leadId) = await LeadAsync();
        using var _ = factory;

        var note = await Ok(await client.PostAsync("/api/activities", Body(new { type = "note", subject = "Prefers mornings", leadId })), "log a note");

        note.GetProperty("occurredAtUtc").GetDateTime().Should().Be(note.GetProperty("createdAtUtc").GetDateTime());
        note.GetProperty("outcome").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task TheTimelineGoesByWhenThingsHappenedNotWhenTheyWereLogged()
    {
        var (factory, client, leadId) = await LeadAsync();
        using var _ = factory;
        await Ok(await client.PostAsync("/api/activities", Body(new { type = "note", subject = "Written today", leadId })), "log a note");
        await Ok(await client.PostAsync("/api/activities", Body(new
        {
            type = "meeting", subject = "Met at the fair last week", leadId, occurredAtUtc = DateTime.UtcNow.AddDays(-7), outcome = "completed",
        })), "log last week's meeting");

        var timeline = await Ok(await client.GetAsync($"/api/activities/lead/{leadId}"), "timeline");

        timeline.EnumerateArray().Select(a => a.GetProperty("subject").GetString())
            .Should().Equal("Written today", "Met at the fair last week");
    }

    [Theory]
    [InlineData("note", "connected", null, null, "a note has no outcome")]
    [InlineData("call", "completed", null, null, "completed is how a meeting went, not a call")]
    [InlineData("meeting", "no_show", null, "inbound", "a meeting has no direction")]
    [InlineData("email", null, 5, null, "an email has no duration")]
    [InlineData("call", null, 1441, null, "no call lasts more than a day")]
    [InlineData("call", null, -1, null, "no call lasts less than nothing")]
    [InlineData("call", null, null, "sideways", "a call is inbound or outbound")]
    public async Task WhatDoesNotApplyToTheKindIsRefused(string type, string? outcome, int? durationMinutes, string? direction, string because)
    {
        var (factory, client, leadId) = await LeadAsync();
        using var _ = factory;

        var res = await client.PostAsync("/api/activities", Body(new { type, subject = "x", leadId, outcome, durationMinutes, direction }));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest, because);
        (await factory.Services.Read(db => db.Activities.Count())).Should().Be(0);
    }

    [Fact]
    public async Task AnActivityCannotHaveHappenedInTheFuture()
    {
        var (factory, client, leadId) = await LeadAsync();
        using var _ = factory;

        var res = await client.PostAsync("/api/activities", Body(new
        {
            type = "meeting", subject = "Next week", leadId, occurredAtUtc = DateTime.UtcNow.AddDays(7),
        }));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("task", "what is planned belongs in a task");
    }

    [Fact]
    public async Task ABackdatedCallIsTheLeadsFirstResponseAndLastContactAtTheTimeItWasMade()
    {
        var (factory, client, leadId) = await LeadAsync();
        using var _ = factory;
        var created = await factory.Services.Read(db => db.Leads.Single(l => l.Id == leadId).CreatedAtUtc);
        var made = DateTime.UtcNow.AddMinutes(-30);
        // Created just now, so pretend it came in two hours ago, as a web lead would have.
        await factory.Services.Read(db =>
        {
            var lead = db.Leads.Single(l => l.Id == leadId);
            lead.CreatedAtUtc = created.AddHours(-2);
            db.SaveChanges();
            return 0;
        });

        await Ok(await client.PostAsync("/api/activities", Body(new
        {
            type = "call", subject = "Called back", leadId, occurredAtUtc = made, direction = "outbound", outcome = "connected",
        })), "log the call");

        var lead = await factory.Services.Read(db => db.Leads.Single(l => l.Id == leadId));
        lead.FirstResponseAtUtc.Should().BeCloseTo(made, TimeSpan.FromSeconds(1));
        lead.LastContactedAt.Should().BeCloseTo(made, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task AnEarlierCallLoggedLateDoesNotMoveLastContactBack()
    {
        var (factory, client, leadId) = await LeadAsync();
        using var _ = factory;
        await Ok(await client.PostAsync("/api/activities", Body(new { type = "call", subject = "Today", leadId, outcome = "connected" })), "today's call");
        var today = await factory.Services.Read(db => db.Leads.Single(l => l.Id == leadId).LastContactedAt);

        await Ok(await client.PostAsync("/api/activities", Body(new
        {
            type = "call", subject = "Yesterday", leadId, occurredAtUtc = DateTime.UtcNow.AddDays(-1), outcome = "connected",
        })), "yesterday's call, logged after");

        (await factory.Services.Read(db => db.Leads.Single(l => l.Id == leadId).LastContactedAt)).Should().Be(today);
    }

    [Theory]
    [InlineData("email", null)]
    [InlineData("call", "no_answer")]
    public async Task SomethingTheyDidThatNobodyAnsweredIsNotOurFirstResponse(string type, string? outcome)
    {
        var (factory, client, leadId) = await LeadAsync();
        using var _ = factory;

        await Ok(await client.PostAsync("/api/activities", Body(new
        {
            type, subject = "They got in touch", leadId, direction = "inbound", outcome,
        })), "log what they did");

        var lead = await factory.Services.Read(db => db.Leads.Single(l => l.Id == leadId));
        lead.FirstResponseAtUtc.Should().BeNull("the team has still not answered them");
        lead.LastContactedAt.Should().BeNull();
    }

    [Fact]
    public async Task ACallOfTheirsThatWasAnsweredIsOurFirstResponse()
    {
        var (factory, client, leadId) = await LeadAsync();
        using var _ = factory;

        await Ok(await client.PostAsync("/api/activities", Body(new
        {
            type = "call", subject = "They rang, we talked", leadId, direction = "inbound", outcome = "connected",
        })), "log their call");

        (await factory.Services.Read(db => db.Leads.Single(l => l.Id == leadId).FirstResponseAtUtc)).Should().NotBeNull();
    }

    [Fact]
    public async Task AnEditCanChangeAndClearTheFacts()
    {
        var (factory, client, leadId) = await LeadAsync();
        using var _ = factory;
        var call = await Ok(await client.PostAsync("/api/activities", Body(new
        {
            type = "call", subject = "Call", leadId, direction = "outbound", outcome = "no_answer", durationMinutes = 2,
        })), "log the call");
        var id = call.GetProperty("id").GetGuid();
        var earlier = DateTime.UtcNow.AddHours(-3);

        var edited = await Ok(await client.PutAsync($"/api/activities/{id}", Body(new
        {
            outcome = "connected", clearDuration = true, occurredAtUtc = earlier,
        })), "edit it");

        edited.GetProperty("outcome").GetString().Should().Be("connected");
        edited.GetProperty("durationMinutes").ValueKind.Should().Be(JsonValueKind.Null);
        edited.GetProperty("direction").GetString().Should().Be("outbound", "it was not part of the edit");
        edited.GetProperty("occurredAtUtc").GetDateTime().Should().BeCloseTo(earlier, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ACallChangedIntoANoteKeepsNothingANoteCannotSay()
    {
        var (factory, client, leadId) = await LeadAsync();
        using var _ = factory;
        var call = await Ok(await client.PostAsync("/api/activities", Body(new
        {
            type = "call", subject = "Call", leadId, direction = "outbound", outcome = "connected", durationMinutes = 9,
        })), "log the call");
        var id = call.GetProperty("id").GetGuid();

        var note = await Ok(await client.PutAsync($"/api/activities/{id}", Body(new { type = "note" })), "make it a note");

        note.GetProperty("outcome").ValueKind.Should().Be(JsonValueKind.Null);
        note.GetProperty("durationMinutes").ValueKind.Should().Be(JsonValueKind.Null);
        note.GetProperty("direction").ValueKind.Should().Be(JsonValueKind.Null);
        (await client.PutAsync($"/api/activities/{id}", Body(new { outcome = "busy" }))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "a note has no outcome to set");
        (await client.PutAsync($"/api/activities/{id}", Body(new { occurredAtUtc = DateTime.UtcNow.AddDays(2) }))).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "an edit cannot date it in the future either");
    }

    [Fact]
    public async Task ADealsLastActivityIsWhenTheLatestThingHappened()
    {
        using var factory = new CustomWebApplicationFactory();
        var (client, userId) = await SignInAsync(factory, "rep@example.ch");
        client.In(await CreateAsync(factory, userId, "Pavillon46"));
        var deal = await Ok(await client.PostAsync("/api/deals", Body(new { name = "Corporate membership", amount = 4800, currency = "CHF" })), "create deal");
        var dealId = deal.GetProperty("id").GetGuid();
        var made = DateTime.UtcNow.AddDays(-12);

        await Ok(await client.PostAsync("/api/activities", Body(new
        {
            type = "call", subject = "The call nobody wrote down", dealId, occurredAtUtc = made, outcome = "connected",
        })), "log the old call today");

        var read = await Ok(await client.GetAsync($"/api/deals/{dealId}"), "read the deal");
        read.GetProperty("lastActivityAtUtc").GetDateTime().Should().BeCloseTo(made, TimeSpan.FromSeconds(1),
            "the deal has not been worked on since that call, whenever it was written down");
    }
}
