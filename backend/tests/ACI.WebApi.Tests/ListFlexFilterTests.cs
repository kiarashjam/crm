using System.Net;
using System.Text;
using System.Text.Json;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The server lists (contacts, companies, leads, the deals board) filter and sort by their
/// records' tags and custom fields, search their text, and export them, for every record, not
/// only the page on screen.
/// </summary>
public class ListFlexFilterTests
{
    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task<HttpClient> WorkspaceAsync(CustomWebApplicationFactory factory)
    {
        var (owner, ownerId) = await SignInAsync(factory, "pierre@example.ch");
        owner.In(await CreateAsync(factory, ownerId, "Pavillon46"));
        return owner;
    }

    private static async Task<Guid> Post(HttpClient client, string url, object body) =>
        (await Ok(await client.PostAsync(url, Body(body)), $"POST {url}")).GetProperty("id").GetGuid();

    private static async Task Values(HttpClient client, string type, Guid id, object values) =>
        (await client.PutAsync("/api/custom-fields/values", Body(new { entityType = type, recordId = id, values }))).StatusCode.Should().Be(HttpStatusCode.NoContent);

    private static async Task Tags(HttpClient client, string type, Guid id, params Guid[] tags) =>
        (await client.PutAsync("/api/tags/records", Body(new { entityType = type, recordId = id, tagIds = tags }))).StatusCode.Should().Be(HttpStatusCode.OK);

    private static async Task<List<string>> Names(HttpClient client, string url) =>
        (await Ok(await client.GetAsync(url), $"GET {url}")).GetProperty("items").EnumerateArray().Select(c => c.GetProperty("name").GetString()!).ToList();

    [Fact]
    public async Task TheContactsListFiltersAndSortsByTagsAndFieldsAcrossEveryPage()
    {
        using var factory = new CustomWebApplicationFactory();
        var owner = await WorkspaceAsync(factory);
        await Post(owner, "/api/custom-fields", new { entityType = "contact", label = "Tier", type = "select", options = new[] { "Gold", "Silver" } });
        await Post(owner, "/api/custom-fields", new { entityType = "contact", label = "Spend", type = "money" });
        await Post(owner, "/api/custom-fields", new { entityType = "contact", label = "Joined", type = "date" });
        await Post(owner, "/api/custom-fields", new { entityType = "contact", label = "Interests", type = "multiselect", options = new[] { "Golf", "Wine" } });
        var vip = await Post(owner, "/api/tags", new { name = "VIP" });
        var lapsed = await Post(owner, "/api/tags", new { name = "Lapsed" });

        var ana = await Post(owner, "/api/contacts", new { name = "Ana", email = "ana@example.ch" });
        var bruno = await Post(owner, "/api/contacts", new { name = "Bruno", email = "bruno@example.ch" });
        var carla = await Post(owner, "/api/contacts", new { name = "Carla", email = "carla@example.ch" });
        var dino = await Post(owner, "/api/contacts", new { name = "Dino", email = "dino@example.ch" });
        await Values(owner, "contact", ana, new { tier = "Gold", spend = 5000, joined = "2025-01-15", interests = new[] { "Golf", "Wine" } });
        await Values(owner, "contact", bruno, new { tier = "Silver", spend = 900, joined = "2026-06-01", interests = new[] { "Wine" } });
        await Values(owner, "contact", carla, new { tier = "Gold", spend = 12000 });
        await Tags(owner, "contact", ana, vip);
        await Tags(owner, "contact", carla, vip, lapsed);
        await Tags(owner, "contact", dino, lapsed);

        (await Names(owner, "/api/contacts?field=tier:is:Gold")).Should().Equal("Ana", "Carla");
        (await Names(owner, "/api/contacts?field=tier:not:Gold")).Should().Equal("Bruno", "Dino");
        (await Names(owner, "/api/contacts?field=spend:gte:1000")).Should().Equal("Ana", "Carla");
        (await Names(owner, "/api/contacts?field=spend:between:500..6000")).Should().Equal("Ana", "Bruno");
        (await Names(owner, "/api/contacts?field=joined:before:2026-01-01")).Should().Equal("Ana");
        (await Names(owner, "/api/contacts?field=interests:any:Golf")).Should().Equal("Ana");
        (await Names(owner, "/api/contacts?field=interests:all:Golf|Wine")).Should().Equal("Ana");
        (await Names(owner, "/api/contacts?field=tier:empty")).Should().Equal("Dino");
        (await Names(owner, $"/api/contacts?tag={vip}")).Should().Equal("Ana", "Carla");
        (await Names(owner, $"/api/contacts?tag={vip}&tag={lapsed}&tagMatch=all")).Should().Equal("Carla");
        (await Names(owner, $"/api/contacts?tag={vip}&tag={lapsed}")).Should().Equal("Ana", "Carla", "Dino");
        (await Names(owner, $"/api/contacts?notTag={lapsed}")).Should().Equal("Ana", "Bruno");
        (await Names(owner, $"/api/contacts?tag={vip}&field=spend:lt:10000")).Should().Equal(new[] { "Ana" }, "conditions hold together");
        (await Names(owner, "/api/contacts?field=nosuchfield:is:x")).Should().HaveCount(4, "a condition on a field that is gone narrows nothing");

        // Sorted by a field, records without a value last whichever way; the count is the whole list's.
        (await Names(owner, "/api/contacts?sortBy=cf:spend&sortDir=desc")).Should().Equal("Carla", "Ana", "Bruno", "Dino");
        (await Names(owner, "/api/contacts?sortBy=cf:spend&sortDir=asc")).Should().Equal("Bruno", "Ana", "Carla", "Dino");
        var page = await Ok(await owner.GetAsync("/api/contacts?sortBy=cf:spend&sortDir=desc&pageSize=1&page=2"), "a later page");
        page.GetProperty("items").EnumerateArray().Single().GetProperty("name").GetString().Should().Be("Ana");
        page.GetProperty("totalCount").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task SearchFindsRecordsByTheirFieldsAndTags()
    {
        using var factory = new CustomWebApplicationFactory();
        var owner = await WorkspaceAsync(factory);
        await Post(owner, "/api/custom-fields", new { entityType = "contact", label = "Membership no", type = "text" });
        var board = await Post(owner, "/api/tags", new { name = "Board member" });
        var ana = await Post(owner, "/api/contacts", new { name = "Ana", email = "ana@example.ch" });
        var bruno = await Post(owner, "/api/contacts", new { name = "Bruno", email = "bruno@example.ch" });
        await Post(owner, "/api/contacts", new { name = "Carla", email = "carla@example.ch" });
        await Values(owner, "contact", ana, new { membership_no = "P46-00731" });
        await Tags(owner, "contact", bruno, board);

        (await Names(owner, "/api/contacts?search=00731")).Should().Equal("Ana");
        (await Names(owner, "/api/contacts?search=board")).Should().Equal("Bruno");
        var global = await Ok(await owner.GetAsync("/api/search?q=p46-007"), "global search");
        global.GetProperty("contacts").EnumerateArray().Select(c => c.GetProperty("name").GetString()).Should().Equal("Ana");
    }

    [Fact]
    public async Task CompaniesAndLeadsFilterAndTheContactExportCarriesFieldsAndTags()
    {
        using var factory = new CustomWebApplicationFactory();
        var owner = await WorkspaceAsync(factory);
        await Post(owner, "/api/custom-fields", new { entityType = "company", label = "Sector", type = "text" });
        await Post(owner, "/api/custom-fields", new { entityType = "lead", label = "Budget", type = "number" });
        await Post(owner, "/api/custom-fields", new { entityType = "contact", label = "Tier", type = "select", options = new[] { "Gold", "Silver" } });
        var vip = await Post(owner, "/api/tags", new { name = "VIP" });

        var bank = await Post(owner, "/api/companies", new { name = "Banque Léman" });
        await Post(owner, "/api/companies", new { name = "Hôtel du Lac" });
        await Values(owner, "company", bank, new { sector = "Private banking" });
        (await Names(owner, "/api/companies?field=sector:contains:bank")).Should().Equal("Banque Léman");

        var big = await Post(owner, "/api/leads", new { name = "Big lead", email = "big@example.ch", status = "New" });
        await Post(owner, "/api/leads", new { name = "Small lead", email = "small@example.ch", status = "New" });
        await Values(owner, "lead", big, new { budget = 50000 });
        (await Names(owner, "/api/leads?field=budget:gt:10000")).Should().Equal("Big lead");
        (await Names(owner, "/api/leads?sortBy=cf:budget&sortDir=desc")).First().Should().Be("Big lead");

        var ana = await Post(owner, "/api/contacts", new { name = "Ana", email = "ana@example.ch" });
        await Values(owner, "contact", ana, new { tier = "Gold" });
        await Tags(owner, "contact", ana, vip);
        var file = await owner.GetAsync($"/api/contacts/export?tag={vip}");
        file.StatusCode.Should().Be(HttpStatusCode.OK);
        var csv = Encoding.UTF8.GetString(await file.Content.ReadAsByteArrayAsync()).TrimStart('﻿');
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().Contain("Tier").And.Contain("Tags");
        lines.Should().HaveCount(2, "the export takes the list's tag filter");
        lines[1].Should().Contain("Gold").And.Contain("VIP");
    }

    [Fact]
    public async Task TheDealsBoardFiltersByTagsAndFields()
    {
        using var factory = new CustomWebApplicationFactory();
        var owner = await WorkspaceAsync(factory);
        await Post(owner, "/api/custom-fields", new { entityType = "deal", label = "Board seat", type = "checkbox" });
        var corporate = await Post(owner, "/api/tags", new { name = "Corporate" });
        var seat = await Post(owner, "/api/deals", new { name = "With a seat", value = "1000" });
        var tagged = await Post(owner, "/api/deals", new { name = "Corporate deal", value = "2000" });
        await Post(owner, "/api/deals", new { name = "Plain", value = "3000" });
        await Values(owner, "deal", seat, new { board_seat = true });
        await Tags(owner, "deal", tagged, corporate);

        static IEnumerable<string> Cards(JsonElement board) =>
            board.GetProperty("columns").EnumerateArray().SelectMany(c => c.GetProperty("deals").EnumerateArray()).Select(d => d.GetProperty("name").GetString()!);

        var withSeat = await Ok(await owner.GetAsync("/api/deals/board?field=board_seat:true"), "board by field");
        Cards(withSeat).Should().Equal("With a seat");
        withSeat.GetProperty("count").GetInt32().Should().Be(1);
        withSeat.GetProperty("summary").GetProperty("total").GetInt32().Should().Be(3, "the figures above stay the whole pipeline's");
        Cards(await Ok(await owner.GetAsync($"/api/deals/board?tag={corporate}"), "board by tag")).Should().Equal("Corporate deal");
        Cards(await Ok(await owner.GetAsync("/api/deals/board?field=board_seat:false"), "without a seat")).Should().BeEquivalentTo("Corporate deal", "Plain");
    }
}
