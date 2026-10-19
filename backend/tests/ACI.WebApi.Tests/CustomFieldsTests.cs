using System.Net;
using System.Text.Json;
using ACI.Domain.Enums;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Custom fields, stored on the server: a workspace's definitions, and each record's values.
/// </summary>
/// <remarks>
/// They used to live in each browser, so a field an admin defined existed on one
/// laptop, and a value typed into a lead never reached the team. The bodies here are
/// the ones the client sends (src/app/api/customFields.ts), so these tests pin the
/// actual contract.
/// </remarks>
public class CustomFieldsTests
{
    private static async Task<JsonElement> ReadJson(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<Guid> CreateLeadAsync(HttpClient client) =>
        (await ReadJson(await client.PostAsync("/api/leads", Body(new { name = "Ines Moreau", email = $"{Guid.NewGuid():N}@example.com", status = "New" })),
            "create lead")).GetProperty("id").GetGuid();

    private static Task<HttpResponseMessage> SaveAsync(HttpClient client, Guid lead, object values) =>
        client.PutAsync("/api/custom-fields/values", Body(new { entityType = "lead", recordId = lead, values }));

    private static async Task<JsonElement> ValuesAsync(HttpClient client, Guid lead) =>
        await ReadJson(await client.GetAsync($"/api/custom-fields/values?entityType=lead&recordId={lead}"), "read values");

    private static async Task<(HttpClient Owner, Guid Org, Guid Lead)> WorkspaceAsync(CustomWebApplicationFactory factory)
    {
        var (owner, ownerId) = await SignInAsync(factory, "pierre@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon46");
        owner.In(org);
        return (owner, org, await CreateLeadAsync(owner));
    }

    private static async Task<JsonElement> DefineAsync(HttpClient client, object definition) =>
        await ReadJson(await client.PostAsync("/api/custom-fields", Body(definition)), "define a field");

    [Fact]
    public async Task OwnersAndManagersDefineFieldsAndMembersFillThemIn()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, org, lead) = await WorkspaceAsync(factory);
        var (member, memberId) = await SignInAsync(factory, "sales@example.ch");
        await AddMemberAsync(factory, org, memberId, OrgMemberRole.Member);
        member.In(org);
        var (manager, managerId) = await SignInAsync(factory, "sara@example.ch");
        await AddMemberAsync(factory, org, managerId, OrgMemberRole.Manager);
        manager.In(org);

        var budget = await DefineAsync(owner, new { entityType = "lead", label = "Budget", type = "number" });
        budget.GetProperty("key").GetString().Should().Be("budget");
        budget.GetProperty("order").GetInt32().Should().Be(0);

        var refused = await member.PostAsync("/api/custom-fields", Body(new { entityType = "lead", label = "Mine", type = "text" }));
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Only the owner or a manager can change custom fields");

        var second = await DefineAsync(manager, new { entityType = "lead", label = "Budget", type = "number" });
        second.GetProperty("key").GetString().Should().Be("budget_2", "keys never collide");
        second.GetProperty("order").GetInt32().Should().Be(1);

        (await SaveAsync(member, lead, new { budget = 12000 })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ValuesAsync(owner, lead)).GetProperty("budget").GetDecimal().Should().Be(12000);

        var definitions = await ReadJson(await member.GetAsync("/api/custom-fields?entityType=lead"), "list definitions");
        definitions.EnumerateArray().Select(d => d.GetProperty("label").GetString()).Should().Equal("Budget", "Budget");
    }

    [Fact]
    public async Task EveryValueIsCheckedAgainstItsField()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, lead) = await WorkspaceAsync(factory);
        await DefineAsync(owner, new { entityType = "lead", label = "Budget", type = "number" });
        await DefineAsync(owner, new { entityType = "lead", label = "Channel", type = "select", options = new[] { "Inbound", "Referral" } });
        await DefineAsync(owner, new { entityType = "lead", label = "Tour date", type = "date" });
        await DefineAsync(owner, new { entityType = "lead", label = "Website", type = "url" });
        await DefineAsync(owner, new { entityType = "lead", label = "Has kids", type = "checkbox" });

        async Task Refused(object values, string message)
        {
            var res = await SaveAsync(owner, lead, values);
            res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await res.Content.ReadAsStringAsync()).Should().Contain(message);
        }
        await Refused(new { budget = "a lot" }, "Budget must be a number");
        await Refused(new { channel = "Billboard" }, "Channel must be one of its options");
        await Refused(new { tour_date = "next Tuesday" }, "Tour date must be a date");
        await Refused(new { website = "not a site" }, "Website must be a web address");
        await Refused(new { has_kids = "yes" }, "Has kids must be ticked or not");

        (await SaveAsync(owner, lead, new
        {
            budget = "4500",
            channel = "Referral",
            tour_date = "2026-11-02",
            website = "www.pavillon46.ch",
            has_kids = false,
            deleted_long_ago = "ignored",
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var values = await ValuesAsync(owner, lead);
        values.GetProperty("budget").GetDecimal().Should().Be(4500, "a number typed as text is read as a number");
        values.GetProperty("channel").GetString().Should().Be("Referral");
        values.GetProperty("tour_date").GetString().Should().Be("2026-11-02");
        values.GetProperty("website").GetString().Should().Be("https://www.pavillon46.ch");
        values.GetProperty("has_kids").GetBoolean().Should().BeFalse();
        values.TryGetProperty("deleted_long_ago", out _).Should().BeFalse("a key with no field is not stored");
    }

    [Fact]
    public async Task SavingSomeValuesKeepsTheRestAndEmptyClearsOne()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, lead) = await WorkspaceAsync(factory);
        await DefineAsync(owner, new { entityType = "lead", label = "Budget", type = "number" });
        await DefineAsync(owner, new { entityType = "lead", label = "Notes", type = "text" });

        await SaveAsync(owner, lead, new { budget = 100, notes = "Wants the lake view" });
        await SaveAsync(owner, lead, new { budget = 200 });
        var values = await ValuesAsync(owner, lead);
        values.GetProperty("budget").GetDecimal().Should().Be(200);
        values.GetProperty("notes").GetString().Should().Be("Wants the lake view");

        await SaveAsync(owner, lead, new { notes = "" });
        (await ValuesAsync(owner, lead)).GetProperty("notes").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task ADeletedFieldsValuesStopShowing()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, lead) = await WorkspaceAsync(factory);
        var budget = await DefineAsync(owner, new { entityType = "lead", label = "Budget", type = "number" });
        await DefineAsync(owner, new { entityType = "lead", label = "Notes", type = "text" });
        await SaveAsync(owner, lead, new { budget = 100, notes = "Keep me" });

        (await owner.DeleteAsync($"/api/custom-fields/{budget.GetProperty("id").GetGuid()}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        var values = await ValuesAsync(owner, lead);
        values.TryGetProperty("budget", out _).Should().BeFalse();
        values.GetProperty("notes").GetString().Should().Be("Keep me");
    }

    [Fact]
    public async Task AFieldCanBeRenamedAndRetypedButNotMovedToAnotherKindOfRecord()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, org, _) = await WorkspaceAsync(factory);
        var field = await DefineAsync(owner, new { entityType = "lead", label = "Source", type = "text" });
        var id = field.GetProperty("id").GetGuid();

        var updated = await ReadJson(await owner.PutAsync($"/api/custom-fields/{id}",
            Body(new { label = "Source channel", type = "select", options = new[] { "Inbound", "Event" } })), "update");
        updated.GetProperty("label").GetString().Should().Be("Source channel");
        updated.GetProperty("options").EnumerateArray().Select(o => o.GetString()).Should().Equal("Inbound", "Event");
        updated.GetProperty("key").GetString().Should().Be("source", "the key never changes, so stored values still match");

        (await owner.PutAsync($"/api/custom-fields/{id}", Body(new { entityType = "deal" })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await owner.PutAsync($"/api/custom-fields/{id}", Body(new { type = "select", options = Array.Empty<string>() })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "a dropdown needs options");

        var (member, memberId) = await SignInAsync(factory, "sales@example.ch");
        await AddMemberAsync(factory, org, memberId, OrgMemberRole.Member);
        (await member.In(org).PutAsync($"/api/custom-fields/{id}", Body(new { label = "Mine now" })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EachWorkspaceKeepsItsOwnFieldsAndValues()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, _, lead) = await WorkspaceAsync(factory);
        await DefineAsync(owner, new { entityType = "lead", label = "Budget", type = "number" });
        await SaveAsync(owner, lead, new { budget = 100 });

        var (other, otherId) = await SignInAsync(factory, "other@example.ch");
        other.In(await CreateAsync(factory, otherId, "Another club"));

        (await ReadJson(await other.GetAsync("/api/custom-fields"), "B's definitions")).GetArrayLength().Should().Be(0);
        (await other.GetAsync($"/api/custom-fields/values?entityType=lead&recordId={lead}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound, "A's lead, and so its values, are not B's to read");
        (await SaveAsync(other, lead, new { budget = 1 })).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "A's lead is not in B's workspace");
        (await ValuesAsync(owner, lead)).GetProperty("budget").GetDecimal().Should().Be(100);
    }

    [Fact]
    public async Task AViewerReadsFieldsAndValuesButChangesNothing()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, org, lead) = await WorkspaceAsync(factory);
        await DefineAsync(owner, new { entityType = "lead", label = "Budget", type = "number" });
        await SaveAsync(owner, lead, new { budget = 100 });
        var (viewer, viewerId) = await SignInAsync(factory, "miguel@example.ch");
        await AddMemberAsync(factory, org, viewerId, OrgMemberRole.Viewer);
        viewer.In(org);

        (await ValuesAsync(viewer, lead)).GetProperty("budget").GetDecimal().Should().Be(100);
        (await SaveAsync(viewer, lead, new { budget = 1 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.PostAsync("/api/custom-fields", Body(new { entityType = "lead", label = "X", type = "text" })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
