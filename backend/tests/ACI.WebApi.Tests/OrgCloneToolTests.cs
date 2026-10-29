using System.Diagnostics;
using System.Text.Json;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace ACI.WebApi.Tests;

/// <summary>
/// The org copy tool (backend/tools/ACI.OrgClone) as the org-clone workflow runs it,
/// against a real SQL Server.
/// </summary>
/// <remarks>
/// Its dry run used to make the whole copy, attachment bytes included, inside a
/// transaction in the production database and roll it back, with no look at whether the
/// database had room: a large organization could fill it and fail every tenant's writes.
/// The dry run now writes nothing, and a copy that would not fit is refused before it starts.
/// </remarks>
public class OrgCloneToolTests
{
    [SqlServerFact]
    public async Task TheDryRunWritesNothing_ItRunsOnADatabaseThatCannotBeWritten()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            await SeedAsync(database, attachments: 1);
            await SqlServerApp.ExecuteAsync("master", $"ALTER DATABASE [{database}] SET READ_ONLY WITH ROLLBACK IMMEDIATE");
            try
            {
                var run = await RunToolAsync(database, "--source", "Pavillon 46", "--name", "Pavillon 46 v2");

                run.ExitCode.Should().Be(0, run.Output);
                run.Output.Should().Contain("DRY RUN: it fits. Nothing was written.");
                run.Output.Should().NotContain("Pavillon", "the output can end up in a public log");
                run.Report.Should().Contain("Attachments").And.Contain("Leads");
            }
            finally
            {
                await SqlServerApp.ExecuteAsync("master", $"ALTER DATABASE [{database}] SET READ_WRITE WITH ROLLBACK IMMEDIATE");
            }
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task ACopyTheDatabaseHasNoRoomForIsRefusedBeforeAnythingIsWritten()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            await SeedAsync(database, attachments: 4);

            // Leave the data file no room to grow: what it holds now, and barely more. (The
            // query store is turned off first, or it fails for want of room itself.)
            await SqlServerApp.ExecuteAsync("master", $"ALTER DATABASE [{database}] SET QUERY_STORE = OFF");
            await SqlServerApp.ExecuteAsync(database, "DBCC SHRINKFILE (1, 1) WITH NO_INFOMSGS");
            var file = (string)(await SqlServerApp.ScalarAsync(database, "SELECT [name] FROM sys.database_files WHERE [file_id] = 1"))!;
            var sizeMb = (int)(await SqlServerApp.ScalarAsync(database, "SELECT ([size] * 8 + 1023) / 1024 FROM sys.database_files WHERE [file_id] = 1"))!;
            await SqlServerApp.ExecuteAsync("master", $"ALTER DATABASE [{database}] MODIFY FILE (NAME = N'{file}', FILEGROWTH = 1MB)");
            await SqlServerApp.ExecuteAsync("master", $"ALTER DATABASE [{database}] MODIFY FILE (NAME = N'{file}', MAXSIZE = {sizeMb + 1}MB)");

            var run = await RunToolAsync(database, "--source", "Pavillon 46", "--name", "Pavillon 46 v2", "--commit");

            run.ExitCode.Should().Be(1, run.Output);
            run.Output.Should().Contain("ABORT: not enough room").And.Contain("Nothing was copied.");
            run.Output.Should().NotContain("DONE:").And.NotContain("CHECK ");
            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [Organizations]")).Should().Be(1, "no copy was started");
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    /// <summary>An organization, "Pavillon 46", with a lead and <paramref name="attachments"/> attachments of 1 MB each.</summary>
    private static async Task SeedAsync(string database, int attachments)
    {
        using var app = SqlServerApp.Start(database);
        var (owner, ownerId) = await Workspaces.SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
        var created = await owner.PostAsync("/api/organizations", Workspaces.Body(new { name = "Pavillon 46" }));
        Guid org;
        using (var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
            org = doc.RootElement.GetProperty("id").GetGuid();

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lead = new Lead { Id = Guid.NewGuid(), UserId = ownerId, OrganizationId = org, Name = "Jean Dupont", Email = "jean@example.ch", Status = "New", CreatedAtUtc = DateTime.UtcNow };
        db.Leads.Add(lead);
        for (var i = 0; i < attachments; i++)
        {
            var content = new byte[1024 * 1024];
            Random.Shared.NextBytes(content);
            db.Attachments.Add(new Attachment
            {
                Id = Guid.NewGuid(), OrganizationId = org, OwnerUserId = ownerId, EntityType = "lead", RecordId = lead.Id,
                FileName = $"contract-{i}.pdf", ContentType = "application/pdf", Size = content.Length, Content = content,
                UploadedByName = "Kia", UploadedAtUtc = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync();
    }

    private sealed record ToolRun(int ExitCode, string Output, string Report);

    /// <summary>Runs the built tool, as the workflow does, with its report written to a file.</summary>
    private static async Task<ToolRun> RunToolAsync(string database, params string[] args)
    {
        // Built with the solution, in the same configuration as these tests.
        var configuration = new DirectoryInfo(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)).Parent!.Name;
        var tool = Path.Combine(ProductionWorkflowTests.RepoRoot(), "backend", "tools", "ACI.OrgClone", "bin", configuration, "net8.0", "ACI.OrgClone.dll");
        File.Exists(tool).Should().BeTrue($"the tool is built with the solution, at {tool}");

        var report = Path.GetTempFileName();
        try
        {
            var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(tool);
            foreach (var arg in args) start.ArgumentList.Add(arg);
            start.ArgumentList.Add("--report");
            start.ArgumentList.Add(report);
            start.Environment["ORG_CLONE_CONNECTION"] = SqlServerApp.ConnectionString(database);

            using var process = Process.Start(start)!;
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            await process.WaitForExitAsync(timeout.Token);
            return new ToolRun(process.ExitCode, await stdout + await stderr, await File.ReadAllTextAsync(report));
        }
        finally
        {
            File.Delete(report);
        }
    }
}
