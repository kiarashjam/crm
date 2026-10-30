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

            await OkJson(await owner.PostAsync($"/api/imports/{batchId}/undo", null), "undo");
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [Leads] WHERE [Email] = N'eva@graf.example'")).Should().Be(0);
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [Leads] WHERE [Email] = N'alice@keller.example'")).Should().Be(1);
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [Companies] WHERE [Name] = N'Acme SA'")).Should().Be(1, "Alice, who stays, is at Acme");
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [Leads] WHERE [Email] = N'bruno.frei@bluewin.ch'")).Should().Be(1);
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
