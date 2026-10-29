using ACI.Application.Common;
using ACI.Domain.Entities;
using ACI.Infrastructure.Persistence;
using ACI.Infrastructure.Repositories;
using ACI.WebApi.Tests.Helpers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ACI.WebApi.Tests;

/// <summary>
/// A club record saved from an older read is refused, not written over the newer one.
/// </summary>
/// <remarks>
/// Charges, gift cards and tabs are JSON documents, and saving one used to overwrite
/// whatever was there. Two requests that both read a gift card with 50 left both wrote
/// back 0, and 100 was spent. These pin the version check on both providers the tests
/// use: the in-memory one the HTTP tests run on, and SQLite, a real relational database
/// whose UPDATE carries the same WHERE clause SQL Server's does.
/// </remarks>
public class ClubDocumentConcurrencyTests
{
    private static async Task SecondWriterIsRefusedAsync(Func<AppDbContext> open)
    {
        var org = Guid.NewGuid();
        var id = Guid.NewGuid();
        await using (var seed = open())
        {
            await new ClubDocumentRepository(seed).SaveAsync(new ClubDocument
            {
                Id = id, OrganizationId = org, Kind = "gift-cards", Payload = "{\"remainingValue\":50}", UpdatedAtUtc = DateTime.UtcNow,
            });
        }

        await using var first = open();
        await using var second = open();
        var firstRepo = new ClubDocumentRepository(first);
        var secondRepo = new ClubDocumentRepository(second);
        // Both read the card before either writes, as two terminals do.
        (await firstRepo.GetAsync(id, org, "gift-cards")).Should().NotBeNull();
        (await secondRepo.GetAsync(id, org, "gift-cards")).Should().NotBeNull();

        await firstRepo.SaveAsync(new ClubDocument { Id = id, OrganizationId = org, Kind = "gift-cards", Payload = "{\"remainingValue\":0}", UpdatedAtUtc = DateTime.UtcNow });
        var late = () => secondRepo.SaveAsync(new ClubDocument { Id = id, OrganizationId = org, Kind = "gift-cards", Payload = "{\"remainingValue\":0,\"late\":true}", UpdatedAtUtc = DateTime.UtcNow });

        await late.Should().ThrowAsync<ConcurrencyConflictException>("the card changed after this request read it");
        await using var check = open();
        (await check.ClubDocuments.AsNoTracking().SingleAsync(d => d.Id == id)).Payload.Should().Be("{\"remainingValue\":0}");
    }

    [Fact]
    public async Task ASaveFromAnOlderReadIsRefused_InMemory()
    {
        var name = $"club-docs-{Guid.NewGuid()}";
        await SecondWriterIsRefusedAsync(() => new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options));
    }

    [Fact]
    public async Task ASaveFromAnOlderReadIsRefused_Relational()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await SecondWriterIsRefusedAsync(() => SqliteDb.Open(connection));
    }
}
