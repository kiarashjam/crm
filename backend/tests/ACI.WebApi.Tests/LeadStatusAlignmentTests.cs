using System.Net;
using System.Text.Json;
using ACI.Domain.Common;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace ACI.WebApi.Tests;

/// <summary>
/// Bringing organisations onto the agreed lead status list, without undoing what
/// managers do with their statuses afterwards.
/// </summary>
/// <remarks>
/// The alignment ran on every start, over every organisation. A status a manager made
/// with a retired name was deleted at the next deploy or restart and its leads moved,
/// with no history of why; a stage they deleted came back, and their order was reset.
/// It now runs once, only touches organisations still on the old list, and never
/// deletes a status a lead uses. The start-up SQL is T-SQL, so these run the real API
/// on SQL Server (see <see cref="SqlServerFactAttribute"/>).
/// </remarks>
public class LeadStatusAlignmentTests
{
    [SqlServerFact]
    public async Task WhatAManagerDoesWithTheStatusesSurvivesARestart()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            Guid org, lead;
            using (var app = SqlServerApp.Start(database))
            {
                var (owner, ownerId) = await Workspaces.SignInAsync(app, $"{Guid.NewGuid():N}@example.com");
                var created = await owner.PostAsync("/api/organizations", Workspaces.Body(new { name = "Pavillon" }));
                created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
                using (var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
                    org = doc.RootElement.GetProperty("id").GetGuid();
                owner.In(org);

                // A status of their own, named like one of the old defaults, that leads use.
                var qualified = await owner.PostAsync("/api/leadstatuses", Workspaces.Body(new { name = "Qualified", displayOrder = 8 }));
                qualified.IsSuccessStatusCode.Should().BeTrue(await qualified.Content.ReadAsStringAsync());
                Guid qualifiedId;
                using (var doc = JsonDocument.Parse(await qualified.Content.ReadAsStringAsync()))
                    qualifiedId = doc.RootElement.GetProperty("id").GetGuid();

                // A stage they do not use, and an order of their own.
                var awaiting = await StatusIdAsync(database, org, "Awaiting Signature");
                (await owner.DeleteAsync($"/api/leadstatuses/{awaiting}")).IsSuccessStatusCode.Should().BeTrue();
                var newStage = await StatusIdAsync(database, org, "New");
                (await owner.PutAsync($"/api/leadstatuses/{newStage}", Workspaces.Body(new { displayOrder = 5 }))).IsSuccessStatusCode.Should().BeTrue();

                lead = await AddLeadAsync(app, org, ownerId, "Qualified", qualifiedId);
            }

            using (SqlServerApp.Start(database))
            {
                (await StatusIdAsync(database, org, "Qualified")).Should().NotBeNull("a status a manager made is theirs");
                (await StatusIdAsync(database, org, "Awaiting Signature")).Should().BeNull("a stage a manager deleted stays deleted");
                (await SqlServerApp.ScalarAsync(database, $"SELECT [DisplayOrder] FROM [LeadStatuses] WHERE [OrganizationId] = '{org}' AND [Name] = N'New'"))
                    .Should().Be(5);
                (await SqlServerApp.ScalarAsync(database, $"SELECT [Status] FROM [Leads] WHERE [Id] = '{lead}'")).Should().Be("Qualified");
                (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [LeadStatusEvents] WHERE [LeadId] = '{lead}'")).Should().Be(0);
            }
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [SqlServerFact]
    public async Task AnOrganisationStillOnTheOldListIsBroughtOntoTheNewOneOnce()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            var org = Guid.NewGuid();
            Guid movedLead, oddLead, newClub, ownLead;
            using (var app = SqlServerApp.Start(database))
            {
                // A database the alignment has never run on: an organisation with the
                // ten statuses this app seeded before the rework.
                await SqlServerApp.ExecuteAsync(database,
                    $"DELETE FROM [AppliedDataFixes] WHERE [Name] = N'{DataFixes.AlignLeadStatusVocabularyName}'");
                var (owner, ownerId) = await Workspaces.SignInAsync(app, $"{Guid.NewGuid():N}@example.com");

                // Next to it, one already on the agreed list whose manager has made
                // statuses with retired names, as production's are when this first runs.
                var created = await owner.PostAsync("/api/organizations", Workspaces.Body(new { name = "New club" }));
                using (var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync()))
                    newClub = doc.RootElement.GetProperty("id").GetGuid();
                owner.In(newClub);
                (await owner.PostAsync("/api/leadstatuses", Workspaces.Body(new { name = "Qualified", displayOrder = 8 }))).IsSuccessStatusCode.Should().BeTrue();
                (await owner.PostAsync("/api/leadstatuses", Workspaces.Body(new { name = "Open", displayOrder = 9 }))).IsSuccessStatusCode.Should().BeTrue();
                ownLead = await AddLeadAsync(app, newClub, ownerId, "Qualified", await StatusIdAsync(database, newClub, "Qualified"));

                using (var scope = app.Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    db.Organizations.Add(new Organization { Id = org, Name = "Old club", OwnerUserId = ownerId, CreatedAtUtc = DateTime.UtcNow.AddYears(-1) });
                    db.LeadStatuses.AddRange(LeadStatusVocabulary.PreviousDefault.Select((name, i) =>
                        new LeadStatus { Id = Guid.NewGuid(), OrganizationId = org, Name = name, DisplayOrder = i }));
                    await db.SaveChangesAsync();
                }
                movedLead = await AddLeadAsync(app, org, ownerId, "Qualified", await StatusIdAsync(database, org, "Qualified"));
                // Points at the old "Lost" row while wearing another status: the row is in use.
                oddLead = await AddLeadAsync(app, org, ownerId, "Contacted", await StatusIdAsync(database, org, "Lost"));
            }

            using (SqlServerApp.Start(database))
            {
                var names = await NamesAsync(database, org);
                names.Should().ContainInOrder(LeadStatusVocabulary.Default);
                names.Should().NotContain(new[] { "Open", "In Progress", "Qualified", "Unqualified", "Open Deal" });
                names.Should().Contain("Lost", "a lead still points at it");

                var contractPending = await StatusIdAsync(database, org, "Contract Pending");
                (await SqlServerApp.ScalarAsync(database, $"SELECT [Status] FROM [Leads] WHERE [Id] = '{movedLead}'")).Should().Be("Contract Pending");
                (await SqlServerApp.ScalarAsync(database, $"SELECT [LeadStatusId] FROM [Leads] WHERE [Id] = '{movedLead}'")).Should().Be(contractPending);
                (await SqlServerApp.ScalarAsync(database,
                    $"SELECT COUNT(*) FROM [LeadStatusEvents] WHERE [LeadId] = '{movedLead}' AND [FromStatus] = N'Qualified' AND [ToStatus] = N'Contract Pending'"))
                    .Should().Be(1, "the move is in the lead's status history");
                (await SqlServerApp.ScalarAsync(database, $"SELECT [LeadStatusId] FROM [Leads] WHERE [Id] = '{oddLead}'"))
                    .Should().Be(await StatusIdAsync(database, org, "Lost"));
                (await SqlServerApp.ScalarAsync(database,
                    $"SELECT COUNT(*) FROM [AppliedDataFixes] WHERE [Name] = N'{DataFixes.AlignLeadStatusVocabularyName}'")).Should().Be(1);

                // The organisation already on the agreed list is left as its manager made it.
                (await StatusIdAsync(database, newClub, "Qualified")).Should().NotBeNull("a status a manager made is theirs");
                (await StatusIdAsync(database, newClub, "Open")).Should().NotBeNull("a status a manager made is theirs");
                (await SqlServerApp.ScalarAsync(database, $"SELECT [Status] FROM [Leads] WHERE [Id] = '{ownLead}'")).Should().Be("Qualified");
                (await SqlServerApp.ScalarAsync(database, $"SELECT COUNT(*) FROM [LeadStatusEvents] WHERE [LeadId] = '{ownLead}'")).Should().Be(0);

                // From now on "Qualified" is a status a manager made, and it stays.
                await SqlServerApp.ExecuteAsync(database,
                    $"INSERT INTO [LeadStatuses] ([Id], [OrganizationId], [Name], [DisplayOrder]) VALUES (NEWID(), '{org}', N'Qualified', 8)");
            }

            using (SqlServerApp.Start(database))
            {
                (await StatusIdAsync(database, org, "Qualified")).Should().NotBeNull("the alignment runs once");
            }
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }

    [Fact]
    public void TheAlignmentIsValidTSql()
    {
        new TSql160Parser(initialQuotedIdentifiers: true).Parse(new StringReader(DataFixes.AlignLeadStatusVocabularySql), out var errors);
        errors.Should().BeEmpty();
    }

    [Fact]
    public void TheAlignmentRunsOnceAndRecordsItselfInTheSameTransaction()
    {
        var sql = DataFixes.AlignLeadStatusVocabularySql;
        var begin = sql.IndexOf("BEGIN TRANSACTION", StringComparison.Ordinal);
        var guard = sql.IndexOf($"IF NOT EXISTS (SELECT 1 FROM [AppliedDataFixes] WITH (UPDLOCK, HOLDLOCK) WHERE [Name] = N'{DataFixes.AlignLeadStatusVocabularyName}')", StringComparison.Ordinal);
        var delete = sql.IndexOf("DELETE s", StringComparison.Ordinal);
        var record = sql.IndexOf($"INSERT INTO [AppliedDataFixes] ([Name], [AppliedAtUtc]) VALUES (N'{DataFixes.AlignLeadStatusVocabularyName}'", StringComparison.Ordinal);
        var commit = sql.IndexOf("COMMIT TRANSACTION", StringComparison.Ordinal);

        new[] { begin, guard, delete, record, commit }.Should().NotContain(-1);
        new[] { begin, guard, delete, record, commit }.Should().BeInAscendingOrder();
    }

    [Fact]
    public void StartupRunsTheOnceOnlyAlignment()
    {
        var program = File.ReadAllText(ProgramPath());
        program.Should().Contain("ExecuteSqlRawAsync(ACI.WebApi.DataFixes.AlignLeadStatusVocabularySql)");
        program.Should().NotContain("ExecuteSqlRawAsync(LeadStatusVocabulary.AlignmentSql())", "unguarded, it runs on every start");
    }

    private static async Task<Guid?> StatusIdAsync(string database, Guid org, string name) =>
        (Guid?)await SqlServerApp.ScalarAsync(database,
            $"SELECT TOP 1 [Id] FROM [LeadStatuses] WHERE [OrganizationId] = '{org}' AND [Name] = N'{name.Replace("'", "''")}'");

    private static async Task<List<string>> NamesAsync(string database, Guid org)
    {
        var joined = (string?)await SqlServerApp.ScalarAsync(database,
            $"SELECT STRING_AGG([Name], N'|') WITHIN GROUP (ORDER BY [DisplayOrder], [Name]) FROM [LeadStatuses] WHERE [OrganizationId] = '{org}'");
        return (joined ?? "").Split('|').ToList();
    }

    private static async Task<Guid> AddLeadAsync(SqlServerApp app, Guid org, Guid owner, string status, Guid? statusId)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var lead = new Lead
        {
            Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Name = "Jean Dupont", Email = $"{Guid.NewGuid():N}@example.ch",
            Status = status, LeadStatusId = statusId, CreatedAtUtc = DateTime.UtcNow,
        };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();
        return lead.Id;
    }

    private static string ProgramPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "backend"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "backend", "src", "ACI.WebApi", "Program.cs");
    }
}
