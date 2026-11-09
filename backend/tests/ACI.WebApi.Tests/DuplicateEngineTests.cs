using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Duplicate contacts and companies are found on the server, and "not the same" is decided
/// once for the whole team.
/// </summary>
/// <remarks>
/// The browser compared contacts by email and ruled out two with different emails. Emails
/// are unique in a workspace, so the Duplicates page and the Contacts banner never found a
/// duplicate on a real server, and "not the same" lived in one browser.
/// </remarks>
public class DuplicateEngineTests
{
    private sealed record Team(CustomWebApplicationFactory Factory, HttpClient Owner, Guid OwnerId, HttpClient Member, HttpClient Viewer, Guid Org);

    private static async Task<Team> TeamAsync()
    {
        var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon");
        owner.In(org);
        var (member, memberId) = await SignInAsync(factory, "member@example.ch");
        await AddMemberAsync(factory, org, memberId, OrgMemberRole.Member);
        member.In(org);
        var (viewer, viewerId) = await SignInAsync(factory, "viewer@example.ch");
        await AddMemberAsync(factory, org, viewerId, OrgMemberRole.Viewer);
        viewer.In(org);
        return new Team(factory, owner, ownerId, member, viewer, org);
    }

    private static async Task<Guid> AddContactAsync(Team t, string name, string email, string? phone = null, Guid? companyId = null, bool archived = false)
    {
        using var scope = t.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var contact = new Contact
        {
            Id = Guid.NewGuid(), UserId = t.OwnerId, OrganizationId = t.Org, Name = name, Email = email, Phone = phone,
            CompanyId = companyId, IsArchived = archived, CreatedAtUtc = DateTime.UtcNow,
        };
        db.Contacts.Add(contact);
        await db.SaveChangesAsync();
        return contact.Id;
    }

    private static async Task<JsonElement> GetJson(HttpClient client, string url)
    {
        var res = await client.GetAsync(url);
        var body = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, $"{url}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private static Guid[] Ids(JsonElement group) =>
        group.GetProperty("records").EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).ToArray();

    [Fact]
    public async Task TheSamePersonUnderTwoEmailsIsFound_AndRecordsComeWithWhatTheMergeNeeds()
    {
        var t = await TeamAsync();
        using var _ = t.Factory;
        var marc = await AddContactAsync(t, "Marc Dubois", "marc@gmail.com", "+41 79 123 45 67");
        var marcAtWork = await AddContactAsync(t, "Marc Dubois", "m.dubois@firm.ch", "079 123 45 67");
        await AddContactAsync(t, "Anna Keller", "anna@example.ch", "+41 78 000 00 00");
        // An archived record is not offered: it is already out of the way.
        await AddContactAsync(t, "Marc Dubois", "old.marc@example.ch", "079 123 45 67", archived: true);

        var scan = await GetJson(t.Owner, "/api/duplicates/contacts");

        var group = scan.GetProperty("groups").EnumerateArray().Should().ContainSingle().Subject;
        Ids(group).Should().BeEquivalentTo(new[] { marc, marcAtWork });
        group.GetProperty("reason").GetString().Should().Be("Same phone · Same name");
        group.GetProperty("key").GetString().Should().Be(string.Join('|', new[] { marc, marcAtWork }.Select(i => i.ToString()).OrderBy(i => i, StringComparer.Ordinal)));
        var record = group.GetProperty("records")[0];
        record.GetProperty("email").GetString().Should().NotBeNullOrEmpty();
        record.GetProperty("createdAtUtc").GetString().Should().NotBeNullOrEmpty();
        scan.GetProperty("notSame").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task NotTheSameIsDecidedOnceForTheWholeTeam_AndCanBeUndone()
    {
        var t = await TeamAsync();
        using var _ = t.Factory;
        var father = await AddContactAsync(t, "Alex Meier", "alex@meier.ch", "044 123 45 67");
        var son = await AddContactAsync(t, "Alex Meier", "alex.junior@meier.ch", "+41 44 123 45 67");

        var mark = await t.Member.PostAsync("/api/duplicates/contacts/not-same", Body(new { recordIds = new[] { father, son } }));
        mark.StatusCode.Should().Be(HttpStatusCode.NoContent, await mark.Content.ReadAsStringAsync());

        // Seen from someone else's browser: the decision is the workspace's.
        var scan = await GetJson(t.Owner, "/api/duplicates/contacts");
        scan.GetProperty("groups").GetArrayLength().Should().Be(0);
        var hidden = scan.GetProperty("notSame").EnumerateArray().Should().ContainSingle().Subject;
        hidden.GetProperty("recordIds").EnumerateArray().Select(i => i.GetGuid()).Should().BeEquivalentTo(new[] { father, son });
        (await GetJson(t.Owner, $"/api/contacts/{father}/lookalikes")).GetArrayLength().Should().Be(0,
            "the contact page does not ask \"is this the same person?\" again either");

        var again = await t.Owner.PostAsync("/api/duplicates/contacts/not-same/clear", Body(new { recordIds = new[] { father, son } }));
        again.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetJson(t.Owner, "/api/duplicates/contacts")).GetProperty("groups").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task TheContactPageAsksAboutTheSamePeopleTheDuplicatesPageFinds()
    {
        var t = await TeamAsync();
        using var _ = t.Factory;
        var marc = await AddContactAsync(t, "Marc Dubois", "marc@gmail.com", "+41 79 123 45 67");
        var marcAtWork = await AddContactAsync(t, "Marc  Dubois", "m.dubois@firm.ch", "079 123 45 67");
        await AddContactAsync(t, "Marc Dubois", "marc@bank.ch", "+41 78 555 55 55");

        var lookalikes = await GetJson(t.Owner, $"/api/contacts/{marc}/lookalikes");

        lookalikes.EnumerateArray().Select(l => l.GetProperty("id").GetGuid()).Should().Equal(new[] { marcAtWork },
            "the same phone however it is written; the third Marc has another number");
        var fields = lookalikes[0].GetProperty("fields").EnumerateArray()
            .ToDictionary(f => f.GetProperty("field").GetString()!, f => f.GetProperty("same").GetBoolean());
        fields.Should().BeEquivalentTo(new Dictionary<string, bool> { ["Name"] = true, ["Email"] = false, ["Phone"] = true });
    }

    [Fact]
    public async Task ADecisionNamesTwoToFiftyRecordsOfThisWorkspace_AndAViewerCannotMakeOne()
    {
        var t = await TeamAsync();
        using var _ = t.Factory;
        var a = await AddContactAsync(t, "Alex Meier", "alex@meier.ch");
        var b = await AddContactAsync(t, "Alex Meier", "alex.junior@meier.ch");

        (await t.Owner.PostAsync("/api/duplicates/contacts/not-same", Body(new { recordIds = new[] { a } })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await t.Owner.PostAsync("/api/duplicates/spaceships/not-same", Body(new { recordIds = new[] { a, b } })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var foreign = await t.Owner.PostAsync("/api/duplicates/contacts/not-same", Body(new { recordIds = new[] { a, Guid.NewGuid() } }));
        foreign.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await foreign.Content.ReadAsStringAsync()).Should().Contain("Dedupe.RecordsNotInWorkspace");
        (await t.Viewer.PostAsync("/api/duplicates/contacts/not-same", Body(new { recordIds = new[] { a, b } })))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden, "a Viewer reads, and does not decide");

        (await t.Factory.Services.Read(db => db.DuplicateDecisions.Count())).Should().Be(0);
    }

    [Fact]
    public async Task AnotherWorkspaceSeesNeitherTheDuplicatesNorTheDecisions()
    {
        var t = await TeamAsync();
        using var _ = t.Factory;
        var a = await AddContactAsync(t, "Alex Meier", "alex@meier.ch", "044 123 45 67");
        var b = await AddContactAsync(t, "Alex Meier", "alex.junior@meier.ch", "044 123 45 67");
        (await t.Owner.PostAsync("/api/duplicates/contacts/not-same", Body(new { recordIds = new[] { a, b } }))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var (stranger, strangerId) = await SignInAsync(t.Factory, "stranger@example.ch");
        var elsewhere = await CreateAsync(t.Factory, strangerId, "Elsewhere");
        stranger.In(elsewhere);
        var scan = await GetJson(stranger, "/api/duplicates/contacts");
        scan.GetProperty("groups").GetArrayLength().Should().Be(0);
        scan.GetProperty("notSame").GetArrayLength().Should().Be(0);
        (await GetJson(stranger, "/api/duplicates/contacts/not-same")).GetArrayLength().Should().Be(0);
        (await stranger.PostAsync("/api/duplicates/contacts/not-same/clear", Body(new { recordIds = new[] { a, b } }))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await t.Factory.Services.Read(db => db.DuplicateDecisions.Count())).Should().Be(1, "clearing in one workspace cannot touch another's decisions");
    }

    [Fact]
    public async Task DuplicateCompaniesAreFoundByDomainOrName()
    {
        var t = await TeamAsync();
        using var _ = t.Factory;
        Guid acme, acmeAgain;
        using (var scope = t.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Company C(string name, string? domain, string? website = null) => new()
            {
                Id = Guid.NewGuid(), UserId = t.OwnerId, OrganizationId = t.Org, Name = name, Domain = domain, Website = website, CreatedAtUtc = DateTime.UtcNow,
            };
            var a = C("Acme AG", "acme.ch");
            var b = C("ACME", null, "https://www.acme.ch/about");
            acme = a.Id;
            acmeAgain = b.Id;
            db.Companies.AddRange(a, b, C("Acme AG", "acme.com"), C("Globex", "globex.ch"));
            await db.SaveChangesAsync();
        }

        var scan = await GetJson(t.Owner, "/api/duplicates/companies");

        var group = scan.GetProperty("groups").EnumerateArray().Should().ContainSingle().Subject;
        Ids(group).Should().BeEquivalentTo(new[] { acme, acmeAgain }, "acme.com is another company of the same name");
        group.GetProperty("reason").GetString().Should().Be("Same domain");
    }

    [Fact]
    public async Task KindsFoundInTheBrowserKeepTheirDecisionsOnTheServerToo()
    {
        var t = await TeamAsync();
        using var _ = t.Factory;
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        using (var scope = t.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Leads.Add(new Lead { Id = a, UserId = t.OwnerId, OrganizationId = t.Org, Name = "Jane Roe", Email = "jane@one.ch", CreatedAtUtc = DateTime.UtcNow });
            db.Leads.Add(new Lead { Id = b, UserId = t.OwnerId, OrganizationId = t.Org, Name = "Jane Roe", Email = "jane@two.ch", CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        (await t.Owner.PostAsync("/api/duplicates/leads/not-same", Body(new { recordIds = new[] { a, b } }))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await t.Owner.PostAsync("/api/duplicates/leads/not-same", Body(new { recordIds = new[] { b, a } }))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var pairs = await GetJson(t.Member, "/api/duplicates/leads/not-same");
        var pair = pairs.EnumerateArray().Should().ContainSingle("marking the same pair twice keeps one decision").Subject;
        new[] { pair.GetProperty("recordIdA").GetGuid(), pair.GetProperty("recordIdB").GetGuid() }.Should().BeEquivalentTo(new[] { a, b });
        // A member's or household's id is not checked against the club's records.
        (await t.Owner.PostAsync("/api/duplicates/households/not-same", Body(new { recordIds = new[] { Guid.NewGuid(), Guid.NewGuid() } })))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task EverySaveWritesTheKeysAContactIsMatchedOn()
    {
        var t = await TeamAsync();
        using var _ = t.Factory;
        var created = await t.Owner.PostAsync("/api/contacts", Body(new { name = "  Zoë   Martin ", email = "zoe@example.ch", phone = "+41 79 123 45 67" }));
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var id = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

        var saved = await t.Factory.Services.Read(db => db.Contacts.AsNoTracking().Single(c => c.Id == id));
        saved.PhoneKey.Should().Be("791234567");
        saved.NameKey.Should().Be("zoe martin");

        (await t.Owner.PutAsync($"/api/contacts/{id}", Body(new { name = "Z", phone = "079 999 88 77" }))).StatusCode.Should().Be(HttpStatusCode.OK);
        var edited = await t.Factory.Services.Read(db => db.Contacts.AsNoTracking().Single(c => c.Id == id));
        edited.PhoneKey.Should().Be("799998877");
        edited.NameKey.Should().Be("", "a name too short to match on is keyed as empty, not left to be filled");
    }

    [Fact]
    public async Task ContactsSavedBeforeTheKeysExistedAreGivenThemAtStart()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        await using var db = SqliteDb.Open(connection);
        var user = new User { Id = Guid.NewGuid(), Name = "Owner", Email = "owner@example.ch", CreatedAtUtc = DateTime.UtcNow };
        db.Users.Add(user);
        for (var i = 0; i < 1200; i++)
            db.Contacts.Add(new Contact { Id = Guid.NewGuid(), UserId = user.Id, Name = $"Person {i}", Email = $"p{i}@example.ch", Phone = "+41 79 000 00 01", CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        // As a database from before the columns, where no save had written them.
        await db.Contacts.ExecuteUpdateAsync(s => s.SetProperty(c => c.NameKey, (string?)null).SetProperty(c => c.PhoneKey, (string?)null));
        db.ChangeTracker.Clear();

        (await ContactMatchKeys.FillMissingAsync(db)).Should().Be(1200);
        (await ContactMatchKeys.FillMissingAsync(db)).Should().Be(0, "the next start has nothing left to do");
        (await db.Contacts.CountAsync(c => c.NameKey == null || c.PhoneKey != "790000001")).Should().Be(0);
    }

    [Fact]
    public void TheDdlGivesAnExistingContactsTableTheKeysAndTheirIndexes()
    {
        var ddl = ClubSchemaTests.ProgramDdl();
        foreach (var (column, type) in new[] { ("PhoneKey", "nvarchar(16)"), ("NameKey", "nvarchar(256)") })
        {
            var guard = ddl.IndexOf($"IF COL_LENGTH(N'dbo.Contacts', N'{column}') IS NULL", StringComparison.Ordinal);
            guard.Should().BeGreaterThan(-1, $"production's Contacts table has no {column}, and only an ALTER gives it one");
            var alter = ddl.IndexOf($"ALTER TABLE [Contacts] ADD [{column}] {type} NULL", guard, StringComparison.Ordinal);
            alter.Should().BeGreaterThan(guard);
            ddl[guard..alter].Should().NotContain(";");
            // The table comes from the migrations, so the general index check skips it.
            ddl.Should().Contain($"CREATE INDEX [IX_Contacts_OrganizationId_{column}] ON [Contacts] ([OrganizationId], [{column}])");
        }
        ddl.Should().Contain("CREATE TABLE [DuplicateDecisions]");
    }

    [SqlServerFact]
    public async Task OnSqlServer_DuplicatesAreFoundAndDecidedInSql()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, ownerId) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var created = await owner.PostAsync("/api/organizations", Body(new { name = "Pavillon" }));
            created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
            var org = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
            owner.In(org);
            Guid marc, marcAtWork;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Contact C(string name, string email, string? phone) => new()
                {
                    Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Name = name, Email = email, Phone = phone, CreatedAtUtc = DateTime.UtcNow,
                };
                var a = C("Marc Dubois", "marc@gmail.com", "+41 79 123 45 67");
                var b = C("Marc Dubois", "m.dubois@firm.ch", "079 123 45 67");
                marc = a.Id;
                marcAtWork = b.Id;
                db.Contacts.AddRange(a, b, C("Anna Keller", "anna@example.ch", null), C("Zoë Martin", "zoe@example.ch", "+41 78 111 11 11"));
                await db.SaveChangesAsync();
            }

            var scan = await GetJson(owner, "/api/duplicates/contacts");
            Ids(scan.GetProperty("groups").EnumerateArray().Single()).Should().BeEquivalentTo(new[] { marc, marcAtWork });
            (await GetJson(owner, $"/api/contacts/{marc}/lookalikes")).GetArrayLength().Should().Be(1);
            (await owner.PostAsync("/api/duplicates/contacts/not-same", Body(new { recordIds = new[] { marc, marcAtWork } }))).StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await GetJson(owner, "/api/duplicates/contacts")).GetProperty("groups").GetArrayLength().Should().Be(0);
            (await GetJson(owner, $"/api/contacts/{marc}/lookalikes")).GetArrayLength().Should().Be(0);
            (await GetJson(owner, "/api/duplicates/companies")).GetProperty("groups").GetArrayLength().Should().Be(0);

            (await SqlServerApp.ScalarAsync(database,
                "SELECT COUNT(*) FROM sys.indexes WHERE name IN ('IX_Contacts_OrganizationId_PhoneKey', 'IX_Contacts_OrganizationId_NameKey', 'IX_DuplicateDecisions_OrganizationId_Kind')"))
                .Should().Be(3);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task OnSqlServer_AnExistingDatabaseIsGivenTheKeysAndTheDecisionsTable()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using (SqlServerApp.Start(database)) { }
            // As production is before this deploy: a contact, and none of the new schema.
            await SqlServerApp.ExecuteAsync(database, "DROP TABLE [DuplicateDecisions]");
            await SqlServerApp.ExecuteAsync(database, "DROP INDEX [IX_Contacts_OrganizationId_PhoneKey] ON [Contacts]");
            await SqlServerApp.ExecuteAsync(database, "DROP INDEX [IX_Contacts_OrganizationId_NameKey] ON [Contacts]");
            await SqlServerApp.ExecuteAsync(database, "ALTER TABLE [Contacts] DROP COLUMN [PhoneKey], [NameKey]");
            var userId = Guid.NewGuid();
            await SqlServerApp.ExecuteAsync(database,
                $"INSERT INTO [Users] ([Id], [Email], [Name], [PasswordHash], [CreatedAtUtc]) VALUES ('{userId}', 'old@example.ch', 'Old', 'x', SYSUTCDATETIME())");
            await SqlServerApp.ExecuteAsync(database,
                $"INSERT INTO [Contacts] ([Id], [UserId], [Name], [Email], [Phone], [CreatedAtUtc], [IsArchived], [DoNotContact]) VALUES (NEWID(), '{userId}', N'Zoë Martin', 'zoe@example.ch', '+41 79 123 45 67', SYSUTCDATETIME(), 0, 0)");

            using (SqlServerApp.Start(database))
            {
                (await SqlServerApp.ScalarAsync(database, "SELECT OBJECT_ID(N'dbo.DuplicateDecisions', N'U')")).Should().NotBeNull();
                (await SqlServerApp.ScalarAsync(database, "SELECT [PhoneKey] FROM [Contacts]")).Should().Be("791234567");
                (await SqlServerApp.ScalarAsync(database, "SELECT [NameKey] FROM [Contacts]")).Should().Be("zoe martin");
                (await SqlServerApp.ScalarAsync(database,
                    "SELECT COUNT(*) FROM sys.indexes WHERE name IN ('IX_Contacts_OrganizationId_PhoneKey', 'IX_Contacts_OrganizationId_NameKey')")).Should().Be(2);
            }
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
