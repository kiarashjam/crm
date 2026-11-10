using System.Net;
using System.Text.Json;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using ACI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static ACI.WebApi.Tests.Helpers.ImportFiles;
using static ACI.WebApi.Tests.Helpers.Workspaces;

using ACI.WebApi.Tests.Helpers;

namespace ACI.WebApi.Tests;

/// <summary>
/// Importing a spreadsheet of leads on the server.
/// </summary>
/// <remarks>
/// The browser used to read the file and post a lead per row. It broke on the ";" Swiss
/// Excel writes, split a notes cell with a line break into two rows, dropped the company
/// column it had mapped, and could neither preview what would happen nor undo it.
/// </remarks>
public class LeadImportTests
{
    /// <summary>The app with an outbound webhook publisher that remembers what it was asked to publish.</summary>
    private sealed class WithWebhooks : CustomWebApplicationFactory
    {
        public readonly List<string> Published = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                var webhooks = new Mock<IOutboundWebhookPublisher>();
                webhooks.Setup(w => w.QueueAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                    .Callback((Guid? _, string name, object _, CancellationToken _) => Published.Add(name))
                    .Returns(Task.CompletedTask);
                services.AddScoped(_ => webhooks.Object);
            });
        }
    }

    // What Excel saves in Switzerland: ";" between columns, and a notes cell with a line break.
    private const string SwissFile =
        "Vorname;Nachname;E-Mail;Firma;Telefon;Notizen\r\n"
        + "Jean;Dupont;jean@dupont.example;Dupont & Fils SA;+41 79 123 45 67;\"Met at the gala\r\nCall after 5\"\r\n"
        + "Anne;Meier;anne@meier.example;;079 765 43 21;\r\n";

    private static readonly string?[] SwissColumns = { "firstName", "lastName", "email", "company", "phone", "description" };

    private static async Task<(HttpClient Client, Guid UserId, Guid OrgId)> WorkspaceAsync(CustomWebApplicationFactory factory, string email = "owner@example.ch")
    {
        var (client, userId) = await SignInAsync(factory, email);
        var orgId = await CreateAsync(factory, userId, "Pavillon46");
        client.In(orgId);
        return (client, userId, orgId);
    }

    private static async Task<Guid> AddLeadAsync(HttpClient client, object lead) =>
        (await OkJson(await client.PostAsync("/api/leads", Body(lead)), "add a lead")).GetProperty("id").GetGuid();

    [Fact]
    public async Task ASwissExcelFileIsReadWithItsColumnsSuggested()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await WorkspaceAsync(factory);

        var analysis = await OkJson(await Analyze(client, "lead", SwissFile), "analyze");

        analysis.GetProperty("separator").GetString().Should().Be("semicolon");
        analysis.GetProperty("rowCount").GetInt32().Should().Be(2, "the line break inside the quoted notes is part of the cell");
        analysis.GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("field").GetString())
            .Should().Equal(SwissColumns);
        analysis.GetProperty("columns")[5].GetProperty("samples")[0].GetString().Should().Be("Met at the gala\r\nCall after 5");
        analysis.GetProperty("fields").EnumerateArray().Select(f => f.GetProperty("key").GetString()).Should().Contain("company");
    }

    [Fact]
    public async Task AFileExportedFromTheLeadsPageImportsAgain()
    {
        // The Leads page's export, as it writes it: a byte-order mark, and a quote before a
        // phone number starting with "+" so a spreadsheet shows it as text.
        await using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await WorkspaceAsync(factory);
        var csv = "﻿Id,Name,Email,Phone,Company,Source,Status\n"
            + "7d3c5c1e-0000-4000-8000-000000000001,Jean Dupont,jean@dupont.example,'+41 79 123 45 67,,Website,New\n";
        var analysis = await OkJson(await Analyze(client, "lead", csv), "analyze");
        var columns = analysis.GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("field").GetString()).ToArray();

        var result = await OkJson(await Import(client, "lead", csv, columns), "import");

        Action(result, 2).Should().Be("create", Message(result, 2));
        (await factory.Services.Read(db => db.Leads.Single())).Phone.Should().Be("+41 79 123 45 67");
    }

    [Fact]
    public async Task TheDryRunSaysWhatEachRowWouldDoAndWritesNothing()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await WorkspaceAsync(factory);
        await AddLeadAsync(client, new { name = "Jean Dupont", email = "jean@dupont.example", phone = "079 123 45 67", status = "New" });
        await AddLeadAsync(client, new { name = "Marc Weber", email = "marc@weber.example", status = "New" });
        var before = await factory.Services.Read(db => db.Leads.Count());

        var csv = "name,email,phone\n"
            + "Marie Curie,marie@curie.example,\n"          // 2: new
            + "M. Weber,MARC@weber.example,\n"                // 3: the same email, in capitals
            + "Jean Dupont,jean.d@else.example,+41 79 123 45 67\n" // 4: the same name and phone, written another way
            + "Marie C.,marie@curie.example,\n"               // 5: repeats row 2
            + "Bob,not-an-email,\n"                           // 6: refused
            + "Anne Dupont,anne@dupont.example,+41 79 123 45 67\n"; // 7: shares the phone only, so she is someone else
        var preview = await OkJson(await Preview(client, "lead", csv, new[] { "name", "email", "phone" }), "preview");

        preview.GetProperty("dryRun").GetBoolean().Should().BeTrue();
        preview.GetProperty("batchId").ValueKind.Should().Be(JsonValueKind.Null);
        Action(preview, 2).Should().Be("create");
        Action(preview, 3).Should().Be("skip");
        Message(preview, 3).Should().Contain("Already in your leads").And.Contain("same email");
        Action(preview, 4).Should().Be("skip");
        Message(preview, 4).Should().Contain("same name and phone");
        Action(preview, 5).Should().Be("skip");
        Message(preview, 5).Should().Be("Same email as row 2");
        Action(preview, 6).Should().Be("error");
        Message(preview, 6).Should().Contain("is not an email address");
        Action(preview, 7).Should().Be("create", "a couple, or colleagues at one desk, share a phone");
        preview.GetProperty("created").GetInt32().Should().Be(2);
        preview.GetProperty("skipped").GetInt32().Should().Be(3);
        preview.GetProperty("failed").GetInt32().Should().Be(1);

        (await factory.Services.Read(db => db.Leads.Count())).Should().Be(before, "a dry run writes nothing");
        (await factory.Services.Read(db => db.ImportBatches.Count())).Should().Be(0);
    }

    [Fact]
    public async Task ImportedLeadsKeepTheirCompanyAndCarryTheirBatchAndSetNothingOff()
    {
        await using var factory = new WithWebhooks();
        var (client, _, _) = await WorkspaceAsync(factory);
        await OkJson(await client.PostAsync("/api/automations", Body(new
        {
            name = "Welcome", trigger = "lead.created", enabled = true,
            actions = new[] { new { type = "create_task", config = new { title = "Call the new lead", dueInDays = "1" } } },
        })), "add a rule");

        var result = await OkJson(await Import(client, "lead", SwissFile, SwissColumns), "import");

        var batchId = result.GetProperty("batchId").GetGuid();
        result.GetProperty("created").GetInt32().Should().Be(2);
        Notes(result, 2).Should().Contain("Adds the company “Dupont & Fils SA”");

        using var scope = factory.Services.CreateScope();
        using var db = scope.ServiceProvider.EveryWorkspace();
        var jean = await db.Leads.SingleAsync(l => l.Email == "jean@dupont.example");
        jean.Name.Should().Be("Jean Dupont", "a first and a last name column make the name");
        jean.Description.Should().Be("Met at the gala\r\nCall after 5");
        jean.Phone.Should().Be("+41 79 123 45 67");
        jean.ImportBatchId.Should().Be(batchId);
        var company = await db.Companies.SingleAsync(c => c.Id == jean.CompanyId);
        company.Name.Should().Be("Dupont & Fils SA", "the company column used to be mapped, shown and then dropped");
        company.ImportBatchId.Should().Be(batchId);
        (await db.Contacts.SingleAsync(c => c.ConvertedFromLeadId == jean.Id)).ImportBatchId
            .Should().Be(batchId, "the contact every lead is given came from the import too");

        // An imported list is people already known: no welcome, no webhook, no first-response clock.
        (await db.TaskItems.CountAsync()).Should().Be(0);
        factory.Published.Should().NotContain("lead.created");
        jean.FirstResponseDueAtUtc.Should().BeNull();

        var batch = await db.ImportBatches.SingleAsync(b => b.Id == batchId);
        batch.Should().Match<ImportBatch>(b => b.EntityType == "lead" && b.CreatedCount == 2 && b.TotalRows == 2 && b.FinishedAtUtc != null);
    }

    [Fact]
    public async Task AnImportCanAskForTheNewLeadAutomations()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await WorkspaceAsync(factory);
        await OkJson(await client.PostAsync("/api/automations", Body(new
        {
            name = "Welcome", trigger = "lead.created", enabled = true,
            actions = new[] { new { type = "create_task", config = new { title = "Call the new lead", dueInDays = "1" } } },
        })), "add a rule");

        await OkJson(await Import(client, "lead", SwissFile, SwissColumns, extra: new { runAutomations = true }), "import");

        (await factory.Services.Read(db => db.TaskItems.Count())).Should().Be(2);
    }

    [Fact]
    public async Task UpdatingWritesTheFilesValuesOverTheMatchWithoutClearingAnything()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (client, _, orgId) = await WorkspaceAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            using var db = scope.ServiceProvider.EveryWorkspace();
            db.LeadStatuses.AddRange(
                new LeadStatus { Id = Guid.NewGuid(), OrganizationId = orgId, Name = "New", DisplayOrder = 0 },
                new LeadStatus { Id = Guid.NewGuid(), OrganizationId = orgId, Name = "Contacted", DisplayOrder = 1 });
            await db.SaveChangesAsync();
        }
        await OkJson(await client.PostAsync("/api/automations", Body(new
        {
            name = "Moved on", trigger = "lead.status_changed", enabled = true,
            actions = new[] { new { type = "create_task", config = new { title = "Follow up", dueInDays = "1" } } },
        })), "add a rule");
        var jean = await AddLeadAsync(client, new { name = "Jean Dupont", email = "jean@dupont.example", phone = "079 123 45 67", status = "New", description = "Keep these notes" });

        var csv = "email,phone,job title,status,notes\n"
            + "jean@dupont.example,+41 79 123 45 67,Head of events,Contacted,\n";
        var columns = new[] { "email", "phone", "jobTitle", "status", "description" };
        var preview = await OkJson(await Preview(client, "lead", csv, columns, duplicates: "update"), "preview");

        Action(preview, 2).Should().Be("update");
        Changes(preview, 2).Should().BeEquivalentTo("Job title: empty → “Head of events”", "Status: “New” → “Contacted”");

        var result = await OkJson(await Import(client, "lead", csv, columns, duplicates: "update"), "import");
        result.GetProperty("updated").GetInt32().Should().Be(1);

        var lead = await factory.Services.Read(db => db.Leads.Single(l => l.Id == jean));
        lead.JobTitle.Should().Be("Head of events");
        lead.Status.Should().Be("Contacted");
        lead.Phone.Should().Be("079 123 45 67", "the same number written another way is not a change");
        lead.Description.Should().Be("Keep these notes", "an empty cell never clears a field");
        lead.ImportBatchId.Should().BeNull("the lead was already here; undoing the import must not remove it");
        (await factory.Services.Read(db => db.TaskItems.Count())).Should().Be(0,
            "the file records where people already are; it does not set off the status automations unless asked");
    }

    [Fact]
    public async Task ARowRefusedForWantingANameDoesNotHideALaterRowWithOne()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await WorkspaceAsync(factory);

        var csv = "email,name\nanna@roth.example,\nanna@roth.example,Anna Roth\n";
        var result = await OkJson(await Preview(client, "lead", csv, new[] { "email", "name" }, duplicates: "update"), "preview");

        Action(result, 2).Should().Be("error");
        Action(result, 3).Should().Be("create", "the row before it imported nothing, so this is not a repeat of it");
    }

    [Fact]
    public async Task TwoRowsReachingOneLeadByDifferentKeysDoNotBothUpdateIt()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await WorkspaceAsync(factory);
        await AddLeadAsync(client, new { name = "Bob Keller", email = "bob@keller.example", phone = "079 123 45 67", status = "New" });

        // The two rows share no key with each other, so neither repeats the other; each
        // reaches Bob by its own.
        var csv = "name,email,phone,job title\n"
            + "Robert Keller,bob@keller.example,,Chef\n"                          // by email
            + "Bob Keller,bob.work@else.example,+41 79 123 45 67,Sommelier\n";   // by name and phone
        var result = await OkJson(await Import(client, "lead", csv, new[] { "name", "email", "phone", "jobTitle" }, duplicates: "update"), "import");

        Action(result, 2).Should().Be("update");
        Action(result, 3).Should().Be("skip");
        Message(result, 3).Should().Be("Same lead as row 2: Bob Keller");
        var bob = await factory.Services.Read(db => db.Leads.Single());
        (bob.Name, bob.Email, bob.JobTitle).Should().Be(("Robert Keller", "bob@keller.example", "Chef"),
            "the second row would have written over the first, email included");
    }

    [Fact]
    public async Task SomeoneWhoSharesOnlyAPhoneIsAddedWithANoteToCheck()
    {
        // A phone alone is not a match: a couple share a landline, colleagues a switchboard.
        // The dry run names who else has the number, so a real duplicate can still be caught.
        await using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await WorkspaceAsync(factory);
        await AddLeadAsync(client, new { name = "Jean Dupont", email = "jean@dupont.example", phone = "079 123 45 67", status = "New" });

        var csv = "name,email,phone\n"
            + "Anne Dupont,anne@dupont.example,+41 79 123 45 67\n"  // 2: Jean's number, written another way
            + "Paul Keller,paul@keller.example,044 555 66 77\n"     // 3: a number nobody has
            + "Rita Keller,rita@keller.example,+41 44 555 66 77\n"  // 4: row 3's number
            + "Hans Muster,hans@muster.example,\n";                 // 5: no number
        var preview = await OkJson(await Preview(client, "lead", csv, new[] { "name", "email", "phone" }), "preview");

        Action(preview, 2).Should().Be("create");
        Notes(preview, 2).Should().Contain("Same phone number as “Jean Dupont”, already in your leads: check they are not the same person");
        Notes(preview, 3).Should().NotContain(n => n.Contains("phone number"));
        Action(preview, 4).Should().Be("create");
        Notes(preview, 4).Should().Contain("Same phone number as row 3: check they are not the same person");
        Notes(preview, 5).Should().NotContain(n => n.Contains("phone number"));
    }

    [Fact]
    public async Task TheDryRunAndTheImportRefuseTheSameRows()
    {
        // A status the workspace has, but longer than a lead's status may be. The dry run
        // and the import check rows the same way, so the preview never promises what the
        // import then refuses.
        await using var factory = new CustomWebApplicationFactory();
        var (client, _, orgId) = await WorkspaceAsync(factory);
        var longStatus = "Waiting for the committee to meet after the summer holidays";
        using (var scope = factory.Services.CreateScope())
        {
            using var db = scope.ServiceProvider.EveryWorkspace();
            db.LeadStatuses.Add(new LeadStatus { Id = Guid.NewGuid(), OrganizationId = orgId, Name = longStatus, DisplayOrder = 0 });
            await db.SaveChangesAsync();
        }

        var csv = $"name,email,status\nJean,jean@dupont.example,{longStatus}\nAnne,anne@meier.example,\n";
        var columns = new[] { "name", "email", "status" };
        var preview = await OkJson(await Preview(client, "lead", csv, columns), "preview");
        var imported = await OkJson(await Import(client, "lead", csv, columns), "import");

        foreach (var result in new[] { preview, imported })
        {
            Action(result, 2).Should().Be("error");
            Message(result, 2).Should().Be("Status is longer than 50 characters");
            // No status in the file: the workspace's only one is the default, and the
            // request check refuses it. The dry run used to promise "Add" here.
            Action(result, 3).Should().Be("error");
            Message(result, 3).Should().Be("Status cannot exceed 50 characters");
        }
        (await factory.Services.Read(db => db.Leads.Count())).Should().Be(0);
    }

    [Fact]
    public async Task AStatusTheWorkspaceDoesNotHaveIsImportedAsTheDefaultAndSaysSo()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (client, _, orgId) = await WorkspaceAsync(factory);
        using (var scope = factory.Services.CreateScope())
        {
            using var db = scope.ServiceProvider.EveryWorkspace();
            db.LeadStatuses.AddRange(
                new LeadStatus { Id = Guid.NewGuid(), OrganizationId = orgId, Name = "New", DisplayOrder = 0 },
                new LeadStatus { Id = Guid.NewGuid(), OrganizationId = orgId, Name = "Contacted", DisplayOrder = 1 });
            await db.SaveChangesAsync();
        }

        var csv = "name,email,status\nJean,jean@dupont.example,Hot prospect\nAnne,anne@meier.example,contacted\n";
        var result = await OkJson(await Import(client, "lead", csv, new[] { "name", "email", "status" }), "import");

        Notes(result, 2).Should().Contain(n => n.Contains("“Hot prospect” is not one of this workspace's") && n.Contains("New"));
        var statuses = await factory.Services.Read(db => db.Leads.ToDictionary(l => l.Email, l => (l.Status, l.LeadStatusId)));
        statuses["jean@dupont.example"].Status.Should().Be("New");
        statuses["anne@meier.example"].Status.Should().Be("Contacted", "the workspace's own name, whatever the case in the file");
        statuses["anne@meier.example"].LeadStatusId.Should().NotBeNull();
    }

    [Fact]
    public async Task CustomFieldsAreCheckedBeforeAnythingIsWrittenAndThenSaved()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await WorkspaceAsync(factory);
        var tier = await OkJson(await client.PostAsync("/api/custom-fields", Body(new
        {
            entityType = "lead", label = "Tier", type = "select", options = new[] { "Gold", "Silver" },
        })), "add a field");
        await OkJson(await client.PostAsync("/api/custom-fields", Body(new { entityType = "lead", label = "Joined", type = "date" })), "add a field");
        var key = tier.GetProperty("key").GetString();

        var csv = "name;email;tier;joined\nJean;jean@dupont.example;gold;31.12.2024\nAnne;anne@meier.example;Platinum;\n";
        var columns = new[] { "name", "email", $"custom:{key}", "custom:joined" };
        var result = await OkJson(await Import(client, "lead", csv, columns), "import");

        Action(result, 2).Should().Be("create");
        Action(result, 3).Should().Be("error");
        Message(result, 3).Should().Be("Tier must be one of: Gold, Silver");
        var jean = await factory.Services.Read(db => db.Leads.Single(l => l.Email == "jean@dupont.example").Id);
        var values = await OkJson(await client.GetAsync($"/api/custom-fields/values?entityType=lead&recordId={jean}"), "values");
        values.GetProperty(key!).GetString().Should().Be("Gold", "the option as the workspace writes it");
        values.GetProperty("joined").GetString().Should().Be("2024-12-31");
        (await factory.Services.Read(db => db.Leads.Count())).Should().Be(1, "a row that does not fit is not half imported");
    }

    [Theory]
    [InlineData(new[] { "name", null }, "Choose the column that holds the email address")]
    [InlineData(new[] { "email", "email" }, "Two columns are going into Email. Choose one")]
    [InlineData(new[] { "email", "favourite colour" }, "\"favourite colour\" is not a field you can import into")]
    [InlineData(new[] { "email" }, "The column choices don't match the file's columns. Choose the columns again")]
    public async Task ColumnChoicesThatCannotWorkAreRefused(string?[] columns, string message)
    {
        await using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await WorkspaceAsync(factory);

        var res = await Preview(client, "lead", "a,b\nJean,jean@dupont.example\n", columns);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var problem = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("detail").GetString().Should().Be(message);
    }

    [Fact]
    public async Task AFileOfEmailsCanUpdatePeopleWithoutANameColumn()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await WorkspaceAsync(factory);
        await AddLeadAsync(client, new { name = "Jean Dupont", email = "jean@dupont.example", status = "New" });

        var csv = "email,country\njean@dupont.example,Switzerland\nnobody@else.example,France\n";
        var columns = new[] { "email", "country" };
        (await Preview(client, "lead", csv, columns)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest, "without a name nobody new can be added");

        var result = await OkJson(await Import(client, "lead", csv, columns, duplicates: "update"), "import");

        Action(result, 2).Should().Be("update");
        Action(result, 3).Should().Be("error");
        Message(result, 3).Should().Be("No name, and nobody here has this email to update");
        (await factory.Services.Read(db => db.Leads.Single().Country)).Should().Be("Switzerland");
    }

    [Fact]
    public async Task AFileThatCannotBeReadIsRefusedWithWhy()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await WorkspaceAsync(factory);

        var res = await Analyze(client, "lead", "name,notes\nJean,\"never closed\nAnne,x\n");

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Row 2 opens a quoted cell that never closes");
        (await client.PostAsync("/api/imports/analyze", Body(new { kind = "deal", content = Content("a\nb\n") })))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest, "deals are not imported");
    }

    [Fact]
    public async Task AFileWithMoreRowsThanOneImportTakesIsRefusedRatherThanCutShort()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (client, _, _) = await WorkspaceAsync(factory);
        var csv = "name,email\n" + string.Concat(Enumerable.Range(0, 2001).Select(i => $"Person {i},p{i}@bluewin.ch\n"));

        var res = await Analyze(client, "lead", csv);

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var problem = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("detail").GetString().Should().Be("The file has more than 2,000 rows. Import up to 2,000 at a time");
    }

    [Fact]
    public async Task AViewerCannotImport()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (_, _, orgId) = await WorkspaceAsync(factory);
        var (viewer, viewerId) = await SignInAsync(factory, "viewer@example.ch");
        await AddMemberAsync(factory, orgId, viewerId, OrgMemberRole.Viewer);
        viewer.In(orgId);

        (await Import(viewer, "lead", SwissFile, SwissColumns)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await factory.Services.Read(db => db.Leads.Count())).Should().Be(0);
    }

    [Fact]
    public async Task APersonalWorkspaceImportsIntoItsOwnRecordsOnly()
    {
        await using var factory = new CustomWebApplicationFactory();
        var (mine, _) = await SignInAsync(factory, "me@example.ch");
        var (theirs, _) = await SignInAsync(factory, "them@example.ch");
        await AddLeadAsync(theirs, new { name = "Jean Dupont", email = "jean@dupont.example", status = "New" });

        var result = await OkJson(await Import(mine, "lead", "name,email\nJean Dupont,jean@dupont.example\n", new[] { "name", "email" }), "import");

        Action(result, 2).Should().Be("create", "another person's lead is not a duplicate of mine, nor something to tell me about");
        (await OkJson(await theirs.GetAsync("/api/imports"), "their imports")).GetArrayLength().Should().Be(0);
        (await OkJson(await mine.GetAsync("/api/imports?kind=lead"), "my imports")).GetArrayLength().Should().Be(1);
    }
}
