using ACI.Domain.Entities;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ACI.WebApi.Tests;

/// <summary>
/// While an import runs, what it adds carries its batch id, whoever adds it.
/// </summary>
/// <remarks>
/// A lead brought in by an import is given a contact, and a company named after its
/// email domain, by code that knows nothing of imports. Undoing the import has to find
/// those too, or it would leave them behind.
/// </remarks>
public class ImportBatchStampTests
{
    [Fact]
    public async Task RecordsAddedDuringAnImportCarryItsIdAndRecordsChangedDoNot()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        // Every workspace: this checks what a save stamps, wherever the rows belong.
        await using var db = SqliteDb.OpenEveryWorkspace(connection);
        var user = new User { Id = Guid.NewGuid(), Name = "Kia", Email = "kia@example.ch", CreatedAtUtc = DateTime.UtcNow };
        var existing = new Company { Id = Guid.NewGuid(), UserId = user.Id, Name = "Already here", CreatedAtUtc = DateTime.UtcNow };
        db.AddRange(user, existing);
        await db.SaveChangesAsync();

        var batch = Guid.NewGuid();
        db.ImportBatchId = batch;
        var lead = new Lead { Id = Guid.NewGuid(), UserId = user.Id, Name = "Jean", Email = "jean@example.ch", CreatedAtUtc = DateTime.UtcNow };
        var contact = new Contact { Id = Guid.NewGuid(), UserId = user.Id, Name = "Jean", Email = "jean@example.ch", CreatedAtUtc = DateTime.UtcNow };
        var company = new Company { Id = Guid.NewGuid(), UserId = user.Id, Name = "Example", CreatedAtUtc = DateTime.UtcNow };
        db.AddRange(lead, contact, company);
        existing.Industry = "Hospitality";
        await db.SaveChangesAsync();

        db.ImportBatchId = null;
        var afterwards = new Lead { Id = Guid.NewGuid(), UserId = user.Id, Name = "Anne", Email = "anne@example.ch", CreatedAtUtc = DateTime.UtcNow };
        db.Leads.Add(afterwards);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        (await db.Leads.SingleAsync(l => l.Id == lead.Id)).ImportBatchId.Should().Be(batch);
        (await db.Contacts.SingleAsync(c => c.Id == contact.Id)).ImportBatchId.Should().Be(batch);
        (await db.Companies.SingleAsync(c => c.Id == company.Id)).ImportBatchId.Should().Be(batch);
        (await db.Companies.SingleAsync(c => c.Id == existing.Id)).ImportBatchId
            .Should().BeNull("a record the import changes was already there, and undoing the import must not remove it");
        (await db.Leads.SingleAsync(l => l.Id == afterwards.Id)).ImportBatchId.Should().BeNull();
    }
}
