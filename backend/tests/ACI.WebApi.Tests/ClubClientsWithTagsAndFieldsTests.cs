using System.Net;
using System.Text.Json;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// A company, a contact or a member the club keeps records of (a corporate membership, a seat
/// on one, a house account), who also carries the workspace's own fields and tags, when they
/// are merged into another or deleted.
/// </summary>
/// <remarks>
/// The two were built side by side: merging or deleting a company moves or lets go of the
/// club's records (ClubClientRecords, MemberCorporate) and, in the same transaction and the
/// same save, of its custom values and tags and the link fields pointing at it (RecordMerge,
/// RecordDependents). These tests run both at once, in memory and on a real SQL Server, where
/// the unique indexes of both sides are enforced on that one save; deleting a company makes
/// set-based updates the in-memory provider cannot run, so that one runs on SQL Server only.
/// </remarks>
public class ClubClientsWithTagsAndFieldsTests
{
    private static async Task<JsonElement> Json(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.IsSuccessStatusCode.Should().BeTrue($"{step} should succeed — got {(int)res.StatusCode}: {body}");
        return body.Length == 0 ? default : JsonDocument.Parse(body).RootElement.Clone();
    }

    private static async Task Done(HttpResponseMessage res, string step)
    {
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.NoContent, $"{step} should succeed — got {(int)res.StatusCode}: {body}");
    }

    private static Guid Id(JsonElement e) => e.GetProperty("id").GetGuid();

    private static async Task<Guid> CompanyAsync(HttpClient client, string name) =>
        Id(await Json(await client.PostAsync("/api/companies", Body(new { name })), $"add {name}"));

    private static async Task<Guid> ContactAsync(HttpClient client, string name, string email) =>
        Id(await Json(await client.PostAsync("/api/contacts", Body(new { name, email })), $"add {name}"));

    private static async Task<Guid> MemberAsync(HttpClient client, string first, string last = "Keller") =>
        Id(await Json(await client.PostAsync("/api/members", Body(new
        {
            firstName = first, lastName = last, email = $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}@example.ch", tier = "Gold", status = "Active",
        })), $"add {first} {last}"));

    private static async Task<Guid> CorporateAsync(HttpClient client, Guid company) =>
        Id(await Json(await client.PostAsync("/api/corporate-memberships", Body(new
        {
            companyId = company, name = "Corporate", seats = 3, nomineeCharges = "company", startsAtUtc = DateTime.UtcNow.Date.ToString("O"),
        })), "make the corporate membership"));

    private static async Task SeatAsync(HttpClient client, Guid membership, Guid member) =>
        await Json(await client.PostAsync($"/api/corporate-memberships/{membership}/nominees", Body(new { memberId = member })), "seat a nominee");

    private static async Task FieldAsync(HttpClient client, string entityType, string label, string type, string? linkTo = null) =>
        await Json(await client.PostAsync("/api/custom-fields", Body(new { entityType, label, type, linkEntityType = linkTo })), $"add the {label} field");

    private static async Task<Guid> TagAsync(HttpClient client, string name) =>
        Id(await Json(await client.PostAsync("/api/tags", Body(new { name })), $"make the {name} tag"));

    private static async Task ValuesAsync(HttpClient client, string entityType, Guid recordId, object values) =>
        await Done(await client.PutAsync("/api/custom-fields/values", Body(new { entityType, recordId, values })), $"fill in a {entityType}'s fields");

    private static async Task TagsAsync(HttpClient client, string entityType, Guid recordId, params Guid[] tagIds) =>
        await Json(await client.PutAsync("/api/tags/records", Body(new { entityType, recordId, tagIds })), $"tag a {entityType}");

    private static async Task<JsonElement> FieldsOf(HttpClient client, string entityType, Guid recordId) =>
        await Json(await client.GetAsync($"/api/custom-fields/values?entityType={entityType}&recordId={recordId}"), $"the {entityType}'s fields");

    private static async Task<IEnumerable<Guid>> TagsOf(HttpClient client, string entityType, Guid recordId) =>
        (await Json(await client.GetAsync($"/api/tags/records?entityType={entityType}&recordId={recordId}"), $"the {entityType}'s tags"))
            .EnumerateArray().Select(t => t.GetGuid()).ToList();

    /// <summary>
    /// Two records of one company, each with something of the club's and something of the
    /// workspace's own. The duplicate holds the corporate membership, whose nominee's drink is on
    /// its house account; it carries the "Key account" tag the company kept carries too, and a
    /// "Golf" tag of its own; it has a sector the company kept has as well, and a founding year
    /// that one lacks; and a contact's employer field points at it. Merged, all of it is the
    /// company kept's, once.
    /// </summary>
    private static async Task MergeTwoCompaniesAsync(HttpClient client, Func<Guid, Task<int>> rowsNaming)
    {
        await FieldAsync(client, "company", "Sector", "text");
        await FieldAsync(client, "company", "Founded", "number");
        await FieldAsync(client, "contact", "Employer", "link", linkTo: "company");
        var keyAccount = await TagAsync(client, "Key account");
        var partner = await TagAsync(client, "Partner");
        var golf = await TagAsync(client, "Golf");

        var acme = await CompanyAsync(client, "Acme AG");
        var duplicate = await CompanyAsync(client, "ACME (old record)");
        await ValuesAsync(client, "company", acme, new { sector = "Banking" });
        await ValuesAsync(client, "company", duplicate, new { sector = "Finance", founded = 1912 });
        await TagsAsync(client, "company", acme, keyAccount, partner);
        await TagsAsync(client, "company", duplicate, keyAccount, golf);
        var lena = await ContactAsync(client, "Lena Graf", "lena@graf.ch");
        await ValuesAsync(client, "contact", lena, new { employer = new { id = duplicate } });

        // Acme has an account of its own with a dinner on it; the duplicate holds the membership.
        await Json(await client.PostAsync($"/api/club-accounts/companies/{acme}", null), "open Acme's account");
        await Json(await client.PostAsync("/api/charges", Body(new { memberId = acme, kind = "Event", description = "Board dinner", amount = 300m })), "charge Acme");
        var membership = await CorporateAsync(client, duplicate);
        var anna = await MemberAsync(client, "Anna");
        await SeatAsync(client, membership, anna);
        var drink = await Json(await client.PostAsync("/api/charges", Body(new { memberId = anna, kind = "Bar", description = "Negroni", amount = 18m })), "Anna at the bar");
        drink.GetProperty("memberId").GetGuid().Should().Be(duplicate, "the company holding her seat pays her charges");

        await Done(await client.PostAsync($"/api/companies/{acme}/merge", Body(new { otherCompanyId = duplicate })), "merge the duplicate into Acme");

        // The club's records are the kept company's...
        (await Json(await client.GetAsync($"/api/corporate-memberships/{membership}"), "the membership, after")).GetProperty("companyId").GetGuid().Should().Be(acme);
        (await Json(await client.GetAsync($"/api/club-accounts/companies/{acme}"), "Acme's account")).GetProperty("owed").GetDecimal()
            .Should().Be(318m, "the duplicate's entries joined Acme's, on one account");
        // ...and so are its own fields and tags, and the link that pointed at the duplicate.
        var fields = await FieldsOf(client, "company", acme);
        fields.GetProperty("sector").GetString().Should().Be("Banking", "the kept company's value wins");
        fields.GetProperty("founded").GetDecimal().Should().Be(1912m, "an empty field takes the duplicate's");
        (await TagsOf(client, "company", acme)).Should().BeEquivalentTo([keyAccount, partner, golf], "the tags of both, once each");
        (await FieldsOf(client, "contact", lena)).GetProperty("employer").GetProperty("id").GetGuid().Should().Be(acme, "her employer is the company kept");
        (await rowsNaming(duplicate)).Should().Be(0, "no value, tag or link is left naming the company merged away");
    }

    /// <summary>
    /// Two records of one member: the duplicate holds a seat on a company's corporate membership
    /// and has a handicap and a tag. Merged, the member kept holds the seat, the company pays
    /// their drinks, and the handicap and the tag are theirs.
    /// </summary>
    private static async Task MergeTwoMembersAsync(HttpClient client, Func<Guid, Task<int>> rowsNaming)
    {
        await FieldAsync(client, "member", "Handicap", "number");
        var board = await TagAsync(client, "Board");
        var acme = await CompanyAsync(client, "Acme AG");
        var membership = await CorporateAsync(client, acme);
        var anna = await MemberAsync(client, "Anna");
        var annaAgain = await MemberAsync(client, "Anna", "Keller-Graf");
        await SeatAsync(client, membership, annaAgain);
        await ValuesAsync(client, "member", annaAgain, new { handicap = 12 });
        await TagsAsync(client, "member", annaAgain, board);

        await Done(await client.PostAsync($"/api/members/{anna}/merge", Body(new { otherMemberId = annaAgain })), "merge Anna's records");

        (await Json(await client.GetAsync($"/api/corporate-memberships/{membership}"), "the membership")).GetProperty("nominees").EnumerateArray()
            .Select(n => n.GetProperty("memberId").GetGuid()).Should().Equal(anna);
        var drink = await Json(await client.PostAsync("/api/charges", Body(new { memberId = anna, kind = "Bar", description = "Negroni", amount = 18m })), "Anna at the bar");
        (drink.GetProperty("memberId").GetGuid(), drink.GetProperty("accountType").GetString()).Should().Be((acme, "company"));
        (await FieldsOf(client, "member", anna)).GetProperty("handicap").GetDecimal().Should().Be(12m);
        (await TagsOf(client, "member", anna)).Should().Equal(board);
        (await rowsNaming(annaAgain)).Should().Be(0, "no value or tag is left on the record merged away");
    }

    private static async Task<HttpClient> ClubAsync(CustomWebApplicationFactory factory)
    {
        var (client, userId) = await SignInAsync(factory, "owner@pavillon46.ch");
        client.In(await CreateAsync(factory, userId, "Pavillon46"));
        return client;
    }

    /// <summary>The custom values on the record, the link values pointing at it and the tags on it, wherever they are kept.</summary>
    private static Func<Guid, Task<int>> RowsNamingInMemory(CustomWebApplicationFactory factory) => id =>
        factory.Services.Read(db =>
            db.CustomFieldEntries.Count(e => e.RecordId == id || e.LinkId == id) + db.RecordTags.Count(t => t.RecordId == id));

    private static Func<Guid, Task<int>> RowsNamingOnSqlServer(string database) => async id =>
        Convert.ToInt32(await SqlServerApp.ScalarAsync(database,
            $"SELECT (SELECT COUNT(*) FROM [CustomFieldEntries] WHERE [RecordId] = '{id}' OR [LinkId] = '{id}') + (SELECT COUNT(*) FROM [RecordTags] WHERE [RecordId] = '{id}')"));

    /// <summary>A club on its own database on the SQL Server in CADENCE_TEST_SQLSERVER.</summary>
    private static async Task OnSqlServerAsync(Func<HttpClient, string, Task> test)
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (client, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            client.In(Id(await Json(await client.PostAsync("/api/organizations", Body(new { name = "Pavillon46" })), "create the club")));
            await test(client, database);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [Fact]
    public async Task MergingTwoCompanies_MovesTheClubsRecords_AndTheirTagsFieldsAndLinks_ToTheCompanyKept()
    {
        using var factory = new CustomWebApplicationFactory();
        await MergeTwoCompaniesAsync(await ClubAsync(factory), RowsNamingInMemory(factory));
    }

    [Fact]
    public async Task MergingTwoMembers_MovesTheSeat_AndTheirTagsAndFields_ToTheMemberKept()
    {
        using var factory = new CustomWebApplicationFactory();
        await MergeTwoMembersAsync(await ClubAsync(factory), RowsNamingInMemory(factory));
    }

    [SqlServerFact]
    public Task OnSqlServer_MergingTwoCompanies_MovesTheClubsRecords_AndTheirTagsFieldsAndLinks_ToTheCompanyKept() =>
        OnSqlServerAsync((client, database) => MergeTwoCompaniesAsync(client, RowsNamingOnSqlServer(database)));

    [SqlServerFact]
    public Task OnSqlServer_MergingTwoMembers_MovesTheSeat_AndTheirTagsAndFields_ToTheMemberKept() =>
        OnSqlServerAsync((client, database) => MergeTwoMembersAsync(client, RowsNamingOnSqlServer(database)));

    [SqlServerFact]
    public Task OnSqlServer_ACompanyTheClubKeeps_KeepsItsTagsAndFields_AndOneNothingKeepsGoesWithThemAndItsEmptyAccount() =>
        OnSqlServerAsync(async (client, database) =>
        {
            await FieldAsync(client, "company", "Sector", "text");
            await FieldAsync(client, "contact", "Employer", "link", linkTo: "company");
            var keyAccount = await TagAsync(client, "Key account");
            var acme = await CompanyAsync(client, "Acme AG");
            var bolt = await CompanyAsync(client, "Bolt GmbH");
            foreach (var company in new[] { acme, bolt })
            {
                await ValuesAsync(client, "company", company, new { sector = "Banking" });
                await TagsAsync(client, "company", company, keyAccount);
            }
            var lena = await ContactAsync(client, "Lena Graf", "lena@graf.ch");
            await ValuesAsync(client, "contact", lena, new { employer = new { id = bolt } });
            await CorporateAsync(client, acme);
            await Json(await client.PostAsync($"/api/club-accounts/companies/{bolt}", null), "open Bolt's account");

            (await client.DeleteAsync($"/api/companies/{acme}")).StatusCode.Should().Be(HttpStatusCode.Conflict, "the club keeps Acme for its corporate membership");
            (await client.DeleteAsync($"/api/companies/{bolt}")).StatusCode.Should().Be(HttpStatusCode.NoContent, "nothing keeps Bolt but an empty account");

            var rowsNaming = RowsNamingOnSqlServer(database);
            (await rowsNaming(acme)).Should().Be(2, "the company the club keeps keeps its sector and its tag");
            (await rowsNaming(bolt)).Should().Be(0, "the company deleted takes its sector, its tag and Lena's employer link with it");
            Convert.ToInt32(await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [ClubHouseAccounts] WHERE [MemberId] = '{bolt}'"))
                .Should().Be(0, "and its empty account");
            (await FieldsOf(client, "contact", lena)).TryGetProperty("employer", out var employer).Should().BeFalse($"Lena's employer is gone, not {employer}");
        });
}
