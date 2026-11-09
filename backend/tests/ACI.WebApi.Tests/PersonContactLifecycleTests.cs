using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using ACI.Infrastructure.Repositories;
using ACI.WebApi.Services;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Every lead with a name and an email has a person contact. Reading a list never makes
/// one, and a contact someone deleted is not made again.
/// </summary>
/// <remarks>
/// The contacts and leads lists, and the lead page's relationships card, used to create
/// the missing contacts as they were read: a Viewer opening a page wrote to the database,
/// and a deleted contact came back as a new, empty one the next time anyone opened or
/// saved the lead. The data tests run on SQLite, which runs the delete's bulk updates.
/// </remarks>
public class PersonContactLifecycleTests
{
    private sealed record World(AppDbContext Db, User User, Organization Org);

    private static async Task<World> OpenAsync(SqliteConnection connection)
    {
        var db = SqliteDb.Open(connection);
        var user = new User { Id = Guid.NewGuid(), Name = "Owner", Email = "owner@example.ch", CreatedAtUtc = DateTime.UtcNow };
        var org = new Organization { Id = Guid.NewGuid(), Name = "Pavillon", OwnerUserId = user.Id, CreatedAtUtc = DateTime.UtcNow };
        db.AddRange(user, org);
        await db.SaveChangesAsync();
        return new World(db, user, org);
    }

    private static Lead NewLead(World w, string name, string email) => new()
    {
        Id = Guid.NewGuid(), UserId = w.User.Id, OrganizationId = w.Org.Id, Name = name, Email = email,
        Status = "New", CreatedAtUtc = DateTime.UtcNow,
    };

    [Fact]
    public async Task ReadingContactsLeadsAndALeadsRelationshipsCreatesNoContact()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon");
        owner.In(org);
        var (viewer, viewerId) = await SignInAsync(factory, "viewer@example.ch");
        await AddMemberAsync(factory, org, viewerId, OrgMemberRole.Viewer);
        viewer.In(org);

        // A lead with no contact, as a tool writing to the database leaves one.
        var leadId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Leads.Add(new Lead
            {
                Id = leadId, UserId = ownerId, OrganizationId = org, Name = "Jane Roe", Email = "jane@example.ch",
                Status = "New", CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        foreach (var client in new[] { owner, viewer })
        {
            foreach (var url in new[]
            {
                "/api/contacts?page=1&pageSize=20", "/api/contacts/all", "/api/contacts/search?q=jane",
                "/api/leads?page=1&pageSize=20", "/api/leads/all", $"/api/leads/{leadId}",
            })
            {
                var res = await client.GetAsync(url);
                res.StatusCode.Should().Be(HttpStatusCode.OK, $"{url}: {await res.Content.ReadAsStringAsync()}");
            }

            var relationships = await client.GetAsync($"/api/leads/{leadId}/relationships");
            var body = await relationships.Content.ReadAsStringAsync();
            relationships.StatusCode.Should().Be(HttpStatusCode.OK, body);
            using var doc = JsonDocument.Parse(body);
            doc.RootElement.GetProperty("contactId").ValueKind.Should().Be(JsonValueKind.Null, "the lead has no contact, and a read does not make one");
            doc.RootElement.GetProperty("contactDeleted").GetBoolean().Should().BeFalse("nobody deleted one");
        }

        (await factory.Services.Read(db => db.Contacts.Count())).Should().Be(0, "reading a list or a lead page writes nothing");
    }

    [Fact]
    public async Task ADeletedContactIsNotMadeAgainWhenItsLeadIsSavedOrTheBackfillRuns()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        var w = await OpenAsync(connection);
        await using var db = w.Db;
        var lead = NewLead(w, "Jane Roe", "jane@example.ch");
        db.Leads.Add(lead);
        await db.SaveChangesAsync();
        var contacts = new ContactRepository(db);

        await contacts.EnsurePersonContactAsync(lead);
        var contactId = lead.ConvertedToContactId;
        contactId.Should().NotBeNull("a lead with a name and an email gets its person contact");

        (await contacts.DeleteAsync(contactId!.Value, w.User.Id, w.Org.Id)).Should().BeTrue();

        db.ChangeTracker.Clear();
        var saved = await db.Leads.AsNoTracking().SingleAsync(l => l.Id == lead.Id);
        saved.ConvertedToContactId.Should().BeNull("the lead no longer points at a contact that is gone");
        saved.PersonContactRemovedAtUtc.Should().NotBeNull("the lead remembers that a person deleted its contact");

        // What a save of the lead does, then what the background job does.
        await contacts.EnsurePersonContactAsync(saved);
        (await contacts.BackfillPersonContactsAsync(500)).Should().Be(0);

        (await db.Contacts.CountAsync()).Should().Be(0, "the delete sticks: no new, empty Jane Roe");
    }

    [Fact]
    public async Task ALeadWhoseContactWasDeletedBeforeThisFixIsNotGivenANewOne()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        var w = await OpenAsync(connection);
        await using var db = w.Db;
        // Deleted earlier, when a delete left the lead pointing at the contact that was gone.
        var lead = NewLead(w, "Jane Roe", "jane@example.ch");
        lead.ConvertedToContactId = Guid.NewGuid();
        db.Leads.Add(lead);
        await db.SaveChangesAsync();

        await new ContactRepository(db).EnsurePersonContactAsync(lead);

        (await db.Contacts.CountAsync()).Should().Be(0);
        db.ChangeTracker.Clear();
        var saved = await db.Leads.AsNoTracking().SingleAsync(l => l.Id == lead.Id);
        saved.ConvertedToContactId.Should().BeNull();
        saved.PersonContactRemovedAtUtc.Should().NotBeNull("recorded as deleted, so later saves skip it at once");
    }

    [Fact]
    public async Task AnArchivedContactIsNotReplacedWithANewOne()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        var w = await OpenAsync(connection);
        await using var db = w.Db;
        // Two leads for the same person: the second one is linked to the contact the first made.
        var first = NewLead(w, "Jane Roe", "jane@example.ch");
        var second = NewLead(w, "Jane Roe", "jane@example.ch");
        db.Leads.AddRange(first, second);
        await db.SaveChangesAsync();
        var contacts = new ContactRepository(db);
        await contacts.EnsurePersonContactAsync(first);
        await contacts.EnsurePersonContactAsync(second);
        second.ConvertedToContactId.Should().Be(first.ConvertedToContactId);

        (await contacts.ArchiveAsync(first.ConvertedToContactId!.Value, w.User.Id, w.Org.Id)).Should().BeTrue();
        await contacts.EnsurePersonContactAsync(second);

        (await db.Contacts.CountAsync()).Should().Be(1, "an archived person is still the lead's contact, not one to make again");
    }

    [Fact]
    public async Task TheBackfillGivesAContactToEveryLeadThatShouldHaveOne_AndSkipsDeletedOnes()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        var w = await OpenAsync(connection);
        await using var db = w.Db;
        var plain = NewLead(w, "Ada Lovelace", "ada@example.ch");
        var deleted = NewLead(w, "Jane Roe", "jane@example.ch");
        deleted.PersonContactRemovedAtUtc = DateTime.UtcNow.AddDays(-1);
        var blank = NewLead(w, "   ", "blank@example.ch");
        db.Leads.AddRange(plain, deleted, blank);
        // Another workspace's lead: the job runs for every workspace.
        var otherUser = new User { Id = Guid.NewGuid(), Name = "Other", Email = "other@example.ch", CreatedAtUtc = DateTime.UtcNow };
        db.Users.Add(otherUser);
        var personal = new Lead { Id = Guid.NewGuid(), UserId = otherUser.Id, Name = "Grace Hopper", Email = "grace@example.ch", Status = "New", CreatedAtUtc = DateTime.UtcNow };
        db.Leads.Add(personal);
        await db.SaveChangesAsync();

        var linked = await new ContactRepository(db).BackfillPersonContactsAsync(500);

        linked.Should().Be(2);
        var made = await db.Contacts.AsNoTracking().ToListAsync();
        made.Select(c => c.Email).Should().BeEquivalentTo(new[] { "ada@example.ch", "grace@example.ch" });
        made.Single(c => c.Email == "grace@example.ch").Should().Match<Contact>(c => c.UserId == otherUser.Id && c.OrganizationId == null,
            "each contact is made in its lead's own workspace");
        (await new ContactRepository(db).BackfillPersonContactsAsync(500)).Should().Be(0, "a second run finds nothing left to do");
    }

    [Fact]
    public async Task TheBackgroundJobRunsTheBackfill()
    {
        using var factory = new CustomWebApplicationFactory();
        var (_, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Leads.Add(new Lead { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Name = "Jane Roe", Email = "jane@example.ch", Status = "New", CreatedAtUtc = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var job = new PersonContactBackfillBackgroundService(
            factory.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<PersonContactBackfillBackgroundService>.Instance);
        (await job.RunOnceAsync(CancellationToken.None)).Should().Be(1);

        (await factory.Services.Read(db => db.Contacts.Count(c => c.Email == "jane@example.ch" && c.OrganizationId == org))).Should().Be(1);
    }

    [Fact]
    public async Task ConvertingALeadIntoAContactClearsTheNoteThatItsContactWasDeleted()
    {
        using var factory = new CustomWebApplicationFactory();
        var (owner, ownerId) = await SignInAsync(factory, "owner@example.ch");
        var org = await CreateAsync(factory, ownerId, "Pavillon");
        owner.In(org);
        var leadId = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Leads.Add(new Lead
            {
                Id = leadId, UserId = ownerId, OrganizationId = org, Name = "Jane Roe", Email = "jane@example.ch",
                Status = "Qualified", CreatedAtUtc = DateTime.UtcNow, PersonContactRemovedAtUtc = DateTime.UtcNow.AddDays(-2),
            });
            await db.SaveChangesAsync();
        }

        var res = await owner.PostAsync($"/api/leads/{leadId}/convert", Body(new { createContact = true }));
        res.StatusCode.Should().Be(HttpStatusCode.OK, await res.Content.ReadAsStringAsync());

        var lead = await factory.Services.Read(db => db.Leads.AsNoTracking().Single(l => l.Id == leadId));
        lead.ConvertedToContactId.Should().NotBeNull();
        lead.PersonContactRemovedAtUtc.Should().BeNull("the person asked for the lead to become a contact");
    }

    [SqlServerFact]
    public async Task OnSqlServer_ADeletedLeadContactStaysDeleted()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var created = await owner.PostAsync("/api/organizations", Body(new { name = "Pavillon" }));
            created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
            var org = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
            owner.In(org);

            var leadRes = await owner.PostAsync("/api/leads", Body(new { name = "Jane Roe", email = "jane@example.ch", status = "New" }));
            leadRes.StatusCode.Should().Be(HttpStatusCode.OK, await leadRes.Content.ReadAsStringAsync());
            var leadId = JsonDocument.Parse(await leadRes.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();

            var relationships = JsonDocument.Parse(await (await owner.GetAsync($"/api/leads/{leadId}/relationships")).Content.ReadAsStringAsync()).RootElement;
            var contactId = relationships.GetProperty("contactId").GetGuid();

            (await owner.DeleteAsync($"/api/contacts/{contactId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

            // Opening the lead page, then saving the lead, as a colleague would the next day.
            var after = JsonDocument.Parse(await (await owner.GetAsync($"/api/leads/{leadId}/relationships")).Content.ReadAsStringAsync()).RootElement;
            after.GetProperty("contactId").ValueKind.Should().Be(JsonValueKind.Null);
            after.GetProperty("contactDeleted").GetBoolean().Should().BeTrue();
            var put = await owner.PutAsync($"/api/leads/{leadId}", Body(new { name = "Jane Roe", email = "jane@example.ch", phone = "+41 79 123 45 67" }));
            put.StatusCode.Should().Be(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
            var add = await owner.PostAsync($"/api/leads/{leadId}/relationships", Body(new { relatedContactId = Guid.NewGuid(), type = "spouse" }));
            add.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            (await add.Content.ReadAsStringAsync()).Should().Contain("ContactRelationship.LeadContactDeleted");

            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [Contacts]")).Should().Be(0, "the delete stuck");
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [Leads] WHERE [Id] = '{leadId}' AND [PersonContactRemovedAtUtc] IS NOT NULL")).Should().Be(1);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [Fact]
    public void AnExistingLeadsTableIsGivenTheNoteByAGuardedAlter()
    {
        var ddl = ClubSchemaTests.ProgramDdl();
        var guard = ddl.IndexOf("IF COL_LENGTH(N'dbo.Leads', N'PersonContactRemovedAtUtc') IS NULL", StringComparison.Ordinal);
        guard.Should().BeGreaterThan(-1, "production's Leads table has no such column, and only an ALTER gives it one");
        var alter = ddl.IndexOf("ALTER TABLE [Leads] ADD [PersonContactRemovedAtUtc] datetime2 NULL", guard, StringComparison.Ordinal);
        alter.Should().BeGreaterThan(guard);
        ddl[guard..alter].Should().NotContain(";", "the ALTER belongs to that IF");
    }

    [SqlServerFact]
    public async Task OnSqlServer_AnExistingDatabaseIsGivenTheNoteOnStart()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using (SqlServerApp.Start(database))
                (await SqlServerApp.ScalarAsync(database, "SELECT COL_LENGTH(N'dbo.Leads', N'PersonContactRemovedAtUtc')")).Should().NotBeNull("a new database has it");
            await SqlServerApp.ExecuteAsync(database, "ALTER TABLE [Leads] DROP COLUMN [PersonContactRemovedAtUtc]");

            using (SqlServerApp.Start(database))
                (await SqlServerApp.ScalarAsync(database, "SELECT COL_LENGTH(N'dbo.Leads', N'PersonContactRemovedAtUtc')")).Should().NotBeNull("an existing one is given it");
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
