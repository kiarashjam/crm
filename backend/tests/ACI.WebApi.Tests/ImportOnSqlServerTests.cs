using System.Net;
using System.Text.Json;
using ACI.WebApi.Tests.Helpers;
using static ACI.WebApi.Tests.Helpers.ImportFiles;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// An import and its undo on a real SQL Server. The in-memory provider runs any LINQ in
/// memory, so only SQL Server shows whether each query the importer makes can be turned
/// into SQL, and whether the removal satisfies the foreign keys.
/// </summary>
public class ImportOnSqlServerTests
{
    [SqlServerFact]
    public async Task AFileIsImportedPreviewedAndUndone()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var created = await owner.PostAsync("/api/organizations", Body(new { name = "Pavillon" }));
            created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
            owner.In(JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid());
            var tier = (await OkJson(await owner.PostAsync("/api/custom-fields", Body(new
            {
                entityType = "lead", label = "Tier", type = "select", options = new[] { "Gold", "Silver" },
            })), "add a field")).GetProperty("key").GetString();
            await OkJson(await owner.PostAsync("/api/leads", Body(new { name = "Bruno Frei", email = "bruno.frei@bluewin.ch", status = "New" })), "add a lead");
            // Eva is a contact already. Saving her imported lead links that contact to it,
            // by a foreign key the removal has to let go of first.
            var eva = (await OkJson(await owner.PostAsync("/api/contacts", Body(new { name = "Eva Graf", email = "eva@graf.example" })), "add a contact"))
                .GetProperty("id").GetGuid();

            var csv = "Vorname;Nachname;E-Mail;Firma;Tier\r\n"
                + "Alice;Keller;alice@keller.example;Acme SA;Gold\r\n"
                + "Bruno;Frei;bruno.frei@bluewin.ch;Acme SA;Silver\r\n"
                + "Eva;Graf;eva@graf.example;;\r\n";
            var columns = new[] { "firstName", "lastName", "email", "company", $"custom:{tier}" };

            var preview = await OkJson(await Preview(owner, "lead", csv, columns, duplicates: "update"), "preview");
            Action(preview, 3).Should().Be("update");

            var result = await OkJson(await Import(owner, "lead", csv, columns, duplicates: "update"), "import");
            result.GetProperty("created").GetInt32().Should().Be(2);
            result.GetProperty("updated").GetInt32().Should().Be(1);
            var batchId = result.GetProperty("batchId").GetGuid();
            (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [Leads] WHERE [ImportBatchId] = '{batchId}'")).Should().Be(2);

            var alice = (Guid)(await SqlServerApp.ScalarAsync(database, "SELECT [Id] FROM [Leads] WHERE [Email] = N'alice@keller.example'"))!;
            await OkJson(await owner.PostAsync("/api/activities", Body(new { type = "call", subject = "Called", leadId = alice })), "log a call");

            var listed = (await OkJson(await owner.GetAsync("/api/imports?kind=lead"), "imports")).EnumerateArray().Single();
            listed.GetProperty("remaining").GetInt32().Should().BeGreaterThan(0);

            var undoPreview = await OkJson(await owner.GetAsync($"/api/imports/{batchId}/undo"), "undo preview");
            undoPreview.GetProperty("leads").GetInt32().Should().Be(1, "Eva only; Alice has a call logged");

            (await SqlServerApp.ScalarAsync(database, $"SELECT [ConvertedFromLeadId] FROM [Contacts] WHERE [Id] = '{eva}'")).Should().NotBeNull();

            await OkJson(await owner.PostAsync($"/api/imports/{batchId}/undo", null), "undo");
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [Leads] WHERE [Email] = N'eva@graf.example'")).Should().Be(0);
            (await SqlServerApp.ScalarAsync(database, $"SELECT [ConvertedFromLeadId] FROM [Contacts] WHERE [Id] = '{eva}'"))
                .Should().BeNull("the contact that was here before stays, and lets go of the lead that went");
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [Leads] WHERE [Email] = N'alice@keller.example'")).Should().Be(1);
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [Companies] WHERE [Name] = N'Acme SA'")).Should().Be(1, "Alice, who stays, is at Acme");
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [Leads] WHERE [Email] = N'bruno.frei@bluewin.ch'")).Should().Be(1);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task CompaniesContactsAndLeadsAreImportedAndUndoneOneAfterAnother()
    {
        // Each undo removes rows that point at each other: a lead, the contact made for it
        // and the company named after its email; a contact and the company it brought.
        // SQL Server refuses a removal in the wrong order, where the in-memory provider does not.
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using var app = SqlServerApp.Start(database);
            var (owner, _) = await SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
            var created = await owner.PostAsync("/api/organizations", Body(new { name = "Pavillon" }));
            created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
            var orgId = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetGuid();
            owner.In(orgId);
            async Task<object?> CountAsync(string table) =>
                await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [{table}] WHERE [OrganizationId] = '{orgId}'");

            var companies = await OkJson(await Import(owner, "company", "name;domain\r\nAcme SA;acme.example\r\nBeta AG;beta.example\r\n",
                new[] { "name", "domain" }), "import companies");
            companies.GetProperty("created").GetInt32().Should().Be(2);

            var contacts = await OkJson(await Import(owner, "contact",
                "first name;last name;email;company\r\nAnna;Roth;anna@roth.example;Acme SA\r\nMarc;Weber;marc@weber.example;Gamma GmbH\r\n",
                new[] { "firstName", "lastName", "email", "company" }), "import contacts");
            contacts.GetProperty("created").GetInt32().Should().Be(2);
            (await CountAsync("Companies")).Should().Be(3, "the contact import adds Gamma GmbH");

            var leads = await OkJson(await Import(owner, "lead", "name,email\r\nDana Frei,dana@frei-treuhand.example\r\n",
                new[] { "name", "email" }), "import leads");
            leads.GetProperty("created").GetInt32().Should().Be(1);
            (await CountAsync("Contacts")).Should().Be(3, "Dana is given a contact");
            (await CountAsync("Companies")).Should().Be(4, "and a company named after her email");

            var undoLeads = await OkJson(await owner.PostAsync($"/api/imports/{leads.GetProperty("batchId").GetGuid()}/undo", null), "undo the leads");
            (undoLeads.GetProperty("leads").GetInt32(), undoLeads.GetProperty("contacts").GetInt32(), undoLeads.GetProperty("companies").GetInt32())
                .Should().Be((1, 1, 1));
            (await CountAsync("Leads")).Should().Be(0);
            (await CountAsync("Contacts")).Should().Be(2);

            var undoContacts = await OkJson(await owner.PostAsync($"/api/imports/{contacts.GetProperty("batchId").GetGuid()}/undo", null), "undo the contacts");
            (undoContacts.GetProperty("contacts").GetInt32(), undoContacts.GetProperty("companies").GetInt32()).Should().Be((2, 1));
            (await CountAsync("Contacts")).Should().Be(0);
            (await CountAsync("Companies")).Should().Be(2, "Acme SA and Beta AG came from the company import");

            var undoCompanies = await OkJson(await owner.PostAsync($"/api/imports/{companies.GetProperty("batchId").GetGuid()}/undo", null), "undo the companies");
            undoCompanies.GetProperty("companies").GetInt32().Should().Be(2);
            (await CountAsync("Companies")).Should().Be(0);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
