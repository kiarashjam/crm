using System.Text.RegularExpressions;
using ACI.Application.Interfaces;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.WebApi.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace ACI.WebApi.Tests;

/// <summary>
/// AppDbContext filters every read of a workspace's records to the workspace the request
/// or job acts for (ITenantContext), whichever repository reads.
/// </summary>
public class WorkspaceFilterTests
{
    /// <summary>
    /// Tables with an OrganizationId that are read across workspaces on purpose: they are
    /// how a person finds the workspaces they belong to, are invited to, or ask to join.
    /// </summary>
    private static readonly Type[] TenancyTables = { typeof(OrganizationMember), typeof(Invite), typeof(JoinRequest) };

    private static AppDbContext SqlServerModel(ITenantContext? tenant = null) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=not-connected;Database=none;").Options, tenant: tenant);

    private static DbContextOptions<AppDbContext> InMemory(string name) =>
        new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options;

    [Fact]
    public void Every_table_with_an_organization_is_filtered_to_the_workspace()
    {
        using var db = SqlServerModel();
        var unfiltered = db.Model.GetEntityTypes()
            .Where(t => t.FindProperty("OrganizationId") != null && !TenancyTables.Contains(t.ClrType))
            .Where(t => t.GetQueryFilter() == null)
            .Select(t => t.ClrType.Name)
            .ToList();

        unfiltered.Should().BeEmpty(
            "a table that belongs to a workspace must be read only within it: add it to AppDbContext.ScopeToWorkspace");
    }

    [Fact]
    public void In_an_organization_a_read_starts_with_a_plain_match_on_the_organization()
    {
        using var db = SqlServerModel(new TestTenant(Guid.NewGuid(), Guid.NewGuid()));

        var sql = db.Leads.ToQueryString();

        // A plain "OrganizationId = @org" ANDed with the rest, which tests only parameters,
        // so what runs can seek on the OrganizationId index; no OR around the column test.
        Regex.IsMatch(sql, @"WHERE \[l\]\.\[OrganizationId\] = @\w+ AND \(@\w+ = CAST\(1 AS bit\) OR ").Should().BeTrue(sql);
    }

    [Fact]
    public void In_a_personal_workspace_a_read_is_the_persons_own_rows_without_an_organization()
    {
        using var db = SqlServerModel(new TestTenant(null, Guid.NewGuid()));

        var sql = db.Leads.ToQueryString();

        // EF writes "IS NULL" for the organization parameter's null value, and the owner test after it.
        Regex.IsMatch(sql, @"WHERE \[l\]\.\[OrganizationId\] IS NULL AND \(.*\[l\]\.\[UserId\] = @\w+\)\)\s*$").Should().BeTrue(sql);
    }

    [Fact]
    public async Task Each_workspace_reads_only_its_own_records_and_no_workspace_reads_none()
    {
        var name = Guid.NewGuid().ToString();
        Guid orgA = Guid.NewGuid(), orgB = Guid.NewGuid(), alice = Guid.NewGuid(), bob = Guid.NewGuid();
        await using (var write = new AppDbContext(InMemory(name)))
        {
            write.Leads.AddRange(
                new Lead { Id = Guid.NewGuid(), Name = "A's lead", Email = "a@example.com", UserId = alice, OrganizationId = orgA },
                new Lead { Id = Guid.NewGuid(), Name = "B's lead", Email = "b@example.com", UserId = bob, OrganizationId = orgB },
                new Lead { Id = Guid.NewGuid(), Name = "Alice's own", Email = "alice@example.com", UserId = alice },
                new Lead { Id = Guid.NewGuid(), Name = "Bob's own", Email = "bob@example.com", UserId = bob });
            await write.SaveChangesAsync();
        }

        async Task<List<string>> NamesAs(ITenantContext? tenant)
        {
            await using var read = new AppDbContext(InMemory(name), tenant: tenant);
            return await read.Leads.Select(l => l.Name).OrderBy(n => n).ToListAsync();
        }

        (await NamesAs(new TestTenant(orgA, alice))).Should().Equal("A's lead");
        (await NamesAs(new TestTenant(orgA, bob))).Should().Equal(new[] { "A's lead" }, "inside an organization its records are read whoever acts");
        (await NamesAs(new TestTenant(null, alice))).Should().Equal("Alice's own");
        (await NamesAs(new TestTenant())).Should().BeEmpty("with no workspace nothing is read");
        (await NamesAs(null)).Should().BeEmpty("a context made without a workspace reads nothing either");
    }

    [Fact]
    public async Task Acting_for_a_workspace_applies_to_the_next_query_on_the_same_context()
    {
        var name = Guid.NewGuid().ToString();
        Guid orgA = Guid.NewGuid(), orgB = Guid.NewGuid(), owner = Guid.NewGuid();
        await using (var write = new AppDbContext(InMemory(name)))
        {
            write.Leads.AddRange(
                new Lead { Id = Guid.NewGuid(), Name = "A", Email = "a@example.com", UserId = owner, OrganizationId = orgA },
                new Lead { Id = Guid.NewGuid(), Name = "B", Email = "b@example.com", UserId = owner, OrganizationId = orgB });
            await write.SaveChangesAsync();
        }

        var tenant = new TestTenant();
        await using var db = new AppDbContext(InMemory(name), tenant: tenant);
        using (tenant.ActFor(orgA, null))
            (await db.Leads.Select(l => l.Name).ToListAsync()).Should().Equal("A");
        using (tenant.ActFor(orgB, null))
            (await db.Leads.Select(l => l.Name).ToListAsync()).Should().Equal("B");
        (await db.Leads.CountAsync()).Should().Be(0, "once the scope ends, the workspace before it applies again");
        (await db.Leads.IgnoreQueryFilters().CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task A_personal_row_without_an_owner_is_not_read_by_someone_acting_for_nobody()
    {
        var name = Guid.NewGuid().ToString();
        await using (var write = new AppDbContext(InMemory(name)))
        {
            write.OutboundMessages.Add(new OutboundMessage { Id = Guid.NewGuid(), Channel = "email", To = "x@example.com" });
            await write.SaveChangesAsync();
        }

        await using var read = new AppDbContext(InMemory(name), tenant: new TestTenant());
        (await read.OutboundMessages.CountAsync()).Should().Be(0);
    }
}
