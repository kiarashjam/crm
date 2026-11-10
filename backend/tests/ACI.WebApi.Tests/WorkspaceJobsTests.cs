using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Services;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using static ACI.WebApi.Tests.Helpers.Workspaces;

namespace ACI.WebApi.Tests;

/// <summary>
/// The background jobs start in no workspace and read none of the workspaces' records by
/// accident: each reads across them where it says so, and does its work in each record's own.
/// </summary>
public class WorkspaceJobsTests
{
    [Fact]
    public async Task One_reminder_pass_reaches_every_workspace_and_writes_each_notice_in_its_own()
    {
        using var factory = new CustomWebApplicationFactory();
        var (_, alpineOwner) = await SignInAsync(factory, "owner@alpine.example");
        var alpine = await CreateAsync(factory, alpineOwner, "Alpine");
        var (_, berninaOwner) = await SignInAsync(factory, "owner@bernina.example");
        var bernina = await CreateAsync(factory, berninaOwner, "Bernina");
        var (_, solo) = await SignInAsync(factory, "solo@example.ch");

        var due = DateTime.UtcNow.AddHours(-1);
        TaskItem Overdue(Guid owner, Guid? org, string title) => new()
        {
            Id = Guid.NewGuid(), UserId = owner, OrganizationId = org, Title = title, DueDateUtc = due, CreatedAtUtc = due.AddDays(-1),
        };
        var tasks = new[] { Overdue(alpineOwner, alpine, "Alpine's"), Overdue(berninaOwner, bernina, "Bernina's"), Overdue(solo, null, "Solo's own") };
        using (var scope = factory.Services.CreateScope())
        {
            await using var db = scope.ServiceProvider.EveryWorkspace();
            db.TaskItems.AddRange(tasks);
            await db.SaveChangesAsync();
        }

        await new TaskReminderBackgroundService(factory.Services.GetRequiredService<IServiceScopeFactory>(), NullLogger<TaskReminderBackgroundService>.Instance)
            .ProcessRemindersAsync(CancellationToken.None);

        var notices = await factory.Services.Read(db => db.Notifications.Where(n => n.SourceKey!.StartsWith("task-overdue-")).ToList());
        notices.Select(n => (n.SourceKey, n.UserId, n.OrganizationId)).Should().BeEquivalentTo(tasks.Select(t => ($"task-overdue-{t.Id}", t.UserId, t.OrganizationId)),
            "each task's owner is reminded, in the task's own workspace, whichever workspace it is in");
    }

    /// <summary>
    /// On SQL Server, what the filters and the bulk updates the app runs (ExecuteUpdate)
    /// actually do: a workspace reads and changes its own rows and no one else's.
    /// </summary>
    [SqlServerFact]
    public async Task On_sql_server_a_workspace_reads_and_bulk_updates_only_its_own_rows()
    {
        await SqlServerApp.WaitForServerAsync();
        var database = await SqlServerApp.NewDatabaseAsync();
        try
        {
            using (SqlServerApp.Start(database)) { }
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(SqlServerApp.ConnectionString(database)).Options;
            Guid alpine = Guid.NewGuid(), bernina = Guid.NewGuid(), alice = Guid.NewGuid(), bob = Guid.NewGuid();
            await using (var seed = new AppDbContext(options))
            {
                seed.Users.AddRange(
                    new User { Id = alice, Name = "Alice", Email = $"{alice:N}@example.ch", CreatedAtUtc = DateTime.UtcNow },
                    new User { Id = bob, Name = "Bob", Email = $"{bob:N}@example.ch", CreatedAtUtc = DateTime.UtcNow });
                seed.Organizations.AddRange(
                    new Organization { Id = alpine, Name = "Alpine", OwnerUserId = alice, CreatedAtUtc = DateTime.UtcNow },
                    new Organization { Id = bernina, Name = "Bernina", OwnerUserId = bob, CreatedAtUtc = DateTime.UtcNow });
                Lead L(string name, Guid owner, Guid? org) => new() { Id = Guid.NewGuid(), Name = name, Email = $"{Guid.NewGuid():N}@example.ch", Status = "New", UserId = owner, OrganizationId = org, CreatedAtUtc = DateTime.UtcNow };
                seed.Leads.AddRange(L("Alpine's", alice, alpine), L("Bernina's", bob, bernina), L("Alice's own", alice, null), L("Bob's own", bob, null));
                await seed.SaveChangesAsync();
            }

            async Task<List<string>> NamesAs(Guid? org, Guid? user)
            {
                await using var read = new AppDbContext(options);
                using var acting = read.ActFor(org, user);
                return await read.Leads.Select(l => l.Name).OrderBy(n => n).ToListAsync();
            }

            (await NamesAs(alpine, alice)).Should().Equal("Alpine's");
            (await NamesAs(null, alice)).Should().Equal("Alice's own");
            (await NamesAs(null, null)).Should().BeEmpty();

            await using (var write = new AppDbContext(options))
            {
                using var acting = write.ActFor(alpine, alice);
                (await write.Leads.ExecuteUpdateAsync(s => s.SetProperty(l => l.Industry, "Touched"))).Should().Be(1, "only Alpine's own lead");
            }
            await using (var write = new AppDbContext(options))
            {
                // Acting for nobody, a bulk update reaches nothing; a job says IgnoreQueryFilters.
                (await write.Leads.ExecuteUpdateAsync(s => s.SetProperty(l => l.Industry, "Everyone"))).Should().Be(0);
            }

            (await SqlServerApp.ScalarAsync(database, "SELECT COUNT(*) FROM [Leads] WHERE [Industry] = N'Touched'")).Should().Be(1);
            (await SqlServerApp.ScalarAsync(database, $"SELECT [Name] FROM [Leads] WHERE [Industry] = N'Touched'")).Should().Be("Alpine's");
        }
        finally
        {
            await SqlServerApp.DropDatabaseAsync(database);
        }
    }
}
