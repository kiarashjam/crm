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

    /// <summary>The columns tables record a personal row's owner in (see AppDbContext.ScopeToWorkspace).</summary>
    private static readonly string[] OwnerColumns =
        { "UserId", "OwnerUserId", "CreatedByUserId", "SentByUserId", "SavedByUserId", "EnrolledByUserId", "ChangedByUserId" };

    /// <summary>
    /// A row of <paramref name="type"/> in a workspace: <paramref name="organizationId"/>,
    /// or with none <paramref name="ownerId"/>'s personal workspace. Every owner column and
    /// every required text is filled, so the row stands for that workspace whatever the table.
    /// </summary>
    private static object Row(Microsoft.EntityFrameworkCore.Metadata.IEntityType type, Guid? organizationId, Guid ownerId)
    {
        var row = Activator.CreateInstance(type.ClrType)!;
        foreach (var property in type.GetProperties().Where(p => p.PropertyInfo != null))
        {
            var info = property.PropertyInfo!;
            if (property.Name == "OrganizationId") info.SetValue(row, organizationId);
            else if (OwnerColumns.Contains(property.Name)) info.SetValue(row, ownerId);
            else if (property.IsPrimaryKey() && info.PropertyType == typeof(Guid)) info.SetValue(row, Guid.NewGuid());
            else if (info.PropertyType == typeof(string) && !property.IsNullable && info.GetValue(row) == null) info.SetValue(row, "x");
            else if (info.PropertyType == typeof(byte[]) && !property.IsNullable && info.GetValue(row) == null) info.SetValue(row, Array.Empty<byte>());
        }
        return row;
    }

    private static IReadOnlyList<object> All(AppDbContext db, Type clrType) =>
        ((IQueryable<object>)typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!.MakeGenericMethod(clrType).Invoke(db, null)!).ToList();

    /// <summary>
    /// Every filtered table, row by row: an organization reads its own rows and nobody
    /// else's, a personal workspace its owner's own rows, and no workspace nothing.
    /// </summary>
    /// <remarks>
    /// This is the net under every repository: a query that forgets its workspace (a
    /// predicate left out, an id from another tenant's URL) still reads only the request's.
    /// Without the filters every row of every table showed to everyone here.
    /// </remarks>
    [Fact]
    public async Task No_table_shows_another_workspaces_rows_to_anyone()
    {
        var name = Guid.NewGuid().ToString();
        Guid orgA = Guid.NewGuid(), orgB = Guid.NewGuid(), alice = Guid.NewGuid(), bob = Guid.NewGuid();
        var labels = new Dictionary<object, string>();
        var written = new List<(object Row, string Label)>();
        IReadOnlyList<Microsoft.EntityFrameworkCore.Metadata.IEntityType> tables;
        await using (var write = new EveryWorkspaceDbContext(InMemory(name)))
        {
            // Only the rows written here: the audit trail would add lines of its own.
            write.RecordsAuditTrail = false;
            using var model = SqlServerModel();
            var filtered = model.Model.GetEntityTypes().Where(t => t.GetQueryFilter() != null && t.ClrType != typeof(Template)).Select(t => t.ClrType).ToHashSet();
            tables = write.Model.GetEntityTypes().Where(t => filtered.Contains(t.ClrType)).ToList();
            foreach (var type in tables)
            {
                var personal = type.FindProperty("OrganizationId")!.IsNullable;
                foreach (var (label, organization, owner) in new (string, Guid?, Guid)[]
                {
                    ("A", orgA, alice), ("B", orgB, bob), ("alice's own", null, alice), ("bob's own", null, bob),
                })
                {
                    if (organization == null && !personal) continue;
                    var row = Row(type, organization, owner);
                    write.Add(row);
                    written.Add((row, label));
                }
            }
            await write.SaveChangesAsync();
            foreach (var (row, label) in written)
                labels[write.Model.FindEntityType(row.GetType())!.FindPrimaryKey()!.Properties[0].PropertyInfo!.GetValue(row)!] = label;
        }
        tables.Should().HaveCountGreaterThan(30, "every table that belongs to a workspace is in this check");

        List<string> Seen(ITenantContext? tenant, Microsoft.EntityFrameworkCore.Metadata.IEntityType type)
        {
            using var read = new AppDbContext(InMemory(name), tenant: tenant);
            var key = type.FindPrimaryKey()!.Properties[0].PropertyInfo!;
            return All(read, type.ClrType).Select(r => labels[key.GetValue(r)!]).OrderBy(l => l).ToList();
        }

        foreach (var type in tables)
        {
            var table = type.ClrType.Name;
            // Whether the table keeps personal rows by an owner: its filter has a personal branch.
            using var model = SqlServerModel();
            var owned = model.Model.FindEntityType(type.ClrType)!.GetQueryFilter()!.ToString().Contains(nameof(AppDbContext.TenantUserId))
                && type.FindProperty("OrganizationId")!.IsNullable;
            Seen(new TestTenant(orgA, alice), type).Should().Equal(new[] { "A" }, $"{table}: organization A reads its own rows only");
            Seen(new TestTenant(orgB, alice), type).Should().Equal(new[] { "B" }, $"{table}: and B its own");
            Seen(new TestTenant(null, alice), type).Should().Equal(owned ? new[] { "alice's own" } : Array.Empty<string>(),
                $"{table}: a personal workspace reads its owner's own rows only, and none of a table with no owner");
            Seen(new TestTenant(), type).Should().BeEmpty($"{table}: with no workspace nothing is read");
            Seen(null, type).Should().BeEmpty($"{table}: nor by a context made without one");
        }
    }

    [Fact]
    public async Task Templates_follow_their_person_and_a_team_shares_its_own()
    {
        var name = Guid.NewGuid().ToString();
        Guid orgA = Guid.NewGuid(), orgB = Guid.NewGuid(), alice = Guid.NewGuid(), bob = Guid.NewGuid();
        await using (var write = new AppDbContext(InMemory(name)))
        {
            Template T(string title, Guid userId, Guid? org, bool system = false) => new()
            {
                Id = Guid.NewGuid(), Title = title, Content = "x", UserId = userId, OrganizationId = org, IsSystemTemplate = system,
                IsSharedWithOrganization = org != null,
            };
            write.Templates.AddRange(
                T("System", Guid.Empty, null, system: true), T("Alice's", alice, null), T("A's team", bob, orgA), T("B's team", bob, orgB));
            await write.SaveChangesAsync();
        }

        async Task<List<string>> TitlesAs(ITenantContext tenant)
        {
            await using var read = new AppDbContext(InMemory(name), tenant: tenant);
            return await read.Templates.Select(t => t.Title).OrderBy(t => t).ToListAsync();
        }

        (await TitlesAs(new TestTenant(orgA, alice))).Should().Equal("A's team", "Alice's", "System");
        (await TitlesAs(new TestTenant(null, alice))).Should().Equal("Alice's", "System");
        (await TitlesAs(new TestTenant())).Should().Equal(new[] { "System" }, "the system's templates are everyone's");
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
