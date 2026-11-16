using System.Net;
using System.Text;
using System.Text.Json;
using ACI.Infrastructure.Migrations;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Custom fields, tags and saved views on a real SQL Server: the migration that copies the
/// values kept as JSON into typed rows, and the lists' filters, order and search as the
/// server translates them (the in-memory tests cannot show that).
/// </summary>
public class FlexDataOnSqlServerTests
{
    private static Task<object?> Value(string database, string sql) => SqlServerApp.ScalarAsync(database, sql);

    [SqlServerFact]
    public async Task ValuesKeptAsJsonAreCopiedIntoTypedRows_WhereAFieldStillHoldsThem_AndASecondRunChangesNothing()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            var migrations = SqlServerSchema.AllMigrationIds();
            var ours = migrations.Single(m => m.EndsWith("_AddFlexibleData", StringComparison.Ordinal));
            await using (var db = SqlServerSchema.Context(database))
                await db.GetService<IMigrator>().MigrateAsync(migrations[migrations.ToList().IndexOf(ours) - 1]);

            var org = Guid.NewGuid();
            var owner = Guid.NewGuid();
            var someoneElse = Guid.NewGuid();
            var fields = new Dictionary<string, Guid>();
            async Task Field(string key, string type, Guid? organization, Guid ownerId, string entityType = "contact")
            {
                var id = Guid.NewGuid();
                fields[$"{entityType}.{key}.{organization?.ToString() ?? ownerId.ToString()}"] = id;
                await SqlServerApp.ExecuteAsync(database, $@"
                    INSERT INTO [CustomFieldDefinitions] ([Id], [OrganizationId], [OwnerUserId], [EntityType], [Key], [Label], [Type], [Required], [Order], [CreatedAtUtc])
                    VALUES ('{id}', {(organization == null ? "NULL" : $"'{organization}'")}, '{ownerId}', N'{entityType}', N'{key}', N'{key}', N'{type}', 0, 0, SYSUTCDATETIME())");
            }
            await Field("tier", "select", org, owner);
            await Field("seats", "number", org, owner);
            await Field("joined", "date", org, owner);
            await Field("vip", "checkbox", org, owner);
            await Field("site", "url", org, owner);
            await Field("note", "text", org, owner);
            await Field("budget", "number", null, owner, "lead");

            async Task Values(Guid recordId, string json, Guid? organization, Guid ownerId, string entityType = "contact")
            {
                await SqlServerApp.ExecuteAsync(database, $@"
                    INSERT INTO [CustomFieldValues] ([Id], [OrganizationId], [OwnerUserId], [EntityType], [RecordId], [ValuesJson], [UpdatedAtUtc])
                    VALUES (NEWID(), {(organization == null ? "NULL" : $"'{organization}'")}, '{ownerId}', N'{entityType}', '{recordId}', N'{json.Replace("'", "''")}', SYSUTCDATETIME())");
            }
            var ana = Guid.NewGuid();
            var bruno = Guid.NewGuid();
            var carla = Guid.NewGuid();
            var personalLead = Guid.NewGuid();
            var strangersLead = Guid.NewGuid();
            await Values(ana, """{"tier":"Gold","seats":"12","joined":"2025-03-01","vip":true,"site":"https://ana.example.ch","note":"   ","gone":"no field"}""", org, owner);
            await Values(bruno, """{"seats":7.5,"joined":"01.03.2025","vip":"yes","tier":""}""", org, owner);
            await Values(carla, "not json at all", org, owner);
            await Values(personalLead, """{"budget":5000}""", null, owner, "lead");
            await Values(strangersLead, """{"budget":100}""", null, someoneElse, "lead");

            await SqlServerSchema.MigrateAloneAsync(database);

            async Task<string?> Cell(Guid record, string fieldKey, string column) =>
                (await Value(database, $@"SELECT CONVERT(nvarchar(200), e.[{column}]) FROM [CustomFieldEntries] e
                    JOIN [CustomFieldDefinitions] d ON d.[Id] = e.[FieldId] WHERE e.[RecordId] = '{record}' AND d.[Key] = N'{fieldKey}'"))?.ToString();
            (await Cell(ana, "tier", "TextValue")).Should().Be("Gold");
            (await Cell(ana, "tier", "TextKey")).Should().Be("Gold");
            decimal.Parse((await Cell(ana, "seats", "NumberValue"))!, System.Globalization.CultureInfo.InvariantCulture).Should().Be(12, "a number written as text is read as a number");
            (await Cell(ana, "joined", "DateValue")).Should().Be("2025-03-01");
            (await Cell(ana, "vip", "BoolValue")).Should().Be("1");
            (await Cell(ana, "site", "TextValue")).Should().Be("https://ana.example.ch");
            (await Cell(ana, "note", "TextValue")).Should().BeNull("a blank value is no value");
            decimal.Parse((await Cell(bruno, "seats", "NumberValue"))!, System.Globalization.CultureInfo.InvariantCulture).Should().Be(7.5m);
            (await Cell(bruno, "joined", "DateValue")).Should().BeNull("a date not written as yyyy-MM-dd is left in the old copy, not guessed at");
            (await Cell(bruno, "vip", "BoolValue")).Should().BeNull("a checkbox that is not true or false is left out");
            (await Cell(bruno, "tier", "TextValue")).Should().BeNull();
            (await Value(database, $"SELECT COUNT(*) FROM [CustomFieldEntries] WHERE [RecordId] = '{carla}'")).Should().Be(0, "JSON that cannot be read copies nothing and stops nothing");
            (await Value(database, $"SELECT COUNT(*) FROM [CustomFieldEntries] WHERE [RecordId] = '{personalLead}' AND [OrganizationId] IS NULL")).Should().Be(1,
                "a personal workspace's values go with its own fields");
            (await Value(database, $"SELECT COUNT(*) FROM [CustomFieldEntries] WHERE [RecordId] = '{strangersLead}'")).Should().Be(0,
                "someone else's personal value does not match another person's field");
            (await Value(database, "SELECT COUNT(*) FROM [CustomFieldEntries]")).Should().Be(7, "five of Ana's, one of Bruno's and the personal budget");
            (await Value(database, "SELECT COUNT(*) FROM [CustomFieldValues]")).Should().Be(5, "the old rows are copied, not moved");

            // Run again, as an idempotent script would: nothing more is copied, nothing changes.
            var before = await Value(database, "SELECT STRING_AGG(CONVERT(nvarchar(36), [RecordId]) + N':' + CONVERT(nvarchar(36), [FieldId]), N';') WITHIN GROUP (ORDER BY [RecordId], [FieldId]) FROM [CustomFieldEntries]");
            await SqlServerApp.ExecuteAsync(database, AddFlexibleData.CopyCustomFieldValuesSql);
            (await Value(database, "SELECT STRING_AGG(CONVERT(nvarchar(36), [RecordId]) + N':' + CONVERT(nvarchar(36), [FieldId]), N';') WITHIN GROUP (ORDER BY [RecordId], [FieldId]) FROM [CustomFieldEntries]"))
                .Should().Be(before);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    private static async Task<JsonElement> Ok(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    [SqlServerFact]
    public async Task TheListsFilterSortAndSearchByTagsAndFields_AsTheServerRunsThem()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (client, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var org = (await Ok(await client.PostAsync("/api/organizations", Body(new { name = "Pavillon46" })), "create the club")).GetProperty("id").GetGuid();
            client.In(org);
            async Task<Guid> Post(string url, object body) => (await Ok(await client.PostAsync(url, Body(body)), $"POST {url}")).GetProperty("id").GetGuid();
            async Task Values(string type, Guid id, object values) =>
                (await client.PutAsync("/api/custom-fields/values", Body(new { entityType = type, recordId = id, values }))).StatusCode.Should().Be(HttpStatusCode.NoContent);
            async Task Tags(string type, Guid id, params Guid[] tags) => await Ok(await client.PutAsync("/api/tags/records", Body(new { entityType = type, recordId = id, tagIds = tags })), "tag");
            async Task<List<string>> Names(string url) =>
                (await Ok(await client.GetAsync(url), $"GET {url}")).GetProperty("items").EnumerateArray().Select(c => c.GetProperty("name").GetString()!).ToList();

            await Post("/api/custom-fields", new { entityType = "contact", label = "Tier", type = "select", options = new[] { "Gold", "Silver" } });
            await Post("/api/custom-fields", new { entityType = "contact", label = "Spend", type = "money", currency = "CHF" });
            await Post("/api/custom-fields", new { entityType = "contact", label = "Joined", type = "date" });
            await Post("/api/custom-fields", new { entityType = "contact", label = "Interests", type = "multiselect", options = new[] { "Golf", "Wine" } });
            await Post("/api/custom-fields", new { entityType = "contact", label = "Membership no", type = "text" });
            var vip = await Post("/api/tags", new { name = "VIP", color = "amber" });
            var lapsed = await Post("/api/tags", new { name = "Lapsed" });
            var ana = await Post("/api/contacts", new { name = "Ana", email = "ana@example.ch" });
            var bruno = await Post("/api/contacts", new { name = "Bruno", email = "bruno@example.ch" });
            var carla = await Post("/api/contacts", new { name = "Carla", email = "carla@example.ch" });
            var dino = await Post("/api/contacts", new { name = "Dino", email = "dino@example.ch" });
            await Values("contact", ana, new { tier = "Gold", spend = new { amount = 5000, currency = "CHF" }, joined = "2025-01-15", interests = new[] { "Golf", "Wine" }, membership_no = "P46-00731" });
            await Values("contact", bruno, new { tier = "Silver", spend = new { amount = 900, currency = "CHF" }, joined = "2026-06-01", interests = new[] { "Wine" } });
            await Values("contact", carla, new { tier = "Gold", spend = new { amount = 12000, currency = "CHF" } });
            await Tags("contact", ana, vip);
            await Tags("contact", carla, vip, lapsed);
            await Tags("contact", dino, lapsed);

            (await Names("/api/contacts?field=tier:is:gold")).Should().Equal(new[] { "Ana", "Carla" }, "options are matched ignoring case");
            (await Names("/api/contacts?field=tier:not:Gold")).Should().Equal("Bruno", "Dino");
            (await Names("/api/contacts?field=spend:between:500..6000")).Should().Equal("Ana", "Bruno");
            (await Names("/api/contacts?field=joined:before:2026-01-01")).Should().Equal("Ana");
            (await Names("/api/contacts?field=interests:all:Golf|Wine")).Should().Equal("Ana");
            (await Names("/api/contacts?field=tier:empty")).Should().Equal("Dino");
            (await Names($"/api/contacts?tag={vip}&tag={lapsed}&tagMatch=all")).Should().Equal("Carla");
            (await Names($"/api/contacts?notTag={lapsed}&field=spend:gte:1000")).Should().Equal("Ana");
            (await Names("/api/contacts?sortBy=cf:spend&sortDir=desc")).Should().Equal("Carla", "Ana", "Bruno", "Dino");
            var byTier = await Names("/api/contacts?sortBy=cf:tier&sortDir=asc");
            byTier.Take(2).Should().BeEquivalentTo("Ana", "Carla");
            byTier.Skip(2).Should().Equal(new[] { "Bruno", "Dino" }, "Gold before Silver, and no tier last");
            (await Names("/api/contacts?search=00731")).Should().Equal("Ana");
            (await Names("/api/contacts?search=laps")).Should().Equal("Carla", "Dino");
            var csv = Encoding.UTF8.GetString(await (await client.GetAsync($"/api/contacts/export?tag={vip}")).Content.ReadAsByteArrayAsync());
            csv.Should().Contain("Gold").And.Contain("VIP");

            var deal = await Post("/api/deals", new { name = "Corporate deal", value = "2000" });
            await Post("/api/deals", new { name = "Plain", value = "3000" });
            await Tags("deal", deal, vip);
            var board = await Ok(await client.GetAsync($"/api/deals/board?tag={vip}"), "the board by tag");
            board.GetProperty("columns").EnumerateArray().SelectMany(c => c.GetProperty("deals").EnumerateArray()).Select(d => d.GetProperty("name").GetString())
                .Should().Equal("Corporate deal");

            var member = await Post("/api/members", new { firstName = "Eva", lastName = "Meier", email = "eva@example.ch", tier = "Gold", status = "Active" });
            await Post("/api/members", new { firstName = "Fritz", lastName = "Meier", email = "fritz@example.ch", tier = "Gold", status = "Active" });
            await Tags("member", member, vip);
            (await Ok(await client.GetAsync($"/api/members?tag={vip}"), "members by tag")).EnumerateArray().Select(m => m.GetProperty("id").GetGuid()).Should().Equal(member);

            var bulk = await Ok(await client.PostAsync("/api/tags/bulk", Body(new { entityType = "contact", recordIds = new[] { ana, bruno }, add = new[] { lapsed }, remove = new[] { vip } })), "bulk");
            bulk.GetProperty("added").GetInt32().Should().Be(2);
            bulk.GetProperty("removed").GetInt32().Should().Be(1);
            (await Ok(await client.GetAsync("/api/tags"), "tags")).EnumerateArray().Single(t => t.GetProperty("name").GetString() == "Lapsed")
                .GetProperty("uses").GetInt32().Should().Be(4);

            var view = await Ok(await client.PostAsync("/api/saved-views", Body(new
            {
                list = "contacts", name = "Gold", shared = true, query = new Dictionary<string, object> { ["field"] = new[] { "tier:is:Gold" } }, columns = new[] { "name", "cf:tier" },
            })), "save a view");
            (await client.PutAsync("/api/saved-views/default", Body(new { list = "contacts", viewId = view.GetProperty("id").GetGuid() }))).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await Ok(await client.GetAsync("/api/saved-views?list=contacts"), "views")).EnumerateArray().Single().GetProperty("isDefault").GetBoolean().Should().BeTrue();

            var batch = await Ok(await client.PostAsync("/api/custom-fields/values/batch", Body(new { entityType = "contact", recordIds = new[] { ana, bruno } })), "batch values");
            batch.GetProperty(ana.ToString()).GetProperty("tier").GetString().Should().Be("Gold");
            (await Ok(await client.GetAsync("/api/automations/catalog"), "the catalog")).GetProperty("triggers").EnumerateArray()
                .Single(t => t.GetProperty("key").GetString() == "deal.won").GetProperty("fields").EnumerateArray()
                .Select(f => f.GetProperty("key").GetString()).Should().Contain("tags");

            // Deleting a contact takes its values and tags with it.
            (await client.DeleteAsync($"/api/contacts/{dino}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await Value(database, $"SELECT COUNT(*) FROM [RecordTags] WHERE [RecordId] = '{dino}'")).Should().Be(0);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
