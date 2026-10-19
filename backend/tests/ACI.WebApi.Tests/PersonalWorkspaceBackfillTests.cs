using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.Infrastructure.Repositories;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ACI.WebApi.Tests;

/// <summary>
/// Creating an organization moves the creator's personal records into it, and what
/// hangs off those records has to move too.
/// </summary>
/// <remarks>
/// <para>
/// The move is a set of ExecuteUpdateAsync statements, which the in-memory provider
/// the other tests use cannot run, so this one runs on SQLite, a real relational
/// database that can.
/// </para>
/// <para>
/// The audit history, attachments, custom fields and their values each carry their own
/// workspace column. Before they were added to the move, a personal deal arrived in the
/// new organization while its files, field values and history stayed behind in the
/// personal workspace, where nobody in the organization could see them.
/// </para>
/// </remarks>
public class PersonalWorkspaceBackfillTests
{
    [Fact]
    public async Task CreatingAnOrganizationBringsAlongWhatHangsOffTheRecords()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        using var db = SqliteDb.Open(connection);

        var me = Guid.NewGuid();
        var someoneElse = Guid.NewGuid();
        var org = Guid.NewGuid();
        // SQLite enforces the foreign keys the in-memory provider ignores, so the people are real rows.
        db.Users.AddRange(
            new User { Id = me, Name = "Pierre", Email = "pierre@example.ch", CreatedAtUtc = DateTime.UtcNow },
            new User { Id = someoneElse, Name = "Sam", Email = "sam@example.ch", CreatedAtUtc = DateTime.UtcNow });
        db.Organizations.Add(new Organization { Id = org, Name = "Pavillon46", OwnerUserId = me, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var deal = new Deal { Id = Guid.NewGuid(), UserId = me, Name = "Founding membership", Value = "12000", CreatedAtUtc = DateTime.UtcNow };
        var theirs = new Deal { Id = Guid.NewGuid(), UserId = someoneElse, Name = "Not mine", Value = "1", CreatedAtUtc = DateTime.UtcNow };
        db.Deals.AddRange(deal, theirs); // the save also writes their "Deal created" audit lines
        db.Attachments.Add(new Attachment
        {
            Id = Guid.NewGuid(), OwnerUserId = me, EntityType = "deal", RecordId = deal.Id, FileName = "terms.pdf",
            ContentType = "application/pdf", Size = 4, Content = "%PDF"u8.ToArray(), UploadedByName = "Pierre", UploadedAtUtc = DateTime.UtcNow,
        });
        db.CustomFieldDefinitions.Add(new CustomFieldDefinition
        {
            Id = Guid.NewGuid(), OwnerUserId = me, EntityType = "deal", Key = "board_seat", Label = "Board seat", Type = "checkbox", CreatedAtUtc = DateTime.UtcNow,
        });
        db.CustomFieldValues.Add(new CustomFieldValueSet
        {
            Id = Guid.NewGuid(), OwnerUserId = me, EntityType = "deal", RecordId = deal.Id, ValuesJson = "{\"board_seat\":true}", UpdatedAtUtc = DateTime.UtcNow,
        });
        db.DealLineItems.Add(new DealLineItem { Id = Guid.NewGuid(), DealId = deal.Id, Name = "Membership", Quantity = 1, UnitPrice = 12000, CreatedAtUtc = DateTime.UtcNow });
        db.Products.Add(new Product { Id = Guid.NewGuid(), OwnerUserId = me, Name = "Membership", UnitPrice = 12000, CreatedAtUtc = DateTime.UtcNow });
        db.ForecastSnapshots.Add(new ForecastSnapshot { Id = Guid.NewGuid(), SavedByUserId = me, MonthUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), SavedAtUtc = DateTime.UtcNow });
        db.ClubMembers.Add(new ClubMember { Id = Guid.NewGuid(), OwnerUserId = me, FirstName = "Jean", LastName = "Dupont", Email = "jean@example.ch" });
        db.AutomationRules.Add(new AutomationRule { Id = Guid.NewGuid(), OwnerUserId = me, Name = "Welcome", Trigger = "lead.created", CreatedAtUtc = DateTime.UtcNow });
        db.Products.Add(new Product { Id = Guid.NewGuid(), OwnerUserId = someoneElse, Name = "Theirs", UnitPrice = 1, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await new OrganizationRepository(db).BackfillUserDataToOrganizationAsync(me, org);
        db.ChangeTracker.Clear();

        (await db.Deals.SingleAsync(d => d.Id == deal.Id)).OrganizationId.Should().Be(org);
        (await db.AuditEvents.Where(e => e.OwnerUserId == me).Select(e => e.OrganizationId).ToListAsync())
            .Should().NotBeEmpty().And.OnlyContain(o => o == org, "the deal's history goes with it");
        (await db.Attachments.SingleAsync()).OrganizationId.Should().Be(org);
        (await db.CustomFieldDefinitions.SingleAsync()).OrganizationId.Should().Be(org);
        (await db.CustomFieldValues.SingleAsync()).OrganizationId.Should().Be(org);
        (await db.Products.SingleAsync(p => p.OwnerUserId == me)).OrganizationId.Should().Be(org);
        (await db.ForecastSnapshots.SingleAsync()).OrganizationId.Should().Be(org);
        (await db.ClubMembers.SingleAsync()).OrganizationId.Should().Be(org);
        (await db.AutomationRules.SingleAsync()).OrganizationId.Should().Be(org);

        // Someone else's personal workspace is untouched.
        (await db.Deals.SingleAsync(d => d.Id == theirs.Id)).OrganizationId.Should().BeNull();
        (await db.Products.SingleAsync(p => p.OwnerUserId == someoneElse)).OrganizationId.Should().BeNull();
        (await db.AuditEvents.Where(e => e.OwnerUserId == someoneElse).Select(e => e.OrganizationId).ToListAsync())
            .Should().OnlyContain(o => o == null);
    }
}
