using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;
using App = ACI.WebApi.Tests.AutomationRunsTests.App;

namespace ACI.WebApi.Tests;

/// <summary>
/// Rules that test a record's tags and custom fields (a lead's and a deal's own, the member's
/// for the club's records), and wait for its custom dates.
/// </summary>
public class AutomationFlexConditionsTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<HttpClient> WorkspaceAsync(App app)
    {
        var (owner, ownerId) = await SignInAsync(app, "owner@pavillon46.ch");
        owner.In(await CreateAsync(app, ownerId, "Pavillon46"));
        return owner;
    }

    private static async Task<Guid> Post(HttpClient client, string url, object body) =>
        (await Json(await client.PostAsync(url, Body(body)), $"POST {url}")).GetProperty("id").GetGuid();

    private static async Task Values(HttpClient client, string type, Guid id, object values) =>
        (await client.PutAsync("/api/custom-fields/values", Body(new { entityType = type, recordId = id, values }))).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task Tags(HttpClient client, string type, Guid id, params Guid[] tags) =>
        await Json(await client.PutAsync("/api/tags/records", Body(new { entityType = type, recordId = id, tagIds = tags })), "tag the record");

    private static object Task(string title) => new { type = "create_task", config = new { title, dueInDays = "1" } };

    private static object Condition(string field, string op, string? value = null) => new { field, op, value };

    private static object All(params object[] conditions) => new { match = "all", conditions };

    private static async Task<Guid> DealAsync(HttpClient owner, string name, object values, params Guid[] tags)
    {
        var id = await Post(owner, "/api/deals", new { name, value = "1000", stage = "Qualification" });
        await Values(owner, "deal", id, values);
        if (tags.Length > 0) await Tags(owner, "deal", id, tags);
        return id;
    }

    [Fact]
    public async Task ADealRuleRunsOnlyForTheDealsWithTheTagAndValuesItAsksFor()
    {
        await using var app = new App();
        var owner = await WorkspaceAsync(app);
        await Post(owner, "/api/custom-fields", new { entityType = "deal", label = "Tier", type = "select", options = new[] { "Gold", "Silver" } });
        await Post(owner, "/api/custom-fields", new { entityType = "deal", label = "Seats", type = "number" });
        await Post(owner, "/api/custom-fields", new { entityType = "deal", label = "Interests", type = "multiselect", options = new[] { "Golf", "Wine" } });
        await Post(owner, "/api/custom-fields", new { entityType = "deal", label = "Board seat", type = "checkbox" });
        var vip = await Post(owner, "/api/tags", new { name = "VIP" });
        await Json(await owner.PostAsync("/api/automations", Body(new
        {
            name = "VIP gold deals", trigger = "deal.stage_changed",
            conditions = All(
                Condition("tags", "is", vip.ToString()),
                Condition("cf:tier", "is", "Gold"),
                Condition("cf:seats", "gte", "2"),
                Condition("cf:interests", "is", "Golf"),
                Condition("cf:board_seat", "is_false")),
            actions = new[] { Task("Call the VIP") },
        })), "add the rule");
        await Json(await owner.PostAsync("/api/automations", Body(new
        {
            name = "Untagged deals", trigger = "deal.stage_changed",
            conditions = All(Condition("tags", "is_empty")),
            actions = new[] { Task("Tag the deal") },
        })), "add the second rule");

        var everything = await DealAsync(owner, "Everything", new { tier = "Gold", seats = 3, interests = new[] { "Wine", "Golf" } }, vip);
        var untagged = await DealAsync(owner, "No tag", new { tier = "Gold", seats = 3, interests = new[] { "Golf" } });
        var oneSeat = await DealAsync(owner, "One seat", new { tier = "Gold", seats = 1, interests = new[] { "Golf" } }, vip);
        var onTheBoard = await DealAsync(owner, "On the board", new { tier = "Gold", seats = 3, interests = new[] { "Golf" }, board_seat = true }, vip);
        foreach (var id in new[] { everything, untagged, oneSeat, onTheBoard })
            await Json(await owner.PutAsync($"/api/deals/{id}", Body(new { stage = "Proposal" })), "move the deal");

        Task<List<string>> TitlesAsync(Guid dealId) => app.Services.Read(db => db.TaskItems.Where(t => t.DealId == dealId).Select(t => t.Title).ToList());
        (await TitlesAsync(everything)).Should().Equal("Call the VIP");
        (await TitlesAsync(untagged)).Should().Equal(new[] { "Tag the deal" }, "it does not have the tag, and has none at all");
        (await TitlesAsync(oneSeat)).Should().BeEmpty("one seat is fewer than two");
        (await TitlesAsync(onTheBoard)).Should().BeEmpty("its board seat box is ticked");
    }

    [Fact]
    public async Task AClubRuleTestsTheMembersTagsAndFields()
    {
        await using var app = new App();
        var owner = await WorkspaceAsync(app);
        await Post(owner, "/api/custom-fields", new { entityType = "member", label = "Handicap", type = "number" });
        var board = await Post(owner, "/api/tags", new { name = "Board" });
        await Json(await owner.PostAsync("/api/automations", Body(new
        {
            name = "A board golfer booked", trigger = "reservation.created",
            conditions = All(Condition("tags", "is", "board"), Condition("cf:handicap", "lt", "10")),
            actions = new object[] { new { type = "notify", config = new { message = "Have the starter ready" } } },
        })), "add the rule");

        async Task<Guid> MemberAsync(string first, object values, params Guid[] tags)
        {
            var id = await Post(owner, "/api/members", new { firstName = first, lastName = "Dupont", email = $"{first}@example.ch", tier = "Gold", status = "Active" });
            await Values(owner, "member", id, values);
            if (tags.Length > 0) await Tags(owner, "member", id, tags);
            return id;
        }
        var jean = await MemberAsync("jean", new { handicap = 8 }, board);
        var anna = await MemberAsync("anna", new { handicap = 20 }, board);
        var paul = await MemberAsync("paul", new { handicap = 5 });
        var court = (await Json(await owner.PostAsync("/api/resources", Body(new { name = "Court", kind = "Court", capacity = 4 })), "add a court")).GetProperty("id").GetString();
        var start = DateTime.UtcNow.Date.AddDays(3).AddHours(8);
        var hour = 0;
        foreach (var member in new Guid?[] { jean, anna, paul, null })
        {
            var at = start.AddHours(hour++);
            await Json(await owner.PostAsync("/api/reservations", Body(member is Guid id
                ? new { resourceId = court, memberId = (Guid?)id, memberName = (string?)null, startAtUtc = at, endAtUtc = at.AddHours(1) }
                : new { resourceId = court, memberId = (Guid?)null, memberName = (string?)"Claire Guest", startAtUtc = at, endAtUtc = at.AddHours(1) })), "book the court");
        }

        (await app.Services.Read(db => db.Notifications.Count(n => n.Title == "A board golfer booked"))).Should().Be(1,
            "only Jean is on the board with a handicap under 10; a guest has no member to test");
    }

    [Fact]
    public async Task SavingARuleChecksItsTagAndFieldConditions_AndTheCatalogOffersThem()
    {
        await using var app = new App();
        var owner = await WorkspaceAsync(app);
        var tier = await Post(owner, "/api/custom-fields", new { entityType = "deal", label = "Tier", type = "select", options = new[] { "Gold", "Silver" } });
        await Post(owner, "/api/custom-fields", new { entityType = "deal", label = "Renewal", type = "date" });
        await Post(owner, "/api/custom-fields", new { entityType = "lead", label = "Budget", type = "money" });
        await Post(owner, "/api/tags", new { name = "VIP" });

        var catalog = await Json(await owner.GetAsync("/api/automations/catalog"), "the catalog");
        var triggers = catalog.GetProperty("triggers").EnumerateArray().ToDictionary(t => t.GetProperty("key").GetString()!);
        Dictionary<string, JsonElement> FieldsOf(string trigger) =>
            triggers[trigger].GetProperty("fields").EnumerateArray().ToDictionary(f => f.GetProperty("key").GetString()!);
        var won = FieldsOf("deal.won");
        won["tags"].GetProperty("choices").EnumerateArray().Select(c => c.GetProperty("label").GetString()).Should().Equal("VIP");
        won["cf:tier"].GetProperty("type").GetString().Should().Be("choice");
        won["cf:tier"].GetProperty("choices").EnumerateArray().Select(c => c.GetProperty("value").GetString()).Should().Equal("Gold", "Silver");
        won["cf:renewal"].GetProperty("type").GetString().Should().Be("number", "a date is tested as the days from today to it");
        won.Should().NotContainKey("cf:budget", "a lead's field is not a deal's");
        triggers["deal.won"].GetProperty("dates").EnumerateArray().Select(d => d.GetProperty("key").GetString()).Should().Contain("cf:renewal");
        FieldsOf("lead.created")["cf:budget"].GetProperty("type").GetString().Should().Be("money");
        FieldsOf("member.created")["tags"].GetProperty("label").GetString().Should().Be("Member's tags");
        FieldsOf("contract.signed").Should().NotContainKey("tags", "a contract carries no tags of its own");
        FieldsOf("task.overdue").Should().NotContainKey("tags");

        async Task<HttpResponseMessage> Save(string trigger, object conditions, object[]? steps = null) =>
            await owner.PostAsync("/api/automations", Body(new { name = "Rule", trigger, conditions, actions = steps ?? new[] { Task("Do it") } }));
        async Task Refused(HttpResponseMessage res, string why)
        {
            res.StatusCode.Should().Be(HttpStatusCode.BadRequest, why);
            (await res.Content.ReadAsStringAsync()).Should().Contain("Automation.Invalid", why);
        }
        await Refused(await Save("deal.won", All(Condition("cf:nosuch", "is", "x"))), "there is no such field");
        await Refused(await Save("deal.won", All(Condition("cf:budget", "gt", "5"))), "the budget is a lead's");
        await Refused(await Save("deal.won", All(Condition("cf:tier", "gt", "5"))), "a choice is not compared as a number");
        await Refused(await Save("contract.signed", All(Condition("tags", "is", "VIP"))), "a contract has no tags");
        await Refused(await Save("deal.won", All(), new object[]
        {
            new { type = "wait", config = new { mode = "until", date = "cf:tier" } }, Task("Later"),
        }), "a choice is not a date to wait for");
        await Json(await Save("lead.created", new { match = "all", conditions = new[] { new { field = "cf:budget", op = "between", value = "1000", to = "5000" } } }),
            "a lead's budget between two amounts");
        await Json(await Save("deal.won", All(), new object[]
        {
            new { type = "wait", config = new { mode = "until", date = "cf:renewal", offsetAmount = "-7", offsetUnit = "days" } }, Task("Renew"),
        }), "a wait for the deal's renewal date");

        // A field deleted after a rule tests it leaves the rule to be renamed or switched off.
        var ruleId = (await Json(await Save("deal.won", All(Condition("cf:tier", "is", "Gold"))), "a rule on the tier")).GetProperty("id").GetGuid();
        (await owner.DeleteAsync($"/api/custom-fields/{tier}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await Json(await owner.PutAsync($"/api/automations/{ruleId}", Body(new { name = "Renamed", enabled = false })), "rename and switch off");
        await Refused(await owner.PutAsync($"/api/automations/{ruleId}", Body(new { conditions = All(Condition("cf:tier", "is", "Silver")) })),
            "conditions sent again are checked against the fields there are");
    }

    [Fact]
    public async Task AWaitCanWaitForACustomDate_AndTheLogNamesWhatABranchTested()
    {
        await using var app = new App();
        var owner = await WorkspaceAsync(app);
        await Post(owner, "/api/custom-fields", new { entityType = "deal", label = "Renewal", type = "date" });
        var vip = await Post(owner, "/api/tags", new { name = "VIP" });
        var ruleId = await Post(owner, "/api/automations", new
        {
            name = "Before the renewal", trigger = "deal.stage_changed",
            actions = new object[]
            {
                new { type = "wait", config = new { mode = "until", date = "cf:renewal", offsetAmount = "-1", offsetUnit = "days" } },
                new
                {
                    type = "branch",
                    condition = All(Condition("tags", "is", vip.ToString())),
                    then = new[] { Task("Renew the VIP") },
                    @else = new[] { Task("Renew") },
                },
            },
        });
        var renewal = DateOnly.FromDateTime(app.Clock.GetUtcNow().UtcDateTime).AddDays(5);
        var deal = await DealAsync(owner, "Gold membership", new { renewal = renewal.ToString("yyyy-MM-dd") }, vip);
        await Json(await owner.PutAsync($"/api/deals/{deal}", Body(new { stage = "Proposal" })), "move the deal");

        var run = await app.Services.Read(db => db.AutomationRuns.Single(r => r.RuleId == ruleId));
        run.Status.Should().Be(AutomationRunStatuses.Waiting);
        run.WaitingFor.Should().StartWith("1 day before the deal's Renewal", "the wait names the custom date it waits for");

        app.Clock.Set(renewal.AddDays(-1).ToDateTime(new TimeOnly(0, 30), DateTimeKind.Utc));
        await app.RunDueAsync();
        (await app.Services.Read(db => db.TaskItems.Where(t => t.DealId == deal).Select(t => t.Title).ToList())).Should().Equal("Renew the VIP");

        var log = await Json(await owner.GetAsync($"/api/automations/runs/{run.Id}"), "the run's log");
        log.GetProperty("steps").EnumerateArray().Select(s => s.GetProperty("summary").GetString())
            .Should().Contain("Yes: tags is VIP.", "the log names the tag, not its id");
    }
}
