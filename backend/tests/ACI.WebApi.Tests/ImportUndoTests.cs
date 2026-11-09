using System.Net;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.ImportFiles;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// Undoing an import: it removes what the import created and nobody has touched since,
/// and nothing else.
/// </summary>
/// <remarks>
/// Before batches, a wrong file could only be cleaned up by deleting its rows one by one,
/// among records that had been there before it.
/// </remarks>
public class ImportUndoTests
{
    // Eva writes from her firm's own domain, so saving her adds a company named after it
    // (as saving any lead does); Bruno and Chiara use a personal inbox, which adds none.
    private const string File =
        "name,email,company\n"
        + "Alice Keller,alice@keller.example,Acme SA\n"
        + "Bruno Frei,bruno.frei@bluewin.ch,Acme SA\n"
        + "Chiara Rossi,chiara.rossi@bluewin.ch,\n"
        + "Daniel Weber,daniel@weber.example,Beta AG\n"
        + "Eva Graf,eva@graf.example,\n";

    private static readonly string?[] Columns = { "name", "email", "company" };

    private static async Task<(HttpClient Client, Guid UserId, Guid OrgId)> WorkspaceAsync(CustomWebApplicationFactory factory)
    {
        var (client, userId) = await SignInAsync(factory, "owner@example.ch");
        var orgId = await CreateAsync(factory, userId, "Pavillon46");
        client.In(orgId);
        return (client, userId, orgId);
    }

    private static Task<Guid> LeadIdAsync(CustomWebApplicationFactory factory, string email) =>
        factory.Services.Read(db => db.Leads.Single(l => l.Email == email).Id);

    [Fact]
    public async Task UndoRemovesWhatTheImportCreatedAndKeepsWhatWasWorkedOnSince()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await WorkspaceAsync(factory);
        var batchId = (await OkJson(await Import(client, "lead", File, Columns), "import")).GetProperty("batchId").GetGuid();

        // Since the import: a call logged with Bruno, and Chiara's lead edited.
        var bruno = await LeadIdAsync(factory, "bruno.frei@bluewin.ch");
        await OkJson(await client.PostAsync("/api/activities", Body(new { type = "call", subject = "Called", leadId = bruno })), "log a call");
        var chiara = await LeadIdAsync(factory, "chiara.rossi@bluewin.ch");
        await OkJson(await client.PutAsync($"/api/leads/{chiara}", Body(new { jobTitle = "Sommelier" })), "edit a lead");

        var preview = await OkJson(await client.GetAsync($"/api/imports/{batchId}/undo"), "undo preview");

        preview.GetProperty("dryRun").GetBoolean().Should().BeTrue();
        preview.GetProperty("leads").GetInt32().Should().Be(3, "Alice, Daniel and Eva are untouched");
        preview.GetProperty("contacts").GetInt32().Should().Be(3, "with the contacts made for them");
        preview.GetProperty("companies").GetInt32().Should().Be(2, "Beta AG, which only Daniel was at, and the company named after Eva's domain");
        var kept = preview.GetProperty("kept").EnumerateArray()
            .ToDictionary(k => $"{k.GetProperty("kind").GetString()}:{k.GetProperty("label").GetString()}", k => k.GetProperty("reason").GetString());
        kept.Should().Contain("lead:Bruno Frei", "has activities logged");
        kept.Should().Contain("lead:Chiara Rossi", "was changed after the import");
        kept.Should().Contain("contact:Bruno Frei", "its lead stays");
        kept.Should().Contain("company:Acme SA", "a lead that stays is at this company");
        (await factory.Services.Read(db => db.Leads.Count())).Should().Be(5, "a preview removes nothing");

        var undone = await OkJson(await client.PostAsync($"/api/imports/{batchId}/undo", null), "undo");
        undone.GetProperty("leads").GetInt32().Should().Be(3);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Leads.Select(l => l.Name).OrderBy(n => n).ToListAsync()).Should().Equal("Bruno Frei", "Chiara Rossi");
            (await db.Contacts.Select(c => c.Name).OrderBy(n => n).ToListAsync()).Should().Equal("Bruno Frei", "Chiara Rossi");
            (await db.Companies.Select(c => c.Name).ToListAsync()).Should().Equal("Acme SA");
            (await db.AuditEvents.CountAsync(e => e.EntityType == "lead" && e.Action == "deleted")).Should().Be(3, "the log says what went");
            (await db.LeadStatusEvents.CountAsync()).Should().Be(2, "a removed lead's status history goes with it");
        }

        var batch = (await OkJson(await client.GetAsync("/api/imports?kind=lead"), "imports")).EnumerateArray().Single();
        batch.GetProperty("removed").GetInt32().Should().Be(8);
        batch.GetProperty("remaining").GetInt32().Should().Be(5);
        batch.GetProperty("undoneAtUtc").ValueKind.Should().NotBe(JsonValueKind.Null);
        batch.GetProperty("canUndo").GetBoolean().Should().BeTrue("what was kept can be undone later, once nobody needs it");
    }

    [Fact]
    public async Task UndoLeavesWhatWasHereBeforeAndWhatTheImportOnlyUpdated()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await WorkspaceAsync(factory);
        var acme = (await OkJson(await client.PostAsync("/api/companies", Body(new { name = "Acme SA" })), "add a company")).GetProperty("id").GetGuid();
        var bruno = (await OkJson(await client.PostAsync("/api/leads", Body(new { name = "Bruno Frei", email = "bruno.frei@bluewin.ch", status = "New" })), "add a lead"))
            .GetProperty("id").GetGuid();

        var result = await OkJson(await Import(client, "lead", File, Columns, duplicates: "update"), "import");
        Action(result, 3).Should().Be("update", "Bruno was already here");
        await OkJson(await client.PostAsync($"/api/imports/{result.GetProperty("batchId").GetGuid()}/undo", null), "undo");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Leads.Select(l => l.Name).ToListAsync()).Should().Equal("Bruno Frei");
        (await db.Leads.SingleAsync()).CompanyId.Should().Be(acme, "the import's change to him stays; undo removes only what it created");
        (await db.Companies.Select(c => c.Id).ToListAsync()).Should().Equal(acme);
        (await db.Leads.SingleAsync(l => l.Id == bruno)).Should().NotBeNull();
    }

    [Fact]
    public async Task AContactThatWasHereBeforeStaysWhenTheLeadLinkedToItGoes()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await WorkspaceAsync(factory);
        var eva = (await OkJson(await client.PostAsync("/api/contacts", Body(new { name = "Eva Graf", email = "eva@graf.example" })), "add a contact"))
            .GetProperty("id").GetGuid();
        var batchId = (await OkJson(await Import(client, "lead", File, Columns), "import")).GetProperty("batchId").GetGuid();
        (await factory.Services.Read(db => db.Contacts.Single(c => c.Id == eva).ConvertedFromLeadId)).Should().NotBeNull("her lead was given her contact");

        var preview = await OkJson(await client.GetAsync($"/api/imports/{batchId}/undo"), "undo preview");
        preview.GetProperty("leads").GetInt32().Should().Be(5);
        preview.GetProperty("contacts").GetInt32().Should().Be(4, "Eva's contact was not made by the import");
        await OkJson(await client.PostAsync($"/api/imports/{batchId}/undo", null), "undo");

        var contact = await factory.Services.Read(db => db.Contacts.Single());
        contact.Id.Should().Be(eva);
        contact.ConvertedFromLeadId.Should().BeNull();
    }

    /// <summary>A batch as an import leaves it part way through, with leads it has saved.</summary>
    private static async Task<(Guid Batch, Guid[] Leads)> UnfinishedAsync(
        CustomWebApplicationFactory factory, Guid userId, Guid orgId, DateTime started, params TimeSpan?[] customValuesAfter)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var batch = new ImportBatch
        {
            Id = Guid.NewGuid(), OrganizationId = orgId, UserId = userId, EntityType = "lead", FileName = "big.csv",
            TotalRows = 2000, StartedAtUtc = started,
        };
        db.ImportBatches.Add(batch);
        var leads = new List<Guid>();
        for (var i = 0; i < customValuesAfter.Length; i++)
        {
            var created = started.AddSeconds(5 + i);
            var lead = new Lead
            {
                Id = Guid.NewGuid(), UserId = userId, OrganizationId = orgId, Name = $"Person {i}", Email = $"p{i}@bluewin.ch",
                Status = "New", CreatedAtUtc = created, ImportBatchId = batch.Id,
            };
            db.Leads.Add(lead);
            if (customValuesAfter[i] is { } after)
                db.CustomFieldValues.Add(new CustomFieldValueSet
                {
                    Id = Guid.NewGuid(), OrganizationId = orgId, OwnerUserId = userId, EntityType = "lead", RecordId = lead.Id,
                    ValuesJson = "{}", UpdatedAtUtc = created + after,
                });
            leads.Add(lead.Id);
        }
        await db.SaveChangesAsync();
        return (batch.Id, leads.ToArray());
    }

    [Fact]
    public async Task AnImportStillRunningCannotBeUndoneYet()
    {
        // Undoing it then would remove rows while later ones are still being linked to them.
        await using var factory = new CustomWebApplicationFactory();
        var (client, userId, orgId) = await WorkspaceAsync(factory);
        var (batchId, _) = await UnfinishedAsync(factory, userId, orgId, DateTime.UtcNow.AddMinutes(-1), (TimeSpan?)null);

        (await client.GetAsync($"/api/imports/{batchId}/undo")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.PostAsync($"/api/imports/{batchId}/undo", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await factory.Services.Read(db => db.Leads.Count())).Should().Be(1);
        var listed = (await OkJson(await client.GetAsync("/api/imports"), "imports")).EnumerateArray().Single();
        listed.GetProperty("running").GetBoolean().Should().BeTrue();
        listed.GetProperty("canUndo").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task OnlyWhoeverRanItOrAManagerCanUndoIt()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (owner, _, orgId) = await WorkspaceAsync(factory);
        var batchId = (await OkJson(await Import(owner, "lead", File, Columns), "import")).GetProperty("batchId").GetGuid();
        var (member, memberId) = await SignInAsync(factory, "member@example.ch");
        await AddMemberAsync(factory, orgId, memberId, OrgMemberRole.Member);
        member.In(orgId);
        var (manager, managerId) = await SignInAsync(factory, "manager@example.ch");
        await AddMemberAsync(factory, orgId, managerId, OrgMemberRole.Manager);
        manager.In(orgId);

        (await member.GetAsync($"/api/imports/{batchId}/undo")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await member.PostAsync($"/api/imports/{batchId}/undo", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await OkJson(await member.GetAsync("/api/imports"), "the member's list")).EnumerateArray().Single()
            .GetProperty("canUndo").GetBoolean().Should().BeFalse();
        (await factory.Services.Read(db => db.Leads.Count())).Should().Be(5);

        var own = (await OkJson(await Import(member, "lead", "name,email\nFritz Huber,fritz.huber@bluewin.ch\n", new[] { "name", "email" }), "the member's import"))
            .GetProperty("batchId").GetGuid();
        await OkJson(await member.PostAsync($"/api/imports/{own}/undo", null), "the member undoes their own");

        await OkJson(await manager.PostAsync($"/api/imports/{batchId}/undo", null), "a manager undoes anyone's");
        (await factory.Services.Read(db => db.Leads.Count())).Should().Be(0);
    }

    [Fact]
    public async Task AnotherWorkspacesImportCannotBeSeenOrUndone()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (owner, _, _) = await WorkspaceAsync(factory);
        var batchId = (await OkJson(await Import(owner, "lead", File, Columns), "import")).GetProperty("batchId").GetGuid();
        var (stranger, strangerId) = await SignInAsync(factory, "stranger@example.ch");
        stranger.In(await CreateAsync(factory, strangerId, "Elsewhere"));

        (await stranger.GetAsync($"/api/imports/{batchId}/undo")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await stranger.PostAsync($"/api/imports/{batchId}/undo", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await OkJson(await stranger.GetAsync("/api/imports"), "their list")).GetArrayLength().Should().Be(0);
        (await factory.Services.Read(db => db.Leads.Count())).Should().Be(5);
    }

    [Fact]
    public async Task AContactThatWasEmailedOrRelatedStaysAndKeepsItsCompany()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await WorkspaceAsync(factory);
        var csv = "name,email,company\nMarc Dubois,marc@dubois.example,Dubois Vins\nLea Dubois,lea@dubois.example,\nTom Ott,tom@ott.example,\n";
        var batchId = (await OkJson(await Import(client, "contact", csv, Columns), "import")).GetProperty("batchId").GetGuid();
        var ids = await factory.Services.Read(db => db.Contacts.ToDictionary(c => c.Name, c => c.Id));
        await OkJson(await client.PostAsync($"/api/contacts/{ids["Marc Dubois"]}/relationships", Body(new { relatedContactId = ids["Lea Dubois"], type = "spouse" })), "record a spouse");

        var undone = await OkJson(await client.PostAsync($"/api/imports/{batchId}/undo", null), "undo");

        undone.GetProperty("contacts").GetInt32().Should().Be(1, "only Tom is untouched");
        (await factory.Services.Read(db => db.Contacts.Select(c => c.Name).OrderBy(n => n).ToList())).Should().Equal("Lea Dubois", "Marc Dubois");
        (await factory.Services.Read(db => db.Companies.Select(c => c.Name).ToList())).Should().Equal("Dubois Vins");
    }
}
